using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>单个效果在 **Preflight（预计执行）** 阶段的结论。</summary>
    public enum EventEffectPreflightState
    {
        Pending = 0,
        Ok = 1,

        /// <summary>预计执行后违反业务不变量（例如资源 / 补给变负）——整笔事务必须停止，零 mutation。</summary>
        Failed = 2
    }

    /// <summary>
    /// Effect Plan 中的一条（= 一个待执行效果 + 它的 preflight 结论）。
    ///
    /// 数值型效果额外带 projected before/after：这是「前序效果可以影响后序效果判断」的可诊断证据（§10 / C12）。
    /// </summary>
    public sealed class EventEffectPlanEntry
    {
        public EventEffectDefinition Effect { get; internal set; }
        public string EffectId { get; internal set; }
        public string EffectType { get; internal set; }
        public int Order { get; internal set; }
        public bool IsTerminal { get; internal set; }

        public string TargetRoleId { get; internal set; }
        public string TargetKey { get; internal set; }
        public bool HasIntValue { get; internal set; }
        public int IntValue { get; internal set; }
        public string StringValue { get; internal set; }

        /// <summary>GrantPersonRest 解析出的具体人物（其余效果为 null）。</summary>
        public string TargetPersonId { get; internal set; }

        public EventEffectPreflightState Preflight { get; internal set; }
        public string PreflightReason { get; internal set; }

        /// <summary>数值型效果：预计执行前的值（Village Resource / Supply）。</summary>
        public int ProjectedBefore { get; internal set; }

        /// <summary>数值型效果：预计执行后的值。</summary>
        public int ProjectedAfter { get; internal set; }

        /// <summary>是否带有 projected 数值（只有 VillageResourceDelta / SupplyDelta 有）。</summary>
        public bool HasProjection { get; internal set; }

        /// <summary>Commit 阶段是否已真实执行（Preflight 失败 / 提前中断的效果保持 false）。</summary>
        public bool Committed { get; internal set; }

        /// <summary>诊断用短描述（§23 的 Trace 形状）。</summary>
        public string Describe()
        {
            string text = Order.ToString(CultureInfo.InvariantCulture) + " " + EffectType;
            if (!string.IsNullOrEmpty(TargetKey)) text += " " + TargetKey;
            if (EffectType == EventEffectTypes.VillageResourceDelta
                || EffectType == EventEffectTypes.SupplyDelta)
            {
                text += " " + (IntValue >= 0 ? "+" : string.Empty) + IntValue.ToString(CultureInfo.InvariantCulture);
            }
            else if (EffectType == EventEffectTypes.AdvanceExpeditionTime)
            {
                text += " " + IntValue.ToString(CultureInfo.InvariantCulture) + " days";
            }
            else if (EffectType == EventEffectTypes.GrantPersonRest && !string.IsNullOrEmpty(TargetPersonId))
            {
                text += " person=" + TargetPersonId;
            }
            return text;
        }

        public override string ToString()
        {
            return Describe() + " : " + Preflight;
        }
    }

    /// <summary>
    /// **Event Effect Plan** —— 一个 Outcome 的全部效果（按 Order 规范排序）+ preflight 结论（§10）。
    ///
    /// 语义：Plan 是「先对整个有序 Effect Sequence 做预计执行」的产物；
    /// 只有 <see cref="AllPreflightPassed"/> 为 true 才允许进入 Commit（真实 mutation）。
    /// </summary>
    public sealed class EventEffectPlan
    {
        public string OutcomeId { get; internal set; }

        private readonly List<EventEffectPlanEntry> _entries = new List<EventEffectPlanEntry>();

        public IReadOnlyList<EventEffectPlanEntry> Entries { get { return _entries; } }

        public bool IsEmpty { get { return _entries.Count == 0; } }

        public bool HasTerminalEffect { get; internal set; }

        public bool AllPreflightPassed { get; internal set; }

        public string FirstFailureReason { get; internal set; }

        internal void Add(EventEffectPlanEntry entry)
        {
            if (entry != null) _entries.Add(entry);
        }

        internal void Evaluate()
        {
            AllPreflightPassed = true;
            FirstFailureReason = string.Empty;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Preflight != EventEffectPreflightState.Failed) continue;

                AllPreflightPassed = false;
                if (string.IsNullOrEmpty(FirstFailureReason))
                {
                    FirstFailureReason = "效果 " + _entries[i].Describe() + " 的 Preflight 失败："
                        + _entries[i].PreflightReason;
                }
            }
        }
    }
}

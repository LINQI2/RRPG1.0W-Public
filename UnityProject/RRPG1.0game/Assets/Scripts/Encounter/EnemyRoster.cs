using System.Collections.Generic;

namespace RRPG.Encounter
{
    /// <summary>敌方 Board 的排（与 Battle 侧 BattleRow 的数值语义一致：0=Front / 1=Middle / 2=Back）。</summary>
    public enum EnemyRosterRow
    {
        Front = 0,
        Middle = 1,
        Back = 2
    }

    /// <summary>敌方 Board 的列（与 BattleFormationPlan.ColumnIndexForSlot 的数值语义一致：0=Left / 1=Center / 2=Right）。</summary>
    public enum EnemyRosterColumn
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    /// <summary>
    /// 一个**敌人实例**（WP-Encounter-1B）。
    ///
    /// 身份是两个不同的概念：
    ///   - <see cref="EnemyInstanceId"/> = 本场阵容内的**实例身份**（同种怪多只时互不相同，可复现，不是长期 Person 身份）；
    ///   - <see cref="MonsterId"/> = **怪物模板身份**（同种怪多只时相同）。
    ///
    /// 刻意**不**复制静态配置里的事实（HP / Attack / Sprite / ThreatCost …）：
    /// 这些仍由 MonsterId 走正式 Monster Definition / Registry 解析（避免双 Authority）。
    /// <see cref="SlotId"/> 只作为来源信息（哪个组合需求位生成了它），便于诊断与后续 1C。
    /// </summary>
    public sealed class GeneratedEnemyEntry
    {
        public string EnemyInstanceId;
        public string MonsterId;
        public EnemyRosterRow Row;
        public EnemyRosterColumn Column;

        /// <summary>来源组合需求位（诊断 / 溯源用；不是身份，也不复制静态配置）。</summary>
        public string SlotId;

        public override string ToString()
        {
            return EnemyInstanceId + " = " + MonsterId + " @" + Row + "/" + Column + "（slot=" + SlotId + "）";
        }
    }

    /// <summary>
    /// 本场**敌方阵容**（EnemyRoster）：Generator 的唯一正式输出（纯逻辑数据，不接 Battle / Prep）。
    ///
    /// 不变量（由 Generator 保证，并被回归测试守护）：
    ///   - <see cref="Entries"/> 内 <c>EnemyInstanceId</c> 唯一；
    ///   - 每个 (Row, Column) 最多 1 只；每排 ≤ 3；总数 ≤ 9（3 排 × 3 列）；
    ///   - <see cref="TotalThreat"/> 落在该 Encounter 的 ThreatBudgetMin..Max 之间；
    ///   - 同 Seed + 同配置 → 完全相同（含 InstanceId 与顺序）。
    /// </summary>
    public sealed class EnemyRoster
    {
        public string EncounterId;

        /// <summary>来源遭遇池（从 Encounter 直接生成时为空）。</summary>
        public string PoolId;

        /// <summary>本次生成使用的随机种子（回放 / 复现已足够，配合静态配置与深度）。</summary>
        public int Seed;

        /// <summary>本场总威胁值 = 所有敌人 ThreatCost 之和（必须在预算区间内）。</summary>
        public int TotalThreat;

        /// <summary>敌人实例列表（顺序 = 稳定 Canonical 顺序：SlotId → 实例序号）。</summary>
        public IReadOnlyList<GeneratedEnemyEntry> Entries;

        public int Count
        {
            get { return Entries == null ? 0 : Entries.Count; }
        }

        public override string ToString()
        {
            return "EnemyRoster[" + EncounterId + (string.IsNullOrEmpty(PoolId) ? "" : " via " + PoolId)
                + " seed=" + Seed + " threat=" + TotalThreat + " count=" + Count + "]";
        }
    }
}

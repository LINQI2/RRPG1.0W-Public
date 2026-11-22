using System;
using System.Collections.Generic;
using System.Text;

namespace RRPG.Encounter
{
    /// <summary>
    /// Encounter 生成失败原因（WP-Encounter-1B §18）。
    ///
    /// 设计原则：**「配置无法生成」是正常结果，不是异常崩溃** —— 调用方必须能区分
    /// 「池不存在」/「就是没有合法解」/「搜索上限耗尽」这三类完全不同的情况。
    /// </summary>
    public enum EncounterGenerationFailure
    {
        None = 0,

        /// <summary>静态配置不可用（Root / Encounter 库 / Monster 库缺失）—— 环境问题，不是配置内容问题。</summary>
        ConfigUnavailable = 1,

        /// <summary>PoolId 不存在。</summary>
        PoolNotFound = 2,

        /// <summary>Pool.Enabled = false。</summary>
        PoolDisabled = 3,

        /// <summary>当前 Depth 下没有任何合法 PoolEntry（含引用不存在 / Encounter 未启用被过滤）。</summary>
        NoCandidateEncounter = 4,

        /// <summary>EncounterId 不存在。</summary>
        EncounterNotFound = 5,

        /// <summary>Encounter.Enabled = false（未被启用，不参与生成）。</summary>
        EncounterDisabled = 6,

        /// <summary>该遭遇没有任何组合需求位（无法表达敌人数）。</summary>
        NoSlotConfigured = 7,

        /// <summary>某个 Slot 没有任何合法候选怪物（缺 EncounterProfile / 被规则筛空 / 固定怪不合法）。</summary>
        NoLegalMonster = 8,

        /// <summary>AllowDuplicateMonster = false 导致无解（跨 Slot 也要保持 MonsterId 唯一）。</summary>
        DuplicateConflict = 9,

        /// <summary>无法让 TotalThreat 落进 ThreatBudgetMin..Max。</summary>
        ThreatBudgetUnsolvable = 10,

        /// <summary>无法满足 3×3 阵型规则（含 Exact 精确列 / 每排人数 / 列结构）。</summary>
        FormationUnsolvable = 11,

        /// <summary>池内所有候选遭遇都无法生成（诊断里逐个列出原因）。</summary>
        AllCandidatesFailed = 12,

        /// <summary>搜索上限耗尽（**不是**「无解」：只说明在给定上限内没找到解）。</summary>
        SearchLimitExhausted = 13
    }

    /// <summary>
    /// 生成结果（成功或明确失败 + 可操作诊断）。工厂方法保证：失败时绝不返回残缺的 EnemyRoster。
    /// </summary>
    public sealed class EncounterGenerationResult
    {
        /// <summary>生成成功时为非 null 的完整合法阵容。</summary>
        public EnemyRoster Roster { get; private set; }

        public EncounterGenerationFailure Failure { get; private set; }

        /// <summary>中文可操作诊断（成功时为空字符串；失败时必须说明原因与下一步）。</summary>
        public string Diagnostic { get; private set; }

        /// <summary>池生成时：逐个候选遭遇的尝试结果（成功项不列入），便于定位「为什么这个池生成不出来」。</summary>
        public IReadOnlyList<string> Attempts { get; private set; }

        public bool Success
        {
            get { return Failure == EncounterGenerationFailure.None && Roster != null; }
        }

        private EncounterGenerationResult() { }

        public static EncounterGenerationResult Ok(EnemyRoster roster)
        {
            return new EncounterGenerationResult
            {
                Roster = roster,
                Failure = EncounterGenerationFailure.None,
                Diagnostic = string.Empty,
                Attempts = new List<string>()
            };
        }

        public static EncounterGenerationResult Fail(EncounterGenerationFailure failure, string diagnostic,
            IReadOnlyList<string> attempts = null)
        {
            if (failure == EncounterGenerationFailure.None)
            {
                throw new ArgumentException("失败结果不得使用 None；成功的唯一构造入口是 Ok()。", "failure");
            }

            return new EncounterGenerationResult
            {
                Roster = null,
                Failure = failure,
                Diagnostic = diagnostic ?? string.Empty,
                Attempts = attempts ?? new List<string>()
            };
        }

        public override string ToString()
        {
            if (Success) return "OK " + Roster;
            return "FAILED " + Failure + "：" + Diagnostic;
        }
    }
}

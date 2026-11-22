using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RRPG.StaticConfig;

namespace RRPG.Encounter
{
    /// <summary>
    /// WP-Encounter-1B：**遭遇生成器**（纯逻辑、可复现、可测试）。
    ///
    /// 输入：EncounterPool + Depth + Seed（或直接给 EncounterId + Seed）
    /// 输出：<see cref="EnemyRoster"/>（本场敌方阵容）或**明确失败 + 可操作诊断**
    ///
    /// 它做什么：条件筛选 → 数量组合 → 怪物选择 → Threat 约束 → 排 / 列站位求解 → EnemyInstanceId。
    /// 它不做什么（1C 之前）：不接 BattleUI / BattleSystem / Prep / Journey / Expedition，
    /// 不创建 BattleUnit，不做 Enemy Intent，不写 Save，不碰 Scene / Prefab / 视觉。
    ///
    /// 三条硬性质（回归测试守护）：
    ///   1. **确定性**：同静态配置 + 同 PoolId + 同 Depth + 同 Seed → 完全相同 EnemyRoster
    ///      （含 MonsterId / Count / Row / Column / EnemyInstanceId / TotalThreat / 顺序）；
    ///   2. **求解而非碰运气**：Count、Monster、Row 都属于求解空间（确定性候选顺序 + 回溯 + 剪枝）；
    ///   3. **明确失败**：无解 / 超搜索上限分别返回不同失败码，绝不返回残缺阵容、绝不放松规则。
    /// </summary>
    public sealed class EncounterGenerator
    {
        /// <summary>
        /// 搜索节点上限（Anti-Hang）。耗尽时返回 <see cref="EncounterGenerationFailure.SearchLimitExhausted"/>，
        /// **不**伪装成「无合法解」。
        /// </summary>
        public const int DefaultSearchNodeLimit = 200000;

        /// <summary>EnemyInstanceId 前缀（格式属实现细节：阵容内唯一、同 Seed 稳定、非 Guid）。</summary>
        public const string EnemyInstanceIdPrefix = "eid_";

        private readonly EncounterGenerationInput _input;
        private readonly int _searchNodeLimit;
        private int _nodes;

        private readonly Dictionary<string, EncounterDefinition> _encounterById =
            new Dictionary<string, EncounterDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EncounterSlotDefinition>> _slotsByEncounterId =
            new Dictionary<string, List<EncounterSlotDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EncounterSlotRuleDefinition>> _rulesBySlotId =
            new Dictionary<string, List<EncounterSlotRuleDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, EncounterPoolDefinition> _poolById =
            new Dictionary<string, EncounterPoolDefinition>(StringComparer.Ordinal);
        private readonly List<EncounterPoolEntryDefinition> _poolEntries = new List<EncounterPoolEntryDefinition>();
        private readonly Dictionary<string, MonsterEncounterProfileDefinition> _profileByMonsterId =
            new Dictionary<string, MonsterEncounterProfileDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _rolesByMonsterId =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>合法怪物集合 = 同时拥有 MonsterDefinition 与 MonsterEncounterProfile 的 MonsterId。</summary>
        private readonly HashSet<string> _legalMonsterIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>合法怪物集合的 Canonical 顺序（MonsterId 升序）：候选枚举必须先排序，再进随机过程。</summary>
        private readonly List<string> _legalMonsterIdsSorted = new List<string>();

        /// <summary>位置求解缓存（同一 Count 向量 → 同一结果；避免重复搜索，且结果与搜索顺序无关）。</summary>
        private readonly Dictionary<string, List<PositionSlot>> _positionCache =
            new Dictionary<string, List<PositionSlot>>(StringComparer.Ordinal);

        public EncounterGenerator(EncounterGenerationInput input)
            : this(input, DefaultSearchNodeLimit)
        {
        }

        public EncounterGenerator(EncounterGenerationInput input, int searchNodeLimit)
        {
            _input = input;
            _searchNodeLimit = searchNodeLimit <= 0 ? DefaultSearchNodeLimit : searchNodeLimit;
            Index();
        }

        /// <summary>
        /// 生产路径：从正式只读静态配置建立生成器。失败返回 null + 可操作诊断（不回退、不猜）。
        /// </summary>
        public static EncounterGenerator FromStaticConfig(out string diagnostic)
        {
            var input = EncounterGenerationInput.FromStaticConfig(out diagnostic);
            if (input == null) return null;
            return new EncounterGenerator(input);
        }

        // ---------------- 正式入口 ----------------

        /// <summary>
        /// 从遭遇池生成：PoolId + Depth + Seed。
        ///
        /// 池内规则（§6）：按 Weight + Seed 形成**确定性的候选遭遇尝试顺序**；
        /// 某个遭遇无法生成时**不放松它的规则**，而是继续尝试池内下一个合法遭遇；
        /// 全部候选都失败 → 返回 <see cref="EncounterGenerationFailure.AllCandidatesFailed"/> 并逐个说明原因。
        /// </summary>
        public EncounterGenerationResult GenerateFromPool(string poolId, int depth, int seed)
        {
            if (_input == null)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.ConfigUnavailable,
                    "Encounter 静态配置不可用：Generator 未获得输入快照（请先运行 RRPG_一键导表.cmd）。");
            }

            if (string.IsNullOrEmpty(poolId))
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.PoolNotFound,
                    "PoolId 为空：无法从遭遇池生成。");
            }

            EncounterPoolDefinition pool;
            if (!_poolById.TryGetValue(poolId, out pool))
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.PoolNotFound,
                    "遭遇池不存在：" + poolId
                    + "（请检查 Development/Config/RRPG_Encounters.xlsx 的 EncounterPool 表）。");
            }

            if (!pool.Enabled)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.PoolDisabled,
                    "遭遇池未启用（Enabled = false）：" + poolId + "（未启用的池不参与生成）。");
            }

            var candidates = CollectPoolCandidates(poolId, depth, out string excluded);
            if (candidates.Count == 0)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.NoCandidateEncounter,
                    "遭遇池 '" + poolId + "' 在深度 " + depth + " 没有可用候选遭遇：" + excluded + "。");
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.EncounterId, b.EncounterId));
            var weights = new List<int>(candidates.Count);
            foreach (var entry in candidates) weights.Add(entry.Weight);

            var poolRng = new DeterministicRandom(
                DeriveSeed(seed, "pool|" + poolId + "|depth|" + depth.ToString(CultureInfo.InvariantCulture)));
            var order = poolRng.WeightedPermutation(candidates, weights);

            var attempts = new List<string>();
            foreach (var entry in order)
            {
                var result = SolveEncounter(entry.EncounterId, poolId, seed);
                if (result.Success) return result;

                attempts.Add(entry.EncounterId + " → " + result.Failure + "：" + result.Diagnostic);
            }

            var builder = new StringBuilder();
            builder.Append("遭遇池 '").Append(poolId).Append("' 在深度 ").Append(depth)
                .Append(" 的 ").Append(order.Count).Append(" 个候选遭遇都无法生成：");
            for (int i = 0; i < attempts.Count; i++) builder.Append("\n  · ").Append(attempts[i]);

            return EncounterGenerationResult.Fail(EncounterGenerationFailure.AllCandidatesFailed,
                builder.ToString(), attempts);
        }

        /// <summary>
        /// 从指定遭遇模板生成（单模板测试 / 剧情特殊遭遇 / 池生成内部复用）。PoolId 为空。
        /// </summary>
        public EncounterGenerationResult GenerateEncounter(string encounterId, int seed)
        {
            return SolveEncounter(encounterId, null, seed);
        }

        // ---------------- 建索引（全部使用 Ordinal 比较 + 显式排序；不依赖字典遍历顺序） ----------------

        private void Index()
        {
            if (_input == null) return;

            var withDefinition = new HashSet<string>(StringComparer.Ordinal);
            foreach (var monster in Safe(_input.Monsters))
            {
                if (monster != null && !string.IsNullOrEmpty(monster.MonsterId)) withDefinition.Add(monster.MonsterId);
            }

            foreach (var profile in Safe(_input.MonsterProfiles))
            {
                if (profile == null || string.IsNullOrEmpty(profile.MonsterId)) continue;

                if (!_profileByMonsterId.ContainsKey(profile.MonsterId)) _profileByMonsterId.Add(profile.MonsterId, profile);

                // §8：没有 EncounterProfile 的怪物不进入随机候选；没有 MonsterDefinition 的也不进入。
                if (withDefinition.Contains(profile.MonsterId)) _legalMonsterIds.Add(profile.MonsterId);
            }

            foreach (var monsterId in _legalMonsterIds) _legalMonsterIdsSorted.Add(monsterId);
            _legalMonsterIdsSorted.Sort(string.CompareOrdinal);

            foreach (var role in Safe(_input.MonsterRoles))
            {
                if (role == null || string.IsNullOrEmpty(role.MonsterId) || string.IsNullOrEmpty(role.Role)) continue;

                List<string> roles;
                if (!_rolesByMonsterId.TryGetValue(role.MonsterId, out roles))
                {
                    roles = new List<string>();
                    _rolesByMonsterId.Add(role.MonsterId, roles);
                }
                if (!roles.Contains(role.Role)) roles.Add(role.Role);
            }
            foreach (var pair in _rolesByMonsterId) pair.Value.Sort(string.CompareOrdinal);

            foreach (var encounter in Safe(_input.Encounters))
            {
                if (encounter == null || string.IsNullOrEmpty(encounter.EncounterId)) continue;
                if (!_encounterById.ContainsKey(encounter.EncounterId)) _encounterById.Add(encounter.EncounterId, encounter);
            }

            foreach (var slot in Safe(_input.Slots))
            {
                if (slot == null || string.IsNullOrEmpty(slot.SlotId) || string.IsNullOrEmpty(slot.EncounterId)) continue;

                List<EncounterSlotDefinition> list;
                if (!_slotsByEncounterId.TryGetValue(slot.EncounterId, out list))
                {
                    list = new List<EncounterSlotDefinition>();
                    _slotsByEncounterId.Add(slot.EncounterId, list);
                }
                list.Add(slot);
            }
            foreach (var pair in _slotsByEncounterId)
            {
                pair.Value.Sort((a, b) => string.CompareOrdinal(a.SlotId, b.SlotId));   // Canonical 顺序
            }

            foreach (var rule in Safe(_input.SlotRules))
            {
                if (rule == null || string.IsNullOrEmpty(rule.SlotId)) continue;

                List<EncounterSlotRuleDefinition> list;
                if (!_rulesBySlotId.TryGetValue(rule.SlotId, out list))
                {
                    list = new List<EncounterSlotRuleDefinition>();
                    _rulesBySlotId.Add(rule.SlotId, list);
                }
                list.Add(rule);
            }

            foreach (var pool in Safe(_input.Pools))
            {
                if (pool == null || string.IsNullOrEmpty(pool.PoolId)) continue;
                if (!_poolById.ContainsKey(pool.PoolId)) _poolById.Add(pool.PoolId, pool);
            }

            foreach (var entry in Safe(_input.PoolEntries))
            {
                if (entry == null || string.IsNullOrEmpty(entry.PoolId) || string.IsNullOrEmpty(entry.EncounterId)) continue;
                _poolEntries.Add(entry);
            }
        }

        /// <summary>池内候选（Depth inclusive + Encounter 启用）；淘汰原因写入 excludedSummary。</summary>
        private List<EncounterPoolEntryDefinition> CollectPoolCandidates(string poolId, int depth, out string excludedSummary)
        {
            var candidates = new List<EncounterPoolEntryDefinition>();
            var excluded = new List<string>();

            foreach (var entry in _poolEntries)
            {
                if (!string.Equals(entry.PoolId, poolId, StringComparison.Ordinal)) continue;

                if (depth < entry.MinDepth || depth > entry.MaxDepth)
                {
                    excluded.Add(entry.EncounterId + "（深度 " + depth + " 不在 " + entry.MinDepth + "~" + entry.MaxDepth + "）");
                    continue;
                }

                EncounterDefinition encounter;
                if (!_encounterById.TryGetValue(entry.EncounterId, out encounter))
                {
                    excluded.Add(entry.EncounterId + "（EncounterId 不存在）");
                    continue;
                }

                if (!encounter.Enabled)
                {
                    excluded.Add(entry.EncounterId + "（Encounter 未启用）");
                    continue;
                }

                candidates.Add(entry);
            }

            excludedSummary = excluded.Count == 0 ? "该池没有任何条目" : string.Join("、", excluded);
            return candidates;
        }

        private static ulong DeriveSeed(int seed, string purpose)
        {
            return DeterministicRandom.StableHash64(
                seed.ToString(CultureInfo.InvariantCulture) + "|" + purpose);
        }

        private static IReadOnlyList<T> Safe<T>(IReadOnlyList<T> list)
        {
            return list ?? (IReadOnlyList<T>)new List<T>();
        }

        // ---------------- 内部模型（全部为私有嵌套类型；不对外暴露实现细节） ----------------

        private sealed class Candidate
        {
            public string MonsterId;
            public int ThreatCost;
            public int Weight;
        }

        private sealed class SlotPlan
        {
            public EncounterSlotDefinition Slot;
            public List<Candidate> Candidates = new List<Candidate>();
            public List<int> CountOrder = new List<int>();
            public List<List<Candidate>> InstanceOrders = new List<List<Candidate>>();
            public int MinThreat;
            public int MaxThreat;
        }

        private sealed class InstanceSpec
        {
            public SlotPlan Plan;
            public int InstanceIndex;
        }

        private sealed class PositionSlot
        {
            public EnemyRosterRow Row;

            /// <summary>列索引（0=Left / 1=Center / 2=Right）；-1 = 尚未确定（排内列填充阶段决定）。</summary>
            public int ColumnIndex = -1;

            /// <summary>该单位是否来自 Exact 组合需求位（列由配置固定，不可被排内填充改写）。</summary>
            public bool IsExact;
        }

        private sealed class Solution
        {
            public List<PositionSlot> Positions;
            public List<string> MonsterIds;
            public List<InstanceSpec> Instances;
            public int Threat;
        }

        private sealed class ReasonTracker
        {
            public bool NoLegalMonster;
            public bool DuplicateConflict;
            public bool ThreatFailure;
            public bool FormationFailure;
            public bool EmptyRoster;
            public string Detail;
        }

        private bool _limitHit;
        private Solution _solution;

        // ---------------- 计划：每个 Slot 的候选怪物 + 数量顺序（确定性） ----------------

        private List<SlotPlan> BuildPlans(string encounterId, List<EncounterSlotDefinition> slots, int seed)
        {
            var plans = new List<SlotPlan>(slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var plan = new SlotPlan { Slot = slot, Candidates = FilterCandidates(slot) };

                if (plan.Candidates.Count > 0)
                {
                    plan.MinThreat = int.MaxValue;
                    plan.MaxThreat = int.MinValue;
                    foreach (var candidate in plan.Candidates)
                    {
                        if (candidate.ThreatCost < plan.MinThreat) plan.MinThreat = candidate.ThreatCost;
                        if (candidate.ThreatCost > plan.MaxThreat) plan.MaxThreat = candidate.ThreatCost;
                    }
                }

                // Count 属于求解空间：先给出一个由 Seed 决定的探索顺序（不是固定 Min 或 Max）
                var countRng = new DeterministicRandom(
                    DeriveSeed(seed, "count|" + encounterId + "|" + slot.SlotId));
                plan.CountOrder = countRng.RangePermutation(slot.CountMin, slot.CountMax);

                // 每个实例各有一条独立的加权候选顺序（权重参与选择；且所有候选都可被回溯探索）
                int orders = Math.Max(1, Math.Max(0, slot.CountMax));
                for (int k = 0; k < orders; k++)
                {
                    var instanceRng = new DeterministicRandom(
                        DeriveSeed(seed, "monster|" + encounterId + "|" + slot.SlotId + "|" + k.ToString(CultureInfo.InvariantCulture)));

                    var ids = new List<Candidate>(plan.Candidates);
                    var weights = new List<int>(plan.Candidates.Count);
                    foreach (var candidate in plan.Candidates) weights.Add(candidate.Weight);

                    plan.InstanceOrders.Add(instanceRng.WeightedPermutation(ids, weights));
                }

                plans.Add(plan);
            }

            return plans;
        }

        /// <summary>
        /// 该 Slot 的合法候选怪物（Canonical 顺序 = MonsterId 升序）。
        ///
        /// Query（§8）：从**同时拥有 MonsterDefinition 与 MonsterEncounterProfile** 的怪物中按规则筛选；
        /// 没有任何规则 = 全部候选都可用（§8.5）。
        /// FixedMonster（§9）：固定怪也必须存在 + 有 EncounterProfile + **通过该 Slot 的规则**
        /// （「Fixed 不代表可以绕过其它 Encounter 规则」），否则该 Slot 无候选 → 明确失败。
        /// </summary>
        private List<Candidate> FilterCandidates(EncounterSlotDefinition slot)
        {
            var result = new List<Candidate>();

            if (IsFixedMonster(slot))
            {
                string fixedId = slot.FixedMonsterId;
                if (string.IsNullOrEmpty(fixedId) || !_legalMonsterIds.Contains(fixedId)) return result;
                if (!PassesRules(slot.SlotId, fixedId)) return result;

                result.Add(ToCandidate(fixedId));
                return result;
            }

            foreach (var monsterId in _legalMonsterIdsSorted)
            {
                if (!PassesRules(slot.SlotId, monsterId)) continue;
                result.Add(ToCandidate(monsterId));
            }
            return result;
        }

        private Candidate ToCandidate(string monsterId)
        {
            var profile = _profileByMonsterId[monsterId];
            return new Candidate
            {
                MonsterId = monsterId,
                ThreatCost = profile.ThreatCost,
                Weight = profile.RandomWeight
            };
        }

        private static bool IsFixedMonster(EncounterSlotDefinition slot)
        {
            return string.Equals(slot.SelectionMode, "FixedMonster", StringComparison.Ordinal);
        }

        // ---------------- §8 筛选规则语义（AnyOf / Require / Exclude × Role / Tier / MonsterId） ----------------

        /// <summary>
        /// 判定一个 Monster 是否通过某组合需求位的筛选规则。
        ///
        /// 正式语义（§8）：
        ///   - <b>Exclude</b>：命中任何一个 Exclude → 直接排除（优先于其它规则）；
        ///   - <b>Require</b>：所有 Require 条件都必须满足；同一 Dimension 写多条 = 必须**同时**满足
        ///     （例如 Role=Melee + Role=Tank → 该怪必须同时具备两个定位，MonsterRole 支持一怪多行）；
        ///   - <b>AnyOf</b>：按 Dimension 分组；每个「存在 AnyOf 的 Dimension」都必须至少命中一条
        ///     （同 Dimension 内 = OR；不同 Dimension 之间 = AND）；
        ///   - 该 Slot **没有任何规则** → 全部候选可用（§8.5，不是错误）。
        /// </summary>
        private bool PassesRules(string slotId, string monsterId)
        {
            List<EncounterSlotRuleDefinition> rules;
            if (!_rulesBySlotId.TryGetValue(slotId, out rules) || rules.Count == 0) return true;

            for (int i = 0; i < rules.Count; i++)
            {
                if (IsMode(rules[i], "Exclude") && Matches(rules[i], monsterId)) return false;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                if (IsMode(rules[i], "Require") && !Matches(rules[i], monsterId)) return false;
            }

            var anyOfDimensions = new List<string>();
            for (int i = 0; i < rules.Count; i++)
            {
                if (!IsMode(rules[i], "AnyOf")) continue;

                string dimension = rules[i].Dimension ?? string.Empty;
                if (!anyOfDimensions.Contains(dimension)) anyOfDimensions.Add(dimension);
            }

            for (int d = 0; d < anyOfDimensions.Count; d++)
            {
                string dimension = anyOfDimensions[d];
                bool hit = false;

                for (int i = 0; i < rules.Count; i++)
                {
                    if (!IsMode(rules[i], "AnyOf")) continue;
                    if (!string.Equals(rules[i].Dimension ?? string.Empty, dimension, StringComparison.Ordinal)) continue;
                    if (!Matches(rules[i], monsterId)) continue;

                    hit = true;
                    break;
                }

                if (!hit) return false;
            }

            return true;
        }

        private static bool IsMode(EncounterSlotRuleDefinition rule, string mode)
        {
            return string.Equals(rule.RuleMode, mode, StringComparison.Ordinal);
        }

        /// <summary>单个规则是否命中该 Monster（未知 Dimension 保守判为不命中，绝不静默放行）。</summary>
        private bool Matches(EncounterSlotRuleDefinition rule, string monsterId)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Value)) return false;

            switch (rule.Dimension)
            {
                case "MonsterId":
                    return string.Equals(monsterId, rule.Value, StringComparison.Ordinal);

                case "Tier":
                {
                    MonsterEncounterProfileDefinition profile;
                    if (!_profileByMonsterId.TryGetValue(monsterId, out profile)) return false;
                    return string.Equals(profile.Tier ?? string.Empty, rule.Value, StringComparison.Ordinal);
                }

                case "Role":
                {
                    List<string> roles;
                    if (!_rolesByMonsterId.TryGetValue(monsterId, out roles)) return false;

                    for (int i = 0; i < roles.Count; i++)
                    {
                        if (string.Equals(roles[i], rule.Value, StringComparison.Ordinal)) return true;
                    }
                    return false;
                }

                default:
                    return false;   // 未知维度：Compiler 已拦；这里不做静默放行
            }
        }

        // ---------------- 求解：Count 向量 → 站位 → 怪物 / 威胁 ----------------

        /// <summary>
        /// 求解单个遭遇模板。
        ///
        /// 顺序（§15 / §16）：Count 向量（含 Exact / Row / Any 的站位可行性）→ 怪物选择 + Threat 约束。
        /// 三者都是**求解空间**：不是「随机一次不满足就 FAIL」，而是确定性候选顺序 + 回溯 + 剪枝。
        /// </summary>
        private EncounterGenerationResult SolveEncounter(string encounterId, string poolId, int seed)
        {
            if (_input == null)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.ConfigUnavailable,
                    "Encounter 静态配置不可用：Generator 未获得输入快照（请先运行 RRPG_一键导表.cmd）。");
            }

            if (string.IsNullOrEmpty(encounterId))
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.EncounterNotFound,
                    "EncounterId 为空：无法生成。");
            }

            EncounterDefinition encounter;
            if (!_encounterById.TryGetValue(encounterId, out encounter))
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.EncounterNotFound,
                    "遭遇模板不存在：" + encounterId + "。");
            }

            if (!encounter.Enabled)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.EncounterDisabled,
                    "遭遇模板未启用（Enabled = false）：" + encounterId + "（未启用的遭遇不参与生成）。");
            }

            List<EncounterSlotDefinition> slots;
            if (!_slotsByEncounterId.TryGetValue(encounterId, out slots) || slots.Count == 0)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.NoSlotConfigured,
                    "遭遇 '" + encounterId + "' 没有任何组合需求位：敌人数量的唯一来源是 EncounterSlot。");
            }

            if (encounter.ThreatBudgetMin > encounter.ThreatBudgetMax)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.ThreatBudgetUnsolvable,
                    "遭遇 '" + encounterId + "' 的威胁预算区间非法（Min " + encounter.ThreatBudgetMin
                    + " > Max " + encounter.ThreatBudgetMax + "）。");
            }

            _nodes = 0;
            _limitHit = false;
            _solution = null;
            _positionCache.Clear();

            var plans = BuildPlans(encounterId, slots, seed);

            // 快速失败：某个位可能需要 >0 只怪，但没有任何合法候选 → 这是配置内容问题
            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                if (plan.Slot.CountMax > 0 && plan.Candidates.Count == 0)
                {
                    return EncounterGenerationResult.Fail(EncounterGenerationFailure.NoLegalMonster,
                        "遭遇 '" + encounterId + "' 的组合需求位 '" + plan.Slot.SlotId
                        + "' 没有任何合法候选怪物：请检查该位的需求（规则是否把候选筛空 / 固定怪是否存在且通过规则 / "
                        + "怪物是否同时拥有 MonsterEncounterProfile 与 MonsterDefinition）。");
                }
            }

            var reasons = new ReasonTracker();
            var counts = new int[plans.Count];

            if (SearchCounts(plans, 0, counts, encounter, seed, reasons) && _solution != null)
            {
                var roster = BuildRoster(encounter, poolId, seed, _solution, reasons);
                if (roster != null) return EncounterGenerationResult.Ok(roster);
            }

            if (_limitHit)
            {
                return EncounterGenerationResult.Fail(EncounterGenerationFailure.SearchLimitExhausted,
                    "遭遇 '" + encounterId + "' 的求解在搜索上限（" + _searchNodeLimit
                    + " 节点）内没有找到解：这不代表无解，只代表上限耗尽。"
                    + "请提高搜索上限或简化该遭遇的条件组合（不要把它当成「配置无解」）。");
            }

            return EncounterGenerationResult.Fail(MapFailure(reasons), BuildFailureDiagnostic(encounter, reasons));
        }

        /// <summary>Count 向量搜索（每个 Count 组合都会先做一次**站位可行性**判定，再做怪物 / 威胁搜索）。</summary>
        private bool SearchCounts(List<SlotPlan> plans, int index, int[] counts, EncounterDefinition encounter,
            int seed, ReasonTracker reasons)
        {
            if (_limitHit) return false;
            if (++_nodes > _searchNodeLimit) { _limitHit = true; return false; }

            if (index == plans.Count)
            {
                string signature = CountSignature(plans, counts, seed);

                List<PositionSlot> positions;
                if (!_positionCache.TryGetValue(signature, out positions))
                {
                    positions = SolvePositions(plans, counts, seed, reasons);
                    _positionCache.Add(signature, positions);   // null 也缓存：同一 Count 向量不重复求解
                }

                if (positions == null) return false;

                var instances = BuildInstances(plans, counts);
                var assigned = new List<string>(instances.Count);
                var used = new HashSet<string>(StringComparer.Ordinal);
                BuildSuffixBounds(instances);

                int threat = 0;
                if (SearchMonsters(instances, 0, encounter, assigned, used, ref threat, reasons))
                {
                    _solution = new Solution
                    {
                        Positions = positions,
                        MonsterIds = new List<string>(assigned),
                        Instances = instances,
                        Threat = threat
                    };
                    return true;
                }

                return false;
            }

            foreach (int count in plans[index].CountOrder)
            {
                counts[index] = count;
                if (SearchCounts(plans, index + 1, counts, encounter, seed, reasons)) return true;
                if (_limitHit) return false;
            }

            counts[index] = 0;
            return false;
        }

        /// <summary>怪物选择 + Duplicate 约束 + Threat 预算（回溯 + 边界剪枝）。</summary>
        private bool SearchMonsters(List<InstanceSpec> instances, int index, EncounterDefinition encounter,
            List<string> assigned, HashSet<string> used, ref int threat, ReasonTracker reasons)
        {
            if (++_nodes > _searchNodeLimit) { _limitHit = true; return false; }

            if (index == instances.Count)
            {
                if (threat >= encounter.ThreatBudgetMin && threat <= encounter.ThreatBudgetMax) return true;

                reasons.ThreatFailure = true;
                if (reasons.Detail == null)
                {
                    reasons.Detail = "总威胁 " + threat + " 不在预算 " + encounter.ThreatBudgetMin
                        + "~" + encounter.ThreatBudgetMax + " 内";
                }
                return false;
            }

            var instance = instances[index];
            var order = instance.Plan.InstanceOrders[
                Math.Min(instance.InstanceIndex, instance.Plan.InstanceOrders.Count - 1)];

            bool skippedByDuplicate = false;
            bool anyTried = false;

            for (int i = 0; i < order.Count; i++)
            {
                var candidate = order[i];

                if (!encounter.AllowDuplicateMonster && used.Contains(candidate.MonsterId))
                {
                    skippedByDuplicate = true;   // §10：同一 MonsterId 在整场最多 1 次（跨 Slot 生效）
                    continue;
                }

                int nextThreat = threat + candidate.ThreatCost;

                // 剪枝（sound）：用松弛后的剩余上下界判断「即使后面全取最小 / 最大也不可能落进预算」
                if (nextThreat + _suffixMin[index + 1] > encounter.ThreatBudgetMax)
                {
                    reasons.ThreatFailure = true;
                    continue;
                }
                if (nextThreat + _suffixMax[index + 1] < encounter.ThreatBudgetMin)
                {
                    reasons.ThreatFailure = true;
                    continue;
                }

                anyTried = true;
                assigned.Add(candidate.MonsterId);
                used.Add(candidate.MonsterId);
                threat = nextThreat;

                if (SearchMonsters(instances, index + 1, encounter, assigned, used, ref threat, reasons)) return true;

                threat = nextThreat - candidate.ThreatCost;
                used.Remove(candidate.MonsterId);
                assigned.RemoveAt(assigned.Count - 1);

                if (_limitHit) return false;
            }

            if (!anyTried && skippedByDuplicate && order.Count > 0)
            {
                reasons.DuplicateConflict = true;
                if (reasons.Detail == null)
                {
                    reasons.Detail = "组合需求位 '" + instance.Plan.Slot.SlotId
                        + "' 需要第 " + (instance.InstanceIndex + 1)
                        + " 只怪，但 AllowDuplicateMonster = false 且可用怪物已被其它组合需求位用完";
                }
            }

            return false;
        }

        /// <summary>每个实例位的剩余威胁上下界（后缀和；用于剪枝）。</summary>
        private void BuildSuffixBounds(List<InstanceSpec> instances)
        {
            int n = instances.Count;
            _suffixMin = new int[n + 1];
            _suffixMax = new int[n + 1];

            for (int i = n - 1; i >= 0; i--)
            {
                _suffixMin[i] = instances[i].Plan.MinThreat + _suffixMin[i + 1];
                _suffixMax[i] = instances[i].Plan.MaxThreat + _suffixMax[i + 1];
            }
        }

        private int[] _suffixMin = new int[1];
        private int[] _suffixMax = new int[1];

        private static List<InstanceSpec> BuildInstances(List<SlotPlan> plans, int[] counts)
        {
            var instances = new List<InstanceSpec>();
            for (int p = 0; p < plans.Count; p++)
            {
                for (int k = 0; k < counts[p]; k++)
                {
                    instances.Add(new InstanceSpec { Plan = plans[p], InstanceIndex = k });
                }
            }
            return instances;
        }

        private static string CountSignature(List<SlotPlan> plans, int[] counts, int seed)
        {
            var builder = new StringBuilder();
            builder.Append(seed.ToString(CultureInfo.InvariantCulture));
            for (int p = 0; p < plans.Count; p++)
            {
                builder.Append('|').Append(plans[p].Slot.SlotId).Append('=')
                    .Append(counts[p].ToString(CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        // ---------------- 站位求解（§13 / §14 / §15：Exact → Row → Any → 排内列） ----------------

        /// <summary>
        /// 为一个 Count 向量求解合法站位。
        ///
        /// 顺序（§15，不把敌人当 9 个格子随机塞）：
        ///   1. 固定 Exact 的 Row（并记录其固定 Column）；
        ///   2. 固定 Row 模式的 Row；
        ///   3. 为 Any 单位选择 Row（Seed 决定的候选顺序 + 回溯，每排 ≤ 3）；
        ///   4. 得到最终每排人数；
        ///   5. 每排按人数确定合法 Column 集；
        ///   6. 校验 Exact 单位指定的 Column 是否仍合法（不合法 → 换其它 Any 分配，不放松 Exact）；
        ///   7. 剩余非 Exact 单位填入该排剩余合法列；
        ///   8. 最终验证：无重叠、无缺位、无非法列。
        /// </summary>
        private List<PositionSlot> SolvePositions(List<SlotPlan> plans, int[] counts, int seed, ReasonTracker reasons)
        {
            var instances = BuildInstances(plans, counts);
            int total = instances.Count;

            if (total == 0)
            {
                reasons.EmptyRoster = true;
                if (reasons.Detail == null) reasons.Detail = "该遭遇在当前配置下只能生成 0 只敌人";
                return null;
            }

            if (total > EnemyBoardFormation.MaxEnemyUnits)
            {
                reasons.FormationFailure = true;
                if (reasons.Detail == null)
                {
                    reasons.Detail = "总敌人数 " + total + " 超过敌方战场容量 "
                        + EnemyBoardFormation.MaxEnemyUnits + "（3 排 × 3 列）";
                }
                return null;
            }

            var positions = new PositionSlot[total];
            var rowCounts = new int[EnemyBoardFormation.RowCount];
            var anyIndexes = new List<int>();

            for (int i = 0; i < total; i++)
            {
                var slot = instances[i].Plan.Slot;
                var position = new PositionSlot();
                positions[i] = position;

                if (IsModeText(slot.PositionMode, "Exact"))
                {
                    EnemyRosterRow row;
                    EnemyRosterColumn column;
                    if (!TryParseRow(slot.Row, out row) || !TryParseColumn(slot.Column, out column))
                    {
                        reasons.FormationFailure = true;
                        if (reasons.Detail == null) reasons.Detail = "Exact 位的 Row / Column 非法：" + slot.SlotId;
                        return null;
                    }

                    int count = counts[PlanIndexOf(plans, instances[i].Plan)];
                    if (count != 1)
                    {
                        reasons.FormationFailure = true;
                        if (reasons.Detail == null)
                        {
                            reasons.Detail = "Exact 位必须只生成 1 只敌人（当前 " + slot.SlotId
                                + " 生成了 " + count + " 只）";
                        }
                        return null;
                    }

                    position.Row = row;
                    position.ColumnIndex = (int)column;
                    position.IsExact = true;
                    rowCounts[(int)row]++;
                    continue;
                }

                if (IsModeText(slot.PositionMode, "Row"))
                {
                    EnemyRosterRow row;
                    if (!TryParseRow(slot.Row, out row))
                    {
                        reasons.FormationFailure = true;
                        if (reasons.Detail == null) reasons.Detail = "Row 位的指定排非法：" + slot.SlotId;
                        return null;
                    }

                    position.Row = row;
                    rowCounts[(int)row]++;
                    continue;
                }

                anyIndexes.Add(i);   // Any：排由求解决定（列永远由排内人数决定）
            }

            for (int r = 0; r < rowCounts.Length; r++)
            {
                if (EnemyBoardFormation.IsLegalRowCount(rowCounts[r])) continue;

                reasons.FormationFailure = true;
                if (reasons.Detail == null)
                {
                    reasons.Detail = "第 " + r + " 排固定了 " + rowCounts[r]
                        + " 只敌人，超过每排上限 " + EnemyBoardFormation.MaxUnitsPerRow;
                }
                return null;
            }

            var rowOrders = new List<List<int>>(anyIndexes.Count);
            for (int k = 0; k < anyIndexes.Count; k++)
            {
                int i = anyIndexes[k];
                var rng = new DeterministicRandom(
                    DeriveSeed(seed, "row|" + instances[i].Plan.Slot.SlotId + "|" + instances[i].InstanceIndex));
                rowOrders.Add(rng.Permutation(EnemyBoardFormation.RowCount));
            }

            if (!AssignAnyRows(anyIndexes, rowOrders, 0, positions, rowCounts))
            {
                // 走到这里说明「Any 的排分配全部尝试完，仍无法满足排内列规则 / Exact 精确列」
                reasons.FormationFailure = true;
                if (reasons.Detail == null)
                {
                    reasons.Detail = "该 Count 组合下任何排分配都无法满足 3×3 列规则（含 Exact 精确列）";
                }
                return null;
            }

            return new List<PositionSlot>(positions);
        }

        private bool AssignAnyRows(List<int> anyIndexes, List<List<int>> rowOrders, int k,
            PositionSlot[] positions, int[] rowCounts)
        {
            if (_limitHit) return false;
            if (++_nodes > _searchNodeLimit) { _limitHit = true; return false; }

            if (k == anyIndexes.Count) return FillColumns(positions, rowCounts);

            int i = anyIndexes[k];
            var order = rowOrders[k];

            for (int o = 0; o < order.Count; o++)
            {
                int row = order[o];
                if (rowCounts[row] >= EnemyBoardFormation.MaxUnitsPerRow) continue;

                rowCounts[row]++;
                positions[i].Row = (EnemyRosterRow)row;

                if (AssignAnyRows(anyIndexes, rowOrders, k + 1, positions, rowCounts)) return true;

                rowCounts[row]--;
                if (_limitHit) return false;
            }

            return false;
        }

        /// <summary>排内列填充 + 最终验证（Exact 列必须仍合法；剩余列按升序分配给该排其它单位）。</summary>
        private static bool FillColumns(PositionSlot[] positions, int[] rowCounts)
        {
            for (int r = 0; r < rowCounts.Length; r++)
            {
                var used = new List<int>();
                var targets = new List<int>();

                for (int i = 0; i < positions.Length; i++)
                {
                    if ((int)positions[i].Row != r) continue;

                    if (positions[i].IsExact)
                    {
                        int column = positions[i].ColumnIndex;
                        if (!EnemyBoardFormation.IsLegalColumn(rowCounts[r], (EnemyRosterColumn)column)) return false;
                        if (used.Contains(column)) return false;
                        used.Add(column);
                        continue;
                    }

                    targets.Add(i);
                }

                if (rowCounts[r] == 0)
                {
                    if (used.Count > 0) return false;
                    continue;
                }

                var required = EnemyBoardFormation.RequiredColumns(rowCounts[r]);
                var remaining = new List<int>();
                for (int c = 0; c < required.Count; c++)
                {
                    int columnIndex = (int)required[c];
                    if (!used.Contains(columnIndex)) remaining.Add(columnIndex);
                }

                if (targets.Count != remaining.Count) return false;

                for (int t = 0; t < targets.Count; t++)
                {
                    positions[targets[t]].ColumnIndex = remaining[t];
                }
            }

            // 最终验证：每个单位都有列、列合法、同一排没有重叠
            for (int i = 0; i < positions.Length; i++)
            {
                int column = positions[i].ColumnIndex;
                if (column < 0) return false;
                if (!EnemyBoardFormation.IsLegalColumn(rowCounts[(int)positions[i].Row], (EnemyRosterColumn)column))
                {
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (positions[j].Row != positions[i].Row) continue;
                    if (positions[j].ColumnIndex == column) return false;
                }
            }

            return true;
        }

        private static int PlanIndexOf(List<SlotPlan> plans, SlotPlan plan)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                if (ReferenceEquals(plans[i], plan)) return i;
            }
            return 0;
        }

        private static bool IsModeText(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private static bool TryParseRow(string value, out EnemyRosterRow row)
        {
            if (string.Equals(value, "Front", StringComparison.Ordinal)) { row = EnemyRosterRow.Front; return true; }
            if (string.Equals(value, "Middle", StringComparison.Ordinal)) { row = EnemyRosterRow.Middle; return true; }
            if (string.Equals(value, "Back", StringComparison.Ordinal)) { row = EnemyRosterRow.Back; return true; }

            row = EnemyRosterRow.Front;
            return false;
        }

        private static bool TryParseColumn(string value, out EnemyRosterColumn column)
        {
            if (string.Equals(value, "Left", StringComparison.Ordinal)) { column = EnemyRosterColumn.Left; return true; }
            if (string.Equals(value, "Center", StringComparison.Ordinal)) { column = EnemyRosterColumn.Center; return true; }
            if (string.Equals(value, "Right", StringComparison.Ordinal)) { column = EnemyRosterColumn.Right; return true; }

            column = EnemyRosterColumn.Center;
            return false;
        }

        // ---------------- 阵容组装 / 失败映射 ----------------

        private EnemyRoster BuildRoster(EncounterDefinition encounter, string poolId, int seed, Solution solution,
            ReasonTracker reasons)
        {
            var entries = new List<GeneratedEnemyEntry>(solution.Instances.Count);

            for (int i = 0; i < solution.Instances.Count; i++)
            {
                var position = solution.Positions[i];
                string monsterId = solution.MonsterIds[i];

                if (position == null || position.ColumnIndex < 0 || string.IsNullOrEmpty(monsterId))
                {
                    reasons.EmptyRoster = true;
                    if (reasons.Detail == null) reasons.Detail = "内部一致性错误：存在未确定位置的敌人实例";
                    return null;
                }

                entries.Add(new GeneratedEnemyEntry
                {
                    EnemyInstanceId = EnemyInstanceIdFor(seed, i, monsterId),
                    MonsterId = monsterId,
                    Row = position.Row,
                    Column = (EnemyRosterColumn)position.ColumnIndex,
                    SlotId = solution.Instances[i].Plan.Slot.SlotId
                });
            }

            if (entries.Count == 0)
            {
                reasons.EmptyRoster = true;
                if (reasons.Detail == null) reasons.Detail = "该遭遇在当前配置下只能生成 0 只敌人";
                return null;
            }

            return new EnemyRoster
            {
                EncounterId = encounter.EncounterId,
                PoolId = poolId,
                Seed = seed,
                TotalThreat = solution.Threat,
                Entries = entries
            };
        }

        /// <summary>
        /// EnemyInstanceId：**实例身份**，不是 MonsterId、不是长期 Person 身份、不使用 Guid。
        /// 格式（实现细节）：<c>eid_&lt;seed 十六进制&gt;_&lt;序号两位&gt;_&lt;MonsterId&gt;</c>
        /// —— 同一阵容内序号唯一；同 Seed + 同配置 → 完全稳定。
        /// </summary>
        private static string EnemyInstanceIdFor(int seed, int index, string monsterId)
        {
            return EnemyInstanceIdPrefix
                + ((uint)seed).ToString("x8", CultureInfo.InvariantCulture)
                + "_" + index.ToString("D2", CultureInfo.InvariantCulture)
                + "_" + monsterId;
        }

        /// <summary>
        /// 失败原因映射（优先级固定且可解释：候选为空 &gt; 重复约束 &gt; 威胁预算 &gt; 阵型）。
        /// 一个遭遇可能同时存在多种成因，诊断文本会把它们全部列出。
        /// </summary>
        private static EncounterGenerationFailure MapFailure(ReasonTracker reasons)
        {
            if (reasons.NoLegalMonster) return EncounterGenerationFailure.NoLegalMonster;
            if (reasons.DuplicateConflict) return EncounterGenerationFailure.DuplicateConflict;
            if (reasons.ThreatFailure) return EncounterGenerationFailure.ThreatBudgetUnsolvable;
            if (reasons.FormationFailure) return EncounterGenerationFailure.FormationUnsolvable;
            if (reasons.EmptyRoster) return EncounterGenerationFailure.NoLegalMonster;

            // 理论不可达的兜底：搜索完整结束却没记录任何具体原因（保留明确失败语义，不伪装成功）。
            return EncounterGenerationFailure.NoLegalMonster;
        }

        private static string BuildFailureDiagnostic(EncounterDefinition encounter, ReasonTracker reasons)
        {
            var parts = new List<string>();

            if (reasons.NoLegalMonster) parts.Add("存在组合需求位没有任何合法候选怪物");
            if (reasons.DuplicateConflict) parts.Add("AllowDuplicateMonster = false 使得某个位无法凑够数量");
            if (reasons.ThreatFailure)
            {
                parts.Add("无法让总威胁落进预算 " + encounter.ThreatBudgetMin + "~" + encounter.ThreatBudgetMax
                    + "（所有合法数量 / 怪物组合都已尝试）");
            }
            if (reasons.FormationFailure) parts.Add("无法满足 3×3 阵型规则（含 Exact 精确列 / 每排人数）");
            if (reasons.EmptyRoster) parts.Add("在当前配置下只能生成 0 只敌人（空阵容不是可用的 EnemyRoster）");

            if (parts.Count == 0) parts.Add("在给定配置下找不到任何合法组合");

            string detail = string.IsNullOrEmpty(reasons.Detail) ? string.Empty : "（首个原因：" + reasons.Detail + "）";
            return "遭遇 '" + encounter.EncounterId + "' 无法生成合法阵容：" + string.Join("；", parts) + detail + "。";
        }
    }
}

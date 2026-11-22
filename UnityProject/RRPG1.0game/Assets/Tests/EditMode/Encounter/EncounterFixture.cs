using System.Collections.Generic;
using RRPG.Encounter;
using RRPG.StaticConfig;

namespace RRPG.Encounter.Tests
{
    /// <summary>
    /// Encounter Generator 的**纯契约测试夹具**（WP-Encounter-1B §20.A）。
    ///
    /// 为什么需要它：算法契约（筛选 / 数量 / 威胁 / 站位 / 重复 / 实例ID）必须能在
    /// **固定测试数据**上验证，不能依赖制作人未来会变化的内容（6 怪 → 7 / 20 / 100 怪）。
    /// 正式内容一致性由 Authoring Smoke Test 单独覆盖。
    ///
    /// 夹具里的 MonsterId 一律使用 <c>fx_</c> 前缀，与正式内容（mon_*）明确区分。
    /// </summary>
    internal sealed class EncounterFixture
    {
        private readonly List<MonsterDefinition> _monsters = new List<MonsterDefinition>();
        private readonly List<MonsterEncounterProfileDefinition> _profiles =
            new List<MonsterEncounterProfileDefinition>();
        private readonly List<MonsterRoleDefinition> _roles = new List<MonsterRoleDefinition>();
        private readonly List<EncounterSlotDefinition> _slots = new List<EncounterSlotDefinition>();
        private readonly List<EncounterSlotRuleDefinition> _rules = new List<EncounterSlotRuleDefinition>();
        private readonly List<EncounterPoolDefinition> _pools = new List<EncounterPoolDefinition>();
        private readonly List<EncounterPoolEntryDefinition> _entries = new List<EncounterPoolEntryDefinition>();
        private readonly List<EncounterDefinition> _extraEncounters = new List<EncounterDefinition>();
        private readonly List<EncounterSlotDefinition> _extraSlots = new List<EncounterSlotDefinition>();

        private string _encounterId = "fx_enc";
        private EncounterDefinition _encounter;

        public EncounterFixture()
        {
            Encounter(0, 100);
        }

        public string EncounterId { get { return _encounterId; } }

        public EncounterFixture Encounter(int threatMin, int threatMax, bool allowDuplicate = true,
            bool enabled = true, string encounterId = null)
        {
            if (!string.IsNullOrEmpty(encounterId)) _encounterId = encounterId;

            _encounter = new EncounterDefinition
            {
                EncounterId = _encounterId,
                DisplayName = "夹具遭遇",
                Enabled = enabled,
                ThreatBudgetMin = threatMin,
                ThreatBudgetMax = threatMax,
                AllowDuplicateMonster = allowDuplicate
            };
            return this;
        }

        /// <summary>加一只怪：MonsterDefinition + MonsterEncounterProfile（+ 可选多个 Role）。</summary>
        public EncounterFixture Monster(string id, int threat, string tier = "Normal", int weight = 100,
            params string[] roles)
        {
            _monsters.Add(new MonsterDefinition
            {
                MonsterId = id,
                Name = "夹具怪物 " + id,
                MaxHP = 10,
                Attack = 1,
                AttackRange = 1,
                AttackCount = 1,
                ActionPoints = 1,
                Faction = "enemy"
            });

            _profiles.Add(new MonsterEncounterProfileDefinition
            {
                MonsterId = id,
                ThreatCost = threat,
                Tier = tier,
                RandomWeight = weight
            });

            if (roles != null)
            {
                foreach (string role in roles) _roles.Add(new MonsterRoleDefinition { MonsterId = id, Role = role });
            }
            return this;
        }

        /// <summary>加一只**只有 MonsterDefinition、没有 EncounterProfile** 的怪（不得成为候选）。</summary>
        public EncounterFixture MonsterWithoutProfile(string id)
        {
            _monsters.Add(new MonsterDefinition { MonsterId = id, Name = "无画像怪物 " + id });
            return this;
        }

        public EncounterFixture Slot(string slotId, string selectionMode, int countMin, int countMax,
            string positionMode, string row = null, string column = null, string fixedMonsterId = null)
        {
            _slots.Add(new EncounterSlotDefinition
            {
                SlotId = slotId,
                EncounterId = _encounterId,
                SelectionMode = selectionMode,
                FixedMonsterId = fixedMonsterId,
                CountMin = countMin,
                CountMax = countMax,
                PositionMode = positionMode,
                Row = row,
                Column = column
            });
            return this;
        }

        public EncounterFixture Rule(string slotId, string ruleMode, string dimension, string value)
        {
            _rules.Add(new EncounterSlotRuleDefinition
            {
                SlotId = slotId,
                RuleMode = ruleMode,
                Dimension = dimension,
                Value = value
            });
            return this;
        }

        public EncounterFixture Pool(string poolId, bool enabled = true)
        {
            _pools.Add(new EncounterPoolDefinition
            {
                PoolId = poolId,
                DisplayName = "夹具池 " + poolId,
                Enabled = enabled
            });
            return this;
        }

        public EncounterFixture Entry(string poolId, string encounterId, int weight, int minDepth, int maxDepth)
        {
            _entries.Add(new EncounterPoolEntryDefinition
            {
                PoolId = poolId,
                EncounterId = encounterId,
                Weight = weight,
                MinDepth = minDepth,
                MaxDepth = maxDepth
            });
            return this;
        }

        /// <summary>额外加一个「另一个遭遇」（用于池回退测试；它自己的 Slot 由本方法给出）。</summary>
        public EncounterFixture OtherEncounter(string encounterId, int threatMin, int threatMax, bool enabled,
            bool allowDuplicate, string slotId, int slotCountMin, int slotCountMax,
            string selectionMode = "Query", string fixedMonsterId = null, string positionMode = "Any",
            string row = null, string column = null)
        {
            _extraEncounters.Add(new EncounterDefinition
            {
                EncounterId = encounterId,
                DisplayName = "夹具遭遇 " + encounterId,
                Enabled = enabled,
                ThreatBudgetMin = threatMin,
                ThreatBudgetMax = threatMax,
                AllowDuplicateMonster = allowDuplicate
            });

            _extraSlots.Add(new EncounterSlotDefinition
            {
                SlotId = slotId,
                EncounterId = encounterId,
                SelectionMode = selectionMode,
                FixedMonsterId = fixedMonsterId,
                CountMin = slotCountMin,
                CountMax = slotCountMax,
                PositionMode = positionMode,
                Row = row,
                Column = column
            });
            return this;
        }

        public EncounterGenerationInput Build()
        {
            var encounters = new List<EncounterDefinition> { _encounter };
            encounters.AddRange(_extraEncounters);

            var slots = new List<EncounterSlotDefinition>(_slots);
            slots.AddRange(_extraSlots);

            return new EncounterGenerationInput
            {
                Encounters = encounters,
                Slots = slots,
                SlotRules = _rules,
                Pools = _pools,
                PoolEntries = _entries,
                MonsterProfiles = _profiles,
                MonsterRoles = _roles,
                Monsters = _monsters
            };
        }

        public EncounterGenerator Generator()
        {
            return new EncounterGenerator(Build());
        }

    }
}

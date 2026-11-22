using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Encounter;
using RRPG.StaticConfig;

namespace RRPG.Encounter.Tests
{
    /// <summary>
    /// 生成结果的**共享不变量断言**（夹具测试与正式配置冒烟测试共用同一套判据）。
    ///
    /// 它检查的是可运行验证的硬性质，而不是某个具体随机结果：
    ///   - 成功 → 完整阵容（非空、InstanceId 唯一、位置唯一且合法、每排 ≤ 3、总数 ≤ 9）；
    ///   - MonsterId 必须在传入快照里同时拥有 Definition 与 EncounterProfile；
    ///   - TotalThreat = 各 Monster ThreatCost 之和，且严格落在该遭遇预算区间内；
    ///   - 列结构与「1 人 Center / 2 人 Left+Right / 3 人 Left+Center+Right」一致。
    /// </summary>
    internal static class EncounterRosterAssert
    {
        public static void AssertValid(EncounterGenerationResult result, EncounterGenerationInput input)
        {
            Assert.IsNotNull(result, "生成结果不得为 null");
            Assert.IsTrue(result.Success, "生成必须成功，实际：" + result.Failure + " / " + result.Diagnostic);

            var roster = result.Roster;
            Assert.IsNotNull(roster, "成功结果必须带完整 EnemyRoster");
            Assert.IsNotEmpty(roster.EncounterId, "EncounterId 不得为空");
            Assert.Greater(roster.Count, 0, "阵容不得为空（空阵容不是可用结果）");
            Assert.LessOrEqual(roster.Count, EnemyBoardFormation.MaxEnemyUnits, "敌人数不得超过 9");

            var encounter = FindEncounter(input, roster.EncounterId);
            Assert.IsNotNull(encounter, "EnemyRoster.EncounterId 必须是真实遭遇：" + roster.EncounterId);

            var monsterIds = MonsterIdsWithDefinition(input);
            var profiles = ProfilesByMonsterId(input);
            var slotIds = SlotIds(input);

            var instanceIds = new HashSet<string>();
            var positions = new HashSet<string>();
            var rowCounts = new Dictionary<EnemyRosterRow, int>();
            int threatSum = 0;

            foreach (var entry in roster.Entries)
            {
                Assert.IsNotNull(entry, "敌人实例不得为 null");
                Assert.IsNotEmpty(entry.EnemyInstanceId, "EnemyInstanceId 不得为空");
                Assert.IsTrue(instanceIds.Add(entry.EnemyInstanceId),
                    "EnemyInstanceId 必须唯一：" + entry.EnemyInstanceId);
                Assert.IsNotEmpty(entry.MonsterId, "MonsterId 不得为空");
                Assert.IsTrue(monsterIds.Contains(entry.MonsterId),
                    "MonsterId 必须存在于 Monster 定义：" + entry.MonsterId);
                Assert.IsTrue(profiles.ContainsKey(entry.MonsterId),
                    "MonsterId 必须拥有 MonsterEncounterProfile：" + entry.MonsterId);
                threatSum += profiles[entry.MonsterId].ThreatCost;

                Assert.IsTrue(slotIds.Contains(entry.SlotId), "SlotId 必须是真实组合需求位：" + entry.SlotId);

                string key = (int)entry.Row + ":" + (int)entry.Column;
                Assert.IsTrue(positions.Add(key), "同一格最多 1 只敌人，重复位置：" + key);

                int count;
                rowCounts.TryGetValue(entry.Row, out count);
                rowCounts[entry.Row] = count + 1;
            }

            foreach (var pair in rowCounts)
            {
                Assert.LessOrEqual(pair.Value, EnemyBoardFormation.MaxUnitsPerRow,
                    "每排不得超过 3 只：" + pair.Key + " = " + pair.Value);
                Assert.IsTrue(EnemyBoardFormation.IsLegalRowCount(pair.Value));
            }

            // 列结构：该排最终 N 人的合法列集合必须被精确使用（无缺位、无多余列）
            foreach (var pair in rowCounts)
            {
                var required = EnemyBoardFormation.RequiredColumns(pair.Value);
                var used = new List<int>();
                foreach (var entry in roster.Entries)
                {
                    if (entry.Row != pair.Key) continue;
                    used.Add((int)entry.Column);
                }

                Assert.AreEqual(required.Count, used.Count, "该排列数必须等于合法列数：" + pair.Key);
                foreach (var column in required) Assert.Contains((int)column, used, "该排缺少合法列：" + pair.Key);
            }

            Assert.AreEqual(threatSum, roster.TotalThreat, "TotalThreat 必须等于各敌人 ThreatCost 之和");
            Assert.GreaterOrEqual(roster.TotalThreat, encounter.ThreatBudgetMin, "总威胁必须 >= ThreatBudgetMin");
            Assert.LessOrEqual(roster.TotalThreat, encounter.ThreatBudgetMax, "总威胁必须 <= ThreatBudgetMax");
        }

        /// <summary>Duplicate = false 时的跨阵容一致性：同一 MonsterId 最多出现一次。</summary>
        public static void AssertNoDuplicateMonsterIds(EnemyRoster roster)
        {
            var seen = new HashSet<string>();
            foreach (var entry in roster.Entries)
            {
                Assert.IsTrue(seen.Add(entry.MonsterId),
                    "AllowDuplicateMonster = false 时不得重复：" + entry.MonsterId);
            }
        }

        /// <summary>两份独立生成的阵容必须完全一致（确定性判据；含顺序）。</summary>
        public static void AssertIdentical(EnemyRoster expected, EnemyRoster actual)
        {
            Assert.IsNotNull(expected);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.EncounterId, actual.EncounterId);
            Assert.AreEqual(expected.PoolId ?? string.Empty, actual.PoolId ?? string.Empty);
            Assert.AreEqual(expected.Seed, actual.Seed);
            Assert.AreEqual(expected.TotalThreat, actual.TotalThreat);
            Assert.AreEqual(expected.Count, actual.Count, "条数必须一致");

            for (int i = 0; i < expected.Count; i++)
            {
                var a = expected.Entries[i];
                var b = actual.Entries[i];
                Assert.AreEqual(a.EnemyInstanceId, b.EnemyInstanceId, "第 " + i + " 个 InstanceId 必须一致");
                Assert.AreEqual(a.MonsterId, b.MonsterId, "第 " + i + " 个 MonsterId 必须一致");
                Assert.AreEqual(a.Row, b.Row, "第 " + i + " 个 Row 必须一致");
                Assert.AreEqual(a.Column, b.Column, "第 " + i + " 个 Column 必须一致");
                Assert.AreEqual(a.SlotId, b.SlotId, "第 " + i + " 个 SlotId 必须一致");
            }
        }

        /// <summary>某遭遇下所有生成出来的 MonsterId 集合（用于筛选用例断言）。</summary>
        public static HashSet<string> MonsterIdsOf(EnemyRoster roster)
        {
            var ids = new HashSet<string>();
            foreach (var entry in roster.Entries) ids.Add(entry.MonsterId);
            return ids;
        }

        // ---------------- 快照查询工具 ----------------

        public static EncounterDefinition FindEncounter(EncounterGenerationInput input, string encounterId)
        {
            foreach (var encounter in input.Encounters)
            {
                if (encounter != null && encounter.EncounterId == encounterId) return encounter;
            }
            return null;
        }

        public static HashSet<string> MonsterIdsWithDefinition(EncounterGenerationInput input)
        {
            var ids = new HashSet<string>();
            foreach (var monster in input.Monsters)
            {
                if (monster != null && !string.IsNullOrEmpty(monster.MonsterId)) ids.Add(monster.MonsterId);
            }
            return ids;
        }

        public static Dictionary<string, MonsterEncounterProfileDefinition> ProfilesByMonsterId(
            EncounterGenerationInput input)
        {
            var profiles = new Dictionary<string, MonsterEncounterProfileDefinition>();
            foreach (var profile in input.MonsterProfiles)
            {
                if (profile != null && !string.IsNullOrEmpty(profile.MonsterId)) profiles[profile.MonsterId] = profile;
            }
            return profiles;
        }

        public static HashSet<string> SlotIds(EncounterGenerationInput input)
        {
            var ids = new HashSet<string>();
            foreach (var slot in input.Slots)
            {
                if (slot != null && !string.IsNullOrEmpty(slot.SlotId)) ids.Add(slot.SlotId);
            }
            return ids;
        }

    }
}

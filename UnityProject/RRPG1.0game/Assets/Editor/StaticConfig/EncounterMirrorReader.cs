using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// 从 Canonical Mirror 读出 Encounter 领域（WP-Encounter-1A）的 7 张表（Editor-only）。
    ///
    /// 读取的文件：`Encounter.json` / `EncounterSlot.json` / `EncounterSlotRule.json` /
    /// `EncounterPool.json` / `EncounterPoolEntry.json`（Encounter 工作簿的 5 张表）
    /// 与 `MonsterEncounterProfile.json` / `MonsterRole.json`（Monster 工作簿的 2 张 Encounter 子表）。
    ///
    /// 校验边界（**与 Compiler 的分工，刻意不重复**）：
    ///   - Compiler 是**领域语义 Authority**：类型 / 范围 / allowed= / 跨列结构（预算区间、数量区间、
    ///     深度区间、ΣCountMax ≤ 9）/ 条件引用语义 / 值域，全部在 XLSX → Mirror 阶段硬失败；
    ///   - 本类只做 Builder 必须自己保证的**自洽性**（第二道防线，全部发生在任何写入之前）：
    ///       1. 结构：7 个文件必须**同时存在或同时不存在**；每个文件的列集合必须精确匹配；rowCount 与行数一致；
    ///       2. 身份：主键 / 复合身份在本文件内不得重复；必填列不得为空；
    ///       3. 引用完整性：Slot→Encounter、Slot.FixedMonsterId→Monster、Rule.SlotId→Slot、
    ///          Rule(Dimension=MonsterId).Value→Monster、PoolEntry.PoolId→Pool、PoolEntry.EncounterId→Encounter、
    ///          Profile/Role.MonsterId→Monster；
    ///       4. 条件形状：FixedMonster 必须有 FixedMonsterId、Query 不得带 FixedMonsterId。
    ///
    /// 缺失语义：7 个文件都不存在 = 该领域尚未迁移（**合法空态**，不生成库、不报错）；
    /// 只存在一部分 = Mirror 不完整 = Hard Error（Builder 必须在写入前停下）。
    /// </summary>
    internal static class EncounterMirrorReader
    {
        public const string EncounterSheet = "Encounter";
        public const string EncounterSlotSheet = "EncounterSlot";
        public const string EncounterSlotRuleSheet = "EncounterSlotRule";
        public const string EncounterPoolSheet = "EncounterPool";
        public const string EncounterPoolEntrySheet = "EncounterPoolEntry";
        public const string MonsterEncounterProfileSheet = "MonsterEncounterProfile";
        public const string MonsterRoleSheet = "MonsterRole";

        public static readonly string[] SheetNames =
        {
            EncounterSheet, EncounterSlotSheet, EncounterSlotRuleSheet,
            EncounterPoolSheet, EncounterPoolEntrySheet,
            MonsterEncounterProfileSheet, MonsterRoleSheet
        };

        public static readonly string[] EncounterFields =
        {
            "EncounterId", "DisplayName", "Enabled",
            "ThreatBudgetMin", "ThreatBudgetMax", "AllowDuplicateMonster"
        };

        public static readonly string[] SlotFields =
        {
            "SlotId", "EncounterId", "SelectionMode", "FixedMonsterId",
            "CountMin", "CountMax", "PositionMode", "Row", "Column"
        };

        public static readonly string[] SlotRuleFields = { "SlotId", "RuleMode", "Dimension", "Value" };

        public static readonly string[] PoolFields = { "PoolId", "DisplayName", "Enabled" };

        public static readonly string[] PoolEntryFields =
        {
            "PoolId", "EncounterId", "Weight", "MinDepth", "MaxDepth"
        };

        public static readonly string[] ProfileFields =
        {
            "MonsterId", "ThreatCost", "Tier", "RandomWeight"
        };

        public static readonly string[] RoleFields = { "MonsterId", "Role" };

        /// <summary>固定怪物模式 / 条件随机模式（形状校验用；取值域 Authority 仍是 Compiler）。</summary>
        public const string SelectionModeFixedMonster = "FixedMonster";
        public const string SelectionModeQuery = "Query";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>7 张表的读取结果（Builder 内部输入模型）。</summary>
        public sealed class Payload
        {
            public bool DomainAbsent;

            public List<EncounterDefinition> Encounters = new List<EncounterDefinition>();
            public List<EncounterSlotDefinition> Slots = new List<EncounterSlotDefinition>();
            public List<EncounterSlotRuleDefinition> SlotRules = new List<EncounterSlotRuleDefinition>();
            public List<EncounterPoolDefinition> Pools = new List<EncounterPoolDefinition>();
            public List<EncounterPoolEntryDefinition> PoolEntries = new List<EncounterPoolEntryDefinition>();
            public List<MonsterEncounterProfileDefinition> MonsterProfiles =
                new List<MonsterEncounterProfileDefinition>();
            public List<MonsterRoleDefinition> MonsterRoles = new List<MonsterRoleDefinition>();

            public string EncounterSha256 = string.Empty;
            public string MonsterProfileSha256 = string.Empty;
            public string MonsterRoleSha256 = string.Empty;

            public int TotalCount
            {
                get
                {
                    return Encounters.Count + Slots.Count + SlotRules.Count + Pools.Count
                        + PoolEntries.Count + MonsterProfiles.Count + MonsterRoles.Count;
                }
            }
        }

        /// <summary>
        /// 读取整个 Encounter 领域。失败返回 null 并给出可操作的中文原因（Builder 必须在此之前不写任何产物）。
        /// </summary>
        public static Payload Read(string mirrorDir, string mirrorRelative, ISet<string> monsterIds,
            out string failure)
        {
            failure = string.Empty;

            int present = 0;
            foreach (string sheetName in SheetNames)
            {
                if (File.Exists(Path.Combine(mirrorDir, sheetName + ".json"))) present++;
            }

            var payload = new Payload();

            if (present == 0)
            {
                payload.DomainAbsent = true;
                return payload;   // 合法空态：该领域尚未迁移
            }

            if (present != SheetNames.Length)
            {
                var missing = new List<string>();
                foreach (string sheetName in SheetNames)
                {
                    if (!File.Exists(Path.Combine(mirrorDir, sheetName + ".json"))) missing.Add(sheetName + ".json");
                }

                failure = "Mirror 不完整：Encounter 领域需要 7 个文件同时存在（缺失："
                    + string.Join(" / ", missing) + "）。请重新运行 Compiler build（RRPG_一键导表.cmd）。";
                return null;
            }

            if (!ReadEncounters(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadSlots(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadSlotRules(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadPools(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadPoolEntries(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadProfiles(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadRoles(mirrorDir, mirrorRelative, payload, out failure)) return null;

            if (!CrossCheck(payload, monsterIds, out failure)) return null;

            return payload;
        }

        // ---------------- 跨表引用与条件形状（第二道防线） ----------------

        private static bool CrossCheck(Payload payload, ISet<string> monsterIds, out string failure)
        {
            failure = string.Empty;

            var encounterIds = NewIdSet();
            foreach (var encounter in payload.Encounters) encounterIds.Add(encounter.EncounterId);

            var slotIds = NewIdSet();
            foreach (var slot in payload.Slots) slotIds.Add(slot.SlotId);

            var poolIds = NewIdSet();
            foreach (var pool in payload.Pools) poolIds.Add(pool.PoolId);

            foreach (var slot in payload.Slots)
            {
                if (!encounterIds.Contains(slot.EncounterId))
                {
                    return Fail(out failure, "EncounterSlot " + slot.SlotId + " 引用不存在的 EncounterId："
                        + slot.EncounterId);
                }

                if (slot.SelectionMode == SelectionModeFixedMonster)
                {
                    if (string.IsNullOrEmpty(slot.FixedMonsterId))
                    {
                        return Fail(out failure, "EncounterSlot " + slot.SlotId
                            + " 的 SelectionMode = FixedMonster 但没有 FixedMonsterId（Mirror 不自洽）。");
                    }

                    if (monsterIds != null && !monsterIds.Contains(slot.FixedMonsterId))
                    {
                        return Fail(out failure, "EncounterSlot " + slot.SlotId + " 引用不存在的怪物："
                            + slot.FixedMonsterId);
                    }
                }
                else if (slot.SelectionMode == SelectionModeQuery && !string.IsNullOrEmpty(slot.FixedMonsterId))
                {
                    return Fail(out failure, "EncounterSlot " + slot.SlotId
                        + " 的 SelectionMode = Query 却带 FixedMonsterId（Mirror 不自洽）：" + slot.FixedMonsterId);
                }
            }

            foreach (var rule in payload.SlotRules)
            {
                if (!slotIds.Contains(rule.SlotId))
                {
                    return Fail(out failure, "EncounterSlotRule 引用不存在的 SlotId：" + rule.SlotId);
                }

                if (string.Equals(rule.Dimension, "MonsterId", StringComparison.Ordinal)
                    && monsterIds != null && !monsterIds.Contains(rule.Value))
                {
                    return Fail(out failure, "EncounterSlotRule（" + rule.SlotId
                        + "）的 Dimension = MonsterId 引用了不存在的怪物：" + rule.Value);
                }
            }

            foreach (var entry in payload.PoolEntries)
            {
                if (!poolIds.Contains(entry.PoolId))
                {
                    return Fail(out failure, "EncounterPoolEntry 引用不存在的 PoolId：" + entry.PoolId);
                }

                if (!encounterIds.Contains(entry.EncounterId))
                {
                    return Fail(out failure, "EncounterPoolEntry 引用不存在的 EncounterId：" + entry.EncounterId);
                }
            }

            if (monsterIds != null)
            {
                foreach (var profile in payload.MonsterProfiles)
                {
                    if (!monsterIds.Contains(profile.MonsterId))
                    {
                        return Fail(out failure, "MonsterEncounterProfile 引用不存在的怪物：" + profile.MonsterId);
                    }
                }

                foreach (var role in payload.MonsterRoles)
                {
                    if (!monsterIds.Contains(role.MonsterId))
                    {
                        return Fail(out failure, "MonsterRole 引用不存在的怪物：" + role.MonsterId);
                    }
                }
            }

            return true;
        }

        private static HashSet<string> NewIdSet()
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        private static bool Fail(out string failure, string message)
        {
            failure = message;
            return false;
        }

        // ---------------- 逐表读取 ----------------

        private sealed class RawTable
        {
            public string FileName;
            public string SourceSha256;
            public List<Dictionary<string, string>> Rows;
        }

        private static bool ReadTable(string mirrorDir, string sheetName, string[] expectedFields,
            out RawTable table, out string failure)
        {
            failure = string.Empty;
            table = null;

            string fileName = sheetName + ".json";
            string json = File.ReadAllText(Path.Combine(mirrorDir, fileName), Utf8NoBom);

            string formatVersion;
            string sourceSha256;
            if (!MirrorJsonText.TryReadHeader(json, fileName, out formatVersion, out sourceSha256, out failure))
            {
                return false;
            }
            if (!MirrorJsonText.TryReadSheet(json, fileName, sheetName, out failure)) return false;
            if (!MirrorJsonText.TryReadColumns(json, fileName, expectedFields, out failure)) return false;

            int declaredRowCount;
            List<Dictionary<string, string>> rows;
            if (!MirrorJsonText.TryReadRows(json, fileName, "rowCount", "rows",
                    out declaredRowCount, out rows, out failure))
            {
                return false;
            }

            table = new RawTable { FileName = fileName, SourceSha256 = sourceSha256, Rows = rows };
            return true;
        }

        private static string Where(RawTable table, int rowNumber, string message)
        {
            return table.FileName + " 第 " + rowNumber + " 行：" + message;
        }

        private static bool ReadEncounters(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, EncounterSheet, EncounterFields, out table, out failure)) return false;
            payload.EncounterSha256 = table.SourceSha256 ?? string.Empty;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string id = MirrorJsonText.Unquote(cells, "EncounterId");
                if (string.IsNullOrEmpty(id))
                {
                    return Fail(out failure, Where(table, rowNumber, "EncounterId 为空。"));
                }
                if (!seen.Add(id))
                {
                    return Fail(out failure, Where(table, rowNumber, "重复 EncounterId：" + id));
                }

                int budgetMin, budgetMax;
                if (!MirrorJsonText.TryInt(cells, "ThreatBudgetMin", out budgetMin))
                {
                    return Fail(out failure, Where(table, rowNumber, "ThreatBudgetMin 不是整数。"));
                }
                if (!MirrorJsonText.TryInt(cells, "ThreatBudgetMax", out budgetMax))
                {
                    return Fail(out failure, Where(table, rowNumber, "ThreatBudgetMax 不是整数。"));
                }

                bool enabled, allowDuplicate;
                if (!MirrorJsonText.TryBool(cells, "Enabled", out enabled))
                {
                    return Fail(out failure, Where(table, rowNumber, "Enabled 不是布尔值。"));
                }
                if (!MirrorJsonText.TryBool(cells, "AllowDuplicateMonster", out allowDuplicate))
                {
                    return Fail(out failure, Where(table, rowNumber, "AllowDuplicateMonster 不是布尔值。"));
                }

                payload.Encounters.Add(new EncounterDefinition
                {
                    EncounterId = id,
                    DisplayName = MirrorJsonText.Unquote(cells, "DisplayName") ?? string.Empty,
                    Enabled = enabled,
                    ThreatBudgetMin = budgetMin,
                    ThreatBudgetMax = budgetMax,
                    AllowDuplicateMonster = allowDuplicate
                });
            }

            return true;
        }

        private static bool ReadSlots(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, EncounterSlotSheet, SlotFields, out table, out failure)) return false;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string id = MirrorJsonText.Unquote(cells, "SlotId");
                string encounterId = MirrorJsonText.Unquote(cells, "EncounterId");
                string selectionMode = MirrorJsonText.Unquote(cells, "SelectionMode");
                string positionMode = MirrorJsonText.Unquote(cells, "PositionMode");

                if (string.IsNullOrEmpty(id)) return Fail(out failure, Where(table, rowNumber, "SlotId 为空。"));
                if (!seen.Add(id)) return Fail(out failure, Where(table, rowNumber, "重复 SlotId：" + id));
                if (string.IsNullOrEmpty(encounterId))
                {
                    return Fail(out failure, Where(table, rowNumber, "EncounterId 为空。"));
                }
                if (string.IsNullOrEmpty(selectionMode))
                {
                    return Fail(out failure, Where(table, rowNumber, "SelectionMode 为空。"));
                }
                if (string.IsNullOrEmpty(positionMode))
                {
                    return Fail(out failure, Where(table, rowNumber, "PositionMode 为空。"));
                }

                int countMin, countMax;
                if (!MirrorJsonText.TryInt(cells, "CountMin", out countMin))
                {
                    return Fail(out failure, Where(table, rowNumber, "CountMin 不是整数。"));
                }
                if (!MirrorJsonText.TryInt(cells, "CountMax", out countMax))
                {
                    return Fail(out failure, Where(table, rowNumber, "CountMax 不是整数。"));
                }

                payload.Slots.Add(new EncounterSlotDefinition
                {
                    SlotId = id,
                    EncounterId = encounterId,
                    SelectionMode = selectionMode,
                    FixedMonsterId = MirrorJsonText.Unquote(cells, "FixedMonsterId"),
                    CountMin = countMin,
                    CountMax = countMax,
                    PositionMode = positionMode,
                    Row = MirrorJsonText.Unquote(cells, "Row"),
                    Column = MirrorJsonText.Unquote(cells, "Column")
                });
            }

            return true;
        }

        private static bool ReadSlotRules(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, EncounterSlotRuleSheet, SlotRuleFields, out table, out failure)) return false;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string slotId = MirrorJsonText.Unquote(cells, "SlotId");
                string ruleMode = MirrorJsonText.Unquote(cells, "RuleMode");
                string dimension = MirrorJsonText.Unquote(cells, "Dimension");
                string value = MirrorJsonText.Unquote(cells, "Value");

                if (string.IsNullOrEmpty(slotId)) return Fail(out failure, Where(table, rowNumber, "SlotId 为空。"));
                if (string.IsNullOrEmpty(ruleMode)) return Fail(out failure, Where(table, rowNumber, "RuleMode 为空。"));
                if (string.IsNullOrEmpty(dimension)) return Fail(out failure, Where(table, rowNumber, "Dimension 为空。"));
                if (string.IsNullOrEmpty(value)) return Fail(out failure, Where(table, rowNumber, "Value 为空。"));

                if (!seen.Add(slotId + "\u001f" + ruleMode + "\u001f" + dimension + "\u001f" + value))
                {
                    return Fail(out failure, Where(table, rowNumber,
                        "复合身份重复（SlotId + RuleMode + Dimension + Value）：" + slotId + " / " + ruleMode
                        + " / " + dimension + " / " + value));
                }

                payload.SlotRules.Add(new EncounterSlotRuleDefinition
                {
                    SlotId = slotId,
                    RuleMode = ruleMode,
                    Dimension = dimension,
                    Value = value
                });
            }

            return true;
        }

        private static bool ReadPools(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, EncounterPoolSheet, PoolFields, out table, out failure)) return false;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string id = MirrorJsonText.Unquote(cells, "PoolId");
                if (string.IsNullOrEmpty(id)) return Fail(out failure, Where(table, rowNumber, "PoolId 为空。"));
                if (!seen.Add(id)) return Fail(out failure, Where(table, rowNumber, "重复 PoolId：" + id));

                bool enabled;
                if (!MirrorJsonText.TryBool(cells, "Enabled", out enabled))
                {
                    return Fail(out failure, Where(table, rowNumber, "Enabled 不是布尔值。"));
                }

                payload.Pools.Add(new EncounterPoolDefinition
                {
                    PoolId = id,
                    DisplayName = MirrorJsonText.Unquote(cells, "DisplayName") ?? string.Empty,
                    Enabled = enabled
                });
            }

            return true;
        }

        private static bool ReadPoolEntries(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, EncounterPoolEntrySheet, PoolEntryFields, out table, out failure)) return false;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string poolId = MirrorJsonText.Unquote(cells, "PoolId");
                string encounterId = MirrorJsonText.Unquote(cells, "EncounterId");

                if (string.IsNullOrEmpty(poolId)) return Fail(out failure, Where(table, rowNumber, "PoolId 为空。"));
                if (string.IsNullOrEmpty(encounterId))
                {
                    return Fail(out failure, Where(table, rowNumber, "EncounterId 为空。"));
                }
                if (!seen.Add(poolId + "\u001f" + encounterId))
                {
                    return Fail(out failure, Where(table, rowNumber,
                        "复合身份重复（PoolId + EncounterId）：" + poolId + " / " + encounterId));
                }

                int weight, minDepth, maxDepth;
                if (!MirrorJsonText.TryInt(cells, "Weight", out weight))
                    return Fail(out failure, Where(table, rowNumber, "Weight 不是整数。"));
                if (!MirrorJsonText.TryInt(cells, "MinDepth", out minDepth))
                    return Fail(out failure, Where(table, rowNumber, "MinDepth 不是整数。"));
                if (!MirrorJsonText.TryInt(cells, "MaxDepth", out maxDepth))
                    return Fail(out failure, Where(table, rowNumber, "MaxDepth 不是整数。"));

                payload.PoolEntries.Add(new EncounterPoolEntryDefinition
                {
                    PoolId = poolId,
                    EncounterId = encounterId,
                    Weight = weight,
                    MinDepth = minDepth,
                    MaxDepth = maxDepth
                });
            }

            return true;
        }

        private static bool ReadProfiles(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, MonsterEncounterProfileSheet, ProfileFields, out table, out failure)) return false;
            payload.MonsterProfileSha256 = table.SourceSha256 ?? string.Empty;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string monsterId = MirrorJsonText.Unquote(cells, "MonsterId");
                string tier = MirrorJsonText.Unquote(cells, "Tier");

                if (string.IsNullOrEmpty(monsterId))
                {
                    return Fail(out failure, Where(table, rowNumber, "MonsterId 为空。"));
                }
                if (!seen.Add(monsterId))
                {
                    return Fail(out failure, Where(table, rowNumber, "重复 MonsterId（一怪一行）：" + monsterId));
                }
                if (string.IsNullOrEmpty(tier))
                {
                    return Fail(out failure, Where(table, rowNumber, "Tier 为空。"));
                }

                int threatCost, randomWeight;
                if (!MirrorJsonText.TryInt(cells, "ThreatCost", out threatCost))
                    return Fail(out failure, Where(table, rowNumber, "ThreatCost 不是整数。"));
                if (!MirrorJsonText.TryInt(cells, "RandomWeight", out randomWeight))
                    return Fail(out failure, Where(table, rowNumber, "RandomWeight 不是整数。"));

                payload.MonsterProfiles.Add(new MonsterEncounterProfileDefinition
                {
                    MonsterId = monsterId,
                    ThreatCost = threatCost,
                    Tier = tier,
                    RandomWeight = randomWeight
                });
            }

            return true;
        }

        private static bool ReadRoles(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, MonsterRoleSheet, RoleFields, out table, out failure)) return false;
            payload.MonsterRoleSha256 = table.SourceSha256 ?? string.Empty;

            var seen = NewIdSet();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string monsterId = MirrorJsonText.Unquote(cells, "MonsterId");
                string role = MirrorJsonText.Unquote(cells, "Role");

                if (string.IsNullOrEmpty(monsterId))
                {
                    return Fail(out failure, Where(table, rowNumber, "MonsterId 为空。"));
                }
                if (string.IsNullOrEmpty(role))
                {
                    return Fail(out failure, Where(table, rowNumber, "Role 为空。"));
                }
                if (!seen.Add(monsterId + "\u001f" + role))
                {
                    return Fail(out failure, Where(table, rowNumber,
                        "复合身份重复（MonsterId + Role）：" + monsterId + " / " + role));
                }

                payload.MonsterRoles.Add(new MonsterRoleDefinition
                {
                    MonsterId = monsterId,
                    Role = role
                });
            }

            return true;
        }







    }
}

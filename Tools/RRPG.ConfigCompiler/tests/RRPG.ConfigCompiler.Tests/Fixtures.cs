using System.Collections.Generic;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>测试用共享 Fixture 定义（抽象，不代表游戏内容）。</summary>
    public static class Fixtures
    {
        public static TestSheet MinimalMonsterSheet(string name = "Monster")
        {
            return new TestSheet(name)
                .Col("MonsterId", "id", "", "怪物ID")
                .Col("Name", "string", "", "怪物名称")
                .Col("MaxHP", "int", ">=1", "最大生命值")
                .Row("mon_001", "史莱姆", 20)
                .Row("mon_002", "骷髅弓手", 15);
        }

        public static TestSheet ParentSheet()
        {
            return new TestSheet("FixtureParent")
                .Col("ParentId", "id", "", "父项ID")
                .Col("Label", "string", "", "父项标签")
                .Col("Weight", "float", ">=0;<=1", "权重")
                .Row("P_A", "父项A", 0.25)
                .Row("P_B", "父项B", 0.5);
        }

        public static TestSheet ChildSheet(params object[][] rows)
        {
            var sheet = new TestSheet("FixtureChild")
                .Col("ParentId", "ref:FixtureParent", "key", "父项引用")
                .Col("Seq", "int", "key;>=1;<=99", "子项序号")
                .Col("Label", "string", "", "子项标签");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        public static object[][] DefaultChildRows()
        {
            // 物理顺序故意打乱
            return new[]
            {
                new object[] { "P_B", 2, "子项B-2" },
                new object[] { "P_A", 2, "子项A-2" },
                new object[] { "P_B", 1, "子项B-1" },
                new object[] { "P_A", 1, "子项A-1" }
            };
        }

        // ---------------- M3 Slice 1A：Enemy Presentation 领域 ----------------

        /// <summary>VisualAsset：三列契约（VisualAssetId / SourceFile / ImportPreset）。</summary>
        public static TestSheet VisualAssetSheet(string visualAssetId, string sourceFile,
            string importPreset = "sprite_point_100")
        {
            return new TestSheet("VisualAsset")
                .Col("VisualAssetId", "id", "", "视觉资源ID")
                .Col("SourceFile", "string", "", "源图")
                .Col("ImportPreset", "string", "allowed=sprite_point_100", "导入预设")
                .Row(visualAssetId, sourceFile, importPreset);
        }

        /// <summary>VisualAsset：只有 id 列（缺 SourceFile / ImportPreset）→ DAT017 形状负例。</summary>
        public static TestSheet VisualAssetSheetWithoutSourceColumns()
        {
            return new TestSheet("VisualAsset")
                .Col("VisualAssetId", "id", "", "视觉资源ID")
                .Row("va_test");
        }

        /// <summary>
        /// VisualAsset：刻意**不写** Row 3 的 <c>allowed=</c>。
        /// 用途：覆盖「XLSX 声明与 Compiler 预设表不同步」时的第二道防线（DAT015），
        /// 否则非法预设会先被 allowed=（DAT012）拦下。
        /// </summary>
        public static TestSheet VisualAssetSheetWithoutAllowedRule(string visualAssetId, string sourceFile,
            string importPreset)
        {
            return new TestSheet("VisualAsset")
                .Col("VisualAssetId", "id", "", "视觉资源ID")
                .Col("SourceFile", "string", "", "源图")
                .Col("ImportPreset", "string", "", "导入预设")
                .Row(visualAssetId, sourceFile, importPreset);
        }

        /// <summary>EnemyPresentation：复合身份 = MonsterId（ref:Monster → ref:VisualAsset）。</summary>
        public static TestSheet EnemyPresentationSheet(params object[][] rows)
        {
            var sheet = new TestSheet("EnemyPresentation")
                .Col("MonsterId", "ref:Monster", "key", "怪物")
                .Col("VisualAssetId", "ref:VisualAsset", "", "视觉资源");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        public static object[][] DefaultEnemyPresentationRows()
        {
            return new[]
            {
                new object[] { "mon_001", "va_enemy_body_placeholder" },
                new object[] { "mon_002", "va_enemy_body_placeholder" }
            };
        }

        // ---------------- WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础） ----------------

        /// <summary>Encounter：威胁预算区间 + 是否允许同种怪重复（敌人数不由本表决定）。</summary>
        public static TestSheet EncounterSheet(params object[][] rows)
        {
            var sheet = new TestSheet("Encounter")
                .Col("EncounterId", "id", "", "遭遇模板ID")
                .Col("DisplayName", "string", "", "中文名称")
                .Col("Enabled", "bool", "", "是否启用")
                .Col("ThreatBudgetMin", "int", ">=0", "最低威胁预算")
                .Col("ThreatBudgetMax", "int", ">=0", "最高威胁预算")
                .Col("AllowDuplicateMonster", "bool", "", "是否允许同种怪重复");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>EncounterSlot：组合需求位（敌人数量的唯一来源）。</summary>
        public static TestSheet EncounterSlotSheet(params object[][] rows)
        {
            var sheet = new TestSheet("EncounterSlot")
                .Col("SlotId", "id", "", "组合需求位ID")
                .Col("EncounterId", "ref:Encounter", "", "所属遭遇模板ID")
                .Col("SelectionMode", "string", "allowed=FixedMonster|Query", "选怪方式")
                .Col("FixedMonsterId", "string", "optional", "固定怪物ID")
                .Col("CountMin", "int", ">=0", "最少数量")
                .Col("CountMax", "int", ">=0", "最多数量")
                .Col("PositionMode", "string", "allowed=Any|Row|Exact", "站位模式")
                .Col("Row", "string", "optional;allowed=Front|Middle|Back", "指定排")
                .Col("Column", "string", "optional;allowed=Left|Center|Right", "指定列");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>EncounterSlotRule：筛选规则（复合身份 = SlotId + RuleMode + Dimension + Value）。</summary>
        public static TestSheet EncounterSlotRuleSheet(params object[][] rows)
        {
            var sheet = new TestSheet("EncounterSlotRule")
                .Col("SlotId", "ref:EncounterSlot", "key", "组合需求位ID")
                .Col("RuleMode", "string", "key;allowed=AnyOf|Require|Exclude", "规则方式")
                .Col("Dimension", "string", "key;allowed=Role|Tier|MonsterId", "筛选维度")
                .Col("Value", "string", "key", "筛选值");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>EncounterPool：遭遇池（保持最小）。</summary>
        public static TestSheet EncounterPoolSheet(params object[][] rows)
        {
            var sheet = new TestSheet("EncounterPool")
                .Col("PoolId", "id", "", "遭遇池ID")
                .Col("DisplayName", "string", "", "中文名称")
                .Col("Enabled", "bool", "", "是否启用");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>EncounterPoolEntry：池条目（复合身份 = PoolId + EncounterId）。</summary>
        public static TestSheet EncounterPoolEntrySheet(params object[][] rows)
        {
            var sheet = new TestSheet("EncounterPoolEntry")
                .Col("PoolId", "ref:EncounterPool", "key", "遭遇池ID")
                .Col("EncounterId", "ref:Encounter", "key", "遭遇模板ID")
                .Col("Weight", "int", ">=1", "抽取权重")
                .Col("MinDepth", "int", ">=0", "最低远征深度")
                .Col("MaxDepth", "int", ">=0", "最高远征深度");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>MonsterEncounterProfile：怪物遭遇画像（写在 Monster 工作簿；一怪一行）。</summary>
        public static TestSheet MonsterEncounterProfileSheet(params object[][] rows)
        {
            var sheet = new TestSheet("MonsterEncounterProfile")
                .Col("MonsterId", "ref:Monster", "key", "怪物ID")
                .Col("ThreatCost", "int", ">=1", "威胁值")
                .Col("Tier", "string", "allowed=Normal|Elite|Boss", "怪物级别")
                .Col("RandomWeight", "int", ">=1", "基础随机权重");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>MonsterRole：怪物战斗定位（同一怪可多行：Melee + Tank）。</summary>
        public static TestSheet MonsterRoleSheet(params object[][] rows)
        {
            var sheet = new TestSheet("MonsterRole")
                .Col("MonsterId", "ref:Monster", "key", "怪物ID")
                .Col("Role", "string", "key;allowed=Melee|Ranged|Tank|Support", "战斗定位");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        /// <summary>
        /// Encounter 领域语料：Encounter 工作簿 5 张表 + Monster 工作簿的 2 张子表。
        /// <paramref name="overrides"/> 按 Sheet 名替换 / 追加单张表（负例用）。
        /// </summary>
        public static TestSheet[] EncounterDomain(params TestSheet[] overrides)
        {
            var sheets = new List<TestSheet>
            {
                EncounterSheet(
                    new object[] { "enc_test_basic", "测试遭遇·基础", true, 3, 9, true },
                    new object[] { "enc_test_boss", "测试遭遇·首领", true, 14, 24, false }),

                EncounterSlotSheet(
                    new object[] { "slot_basic_front", "enc_test_basic", "Query", "", 1, 3, "Row", "Front", "" },
                    new object[] { "slot_basic_back", "enc_test_basic", "Query", "", 0, 2, "Row", "Back", "" },
                    new object[] { "slot_boss_fixed", "enc_test_boss", "FixedMonster", "mon_001", 1, 1, "Exact", "Front", "Center" }),

                EncounterSlotRuleSheet(
                    new object[] { "slot_basic_front", "Require", "Role", "Melee" },
                    new object[] { "slot_basic_back", "Exclude", "Role", "Melee" },
                    new object[] { "slot_basic_back", "AnyOf", "MonsterId", "mon_002" },
                    new object[] { "slot_boss_fixed", "Require", "Tier", "Normal" }),

                EncounterPoolSheet(
                    new object[] { "pool_test_general", "测试遭遇池·通用", true }),

                EncounterPoolEntrySheet(
                    new object[] { "pool_test_general", "enc_test_basic", 100, 0, 3 },
                    new object[] { "pool_test_general", "enc_test_boss", 10, 5, 99 }),

                MonsterEncounterProfileSheet(
                    new object[] { "mon_001", 1, "Normal", 100 },
                    new object[] { "mon_002", 2, "Elite", 40 }),

                MonsterRoleSheet(
                    new object[] { "mon_001", "Melee" },
                    new object[] { "mon_001", "Tank" },
                    new object[] { "mon_002", "Ranged" })
            };

            if (overrides == null) return sheets.ToArray();

            foreach (var sheet in overrides)
            {
                int index = sheets.FindIndex(s => s.Name == sheet.Name);
                if (index >= 0) sheets[index] = sheet;
                else sheets.Add(sheet);
            }
            return sheets.ToArray();
        }


    }
}

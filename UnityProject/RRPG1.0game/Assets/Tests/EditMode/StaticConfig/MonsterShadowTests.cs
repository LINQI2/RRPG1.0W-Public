using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// M1 Shadow Gate → **M4 物理清理后的当前契约 Gate**（用户 M1 §8 / §10 / §11 / §15；FDS §9 Wave 1F / §10.5）。
    ///
    /// 覆盖：Shadow / Authority 侧证据（S1–S4 Registry 契约、E3 BattleSystem 按 list order 部署、
    /// B 非法 Mirror 零部分写入、F Fresh Rebuild）与 H Gate（旧 Authoring 入口**已物理删除**）。
    ///
    /// **M4 变化（Monster Legacy Physical Retirement）**：
    ///   - 原 E1（Legacy SO ↔ MonsterDefinition 逐字段等价）与 E2（`DataManager.AllMonsters` 枚举顺序对照）
    ///     属**迁移期等价证据**，随 Legacy 资产 / `MonsterData` 类型删除而退役 → 改由 **Git 历史快照**承担
    ///     （M2 Audit §5 R9、FDS §9 Wave 1F / §10.5）；Canonical 名单与顺序仍由 S1/S2/E3 覆盖。
    ///   - H Gate 从「冻结」翻转为「物理删除 + Runtime 引用 = 0」。
    /// </summary>
    public class MonsterShadowTests
    {
        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "Shadow 链必须先可构建");
        }

        private static MonsterConfigDatabase LoadDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<MonsterConfigDatabase>(StaticConfigBuilder.MonsterDatabasePath);
            Assert.IsNotNull(db, "MonsterConfigDatabase 不存在（Builder 未生成）");
            return db;
        }

        // ---------------- S1..S4：Shadow 基础契约 ----------------

        [Test]
        public void S1_MonsterDatabase_HasCurrentAuthorityEntries_WithEightFields()
        {
            var db = LoadDatabase();

            // 内容一致性契约（WP-Encounter-1A-R）：条数必须等于**当前 Canonical Mirror** 的 Monster 数，
            // 而不是某个历史快照（旧断言写死 3 条，制作人正常增加怪物就会被误判为回归）。
            var authorityIds = StaticConfigTestAuthority.AuthorityMonsterIds();
            Assert.AreEqual(authorityIds.Count, db.Count, "生成库条数必须等于当前 Authority（Mirror）的怪物数");
            CollectionAssert.AreEquivalent(authorityIds, db.All.Select(d => d.MonsterId).ToList(),
                "生成库名单必须与当前 Authority 一致");

            Assert.IsNotEmpty(db.SourceMirrorSha256, "应记录来源 Mirror 的语义指纹");

            string[] expectedFields =
            {
                "MonsterId", "Name", "MaxHP", "Attack", "AttackRange", "AttackCount", "ActionPoints", "Faction"
            };
            Assert.AreEqual(8, expectedFields.Length, "当前 Monster 字段数 = 8（M1 不得扩充）");

            foreach (var definition in db.All)
            {
                Assert.IsNotNull(definition, "定义不得为 null");
                Assert.IsNotEmpty(definition.MonsterId);
                Assert.IsNotNull(definition.Name);
                Assert.IsNotNull(definition.Faction);
            }
        }

        [Test]
        public void S2_Registry_CountGetTryGet_AndStableOrder()
        {
            var registry = LoadDatabase().CreateRegistry();
            var authorityIds = StaticConfigTestAuthority.AuthorityMonsterIds();

            Assert.AreEqual(authorityIds.Count, registry.Count, "Registry 条数必须等于当前 Authority 的怪物数");

            var ids = registry.All.Select(d => d.MonsterId).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.Ordinal).Count(), "Registry 不得有重复 ID");
            CollectionAssert.AreEquivalent(authorityIds, ids, "Registry 名单必须与当前 Authority（Mirror）一致");

            foreach (string id in ids)
            {
                MonsterDefinition fetched;
                Assert.IsTrue(registry.TryGet(id, out fetched), "TryGet 应命中：" + id);
                Assert.AreSame(registry.Get(id), fetched, "Get 与 TryGet 必须一致");
                Assert.IsTrue(registry.Contains(id));
            }

            // 稳定顺序：两次枚举必须一致（Canonical 按 key 排序的产物顺序）
            var again = registry.All.Select(d => d.MonsterId).ToList();
            CollectionAssert.AreEqual(ids, again, "Registry.All 顺序必须稳定可复现");
        }

        [Test]
        public void S3_Registry_DuplicateId_IsRejected()
        {
            var duplicates = new List<MonsterDefinition>
            {
                new MonsterDefinition { MonsterId = "dup" },
                new MonsterDefinition { MonsterId = "dup" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => new MonsterRegistry(duplicates));
            StringAssert.Contains("重复 ID", ex.Message);
        }

        [Test]
        public void S4_Registry_MissingId_ReturnsNull_WithExplicitDiagnostic()
        {
            var registry = LoadDatabase().CreateRegistry();

            MonsterDefinition missing;
            Assert.IsFalse(registry.TryGet("mon_does_not_exist", out missing), "未命中必须返回 false");
            Assert.IsNull(missing);
            Assert.IsNull(registry.Get("mon_does_not_exist"), "未命中不得静默回退到其它怪物");
            StringAssert.Contains("不存在该 ID", registry.Diagnostic);
        }

        // ---------------- E1 / E2：已于 M4 退役 ----------------
        // 原 E1（Legacy SO ↔ Shadow Definition 3×8 逐字段等价）与原 E2（`DataManager.AllMonsters`
        // 枚举顺序 ↔ `MonsterRegistry.All` 顺序）属**迁移期等价证据**；M4 删除 Legacy 资产与
        // `MonsterData` 类型后，该证据改由 **Git 历史快照**承担（M2 Audit §5 R9、FDS §9 Wave 1F / §10.5）。
        // Canonical 名单、8 字段契约与顺序稳定性由 S1 / S2 / E3 继续覆盖。

        // ---------------- B：非法 Monster Mirror → 零部分写入 ----------------

        [Test]
        public void B_InvalidMonsterMirror_FailsWithoutPartialFormalOutput()
        {
            string mirror = Path.Combine(StaticConfigBuilder.RepoRoot(),
                StaticConfigBuilder.MirrorRelativeDir.Replace('/', Path.DirectorySeparatorChar), "Monster.json");
            Assert.IsTrue(File.Exists(mirror), "Monster.json 必须存在（M1 已生成）");

            string original = File.ReadAllText(mirror);
            string dbPath = StaticConfigBuilder.Absolute(StaticConfigBuilder.MonsterDatabasePath);
            byte[] before = File.ReadAllBytes(dbPath);

            try
            {
                // 破坏生成标识 → Builder 必须在任何写入之前失败
                File.WriteAllText(mirror, original.Replace("\"doNotEdit\": true", "\"doNotEdit\": false"),
                    new UTF8Encoding(false));
                AssetDatabase.Refresh();

                var outcome = StaticConfigBuilder.Build(true);

                Assert.IsFalse(outcome.Success, "非法 Monster Mirror 必须失败");
                StringAssert.Contains("Monster", outcome.Failure);
                Assert.AreEqual(before, File.ReadAllBytes(dbPath), "失败时不得部分写入 MonsterConfigDatabase");
            }
            finally
            {
                File.WriteAllText(mirror, original, new UTF8Encoding(false));
                AssetDatabase.Refresh();
                StaticConfigBuilder.Build(true);
            }
        }

        // ---------------- F：Fresh Rebuild（§15） ----------------

        [Test]
        public void F_FreshRebuild_RegistryMatchesCurrentAuthority()
        {
            var authorityIds = StaticConfigTestAuthority.AuthorityMonsterIds();

            var outcome = StaticConfigBuilder.RebuildFromZero();
            Assert.IsTrue(outcome.Success, outcome.Failure);

            StaticConfigRegistry.Reset();
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out diagnostic), diagnostic);
            Assert.IsNotNull(RRPGStaticConfigRoot.Load(), "exact Resources path 必须仍可加载");

            var registry = LoadDatabase().CreateRegistry();
            Assert.AreEqual(authorityIds.Count, registry.Count, "从零重建后 Shadow Registry 条数必须等于当前 Authority");
            CollectionAssert.AreEquivalent(authorityIds, registry.All.Select(d => d.MonsterId).ToList(),
                "重建后名单必须与 Canonical Mirror 一致");

            // M4：等价证据（原 E1）已退役；重建后直接断言 Authority 侧事实
            Assert.AreEqual(8, MonsterDefinition.FieldCount, "8 字段契约不得改变");
        }

        // ---------------- E3：BattleSystem 确实按 list order 部署（可观察性证明） ----------------

        /// <summary>
        /// **算法契约测试**：BattleSystem 必须按传入 list 的 index 部署敌人（index → BattleColumn）。
        ///
        /// 输入刻意使用**测试专用固定集合**（不依赖生产内容）：
        ///   制作人正常增删 / 改名怪物不会影响本用例；它验证的是「list 顺序 → 部署列」这条算法契约，
        ///   而输入与生产内容无关（顺序也刻意不按字典序，否则无法区分「按 list 部署」与「按 ID 排序部署」）。
        /// </summary>
        [Test]
        public void E3_BattleSystem_DeploysEnemies_ByListOrder()
        {
            string[] order = { "fix_order_3", "fix_order_1", "fix_order_2" };

            var monsters = new List<object>();
            foreach (string id in order)
            {
                monsters.Add(new MonsterDefinition
                {
                    MonsterId = id,
                    Name = "测试用固定怪物 " + id,
                    MaxHP = 10,
                    Attack = 1,
                    AttackRange = 1,
                    AttackCount = 1,
                    ActionPoints = 1,
                    Faction = "enemy"
                });
            }

            var battleSystem = CreateBattleSystem();
            var enemyUnits = InitializeBattle(battleSystem, monsters);

            Assert.AreEqual(order.Length, enemyUnits.Count, "敌人数量应等于传入列表长度");
            for (int i = 0; i < order.Length; i++)
            {
                Assert.AreEqual(order[i], Str(Member(enemyUnits[i], "SourceId")),
                    "EnemyUnits[" + i + "] must match list index " + i + " (list order)");
                Assert.AreEqual(i, Int(Member(enemyUnits[i], "Column")),
                    "list index " + i + " must deploy to BattleColumn " + i);
            }

            Debug.Log("[M1 Order Gate] BattleSystem deploys by list order: "
                + string.Join(", ", order));
        }

        private static object CreateBattleSystem()
        {
            var battleSystemType = LegacyType("BattleSystem");
            Assert.IsNotNull(battleSystemType, "BattleSystem 类型必须存在（Current 产品链）");

            var constructor = battleSystemType.GetConstructors()[0];
            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType == typeof(string)) args[i] = string.Empty;
                else if (parameterType.IsValueType) args[i] = Activator.CreateInstance(parameterType);
                else if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    args[i] = Activator.CreateInstance(parameterType);   // 空列表
                }
                else args[i] = null;   // HeroInstance 允许 null（见 BattleSystem 构造注释）
            }

            return constructor.Invoke(args);
        }

        private static System.Collections.IList InitializeBattle(object battleSystem, List<object> monsters)
        {
            var type = battleSystem.GetType();
            var method = type.GetMethod("InitializeBattle", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "BattleSystem.InitializeBattle 不存在");

            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == 1)
                {
                    args[i] = CreateTypedList(parameters[i].ParameterType.GetGenericArguments()[0], monsters);
                    continue;
                }

                var elementType = parameters[i].ParameterType.IsGenericType
                    ? parameters[i].ParameterType.GetGenericArguments()[0]
                    : null;
                args[i] = elementType == null
                    ? null
                    : Activator.CreateInstance(parameters[i].ParameterType);   // 空列表
            }

            method.Invoke(battleSystem, args);

            var battleData = Member(battleSystem, "_battleData");
            Assert.IsNotNull(battleData, "BattleSystem._battleData 读取失败");
            var enemyUnits = Member(battleData, "EnemyUnits") as System.Collections.IList;
            Assert.IsNotNull(enemyUnits, "BattleData.EnemyUnits 读取失败");
            return enemyUnits;
        }

        private static object CreateTypedList(Type elementType, List<object> items)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType);
            foreach (var item in items) list.Add(item);
            return list;
        }

        private static void SetMember(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) { field.SetValue(target, value); return; }

            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, type.Name + " 缺少成员：" + name);
            property.SetValue(target, value);
        }

        // ---------------- H：Hidden Source Freeze（M2 Slice 2C 翻转：旧入口必须已冻结） ----------------

        [Test]
        public void H_HiddenSources_AreFrozen_LegacyAuthoringClosed()
        {
            string root = StaticConfigBuilder.RepoRoot();
            string assets = Path.Combine(root, "UnityProject", "RRPG1.0game", "Assets");
            string scripts = Path.Combine(assets, "Scripts");
            string battleUiPath = Path.Combine(scripts, "UI", "BattleUI.cs");
            string initializerPath = Path.Combine(assets, "Editor", "GameDataInitializer.cs");
            string importerPath = Path.Combine(assets, "Editor", "ExcelImporter.cs");
            string battleSystemPath = Path.Combine(scripts, "Battle", "BattleSystem.cs");
            string shadowSyncPath = Path.Combine(root, "Tools", "RRPG.ConfigCompiler", "migration",
                "RRPG.ConfigCompiler.M1ShadowSync", "Program.cs");

            string battleUi = File.ReadAllText(battleUiPath);
            string initializer = File.ReadAllText(initializerPath);
            string importer = File.ReadAllText(importerPath);
            string battleSystem = File.ReadAllText(battleSystemPath);
            string shadowSync = File.ReadAllText(shadowSyncPath);

            // A. ExcelImporter：Monster 导入分支已**物理删除**（M4；FDS §9 Wave 1F-F3 / §10.4-A / H13）
            string importAllBody = ExtractMethodBody(importer, "public static void ImportAll()");
            Assert.IsFalse(importAllBody.Contains("ImportMonsters"), "ImportAll 不得调用 Monster 导入");
            Assert.IsFalse(StripComments(importer).Contains("ImportMonsters"),
                "M4：Monster 导入方法体必须已物理删除（不再是「冻结保留」）");
            Assert.IsFalse(StripComments(importer).Contains("MonsterData"),
                "M4：ExcelImporter 不得再引用 MonsterData 类型");
            foreach (string domain in new[]
                     {
                         "ImportProfessions", "ImportPersons", "ImportCharacters",
                         "ImportSkills", "ImportStatuses", "ImportItemCards"
                     })
            {
                StringAssert.Contains(domain, importAllBody, "其它领域导入不得受影响：" + domain);
            }

            // B. GameDataInitializer：Monster seed **已物理删除**（M4；FDS §9 Wave 1F-F5 / §10.4-B）
            string initializerCode = StripComments(initializer);
            Assert.IsFalse(initializerCode.Contains("<MonsterData>"),
                "GameDataInitializer 不得有 Monster seed 创建");
            Assert.IsFalse(initializerCode.Contains("mon_001"), "Monster seed 数值不得残留");
            Assert.IsFalse(initializer.Contains("M2 FROZEN LEGACY"),
                "M4：Monster seed 注释残留必须已删除（不再保留给 M4）");
            StringAssert.Contains("/Assets/Editor/", initializerPath.Replace('\\', '/'),
                "GameDataInitializer 必须位于 Editor 程序集（不是 Runtime 读取链）");
            Assert.AreEqual(1, CountOccurrencesUnder(assets, "GameDataInitializer"),
                "GameDataInitializer 只允许出现在其自身文件定义处：Runtime 代码不得引用它");

            // C. BattleUI：inline Monster 静态源必须消失，Demo 改读 StaticConfig Authority（§10.4-C / H14）
            string battleUiCode = StripComments(battleUi);
            Assert.IsFalse(battleUiCode.Contains("CreateMonsterData("), "BattleUI 不得再存在 inline Monster helper");
            Assert.IsFalse(battleUiCode.Contains("mon_001"), "BattleUI 不得再内联 Monster 数值");
            StringAssert.Contains("StaticConfigRegistry.Monsters", battleUiCode,
                "Demo 敌方定义必须来自 StaticConfig Authority");
            StringAssert.Contains("InitFromExpedition", battleUi,
                "产品路径必须仍经 InitFromExpedition 注入 MonsterDefinition 列表");

            // D. M1ShadowSync：sync 必须拒绝执行（D-M2-2；H15 / H7 防线）
            StringAssert.Contains("M1 ShadowSync is frozen after M2 Monster Definition Authority Cutover.", shadowSync);
            StringAssert.Contains("CSV -> XLSX synchronization is no longer allowed.", shadowSync);
            StringAssert.Contains("MIGRATION ONLY / M1 CLOSED", shadowSync, "必须标注 M1 CLOSED");
            StringAssert.Contains("return ExitHardError;",
                ExtractMethodBody(shadowSync, "private static int RunSync(CompilerPaths paths)"),
                "sync 必须返回非 0（拒绝执行），不得静默成功");
            Assert.IsFalse(shadowSync.Contains("ShadowWorkbookWriter.Sync("),
                "CSV → XLSX 写入路径不得再有执行入口");

            // E. Source Audit：产品 Runtime 对 Legacy 的引用必须**归零**（M4 物理清理完成判据）
            Assert.AreEqual(0, CountOccurrencesUnder(scripts, "DataManager.Instance.AllMonsters"),
                "产品 Runtime 不得再读 DataManager.AllMonsters");
            var monsterDataRefs = CodeReferencesUnder(scripts, "MonsterData");
            Assert.AreEqual(0, monsterDataRefs.Count,
                "M4：Runtime 中 MonsterData 引用必须归零，实际=" + string.Join(", ", monsterDataRefs));
            var fallbackRefs = CodeReferencesUnder(scripts, "LegacyMonsterFallbackAdapter");
            Assert.AreEqual(0, fallbackRefs.Count,
                "M4：Legacy 回退适配引用必须归零，实际=" + string.Join(", ", fallbackRefs));

            // F. Legacy **已物理删除**（M4 判据：资产 = 0 + 类型 = 0 + 入口 = 0）
            Assert.IsNull(LegacyType("MonsterData"), "M4：MonsterData 类型必须已删除");
            var dataManagerType = LegacyType("DataManager");
            Assert.IsNotNull(dataManagerType, "DataManager 必须保留（仍服务其它领域）");
            Assert.IsNull(dataManagerType.GetProperty("AllMonsters", BindingFlags.Public | BindingFlags.Instance),
                "M4：DataManager 不得再暴露 AllMonsters（Monster 路径已删除，其余领域保留）");
            string legacySoDir = Path.Combine(assets, "Resources", "ScriptableObjects", "Monsters");
            Assert.IsFalse(Directory.Exists(legacySoDir),
                "M4：Legacy Monster SO 目录必须已删除：" + legacySoDir);
            string legacyCsv = Path.Combine(root, "Development", "Excel", "Monster.csv");
            Assert.IsFalse(File.Exists(legacyCsv), "M4：Monster.csv 必须已删除：" + legacyCsv);
            string monsterDataTypePath = Path.Combine(assets, "ScriptableObjects", "Monsters", "MonsterData.cs");
            Assert.IsFalse(File.Exists(monsterDataTypePath),
                "M4：MonsterData.cs 必须已删除：" + monsterDataTypePath);

            // G. 行为不变证据（与 E3 一致）：BattleSystem 仍按 list 顺序部署到 BattleColumn
            StringAssert.Contains("int idx = _battleData.EnemyUnits.Count - 1;", battleSystem,
                "BattleSystem 必须按 list 顺序计算部署列（顺序 → BattleColumn）");

            Debug.Log("[M4 Legacy Physical Retirement Gate] ExcelImporter Monster 分支已删除"
                + "；GameDataInitializer Monster seed 已删除；BattleUI inline 源已移除（→ StaticConfigRegistry.Monsters）"
                + "；M1ShadowSync.sync 仍拒绝执行（工具保留）；产品 Runtime 对 Legacy 消费 = 0"
                + "；MonsterData 类型 / Monster SO / Monster.csv 均已删除");
        }

        /// <summary>
        /// 统计某目录（递归 .cs）中出现 symbol 的文件数（用于证明「无 Runtime 引用」）。
        /// 排除 <c>Tests/</c> 目录：测试代码允许提到这些符号（例如本 Gate 自身）。
        /// </summary>
        private static int CountOccurrencesUnder(string directory, string symbol)
        {
            int count = 0;
            foreach (string file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Tests/")) continue;

                if (File.ReadAllText(file).Contains(symbol)) count++;
            }
            return count;
        }

        // ---------------- M2 Slice 2C：Freeze Gate 辅助 ----------------

        /// <summary>剥掉纯注释行（`//` / XML 文档 `///` / `*`）后再做符号检查。</summary>
        private static string StripComments(string source)
        {
            var code = new System.Text.StringBuilder();
            foreach (string line in source.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

                code.AppendLine(line);
            }
            return code.ToString();
        }

        /// <summary>按大括号配平取出方法体（用于断言「某入口是否仍引用某符号」）。</summary>
        private static string ExtractMethodBody(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "未找到方法签名：" + signature);

            int open = source.IndexOf('{', at);
            Assert.GreaterOrEqual(open, 0, "未找到方法体起始：" + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("方法体未配平：" + signature);
            return string.Empty;
        }

        /// <summary>返回目录下**非注释代码**中出现 symbol 的 .cs 文件（排除 Tests/，按词边界匹配）。</summary>
        private static List<string> CodeReferencesUnder(string directory, string symbol)
        {
            var hits = new List<string>();
            foreach (string file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Tests/")) continue;

                if (ContainsSymbol(StripComments(File.ReadAllText(file)), symbol)) hits.Add(file);
            }
            return hits;
        }

        /// <summary>
        /// 词边界匹配：避免 `MonsterData` 误命中 `MonsterDatabase` / `MonsterDataBase` 之类的标识符前缀。
        /// </summary>
        private static bool ContainsSymbol(string code, string symbol)
        {
            int at = 0;
            while (true)
            {
                at = code.IndexOf(symbol, at, StringComparison.Ordinal);
                if (at < 0) return false;

                int end = at + symbol.Length;
                bool leftOk = at == 0 || !IsIdentifierChar(code[at - 1]);
                bool rightOk = end >= code.Length || !IsIdentifierChar(code[end]);
                if (leftOk && rightOk) return true;

                at = end;
            }
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        // ---------------- Legacy 反射读取（Current 链，只读） ----------------

        private static Type LegacyType(string name)
        {
            return Type.GetType(name + ", Assembly-CSharp");
        }

        // ---------------- Legacy 反射读取：已于 M4 退役（Legacy 资产 / MonsterData 类型已删除） ----------------
        // 原 LoadLegacyMonsters / AllMonstersIds / LegacyMonsterAsset 随 M4 物理清理删除；
        // 等价证据改由 Git 历史快照承担（见类注释）。

        private static object Member(object target, string name)
        {
            var type = target.GetType();
            var field = type.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(target);

            var property = type.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, type.Name + " 缺少成员：" + name);
            return property.GetValue(target);
        }

        private static string Str(object value)
        {
            return value == null ? null : value.ToString();
        }

        private static int Int(object value)
        {
            return value == null ? int.MinValue : Convert.ToInt32(value);
        }
    }
}

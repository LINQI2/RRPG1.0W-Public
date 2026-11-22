using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using RRPG.Encounter;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEngine;
using UnityEngine.TestTools;

namespace RRPG.Encounter.Tests
{
    /// <summary>
    /// **WP-Encounter-1C 产品集成保护**（§15 C1–C9）。
    ///
    /// 与 1B Generator 契约测试的分工：
    ///   - 1B 测试证明「生成器能解出合法阵容」；
    ///   - 本组证明「产品层真的在消费这份阵容」：Prep 与 Battle 看到同一批敌人、同一实例身份、
    ///     同一逻辑阵型；1..9 只全部合法部署（不再只部署前 3 只）；同种怪多只互不串身份；
    ///     同 Node 重开不重抽；Resolve 后清除；生成失败不回退整个 Monster Registry；
    ///     EncounterDepth 按「已进入 Node 数量 - 1」推进。
    ///
    /// 访问方式（与既有 EditMode 契约测试一致）：BattleUI / BattleSystem / AdventureManager 位于
    /// 预定义程序集 Assembly-CSharp，测试程序集不编译期引用 → 统一用 System.Reflection 访问；
    /// Encounter 域与 StaticConfig 域是编译期引用（类型安全）。
    /// </summary>
    public class EncounterProductIntegrationTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        private const int FixedSeed = 20260925;

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "前置：静态配置生成根必须可构建");
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            StaticConfigRegistry.Reset();

            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        // ==================== 反射工具（Assembly-CSharp） ====================

        private static Type T(string typeName)
        {
            var type = Type.GetType(typeName + ", Assembly-CSharp");
            if (type != null) return type;

            // 兼容：部分产品类型位于 asmdef 程序集（例如 ExpeditionNodeOutcome ∈ ExpeditionProgression.Runtime）。
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName, false);
                if (type != null) return type;
            }

            Assert.Fail("未找到类型：" + typeName);
            return null;
        }

        private static MethodInfo MethodOf(object target, string method, int parameterCount)
        {
            foreach (var candidate in target.GetType().GetMethods(F))
            {
                if (candidate.Name == method && candidate.GetParameters().Length == parameterCount)
                    return candidate;
            }

            Assert.Fail("未找到方法：" + target.GetType().Name + "." + method + "/" + parameterCount + " 参数");
            return null;
        }

        private static MethodInfo StaticMethodOf(string typeName, string method, int parameterCount)
        {
            var type = T(typeName);
            foreach (var candidate in type.GetMethods(F))
            {
                if (candidate.Name == method && candidate.GetParameters().Length == parameterCount)
                    return candidate;
            }

            Assert.Fail("未找到静态方法：" + typeName + "." + method + "/" + parameterCount + " 参数");
            return null;
        }

        private static object Call(object target, string method, params object[] args)
        {
            return MethodOf(target, method, args.Length).Invoke(target, args);
        }

        private static object CallStatic(string typeName, string method, params object[] args)
        {
            return StaticMethodOf(typeName, method, args.Length).Invoke(null, args);
        }

        /// <summary>带 1 个 out 参数的实例调用。</summary>
        private static object CallWithOut(object target, string method, out object outValue, params object[] inArgs)
        {
            var info = MethodOf(target, method, inArgs.Length + 1);
            var args = new object[inArgs.Length + 1];
            Array.Copy(inArgs, args, inArgs.Length);

            object result = info.Invoke(target, args);
            outValue = args[inArgs.Length];
            return result;
        }

        /// <summary>带 2 个 out 参数的静态调用（TryBuildEnemyUnitPlan / TryBuildEnemyRosterPrepPlan）。</summary>
        private static bool CallStaticWithTwoOut(string typeName, string method,
            out object out1, out object out2, params object[] inArgs)
        {
            var info = StaticMethodOf(typeName, method, inArgs.Length + 2);
            var args = new object[inArgs.Length + 2];
            Array.Copy(inArgs, args, inArgs.Length);

            object result = info.Invoke(null, args);
            out1 = args[inArgs.Length];
            out2 = args[inArgs.Length + 1];
            return (bool)result;
        }

        /// <summary>带 2 个 out 参数的实例调用（TryGetActiveEnemyRoster / TryBuildEnemyPrepPlan）。</summary>
        private static bool CallWithTwoOut(object target, string method,
            out object out1, out object out2, params object[] inArgs)
        {
            var info = MethodOf(target, method, inArgs.Length + 2);
            var args = new object[inArgs.Length + 2];
            Array.Copy(inArgs, args, inArgs.Length);

            object result = info.Invoke(target, args);
            out1 = args[inArgs.Length];
            out2 = args[inArgs.Length + 1];
            return (bool)result;
        }

        private static object GetMember(object target, string name)
        {
            Assert.IsNotNull(target, "成员读取目标为 null：" + name);
            var type = target.GetType();

            var field = type.GetField(name, F);
            if (field != null) return field.GetValue(target);

            var property = type.GetProperty(name, F);
            if (property != null) return property.GetValue(target, null);

            Assert.Fail("成员不存在：" + type.Name + "." + name);
            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, F);
            if (field != null) { field.SetValue(target, value); return; }

            var property = type.GetProperty(name, F);
            Assert.IsNotNull(property, "成员不存在：" + type.Name + "." + name);
            property.SetValue(target, value, null);
        }

        // ==================== 夹具（纯测试数据） ====================

        /// <summary>3 排 × perRow 人的夹具（每排一个 Row 模式 Slot；怪物全为 fx_ 前缀）。</summary>
        private static EncounterFixture GridFixture(int perRow, bool allowDuplicate = true)
        {
            var fixture = new EncounterFixture()
                .Encounter(0, 100, allowDuplicate: allowDuplicate)
                .Monster("fx_a", 1)
                .Monster("fx_b", 1)
                .Monster("fx_c", 1);

            fixture.Slot("fx_slot_front", "Query", perRow, perRow, "Row", "Front");
            fixture.Slot("fx_slot_middle", "Query", perRow, perRow, "Row", "Middle");
            fixture.Slot("fx_slot_back", "Query", perRow, perRow, "Row", "Back");
            return fixture;
        }

        private static EnemyRoster Generate(EncounterFixture fixture, out IReadOnlyList<MonsterDefinition> catalog,
            int seed = FixedSeed)
        {
            var input = fixture.Build();
            catalog = input.Monsters;

            var generator = new EncounterGenerator(input);
            var result = generator.GenerateEncounter(fixture.EncounterId, seed);

            Assert.IsTrue(result.Success, "夹具遭遇必须可解：" + result.Failure + " —— " + result.Diagnostic);
            Assert.IsNotNull(result.Roster, "成功必须带完整 EnemyRoster");
            return result.Roster;
        }

        private static List<string> RosterKeys(EnemyRoster roster)
        {
            var keys = new List<string>();
            foreach (var entry in roster.Entries)
                keys.Add(entry.EnemyInstanceId + "|" + entry.MonsterId + "|" + entry.Row + "|" + entry.Column);
            return keys;
        }

        /// <summary>把任意「含 EnemyInstanceId/MonsterId/Row/Column 四项」的计划列表转成可比对的键。</summary>
        private static List<string> PlanKeys(IEnumerable plan)
        {
            var keys = new List<string>();
            foreach (var item in plan)
            {
                keys.Add(GetMember(item, "EnemyInstanceId") + "|" + GetMember(item, "MonsterId")
                    + "|" + GetMember(item, "Row") + "|" + GetMember(item, "Column"));
            }
            return keys;
        }

        private static void AssertSortedByRowThenColumn(IEnumerable plan, string what)
        {
            int previousRow = int.MinValue;
            int previousColumn = int.MinValue;
            int index = 0;

            foreach (var item in plan)
            {
                int row = Convert.ToInt32(GetMember(item, "Row"), System.Globalization.CultureInfo.InvariantCulture);
                int column = Convert.ToInt32(GetMember(item, "Column"), System.Globalization.CultureInfo.InvariantCulture);

                bool ordered = row > previousRow || (row == previousRow && column >= previousColumn);
                Assert.IsTrue(ordered, what + " 必须按 排 → 列 稳定排序（第 " + index + " 项乱序）");

                previousRow = row;
                previousColumn = column;
                index++;
            }
        }

        // ==================== C1：Prep / Battle 同一份阵容 ====================

        [Test]
        public void C1_PrepAndBattleConsumeIdenticalRoster()
        {
            var roster = Generate(GridFixture(2), out var catalog);

            Assert.IsTrue(CallStaticWithTwoOut("BattleUI", "TryBuildEnemyRosterPrepPlan",
                out object prepOut, out object prepDiagnostic, roster, catalog),
                "Prep 必须能消费本场 EnemyRoster：" + prepDiagnostic);

            Assert.IsTrue(CallStaticWithTwoOut("BattleSystem", "TryBuildEnemyUnitPlan",
                out object battleOut, out object battleDiagnostic, catalog, roster),
                "Battle 必须能消费同一份 EnemyRoster：" + battleDiagnostic);

            var prepKeys = PlanKeys((IEnumerable)prepOut);
            var battleKeys = PlanKeys((IEnumerable)battleOut);

            CollectionAssert.AreEquivalent(prepKeys, battleKeys,
                "Prep 与 Battle 的 (EnemyInstanceId, MonsterId, 排, 列) 必须逐项一致；\nPrep:   "
                + string.Join(" / ", prepKeys) + "\nBattle: " + string.Join(" / ", battleKeys));

            Assert.AreEqual(roster.Count, prepKeys.Count, "Prep 敌人数必须等于本场阵容数");
            AssertSortedByRowThenColumn((IEnumerable)prepOut, "Prep 显示序");
        }

        // ==================== 战斗侧辅助 ====================

        private object NewBattleSystem()
        {
            var config = ScriptableObject.CreateInstance(T("InjuryProbabilityConfig"));
            _created.Add(config);

            return Activator.CreateInstance(T("BattleSystem"),
                new object[] { NewListOf("CharacterInstance"), null, config });
        }

        private IList NewListOf(string typeName)
        {
            return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T(typeName)));
        }

        /// <summary>
        /// 用**真实 BattleSystem** 消费 EnemyRoster（产品重载），返回 EnemyUnits。
        /// 同时断言：每只都在棋盘上有唯一格 —— 旧缺陷「只部署前 3 只、其余单位没有合法位置」
        /// 会在这里立刻失败。
        /// </summary>
        private IList DeployEnemiesViaBattleSystem(EnemyRoster roster, IReadOnlyList<MonsterDefinition> catalog)
        {
            var battleSystem = NewBattleSystem();

            object diagnostic;
            bool ok = (bool)CallWithOut(battleSystem, "InitializeBattleFromEnemyRoster", out diagnostic,
                NewListOf("CharacterCardData"), catalog, roster, NewListOf("SkillData"), NewListOf("SkillData"));
            Assert.IsTrue(ok, "敌方阵容入场必须成功：" + diagnostic);

            var data = Call(battleSystem, "GetBattleData");
            var enemyField = GetMember(data, "EnemyField");
            var units = (IList)GetMember(data, "EnemyUnits");

            var cells = new List<string>();
            foreach (var unit in units)
            {
                Assert.IsTrue((bool)Call(enemyField, "Contains", unit),
                    "敌方单位必须占据棋盘格（不得存在无合法位置的单位）：" + GetMember(unit, "UnitId"));
                cells.Add(GetMember(unit, "Row") + ":" + GetMember(unit, "Column"));
            }

            CollectionAssert.AllItemsAreUnique(cells, "敌方单位不得重叠占格：" + string.Join(" / ", cells));
            Assert.AreEqual(units.Count, ((IList)Call(enemyField, "GetAllUnits")).Count,
                "棋盘上的敌军数必须等于本场敌人数（创建 = 部署）");

            return units;
        }

        private static int IntMember(object target, string name)
        {
            return Convert.ToInt32(GetMember(target, name), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AssertRowCounts(IList units, int front, int middle, int back)
        {
            var counts = new int[3];
            foreach (var unit in units)
            {
                int row = IntMember(unit, "Row");
                Assert.IsTrue(row >= 0 && row <= 2, "排索引越界：" + row);
                counts[row]++;
            }

            CollectionAssert.AreEqual(new[] { front, middle, back }, counts, "每排人数必须正确");
        }

        private static void AssertPlanRowCounts(IEnumerable plan, int front, int middle, int back)
        {
            var counts = new int[3];
            foreach (var item in plan) counts[IntMember(item, "Row")]++;
            CollectionAssert.AreEqual(new[] { front, middle, back }, counts, "Prep 每排人数必须与阵容一致");
        }

        // ==================== C2：同种怪多只互不串身份 ====================

        [Test]
        public void C2_TwoIdenticalMonstersStayIndependentUnits()
        {
            var fixture = new EncounterFixture()
                .Encounter(0, 100, allowDuplicate: true)
                .Monster("fx_twin", 2);
            fixture.Slot("fx_slot_pair", "Query", 2, 2, "Row", "Front");

            var roster = Generate(fixture, out var catalog);
            Assert.AreEqual(2, roster.Count, "本场必须生成 2 只同种怪");
            Assert.AreEqual(roster.Entries[0].MonsterId, roster.Entries[1].MonsterId, "两只必须同 MonsterId");
            Assert.AreNotEqual(roster.Entries[0].EnemyInstanceId, roster.Entries[1].EnemyInstanceId,
                "同种怪两只必须不同 EnemyInstanceId");

            var units = DeployEnemiesViaBattleSystem(roster, catalog);
            Assert.AreEqual(2, units.Count);
            Assert.AreNotSame(units[0], units[1], "必须是两个独立 BattleUnit（不串 HP / 状态 / 身份）");
            Assert.AreNotEqual(GetMember(units[0], "UnitId"), GetMember(units[1], "UnitId"),
                "同种怪多只 UnitId 必须不同（= EnemyInstanceId）");
            Assert.AreEqual(GetMember(units[0], "SourceId"), GetMember(units[1], "SourceId"),
                "同种怪 SourceId 必须相同（= MonsterId 模板身份）");
            Assert.AreEqual(roster.Entries[0].EnemyInstanceId, GetMember(units[0], "UnitId"),
                "UnitId 必须 = EnemyInstanceId（本场身份）");
            Assert.AreEqual(roster.Entries[0].MonsterId, GetMember(units[0], "SourceId"),
                "SourceId 必须 = MonsterId（模板身份 / 美术 / AI 键）");
        }

        // ==================== C3 / C4：6 只、9 只全部合法部署 ====================

        [Test]
        public void C3_SixEnemiesAreAllCreatedAndDeployed()
        {
            var roster = Generate(GridFixture(2), out var catalog);
            Assert.AreEqual(6, roster.Count, "夹具必须产出 6 只（2 + 2 + 2）");

            var units = DeployEnemiesViaBattleSystem(roster, catalog);
            Assert.AreEqual(6, units.Count, "6 只必须全部创建（不得只部署前 3 只）");
            AssertRowCounts(units, 2, 2, 2);
        }

        [Test]
        public void C4_NineEnemiesFillThreeRowsWithoutOverlap()
        {
            var roster = Generate(GridFixture(3), out var catalog);
            Assert.AreEqual(9, roster.Count, "夹具必须产出 9 只（3 + 3 + 3）");

            var units = DeployEnemiesViaBattleSystem(roster, catalog);
            Assert.AreEqual(9, units.Count, "9 只必须全部创建并占领 3×3 棋盘");
            AssertRowCounts(units, 3, 3, 3);

            for (int row = 0; row < 3; row++)
            {
                var columns = new List<int>();
                foreach (var unit in units)
                    if (IntMember(unit, "Row") == row) columns.Add(IntMember(unit, "Column"));
                columns.Sort();
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, columns,
                    "第 " + row + " 排必须是 Left + Center + Right 且不重叠");
            }
        }

        // ==================== C5：1 / 2 / 3 人排的列规则 ====================

        private static List<EnemyRosterColumn> ColumnsOf(EnemyRoster roster, EnemyRosterRow row)
        {
            var columns = new List<EnemyRosterColumn>();
            foreach (var entry in roster.Entries)
                if (entry.Row == row) columns.Add(entry.Column);
            columns.Sort();
            return columns;
        }

        private static List<int> PlanColumnsOf(IEnumerable plan, int rowIndex)
        {
            var columns = new List<int>();
            foreach (var item in plan)
                if (IntMember(item, "Row") == rowIndex) columns.Add(IntMember(item, "Column"));
            columns.Sort();
            return columns;
        }

        [Test]
        public void C5_OneTwoThreeRowColumnsFollowFrozenRules()
        {
            var fixture = new EncounterFixture()
                .Encounter(0, 100, allowDuplicate: true)
                .Monster("fx_a", 1)
                .Monster("fx_b", 1)
                .Monster("fx_c", 1);
            fixture.Slot("fx_front1", "Query", 1, 1, "Row", "Front");
            fixture.Slot("fx_middle2", "Query", 2, 2, "Row", "Middle");
            fixture.Slot("fx_back3", "Query", 3, 3, "Row", "Back");

            var roster = Generate(fixture, out var catalog);

            CollectionAssert.AreEqual(new[] { EnemyRosterColumn.Center }, ColumnsOf(roster, EnemyRosterRow.Front),
                "1 人排 → Center");
            CollectionAssert.AreEqual(new[] { EnemyRosterColumn.Left, EnemyRosterColumn.Right },
                ColumnsOf(roster, EnemyRosterRow.Middle), "2 人排 → Left + Right");
            CollectionAssert.AreEqual(
                new[] { EnemyRosterColumn.Left, EnemyRosterColumn.Center, EnemyRosterColumn.Right },
                ColumnsOf(roster, EnemyRosterRow.Back), "3 人排 → Left + Center + Right");

            Assert.IsTrue(CallStaticWithTwoOut("BattleUI", "TryBuildEnemyRosterPrepPlan",
                out object prepOut, out object prepDiagnostic, roster, catalog),
                "Prep 必须能消费本场 EnemyRoster：" + prepDiagnostic);

            var prep = (IEnumerable)prepOut;
            AssertPlanRowCounts(prep, 1, 2, 3);
            CollectionAssert.AreEqual(new[] { 1 }, PlanColumnsOf(prep, 0), "Prep 1 人排必须 Center");
            CollectionAssert.AreEqual(new[] { 0, 2 }, PlanColumnsOf(prep, 1), "Prep 2 人排必须 Left + Right");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, PlanColumnsOf(prep, 2), "Prep 3 人排必须 Left + Center + Right");
        }

        // ==================== 产品（AdventureManager）辅助 ====================

        /// <summary>构造最小产品 AdventureManager（无 PlayerCollection：SeedRunState 走演示警告路径，可运行）。</summary>
        private object NewAdventureManager()
        {
            var monsters = StaticConfigRegistry.Monsters;
            Assert.IsNotNull(monsters, "前置：Monster Registry 必须可用（请先导表）");

            var cards = NewListOf("CharacterCardData");
            var card = (UnityEngine.Object)ScriptableObject.CreateInstance(T("CharacterCardData"));
            _created.Add(card);
            SetMember(card, "CardId", "pcard_1");
            SetMember(card, "CharacterId", "char_001");
            cards.Add(card);

            var adventure = Activator.CreateInstance(T("AdventureManager"),
                new object[] { null, cards, null, null, null, monsters.All, null });
            Assert.IsNotNull(adventure, "AdventureManager 构造失败");

            // 产品同构：确认出征后建立 Progression 会话（rate=0 → 任何节点都可负担）
            Call(adventure, "StartProgression", 4, 3, 1, 0, 0, null);
            Assert.IsNotNull(GetMember(adventure, "ProgressionState"), "Progression 会话必须初始化成功");

            return adventure;
        }

        /// <summary>进入当前第一个可点击节点（返回节点对象）；没有可进入节点时返回 null。</summary>
        private static object EnterFirstAvailableNode(object adventure, out string nodeId)
        {
            object node = null;
            foreach (var candidate in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
            {
                node = candidate;
                break;
            }

            if (node == null)
            {
                nodeId = null;
                return null;
            }

            nodeId = (string)GetMember(node, "NodeId");
            object enterResult = Call(adventure, "TryEnterNode", nodeId);
            Assert.AreEqual("Success", enterResult.ToString(), "进入节点必须成功：" + enterResult);
            return node;
        }

        private EnemyRoster GenerateActiveRoster(object adventure, out object activeEncounter, out string diagnostic)
        {
            object rosterOut = null;
            object diagnosticOut = null;

            bool ok = CallWithTwoOut(adventure, "TryGetActiveEnemyRoster", out rosterOut, out diagnosticOut);
            diagnostic = (string)diagnosticOut;
            Assert.IsTrue(ok, "必须能生成 Active EnemyRoster：" + diagnostic);

            activeEncounter = GetMember(adventure, "ActiveEncounter");
            Assert.IsNotNull(activeEncounter, "成功生成后必须存在 Active Encounter 状态");

            return (EnemyRoster)rosterOut;
        }

        private static object ResolveSuccess(object adventure)
        {
            var outcome = Enum.Parse(T("ExpeditionNodeOutcome"), "Success");
            object data = null;
            return CallWithOut(adventure, "TryResolveCurrentNode", out data, outcome);
        }

        private static List<string> AvailableNodeIds(object adventure)
        {
            var ids = new List<string>();
            foreach (var node in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
            {
                if (node == null) continue;
                ids.Add((string)GetMember(node, "NodeId"));
            }
            return ids;
        }

        private static string FirstAvailableNodeId(object adventure)
        {
            var ids = AvailableNodeIds(adventure);
            return ids.Count > 0 ? ids[0] : null;
        }

        private static string NodeKindOf(object adventure, string nodeId)
        {
            var graph = GetMember(GetMember(adventure, "ProgressionState"), "Graph");
            var method = graph.GetType().GetMethod("TryGetNode");
            var args = new object[] { nodeId, null };
            method.Invoke(graph, args);
            Assert.IsNotNull(args[1], "node 不存在：" + nodeId);
            return GetMember(args[1], "NodeKind").ToString();
        }

        private static int EnteredCount(object adventure)
        {
            var state = GetMember(adventure, "ProgressionState");
            var entered = (ICollection)Call(state, "GetEnteredNodeIds");
            return entered.Count;
        }

        private static int SupplyCarry(object adventure)
        {
            var state = GetMember(adventure, "ProgressionState");
            return (int)GetMember(GetMember(state, "Supply"), "CurrentCarryUnits");
        }

        // ==================== C6：同 Node 重开不重抽 ====================

        [Test]
        public void C6_ReopeningSameNodeDoesNotRerollRoster()
        {
            var adventure = NewAdventureManager();

            // 兼容池覆盖 seam：隔离「节点种类随机性」，使本用例只验证「同一 Node 复用同一份阵容」。
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", EncounterPoolSelector.NormalPoolId);

            EnterFirstAvailableNode(adventure, out string nodeId);

            var first = GenerateActiveRoster(adventure, out object firstState, out _);
            var second = GenerateActiveRoster(adventure, out object secondState, out _);

            Assert.AreSame(first, second, "同一 CurrentNode 必须复用同一份 EnemyRoster（Prep 重开 / Battle 开始不重抽）");
            Assert.AreSame(firstState, secondState, "Active Encounter 必须复用同一状态对象");
            Assert.AreEqual(GetMember(firstState, "Seed"), GetMember(secondState, "Seed"), "EncounterSeed 不得改变");
            Assert.AreEqual(nodeId, GetMember(firstState, "NodeId"));
            CollectionAssert.AreEqual(RosterKeys(first), RosterKeys(second), "EnemyInstanceId / MonsterId / 站位不得改变");
        }

        // ==================== C7：Resolve 清除 + 下一节点重新生成 ====================

        [Test]
        public void C7_ResolveClearsActiveRosterAndNextNodeGeneratesNewOne()
        {
            var adventure = NewAdventureManager();

            // 为隔离「节点种类随机性」：本用例只验证生命周期，使用兼容池覆盖 seam 指向普通池
            //（pool_test_general 覆盖 depth 0..3 / 1..99，任意前几层节点都可生成）。
            // 池深度窗口不覆盖时的**明确失败**语义由 C7b 单独守护。
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", EncounterPoolSelector.NormalPoolId);

            EnterFirstAvailableNode(adventure, out string firstNodeId);
            var firstRoster = GenerateActiveRoster(adventure, out object firstState, out _);

            Assert.AreEqual("Success", ResolveSuccess(adventure).ToString(), "当前节点必须以 Success 结算");
            Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "Resolve 后必须清除 Active EnemyRoster");

            EnterFirstAvailableNode(adventure, out string secondNodeId);
            Assert.IsNotNull(secondNodeId, "结算后必须还能进入下一个节点");
            Assert.AreNotEqual(firstNodeId, secondNodeId, "必须进入的是新节点");

            var secondRoster = GenerateActiveRoster(adventure, out object secondState, out _);
            Assert.AreNotSame(firstRoster, secondRoster, "下一个 Battle 类节点必须生成新阵容");
            Assert.AreEqual(secondNodeId, GetMember(secondState, "NodeId"));
            Assert.AreEqual(1, IntMember(secondState, "Depth"), "第二个节点 EncounterDepth 必须为 1");
        }

        [Test]
        public void C7b_PoolWithoutCandidateAtCurrentDepthFailsExplicitly()
        {
            LogAssert.ignoreFailingMessages = true;

            var adventure = NewAdventureManager();

            // V0.1 兼容映射下，精英 / Boss 节点用 deep 池；该池条目 MinDepth 分别为 2 / 5，
            // 因此 depth=0 时**没有任何候选** → 必须明确失败（不得回退普通池、不得回退整个 Registry）。
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", EncounterPoolSelector.DeepPoolId);
            EnterFirstAvailableNode(adventure, out string nodeId);

            object rosterOut = null;
            object diagnosticOut = null;
            bool ok = CallWithTwoOut(adventure, "TryGetActiveEnemyRoster", out rosterOut, out diagnosticOut);

            Assert.IsFalse(ok, "deep 池在 depth=0 没有候选时必须明确失败");
            Assert.IsNull(rosterOut, "失败时不得返回任何阵容（不得回退普通池 / 整个 Monster Registry）");
            Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "失败时不得建立 Active Encounter");

            var diagnostic = (string)diagnosticOut;
            StringAssert.Contains("NoCandidateEncounter", diagnostic, "诊断必须给出明确失败码");
            StringAssert.Contains(EncounterPoolSelector.DeepPoolId, diagnostic, "诊断必须指出使用的池");
            StringAssert.Contains(nodeId, diagnostic, "诊断必须指出发生问题的 NodeId");
        }

        // ==================== C9：EncounterDepth ====================

        [Test]
        public void C9_EncounterDepthCountsEveryEnteredNodeNotBattles()
        {
            Assert.AreEqual(0, EncounterDepthRule.DepthFor(0), "无节点 → 0");
            Assert.AreEqual(0, EncounterDepthRule.DepthFor(1), "第一节点 → 0");
            Assert.AreEqual(1, EncounterDepthRule.DepthFor(2), "第二节点 → 1");
            Assert.AreEqual(2, EncounterDepthRule.DepthFor(3), "第三节点 → 2");

            var adventure = NewAdventureManager();

            // 同 C7：用兼容池覆盖 seam 隔离「节点种类随机性」（普通池覆盖 depth 0..3 / 1..99）。
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", EncounterPoolSelector.NormalPoolId);

            var visitedKinds = new List<string>();
            int entered = 0;

            while (entered < 3)
            {
                object node = EnterFirstAvailableNode(adventure, out string nodeId);
                if (node == null) break;
                entered++;

                var roster = GenerateActiveRoster(adventure, out object state, out _);
                Assert.IsNotNull(roster);

                Assert.AreEqual(entered - 1, IntMember(state, "Depth"),
                    "进入第 " + entered + " 个节点时 EncounterDepth 必须 = 已进入节点数 - 1（与节点种类无关）");
                Assert.AreEqual(nodeId, GetMember(state, "NodeId"));

                visitedKinds.Add(GetMember(node, "NodeKind").ToString());

                Assert.AreEqual("Success", ResolveSuccess(adventure).ToString(), "节点结算必须成功");
            }

            Debug.Log("[Encounter 1C C9] 实际路线节点种类：" + string.Join(" → ", visitedKinds));
            Assert.GreaterOrEqual(entered, 2, "至少应能走过 2 个节点（验证 0 → 1）");
        }

        // ==================== C8：失败必须明确，不得回退整个 Monster Registry ====================

        [Test]
        public void C8_InvalidPoolFailsExplicitlyAndLeavesNoActiveEncounter()
        {
            LogAssert.ignoreFailingMessages = true;   // 该路径必须产生 LogError（产品要求）

            var adventure = NewAdventureManager();
            EnterFirstAvailableNode(adventure, out string nodeId);
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", "pool_this_does_not_exist");

            object rosterOut = null;
            object diagnosticOut = null;
            bool ok = CallWithTwoOut(adventure, "TryGetActiveEnemyRoster", out rosterOut, out diagnosticOut);

            Assert.IsFalse(ok, "指定的 Pool 不存在时必须明确失败");
            Assert.IsNull(rosterOut, "失败时不得返回任何阵容（更不得回退整个 Monster Registry）");
            Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "失败时不得建立 Active Encounter");
            Assert.IsTrue((bool)GetMember(adventure, "HasActiveEnemyRoster") == false, "失败时不得存在冻结阵容");

            var diagnostic = (string)diagnosticOut;
            StringAssert.Contains("PoolNotFound", diagnostic, "诊断必须给出明确失败码");
            StringAssert.Contains("pool_this_does_not_exist", diagnostic, "诊断必须指出缺失的 PoolId");
            StringAssert.Contains(nodeId, diagnostic, "诊断必须指出发生问题的 NodeId");
        }

        [Test]
        public void C8b_BattleUiRefusesToStartWithoutFrozenRoster_AndCatalogSizeIsNotAuthority()
        {
            LogAssert.ignoreFailingMessages = true;

            var host = new GameObject("EncounterProductTestHost", typeof(RectTransform));
            _created.Add(host);
            var battleUi = host.AddComponent(T("BattleUI"));
            Assert.IsNotNull(battleUi, "BattleUI 组件创建失败");

            // 目录里放满正式怪物（模拟「整个 Monster Registry 可用」）
            var catalog = new List<MonsterDefinition>(StaticConfigRegistry.Monsters.All);
            Assert.Greater(catalog.Count, 2, "前置：正式 Monster 目录应多于 2 只（用于区分「目录大小」与「本场敌人数」）");
            SetMember(battleUi, "_enemyMonsters", catalog);

            // 1) 明确失败表态（遭遇生成失败）→ 即使目录可用也不得开战
            Call(battleUi, "BindActiveEnemyRoster", new object[] { null });
            Assert.IsTrue((bool)GetMember(battleUi, "IsEnemyRosterMode"), "绑定后必须进入 Active EnemyRoster 权威模式");
            Assert.IsNull(GetMember(battleUi, "ActiveEnemyRoster"));

            Call(battleUi, "OnStartBattle");
            Assert.IsFalse((bool)GetMember(battleUi, "_battleStarted"),
                "敌方阵容未冻结时不得开战（不得回退整个 Monster Registry、不得生成默认 3 怪）");
            Assert.IsNull(GetMember(battleUi, "_battleSystem"), "不得创建半套 BattleSystem");

            // 2) 绑定合法阵容后：敌人数 = 阵容数（目录大小不再是 Authority）
            var fixture = new EncounterFixture()
                .Encounter(0, 100, allowDuplicate: true)
                .Monster("fx_a", 1)
                .Monster("fx_b", 1);
            fixture.Slot("fx_slot_pair", "Query", 2, 2, "Row", "Front");

            var roster = Generate(fixture, out var fixtureCatalog);
            Call(battleUi, "BindActiveEnemyRoster", roster);
            SetMember(battleUi, "_enemyMonsters", new List<MonsterDefinition>(fixtureCatalog));

            object rosterDiagnostic = null;
            bool valid = (bool)CallWithOut(battleUi, "TryValidateEnemyRosterForBattle", out rosterDiagnostic);
            Assert.IsTrue(valid, "合法阵容必须通过开战预检：" + rosterDiagnostic);

            var bound = (EnemyRoster)GetMember(battleUi, "ActiveEnemyRoster");
            Assert.AreEqual(2, bound.Count, "BattleUI 必须消费绑定的 EnemyRoster（本场敌人数 = 2）");
            Assert.AreNotEqual(2, catalog.Count, "对照：正式目录大小与「本场敌人数」不是同一个概念");
        }

        [Test]
        public void C8c_ProductEntryBindsFrozenRosterAndEnemyUiUsesInstanceId()
        {
            string scripts = Path.Combine(StaticConfigBuilder.RepoRoot(),
                "UnityProject", "RRPG1.0game", "Assets", "Scripts", "UI");

            string expeditionUi = File.ReadAllText(Path.Combine(scripts, "ExpeditionUI.cs"));
            StringAssert.Contains("TryPrepareAndEnterBattleNode", expeditionUi,
                "产品入口必须走「预检 → 预生成 → Enter → 提交」的原子进入（WP-Encounter-1C-R R1）");
            StringAssert.Contains("BindActiveEnemyRoster", expeditionUi,
                "产品入口必须把冻结阵容绑定给 BattleUI（Prep 与 Battle 消费同一份）");
            StringAssert.Contains("拒绝进入战斗节点", expeditionUi,
                "生成失败必须明确拒绝进入战斗节点（不得静默回退、不得空战斗、不得留下半进入状态）");
            Assert.IsFalse(expeditionUi.Contains("TryGetActiveEnemyRoster"),
                "产品入口不得再用「先 Enter 再生成」的旧路径（会产生 Entered 半状态）");

            // 敌方 UI 卡名不得再绑定到 SourceId（本场身份必须用 UnitId = EnemyInstanceId）
            string[] uiFiles = { "BattleUI.cs", "BattleUIEnemyIntent.cs", "BattleUIRulePipeline.cs" };
            foreach (string file in uiFiles)
            {
                string code = File.ReadAllText(Path.Combine(scripts, file));
                Assert.IsFalse(code.Contains("\"Card_\" + u.SourceId") || code.Contains("\"Card_\" + unit.SourceId"),
                    file + " 不得再把敌方 UI 卡名绑定到 SourceId（同种怪多只会互相覆盖 / 点击错单位）");
            }
        }

        // ==================== WP-Encounter-1C-R（R1）：Enter 原子性 ====================

        /// <summary>
        /// **R1 长期契约**：Encounter 生成失败**不得**把 Progression 留在 Entered（不得产生半进入状态）。
        ///
        /// 构造「合法可进入的 Battle Node + 明确无法生成的 Pool」，调用产品入口
        /// `TryPrepareAndEnterBattleNode`，验证零 mutation：Phase / CurrentNodeId / 已进入集合 /
        /// Supply / elapsed 全部不变、ActiveEncounter == null；随后同一节点换回正常池仍可进入（不要求 Retreat）。
        /// </summary>
        [Test]
        public void R1_EncounterGenerationFailureDoesNotEnterNode()
        {
            LogAssert.ignoreFailingMessages = true;   // 该失败路径必须产生 LogError（产品要求）

            var adventure = NewAdventureManager();

            // 合法可进入：起始 Node（MapGenerator 保证为 Battle），但兼容池 override 指向不存在的池
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", "pool_this_does_not_exist");
            string nodeId = FirstAvailableNodeId(adventure);
            Assert.IsNotNull(nodeId, "必须存在可进入的节点");

            var state = GetMember(adventure, "ProgressionState");
            string phaseBefore = GetMember(state, "Phase").ToString();
            object currentNodeBefore = GetMember(state, "CurrentNodeId");
            int enteredBefore = EnteredCount(adventure);
            int supplyBefore = SupplyCarry(adventure);
            int elapsedBefore = IntMember(adventure, "ExpeditionElapsedDays");

            object rosterOut = null;
            object diagnosticOut = null;
            bool ok = CallWithTwoOut(adventure, "TryPrepareAndEnterBattleNode",
                out rosterOut, out diagnosticOut, nodeId);

            Assert.IsFalse(ok, "Encounter 生成失败时原子进入必须返回失败");
            Assert.IsNull(rosterOut, "失败时不得返回任何阵容（更不得回退整个 Monster Registry）");

            // ---- 零 mutation 断言 ----
            Assert.AreEqual(phaseBefore, GetMember(state, "Phase").ToString(), "ProgressionState.Phase 不得改变");
            Assert.AreEqual(currentNodeBefore, GetMember(state, "CurrentNodeId"), "CurrentNodeId 不得改变");
            Assert.AreEqual(enteredBefore, EnteredCount(adventure), "GetEnteredNodeIds() 不得增加");
            Assert.IsFalse((bool)Call(state, "IsNodeEntered", nodeId), "该节点不得处于 Entered");
            Assert.AreEqual(supplyBefore, SupplyCarry(adventure), "Supply 不得改变");
            Assert.AreEqual(elapsedBefore, IntMember(adventure, "ExpeditionElapsedDays"), "elapsed 不得改变");
            Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "不得留下半成品 ActiveEncounter");
            Assert.IsFalse((bool)GetMember(adventure, "HasActiveEnemyRoster"), "不得存在冻结阵容");

            var diagnostic = (string)diagnosticOut;
            StringAssert.Contains("PoolNotFound", diagnostic, "诊断必须给出明确失败码");
            StringAssert.Contains(nodeId, diagnostic, "诊断必须指出目标 NodeId");
            StringAssert.Contains("节点未被进入", diagnostic, "诊断必须明确「节点未被进入」");

            // ---- 玩家未被卡死：换回正常池后同一节点仍可进入（不需 Retreat） ----
            SetMember(adventure, "EncounterPoolOverrideForDiagnostics", EncounterPoolSelector.NormalPoolId);

            object rosterRetry = null;
            object diagnosticRetry = null;
            bool retried = CallWithTwoOut(adventure, "TryPrepareAndEnterBattleNode",
                out rosterRetry, out diagnosticRetry, nodeId);

            Assert.IsTrue(retried, "失败后节点必须仍可正常进入（不要求 Retreat）：" + diagnosticRetry);
            Assert.IsNotNull((EnemyRoster)rosterRetry);
            Assert.IsTrue((bool)Call(state, "IsNodeEntered", nodeId), "重试成功后节点应已进入");
            Assert.AreEqual(nodeId, GetMember(GetMember(adventure, "ActiveEncounter"), "NodeId"));
        }

        // ==================== WP-Encounter-1C-R（R2）：兼容池覆盖 ====================

        /// <summary>
        /// **R2 长期契约（内容窗口）**：当前 V0.1 默认 Legacy 4×3 Compatibility 路线涉及的
        /// Battle-like depth 范围内，两个 selector 使用的池都不得因 PoolEntry 深度窗口产生无候选窗口。
        ///
        /// 边界：本用例只保护**当前兼容产品链**，不作为未来无限远征正式 Depth 设计。
        /// </summary>
        [Test]
        public void R2_CompatibilityPoolCoverage_DefaultFourByThreePath()
        {
            string diagnostic;
            var input = EncounterGenerationInput.FromStaticConfig(out diagnostic);
            Assert.IsNotNull(input, "正式配置必须可用：" + diagnostic);

            var generator = new EncounterGenerator(input);
            int[] seeds = { 1, 20260925 };

            // 默认 4×3 路线的深度范围：0 = 起始 Node（MapGenerator 固定 Battle → Normal 池）；
            // 1..3 = 普通 / 精英行；4 = Boss（→ Deep 池）。
            for (int depth = 0; depth <= 4; depth++)
            {
                foreach (int seed in seeds)
                {
                    var normal = generator.GenerateFromPool(EncounterPoolSelector.NormalPoolId, depth, seed);
                    Assert.IsTrue(normal.Success, "Normal 池在 depth=" + depth + " 必须可生成（seed=" + seed + "）："
                        + normal.Failure + " —— " + normal.Diagnostic);
                }
            }

            // Elite 可在 depth = 1 出现、Boss 在 depth = 4：Deep 池必须覆盖 1..5，
            // 且 depth=1 不得出现 NoCandidateEncounter。
            for (int depth = 1; depth <= 5; depth++)
            {
                foreach (int seed in seeds)
                {
                    var deep = generator.GenerateFromPool(EncounterPoolSelector.DeepPoolId, depth, seed);
                    Assert.AreNotEqual(EncounterGenerationFailure.NoCandidateEncounter, deep.Failure,
                        "Deep 池在 depth=" + depth + " 不得出现「没有候选遭遇」（Elite / Boss 兼容覆盖；seed=" + seed + "）");
                    Assert.IsTrue(deep.Success, "Deep 池在 depth=" + depth + " 必须可生成（seed=" + seed + "）："
                        + deep.Failure + " —— " + deep.Diagnostic);
                }
            }

            // 窗口承诺：Deep 池至少有一个浅层（MinDepth ≤ 1）条目 —— 即 R2 的最小内容修复。
            bool deepHasShallowEntry = false;
            foreach (var entry in input.PoolEntries)
            {
                if (entry == null || entry.PoolId != EncounterPoolSelector.DeepPoolId) continue;
                if (entry.MinDepth <= 1) deepHasShallowEntry = true;
            }
            Assert.IsTrue(deepHasShallowEntry, "Deep 池必须至少有一个 MinDepth ≤ 1 的条目（浅层兼容覆盖）");
        }

        /// <summary>
        /// **R2 长期契约（产品链）**：沿默认 4×3 路线实际走一遍，凡 Battle 类节点都必须能
        /// 「原子进入 + 生成遭遇」，不得因深度窗口出现纯内容软锁（优先选 Boss / Elite 以最大化覆盖）。
        /// </summary>
        [Test]
        public void R2b_ProductPath_BattleLikeNodesAlwaysOpen()
        {
            var adventure = NewAdventureManager();
            var visited = new List<string>();

            for (int step = 0; step < 5; step++)
            {
                var candidates = new List<KeyValuePair<int, string>>();
                var kinds = new Dictionary<string, string>();

                foreach (var id in AvailableNodeIds(adventure))
                {
                    string kind = NodeKindOf(adventure, id);
                    kinds[id] = kind;
                    candidates.Add(new KeyValuePair<int, string>(NodePreference(kind), id));
                }

                if (candidates.Count == 0) break;
                candidates.Sort((a, b) => a.Key.CompareTo(b.Key));

                string chosen = candidates[0].Value;
                string chosenKind = kinds[chosen];
                bool battleLike = chosenKind == "Battle" || chosenKind == "Elite" || chosenKind == "Boss";

                if (battleLike)
                {
                    object rosterOut = null;
                    object diagnosticOut = null;
                    bool ok = CallWithTwoOut(adventure, "TryPrepareAndEnterBattleNode",
                        out rosterOut, out diagnosticOut, chosen);

                    Assert.IsTrue(ok, "Battle 类节点必须能进入（kind=" + chosenKind + "）：" + diagnosticOut);
                    Assert.IsNotNull((EnemyRoster)rosterOut);
                    Assert.AreEqual(chosen, GetMember(GetMember(adventure, "ActiveEncounter"), "NodeId"));
                    visited.Add(chosenKind);
                }
                else
                {
                    // 非战斗内容节点保持既有语义（占位内容 → 立即 Success 结算）
                    Assert.AreEqual("Success", Call(adventure, "TryEnterNode", chosen).ToString(),
                        "非战斗节点应可进入：" + chosen);
                    visited.Add(chosenKind + "(占位)");
                }

                Assert.AreEqual("Success", ResolveSuccess(adventure).ToString(), "节点结算必须成功");
            }

            Debug.Log("[Encounter 1C-R R2b] 默认 4×3 路线实际节点：" + string.Join(" → ", visited));
            Assert.GreaterOrEqual(visited.Count, 2, "至少应能连续走过 2 个节点");
        }

        private static int NodePreference(string kind)
        {
            if (kind == "Boss") return 0;
            if (kind == "Elite") return 1;
            if (kind == "Battle") return 2;
            return 3;
        }
    }
}

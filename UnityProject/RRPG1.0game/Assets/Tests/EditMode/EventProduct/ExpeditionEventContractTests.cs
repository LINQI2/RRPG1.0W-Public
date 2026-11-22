using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using UnityEngine;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D2：**远征事件产品切换**的长期契约（不依赖 Scene 的部分）。
    ///
    /// 覆盖（对应 D2 Spec §19 / §20 / §21 的契约）：
    ///   E-trigger：远征触发时机与稳定派生（TriggerKey / ResolutionSeed 确定性，UI 重开不重抽）；
    ///   E17：§18 内容覆盖守卫（默认产品图必然存在浅层 Event/Shop/Recruit 节点 →
    ///        必须存在「Repeatable 且无深度下界」的远征事件候选，否则正常产品必卡）；
    ///   E4：**No Placeholder Fallback** 源码级守卫（正式路径不得残留占位 Continue）；
    ///   分类：正式 Event 节点在 Journey 表现层必须被分类为 `JourneyEncounterKind.Event`。
    ///
    /// 真实产品生命周期契约（原子进入 / 冻结 / 结算 / StartBattle / exactly-once）由
    /// PlayMode 真实产品链（`ExpeditionEventProductChainPlayTests`）覆盖 —— 单一契约不在两处重复实现。
    /// </summary>
    public class ExpeditionEventContractTests
    {
        private const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static Type Cs(string name)
        {
            var type = Type.GetType(name + ", Assembly-CSharp");
            Assert.IsNotNull(type, "类型不存在：" + name + "（Assembly-CSharp）");
            return type;
        }

        /// <summary>
        /// 按简单名在**已加载程序集**中查找类型（用于不属于 Assembly-CSharp 的表现层类型：
        /// `JourneyEncounterKind` 属 `Assets/Scripts/Journey/Runtime` 的 asmdef）。
        /// </summary>
        private static Type Loaded(string simpleName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(simpleName, false);
                if (type != null) return type;
            }

            Assert.Fail("已加载程序集中找不到类型：" + simpleName);
            return null;
        }

        private static MethodInfo Method(Type type, string name, int parameterCount)
        {
            foreach (var m in type.GetMethods(Flags))
            {
                if (m.Name != name) continue;
                if (m.GetParameters().Length == parameterCount) return m;
            }

            Assert.Fail("方法不存在：" + type.Name + "." + name + "/" + parameterCount);
            return null;
        }

        private static string SourcePath(string relativeUnderScripts)
        {
            return Path.Combine(Application.dataPath, relativeUnderScripts.Replace('/', Path.DirectorySeparatorChar));
        }

        // ==================== E-trigger：触发时机 / 稳定派生 ====================

        [Test]
        public void E_trigger_ExpeditionEventFlow_UsesFrozenExpeditionTrigger()
        {
            var type = Cs("ExpeditionEventFlow");
            var field = type.GetField("TriggerType", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, "ExpeditionEventFlow.TriggerType 必须存在（正式触发时机的唯一来源）");

            Assert.AreEqual(ExpeditionTrigger, field.GetValue(null),
                "远征 Event Node 必须使用 EventTriggerTypes.ExpeditionEventNodeReached（不得自造触发时机）");

            Assert.IsTrue(EventTriggerTypes.IsKnown(ExpeditionTrigger), "触发时机必须在 Event Core 已知词表内");
            Assert.AreEqual("Expedition", EventTriggerTypes.ResolveDomain(ExpeditionTrigger), "必须推导为 Expedition 域");
        }

        [Test]
        public void E_determinism_TriggerKeyAndResolutionSeed_AreStable_AndNodeSpecific()
        {
            var flow = Cs("ExpeditionEventFlow");

            var buildKey = Method(flow, "BuildTriggerKey", 1);
            var deriveSeed = Method(flow, "DeriveResolutionSeed", 3);

            string keyA1 = (string)buildKey.Invoke(null, new object[] { "node_7" });
            string keyA2 = (string)buildKey.Invoke(null, new object[] { "node_7" });
            string keyB = (string)buildKey.Invoke(null, new object[] { "node_8" });

            Assert.AreEqual(keyA1, keyA2, "同 Node 的 TriggerKey 必须稳定（UI 重开 / 卡片刷新不重抽）");
            Assert.AreNotEqual(keyA1, keyB, "不同 Node 必须得到不同 TriggerKey");

            int seedA1 = (int)deriveSeed.Invoke(null, new object[] { 12345, "node_7", ExpeditionTrigger });
            int seedA2 = (int)deriveSeed.Invoke(null, new object[] { 12345, "node_7", ExpeditionTrigger });
            int seedOtherNode = (int)deriveSeed.Invoke(null, new object[] { 12345, "node_8", ExpeditionTrigger });
            int seedOtherSession = (int)deriveSeed.Invoke(null, new object[] { 54321, "node_7", ExpeditionTrigger });

            Assert.AreEqual(seedA1, seedA2, "同 (SessionSeed, NodeId, TriggerType) 必须得到同一 ResolutionSeed");
            Assert.AreNotEqual(seedA1, seedOtherNode, "不同 Node 必须得到不同 Seed");
            Assert.AreNotEqual(seedA1, seedOtherSession, "不同远征会话必须得到不同 Seed");
            Assert.GreaterOrEqual(seedA1, 0, "Seed 必须为非负（诊断 / 复现友好）");
        }

        [Test]
        public void E_worldView_DeclaresVillageResourceSentinel_NotFabricatedZero()
        {
            var type = Cs("ExpeditionEventWorldView");
            var field = type.GetField("UnknownVillageResourceValue", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(field, "远征 WorldView 必须显式声明「本域不拥有村庄资源」的哨兵常量");
            Assert.Less((int)field.GetValue(null), 0,
                "哨兵必须是负数：0 是合法村庄资源值，用它冒充会让条件 Fail Open");
        }

        // ==================== E17（§18）：内容覆盖守卫 ====================

        /// <summary>
        /// 默认产品图（`MapGenerator.Generate(4,3)`）**必然**包含 Event / Shop / Recruit 节点
        ///（后两者经 `LegacyMapGraphAdapter` 映射为 `ExpeditionNodeKind.Event`），其投影深度可为 1（行 1），
        /// 且一次远征可出现多个 Event 节点。因此必须存在**至少一个**「Repeatable 且无深度下界」的
        /// Enabled 远征事件候选；否则浅层节点或第二个 Event 节点必然 `NoCandidateEvent` → 正常产品必卡。
        ///
        /// 这是**长期内容守卫**：制作人以后调整 Test 内容时必须保持该性质（或提供等价覆盖）。
        /// </summary>
        [Test]
        public void E17_ExpeditionEvents_ProvideCoverageForEveryDepthAndRepeat()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic),
                "Event 静态配置必须可用：" + diagnostic);

            var expeditionEvents = new List<EventDefinition>();
            foreach (var definition in database.Events)
            {
                if (definition == null || !definition.Enabled) continue;

                foreach (var trigger in database.Triggers)
                {
                    if (trigger == null) continue;
                    if (trigger.EventId != definition.EventId) continue;
                    if (trigger.TriggerType != ExpeditionTrigger) continue;

                    expeditionEvents.Add(definition);
                    break;
                }
            }

            Assert.IsNotEmpty(expeditionEvents,
                "至少需要一个 Enabled 的远征事件（TriggerType = " + ExpeditionTrigger
                + "）：否则所有 Event 节点都会 NoCandidateEvent");

            bool alwaysAvailable = false;
            string describe = string.Empty;

            foreach (var definition in expeditionEvents)
            {
                bool hasDepthLowerBound = false;
                foreach (var condition in database.Conditions)
                {
                    if (condition == null || condition.EventId != definition.EventId) continue;
                    if (condition.ConditionType != EventConditionTypes.ExpeditionDepthAtLeast) continue;
                    if (condition.HasIntValue && condition.IntValue > 0) hasDepthLowerBound = true;
                }

                bool repeatable = string.Equals(definition.RepeatPolicy, "Repeatable", StringComparison.Ordinal);
                bool ok = repeatable && !hasDepthLowerBound;
                describe += "\n  - " + definition.EventId + " repeat=" + definition.RepeatPolicy
                    + " depthLowerBound=" + hasDepthLowerBound + " => " + (ok ? "COVERS" : "does not cover");

                if (ok) alwaysAvailable = true;
            }

            Assert.IsTrue(alwaysAvailable,
                "必须存在「Repeatable 且无深度下界」的远征事件候选，否则浅层 Event 节点 / 一次远征内第二个"
                + " Event 节点必然 NoCandidateEvent（正常产品会卡住，且本 WP 禁止回退占位 Continue）。当前候选："
                + describe);
        }

        // ==================== E4：No Placeholder Fallback（源码级守卫） ====================

        [Test]
        public void E4_ExpeditionUI_ContentNodes_GoThroughFormalEventFlow_AndNoPlaceholderRemains()
        {
            string source = File.ReadAllText(SourcePath("Scripts/UI/ExpeditionUI.cs"));

            StringAssert.Contains("ExpeditionEventFlow.TryEnterEventNode", source,
                "非战斗节点必须走正式 Event 原子进入（ValidateEnterNode → EventResolver → TryEnterNode）");
            StringAssert.Contains("ExpeditionEventCardAdapter.TryBuildOptionCard", source,
                "进入成功后必须渲染**正式事件卡**（内容来自 Event Config）");
            StringAssert.Contains("ExpeditionEventCardAdapter.TryBuildResultCard", source,
                "非战斗结果必须走 Result Mode（玩家「继续」后才结算）");
            StringAssert.Contains("ExpeditionEventFlow.TryContinueActiveEvent", source,
                "结果「继续」必须经事件 Flow 结算当前 Node（含 PendingExtraElapsedDays）");
            StringAssert.Contains("ExpeditionEventFlow.TryResolveActiveEventBattle", source,
                "Event 内战斗必须结算**同一个** Event Node（Victory / Defeat 都含额外时间结算）");

            StringAssert.DoesNotContain("占位内容节点已结算", source,
                "不得残留「进入后立即 Success 结算」的占位 Continue 语义（D2 §4 禁止 Placeholder Fallback）");
        }

        [Test]
        public void E_journeyView_FormalEventCard_ReusesTheSingleEventCardView()
        {
            string viewSource = File.ReadAllText(SourcePath("Scripts/UI/ExpeditionJourneyView.cs"));
            StringAssert.Contains("PresentFormalEventCard", viewSource,
                "正式 Event 卡必须复用统一卡面入口（不得新建第二套 Event UI）");

            string cardSource = File.ReadAllText(SourcePath("Scripts/UI/JourneyEventCard.cs"));
            StringAssert.Contains("JourneyEventCardView", cardSource, "统一卡面 View 仍是唯一渲染宿主");
            StringAssert.Contains("EventOption = 3", cardSource,
                "正式 Event Option 必须由专属角色标识（不再复用 ContinueContent 固定语义）");
            StringAssert.Contains("ContinueResult = 4", cardSource,
                "结果卡「继续」必须有专属角色（结算语义与占位 Continue 明确分离）");
        }

        [Test]
        public void E_classification_NonBattleProgressionNodes_AreJourneyEventKind()
        {
            var kindType = Loaded("JourneyEncounterKind");
            Assert.IsTrue(Enum.IsDefined(kindType, "Event"),
                "必须存在 JourneyEncounterKind.Event：正式 Event 节点进入前不得弹占位卡");

            int eventKind = (int)Enum.Parse(kindType, "Event");
            Assert.AreEqual(4, eventKind, "JourneyEncounterKind.Event 的值必须显式冻结（4）");

            string coordinator = File.ReadAllText(SourcePath("Scripts/UI/ExpeditionJourneyCoordinator.cs"));
            StringAssert.Contains("JourneyEncounterKind.Event", coordinator,
                "非战斗 Progression 节点必须分类为正式 Event（否则会退回占位卡表现）");

            string cycle = File.ReadAllText(SourcePath("Scripts/Journey/Runtime/JourneyPresentationCycle.cs"));
            StringAssert.Contains("JourneyEncounterKind.Event", cycle,
                "RequiresEncounterConfirmation 必须显式处理 Event 节点（进入前无占位卡）");
        }
    }
}

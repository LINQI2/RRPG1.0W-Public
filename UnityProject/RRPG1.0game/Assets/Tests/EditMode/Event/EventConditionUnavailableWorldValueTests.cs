using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D1 §2：**不可用 WorldView 数值必须 Fail Closed**。
    ///
    /// 背景：Village 域 Adapter（`VillageEventWorldView`）对「本域不拥有」的远征数值返回
    /// 明确非法哨兵 `UnknownExpeditionValue = -1`。若该哨兵被当作正常值参与比较，
    /// `ExpeditionDepthAtMost 3` 会得到 `-1 &lt;= 3 = true` → **损坏配置 Fail Open**。
    ///
    /// 规则（唯一解释器集中判定）：Depth / Supply / PartySize / NodeOrdinal 的合法域恒 ≥ 0；
    /// 任何 &lt; 0 的值 → 条件**不成立**（Fail Closed），且**不是**配置错误（顶层仍归 NoCandidateEvent）。
    /// </summary>
    public class EventConditionUnavailableWorldValueTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        /// <summary>最小 World View：只暴露本用例关心的数值（默认全部 = 不可用哨兵 -1）。</summary>
        private sealed class UnavailableWorld : IEventWorldView
        {
            public int Depth = -1;
            public int Supply = -1;
            public int PartySize = -1;
            public int NodeOrdinal = -1;

            public bool PersonExists(string personId) { return false; }
            public bool PersonInVillage(string personId) { return false; }
            public bool PersonOnExpedition(string personId) { return false; }
            public bool PersonAvailable(string personId) { return false; }
            public IReadOnlyList<string> VillagePersonIds { get { return new List<string>(); } }
            public IReadOnlyList<string> ExpeditionPartyPersonIds { get { return new List<string>(); } }
            public int VillageDayOrdinal { get { return 1; } }
            public int Ingredients { get { return 0; } }
            public int Food { get { return 0; } }
            public int ExpeditionDepth { get { return Depth; } }
            public int ExpeditionSupply { get { return Supply; } }
            public int ExpeditionPartySize { get { return PartySize; } }
            public int ExpeditionNodeOrdinal { get { return NodeOrdinal; } }
        }

        private static EventResolutionResult ResolveFor(string conditionType, int intValue, IEventWorldView world)
        {
            var catalog = SingleEventCatalog(Evt("evt_unavailable"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_unavailable", 1, conditionType, intValue: intValue)
                });

            return Resolve(catalog, Request(VillageTrigger, "k_unavailable", 1), world);
        }

        private static void AssertFailClosed(string conditionType, int intValue, IEventWorldView world,
            string message)
        {
            var result = ResolveFor(conditionType, intValue, world);

            Assert.IsFalse(result.Success, message);
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, result.Code,
                message + "：不可用值只能让条件不成立（Fail Closed），不得被当作配置错误或正常比较。");
        }

        [Test]
        public void UnavailableDepth_NeverSatisfiesAtMost_ButLegalDepthStillDoes()
        {
            AssertFailClosed("ExpeditionDepthAtMost", 3, new UnavailableWorld { Depth = -1 },
                "depth = -1 不得满足 AtMost 3");

            Assert.IsTrue(ResolveFor("ExpeditionDepthAtMost", 3, new UnavailableWorld { Depth = 1 }).Success,
                "合法 depth 必须仍按原语义成立");
        }

        [Test]
        public void UnavailableSupply_NeverSatisfiesAtMost_ButLegalSupplyStillDoes()
        {
            AssertFailClosed("SupplyAtMost", 100, new UnavailableWorld { Supply = -1 },
                "supply = -1 不得满足 AtMost 100");

            Assert.IsTrue(ResolveFor("SupplyAtMost", 100, new UnavailableWorld { Supply = 10 }).Success,
                "合法 supply 必须仍按原语义成立");
        }

        [Test]
        public void UnavailablePartySize_NeverSatisfiesAtMost_ButLegalPartySizeStillDoes()
        {
            AssertFailClosed("PartySizeAtMost", 10, new UnavailableWorld { PartySize = -1 },
                "partySize = -1 不得满足 AtMost 10");

            Assert.IsTrue(ResolveFor("PartySizeAtMost", 10, new UnavailableWorld { PartySize = 2 }).Success,
                "合法 partySize 必须仍按原语义成立");
        }

        [Test]
        public void UnavailableValues_NeverSatisfyAtLeastZeroEither()
        {
            // AtLeast 方向同样不得让哨兵蒙混过关（-1 >= 0 本来为假，但必须是「不可用」而不是「碰巧不成立」）。
            AssertFailClosed("ExpeditionDepthAtLeast", 0, new UnavailableWorld { Depth = -1 },
                "depth = -1 不得满足 AtLeast 0");
            AssertFailClosed("SupplyAtLeast", 0, new UnavailableWorld { Supply = -1 },
                "supply = -1 不得满足 AtLeast 0");
            AssertFailClosed("PartySizeAtLeast", 0, new UnavailableWorld { PartySize = -1 },
                "partySize = -1 不得满足 AtLeast 0");
        }

        [Test]
        public void LegalExpeditionWorld_StillEvaluatesNormally()
        {
            // 既有 Expedition Fixture 语义不得被本次防御改动影响。
            Assert.IsTrue(Resolve(SingleEventCatalog(Evt("evt_ok"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_ok", 1, "ExpeditionDepthAtLeast", intValue: 3)
                    }), Request(VillageTrigger, "k_ok", 1)).Success, "StandardWorld Depth 3 >= 3");

            Assert.IsFalse(Resolve(SingleEventCatalog(Evt("evt_ok2"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_ok2", 1, "ExpeditionDepthAtMost", intValue: 2)
                    }), Request(VillageTrigger, "k_ok2", 1)).Success, "StandardWorld Depth 3 > 2");
        }
        // ---------------- §2（协调层裁决）：`IEventWorldView` 不可用 → Fail Closed ----------------

        /// <summary>
        /// 顶层契约（1B 冻结）：`IEventWorldView == null` 时 Resolver 直接拒绝解析 —— 比「条件恰好不成立」
        /// 更强的 Fail Closed；解释器内部也各加了 `World == null` guard（防御纵深，不按 0 猜测）。
        /// 因此这里用 `EventResolver.Resolve` 直接传 null（Kit 的 `Resolve` 会用 StandardWorld 兜底）。
        /// </summary>
        private static EventResolutionResult ResolveWithNullWorld(string conditionType, int intValue,
            string targetKey)
        {
            var catalog = SingleEventCatalog(Evt("evt_null_world"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_null_world", 1, conditionType, targetKey: targetKey, intValue: intValue)
                });

            return EventResolver.Resolve(Request(VillageTrigger, "k_null_world", 1), catalog, null,
                new EventWorldState(), new ExpeditionEventSessionState());
        }

        [Test]
        public void WorldNull_WorldDependentConditions_NeverSucceed()
        {
            AssertWorldNullFails("VillageResourceAtMost", 100, "Food");
            AssertWorldNullFails("VillageResourceAtLeast", 0, "Ingredients");
            AssertWorldNullFails("ExpeditionDepthAtMost", 100, null);
            AssertWorldNullFails("SupplyAtMost", 100, null);
            AssertWorldNullFails("PartySizeAtMost", 100, null);
        }

        private static void AssertWorldNullFails(string conditionType, int intValue, string targetKey)
        {
            var result = ResolveWithNullWorld(conditionType, intValue, targetKey);

            Assert.IsFalse(result.Success, conditionType + " 在 World View 不可用时绝不能成立");
            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig, result.Code,
                conditionType + "：World View 缺失属于运行时不可用，必须显式失败（不得静默按 0 比较）");
        }

        [Test]
        public void WorldNull_IsRefusedAtResolverInput_EvenForContextOnlyCondition()
        {
            // 本用例**只**钉住 1B 顶层输入契约：`IEventWorldView == null` → `InvalidRuntimeConfig`
            // （协调层裁决 2026-09-26：不放宽该契约）。
            // 它**不是** Context 条件语义的断言：Context-only 条件在正式路径中始终运行在
            // 「WorldView 已通过顶层校验」之后，解释器被单独调用时的行为不构成公共契约，
            // 因此不得据此推导「Resolver + World=null + Context 条件成功」。
            // 关键点：绝不产生 Candidate、绝不按 0 比较、绝不静默继续。
            var refused = ResolveWithNullWorld("ContextIntAtMost", 3, "ObservedCount");

            Assert.IsFalse(refused.Success);
            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig, refused.Code);
        }

        [Test]
        public void ContextOnlyCondition_WithAvailableWorld_StillEvaluatesNormally()
        {
            // 反向对照：World 正常时 Context-only 条件按既有语义求值（证明上面的失败只来自 World 缺失）。
            var catalog = SingleEventCatalog(Evt("evt_ctx"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_ctx", 1, "ContextIntAtMost", targetKey: "ObservedCount", intValue: 3)
                });

            var request = Request(VillageTrigger, "k_ctx", 1,
                new EventContext().SetInt("ObservedCount", 3));

            Assert.IsTrue(Resolve(catalog, request).Success, "ObservedCount 3 <= 3 应成立");
        }

        // ---------------- WP-Event-1D Gate D2：远征域 Adapter 对**村庄资源**返回哨兵（本域不拥有） ----------------

        /// <summary>
        /// 远征域 World View：**村庄资源 = 不拥有哨兵 -1**（远征域不拥有 Village Resource Authority）；
        /// 远征数值保持合法。该形态与 `ExpeditionEventWorldView.UnknownVillageResourceValue` 一致。
        /// </summary>
        private sealed class UnknownVillageResourceWorld : IEventWorldView
        {
            public int IngredientsAmount = -1;
            public int FoodAmount = -1;

            public bool PersonExists(string personId) { return false; }
            public bool PersonInVillage(string personId) { return false; }
            public bool PersonOnExpedition(string personId) { return false; }
            public bool PersonAvailable(string personId) { return false; }
            public IReadOnlyList<string> VillagePersonIds { get { return new List<string>(); } }
            public IReadOnlyList<string> ExpeditionPartyPersonIds { get { return new List<string>(); } }
            public int VillageDayOrdinal { get { return 1; } }
            public int Ingredients { get { return IngredientsAmount; } }
            public int Food { get { return FoodAmount; } }
            public int ExpeditionDepth { get { return 0; } }
            public int ExpeditionSupply { get { return 0; } }
            public int ExpeditionPartySize { get { return 1; } }
            public int ExpeditionNodeOrdinal { get { return 1; } }
        }

        private static EventResolutionResult ResolveVillageResource(string conditionType, string targetKey,
            int intValue, IEventWorldView world)
        {
            var catalog = SingleEventCatalog(Evt("evt_village_res"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_village_res", 1, conditionType, targetKey: targetKey, intValue: intValue)
                });

            return Resolve(catalog, Request(VillageTrigger, "k_village_res", 1), world);
        }

        [Test]
        public void UnknownVillageResource_NeverSatisfiesAtMost_ButLegalResourceStillDoes()
        {
            var unavailable = ResolveVillageResource("VillageResourceAtMost", "Food", 100,
                new UnknownVillageResourceWorld());

            Assert.IsFalse(unavailable.Success, "村庄资源 = -1（本域不拥有）不得满足 AtMost 100");
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, unavailable.Code,
                "哨兵只能让条件不成立（Fail Closed），不得被当作正常值比较（否则损坏配置会 Fail Open）");

            var legal = ResolveVillageResource("VillageResourceAtMost", "Food", 100,
                new UnknownVillageResourceWorld { FoodAmount = 10 });

            Assert.IsTrue(legal.Success, "合法村庄资源必须仍按原语义成立");
        }

        [Test]
        public void UnknownVillageResource_NeverSatisfiesAtLeastZeroEither()
        {
            var unavailable = ResolveVillageResource("VillageResourceAtLeast", "Ingredients", 0,
                new UnknownVillageResourceWorld());

            Assert.IsFalse(unavailable.Success, "村庄资源 = -1 不得满足 AtLeast 0");
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, unavailable.Code);
        }
    }
}

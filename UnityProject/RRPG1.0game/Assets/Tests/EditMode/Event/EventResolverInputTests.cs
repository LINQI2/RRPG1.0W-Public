using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：输入 / 配置边界（§23）。环境不可用、调用方错误、配置非法必须与
    /// 「这次没有事件」区分开；未知枚举绝不静默接受。
    /// </summary>
    public class EventResolverInputTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        [Test]
        public void NullCatalog_IsConfigUnavailable()
        {
            var result = EventResolver.Resolve(Request(VillageTrigger, "k"),
                null, StandardWorld(), new EventWorldState(), new ExpeditionEventSessionState());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.ConfigUnavailable, result.Code);
        }

        [Test]
        public void NullRequest_IsInvalidRuntimeConfig()
        {
            var result = EventResolver.Resolve(null, Catalog(), StandardWorld(), new EventWorldState(),
                new ExpeditionEventSessionState());

            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig, result.Code);
        }

        [Test]
        public void EmptyTriggerType_And_EmptyTriggerKey_AreInvalidRuntimeConfig()
        {
            var catalog = SingleEventCatalog(Evt("evt_x"), VillageTrigger);

            var emptyType = EventResolver.Resolve(Request(string.Empty, "k"), catalog, StandardWorld(),
                new EventWorldState(), new ExpeditionEventSessionState());
            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig, emptyType.Code);

            var emptyKey = EventResolver.Resolve(Request(VillageTrigger, string.Empty), catalog, StandardWorld(),
                new EventWorldState(), new ExpeditionEventSessionState());
            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig, emptyKey.Code);
            StringAssert.Contains("TriggerKey", emptyKey.Diagnostic);
        }

        [Test]
        public void UnknownTriggerType_IsUnknownTrigger()
        {
            var result = Resolve(Catalog(), Request("VillageWeatherChanged", "k"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.UnknownTrigger, result.Code);
            StringAssert.Contains("VillageWeatherChanged", result.Diagnostic);
        }

        [Test]
        public void MissingWorldObjects_AreInvalidRuntimeConfig()
        {
            var catalog = SingleEventCatalog(Evt("evt_x"), VillageTrigger);
            var request = Request(VillageTrigger, "k");

            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig,
                EventResolver.Resolve(request, catalog, null, new EventWorldState(),
                    new ExpeditionEventSessionState()).Code);

            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig,
                EventResolver.Resolve(request, catalog, StandardWorld(), null,
                    new ExpeditionEventSessionState()).Code);

            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig,
                EventResolver.Resolve(request, catalog, StandardWorld(), new EventWorldState(), null).Code);
        }

        [Test]
        public void UnknownConditionType_MakesCatalogInvalid_AndResolutionFailsExplicitly()
        {
            var catalog = SingleEventCatalog(Evt("evt_bad"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c_bad", "evt_bad", 1, "WeatherIsNice")
                });

            Assert.IsFalse(catalog.IsValid);
            Assert.IsNotEmpty(catalog.Errors);

            var result = Resolve(catalog, Request(VillageTrigger, "k"));
            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.InvalidRuntimeConfig, result.Code);
            StringAssert.Contains("ConditionType", result.Diagnostic);
        }

        [Test]
        public void DanglingRoleCondition_MakesCatalogInvalid()
        {
            var catalog = Catalog(
                new List<EventDefinition> { Evt("evt_bad") },
                new List<EventTriggerDefinition> { Trigger("trg", "evt_bad", VillageTrigger) },
                null,
                new List<EventRoleConditionDefinition> { RoleCondition("rc", "role_missing", "PersonAvailable") });

            Assert.IsFalse(catalog.IsValid);

            bool found = false;
            foreach (string error in catalog.Errors)
            {
                if (error.Contains("role_missing")) found = true;
            }
            Assert.IsTrue(found, "必须明确指出悬空的 RoleId：" + string.Join(" | ", catalog.Errors));
        }

        [Test]
        public void UnknownRepeatPolicy_MakesCatalogInvalid()
        {
            var catalog = SingleEventCatalog(Evt("evt_bad", repeatPolicy: "Sometimes"), VillageTrigger);

            Assert.IsFalse(catalog.IsValid);

            bool found = false;
            foreach (string error in catalog.Errors)
            {
                if (error.Contains("RepeatPolicy")) found = true;
            }
            Assert.IsTrue(found, "必须明确指出未知 RepeatPolicy：" + string.Join(" | ", catalog.Errors));
        }
    }
}

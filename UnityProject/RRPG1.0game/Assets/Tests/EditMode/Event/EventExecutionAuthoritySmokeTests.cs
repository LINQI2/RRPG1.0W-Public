using System.Collections.Generic;
using System;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C §27 / §29：**Current Authoring Smoke**（对当前正式静态配置做最小执行验证）。
    ///
    /// 原则（与 1B 的 Authority Smoke 一致）：
    ///   - 期望值全部**从 Authority 推导**（`StaticConfigRegistry.EventConfig`），不写死「永远只有 2 个事件」；
    ///   - 每个启用事件的**每个 Option** 都必须得到一致结论：
    ///     可选 → 真实执行成功（含 exactly-once / History 语义）；不可选 → 被一致拒绝且零 mutation；
    ///   - 使用宽松 Fixture 世界 + Fixture Gateway：**不接真实产品 Authority**（不碰 SocialResponseLog、
    ///     不改 ExpeditionSupplyState、不创建 Battle）。
    /// </summary>
    public partial class EventExecutionAuthoritySmokeTests
    {
        private const int SmokeSeed = 20260926;

        internal static EventStaticCatalog Project(EventConfigDatabase database)
        {
            return new EventStaticCatalog(database.Events, database.Triggers, database.Roles,
                database.RoleConditions, database.Conditions, database.Options, database.OptionConditions,
                database.Outcomes, database.Effects);
        }

        [Test]
        public void Authority_EveryEnabledEvent_EveryOption_IsConsistentAndExecutable()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var catalog = Project(database);
            Assert.IsTrue(catalog.IsValid, "正式 Event 静态配置必须自洽：" + string.Join(" | ", catalog.Errors));

            int enabledEvents = 0;
            int availableOptions = 0;

            foreach (var eventRecord in catalog.Events)
            {
                if (!eventRecord.Enabled) continue;
                enabledEvents++;

                var triggers = catalog.GetTriggers(eventRecord.EventId);
                Assert.IsNotEmpty(triggers, "启用事件必须有触发：" + eventRecord.EventId);

                var fixture = EventResolverSmokeFixture.Build(catalog, eventRecord.EventId);

                var resolution = EventResolver.Resolve(
                    new EventResolutionRequest(triggers[0].TriggerType,
                        "exec-smoke:" + eventRecord.EventId, SmokeSeed, fixture.Context),
                    catalog, fixture.World, fixture.WorldState,
                    new ExpeditionEventSessionState("smoke_session"));

                Assert.IsTrue(resolution.Success, "应先解析出实例：" + eventRecord.EventId
                    + " → " + resolution.Code + " / " + resolution.Diagnostic);

                bool isExpedition = string.Equals(EventTriggerTypes.ResolveDomain(resolution.Instance.TriggerType),
                    "Expedition", StringComparison.Ordinal);

                foreach (var option in catalog.GetOptions(eventRecord.EventId))
                {
                    if (CheckOption(catalog, resolution.Instance, option, fixture, isExpedition)) availableOptions++;
                }
            }

            Assert.Greater(enabledEvents, 0, "当前正式配置至少应有 1 个启用事件");
            Assert.Greater(availableOptions, 0, "当前正式配置至少应有 1 个可选 Option 可被真实执行");
        }
    }
}

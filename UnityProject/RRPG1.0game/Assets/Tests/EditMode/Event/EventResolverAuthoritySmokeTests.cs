using System;
using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B §29：**Current Authoring Smoke**（对当前正式静态配置做最小验证）。
    ///
    /// 原则：
    ///   - 期望值全部**从 Authority 推导**（`StaticConfigRegistry.EventConfig` 的事件 / 触发 / 角色 / 条件），
    ///     不写死「正式长期只有 2 个事件」；
    ///   - 每个启用事件都必须至少有一条**可解析路径**（用宽松 Fixture 世界 / Context 构造）；
    ///     如果当前配置让某个事件无法解析 → 真实失败，不偷偷跳过；
    ///   - 本轮不接产品 Adapter：Fixture 世界只用于证明 Resolver 能在正式静态配置上工作。
    ///
    /// 边界：这里**不**断言具体内容语义（那是 R1–R26 的职责），只断言
    /// 「Authority 的每个启用事件在今天的内容下都真的能被解析出来」。
    /// </summary>
    public class EventResolverAuthoritySmokeTests
    {
        private const int SmokeSeed = 20260926;

        [Test]
        public void Authority_EventConfig_ProjectsToValidCatalog()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);
            Assert.IsNotNull(database);
            Assert.Greater(database.TotalCount, 0, "Event 领域必须有内容（1A 的测试内容也算）");

            var catalog = Project(database);
            Assert.IsTrue(catalog.IsValid, "正式 Event 静态配置必须自洽：" + string.Join(" | ", catalog.Errors));
            Assert.IsNotEmpty(catalog.Events);
        }

        [Test]
        public void Authority_EveryEnabledEvent_HasAtLeastOneResolvablePath()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var catalog = Project(database);
            Assert.IsTrue(catalog.IsValid, string.Join(" | ", catalog.Errors));

            int enabledCount = 0;

            foreach (var eventRecord in catalog.Events)
            {
                if (!eventRecord.Enabled) continue;
                enabledCount++;

                var triggers = catalog.GetTriggers(eventRecord.EventId);
                Assert.IsNotEmpty(triggers, "启用事件必须有触发：" + eventRecord.EventId);

                var trigger = triggers[0];   // Catalog 内按 TriggerId 规范排序
                var fixture = EventResolverSmokeFixture.Build(catalog, eventRecord.EventId);
                var request = new EventResolutionRequest(trigger.TriggerType,
                    "smoke:" + trigger.TriggerType + ":" + eventRecord.EventId, SmokeSeed, fixture.Context);

                var result = EventResolver.Resolve(request, catalog, fixture.World, fixture.WorldState,
                    new ExpeditionEventSessionState("smoke_session"));

                Assert.IsTrue(result.Success,
                    "事件 " + eventRecord.EventId + " 在当前正式配置下应至少有一条可解析路径："
                    + result.Code + " / " + result.Diagnostic + "\n" + result.Trace.ToText());

                var entry = result.Trace.Find(eventRecord.EventId);
                Assert.IsNotNull(entry, "Trace 必须覆盖该事件");
                Assert.IsTrue(entry.IsCandidate, "事件 " + eventRecord.EventId + " 必须成为候选；拒绝原因："
                    + entry.RejectStage + " " + entry.RejectReason);

                foreach (var role in catalog.GetRoles(eventRecord.EventId))
                {
                    string personId;
                    Assert.IsTrue(result.Instance.TryGetBoundPerson(role.RoleId, out personId),
                        "角色未完成绑定：" + role.RoleId);
                    Assert.IsNotEmpty(personId);
                }

                var again = EventResolver.Resolve(request, catalog, fixture.World, fixture.WorldState,
                    new ExpeditionEventSessionState("smoke_session"));
                Assert.IsTrue(again.Success);
                Assert.AreEqual(result.Instance.EventInstanceId, again.Instance.EventInstanceId,
                    "同输入 / 同 Seed 必须得到同一个实例身份");
            }

            Assert.Greater(enabledCount, 0, "当前正式配置至少应有 1 个启用事件");
        }

        private static EventStaticCatalog Project(EventConfigDatabase database)
        {
            return new EventStaticCatalog(database.Events, database.Triggers, database.Roles,
                database.RoleConditions, database.Conditions);
        }
    }
}

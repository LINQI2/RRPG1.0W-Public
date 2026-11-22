using System;
using RRPG.Event;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B §29：Authority Smoke 的 Fixture 构造（测试程序集专用）。
    ///
    /// 定位：**宽松但只读** —— 它的唯一目的是让「当前正式静态配置里的每个启用事件」都有
    /// 一条可解析路径（人物 / 村庄资源 / 远征状态 / Flag / 历史全部给足），从而验证
    /// Resolver 能在真实 Authority 内容上工作。它**不是**条件语义测试（那是 R1–R26 的职责），
    /// 也**不是**产品 Adapter（产品 Adapter 属 1D）。
    /// </summary>
    internal sealed class EventResolverSmokeFixture
    {
        public EventContext Context = new EventContext();
        public EventWorldViewSnapshot World = new EventWorldViewSnapshot();
        public EventWorldState WorldState = new EventWorldState();

        public static EventResolverSmokeFixture Build(EventStaticCatalog catalog, string eventId)
        {
            var fixture = new EventResolverSmokeFixture();

            // 宽松人物：同一批人「在村 + 在远征 + 可用」→ 覆盖全部 Person 语义
            for (int i = 1; i <= 3; i++)
            {
                fixture.World.AddPerson("person_smoke_" + i, inVillage: true, onExpedition: true, available: true);
            }

            fixture.World.SetVillageState(villageDayOrdinal: 999, ingredients: 9999, food: 9999);
            fixture.World.SetExpeditionState(depth: 99, supply: 999, partySize: 3, nodeOrdinal: 99);

            foreach (var role in catalog.GetRoles(eventId))
            {
                if (string.Equals(role.SelectorType, "ContextPerson", StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(role.SourceKey))
                {
                    fixture.Context.SetPerson(role.SourceKey, "person_smoke_1");
                }

                if (string.Equals(role.SelectorType, "FixedPerson", StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(role.FixedPersonId))
                {
                    fixture.World.AddPerson(role.FixedPersonId, inVillage: true, onExpedition: true,
                        available: true);
                }
            }

            foreach (var condition in catalog.GetConditions(eventId))
            {
                if (string.Equals(condition.ConditionType, "ContextStringEquals", StringComparison.Ordinal))
                {
                    fixture.Context.SetString(condition.TargetKey, condition.StringValue);
                }
                else if (string.Equals(condition.ConditionType, "ContextIntAtLeast", StringComparison.Ordinal)
                         || string.Equals(condition.ConditionType, "ContextIntAtMost", StringComparison.Ordinal))
                {
                    fixture.Context.SetInt(condition.TargetKey, condition.IntValue);
                }
                else if (string.Equals(condition.ConditionType, "FlagSet", StringComparison.Ordinal))
                {
                    fixture.WorldState.SeedFlagForTesting(condition.TargetKey);
                }
                else if (string.Equals(condition.ConditionType, "EventSeenCountAtLeast", StringComparison.Ordinal))
                {
                    for (int i = 0; i < condition.IntValue; i++)
                    {
                        fixture.WorldState.History.SeedOccurrenceForTesting(condition.TargetKey, i);
                    }
                }
            }

            return fixture;
        }
    }
}

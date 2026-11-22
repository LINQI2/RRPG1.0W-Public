using System.Collections.Generic;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.EventProduct.Tests
{
    /// <summary>D0 存档往返夹具：最小可解析事件 + 一个 ContextPerson 角色 + 两个 Outcome。</summary>
    internal static class EventWorldPersistenceFixtures
    {
        public const string EventId = "evt_d0_pending";
        public const string OptionId = "opt_d0";

        public static EventStaticCatalog BuildCatalog()
        {
            return new EventStaticCatalog(
                new List<EventDefinition>
                {
                    new EventDefinition
                    {
                        EventId = EventId, DisplayName = "D0 测试事件", Enabled = true, Weight = 100,
                        RepeatPolicy = "Repeatable", CooldownType = "None", CooldownValue = 0
                    }
                },
                new List<EventTriggerDefinition>
                {
                    new EventTriggerDefinition
                    {
                        TriggerId = "trg_d0", EventId = EventId, TriggerType = "VillageRecognitionRaised"
                    }
                },
                new List<EventRoleDefinition>
                {
                    new EventRoleDefinition
                    {
                        RoleId = "role_d0", EventId = EventId, RoleName = "当事人",
                        SelectorType = "ContextPerson", SourceKey = "Observer"
                    }
                },
                null,
                null,
                new List<EventOptionDefinition>
                {
                    new EventOptionDefinition
                    {
                        OptionId = OptionId, EventId = EventId, SortOrder = 1, LabelTemplate = "选项",
                        UnavailableMode = "Hide"
                    }
                },
                null,
                new List<EventOutcomeDefinition>
                {
                    new EventOutcomeDefinition { OutcomeId = "out_d0_a", OptionId = OptionId, Weight = 70 },
                    new EventOutcomeDefinition { OutcomeId = "out_d0_b", OptionId = OptionId, Weight = 30 }
                },
                null);
        }

        public static ResolvedEventInstance ResolveInstance(EventStaticCatalog catalog, int seed)
        {
            var world = new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(villageDayOrdinal: 321, ingredients: 50, food: 40)
                .SetExpeditionState(depth: 1, supply: 10, partySize: 1, nodeOrdinal: 2);

            var context = new EventContext()
                .SetPerson("Observer", "p1")
                .SetString("RecognitionType", "TemporaryDraftBuildup")
                .SetInt("ObservedCount", 4);

            var result = EventResolver.Resolve(
                new EventResolutionRequest("VillageRecognitionRaised", "recognition:p1:year_2", seed, context),
                catalog, world, new EventWorldState(), new ExpeditionEventSessionState());

            return result.Success ? result.Instance : null;
        }
    }
}

using RRPG.Event;

namespace RRPG.Event.Tests
{
    /// <summary>WP-Event-1B：Event Resolver 的输入 / 世界夹具（测试程序集专用）。</summary>
    internal static class EventResolverTestWorld
    {
        public static EventResolutionRequest Request(string triggerType, string triggerKey = "key_test",
            int seed = 1, EventContext context = null)
        {
            return new EventResolutionRequest(triggerType, triggerKey, seed, context);
        }

        public static EventContext ContextWithPerson(string key, string personId)
        {
            return new EventContext().SetPerson(key, personId);
        }

        /// <summary>
        /// 常用世界（Test Fixture）：p1 / p2 在村可用；p3 在远征可用；p4 在村但**不可用**；
        /// 村庄日序 100、Ingredients 50 / Food 40；远征 Depth 3 / Supply 20 / PartySize 2 / NodeOrdinal 5。
        /// </summary>
        public static EventWorldViewSnapshot StandardWorld()
        {
            return new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .AddPerson("p2", inVillage: true, available: true)
                .AddPerson("p3", onExpedition: true, available: true)
                .AddPerson("p4", inVillage: true, available: false)
                .SetVillageState(villageDayOrdinal: 100, ingredients: 50, food: 40)
                .SetExpeditionState(depth: 3, supply: 20, partySize: 2, nodeOrdinal: 5);
        }

        public static EventResolutionResult Resolve(EventStaticCatalog catalog, EventResolutionRequest request,
            IEventWorldView world = null, EventWorldState worldState = null,
            ExpeditionEventSessionState sessionState = null, EventResolverOptions options = null)
        {
            return EventResolver.Resolve(request, catalog, world ?? StandardWorld(),
                worldState ?? new EventWorldState(), sessionState ?? new ExpeditionEventSessionState(), options);
        }
    }
}

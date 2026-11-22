using System;
using System.Collections.Generic;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：**Effect Gateway 夹具**（纯内存实现 `IEventEffectGateway`）。
    ///
    /// 定位：1C 不接真实产品 Authority（Village / Expedition / Battle），因此用本夹具证明：
    ///   - projected state 的 Preflight 语义（含前序效果影响后序）；
    ///   - Commit 的真实 mutation 只发生在 Commit 阶段；
    ///   - Gateway 失败时 Event Core 的行为（不写 History / 不标 Completed）。
    ///
    /// 它**不是**产品实现：真实 Adapter 属 1D（映射到 VillageResourceState / 正式 Rest Authority /
    /// Expedition Supply / Node resolution / Encounter 生命周期）。
    /// </summary>
    internal sealed partial class EventEffectGatewayFixture : IEventEffectGateway
    {
        public readonly Dictionary<string, int> VillageResources = new Dictionary<string, int>(StringComparer.Ordinal);
        public int ExpeditionSupply;

        public readonly List<string> RestGrants = new List<string>();
        public readonly List<int> AdvancedDays = new List<int>();
        public readonly List<string> BattleStarts = new List<string>();

        // 失败 / 异常注入（用于 C14 / C17 一类用例）
        public bool FailGrantRest;
        public bool FailCanGrantRest;
        public bool FailApplySupplyDelta;
        public bool ThrowOnStartBattle;
        public bool ThrowOnApplyVillageResourceDelta;

        /// <summary>仅让 **Commit** 阶段的 StartBattle 失败（Preflight 的 CanStartBattle 仍通过）。</summary>
        public bool FailStartBattle;

        /// <summary>仅让 **Preflight** 的 CanStartBattle 返回不可执行。</summary>
        public bool FailCanStartBattle;

        // 调用计数（用于证明「Preflight 不 mutation」）
        public int ApplyVillageResourceDeltaCalls;
        public int ApplySupplyDeltaCalls;
        public int GrantRestCalls;
        public int AdvanceExpeditionTimeCalls;
        public int StartBattleCalls;
        public int CanGrantRestCalls;
        public int CanAdvanceExpeditionTimeCalls;
        public int CanStartBattleCalls;

        public EventEffectGatewayFixture(int ingredients = 50, int food = 40, int supply = 20)
        {
            VillageResources["Ingredients"] = ingredients;
            VillageResources["Food"] = food;
            ExpeditionSupply = supply;
        }

        /// <summary>是否发生过任何「真实提交」调用（Preflight 阶段必须保持 0）。</summary>
        public int TotalMutationCalls
        {
            get
            {
                return ApplyVillageResourceDeltaCalls + ApplySupplyDeltaCalls + GrantRestCalls
                    + AdvanceExpeditionTimeCalls + StartBattleCalls;
            }
        }
    }
}

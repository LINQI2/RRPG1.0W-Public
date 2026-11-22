using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// Read-only World View —— Event Resolver 能查询的**最小只读世界事实契约**（WP-Event-1B §4）。
    ///
    /// 为什么用接口而不是直接持有 VillageState / AdventureManager：
    ///   - Event Core **不拥有**这些 Authority（Village Simulation / Expedition Progression /
    ///     Person Identity / Resource Authority 仍属各自领域）；
    ///   - Event.Runtime 是纯逻辑程序集（`noEngineReferences = true`）：不得引用 UI / Scene /
    ///     MonoBehaviour / SaveSystem，也不得引用 Village.Runtime / ExpeditionProgression.Runtime
    ///     等产品程序集（否则形成循环或把 Event 绑死在具体实现上）；
    ///   - 因此真实实现由 **1D Adapter** 提供（从 VillageTime / VillageResourceState /
    ///     ExpeditionProgressionState / VillageWorldPresence 只读桥接），1B 只用 Fixture 实现。
    ///
    /// 契约（实现方必须保证）：
    ///   - 全部查询**只读**、无副作用、无随机、无时间依赖；
    ///   - `PersonExists` 为 false 时，其余 Person 查询返回 false；
    ///   - `VillagePersonIds` / `ExpeditionPartyPersonIds` 只是**候选池**（可含不可用人物），
    ///     顺序不作语义（Resolver 自己会做规范排序）；其中的 PersonId 必须是已存在人物；
    ///   - `VillageDayOrdinal` / `ExpeditionNodeOrdinal` 是**单调序数**，由适配器从正式时间 Authority
    ///     提供（Event Core **不**自己推 Gregorian 日期）。
    /// </summary>
    public interface IEventWorldView
    {
        bool PersonExists(string personId);

        bool PersonInVillage(string personId);

        bool PersonOnExpedition(string personId);

        bool PersonAvailable(string personId);

        /// <summary>当前村庄人物候选池（未排序；可含不可用人物）。</summary>
        IReadOnlyList<string> VillagePersonIds { get; }

        /// <summary>当前远征队伍候选池（未排序；可含不可用人物）。</summary>
        IReadOnlyList<string> ExpeditionPartyPersonIds { get; }

        /// <summary>当前村庄绝对日序（单调；由 Village Time Authority 提供）。</summary>
        int VillageDayOrdinal { get; }

        int Ingredients { get; }

        int Food { get; }

        /// <summary>当前远征深度。</summary>
        int ExpeditionDepth { get; }

        /// <summary>当前远征补给。</summary>
        int ExpeditionSupply { get; }

        /// <summary>当前远征队伍人数（语义由 Expedition Authority 定义）。</summary>
        int ExpeditionPartySize { get; }

        /// <summary>当前远征节点序（单调；新 Session 从起点重新计数）。</summary>
        int ExpeditionNodeOrdinal { get; }
    }
}

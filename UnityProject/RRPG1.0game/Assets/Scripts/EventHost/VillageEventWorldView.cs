using System;
using System.Collections.Generic;
using RRPG.Event;

/// <summary>
/// **VillageEventWorldView（Village 事件世界只读适配器）** —— WP-Event-1D Gate D1 §2/§3。
///
/// 职责：把 Village 既有 Authority 以 **只读**方式暴露给 Event Core（`IEventWorldView`）。
/// 它**不拥有**任何状态：不缓存长期数据、不复制人物真相源、不做规则推理、无副作用、无随机、无时间依赖。
///
/// 数据来源（全部是既有 Authority）：
///   - Person 存在 / 显示名：`PlayerCollection`（长期人物真相源）；
///   - Person 位置与可用：`VillageRuntimeBridge.GetPersonAvailability` + `IsPersonTemporarilyOccupied`
///     （Expedition / Unavailable / Active Project 占用统一由 Village 侧登记）；
///   - 资源：`VillageRuntimeBridge.Resources`（`VillageResourceState`）；
///   - 时间：`VillageRuntimeBridge.Time` → `VillageDayOrdinal.FromVillageTime`（单调，不使用现实时间）。
///
/// **Expedition-only 字段的诚实语义**：Village 域 Adapter **不伪造** Expedition 状态。
/// 由于 `IEventWorldView` 把 Expedition 字段定义为 `int`，这里用**明确非法哨兵 `UnknownExpeditionValue = -1`**
/// 表达「Village 域不拥有该数据」（Supply / PartySize 的正常取值恒 ≥ 0，不可能与哨兵混淆）。
/// 该字段不会被求值：Compiler 的域校验（DAT043）禁止 Village Event 引用 Expedition 条件 / 效果，
/// 且本 Adapter 的 Gateway 对 Expedition 效果一律 Fail Closed。
/// </summary>
public sealed class VillageEventWorldView : IEventWorldView
{
    /// <summary>Expedition-only 字段在 Village 域的「不拥有」哨兵（≠ 任何合法值）。</summary>
    public const int UnknownExpeditionValue = -1;

    private readonly Func<PlayerCollection> _personSource;

    /// <summary>
    /// 默认使用运行时的 `VillageRuntimeBridge.SimulationPersonSource`（可注入以便测试）。
    /// </summary>
    public VillageEventWorldView(Func<PlayerCollection> personSource = null)
    {
        _personSource = personSource ?? (() => VillageRuntimeBridge.SimulationPersonSource);
    }

    // ---------------- Person ----------------

    public bool PersonExists(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return false;
        var source = _personSource();
        if (source == null) return false;
        return source.FindPersonByPersonId(personId) != null;
    }

    public bool PersonInVillage(string personId)
    {
        return AvailabilityOf(personId) == VillageAvailability.InVillage;
    }

    public bool PersonOnExpedition(string personId)
    {
        return AvailabilityOf(personId) == VillageAvailability.Expedition;
    }

    /// <summary>
    /// 可用 = 存在 + 在村庄 + 未被临时占用（远征 / 不可用 / Active Project 参与者）。
    /// 判定本身复用 Village 既有登记，**不**在本 Adapter 里复制占用规则。
    /// </summary>
    public bool PersonAvailable(string personId)
    {
        if (!PersonExists(personId)) return false;
        if (AvailabilityOf(personId) != VillageAvailability.InVillage) return false;
        return !VillageRuntimeBridge.IsPersonTemporarilyOccupied(personId);
    }

    /// <summary>村庄人物候选池（在村人物；可含「在村但暂时不可用」者，由 Resolver 自己判可用）。</summary>
    public IReadOnlyList<string> VillagePersonIds
    {
        get
        {
            var ids = new List<string>();
            foreach (var view in AvailabilityViews())
            {
                if (view == null || string.IsNullOrEmpty(view.PersonId)) continue;
                if (view.Availability == VillageAvailability.InVillage) ids.Add(view.PersonId);
            }
            return ids;
        }
    }

    /// <summary>远征队伍候选池（真实登记，不伪造；无远征时为空）。</summary>
    public IReadOnlyList<string> ExpeditionPartyPersonIds
    {
        get
        {
            var ids = new List<string>();
            foreach (var id in VillageRuntimeBridge.ExpeditionPersonIds)
            {
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            return ids;
        }
    }

    private VillageAvailability? AvailabilityOf(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;
        foreach (var view in AvailabilityViews())
        {
            if (view == null) continue;
            if (string.Equals(view.PersonId, personId, StringComparison.Ordinal)) return view.Availability;
        }
        return null;
    }

    private List<VillagePersonView> AvailabilityViews()
    {
        var source = _personSource();
        if (source == null) return new List<VillagePersonView>();
        return VillageRuntimeBridge.GetPersonAvailability(source);
    }

    // ---------------- 时间 / 资源 ----------------

    /// <summary>Village 绝对日序（由 `VillageDayOrdinal` 从正式 Village 时间换算；非法 → 0）。</summary>
    public int VillageDayOrdinal
    {
        // 注意：本属性名与全局类型 `VillageDayOrdinal` 同名 → 必须用 global:: 明确指向类型，
        // 否则成员解析会把 `VillageDayOrdinal.FromVillageTime` 当成 int 属性的成员访问（CS1061）。
        get { return global::VillageDayOrdinal.FromVillageTime(VillageRuntimeBridge.Time); }
    }

    public int Ingredients
    {
        get
        {
            var resources = VillageRuntimeBridge.Resources;
            return resources != null ? resources.Ingredients : 0;
        }
    }

    public int Food
    {
        get
        {
            var resources = VillageRuntimeBridge.Resources;
            return resources != null ? resources.Food : 0;
        }
    }

    // ---------------- Expedition-only（Village 域不拥有：返回明确非法哨兵） ----------------

    public int ExpeditionDepth { get { return UnknownExpeditionValue; } }

    public int ExpeditionSupply { get { return UnknownExpeditionValue; } }

    public int ExpeditionPartySize { get { return UnknownExpeditionValue; } }

    public int ExpeditionNodeOrdinal { get { return UnknownExpeditionValue; } }
}

using System;
using System.Collections.Generic;
using RRPG.Event;

/// <summary>
/// **ExpeditionEventWorldView（远征事件世界只读适配器）** —— WP-Event-1D Gate D2 §6。
///
/// 职责：把**当前这次远征**的正式事实以只读方式暴露给 Event Core（`IEventWorldView`）。
/// 它不拥有任何状态：不缓存、不复制人物真相源、不做规则推理、无副作用、无随机、无时间依赖。
///
/// 数据来源（全部是既有 Authority，本类不新建）：
///   - Person 存在 / 显示名：`AdventureManager.PlayerData`（长期人物真相源）；在村/占用：`VillageRuntimeBridge`；
///   - **本次远征队伍**：`AdventureManager.GetExpeditionPersonIds()`（本次会话真实队伍，不是 Village 全局登记）；
///   - 补给 / 人数：`ExpeditionProgressionState.Supply`（唯一 Supply Authority）；
///   - Depth / NodeOrdinal：`EncounterDepthRule` + 本次要进入的 NodeId（与 Battle 路径同一条深度规则）；
///   - 村庄日序：`VillageDayOrdinal.FromVillageTime`（与本域无关，只作为长期历史的稳定时间戳）。
///
/// **本域不拥有的数值一律返回明确哨兵，不伪造**（沿用 Gate D1 §21.8 冻结的裁决）：
///   - 村庄资源（Ingredients / Food）：远征域不拥有 → <see cref="UnknownVillageResourceValue"/>（-1）。
///     唯一解释器 `EventConditionEvaluator.Numeric` 对该哨兵 **Fail Closed**（条件视为不成立），
///     因此损坏配置（远征事件引用村庄资源条件）不会变成 Fail Open。
///
/// 语义说明（有意冻结，避免第二套口径）：
///   - `PersonAvailable`：本次远征队伍成员 = 可用（远征事件的可选人物就是队伍成员）；
///     非队伍成员则按 Village 既有占用登记判断（在村且未被临时占用）。
///   - `ExpeditionDepth` / `ExpeditionNodeOrdinal`：**本次要进入的 Node** 的投影深度 / 序数
///     （= 进入后的已进入数量 − 1 / 进入后的已进入数量）。这是求解与结算都成立的同一口径：
///     Option 执行时该 Node 已进入，其 `enteredCount` 自然等于投影值。
/// </summary>
public sealed class ExpeditionEventWorldView : IEventWorldView
{
    /// <summary>村庄资源在远征域的「本域不拥有」哨兵（≠ 任何合法值；解释器 Fail Closed）。</summary>
    public const int UnknownVillageResourceValue = -1;

    private readonly AdventureManager _adventure;
    private readonly string _targetNodeId;
    private readonly Func<PlayerCollection> _personSource;

    public ExpeditionEventWorldView(AdventureManager adventure, string targetNodeId,
        Func<PlayerCollection> personSource = null)
    {
        _adventure = adventure;
        _targetNodeId = targetNodeId;
        _personSource = personSource;
    }

    // ---------------- Person ----------------

    public bool PersonExists(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return false;
        var source = PersonSource();
        if (source == null) return false;
        return source.FindPersonByPersonId(personId) != null;
    }

    public bool PersonInVillage(string personId)
    {
        return AvailabilityOf(personId) == VillageAvailability.InVillage;
    }

    public bool PersonOnExpedition(string personId)
    {
        return IsInThisExpeditionParty(personId);
    }

    /// <summary>
    /// 可用 = 存在 + （本次远征队伍成员 → 可用）或（在村庄且未被临时占用）。
    /// 判定复用既有 Authority，**不**在本 Adapter 里复制占用规则。
    /// </summary>
    public bool PersonAvailable(string personId)
    {
        if (!PersonExists(personId)) return false;
        if (IsInThisExpeditionParty(personId)) return true;   // 本次远征成员即远征事件的可选人物
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

    /// <summary>**本次远征队伍**候选池（真实登记，不伪造；无会话时为空）。</summary>
    public IReadOnlyList<string> ExpeditionPartyPersonIds
    {
        get
        {
            var ids = new List<string>();
            if (_adventure == null) return ids;

            var party = _adventure.GetExpeditionPersonIds();
            if (party == null) return ids;

            for (int i = 0; i < party.Count; i++)
            {
                if (!string.IsNullOrEmpty(party[i]) && !ids.Contains(party[i])) ids.Add(party[i]);
            }
            return ids;
        }
    }

    // ---------------- 时间 / 村庄资源（本域不拥有 → 明确哨兵） ----------------

    /// <summary>
    /// 村庄绝对日序（由 `VillageDayOrdinal` 从正式 Village 时间换算；无时间 → 0 = 明确无效哨兵）。
    /// 远征域不消费村庄日序，这里只用于长期 Event History 的稳定时间戳。
    /// </summary>
    public int VillageDayOrdinal
    {
        // 注意：本属性名与全局类型 `VillageDayOrdinal` 同名 → 必须用 global:: 明确指向类型。
        get { return global::VillageDayOrdinal.FromVillageTime(VillageRuntimeBridge.Time); }
    }

    public int Ingredients { get { return UnknownVillageResourceValue; } }

    public int Food { get { return UnknownVillageResourceValue; } }

    // ---------------- Expedition（本域拥有的正式事实） ----------------

    public int ExpeditionDepth
    {
        get { return _adventure == null ? 0 : _adventure.ProjectedDepthForNode(_targetNodeId); }
    }

    public int ExpeditionSupply
    {
        get
        {
            var state = _adventure != null ? _adventure.ProgressionState : null;
            return state != null && state.Supply != null ? state.Supply.CurrentCarryUnits : 0;
        }
    }

    public int ExpeditionPartySize
    {
        get { return ExpeditionPartyPersonIds.Count; }
    }

    /// <summary>本次要进入的 Node 的投影节点序（= 进入后的已进入数量）。</summary>
    public int ExpeditionNodeOrdinal
    {
        get { return ExpeditionDepth + 1; }
    }

    // ---------------- 内部 ----------------

    private PlayerCollection PersonSource()
    {
        if (_personSource != null) return _personSource();
        return _adventure != null ? _adventure.PlayerData : null;
    }

    private bool IsInThisExpeditionParty(string personId)
    {
        if (string.IsNullOrEmpty(personId) || _adventure == null) return false;

        var party = _adventure.GetExpeditionPersonIds();
        if (party == null) return false;

        for (int i = 0; i < party.Count; i++)
        {
            if (string.Equals(party[i], personId, StringComparison.Ordinal)) return true;
        }
        return false;
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
        var source = PersonSource();
        if (source == null) return new List<VillagePersonView>();
        return VillageRuntimeBridge.GetPersonAvailability(source);
    }
}

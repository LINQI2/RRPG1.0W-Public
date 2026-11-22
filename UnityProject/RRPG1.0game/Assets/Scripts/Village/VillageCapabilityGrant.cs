using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VillageCapabilityGrant —— V4 Gate 1 的 **Test / Not Frozen** Minimal Capability 授予。
///
/// 目的（Village-GameSpec §2.11 / §6.6）：只用于证明一条最小产品链成立：
///   Village Project Completion
///     → Test Capability Grant
///     → 具体 Person 现有 Growth Authority Unlocked Pool 扩大
///     → Character Growth Offer 与 Run Reward Candidate 共同读取
///     → Save / Load 后仍存在
///
/// 边界（硬约束）：
///   - **不建立 Capability 数据模型 / Capability Save 区块 / Capability Tree / Research Tree**；
///     本类不持有任何状态、不持久化任何字段；持久化事实 = 「卡已在该 Person 的 Pool 中」。
///   - 唯一 Authority：`GrowthAuthority.Resolve(person).UnlockedPoolCardIds`
///     （Hero → Legacy 池；已形成 Profession 的普通 Character → ProfessionProgress 池）。
///   - 写入前置门禁：`auth != null && auth.IsAvailable`；否则 skip + warning。
///     **禁止 fallback**：不得写 Legacy 池 / `EmptyAuthorityPool` / TemplateId / CardId / 第二套 Dictionary。
///   - 生产者唯一：VillageRuntimeBridge.HandleDayTick 的既有「Project 完成快照增量」循环；
///     本类不做完成判定、不订阅事件、不被 UI / RunState 调用。
///   - 重复授予：不重复 CardId（contains 幂等）。
///
/// 排序声明（**Test / Not Frozen Observable Strategy**，不是正式设计）：
///   当前 CardGrowthResolver.OfferCount = 3，且产品人物池规模为 3，
///   因此「纯追加」新卡在三选一中不可观测；本类在授予时把 granted Card 放到 index 0，
///   仅为让 V4 证明可确定性观测。**未来正式 Offer / Pool 规则可替换，不得据此冻结排序语义。**
///
/// 全部名称 / 卡 / 触发条件均为 Test / Not Frozen，不得升级为正式 Capability 内容。
/// </summary>
public static class VillageCapabilityGrant
{
    /// <summary>
    /// 【Test / Not Frozen】可读的 Test Capability 名称，仅用于日志与测试可读性；
    /// 不持久化、不参与判定、不作为长期 Authority。
    /// </summary>
    public const string TestCapabilityName = "cap_test_project_rally";

    /// <summary>【Test / Not Frozen】产生该 Capability 的 Test Project 定义 Id。</summary>
    public const string TestCapabilityProjectDefId = "prj_test_02";

    /// <summary>【Test / Not Frozen】该 Capability 授予的牌模板 Id（必须是 CardKind = Character 的模板）。</summary>
    public const string TestCapabilityCardId = "bcard_rally";

    /// <summary>
    /// 解析某 Project 定义完成后应授予的 Test Capability 卡 Id；无映射返回 null（不猜、不默认授予）。
    /// </summary>
    public static string ResolveTestCapabilityCardId(string projectDefId)
    {
        if (string.IsNullOrEmpty(projectDefId)) return null;
        return projectDefId == TestCapabilityProjectDefId ? TestCapabilityCardId : null;
    }

    /// <summary>
    /// Project 完成时的唯一授予入口（由 VillageRuntimeBridge 在同一完成增量循环内调用）。
    /// 对每个参与者按同一 Test Capability 尝试授予；返回实际新增（此前不存在）的人数。
    /// 无映射 / 无参与者 → 0，且不产生任何副作用。
    /// </summary>
    public static int GrantForCompletedProject(IReadOnlyList<string> participantPersonIds, string projectDefId)
    {
        string cardId = ResolveTestCapabilityCardId(projectDefId);
        if (string.IsNullOrEmpty(cardId)) return 0;
        if (participantPersonIds == null || participantPersonIds.Count == 0) return 0;

        int granted = 0;
        for (int i = 0; i < participantPersonIds.Count; i++)
        {
            string personId = participantPersonIds[i];
            if (string.IsNullOrEmpty(personId)) continue;
            if (TryGrantToPerson(personId, cardId) == VillageCapabilityGrantResult.Granted) granted++;
        }
        return granted;
    }

    /// <summary>
    /// 对单个具体 Person 尝试授予一张 Test Capability 卡。
    /// 成功路径：`GrowthAuthority.Resolve(person).UnlockedPoolCardIds` 中确保该卡存在且位于 index 0。
    /// 失败路径：全部为「跳过 + 告警」，绝不 fallback 到任何其它池或猜测身份。
    /// </summary>
    public static VillageCapabilityGrantResult TryGrantToPerson(string personId, string cardId)
    {
        if (string.IsNullOrEmpty(personId) || string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("[VillageCapabilityGrant] 参数非法（personId/cardId 为空），跳过授予。");
            return VillageCapabilityGrantResult.InvalidArguments;
        }

        // 唯一长期人物真相源（场景会话级绑定；不复制、不新建第二份 Person 数据）。
        PlayerCollection source = VillageRuntimeBridge.SimulationPersonSource;
        if (source == null)
        {
            Debug.LogWarning("[VillageCapabilityGrant] 未绑定 SimulationPersonSource，跳过授予（person="
                + personId + "，card=" + cardId + "）。");
            return VillageCapabilityGrantResult.SkippedNoPersonSource;
        }

        // M1：只按真实 PersonId 解析；找不到 → 跳过（不用 TemplateId / CardId 兜底）。
        IPersonGrowth person = source.FindPersonByPersonId(personId);
        if (person == null)
        {
            Debug.LogWarning("[VillageCapabilityGrant] 找不到长期 Person（personId=" + personId
                + "），跳过授予（禁止用模板 Id 猜测）。");
            return VillageCapabilityGrantResult.SkippedUnknownPerson;
        }

        // 唯一 Growth Authority：普通 Character 无 Active Profession → IsAvailable=false。
        // 该情况下必须跳过：其 authority 池是共享静态空池，写入会污染全局；也禁止改走 Legacy 池。
        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable)
        {
            Debug.LogWarning("[VillageCapabilityGrant] Person 无可用 Growth Authority（personId=" + personId
                + "），跳过授予（不 fallback Legacy 池 / 不写空池）。");
            return VillageCapabilityGrantResult.SkippedNoGrowthAuthority;
        }

        List<string> pool = auth.UnlockedPoolCardIds;
        if (pool == null)
        {
            Debug.LogWarning("[VillageCapabilityGrant] Authority Pool 为 null（personId=" + personId
                + "），跳过授予（不猜测池）。");
            return VillageCapabilityGrantResult.SkippedNoPool;
        }

        bool existed = pool.Contains(cardId);
        EnsureCardAtHead(pool, cardId);

        if (!existed)
        {
            // 可读日志（Test / Not Frozen 名称，仅可读性用途）。
            Debug.Log("[VillageCapabilityGrant] Test Capability 授予：" + TestCapabilityName
                + " → person=" + personId + "，card=" + cardId + "（Unlocked Pool 扩大，Test / Not Frozen）。");
        }
        return existed ? VillageCapabilityGrantResult.AlreadyPresent : VillageCapabilityGrantResult.Granted;
    }

    /// <summary>
    /// 【Test / Not Frozen Observable Strategy】确保卡存在且位于 index 0。
    /// 去重（重复授予不重复 CardId）；不新增第二套池、不改 Pool 所有权。
    /// </summary>
    private static void EnsureCardAtHead(List<string> pool, string cardId)
    {
        pool.Remove(cardId);    // 去重：同 CardId 不重复
        pool.Insert(0, cardId); // Test 可观测策略：granted card 置于首位（非正式排序设计）
    }
}

/// <summary>
/// VillageCapabilityGrant 的单次授予结果（Test / Not Frozen）。
/// </summary>
public enum VillageCapabilityGrantResult
{
    /// <summary>本次新加入该卡（Pool 扩大）。</summary>
    Granted = 0,

    /// <summary>该卡此前已存在（重复授予不重复 CardId；仅确保可观测位置）。</summary>
    AlreadyPresent = 1,

    /// <summary>参数非法（空 personId / cardId）。</summary>
    InvalidArguments = 2,

    /// <summary>未绑定长期人物真相源（SimulationPersonSource 为 null）。</summary>
    SkippedNoPersonSource = 3,

    /// <summary>按 PersonId 找不到长期 Person（不猜测身份）。</summary>
    SkippedUnknownPerson = 4,

    /// <summary>该 Person 无可用 Growth Authority（普通 Character 未形成 Profession）→ 跳过。</summary>
    SkippedNoGrowthAuthority = 5,

    /// <summary>Authority Pool 为 null → 跳过（不猜测池）。</summary>
    SkippedNoPool = 6
}

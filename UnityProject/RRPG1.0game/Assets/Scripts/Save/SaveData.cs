using System;
using System.Collections.Generic;
using RRPG.Event;
using RRPG.Social;
using RRPG.AutonomousActivity;

/// <summary>
/// P0 single-slot save DTO.
///
/// Stores only long-term player collection data. ScriptableObject references,
/// battle state, map progress, UI state, and DataManager caches are not saved.
/// </summary>
[Serializable]
public class SaveData
{
    public int SaveVersion;
    public string GameVersion;
    public long SavedAtUnixTime;
    public PlayerCollectionSaveData Player;

    /// <summary>Nullable world-social-domain snapshot. Old saves restore empty; SaveVersion stays 1.</summary>
    public SocialWorldSaveData SocialWorld;

    /// <summary>Nullable activity derived-state snapshot; no transient activity state, version stays 1.</summary>
    public ActivityWorldSaveData ActivityWorld;
    public RRPG.AutonomousSelection.AutonomousDecisionWorldSaveData AutonomousDecisionWorld;
    public RRPG.AutonomousSocialFeedback.SocialFeedbackWorldSaveData SocialFeedbackWorld;

    /// <summary>
    /// Event World 区块（WP-Event-1D，可空增量，不提升 SaveVersion）。
    /// 只保存 World Flags / World Event History / 至多一条未处理的 Village Event
    /// （Expedition Session History 与 Completed Instance 刻意不落档）。
    /// 旧档缺失 → 空默认（由 <c>EventRuntimeBridge.Restore(null)</c> 初始化）。
    /// </summary>
    public EventWorldSaveData EventWorld;

    /// <summary>
    /// Village 区块（Stage V1，可空）。
    /// 旧档缺失该字段时由 SaveSystem 以兼容默认状态初始化；不强制提升 SaveVersion
    /// （依据存档规格：SaveVersion 拒绝未知版本，提升会破坏旧档；本字段为向后兼容的可空增量，
    /// 与 Growth 长期成长字段加入存档时不提升版本的模式一致）。
    /// </summary>
    public VillageSaveData Village;
}

/// <summary>
/// Serializable snapshot of PlayerCollection.
/// </summary>
[Serializable]
public class PlayerCollectionSaveData
{
    public int Gold;
    public List<HeroSaveData> Heroes = new List<HeroSaveData>();
    public List<CharacterSaveData> Characters = new List<CharacterSaveData>();
    public List<string> OwnedCharacterCardIds = new List<string>();
    /// <summary>
    /// Legacy / Deprecated（WP-C）：独立 Item Card 产品语义已退出，Current Save 写入空值、
    /// Load 安全忽略；字段为旧档兼容 no-op，继续保留以保证旧档可反序列化（SaveVersion 不变）。
    /// </summary>
    public List<string> OwnedItemCardIds = new List<string>();

    /// <summary>角色卡 → PersonId 绑定（M1，可空）。旧档缺失时缺绑定的卡保持 unresolved（SaveSystem 不按模板候选唯一猜测补绑，仅告警）。</summary>
    public List<CharacterCardOwnerSaveData> OwnedCharacterCardOwners;
}

/// <summary>
/// Serializable long-term hero progress.
/// </summary>
[Serializable]
public class HeroSaveData
{
    public string HeroId;
    /// <summary>具体英雄实例的唯一稳定身份（M1）。旧档缺失时由 SaveSystem 生成并在下一次保存时写入。</summary>
    public string PersonId;
    public int Level;
    public int Exp;
    public InjuryStatus Injury;
    public int MaxHP;

    // V0.1 人物长期成长（测试值未冻结；旧存档缺失时由 SaveSystem 恢复默认种子）
    public List<StarterSlotSaveData> StarterSlots;
    public List<string> UnlockedPoolCardIds;
    public string TraitId;
    public int TraitLevel;

    // WP-3（additive）：Person-owned Fulfillment / Hunger 最小状态（Character/Hero 语义一致）。
    // 旧档缺失 → false / 0（值类型缺省自动默认，SaveVersion 保持 1，无需哨兵）。
    public bool Hungry;
    public int ConsecutiveUnderfedDays;
}

/// <summary>
/// Serializable long-term character progress.
/// </summary>
[Serializable]
public class CharacterSaveData
{
    public string CharacterId;

    /// <summary>
    /// Initial Person &amp; Profession Bootstrap（additive，不提升 SaveVersion）：
    /// 该人物由哪个 PersonDefinition 创建（静态配置 provenance）。
    /// 用途：Load 时恢复人物表现/战斗默认值（人物名字、战斗定义兜底）。
    /// 空 = 旧档（迁移前）或非 PersonDefinition 创建路径 → 走 Legacy 回退，不猜人物定义。
    /// 注意：这不是身份字段；人物身份仍只有 PersonId。
    /// </summary>
    public string PersonDefinitionId;

    /// <summary>
    /// 人物显示名（表现字段；玩家识别人靠名字）。
    /// 由 New Game Bootstrap 从 PersonDefinition.DisplayName 写入具体人物，Load 时优先按本字段恢复；
    /// 旧档缺失 → 回退 PersonDefinition.DisplayName → 战斗定义名（Legacy）。
    /// 不是身份 Authority（身份只有 PersonId）。
    /// </summary>
    public string DisplayName;

    /// <summary>具体角色实例的唯一稳定身份（M1）。旧档缺失时由 SaveSystem 生成并在下一次保存时写入。</summary>
    public string PersonId;
    public int Level;
    public int Exp;
    public int CurrentAttack;
    public InjuryStatus Injury;
    public bool IsDead;
    public int MaxHP;

    // V0.1 人物长期成长（测试值未冻结；旧存档缺失时由 SaveSystem 恢复默认种子）
    public List<StarterSlotSaveData> StarterSlots;
    public List<string> UnlockedPoolCardIds;
    public string TraitId;
    public int TraitLevel;

    /// <summary>
    /// M3：普通 Person 的 Active Profession 进度存在标记（Person-owned）。
    /// true = 该 Character 的存档里带真实 Active Profession Progress（可读取下方 ActiveProfessionProgress 区块）；
    /// false = 尚未正式形成 Profession（合法，非异常）。
    ///
    /// 为什么需要显式 bool：本团结引擎的 JsonUtility 会把“null / 缺失”的可序列化 class 字段在序列化时写成默认实例、
    /// 反序列化时物化为非 null 默认对象，因此不能用“DTO 引用是否为 null”表达 Active 与否（实测：null → 默认实例，
    /// 缺失 key → 默认实例）。Active 语义由本哨兵唯一表达。
    ///
    /// 兼容：旧档无此字段 → false（未形成 Profession），SaveSystem 恢复为 null，
    /// 不强制提升 SaveVersion（与 Village 区块 / Growth 长期成长字段加入存档时不提升版本的可空增量模式一致），
    /// 也绝不根据 CharacterId / Assignment / 旧 Level/Exp 自动推断职业。
    /// </summary>
    public bool HasActiveProfessionProgress;

    /// <summary>
    /// M3：Active Profession Progress 存档区块（仅在 HasActiveProfessionProgress = true 时具有语义）。
    /// 注意：即使无职业，序列化输出也会出现该字段的默认实例；是否 Active 一律以 HasActiveProfessionProgress 为准。
    /// </summary>
    public ProfessionProgressSaveData ActiveProfessionProgress;

    /// <summary>
    /// M6（Stage 1/3）：Person-owned Formation 算法派生状态存档区块（可空增量，不提升 SaveVersion）。
    /// 旧档缺失 / 无 Formation 活动 → Restore 为空状态（合法）；绝不根据 CharacterId / Assignment /
    /// Legacy Growth / TotalExecutedDays 推断 Formation；unknown Profession 条目保留原样，不改投其它职业。
    /// Formation Evidence / Strength 在首次 Formation 后继续保留（不清零）。
    /// </summary>
    public FormationStateSaveData FormationState;

    /// <summary>
    /// M6（Stage 1/3）：最小 Person Life History（人生事实 Source of Record）存档区块（可空增量，不提升 SaveVersion）。
    /// 旧档缺失 → 空 History（合法）；与 FormationState 职责分离。当前只可靠持久记录 ProfessionFormed(Date, ProfessionId)。
    /// </summary>
    public PersonLifeHistorySaveData LifeHistory;

    // WP-3（additive）：Person-owned Fulfillment / Hunger 最小状态（Character/Hero 语义一致）。
    // 旧档缺失 → false / 0（值类型缺省自动默认，SaveVersion 保持 1，无需哨兵）。
    public bool Hungry;
    public int ConsecutiveUnderfedDays;
}

/// <summary>
/// 角色卡 → 具体人物实例的持久化绑定（M1）。
/// 一张角色卡（CharacterCardData）代表卡册中的一个具体人物；同模板多卡各自绑定不同 PersonId。
/// 旧档（无该字段）：缺绑定的卡保持 unresolved（SaveSystem 不按模板候选唯一猜测补绑，仅告警）。
/// </summary>
[Serializable]
public class CharacterCardOwnerSaveData
{
    public string CardId;
    public string PersonId;
}

/// <summary>
/// Serializable Starter slot entry（一个槽 = 每次 Run 开始时 1 张对应起始牌副本）。
/// </summary>
[Serializable]
public class StarterSlotSaveData
{
    public string CardId;
    public int Proficiency;
}

/// <summary>
/// M3 存档 DTO：普通 Person 的 Active Profession 进度。
/// 与 Profession.Runtime 的 ProfessionProgress（Person-owned runtime 容器）字段一一镜像；
/// 由 SaveSystem 负责 Build（runtime → DTO）与 Restore（DTO → runtime，独立新实例）。
/// </summary>
[Serializable]
public class ProfessionProgressSaveData
{
    public string ProfessionId;
    public int ProfessionLevel;
    public int ProfessionExp;
    public List<string> UnlockedPoolCardIds;
    public int ProfessionTraitLevel;
}

/// <summary>
/// M6 存档 DTO：具体 Person 的 Formation 算法派生状态。
/// 与 Profession.Runtime 的 FormationState / ProfessionFormationEntry 字段一一镜像；
/// 由 SaveSystem 负责 Build（runtime → DTO）与 Restore（DTO → runtime，独立新实例）。
/// old-save 缺失 / 空状态 → Restore 为空状态（合法）。数值均 Test / Not Frozen。
/// </summary>
[Serializable]
public class FormationStateSaveData
{
    public List<ProfessionFormationEntrySaveData> Entries;
    public bool FormationWindowOpen;
    public string FormationTargetProfessionId;
    public bool IsAmbiguous;
}

/// <summary>单条 Person×Profession Formation 派生状态存档镜像。</summary>
[Serializable]
public class ProfessionFormationEntrySaveData
{
    public string ProfessionId;
    public float Strength;
    public int ActiveCareerDays;
    public int InactiveDays;
    public float Stability;
    public bool IsCandidate;
}

/// <summary>
/// M6 存档 DTO：最小 Person Life History（人生事实 Source of Record）。
/// 与 Profession.Runtime 的 PersonLifeHistory 一一镜像；当前只记录 ProfessionFormed(Date, ProfessionId)。
/// </summary>
[Serializable]
public class PersonLifeHistorySaveData
{
    public List<PersonLifeEventSaveData> Events;
}

/// <summary>单条人生事实存档镜像（Type = PersonLifeEventType 枚举序；M6 只落 ProfessionFormed=0）。</summary>
[Serializable]
public class PersonLifeEventSaveData
{
    public int Type;
    public int Year;
    public int Month;
    public int Day;
    public string ProfessionId;

    /// <summary>
    /// V4 Gate 2（additive，不提升 SaveVersion）：ExpeditionReturned 的 Return Outcome（RunEndMode 枚举序）；
    /// 其它类型不使用；旧档缺失 → 0（NotSet）。
    /// </summary>
    public int Outcome;
}


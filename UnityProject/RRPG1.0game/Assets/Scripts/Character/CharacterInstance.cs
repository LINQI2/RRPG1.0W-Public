using System.Collections.Generic;

/// <summary>
/// 角色运行时数据
///
/// 负责：等级、经验、伤残、死亡状态、人物长期成长（Starter / Unlocked Pool / Trait）
/// 不负责：战斗逻辑、技能释放、UI显示
///
/// M4-correction（Narrow Growth Authority Routing）：
///   ActiveProfessionProgress 只是 Person-owned 的 Profession 进度容器（M3），本身不改变本类的
///   IPersonGrowth 通用长期契约。普通 Character 的 Profession Growth 权威（ProfessionLevel / ProfessionExp /
///   ProfessionProgress.UnlockedPoolCardIds / ProfessionTraitLevel）只经 IGrowthAuthority
///   （GrowthAuthority.Resolve，Growth 系统专用）显式解析；本类下方 Level/Exp/UnlockedPoolCardIds/
///   TraitId/TraitLevel 保留为 Legacy 字段（Run/Save/Reward/StarterDeck/播种等一般长期语义沿用，
///   最终删除属后续 Cleanup，不在 M4）。
///
/// 每场战斗开始时满血重置：
/// BattleUnit.CurrentHP = MaxHP
/// 战斗中 BattleUnit 的变化不影响本实例
///
/// 死亡之门结果直接写入 Injury / IsDead
/// </summary>
public class CharacterInstance : IPersonGrowth, IGrowthAuthority, IFormationHost, IFulfillmentHost
{
    /// <summary>
    /// 引用 CharacterData.CharacterId（静态模板 ID）
    /// 存档友好，不依赖 SO 引用
    /// 注意：CharacterId 不是人物身份；同一模板可被多个具体人物共享。
    /// </summary>
    public string CharacterId;

    /// <summary>
    /// 本人物实例的稳定唯一身份（M1）。
    /// 一个具体人物从创建到长期数据 / Village / Expedition / Run / Battle / Reward / Save-Load 全链路不变；
    /// 同 CharacterId 的不同实例必须拥有不同 PersonId。
    /// 生成逻辑集中在 PersonIdentity.NewPersonId()；Restore 时可注入既有 PersonId。
    /// </summary>
    public string PersonId;

    /// <summary>
    /// 本人物在 New Game 由哪个 PersonDefinition 创建（静态配置 provenance；NOT Person identity）。
    /// 用途：Save/Load 时恢复人物表现/战斗默认值（DisplayName、战斗定义兜底）。
    /// 身份 Authority 永远只有 PersonId；本字段不得参与身份判定、不得与 PersonId 互换。
    /// 空 = 旧档或非 PersonDefinition 创建路径（Legacy Compatibility）。
    /// </summary>
    public string PersonDefinitionId;

    /// <summary>
    /// 引用 CharacterData.DisplayName
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 等级（Legacy Compatibility Field；M4 Growth 权威 = ActiveProfessionProgress.ProfessionLevel）
    /// </summary>
    public int Level;

    /// <summary>
    /// 当前经验（Legacy Compatibility Field；M4 Growth 权威 = ActiveProfessionProgress.ProfessionExp）
    /// </summary>
    public int Exp;

    /// <summary>
    /// 当前生命值
    /// 创建时 = MaxHP，后续通过死亡之门修改
    /// 战斗中 BattleUnit 变化不影响此值
    /// </summary>
    public int CurrentHP;

    /// <summary>
    /// 最大生命值
    /// BaseHP + 升级加点 - 伤残惩罚
    /// </summary>
    public int MaxHP;

    /// <summary>
    /// 当前攻击力
    /// BaseAttack + 升级加点 - 伤残惩罚
    /// </summary>
    public int CurrentAttack;

    /// <summary>
    /// 伤残状态
    /// 默认 Normal，死亡之门失败后修改
    /// </summary>
    public InjuryStatus Injury;

    /// <summary>
    /// 是否永久死亡
    /// 角色死亡后永久失去
    /// </summary>
    public bool IsDead;

    // ==================== 人物长期成长（Legacy 字段；V0.1，测试值未冻结） ====================
    // M4-correction：这些字段是普通 Character 的共享长期契约（Run/Save/Reward/StarterDeck/播种沿用）；
    // Starter 槽 Character-owned。普通 Character 的 Profession Growth 权威经 IGrowthAuthority 显式解析，
    // 不使用这些字段作为 Growth fallback（字段最终删除属后续 Cleanup，不在 M4）。

    /// <summary>Starter 槽（一个槽 = 每次 Run 开始时 1 张对应起始牌副本；Character-owned）</summary>
    public List<StarterSlotEntry> StarterSlots { get; set; } = new List<StarterSlotEntry>();

    /// <summary>Unlocked Pool：Legacy 长期字段（Run 奖励等一般长期语义沿用；Growth 权威见 IGrowthAuthority）</summary>
    public List<string> UnlockedPoolCardIds { get; set; } = new List<string>();

    /// <summary>人物唯一专属 Trait ID：Legacy 长期字段（M4 不为新 Profession 语义创建 Person Trait）</summary>
    public string TraitId { get; set; }

    /// <summary>该 Trait 当前等级：Legacy 长期字段（正式效果与上限未冻结）</summary>
    public int TraitLevel { get; set; }

    // ==================== Profession Progress（M3 容器；权威仅由 Growth Authority 解析） ====================

    /// <summary>
    /// 当前 Active Profession 进度（Person-owned，普通 Character 专属）。
    /// null = 该 Person 尚未正式形成 Profession —— 合法状态，不是异常。
    /// 只允许 0 或 1 个 Active ProfessionProgress；创建由 M6 Formation Commit 原子完成
    /// （形成即 ProfessionLevel = 1 / ProfessionExp = 0 / clone InitialUnlockedCards /
    /// ProfessionTraitLevel = 1 / StarterSlots 原子替换为模板 InitialStarterBuild 深拷贝）。
    /// 不实现 转职 / 多职业 / Dormant / 旧职业历史 / M6 自动 Transition；本字段不根据
    /// CharacterId / Assignment / 旧 Level/Exp 伪造职业。
    ///
    /// M4-correction（Narrow Growth Authority Routing）：本容器不改变 IPersonGrowth 通用长期契约。
    /// Profession Growth 权威只在 Growth 系统入口经 GrowthAuthority.Resolve(person) → IGrowthAuthority
    /// 解析后生效：Level → ProfessionLevel；Exp → ProfessionExp；UnlockedPool → UnlockedPoolCardIds；
    /// Trait 强化数值状态 → ProfessionTraitLevel（Trait Definition 恒来自 Profession Template 的
    /// ProfessionTraitId，不在 Person 侧复制 Person-specific Trait 定义）。
    /// </summary>
    public ProfessionProgress ActiveProfessionProgress { get; set; }

    /// <summary>是否已存在 Active Profession（= ActiveProfessionProgress != null）。</summary>
    public bool HasActiveProfession => ActiveProfessionProgress != null;

    // ==================== Formation（M6；Person-owned；普通 Character 专属；Hero 不拥有） ====================
    // 职责分离（GameSpec §12.7）：
    //   LifeHistory     = 人生事实 Source of Record（本轮只记录 ProfessionFormed）；
    //   FormationState  = Formation 算法派生状态（Strength / Stability / Target / Ambiguity 等）。
    // 两者均随 Save/Load 连续（CharacterSaveData 增量区块，SaveVersion 保持 1）；旧档缺失 → 空状态。
    // 绝不根据 CharacterId / Assignment / Legacy Growth / TotalExecutedDays 推断或伪造 Formation。

    /// <summary>
    /// Formation 算法派生状态（Person-owned；空状态合法，恒非 null）。
    /// Formation Evidence / Strength 在首次 Formation 后继续保留（不清零）。
    /// </summary>
    public FormationState FormationState { get; set; } = new FormationState();

    /// <summary>
    /// 最小 Person Life History（人生事实 Source of Record；M6 只记录 ProfessionFormed 事件）。
    /// 与 FormationState 严格职责分离。
    /// </summary>
    public PersonLifeHistory LifeHistory { get; set; } = new PersonLifeHistory();

    // ==================== Fulfillment / Hunger（WP-3；Person-owned；Character/Hero 语义一致） ====================
    // 最小长期状态：Hungry（当日未满足）+ ConsecutiveUnderfedDays（连续未满足天数，满足日清零）。
    // 只由 VillageResourceSettlement 每日结算写回（Gate 3 接线）；本类只负责承载与 Save/Load 连续。
    // SaveVersion 保持 1：旧档缺失 → false / 0（值类型缺省自动默认，无需哨兵）。
    // 禁止在本类引入 Hunger 后果 / 效率惩罚等新状态系统（后果数值 Not Frozen）。

    /// <summary>当日是否处于未满足（Hungry）状态（WP-3；Person-owned；默认 false）。</summary>
    public bool Hungry { get; set; }

    /// <summary>连续未满足天数（满足日清零；Person-owned；默认 0，恒 >= 0）。</summary>
    public int ConsecutiveUnderfedDays { get; set; }

    // IFormationHost（M6；Person-owned Formation 宿主）：
    //   PersonId 显式实现（本类字段 PersonId 不能隐式实现接口属性）；
    //   FormationState / LifeHistory / HasActiveProfession / StarterSlots（get+set）/
    //   ActiveProfessionProgress（get+set）由上方公开属性/字段隐式实现。
    string IFormationHost.PersonId => PersonId;

    // IFulfillmentHost（WP-3）：PersonId 显式实现；Hungry / ConsecutiveUnderfedDays 由上方属性隐式实现。
    string IFulfillmentHost.PersonId => PersonId;

    /// <summary>
    /// 无 Active Profession 时返回的空权威池（防御值：Growth 在 IsAvailable=false 时不会读取）。
    /// </summary>
    private static readonly List<string> EmptyAuthorityPool = new List<string>();

    // IPersonGrowth（共享长期契约，Legacy 语义）：不因 ActiveProfessionProgress 改变宿主。
    // PersonId/DisplayName/Level/Exp 显式映射到本类字段；StarterSlots/UnlockedPoolCardIds/TraitId/TraitLevel
    // 由下方公开属性隐式实现（与 M1-M3 一致）。
    string IPersonGrowth.PersonId => PersonId;
    string IPersonGrowth.DisplayName => DisplayName;
    int IPersonGrowth.Level { get => Level; set => Level = value; }
    int IPersonGrowth.Exp { get => Exp; set => Exp = value; }

    // IGrowthAuthority（Growth.Runtime 专用；仅 Growth 系统经 GrowthAuthority.Resolve 使用）：
    //   - 无 Active Profession：IsAvailable=false —— Growth 不可执行，不读写 Legacy 作为 fallback；
    //   - 有 Active Profession：权威读写 = ActiveProfessionProgress（Starter 仍 Character-owned）。
    bool IGrowthAuthority.IsAvailable => HasActiveProfession;
    bool IGrowthAuthority.IsProfessionGrowth => HasActiveProfession;

    int IGrowthAuthority.Level
    {
        get
        {
            var pp = ActiveProfessionProgress;
            return pp != null ? pp.ProfessionLevel : 0;
        }
        set
        {
            var pp = ActiveProfessionProgress;
            if (pp != null) pp.ProfessionLevel = value;
        }
    }

    int IGrowthAuthority.Exp
    {
        get
        {
            var pp = ActiveProfessionProgress;
            return pp != null ? pp.ProfessionExp : 0;
        }
        set
        {
            var pp = ActiveProfessionProgress;
            if (pp != null) pp.ProfessionExp = value;
        }
    }

    List<string> IGrowthAuthority.UnlockedPoolCardIds
    {
        get
        {
            var pp = ActiveProfessionProgress;
            return pp != null ? pp.UnlockedPoolCardIds : EmptyAuthorityPool;
        }
    }

    string IGrowthAuthority.TraitId
    {
        get => null;
        set { /* Profession Growth 不创建 Person-specific Trait / 正式 TraitId（M4-correction） */ }
    }

    int IGrowthAuthority.TraitLevel
    {
        get
        {
            var pp = ActiveProfessionProgress;
            return pp != null ? pp.ProfessionTraitLevel : 0;
        }
        set
        {
            var pp = ActiveProfessionProgress;
            if (pp != null) pp.ProfessionTraitLevel = value;
        }
    }

    List<StarterSlotEntry> IGrowthAuthority.StarterSlots => StarterSlots; // Starter 仍 Character-owned


    /// <summary>
    /// 创建角色实例
    /// 基于 CharacterData 初始化运行时数据
    /// </summary>
    /// <param name="characterId">角色模板ID</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="baseHP">基础生命值</param>
    /// <param name="baseAttack">基础攻击力</param>
    public CharacterInstance(string characterId, string displayName, int baseHP, int baseAttack)
    {
        CharacterId = characterId;
        PersonId = PersonIdentity.NewPersonId(); // M1：新人物创建时立即拥有唯一实例身份
        DisplayName = displayName;
        Level = 1;
        Exp = 0;
        MaxHP = baseHP;
        CurrentAttack = baseAttack;
        CurrentHP = MaxHP;
        Injury = InjuryStatus.Normal;
        IsDead = false;
    }
}

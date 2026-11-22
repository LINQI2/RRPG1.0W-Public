using System.Collections.Generic;

/// <summary>
/// 英雄运行时数据
///
/// 负责：等级、经验、当前状态、人物长期成长（Starter / Unlocked Pool / Trait）
/// 不负责：战斗逻辑、技能释放、UI显示
///
/// 英雄不会永久死亡
/// 被击倒后强制下阵（不进入死亡之门），战斗不结束
///
/// 每场战斗开始时满血重置：
/// BattleUnit.CurrentHP = MaxHP
/// </summary>
public class HeroInstance : IPersonGrowth, IGrowthAuthority, IFulfillmentHost
{
    /// <summary>
    /// 引用 HeroData.HeroId（静态模板 ID）
    /// 存档友好，不依赖 SO 引用
    /// 注意：HeroId 不是人物身份；同模板多实例必须用 PersonId 区分。
    /// </summary>
    public string HeroId;

    /// <summary>
    /// 本英雄实例的稳定唯一身份（M1）。
    /// 生成逻辑集中在 PersonIdentity.NewPersonId()；Restore 时可注入既有 PersonId。
    /// </summary>
    public string PersonId;

    /// <summary>
    /// 引用 HeroData.DisplayName
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 等级
    /// </summary>
    public int Level;

    /// <summary>
    /// 当前经验
    /// </summary>
    public int Exp;

    /// <summary>
    /// 当前生命值
    /// 创建时 = MaxHP
    /// 战斗中 BattleUnit 变化不影响此值
    /// </summary>
    public int CurrentHP;

    /// <summary>
    /// 最大生命值
    /// BaseHP + 等级成长
    /// </summary>
    public int MaxHP;

    /// <summary>
    /// 伤残状态
    /// 默认 Normal
    /// </summary>
    public InjuryStatus Injury;

    // ==================== 人物长期成长（V0.1，测试值未冻结） ====================

    /// <summary>Starter 槽（一个槽 = 每次 Run 开始时 1 张对应起始牌副本）</summary>
    public List<StarterSlotEntry> StarterSlots { get; set; } = new List<StarterSlotEntry>();

    /// <summary>Unlocked Pool：当前已解锁、允许被随机获得的角色牌模板ID集合</summary>
    public List<string> UnlockedPoolCardIds { get; set; } = new List<string>();

    /// <summary>人物唯一专属 Trait ID（TraitUpgrade 强化同一个，不替换）</summary>
    public string TraitId { get; set; }

    /// <summary>该 Trait 当前等级（正式效果与上限未冻结）</summary>
    public int TraitLevel { get; set; }

    // ==================== Fulfillment / Hunger（WP-3；Person-owned；Character/Hero 语义一致） ====================
    // 最小长期状态：Hungry（当日未满足）+ ConsecutiveUnderfedDays（连续未满足天数，满足日清零）。
    // 只由 VillageResourceSettlement 每日结算写回（Gate 3 接线）；本类只负责承载与 Save/Load 连续。
    // SaveVersion 保持 1：旧档缺失 → false / 0（值类型缺省自动默认，无需哨兵）。

    /// <summary>当日是否处于未满足（Hungry）状态（WP-3；Person-owned；默认 false）。</summary>
    public bool Hungry { get; set; }

    /// <summary>连续未满足天数（满足日清零；Person-owned；默认 0，恒 >= 0）。</summary>
    public int ConsecutiveUnderfedDays { get; set; }

    // IPersonGrowth 显式实现：PersonId 返回真正的实例身份（不再是模板 HeroId）；
    // 其余复用既有 Level/Exp/DisplayName 字段。Hero 不进入 Profession 迁移（M4 Hero 零迁移），
    // 且 IPersonGrowth 共享长期契约保持 Legacy 语义。
    string IPersonGrowth.PersonId => PersonId;
    string IPersonGrowth.DisplayName => DisplayName;
    int IPersonGrowth.Level { get => Level; set => Level = value; }
    int IPersonGrowth.Exp { get => Exp; set => Exp = value; }

    // IFulfillmentHost（WP-3）：PersonId 显式实现；Hungry / ConsecutiveUnderfedDays 由上方属性隐式实现。
    string IFulfillmentHost.PersonId => PersonId;

    // IGrowthAuthority（Growth.Runtime 专用；经 GrowthAuthority.Resolve 解析）：Hero = Legacy 权威。
    // 不为 Hero 增加 ActiveProfessionProgress / Profession 结构。
    bool IGrowthAuthority.IsAvailable => true;
    bool IGrowthAuthority.IsProfessionGrowth => false;
    int IGrowthAuthority.Level { get => Level; set => Level = value; }
    int IGrowthAuthority.Exp { get => Exp; set => Exp = value; }
    List<string> IGrowthAuthority.UnlockedPoolCardIds => UnlockedPoolCardIds;
    string IGrowthAuthority.TraitId { get => TraitId; set => TraitId = value; }
    int IGrowthAuthority.TraitLevel { get => TraitLevel; set => TraitLevel = value; }
    List<StarterSlotEntry> IGrowthAuthority.StarterSlots => StarterSlots;

    /// <summary>
    /// 创建英雄实例
    /// 基于 HeroData 初始化运行时数据
    /// </summary>
    /// <param name="heroId">英雄模板ID</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="baseHP">基础生命值</param>
    public HeroInstance(string heroId, string displayName, int baseHP)
    {
        HeroId = heroId;
        PersonId = PersonIdentity.NewPersonId(); // M1：新人物创建时立即拥有唯一实例身份
        DisplayName = displayName;
        Level = 1;
        Exp = 0;
        MaxHP = baseHP;
        CurrentHP = MaxHP;
        Injury = InjuryStatus.Normal;
    }
}

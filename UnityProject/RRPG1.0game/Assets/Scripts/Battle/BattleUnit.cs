using System.Collections.Generic;

/// <summary>
/// 战斗单位
///
/// 负责：保存战斗中的角色当前状态
/// 不负责：技能逻辑计算、UI显示、存档
///
/// 角色、英雄、怪物进入战斗后全部转换为 BattleUnit
/// 进入战斗时满血：CurrentHP = MaxHP
/// 实际可攻击次数 = min(CurrentActionPoints, AttackCount)
/// BattleUnit 不包含 TP 字段（TP 是队伍公共资源，由 TPSystem 管理）
/// </summary>
public class BattleUnit
{
    /// <summary>
    /// 战斗实例ID
    /// </summary>
    public string UnitId;

    /// <summary>
    /// 来源ID
    /// 引用 CharacterData/HeroData 的ID；敌方为 StaticConfig MonsterDefinition.MonsterId
    /// </summary>
    public string SourceId;

    /// <summary>
    /// 单位显示名（表现字段）：部署时由具体人物/英雄的长期状态带入，仅用于 UI 显示。
    /// 不是身份 Authority（长期身份见 PersonId / Run Person）；为空时调用方回退到既有 Legacy 显示逻辑。
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 单位类型
    /// Character / Hero / Monster
    /// </summary>
    public UnitType UnitType;

    /// <summary>
    /// 当前生命
    /// 战斗开始时满血（= MaxHP）
    /// </summary>
    public int CurrentHP;

    /// <summary>
    /// 最大生命
    /// </summary>
    public int MaxHP;

    /// <summary>
    /// 当前攻击力
    /// </summary>
    public int Attack;

    /// <summary>
    /// 当前攻击次数上限
    /// </summary>
    public int AttackCount;

    /// <summary>
    /// 攻击距离
    /// 决定能攻击到前方第几排
    /// </summary>
    public int AttackRange;

    /// <summary>
    /// 当前行动力
    /// 每回合回满至上限
    /// [LEGACY-V0.2.3] Battle V0.1 起个人 AP 不再具有当前玩法意义（共享行动力 SharedActionPointPool 替代）。
    /// 本字段保留仅供旧流程编译运行，新系统不得使用。
    /// </summary>
    public int CurrentActionPoints;

    /// <summary>
    /// 行动力上限
    /// 默认值为 1
    /// [LEGACY-V0.2.3] Battle V0.1 起个人 AP 不再具有当前玩法意义（共享行动力 SharedActionPointPool 替代）。
    /// 本字段保留仅供旧流程编译运行，新系统不得使用。
    /// </summary>
    public int MaxActionPoints;

    /// <summary>
    /// 阵营
    /// 来源自 CharacterData/HeroData（敌方为 StaticConfig MonsterDefinition）
    /// </summary>
    public string Faction;

    /// <summary>
    /// 当前排
    /// Front / Middle / Back
    /// </summary>
    public BattleRow Row;

    /// <summary>
    /// 当前列
    /// Left / Center / Right
    /// </summary>
    public BattleColumn Column;

    /// <summary>
    /// 是否倒地
    /// </summary>
    public bool IsDown;

    /// <summary>
    /// 是否处于死亡之门
    /// </summary>
    public bool InDeathDoor;

    /// <summary>
    /// 本场临时护盾（Battle V0.1 Gate 0）。
    /// 只属于当前 Battle 的临时防御值：受击时先由护盾吸收，不足部分再扣 HP；
    /// 下一玩家回合开始时清除剩余护盾。不进入 Run / Person / Save / 下一场 Battle。
    /// </summary>
    public int Shield;

    /// <summary>
    /// 本场战斗中被击中的次数
    /// 影响死亡之门概率（初始10%，每被击中+10%）
    /// </summary>
    public int HitCount;

    /// <summary>
    /// 状态运行时列表
    /// </summary>
    public List<StatusRuntime> StatusList;

    /// <summary>
    /// 创建战斗单位（从角色/英雄实例初始化）
    /// 进入战斗时满血；Stage 6A Run 链路由调用方在构造后按 Run CurrentHP 覆盖（见 BattleSystem.DeployCharacter/DeployHero）。
    /// 注意：保持本构造函数参数个数不变（既有测试经 Activator 反射构造）。
    /// </summary>
    public BattleUnit(string unitId, string sourceId, UnitType unitType,
                      int maxHP, int attack, int attackCount, int attackRange,
                      int maxActionPoints, string faction)
    {
        UnitId = unitId;
        SourceId = sourceId;
        UnitType = unitType;

        MaxHP = maxHP;
        CurrentHP = maxHP; // 默认满血；Run 链路由调用方覆盖

        Attack = attack;
        AttackCount = attackCount;
        AttackRange = attackRange;
        MaxActionPoints = maxActionPoints;
        CurrentActionPoints = maxActionPoints;

        Faction = faction;

        // 默认前排中列
        Row = BattleRow.Front;
        Column = BattleColumn.Center;

        IsDown = false;
        InDeathDoor = false;

        StatusList = new List<StatusRuntime>();
    }
}
using System.Collections.Generic;
using System.Linq;
using RRPG.StaticConfig;
using UnityEngine;

/// <summary>
/// 战斗系统
///
/// 负责：战斗流程主控，整合所有系统
/// 不负责：回合细节、技能逻辑、状态计算（交由各子系统处理）
///
/// 完整战斗流程：
///   Initialize → 战前准备 → StartBattle → 回合循环 → 胜负判定
/// </summary>
public partial class BattleSystem
{
    private BattleData _battleData;
    private TurnSystem _turnSystem;
    private SkillSystem _skillSystem;
    private StatusSystem _statusSystem;
    private DeathDoorSystem _deathDoorSystem;
    private CardSystem _cardSystem;

    /// <summary>
    /// 创建战斗系统，初始化所有子系统
    /// </summary>
    /// <param name="characterInstances">角色实例列表</param>
    /// <param name="heroInstance">英雄实例（可为null）</param>
    /// <param name="probabilityConfig">受伤概率配置</param>
    public BattleSystem(List<CharacterInstance> characterInstances, HeroInstance heroInstance,
                        InjuryProbabilityConfig probabilityConfig)
    {
        _battleData = new BattleData();
        _cardSystem = new CardSystem();
        _turnSystem = new TurnSystem(_battleData, _cardSystem);
        _statusSystem = new StatusSystem(_battleData);
        _skillSystem = new SkillSystem(_battleData, _statusSystem);
        _deathDoorSystem = new DeathDoorSystem(characterInstances, probabilityConfig);
    }

    /// <summary>
    /// 初始化战斗
    /// 战前准备：部署区、抽牌堆、上阵单位
    ///
    /// 【Legacy / Demo 兼容重载】——**不是** WP-Encounter-1C 起的产品敌方入场路径。
    /// 语义保持与 M2 Slice 2A 完全一致：按 list 顺序生成敌方单位，只有前 3 只进入 Enemy Board
    /// （索引 → Back 排列 0..2）。产品路径请用 WP-Encounter-1C 的 EnemyRoster 重载
    /// （见 BattleSystemEnemyRoster.cs），它会把 1..9 只全部合法部署。
    /// </summary>
    /// <param name="playerCharacters">玩家选择的角色卡</param>
    /// <param name="enemyMonsters">敌方怪物定义（M2 Slice 2A：StaticConfig MonsterDefinition；字段语义不变）</param>
    /// <param name="playerSkills">玩家初始技能</param>
    /// <param name="enemySkills">敌方初始技能</param>
    public void InitializeBattle(List<CharacterCardData> playerCharacters,
                                  IReadOnlyList<MonsterDefinition> enemyMonsters,
                                  List<SkillData> playerSkills, List<SkillData> enemySkills)
    {
        PrepareBattleBeforeEnemySetup(playerCharacters, enemySkills);

        // 生成敌方单位（Legacy 顺序语义：list index → Column；只有前 3 只进入 Board）
        foreach (var monster in enemyMonsters)
        {
            var battleUnit = CreateMonsterUnit(monster);
            _battleData.EnemyUnits.Add(battleUnit);

            int idx = _battleData.EnemyUnits.Count - 1;
            if (idx <= 2)
                // Battle V0.1 R5.0：敌方单位进入 Enemy Board（与 Player Board 完全独立占位）
                _battleData.EnemyField.Deploy(battleUnit, BattleRow.Back, (BattleColumn)idx);
        }
    }

    /// <summary>
    /// 战斗初始化中「与敌方阵容无关」的公共前置步骤（阶段 / 敌方 Intent 残留清理 / 牌组 / 部署区 /
    /// 敌方技能注册）。两个 InitializeBattle 重载共用，避免出现两套战前准备语义。
    /// </summary>
    private void PrepareBattleBeforeEnemySetup(List<CharacterCardData> playerCharacters, List<SkillData> enemySkills)
    {
        _battleData.CurrentPhase = BattlePhase.Preparation;

        // Stage 5：清空上一场敌人的 Intent / 防御 / OncePerBattle 运行时状态（多场战斗防残留）
        ResetEnemyIntentState();

        // 1. 卡牌系统准备出征牌组
        _cardSystem.PrepareDeck(playerCharacters);

        // 2. 设置战前部署区（抽6张角色卡，未上阵进入手牌）
        _cardSystem.SetupPreparationZone(_battleData.PlayerHand);

        // 3. 注册敌方技能（玩家技能由 DeployCharacter/DeployHero 时按单位注册）
        _skillSystem.RegisterSkills(enemySkills);
    }

    /// <summary>
    /// 开始战斗（触发 BattleStart 技能）
    /// </summary>
    public void StartBattle()
    {
        _battleData.CurrentPhase = BattlePhase.BattleStart;

        // 触发所有 BattleStart 技能
        foreach (var unit in _battleData.PlayerUnits)
        {
            _skillSystem.TriggerSkills(TriggerType.BattleStart, unit);
        }
        foreach (var unit in _battleData.EnemyUnits)
        {
            _skillSystem.TriggerSkills(TriggerType.BattleStart, unit);
        }

        // 发布战斗开始事件
        EventBus.Publish(new BattleStartEvent());
    }

    /// <summary>
    /// 回合前半部分：回合推进 → 抽牌 → 决策阶段
    /// （Battle V0.1：TP 与角色个人行动力已退出玩家资源模型，本方法不再恢复二者）
    /// </summary>
    public void ExecuteTurnPart1()
    {
        if (_battleData.IsBattleOver) return;

        // 1. 回合开始阶段（TurnStart：回合数推进 + 事件发布）
        _turnSystem.StartTurn();

        // 触发 TurnStart 技能
        foreach (var unit in _battleData.PlayerUnits)
        {
            if (!unit.IsDown && unit.CurrentHP > 0)
                _skillSystem.TriggerSkills(TriggerType.TurnStart, unit);
        }

        // 2. 抽牌阶段
        _turnSystem.EnterDrawPhase();

        // 3. 决策阶段（玩家操作，由UI触发）
        _turnSystem.EnterDecisionPhase();
    }

    /// <summary>
    /// Stage 3A：V1 玩家回合开始触发 TriggerType.TurnStart 技能（只迁移技能触发职责）。
    /// 不重新启用 Legacy ExecuteTurnPart1 的 TP 恢复 / 个人行动力恢复 / 旧抽牌 / 决策阶段。
    /// 由 BattleUI 的 V1 玩家回合循环在 BeginPlayerTurn 后调用。
    /// </summary>
    public void TriggerTurnStartSkillsForPlayer()
    {
        if (_battleData == null || _battleData.IsBattleOver) return;

        foreach (var unit in _battleData.PlayerUnits)
        {
            if (!unit.IsDown && unit.CurrentHP > 0)
                _skillSystem.TriggerSkills(TriggerType.TurnStart, unit);
        }
    }

    /// <summary>
    /// 回合后半部分：敌方攻击 → 回合结束（DOT + 状态递减） → 胜负判定
    /// </summary>
    public void ExecuteTurnPart2()
    {
        _enemyActionResults.Clear();
        if (_battleData.IsBattleOver) return;

        // Current GameSpec R15.x / EnemyTurn 生命周期：每次 EnemyTurn 开始时，
        // 仍具战斗能力的敌人恢复其当前敌方行动逻辑需要的行动力。
        // Legacy 敌方行动依赖个人 ActionPoints，但敌方个人 AP 不构成玩家双资源，
        // 玩家个人 AP 仍不被恢复（V1 共享行动力保持不变）。
        foreach (var enemy in _battleData.EnemyUnits)
        {
            if (!enemy.IsDown && enemy.CurrentHP > 0)
                enemy.CurrentActionPoints = enemy.MaxActionPoints;
        }

        // 4. 敌方攻击阶段（怪物自动攻击）
        ExecuteEnemyPhase();

        // 5. 回合结束阶段
        // 5a. 执行 DOT 伤害 + 状态递减/过期移除
        _statusSystem.OnTurnEnd();

        // 5b. 检查因 DOT 受伤导致死亡的单位
        CheckDotDeaths();

        // 5c. 行动力清零
        _turnSystem.EndTurn();

        // 6. 胜负判定
        CheckBattleResult();
    }

    /// <summary>
    /// 检查因 DOT 伤害导致单位 HP 归零的情况
    /// Current GameSpec R15.x：任何来源导致的 0HP 都立即失去本场战斗能力，不进入死亡之门
    /// </summary>
    private void CheckDotDeaths()
    {
        foreach (var unit in _battleData.PlayerUnits)
        {
            if (!unit.IsDown && unit.CurrentHP <= 0)
                ApplyUnitDown(unit);
        }
        foreach (var unit in _battleData.EnemyUnits)
        {
            if (!unit.IsDown && unit.CurrentHP <= 0)
                ApplyUnitDown(unit);
        }
    }

    /// <summary>
    /// Current GameSpec R15.x：单位 HP 归零立即失去本场战斗能力（IsDown），
    /// 不进入 Legacy 死亡之门、不产生永久死亡/伤病。供 DOT 等不直接造成伤害的路径统一调用。
    /// </summary>
    private void ApplyUnitDown(BattleUnit unit)
    {
        if (unit == null || unit.IsDown) return;

        unit.CurrentHP = 0;
        unit.IsDown = true;
        unit.InDeathDoor = false;
        // R15.4：失能后退出所属 Board 的规则层占位（释放该逻辑格）
        ReleaseBoardCell(unit);
        EventBus.Publish(new DeathEvent(unit.UnitId, unit.UnitType));
        CheckBattleResult();
    }

    /// <summary>
    /// 执行敌方攻击阶段
    /// 所有存活怪物依次行动：找目标 → 攻击
    /// 目标选择规则：同排倒地角色优先 → 前排优先 → 中排 → 后排，同排随机
    /// </summary>
    private void ExecuteEnemyPhase()
    {
        _battleData.CurrentPhase = BattlePhase.EnemyAction;

        // Stage 5：上一 EnemyTurn 的防御状态过期（防御只影响"防御回合之后的下一次受击"）
        _defendingEnemyIds.Clear();

        foreach (var enemy in _battleData.EnemyUnits)
        {
            if (enemy.IsDown || enemy.CurrentHP <= 0)
                continue;

            // Stage 5：按已锁定 Intent 执行（Action 锁定，Target 由 TargetRule 在当前战况重新选择）。
            // 未锁定时（如测试直接调用本方法）回退为普通攻击，保持旧行为。
            var intent = GetEnemyIntent(enemy);
            if (intent == null)
                intent = CreateFallbackEnemyIntent();

            _enemyActionResults.Add(ExecuteEnemyIntent(enemy, intent));
        }

        // Stage 5：本回合 Intent 已执行/消费，下一玩家回合由 LockEnemyIntents 重新生成
        ConsumeEnemyIntents();
    }

    /// <summary>
    /// 为怪物寻找攻击目标
    /// 规则：优先攻击同排倒地角色 → 然后按前排→中排→后排优先，
    /// 同排内优先血量最低的
    /// </summary>
    private BattleUnit FindEnemyTarget(BattleUnit enemy)
    {
        // 包含所有玩家单位，包括倒地的
        var allPlayers = _battleData.PlayerUnits.FindAll(u => u.CurrentHP > 0);
        if (allPlayers.Count == 0) return null;

        // 筛选攻击范围内的目标
        var inRangePlayers = new List<BattleUnit>();
        foreach (var player in allPlayers)
        {
            if (IsInRange(enemy, player))
                inRangePlayers.Add(player);
        }

        // 如果范围内没有目标，返回 null（不攻击）
        if (inRangePlayers.Count == 0)
            return null;

        // 按排优先级排序：同排倒地 > 前排 > 中排 > 后排
        // 同排内：倒地优先 > 血量低的优先
        inRangePlayers.Sort((a, b) =>
        {
            int aRow = GetPhysicalRowIndex(a.Row, true);
            int bRow = GetPhysicalRowIndex(b.Row, true);

            // 同排：倒地优先，然后血量低的优先
            if (aRow == bRow)
            {
                // 倒地优先（IsDown = true 排前面）
                if (a.IsDown != b.IsDown)
                    return a.IsDown ? -1 : 1;
                // 血量低的优先
                return a.CurrentHP.CompareTo(b.CurrentHP);
            }

            // 不同排：前排（物理索引小）优先
            return aRow.CompareTo(bRow);
        });

        return inRangePlayers[0];
    }

    /// <summary>
    /// Stage 3A 统一距离规则源：计算 attacker 与 target 之间的非空阻挡排数
    /// （不含两端、不含倒地；同排多个单位按 1 排去重）。
    /// 物理排索引按所属 Board 统一（Player Board：Back→0 .. Front→2；Enemy Board：Front→3 .. Back→5），
    /// 玩家普通攻击 / 敌方 AI / V1 卡牌共用同一公式。
    /// </summary>
    public int GetBlockingRowCount(BattleUnit attacker, BattleUnit target)
    {
        int startIdx = GetPhysicalRowIndexFor(attacker);
        int endIdx = GetPhysicalRowIndexFor(target);
        int minIdx = System.Math.Min(startIdx, endIdx);
        int maxIdx = System.Math.Max(startIdx, endIdx);

        // 对称规则（Current GameSpec R5.x）：无论攻击者属于哪一方，
        // 攻击者与目标之间所有仍具战斗能力的单位形成的非空排都计入阻挡；
        // 空排不计、同排多个单位只计一排、攻击者和目标所在排不计。
        var blockingRows = new HashSet<int>();
        foreach (var unit in _battleData.PlayerUnits.Concat(_battleData.EnemyUnits))
        {
            if (unit == attacker || unit == target || unit.IsDown || unit.CurrentHP <= 0)
                continue;

            int unitIdx = GetPhysicalRowIndexFor(unit);
            if (unitIdx > minIdx && unitIdx < maxIdx)
                blockingRows.Add(unitIdx);
        }

        return blockingRows.Count;
    }

    /// <summary>
    /// Stage 3A 统一距离判定：阻挡排数 &lt; AttackRange + RangeModifier 才算在射程内。
    /// 敌方 AI（FindEnemyTarget）、V1 卡牌目标校验（RangeModifier）共用此源。
    /// </summary>
    public bool IsInRange(BattleUnit attacker, BattleUnit target, int rangeModifier = 0)
    {
        if (attacker == null || target == null || attacker == target) return false;
        return GetBlockingRowCount(attacker, target) < attacker.AttackRange + rangeModifier;
    }

    /// <summary>
    /// 执行一个完整回合（自动模式）
    /// </summary>
    public void ExecuteTurn()
    {
        ExecuteTurnPart1();
        ExecuteTurnPart2();
    }

    /// <summary>
    /// 上阵角色（玩家决策阶段调用）
    /// </summary>
    /// <param name="card">角色卡</param>
    /// <param name="row">目标排</param>
    /// <param name="col">目标列</param>
    /// <param name="character">角色运行时数据</param>
    /// <param name="characterData">角色模板配置</param>
    /// <param name="skills">初始技能列表</param>
    public bool DeployCharacter(CharacterCardData card, BattleRow row, BattleColumn col,
                                 CharacterInstance character, CharacterData characterData,
                                List<SkillData> skills)
    {
        // Current GameSpec R4.1：最多同时上阵 6 个我方单位（英雄占正常位置），任何路径统一强制
        if (_battleData.PlayerUnits.Count >= 6)
            return false;

        // Current GameSpec R5.0 / R5.1：同一 Board 内两个仍具战斗能力的单位不得占同一格（失败零写入）
        if (!_battleData.PlayerField.CanDeploy(row, col))
            return false;

        // Battle V0.1：上阵不再消耗 TP / 角色个人行动力（玩家资源只有共享行动力）；
        // 本方法只做上阵数量与 Board 占位校验。
        // 创建BattleUnit（从CharacterData读取战斗属性）
        // Stage 6A：Run 跨战 HP 继承（character.CurrentHP 来自 Run 状态；无 Run 时=MaxHP 满血）
        var unit = new BattleUnit(
            unitId: card.CardId,
            sourceId: character.CharacterId,
            unitType: UnitType.Character,
            maxHP: character.MaxHP,
            attack: character.CurrentAttack,
            attackCount: characterData.AttackCount,
            attackRange: characterData.AttackRange,
            maxActionPoints: characterData.ActionPoints,
            faction: characterData.Faction
        );
        unit.CurrentHP = character.CurrentHP > 0 ? character.CurrentHP : character.MaxHP;
        // 表现字段：人物显示名（来自具体 Person / Bootstrap；战斗层不再以战斗定义名回答「这个人是谁」）。
        unit.DisplayName = character.DisplayName;

        // 部署到我方 Board（R5.0：Player Board 独立 Occupancy；对方 Board 同坐标不影响）
        _battleData.PlayerField.Deploy(unit, row, col);

        _battleData.PlayerUnits.Add(unit);

        // 注册技能并关联拥有者
        _skillSystem.RegisterSkillsWithOwner(skills, unit.SourceId);

        return true;
    }

    /// <summary>
    /// 获取单位在虚拟战斗距离序列（R5.3.3）中的物理排索引（按实际前后顺序）
    /// 战场布局：玩家后排 - 玩家中排 - 玩家前排 - 敌方前排 - 敌方中排 - 敌方后排
    /// 玩家方：Back(2)→0, Middle(1)→1, Front(0)→2
    /// 敌方：  Front(0)→3, Middle(1)→4, Back(2)→5
    /// </summary>
    private int GetPhysicalRowIndex(BattleRow row, bool isPlayer)
    {
        return isPlayer ? (int)(BattleRow.Back) - (int)row : (int)row + 3;
    }

    /// <summary>
    /// 双 Board 下按「单位所属 Board」取物理排索引（R5.3.3 虚拟六排战斗距离投影）。
    /// 单位在哪一侧由所属 Board 决定；不属于任何 Board（未上阵替补）按敌方侧处理。
    /// </summary>
    private int GetPhysicalRowIndexFor(BattleUnit unit)
    {
        bool isPlayerBoard = _battleData != null
                             && _battleData.GetBoardOf(unit) == _battleData.PlayerField;
        return GetPhysicalRowIndex(unit.Row, isPlayerBoard);
    }

    /// <summary>单位所属 Board（转发到 BattleData 的唯一判定入口，不建立第二套空间真相源）</summary>
    private BattleField GetBoardOf(BattleUnit unit)
    {
        return _battleData != null ? _battleData.GetBoardOf(unit) : null;
    }

    /// <summary>
    /// Current GameSpec R15.4：失去战斗能力的单位退出所属 Board 的规则层占位（该格重新视为空位）。
    /// 只清规则占位；BattleUnit.Row / Column 保留最后逻辑位置供表现层使用（表现不构成 Occupancy）。
    /// </summary>
    private void ReleaseBoardCell(BattleUnit unit)
    {
        var board = GetBoardOf(unit);
        if (board != null) board.Remove(unit);
    }

    /// <summary>
    /// 注册技能到系统
    /// </summary>
    public void RegisterSkill(SkillData skill)
    {
        _skillSystem.RegisterSkill(skill);
    }

    /// <summary>
    /// 释放技能（委托给 SkillSystem）
    /// </summary>
    public bool CastSkill(BattleUnit caster, SkillData skill, BattleUnit target)
    {
        return _skillSystem.CastSkill(caster, skill, target);
    }

    /// <summary>
    /// 上阵英雄（玩家决策阶段调用）
    /// </summary>
    /// <param name="hero">英雄运行时数据</param>
    /// <param name="heroData">英雄模板配置</param>
    /// <param name="skills">初始技能列表</param>
    /// <param name="row">目标排</param>
    /// <param name="col">目标列</param>
    /// <returns>true=上阵成功</returns>
    public bool DeployHero(HeroInstance hero, HeroData heroData, List<SkillData> skills,
                            BattleRow row, BattleColumn col)
    {
        // Current GameSpec R4.1：最多同时上阵 6 个我方单位（英雄占正常位置），任何路径统一强制
        if (_battleData.PlayerUnits.Count >= 6)
            return false;

        // Current GameSpec R5.0 / R5.1：同一 Board 内两个仍具战斗能力的单位不得占同一格（失败零写入）
        if (!_battleData.PlayerField.CanDeploy(row, col))
            return false;

        var unit = new BattleUnit(
            unitId: hero.HeroId,
            sourceId: hero.HeroId,
            unitType: UnitType.Hero,
            maxHP: hero.MaxHP,
            attack: heroData.BaseAttack,
            attackCount: heroData.AttackCount,
            attackRange: heroData.AttackRange,
            maxActionPoints: heroData.ActionPoints,
            faction: heroData.Faction
        );
        unit.CurrentHP = hero.CurrentHP > 0 ? hero.CurrentHP : hero.MaxHP; // Stage 6A：Run 跨战 HP 继承

        _battleData.PlayerField.Deploy(unit, row, col);

        _battleData.PlayerUnits.Add(unit);
        _skillSystem.RegisterSkillsWithOwner(skills, unit.SourceId);
        return true;
    }

    /// <summary>
    /// 检查战斗结果
    /// 战斗结束后自动清理 EventBus 订阅和牌堆
    /// 胜负判定：统计真正存活的单位数（!IsDown && CurrentHP > 0）
    /// </summary>
    private void CheckBattleResult()
    {
        int alivePlayers = _battleData.PlayerUnits.Count(u => !u.IsDown && u.CurrentHP > 0);
        int aliveEnemies = _battleData.EnemyUnits.Count(u => !u.IsDown && u.CurrentHP > 0);

        bool allEnemiesDown = aliveEnemies == 0;
        bool allPlayersDown = alivePlayers == 0;

        if (allEnemiesDown)
        {
            _battleData.IsBattleOver = true;
            _battleData.CurrentPhase = BattlePhase.TurnEnd;
            EventBus.Publish(new BattleEndEvent(true));
        }
        else if (allPlayersDown)
        {
            _battleData.IsBattleOver = true;
            _battleData.CurrentPhase = BattlePhase.TurnEnd;
            EventBus.Publish(new BattleEndEvent(false));
        }

        // 战斗结束后清理，防止多场战斗残留累积
        if (_battleData.IsBattleOver)
        {
            EventBus.Clear();
            _battleData.PlayerHand.CharacterCards.Clear();
        }
    }

    /// <summary>
    /// 从怪物定义创建战斗单位（M2 Slice 2A：入参类型改为 StaticConfig MonsterDefinition）
    /// 逐字段映射与旧 Legacy Monster 静态定义完全一致（8 字段语义不变；不新增/删除字段）。
    ///
    /// WP-Encounter-1C §8（身份分离）：
    ///   - <paramref name="unitIdOverride"/> = 敌方**运行时实例身份**（EnemyInstanceId，产品路径必传），
    ///     用于 BattleUnit.UnitId / HP / Status / Intent / Target 等本场身份；
    ///   - <c>SourceId</c> 始终 = <c>monster.MonsterId</c> = **怪物模板身份**（定义 / 名称 / 数值 / 美术 / AI 规则）。
    ///   两者不得互相替代：同种怪多只必须 UnitId 唯一、SourceId 相同。
    /// 未传 override 时保持 Legacy 语义（UnitId = MonsterId，演示 / 兼容路径）。
    /// </summary>
    private BattleUnit CreateMonsterUnit(MonsterDefinition monster, string unitIdOverride = null)
    {
        string unitId = string.IsNullOrEmpty(unitIdOverride) ? monster.MonsterId : unitIdOverride;

        return new BattleUnit(
            unitId: unitId,
            sourceId: monster.MonsterId,
            unitType: UnitType.Monster,
            maxHP: monster.MaxHP,
            attack: monster.Attack,
            attackCount: monster.AttackCount,
            attackRange: monster.AttackRange,
            maxActionPoints: monster.ActionPoints,
            faction: monster.Faction
        );
    }

    /// <summary>
    /// 获取战斗数据（供UI读取）
    /// </summary>
    public BattleData GetBattleData()
    {
        return _battleData;
    }

    /// <summary>
    /// 注册状态配置到 StatusSystem
    /// 需要在战斗开始前调用，否则 DOT 效果不会生效
    /// </summary>
    public void RegisterStatusData(List<StatusData> statusDatas)
    {
        _statusSystem.RegisterStatusData(statusDatas);
    }

    /// <summary>
    /// 邻接判定统一入口（只转发到 SkillSystem.IsAdjacentAlly，不复制邻接规则）。
    /// Gate 0 规则层读取 Adjacency Fact 时使用，保证全工程仍只有一套动态邻接实现。
    /// </summary>
    public bool IsAdjacentAlly(BattleUnit a, BattleUnit b)
    {
        if (a == null || b == null) return false;
        return _skillSystem.IsAdjacentAlly(a, b);
    }

    /// <summary>
    /// Battle V0.1 卡牌最小伤害结算（Stage 2 最小接线）。
    /// 遵循 GameSpec R15.3：伤害后 HP ≤ 0 → 立即失去本场战斗能力（IsDown=true），
    /// 不进入 Legacy 死亡之门判定；随后检查胜负。
    /// </summary>
    /// <param name="target">受击单位</param>
    /// <param name="amount">伤害值</param>
    /// <returns>true=结算成功</returns>
    public bool ApplyBattleDamage(BattleUnit target, int amount)
    {
        if (target == null || target.IsDown || target.CurrentHP <= 0) return false;
        if (amount <= 0) return false;

        // Stage 5：防御中的敌人受到的下一次伤害按配置比例减免（一次性，受击后移除防御状态）
        if (target.UnitType == UnitType.Monster && IsEnemyDefending(target))
        {
            amount = Mathf.CeilToInt(amount * EnemyDefendDamageRatio);
            _defendingEnemyIds.Remove(target.UnitId);
        }

        // Battle V0.1 Gate 0：护盾先行吸收，不足部分再扣 HP（Shield 不足时归零）
        // 说明：护盾吸收不新增公共 EventBus 事件（避免扩大公共接口）；结果通过战场/单位状态可读。
        if (target.Shield > 0)
        {
            int absorbed = System.Math.Min(target.Shield, amount);
            target.Shield -= absorbed;
            amount -= absorbed;
        }

        if (amount > 0)
        {
            target.CurrentHP = System.Math.Max(0, target.CurrentHP - amount);
            EventBus.Publish(new DamageEvent(target.UnitId, amount));
        }

        if (target.CurrentHP <= 0)
        {
            // R15.3：0HP 角色失去本场战斗能力（无 Death's Door / 永久死亡）
            target.IsDown = true;
            target.InDeathDoor = false;
            target.Shield = 0;
            // R15.4：退出所属 Board 的有效 Occupancy（释放该逻辑格，新单位可进入）
            ReleaseBoardCell(target);
            EventBus.Publish(new DeathEvent(target.UnitId, target.UnitType));
        }

        CheckBattleResult();
        return true;
    }

    /// <summary>
    /// Battle V0.1 Gate 0 临时护盾结算：只写当前 Battle 的临时防御值（BattleUnit.Shield）。
    /// 失能 / 已无 HP 单位不接受护盾；护盾不进入 Run / Person / Save。
    /// </summary>
    /// <param name="target">获得护盾的单位</param>
    /// <param name="amount">护盾值（≤0 视为 Not Applicable）</param>
    /// <returns>true=已结算</returns>
    public bool ApplyBattleShield(BattleUnit target, int amount)
    {
        if (target == null || target.IsDown || target.CurrentHP <= 0) return false;
        if (amount <= 0) return false;

        target.Shield += amount;
        return true;
    }

    /// <summary>
    /// 清除全场剩余临时护盾（玩家回合开始时调用；护盾不跨玩家回合残留）。
    /// </summary>
    public void ClearAllShields()
    {
        if (_battleData == null) return;

        foreach (var unit in _battleData.PlayerUnits)
        {
            if (unit != null) unit.Shield = 0;
        }
        foreach (var unit in _battleData.EnemyUnits)
        {
            if (unit != null) unit.Shield = 0;
        }
    }

    /// <summary>
    /// Battle V0.1 卡牌最小治疗结算（Stage 2 最小接线）。
    /// 恢复生命，不超过最大生命；不会复活已失去战斗能力的单位。
    /// </summary>
    /// <param name="target">治疗单位</param>
    /// <param name="amount">治疗量</param>
    /// <returns>true=结算成功</returns>
    public bool ApplyBattleHeal(BattleUnit target, int amount)
    {
        if (target == null || target.IsDown || target.CurrentHP <= 0) return false;
        if (amount <= 0) return false;

        target.CurrentHP = System.Math.Min(target.MaxHP, target.CurrentHP + amount);
        EventBus.Publish(new HealEvent(target.UnitId, amount));
        return true;
    }
}

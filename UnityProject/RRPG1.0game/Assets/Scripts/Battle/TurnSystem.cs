/// <summary>
/// 回合系统
///
/// 负责：管理战斗回合流程，按固定顺序推进阶段
/// 不负责：技能逻辑、状态计算、UI显示
///
/// 每回合固定执行顺序：
///   TurnStart（回合推进 → TurnStart技能触发）
///   → Draw（抽牌：共享牌库接管）
///   → Decision（玩家共享手牌行动）
///   → EnemyAction（怪物攻击）
///   → TurnEnd（状态递减/DOT/敌方未使用行动力不保留）
/// Battle V0.1：TP 公共资源与角色个人行动力已退出玩家资源模型，本类不再恢复玩家个人 AP。
/// </summary>
public class TurnSystem
{
    private BattleData _battleData;

    /// <summary>
    /// 创建回合系统
    /// </summary>
    /// <param name="battleData">战斗数据</param>
    /// <param name="cardSystem">（Legacy 兼容参数）Battle V0.1 起抽牌由共享牌库承担，本类不再使用</param>
    public TurnSystem(BattleData battleData, CardSystem cardSystem = null)
    {
        _battleData = battleData;
    }

    /// <summary>
    /// 执行回合开始阶段
    ///
    /// 固定顺序：
    ///   1. 回合数推进 + 发布回合开始事件（Battle V0.1：TP 与角色个人行动力已退出玩家资源模型）
    /// </summary>
    public void StartTurn()
    {
        _battleData.Turn++;
        _battleData.CurrentPhase = BattlePhase.TurnStart;

        // 发布回合开始事件
        EventBus.Publish(new TurnStartEvent(_battleData.Turn));
    }

    /// <summary>
    /// 执行回合结束阶段
    ///
    /// 状态持续时间减少 → TurnEnd技能触发 → DOT结算
    /// 未使用行动力不保留（仅敌方当前行动逻辑仍读取其自身行动力）
    /// </summary>
    public void EndTurn()
    {
        _battleData.CurrentPhase = BattlePhase.TurnEnd;

        // 状态持续时间减少 — 由 StatusSystem 处理

        // TurnEnd技能触发 — 由 SkillSystem 处理

        // DOT结算 — 由 StatusSystem 处理

        foreach (var unit in _battleData.EnemyUnits)
        {
            unit.CurrentActionPoints = 0;
        }

        // 发布回合结束事件
        EventBus.Publish(new TurnEndEvent(_battleData.Turn));
    }

    /// <summary>
    /// 进入抽牌阶段（Battle V0.1：抽牌由共享牌库 BattleV1Runtime 承担，本阶段只推进阶段标记）
    /// </summary>
    public void EnterDrawPhase()
    {
        _battleData.CurrentPhase = BattlePhase.Draw;
    }

    /// <summary>
    /// 进入决策阶段
    /// </summary>
    public void EnterDecisionPhase()
    {
        _battleData.CurrentPhase = BattlePhase.Decision;
        // 玩家操作由 UI 层处理
    }

    /// <summary>
    /// 进入敌方攻击阶段
    /// </summary>
    public void EnterEnemyPhase()
    {
        _battleData.CurrentPhase = BattlePhase.EnemyAction;
        // 怪物AI逻辑由 MonsterAI 处理
    }

    /// <summary>
    /// Fisher-Yates 洗牌
    /// </summary>
    private void Shuffle<T>(System.Collections.Generic.List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
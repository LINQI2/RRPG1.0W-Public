using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 状态系统
///
/// 负责：管理战斗中状态的添加与移除
/// 不负责：技能释放、战斗流程控制、UI显示
///
/// StatusData（配置层）：定义状态的静态属性
/// StatusRuntime（运行时层）：追踪战斗中的动态数据
/// Duration 按作用方回合计算
///
/// DOT 伤害流程：
///   OnTurnEnd → 查找 SubHP 效果 → 发布 DamageEvent → 减少回合 → 移除过期状态
/// </summary>
public class StatusSystem
{
    private BattleData _battleData;
    private Dictionary<string, StatusData> _statusRegistry;

    /// <summary>
    /// 创建状态系统
    /// </summary>
    /// <param name="battleData">战斗数据</param>
    public StatusSystem(BattleData battleData)
    {
        _battleData = battleData;
        _statusRegistry = new Dictionary<string, StatusData>();
    }

    /// <summary>
    /// 注册状态配置到系统
    /// 供查找 DOT 效果使用
    /// </summary>
    public void RegisterStatusData(StatusData statusData)
    {
        if (statusData != null && !_statusRegistry.ContainsKey(statusData.StatusId))
        {
            _statusRegistry[statusData.StatusId] = statusData;
        }
    }

    /// <summary>
    /// 批量注册状态配置
    /// </summary>
    public void RegisterStatusData(IEnumerable<StatusData> statusDatas)
    {
        foreach (var sd in statusDatas)
        {
            RegisterStatusData(sd);
        }
    }

    /// <summary>
    /// 应用状态效果到目标单位（加攻/减攻等即时属性修改）
    /// </summary>
    private void ApplyStatusEffects(BattleUnit target, StatusData statusData, bool apply)
    {
        if (statusData.Effects == null) return;
        int sign = apply ? 1 : -1;
        foreach (var effect in statusData.Effects)
        {
            switch (effect.EffectType)
            {
                case EffectType.AddAttack:
                    target.Attack += effect.Value * sign;
                    if (target.Attack < 0) target.Attack = 0;
                    break;
                case EffectType.SubAttack:
                    target.Attack -= effect.Value * sign;
                    if (target.Attack < 0) target.Attack = 0;
                    break;
                // DOT 效果由 OnTurnEnd 处理，不在此处应用
            }
        }
    }

    /// <summary>
    /// 添加状态到目标单位
    /// 如果已有同类状态，增加层数；否则新建
    /// 添加时立即应用非 DOT 效果（如加攻/减攻）
    /// </summary>
    /// <param name="target">目标单位</param>
    /// <param name="statusData">状态配置</param>
    /// <param name="sourceUnitId">来源单位ID</param>
    public void AddStatus(BattleUnit target, StatusData statusData, string sourceUnitId)
    {
        // 检查是否已有同类状态
        var existing = target.StatusList.FirstOrDefault(s => s.StatusId == statusData.StatusId);

        if (existing != null)
        {
            // 已有：增加层数
            existing.StackCount++;
        }
        else
        {
            // 新增：应用状态效果（加攻/减攻等）
            ApplyStatusEffects(target, statusData, true);
            var runtime = new StatusRuntime(statusData.StatusId, statusData.Duration, sourceUnitId);
            target.StatusList.Add(runtime);
        }

        UnityEngine.Debug.Log($"[Status] 添加状态 [{statusData.StatusId}] 到 {target.UnitId}，持续 {statusData.Duration} 回合，当前层数={target.StatusList.Count}");
        EventBus.Publish(new StatusAddEvent(target.UnitId, statusData.StatusId));
    }

    /// <summary>
    /// 从目标单位移除指定状态
    /// 移除时还原状态效果
    /// </summary>
    public void RemoveStatus(BattleUnit target, string statusId)
    {
        var status = target.StatusList.FirstOrDefault(s => s.StatusId == statusId);
        if (status != null)
        {
            // 还原状态效果
            if (_statusRegistry.TryGetValue(statusId, out var statusData))
            {
                ApplyStatusEffects(target, statusData, false);
            }
            target.StatusList.Remove(status);
            UnityEngine.Debug.Log($"[Status] 移除状态 [{statusId}] 从 {target.UnitId}");
            EventBus.Publish(new StatusRemoveEvent(target.UnitId, statusId));
        }
    }

    /// <summary>
    /// 检查目标是否拥有指定状态
    /// </summary>
    public bool HasStatus(BattleUnit target, string statusId)
    {
        return target.StatusList.Any(s => s.StatusId == statusId);
    }

    /// <summary>
    /// 回合结束时处理所有状态
    /// 执行DOT效果 → 减少剩余回合 → 移除过期状态
    /// </summary>
    public void OnTurnEnd()
    {
        var allUnits = new List<BattleUnit>();
        // 双 Board：状态结算覆盖两条 Board 上的逻辑单位（不再读取单一共享网格）
        allUnits.AddRange(_battleData.PlayerField.GetAllUnits());
        allUnits.AddRange(_battleData.EnemyField.GetAllUnits());

        foreach (var unit in allUnits)
        {
            // 1. 执行 DOT 伤害（先扣血再减少回合）
            foreach (var status in unit.StatusList)
            {
                if (_statusRegistry.TryGetValue(status.StatusId, out var statusData) &&
                    statusData.Effects != null)
                {
                    foreach (var effect in statusData.Effects)
                    {
                        if (effect.EffectType == EffectType.SubHP && effect.Value != 0)
                        {
                            // 根据叠加层数计算 DOT 伤害
                            int dotDamage = effect.Value * status.StackCount;
                            unit.CurrentHP -= dotDamage;
                            EventBus.Publish(new DamageEvent(unit.UnitId, dotDamage));
                        }
                    }
                }
            }

            // 2. 减少剩余回合
            var expiredStatuses = new List<StatusRuntime>();
            foreach (var status in unit.StatusList)
            {
                status.RemainingTurns--;
                if (status.RemainingTurns <= 0)
                {
                    expiredStatuses.Add(status);
                }
            }

            // 3. 移除过期状态（通过 RemoveStatus 以触发效果还原）
            foreach (var expired in expiredStatuses)
            {
                string expiredId = expired.StatusId;
                string sourceId = expired.SourceUnitId;
                RemoveStatus(unit, expiredId);

                // 链式触发：如果状态配置了到期转移，为目标添加新状态
                if (_statusRegistry.TryGetValue(expiredId, out var expiredData) &&
                    !string.IsNullOrEmpty(expiredData.OnExpireStatusId))
                {
                    var nextStatusData = GetStatusData(expiredData.OnExpireStatusId);
                    if (nextStatusData != null)
                    {
                        AddStatus(unit, nextStatusData, sourceId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 获取目标单位指定状态的叠加层数
    /// </summary>
    public int GetStackCount(BattleUnit target, string statusId)
    {
        var status = target.StatusList.FirstOrDefault(s => s.StatusId == statusId);
        return status?.StackCount ?? 0;
    }

    /// <summary>
    /// 根据状态ID获取已注册的状态配置
    /// </summary>
    public StatusData GetStatusData(string statusId)
    {
        _statusRegistry.TryGetValue(statusId, out var data);
        return data;
    }
}
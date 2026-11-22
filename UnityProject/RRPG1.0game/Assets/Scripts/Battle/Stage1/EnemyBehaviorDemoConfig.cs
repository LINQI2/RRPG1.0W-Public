using System.Collections.Generic;

/// <summary>
/// Battle V0.1 Stage 5：敌人行为演示配置（内容层，最小且可替换）
///
/// 与 BattleV1DemoCardPool 同一定位：正式敌人 AI 内容系统落地前，
/// 用最少的演示/测试敌人证明统一决策框架可表达：
///   普通攻击行为 / 条件优先行为 / 防御或特殊行为。
/// 未配置的怪物回退为普通攻击（Attack + Frontmost），保持旧行为。
/// 本配置仅为内容层测试值（可重复执行、可审计），不冻结未来怪物 AI。
/// </summary>
public static class EnemyBehaviorDemoConfig
{
    /// <summary>MonsterId → 行为规则列表</summary>
    private static readonly Dictionary<string, List<EnemyActionRule>> MonsterRules =
        new Dictionary<string, List<EnemyActionRule>>
        {
            // 普通敌人（史莱姆）：永远攻击合法目标（Frontmost：物理前排优先，同排低血优先）
            { "mon_001", new List<EnemyActionRule>
                {
                    new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100, EnemyTargetRule.Frontmost)
                }
            },

            // 远程普通（骷髅弓手）：AttackRange=2，仍按 Frontmost 选目标（复用统一距离判定）
            { "mon_002", new List<EnemyActionRule>
                {
                    new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100, EnemyTargetRule.Frontmost)
                }
            },

            // 刺客型（野狼，条件优先 + 特殊行动）：
            //   存在低生命我方目标时，优先特殊攻击生命最低目标（2×伤害，Priority 1）；
            //   否则回退普通攻击（Priority 0）。
            { "mon_003_wolf", new List<EnemyActionRule>
                {
                    new EnemyActionRule(EnemyActionType.Special, EnemyConditionId.AnyTargetLowHp, 1, 100,
                                        EnemyTargetRule.LowestHp, specialDamageMultiplier: 2),
                    new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100, EnemyTargetRule.Frontmost)
                }
            },

            // 防御型演示（测试用：mon_demo_defender）：
            //   自身低血时优先防御（SelfLowHp + Priority 1），否则普通攻击（Priority 0）。
            //   供 EditMode 测试证明「防御」可由同一决策框架表达；正式怪物内容系统落地后替换。
            { "mon_demo_defender", new List<EnemyActionRule>
                {
                    new EnemyActionRule(EnemyActionType.Defend, EnemyConditionId.SelfLowHp, 1, 100),
                    new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100, EnemyTargetRule.Frontmost)
                }
            }
        };

    /// <summary>
    /// 读取指定怪物（MonsterId）的行为规则；未配置时回退为普通攻击（保持旧行为）。
    /// </summary>
    public static List<EnemyActionRule> GetRules(string monsterId)
    {
        if (monsterId != null && MonsterRules.TryGetValue(monsterId, out var rules) && rules != null && rules.Count > 0)
            return rules;

        return new List<EnemyActionRule>
        {
            new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100, EnemyTargetRule.Frontmost)
        };
    }

    /// <summary>该怪物是否有自定义行为配置（供诊断/测试判断回退是否发生）</summary>
    public static bool HasCustomRules(string monsterId)
    {
        return monsterId != null && MonsterRules.ContainsKey(monsterId);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗结算系统（Stage 6A：结算结果写入 RunState Pending，不再直接写长期 PlayerCollection）
///
/// 负责：战斗结束后计算经验、金币，并把结果写入 Run 待结算收益（PendingRewards）
/// 不负责：战斗流程、UI显示、长期存档写入
///
/// M5 Run/Return Growth Semantic Migration（Current Rule：GameSpec/Person §10）：
///   - Battle ProfessionExp（PendingProfessionExp 通道）：只给“实际参战且当前有 Active Profession”的
///     普通 Character；随队但未上阵 = 0；胜负获得的 Battle ProfessionExp 相同；
///     不按输出/击杀/MVP 分配；数值为测试值未冻结（BattleProfessionExp）；
///     普通 Character 无 Active Profession 不产生 ProfessionExp、不 fallback 到 Legacy Exp；
///     第一版不额外给予 Expedition Completion ProfessionExp。
///   - Hero（Legacy 通道）保持 M4 后 Legacy Run Growth 不变：胜利才获得 Legacy 成长经验（无论是否上阵）。
///   - 长期兑现：由 AdventureManager.EndRun → CommitPendingProfessionExp / CommitPendingRewards 完成；
///     每战结算不再直接 SaveSystem.Save / 修改 PlayerCollection。
///
/// 经验规则（Legacy 通道展示/测试旧值，不冻结为新正式数值）：
///   英雄：胜利后获得经验（无论是否上阵）
///   角色：M5 起不再通过 Legacy 通道获得成长经验
/// 金币规则：旧固定奖励（BaseGold/LoseGold），仅写入 Run PendingGold；
///   Legacy 常量不得自动升级为新的正式奖励数值（问题 50 未决）。
/// </summary>
public class BattleSettlement
{
    /// <summary>胜利基础经验（Legacy 展示/测试旧值，未冻结）</summary>
    public const int BaseExp = 10;

    /// <summary>
    /// M5 Battle ProfessionExp 测试值（未冻结）：每位“实际参战 + 有 Active Profession”的人物每场 Battle
    /// 获得该量 ProfessionExp（胜负相同）。仅为 Test 数值，正式数值待数值设计冻结。
    /// </summary>
    public const int BattleProfessionExp = 10;

    /// <summary>胜利基础金币</summary>
    public const int BaseGold = 20;

    /// <summary>失败金币（安慰奖）</summary>
    public const int LoseGold = 5;

    /// <summary>战斗是否胜利</summary>
    public bool IsVictory { get; private set; }

    /// <summary>获得的经验</summary>
    public int GainedExp { get; private set; }

    /// <summary>获得的金币</summary>
    public int GainedGold { get; private set; }

    /// <summary>角色伤亡详情（每人的结果）</summary>
    public List<CharacterResult> CharacterResults { get; private set; }

    /// <summary>
    /// 角色结算结果
    /// </summary>
    public class CharacterResult
    {
        /// <summary>
        /// 模板 CharacterId（Legacy 展示字段：仅表示该人物来自哪个角色模板，不代表具体人物身份）。
        /// 产品逻辑的唯一人物归属请使用 PersonId。
        /// </summary>
        public string CharacterId;

        /// <summary>
        /// 本场参战具体人物实例的 PersonId（M1；产品逻辑归属唯一键）。
        /// </summary>
        public string PersonId;

        /// <summary>显示名称</summary>
        public string DisplayName;
        /// <summary>是否上阵</summary>
        public bool WasDeployed;
        /// <summary>是否死亡</summary>
        public bool IsDead;
        /// <summary>伤残状态（仅存活角色有）</summary>
        public InjuryStatus Injury;
        /// <summary>获得的经验</summary>
        public int ExpGained;
    }

    /// <summary>
    /// M5：判断一个具体人物当前是否是“Profession Growth 可执行”的普通 Character
    /// （有 Active Profession）。经 GrowthAuthority 窄解析，不修改 IPersonGrowth 全局语义。
    /// Hero / 无 Profession 普通 Character 返回 false。
    /// </summary>
    private static bool IsProfessionPerson(IPersonGrowth person)
    {
        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        return auth != null && auth.IsAvailable && auth.IsProfessionGrowth;
    }

    /// <summary>
    /// 结算的 Profession 事实来源（产品路径由 BattleUI 注入：PersonId → 真实长期 Person）。
    ///
    /// 为什么需要：`BattleUI.InitFromExpedition` 为本场战斗创建的是 **Battle 临时 CharacterInstance**
    /// （只从 Card→Person 绑定复制 PersonId，不携带长期 `ActiveProfessionProgress`）。因此结算不能
    /// 用传入实例判定「该具体人物当前是否有 Active Profession」——那是长期 Person Authority 的事实。
    ///
    /// 语义边界：
    ///   - 只用于**读取**真实长期 Person 的 Profession 事实，不复制 / 不写回任何 Authority；
    ///   - 未注入（Demo / 纯逻辑测试）时保持按传入实例自身判定（既有语义不变）；
    ///   - 解析失败（找不到 Person）不 fallback 到 Legacy / 模板 ID，按无 Profession 处理。
    /// </summary>
    public System.Func<string, IPersonGrowth> LongTermPersonLookup;

    /// <summary>
    /// 结算专用 Profession 判定：已注入 lookup 时只接受长期 Person Authority；
    /// 只有未注入时才按传入实例自身判定（Demo / 测试兼容）。
    /// </summary>
    private bool IsProfessionPersonForSettlement(CharacterInstance character, string personId)
    {
        if (LongTermPersonLookup == null) return IsProfessionPerson(character);
        if (string.IsNullOrEmpty(personId)) return false;

        IPersonGrowth longTerm;
        try
        {
            longTerm = LongTermPersonLookup(personId);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[BattleSettlement] 长期人物查询异常（PersonId=" + personId
                + "，" + exception.GetType().Name
                + "）：不借用输入实例的 Profession 资格，不写本人物 PendingProfessionExp。");
            return false;
        }
        if (longTerm == null)
        {
            Debug.LogWarning("[BattleSettlement] 长期人物查询未找到 PersonId=" + personId
                + "：不借用输入实例的 Profession 资格，不写本人物 PendingProfessionExp。");
            return false;
        }
        return IsProfessionPerson(longTerm);
    }

    /// <summary>
    /// 处理战斗结算（Stage 6A：只写 RunState Pending，不写长期 PlayerCollection / 不保存存档）
    /// M1：参战人物归属一律按 PersonId（allCharacters 每人的 PersonId / deployedPersonIds）。
    /// CharacterResult.CharacterId 仅为 Legacy 展示字段。
    ///
    /// M5（ProfessionExp 通道）：胜利/失败都会为「实际参战 + 有 Active Profession」的角色写入
    /// PendingProfessionExp（量相同，测试值）；随队未上阵 = 0；无 Profession 角色不产生；
    /// Hero 走 Legacy 通道，行为不变（胜利才获得）。
    /// </summary>
    /// <param name="isVictory">是否胜利</param>
    /// <param name="allCharacters">所有参战角色实例</param>
    /// <param name="deployedPersonIds">本场上阵人物的 PersonId 列表（不再承载模板 CharacterId 语义）</param>
    /// <param name="hero">英雄实例（可为null）</param>
    /// <param name="runState">Run 运行时状态（可为null：仅计算展示，不写 Pending）</param>
    public void ProcessSettlement(bool isVictory, List<CharacterInstance> allCharacters,
                                   List<string> deployedPersonIds, HeroInstance hero,
                                   RunState runState)
    {
        IsVictory = isVictory;
        CharacterResults = new List<CharacterResult>();

        if (isVictory)
        {
            GainedExp = BaseExp;
            GainedGold = BaseGold;

            // 待结算长期收益：金币 + 每人物成长经验（Return 时由 EndRun 兑现）
            if (runState != null)
            {
                runState.AddPendingGold(GainedGold);

                // 英雄获得经验（Legacy 通道；M5 Hero Legacy Run Growth 不变。M1：按英雄实例 PersonId 归属）
                // Foundation Identity Cleanup：Hero 缺 PersonId 属非法身份，不写 Pending（不用 HeroId 当 Person）。
                if (hero != null)
                {
                    if (string.IsNullOrEmpty(hero.PersonId))
                    {
                        Debug.LogWarning("[BattleSettlement] 英雄（HeroId=" + hero.HeroId
                            + "）缺少真实 PersonId，不写 Run PendingGrowthExp（产品路径禁止 HeroId fallback）。");
                    }
                    else
                    {
                        runState.AddPendingGrowthExp(hero.PersonId, BaseExp);
                    }
                }
            }

            // 角色结算
            foreach (var character in allCharacters)
            {
                // Foundation Identity Cleanup：产品路径只按真实 PersonId 归属；空 PersonId 属非法身份，
                // 不把模板 CharacterId 当 PersonId 使用（无法匹配部署/写 Pending，保持 unresolved）。
                string personId = character.PersonId;
                if (runState != null && string.IsNullOrEmpty(personId))
                {
                    Debug.LogWarning("[BattleSettlement] 角色（CharacterId=" + character.CharacterId
                        + "）缺少真实 PersonId，Run 结算不写 Pending（产品路径禁止 CharacterId fallback）。");
                }
                bool wasDeployed = deployedPersonIds != null && deployedPersonIds.Contains(personId);
                bool isDead = character.IsDead;
                bool profession = IsProfessionPersonForSettlement(character, personId);

                int expGained = 0;
                if (profession && wasDeployed && !string.IsNullOrEmpty(personId))
                {
                    // M5：实际参战 → Battle ProfessionExp（写入 Run Pending，不写长期实例）。
                    // 上阵即参战（Battle V0.1 无常规战斗永久死亡）；胜负同量；随队未上阵 = 0。
                    // Foundation Identity Cleanup：真实 PersonId 缺失时不写 Pending、不计数。
                    runState?.AddPendingProfessionExp(personId, BattleProfessionExp);
                    expGained = BattleProfessionExp;
                }

                var result = new CharacterResult
                {
                    CharacterId = character.CharacterId,
                    PersonId = personId,
                    DisplayName = character.DisplayName,
                    WasDeployed = wasDeployed,
                    IsDead = isDead,
                    Injury = character.Injury,
                    ExpGained = expGained
                };
                CharacterResults.Add(result);

                // Battle V0.1：无常规战斗永久死亡，不再从长期卡册移除死亡角色
                // （Legacy 移除分支已由 Battle V0.1 规则替代，见 GameSpec/Battle）
            }
        }
        else
        {
            // 失败
            GainedExp = 0;
            GainedGold = LoseGold;

            // 失败安慰金币进入 Run Pending（不直接写长期；全灭损失比例未冻结，由 EndRun 退出策略决定）
            runState?.AddPendingGold(LoseGold);

            // 角色结算（M5：失败时实际参战的 Profession 角色同样获得 Battle ProfessionExp；Hero 不获得 Legacy）
            foreach (var character in allCharacters)
            {
                // Foundation Identity Cleanup：产品路径只按真实 PersonId 归属；空 PersonId 属非法身份，
                // 不把模板 CharacterId 当 PersonId 使用（无法匹配部署/写 Pending，保持 unresolved）。
                string personId = character.PersonId;
                if (runState != null && string.IsNullOrEmpty(personId))
                {
                    Debug.LogWarning("[BattleSettlement] 角色（CharacterId=" + character.CharacterId
                        + "）缺少真实 PersonId，Run 结算不写 Pending（产品路径禁止 CharacterId fallback）。");
                }
                bool wasDeployed = deployedPersonIds != null && deployedPersonIds.Contains(personId);
                bool profession = IsProfessionPersonForSettlement(character, personId);

                int expGained = 0;
                if (profession && wasDeployed && !string.IsNullOrEmpty(personId))
                {
                    // Foundation Identity Cleanup：真实 PersonId 缺失时不写 Pending、不计数。
                    runState?.AddPendingProfessionExp(personId, BattleProfessionExp);
                    expGained = BattleProfessionExp;
                }

                var result = new CharacterResult
                {
                    CharacterId = character.CharacterId,
                    PersonId = personId,
                    DisplayName = character.DisplayName,
                    WasDeployed = wasDeployed,
                    IsDead = character.IsDead,
                    Injury = character.Injury,
                    ExpGained = expGained
                };
                CharacterResults.Add(result);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 一次卡牌奖励候选（角色牌保留具体人物归属；通用牌 PersonId 为空）。
/// Weight 为测试权重，稀有度/正式 RewardWeight 概率尚未冻结。
/// </summary>
public class RunRewardCandidate
{
    /// <summary>具体人物归属（PersonId）；空/null = 通用牌</summary>
    public string PersonId;

    /// <summary>静态牌模板</summary>
    public BattleCardData Template;

    /// <summary>测试权重（未冻结；当前测试内容一律用简单权重）</summary>
    public int Weight = 1;

    public bool IsCommon => string.IsNullOrEmpty(PersonId);

    /// <summary>候选实例唯一标识（同 PersonId + 同模板视为完全相同候选实例，同一 offer 内去重）</summary>
    public string IdentityKey =>
        (string.IsNullOrEmpty(PersonId) ? "__common__" : PersonId) + "|" + (Template != null ? Template.CardId : "");
}

/// <summary>
/// 奖励候选来源 Provider（可替换，不要把 DemoCardPool 永久写死进奖励算法）。
/// 正式长期 Mastered Pool 落地后由新 Provider 替换本实现。
/// </summary>
public interface IRunRewardCandidateProvider
{
    /// <summary>构建当前远征全部候选（所有人物一起进入池，不先随机人物）+ 通用牌</summary>
    IReadOnlyList<RunRewardCandidate> BuildCandidates(RunState runState, IEnumerable<BattleCardData> allTemplates);
}

/// <summary>
/// 临时内容种子 Provider（Stage 6B）。
/// 复用 BattleV1DemoCardPool 作为测试内容来源：为远征每个人物提供其映射牌 + 全部通用牌；
/// 仅是 Reward Provider 的临时内容种子，不是正式长期人物学习系统。
/// 所有权重为简单测试权重（DefaultTestWeight），未冻结。
/// </summary>
public class DemoCardPoolRewardProvider : IRunRewardCandidateProvider
{
    public const int DefaultTestWeight = 1;

    public virtual IReadOnlyList<RunRewardCandidate> BuildCandidates(RunState runState, IEnumerable<BattleCardData> allTemplates)
    {
        var result = new List<RunRewardCandidate>();
        if (runState == null) return result;

        var templates = allTemplates != null ? allTemplates.ToList() : new List<BattleCardData>();

        // 当前远征所有人物一起进入有效奖励池（不先随机一个人物）
        foreach (var person in runState.Persons.Values)
        {
            if (person == null || string.IsNullOrEmpty(person.PersonId)) continue;

            var ids = BattleV1DemoCardPool.GetCardsForPerson(person.PersonId);
            foreach (var id in ids)
            {
                var t = templates.Find(x => x != null && x.CardId == id && x.CardKind == BattleCardKind.Character);
                if (t != null)
                {
                    // 角色牌保留具体人物归属；不同人物同模板是不同候选
                    result.Add(new RunRewardCandidate { PersonId = person.PersonId, Template = t, Weight = DefaultTestWeight });
                }
            }
        }

        // 通用牌：不绑定人物，始终可奖励
        foreach (var t in templates)
        {
            if (t != null && t.CardKind == BattleCardKind.Common)
                result.Add(new RunRewardCandidate { PersonId = null, Template = t, Weight = DefaultTestWeight });
        }

        return result;
    }
}

/// <summary>
/// 奖励候选抽取（纯逻辑）：
///   - 从“全人物 + 通用”的扁平池中直接加权抽取（不先随机人物）；
///   - 同一 offer 内去掉完全相同的候选实例（同 PersonId + 同模板）；
///   - 加权不放回抽取；无正式 RewardWeight 时使用简单测试权重。
/// </summary>
public static class RunRewardPicker
{
    public static List<RunRewardCandidate> PickDistinct(IReadOnlyList<RunRewardCandidate> pool, int count, System.Random rng = null)
    {
        var result = new List<RunRewardCandidate>();
        if (pool == null || count <= 0) return result;

        var remaining = new List<RunRewardCandidate>();
        var seen = new HashSet<string>();
        foreach (var c in pool)
        {
            if (c == null || c.Template == null) continue;
            if (seen.Add(c.IdentityKey)) remaining.Add(c);
        }

        var localRng = rng ?? new System.Random();
        while (result.Count < count && remaining.Count > 0)
        {
            int total = 0;
            foreach (var c in remaining) total += System.Math.Max(1, c.Weight);

            int roll = localRng.Next(total);
            int acc = 0;
            int pickIdx = remaining.Count - 1;
            for (int i = 0; i < remaining.Count; i++)
            {
                acc += System.Math.Max(1, remaining[i].Weight);
                if (roll < acc) { pickIdx = i; break; }
            }

            result.Add(remaining[pickIdx]);
            remaining.RemoveAt(pickIdx);
        }
        return result;
    }
}

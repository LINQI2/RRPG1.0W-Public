using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 正式 Run 奖励候选 Provider：从人物长期 Unlocked Pool 读取候选
/// （当前远征所有人物一起进入扁平池，不先随机人物 + 通用牌）。
///
/// M5 Run Card Reward Pool 显式迁移（GameSpec/Person §7 / M5 边界）：
///   - Profession Character（普通 Character 且当前有 Active Profession）：
///     候选只读该具体 Person 当前 Profession Unlocked Pool（ProfessionProgress.UnlockedPoolCardIds），
///     不读 Legacy UnlockedPool、不 fallback Demo 种子；
///   - Hero 与无 Profession 普通 Character：继续读取自身长期 Legacy Unlocked Pool（IPersonGrowth
///     UnlockedPoolCardIds，M4 前语义；Hero Legacy Pool / Character-owned Legacy 池）；
///   - personLookup 找不到长期人物（异常路径）才回退 DemoCardPoolRewardProvider（临时内容种子）。
/// 实现经 GrowthAuthority 窄解析，不通过修改 IPersonGrowth 全局语义达成。
///
/// 同一个 Unlocked Pool 同时供：人物长期 CardGrowth 三选一（CardGrowthResolver.BuildOffer）
/// 与 Run 内卡牌奖励三选一（本类）。人物长期状态通过 personLookup 注入（纯逻辑可测）。
/// DemoCardPool 不作为正式长期牌池结构。
/// </summary>
public class UnlockedPoolRewardProvider : IRunRewardCandidateProvider
{
    private readonly Func<string, IPersonGrowth> _personLookup;
    private readonly DemoCardPoolRewardProvider _fallback = new DemoCardPoolRewardProvider();

    public UnlockedPoolRewardProvider(Func<string, IPersonGrowth> personLookup)
    {
        _personLookup = personLookup;
    }

    public virtual IReadOnlyList<RunRewardCandidate> BuildCandidates(
        RunState runState, IEnumerable<BattleCardData> allTemplates)
    {
        var empty = new List<RunRewardCandidate>();
        if (runState == null) return empty;
        if (_personLookup == null) return _fallback.BuildCandidates(runState, allTemplates);

        var result = new List<RunRewardCandidate>();
        var templates = allTemplates != null ? allTemplates.ToList() : new List<BattleCardData>();

        foreach (var person in runState.Persons.Values)
        {
            if (person == null || string.IsNullOrEmpty(person.PersonId)) continue;

            IPersonGrowth growth = null;
            try { growth = _personLookup(person.PersonId); }
            catch { growth = null; }

            if (growth == null)
            {
                // 无长期状态（异常兜底）：临时内容种子
                foreach (var id in BattleV1DemoCardPool.GetCardsForPerson(person.PersonId))
                {
                    var t = templates.Find(x => x != null && x.CardId == id && x.CardKind == BattleCardKind.Character);
                    if (t != null)
                        result.Add(new RunRewardCandidate
                        {
                            PersonId = person.PersonId,
                            Template = t,
                            Weight = DemoCardPoolRewardProvider.DefaultTestWeight
                        });
                }
                continue;
            }

            // M5：经 GrowthAuthority 窄解析选择该 Person 的 Run 奖励牌池。
            IGrowthAuthority auth = GrowthAuthority.Resolve(growth);
            bool profession = auth != null && auth.IsAvailable && auth.IsProfessionGrowth;
            List<string> pool = profession
                ? auth.UnlockedPoolCardIds      // Profession Character → 当前 Profession Unlocked Pool
                : growth.UnlockedPoolCardIds;   // Hero / 无 Profession Character → Legacy 池

            if (pool == null || pool.Count == 0)
            {
                // Profession Character：池为空 = 当前没有可奖励内容 → 本人物无角色候选，不 Demo 兜底
                // （Demo 兜底只属于“找不到长期人物”的异常路径；空 Profession 池不允许被临时内容替代）。
                // Hero / 无 Profession Character：空 Legacy 池时保持既有回退语义（临时内容种子）。
                if (!profession)
                {
                    foreach (var id in BattleV1DemoCardPool.GetCardsForPerson(person.PersonId))
                    {
                        var t = templates.Find(x => x != null && x.CardId == id && x.CardKind == BattleCardKind.Character);
                        if (t != null)
                            result.Add(new RunRewardCandidate
                            {
                                PersonId = person.PersonId,
                                Template = t,
                                Weight = DemoCardPoolRewardProvider.DefaultTestWeight
                            });
                    }
                }
                continue;
            }

            foreach (var id in pool)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var t = templates.Find(x => x != null && x.CardId == id && x.CardKind == BattleCardKind.Character);
                if (t != null)
                    result.Add(new RunRewardCandidate
                    {
                        PersonId = person.PersonId,
                        Template = t,
                        Weight = DemoCardPoolRewardProvider.DefaultTestWeight
                    });
            }
        }

        // 通用牌：不绑定人物，始终可奖励
        foreach (var t in templates)
        {
            if (t != null && t.CardKind == BattleCardKind.Common)
                result.Add(new RunRewardCandidate
                {
                    PersonId = null,
                    Template = t,
                    Weight = DemoCardPoolRewardProvider.DefaultTestWeight
                });
        }

        return result;
    }
}


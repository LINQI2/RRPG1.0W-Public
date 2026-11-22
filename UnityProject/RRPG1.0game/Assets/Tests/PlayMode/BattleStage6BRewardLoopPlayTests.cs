using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Battle V0.1 Stage 6B — Run 卡牌奖励闭环 PlayMode 验证（Playable Milestone D 核心）。
///
/// 真实产品路径闭环（RunState/RunConfig 由 AdventureManager 创建，不手工塞 PendingCardRewardCount）：
///   Battle 1 → 胜利获得领悟跨过阈值（默认测试值 30/30）→ 三选一奖励界面出现
///   → 点击候选牌 / Skip → Run Deck 持久化 → 结算显示 → Battle 2 SharedDeck 真实反映结果。
///
/// 两条真实路径：
///   RewardLoop_SelectViaRealPath...  ：选择候选牌 → Copies+1 → Battle 2 含新牌；
///   RewardLoop_SkipViaRealPath...    ：Skip → 不获得牌但消耗机会 → Battle 2 不含该牌。
/// </summary>
public class BattleStage6BRewardLoopPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object GetDataManager() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(o);
    }

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    /// <summary>M1：按模板 CharacterId 取该集合中具体人物实例的真实 PersonId（默认每模板单实例；同模板首个）。</summary>
    private static string CharacterPersonId(object collection, string characterId)
    {
        var owned = (IList)GetMember(collection, "OwnedCharacters");
        if (owned != null)
        {
            foreach (var c in owned)
            {
                if (c == null) continue;
                if ((string)GetMember(c, "CharacterId") == characterId)
                {
                    string pid = (string)GetMember(c, "PersonId");
                    if (!string.IsNullOrEmpty(pid)) return pid;
                }
            }
        }
        return characterId;
    }

    [UnityTest]
    public IEnumerator RewardLoop_SelectViaRealPath_NextBattleDeckContainsNewCard()
    {
        yield return RunProtected(RunRewardLoop(true));
    }

    [UnityTest]
    public IEnumerator RewardLoop_SkipViaRealPath_ConsumesWithoutCard()
    {
        yield return RunProtected(RunRewardLoop(false));
    }

    private IEnumerator RunProtected(IEnumerator run)
    {
        while (true)
        {
            bool moved;
            try { moved = run.MoveNext(); }
            catch (System.Exception e) { Debug.LogError("[Stage6BPlayTest] 异常：" + e); throw; }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunRewardLoop(bool chooseCard)
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath", null);
        try
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var battleUI = FindInstance("BattleUI");
            Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

            var collection = CallStatic("SaveSystem", "NewGame", null);
            Assert.IsNotNull(collection, "NewGame 失败");

            var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
            var dataManager = GetDataManager();

            object hero = null;
            var heroes = (IList)GetMember(collection, "OwnedHeroes");
            if (heroes != null && heroes.Count > 0)
            {
                hero = heroes[0];
                var hd = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
                if (hd != null) Call(exp, "SelectHero", hero, hd);
            }
            var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
            Assert.IsNotNull(charCards, "角色卡册为空");
            if (charCards.Count > 0) Call(exp, "AddCharacterCard", charCards[0]); // 战士
            if (charCards.Count > 1) Call(exp, "AddCharacterCard", charCards[1]); // 猎人

            var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
            var args = new object[2];
            Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
            var chars = args[0];
            hero = args[1];

            // ---------- 真实产品路径：RunState / RunConfig 由 AdventureManager 创建 ----------
            // 默认测试值：InsightThreshold=30, InsightPerBattle=30 → 一场胜利自然触发 1 次卡牌奖励机会。
            // 不手工塞 PendingCardRewardCount、不直接调用 ShowReward —— 完全走 BattleVictory → AddInsight → 奖励分支。
            var adventure = System.Activator.CreateInstance(GetType("AdventureManager"),
                new object[]
                {
                    collection, chars, hero,
                    GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
                    StaticMonsterTestData.Build(), null
                });
            var runState = (RunState)GetProperty(adventure, "RunState");
            var runConfig = GetProperty(adventure, "RunConfig");
            Assert.AreEqual(30, runState.InsightThreshold, "真实路径 RunConfig 默认阈值应为 30（测试值，未冻结）");

            Call(battleUI, "SetRunState", runState, runConfig);
            Call(battleUI, "InitFromExpedition", chars, hero,
                GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
                StaticMonsterTestData.Build(), collection);

            var expUI = FindInstance("ExpeditionUI");
            var canvas = GetField(expUI, "battleCanvas");
            if (canvas != null) ((GameObject)canvas).SetActive(true);
            var battleGo = GetProperty(battleUI, "gameObject");
            if (battleGo != null) ((GameObject)battleGo).SetActive(true);
            yield return null;

            Call(battleUI, "StartBattleWithExpedition", null);
            yield return null;

            var charList = (IList)chars;
            if (charList.Count > 0)
            {
                var cd = Call(dataManager, "GetCharacterById", GetMember(charList[0], "CharacterId"));
                if (cd != null) Call(battleUI, "PlaceUnit", charList[0], cd, GetField(battleUI, "playerFrontRow"));
            }
            if (charList.Count > 1)
            {
                var cd = Call(dataManager, "GetCharacterById", GetMember(charList[1], "CharacterId"));
                if (cd != null) Call(battleUI, "PlaceUnit", charList[1], cd, GetField(battleUI, "playerMiddleRow"));
            }
            if (hero != null)
            {
                var hd = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
                if (hd != null) Call(battleUI, "PlaceHero", hd, hero, GetField(battleUI, "playerBackRow"));
            }
            yield return null;

            Call(battleUI, "OnStartBattle", null);
            yield return null;

            var v1 = GetField(battleUI, "v1Runtime");
            Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
            float wait = 0f;
            while (true)
            {
                var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
                if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn) break;
                wait += Time.deltaTime;
                if (wait > 10f) break;
                yield return null;
            }
            Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn,
                (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");

            var battleSystem = GetField(battleUI, "_battleSystem");
            Assert.IsNotNull(battleSystem, "_battleSystem 未初始化");
            var data = Call(battleSystem, "GetBattleData", null);

            // ---------- 全灭敌人 → 结算 → 奖励流程 ----------
            var enemies = (IList)GetMember(data, "EnemyUnits");
            Assert.Greater(enemies.Count, 0, "应存在敌人");
            foreach (var e in enemies) Call(battleSystem, "ApplyBattleDamage", e, 9999);

            var rewardUI = FindInstance("RunRewardUI");
            Assert.IsNotNull(rewardUI, "RunRewardUI 宿主应存在");
            var resultUI = GetField(battleUI, "_resultUI");
            Assert.IsNotNull(resultUI, "_resultUI 未绑定");

            wait = 0f;
            while (true)
            {
                var rewardCanvas = (GameObject)GetField(rewardUI, "canvasRoot");
                if (rewardCanvas != null && rewardCanvas.activeSelf) break;
                wait += Time.deltaTime;
                if (wait > 6f) break;
                yield return null;
            }


            // ==================== 奖励流程断言 ====================

            // 奖励界面先于结算界面出现（非每战固定三选一：有 Pending 才进入）
            Assert.IsTrue(((GameObject)GetField(rewardUI, "canvasRoot")).activeSelf, "存在 Pending 奖励时应先显示选牌界面");
            Assert.IsFalse(((GameObject)GetField(resultUI, "canvasRoot")).activeSelf, "结算界面应在奖励处理完后才显示");

            // 领悟跨阈值（真实链路：BattleVictory 30 领悟 ≥ 阈值 30）→ 1 次奖励机会
            Assert.AreEqual(1, runState.PendingCardRewardCount, "一场胜利应自然触发 1 次卡牌奖励机会");
            Assert.AreEqual(0, runState.CurrentInsight, "30 - 30 = 0，触发只扣阈值不清零");

            // 三张候选牌已实例化
            var container = (RectTransform)GetField(rewardUI, "cardContainer");
            Assert.IsNotNull(container, "Container_RewardCards 未解析");
            int rewardCardCount = 0;
            Button firstCardBtn = null;
            foreach (Transform child in container)
            {
                if (child == null || !child.name.StartsWith("RewardCard_")) continue;
                rewardCardCount++;
                var btn = child.GetComponent<Button>();
                if (btn != null && firstCardBtn == null) firstCardBtn = btn;
            }
            Assert.AreEqual(3, rewardCardCount, "每次三选一应显示 3 张候选牌");
            Assert.IsNotNull(firstCardBtn, "候选牌应可点击");

            // 记录第一个候选
            var candidates = (IList)GetField(rewardUI, "_currentCandidates");
            Assert.AreEqual(3, candidates.Count);
            var cand0 = candidates[0];
            string personId0 = (string)GetMember(cand0, "PersonId");
            string templateId0 = ((BattleCardData)GetMember(cand0, "Template")).CardId;
            Assert.IsFalse(string.IsNullOrEmpty(templateId0), "候选牌模板不应为空");

            int copiesBefore = 0;
            var entryBefore = runState.RunDeckEntries.FirstOrDefault(e => e.PersonId == personId0 && e.Template.CardId == templateId0);
            if (entryBefore != null) copiesBefore = entryBefore.Copies;

            if (chooseCard)
            {
                // ---------- 真实路径：点击候选牌选择 ----------
                firstCardBtn.onClick.Invoke();
                yield return null;

                Assert.AreEqual(0, runState.PendingCardRewardCount, "选择应消耗全部 1 次机会");
                var entryAfter = runState.RunDeckEntries.FirstOrDefault(e => e.PersonId == personId0 && e.Template.CardId == templateId0);
                Assert.IsNotNull(entryAfter, "选中牌应加入对应具体人物 Run Deck");
                Assert.AreEqual(copiesBefore + 1, entryAfter.Copies, "选择后具体人物 Run Deck Copies 应 +1");
            }
            else
            {
                // ---------- 真实路径：Skip（不获得牌但消耗机会） ----------
                int deckBeforeSkip = runState.RunDeckEntries.Count;
                ((Button)GetField(rewardUI, "btnSkip")).onClick.Invoke();
                yield return null;

                Assert.AreEqual(0, runState.PendingCardRewardCount, "Skip 应消耗全部 1 次机会");
                Assert.AreEqual(deckBeforeSkip, runState.RunDeckEntries.Count, "Skip 不增加牌");
            }

            Assert.IsFalse(((GameObject)GetField(rewardUI, "canvasRoot")).activeSelf, "全部机会处理完奖励面板应隐藏");
            Assert.IsTrue(((GameObject)GetField(resultUI, "canvasRoot")).activeSelf, "结算界面应在奖励处理完后显示");
            Assert.AreEqual(0, runState.CurrentInsight, "胜利领悟 30 跨阈值 30 后余量 0");

            // 人工验收修复：战斗结束离开 Battle 后，上一战 V1 手牌 UI 必须立即清理（不残留到下一战战前布阵）
            var handContainer = (RectTransform)GetField(v1, "handContainer");
            int v1Remain = 0;
            if (handContainer != null)
            {
                foreach (Transform child in handContainer)
                {
                    if (child != null && child.name.StartsWith("V1Card_")) v1Remain++;
                }
            }
            Assert.AreEqual(0, v1Remain, "战斗结束离开 Battle 后手牌区不得残留上一战 V1 手牌");

            // ==================== Battle 2：SharedDeck 真实包含/不包含上一战牌 ====================
            var deckEntriesBefore = runState.RunDeckEntries.Count;
            // M1：Run Deck 归属为真实 PersonId（播种于 AdventureManager），过滤必须用真实 PersonId
            var runChars = (IList)chars;
            string runPidA = CharacterPersonId(collection, (string)GetMember(runChars[0], "CharacterId"));
            string runPidB = CharacterPersonId(collection, (string)GetMember(runChars[1], "CharacterId"));
            string runHeroPid = hero != null ? (string)GetMember(hero, "PersonId") : null;
            var deployedIds = new List<string> { runPidA, runPidB, runHeroPid };
            Call(v1, "InitForBattle", deployedIds, runState.GetRunDeckEntries());
            var deck = (SharedDeck)GetProperty(v1, "Deck");
            if (chooseCard)
            {
                Assert.IsTrue(deck.DrawPile.Exists(c => c.TemplateId == templateId0 && c.OwnerPersonId == personId0),
                    "Battle 2 SharedDeck 应包含上一战获得的牌");
            }
            else
            {
                // Skip 不增加牌：候选模板可能在种子牌组已存在，故校验“该具体人物该模板 Copies 未增加”而非“不存在”。
                var entryAfterSkip = runState.RunDeckEntries.FirstOrDefault(e => e.PersonId == personId0 && e.Template.CardId == templateId0);
                if (entryAfterSkip != null)
                    Assert.AreEqual(copiesBefore, entryAfterSkip.Copies, "Skip 后具体人物该模板 Copies 不应增加");
            }
            Assert.AreEqual(deckEntriesBefore, runState.RunDeckEntries.Count, "每战重建 SharedDeck 不得清除 Run Deck");
        }
        finally
        {
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }
}


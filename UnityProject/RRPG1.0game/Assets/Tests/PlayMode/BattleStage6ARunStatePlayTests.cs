using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Battle V0.1 Stage 6A — Run 状态与结算生命周期 PlayMode 集成验证
///
/// 从正式产品路径（SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition → BattleUI + RunState）进入战斗，验证：
///   1. 第一战掉血 → 战斗结束 HP 回写 Run 状态 + 战后恢复 → 第二战不是满血；
///   2. Battle 结束不直接写长期 Gold/Exp（写入 Run PendingRewards）；胜利提供领悟；
///   3. Run Deck 第一战新增牌后，第二战仍保留；每战重建 SharedDeck 不清除 Run Deck；
///   4. EndRun(SafeReturn) 兑现 PendingRewards；EndRun(Defeat/Retreat) 损失未冻结仅占位。
///
/// 团结引擎限制 asmdef 引用 Assembly-CSharp：BattleUI/AdventureManager 等经反射访问；
/// RunState / SharedDeck / BattleCardData 位于 BattleStage1.Runtime 可强类型访问。
/// </summary>
public class BattleStage6ARunStatePlayTests
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

    private static object NewAdventure(object collection, object chars, object hero, object dataManager)
    {
        return System.Activator.CreateInstance(GetType("AdventureManager"),
            new object[]
            {
                collection, chars, hero,
                GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
                StaticMonsterTestData.Build(), null
            });
    }

    private static int LongTermExpOf(object collection, string personId)
    {
        var owned = (IList)GetMember(collection, "OwnedCharacters");
        if (owned == null) return -1;
        foreach (var c in owned)
        {
            if ((string)GetMember(c, "CharacterId") == personId) return (int)GetMember(c, "Exp");
        }
        return -1;
    }

    /// <summary>
    /// Initial Person &amp; Profession Bootstrap / Battle → Person EXP 回归：读取该模板对应具体人物的
    /// Profession EXP（= CharacterGrowthUI 读取的 GrowthAuthority.Resolve(person).Exp）；无 Profession 返回 -1。
    /// </summary>
    private static int LongTermProfessionExpOf(object collection, string characterId)
    {
        string personId = CharacterPersonId(collection, characterId);
        if (string.IsNullOrEmpty(personId)) return -1;
        object person = Call(collection, "FindPersonByPersonId", personId);
        if (person == null) return -1;
        object progress = GetMember(person, "ActiveProfessionProgress");
        return progress == null ? -1 : (int)GetMember(progress, "ProfessionExp");
    }

    /// <summary>
    /// M1：按模板 CharacterId 取该集合中具体人物实例的真实 PersonId（默认每模板单实例；同模板首个）。
    /// </summary>
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

    private static int CharacterMaxHp(object collection, string characterId)
    {
        var owned = (IList)GetMember(collection, "OwnedCharacters");
        if (owned != null)
        {
            foreach (var c in owned)
            {
                if (c == null) continue;
                if ((string)GetMember(c, "CharacterId") == characterId) return (int)GetMember(c, "MaxHP");
            }
        }
        return -1;
    }

    private static string CharacterDisplayName(object collection, string characterId)
    {
        var owned = (IList)GetMember(collection, "OwnedCharacters");
        if (owned != null)
        {
            foreach (var c in owned)
            {
                if (c == null) continue;
                if ((string)GetMember(c, "CharacterId") == characterId) return (string)GetMember(c, "DisplayName");
            }
        }
        return characterId;
    }

    [UnityTest]
    public IEnumerator RunHpCarriesToNextBattle_AndSettlementWritesPending_NotLongTerm()
    {
        IEnumerator run = RunStage6AFlow();
        while (true)
        {
            bool moved;
            try { moved = run.MoveNext(); }
            catch (System.Exception e) { Debug.LogError("[Stage6APlayTest] 异常：" + e); throw; }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunStage6AFlow()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath", null);
        try
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var battleUI = FindInstance("BattleUI");
            Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

            // ---------- 正式远征数据流 ----------
            var collection = CallStatic("SaveSystem", "NewGame", null);
            Assert.IsNotNull(collection, "NewGame 失败");
            Assert.AreEqual(100, (int)GetMember(collection, "Gold"), "NewGame 初始金币");

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
            Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
            var args = new object[2];
            Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
            var chars = args[0];
            hero = args[1];

            // ---------- Stage 6A：Run 状态宿主（强类型） ----------
            var runState = new RunState();
            runState.InsightThreshold = 100;
            runState.PostBattleRecoveryPercent = 0.05f;
            runState.PostBattleRecoveryMin = 1;
            var allBattleCards = (IEnumerable<BattleCardData>)GetProperty(dataManager, "AllBattleCards");

            // M1：Run 播种与 Battle 归属全部使用真实 PersonId（同模板多实例不塌缩）。
            // 这里从长期实例解析：每名出征角色 = 该卡对应实例（默认单实例下按模板查找即可）。
            var charList0 = (IList)chars;
            string cid1 = (string)GetMember(charList0[0], "CharacterId");
            string cid2 = (string)GetMember(charList0[1], "CharacterId");
            string pid1 = CharacterPersonId(collection, cid1);
            string pid2 = CharacterPersonId(collection, cid2);
            string heroPid6a = hero != null
                ? ((string)GetMember(hero, "PersonId") ?? (string)GetMember(hero, "HeroId")) : null;
            runState.GetOrCreatePerson(pid1, CharacterDisplayName(collection, cid1), CharacterMaxHp(collection, cid1));
            runState.GetOrCreatePerson(pid2, CharacterDisplayName(collection, cid2), CharacterMaxHp(collection, cid2));
            if (heroPid6a != null)
                runState.GetOrCreatePerson(heroPid6a, (string)GetMember(hero, "DisplayName"), (int)GetMember(hero, "MaxHP"));
            runState.SeedRunDeckFromPool(new List<string> { pid1, pid2, heroPid6a }, allBattleCards);

            Call(battleUI, "SetRunState", runState, null);
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


            // 布阵：战士 Front、猎人 Middle、英雄 Back
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

            // 等待玩家回合（InitV1Runtime 已从 Run Deck 构筑共享牌库）
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

            // ---------- 第一战：我方 char_001 掉血 40，然后全灭敌人 → 结算 ----------
            var playerUnits = (IList)GetMember(data, "PlayerUnits");
            object char001Unit = null;
            foreach (var u in playerUnits)
            {
                if ((string)GetMember(u, "SourceId") == cid1) { char001Unit = u; break; }
            }
            Assert.IsNotNull(char001Unit, "首名远征角色未上阵");
            int unitMaxHp = (int)GetMember(char001Unit, "MaxHP");
            Call(battleSystem, "ApplyBattleDamage", char001Unit, 40);

            var enemies = (IList)GetMember(data, "EnemyUnits");
            Assert.Greater(enemies.Count, 0, "应存在敌人");
            foreach (var e in enemies) Call(battleSystem, "ApplyBattleDamage", e, 9999);

            var resultUI = GetField(battleUI, "_resultUI");
            Assert.IsNotNull(resultUI, "_resultUI 未绑定");
            wait = 0f;
            while (true)
            {
                var canvasRoot = (GameObject)GetField(resultUI, "canvasRoot");
                if (canvasRoot != null && canvasRoot.activeSelf) break;
                wait += Time.deltaTime;
                if (wait > 6f) break;
                yield return null;
            }
            Assert.IsTrue(((GameObject)GetField(resultUI, "canvasRoot")).activeSelf, "结算界面应显示");


            // ==================== 战后断言 ====================

            // 1. HP 回写 + 战后恢复（不是满血）
            var p = runState.GetPerson(pid1);
            Assert.IsNotNull(p, "Run 人物（PersonId）应存在");
            int damagedHp = Mathf.Max(0, unitMaxHp - 40); // Battle 内伤害下限 0（ApplyBattleDamage 钳制）
            int recover = Mathf.Max(1, Mathf.CeilToInt(unitMaxHp * 0.05f - 0.0001f)); // 与 RunState 公式一致（epsilon）
            int expectedRecovered = Mathf.Min(unitMaxHp, damagedHp + recover);
            Assert.AreEqual(expectedRecovered, p.CurrentHP, "战后 HP 应回写并按配置恢复");
            Assert.AreNotEqual(unitMaxHp, p.CurrentHP, "战后不自动满血");

            // 2. Battle 结束不写长期 Gold/Exp；只写 Run Pending。
            // M5 Run/Return Growth Semantic Migration：产品默认角色（普通 Character）无 Active Profession，
            // 故不产生 Battle ProfessionExp、也不 fallback 到 Legacy Exp（上阵角色 Pending 均为 0）；
            // 英雄保持 M4 后 Legacy Run Growth（胜利获得 Legacy 经验 → Run Pending）。
            Assert.AreEqual(100, (int)GetMember(collection, "Gold"), "Battle 结束不得直接写长期 Gold");
            Assert.AreEqual(20, runState.PendingGold, "胜利金币应写入 Run PendingGold");
            Assert.IsNotNull(hero, "本场应存在英雄");
            Assert.AreEqual(10, runState.GetPerson(heroPid6a).PendingGrowthExp,
                "Hero Legacy Run Growth：胜利后英雄 Legacy 经验应写入 Run Pending");
            Assert.AreEqual(0, runState.GetPerson(pid1).PendingGrowthExp,
                "M5：普通角色不产生 Legacy Pending GrowthExp（不 fallback Legacy）");
            // Initial Person &amp; Profession Bootstrap + Battle → Person EXP 回归修复：
            // 固定初始人物开局即有 Active Profession，且结算的 Profession 事实来自真实长期 Person Authority
            // → 实际上阵的角色必须产生 Battle ProfessionExp（只写 Run Pending，不直接写长期）。
            Assert.AreEqual(10, runState.GetPerson(pid1).PendingProfessionExp,
                "上阵的 Profession Character 必须产生 Battle ProfessionExp（+10，Test 值）");
            Assert.AreEqual(0, (int)GetMember(hero, "Exp"), "英雄长期经验不得在战中被写");
            Assert.AreEqual(0, LongTermExpOf(collection, cid1), "角色 Legacy 经验不得在战中被写");
            Assert.AreEqual(0, LongTermProfessionExpOf(collection, cid1),
                "Battle 只写 Run Pending：不得直接写长期 ProfessionExp");

            // 3. 胜利提供全队共享领悟（默认每战 30）
            Assert.AreEqual(30, runState.CurrentInsight, "胜利应获得领悟");

            // 4. 第二战从 Run CurrentHP 初始化（不是满血）
            Call(battleUI, "SetRunState", runState, null);
            Call(battleUI, "InitFromExpedition", chars, hero,
                GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
                StaticMonsterTestData.Build(), collection);
            var allChars = (IList)GetField(battleUI, "_allCharacters");
            int secondHp = -1;
            foreach (var ci in allChars)
            {
                if ((string)GetMember(ci, "CharacterId") == cid1) { secondHp = (int)GetMember(ci, "CurrentHP"); break; }
            }
            Assert.AreEqual(expectedRecovered, secondHp, "第二战角色应从 Run CurrentHP 初始化");
            Assert.AreNotEqual(unitMaxHp, secondHp, "第二战不应满血");

            // 5. Run Deck：第一战新增牌 → 第二战保留；每战重建 SharedDeck 不清除 Run Deck
            var newCard = ScriptableObject.CreateInstance<BattleCardData>();
            newCard.CardId = "bcard_reward_test";
            newCard.DisplayName = "奖励测试牌";
            newCard.CardKind = BattleCardKind.Character;
            runState.AddRunCard(pid1, newCard);
            int deckEntriesBefore = runState.RunDeckEntries.Count;

            var deployedIds = new List<string> { pid1, pid2, heroPid6a };
            Call(v1, "InitForBattle", deployedIds, runState.GetRunDeckEntries());
            var deck = (SharedDeck)GetProperty(v1, "Deck");
            Assert.IsTrue(deck.DrawPile.Exists(c => c.TemplateId == "bcard_reward_test"), "Run Deck 新增牌应进入下一战");
            Assert.AreEqual(deckEntriesBefore, runState.RunDeckEntries.Count, "每战重建 SharedDeck 不得清除 Run Deck");

            // 6. EndRun：SafeReturn 兑现 Pending；Defeat 占位不兑现（独立集合验证）
            var collectionSafe = CallStatic("SaveSystem", "NewGame", null);
            var advSafe = NewAdventure(collectionSafe, chars, hero, dataManager);
            var advRunSafe = (RunState)GetProperty(advSafe, "RunState");
            advRunSafe.AddPendingGold(20);
            advRunSafe.AddPendingGrowthExp(CharacterPersonId(collectionSafe, cid1), 10);
            advRunSafe.AddPendingProfessionExp(CharacterPersonId(collectionSafe, cid1), 10);
            Call(advSafe, "EndRun", RunEndMode.SafeReturn);
            Assert.AreEqual(120, (int)GetMember(collectionSafe, "Gold"), "SafeReturn 应全额兑现 PendingGold");
            Assert.AreEqual(10, LongTermExpOf(collectionSafe, cid1), "SafeReturn 应兑现 Legacy PendingGrowthExp");
            Assert.AreEqual(10, LongTermProfessionExpOf(collectionSafe, cid1),
                "SafeReturn（产品 Return Settlement）必须把 PendingProfessionExp 兑现到长期 Person 的 ProfessionProgress");

            var collectionDefeat = CallStatic("SaveSystem", "NewGame", null);
            var advDefeat = NewAdventure(collectionDefeat, chars, hero, dataManager);
            var advRunDefeat = (RunState)GetProperty(advDefeat, "RunState");
            advRunDefeat.AddPendingGold(20);
            Call(advDefeat, "EndRun", RunEndMode.Defeat);
            Assert.AreEqual(100, (int)GetMember(collectionDefeat, "Gold"), "全灭损失未冻结：Defeat 占位不兑现 Pending");
        }
        finally
        {
            // 清理结算可能产生的存档，不污染真实存档
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }
}


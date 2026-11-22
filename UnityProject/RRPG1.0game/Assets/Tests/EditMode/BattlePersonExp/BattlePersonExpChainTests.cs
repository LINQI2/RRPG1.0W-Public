using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Battle → Person EXP（ProfessionExp）真实链的**长期冻结契约测试**（Disposable by Default，单文件、无新框架）。
///
/// 背景（Current Rule：GameSpec/Person §10 / M5）：
///   普通 Character 实际参战 → Battle ProfessionExp → Run Pending(PendingProfessionExp) →
///   Return Settlement 全量兑现 → Person-owned ProfessionProgress.ProfessionExp → Save/Load →
///   CharacterGrowthUI 读取 GrowthAuthority.Resolve(person).Exp。
///
/// 关键机制（本测试显式固化，防止再次回归）：
///   `BattleUI.InitFromExpedition` 为本场战斗创建 **Battle 临时 CharacterInstance**（只复制 PersonId，
///   不复制长期 profession 状态）；因此结算的 Profession 事实必须来自**真实长期 Person Authority**，
///   而不是结算时传入的那个临时实例。
/// </summary>
public class BattlePersonExpChainTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type Asm(string name) => Type.GetType(name + ", Assembly-CSharp");
    private static readonly Type DataManagerType = Asm("DataManager");
    private static readonly Type SaveSystemType = Asm("SaveSystem");
    private static readonly Type CharacterType = Asm("CharacterInstance");

    private string _savePath;

    [SetUp]
    public void SetUp()
    {
        _savePath = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        DeleteSave();
    }

    [TearDown]
    public void TearDown() => DeleteSave();

    private void DeleteSave()
    {
        if (!string.IsNullOrEmpty(_savePath) && System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
    }

    private static object Get(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    private static void Set(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) { f.SetValue(o, value); return; }
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "字段/属性不存在：" + name);
        p.SetValue(o, value, null);
    }

    private static object InvokeStatic(Type type, string method, params object[] args)
    {
        int count = args?.Length ?? 0;
        foreach (var m in type.GetMethods(F).Where(m => m.Name == method && m.GetParameters().Length == count))
            return m.Invoke(null, args);
        Assert.Fail("静态方法不存在或参数数量不匹配：" + method + " @ " + type.FullName);
        return null;
    }

    private static object InvokeMember(object o, string method, params object[] args)
    {
        int count = args?.Length ?? 0;
        foreach (var m in o.GetType().GetMethods(F).Where(m => m.Name == method && m.GetParameters().Length == count))
            return m.Invoke(o, args);
        Assert.Fail("方法不存在或参数数量不匹配：" + method + " @ " + o.GetType().FullName);
        return null;
    }

    private static object DataManagerInstance() =>
        DataManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    // ==================== 产品对象构造（镜像 BattleUI.InitFromExpedition 的真实路径） ====================

    /// <summary>Fresh New Game → 真实长期 PlayerCollection（固定初始人物已有 Active Profession）。</summary>
    private static object NewProductCollection() => InvokeStatic(SaveSystemType, "NewGame");

    private static object FindLongTermPersonWithProfession(object collection)
    {
        foreach (var c in (IEnumerable)Get(collection, "OwnedCharacters"))
        {
            if (c != null && (bool)Get(c, "HasActiveProfession")) return c;
        }
        Assert.Fail("Fresh New Game 后应有具备 Active Profession 的普通人物（Bootstrap 契约）");
        return null;
    }

    private static object FindCardOfPerson(object collection, string personId)
    {
        foreach (var card in (IEnumerable)Get(collection, "OwnedCharacterCards"))
        {
            if (card == null) continue;
            string bound = (string)InvokeMember(collection, "ResolvePersonIdForCard", card);
            if (bound == personId) return card;
        }
        return null;
    }

    /// <summary>
    /// 构造 Battle 临时 CharacterInstance —— 与 `BattleUI.InitFromExpedition` 同构：
    /// 只用战斗定义创建实例（不复制长期 ProfessionProgress）；PersonId 由调用方按 Card→Person 绑定写入。
    /// </summary>
    private static object NewBattleTempInstance(object card, string longTermPersonId)
    {
        string characterId = (string)Get(card, "CharacterId");
        object combatDefinition = InvokeMember(DataManagerInstance(), "GetCharacterById", characterId);
        Assert.IsNotNull(combatDefinition, "战斗定义必须存在：" + characterId);
        object temp = Activator.CreateInstance(CharacterType, characterId,
            (string)Get(combatDefinition, "DisplayName"),
            (int)Get(combatDefinition, "BaseHP"),
            (int)Get(combatDefinition, "BaseAttack"));
        Set(temp, "PersonId", longTermPersonId); // M1：战斗视图实例与长期人物共享同一 PersonId
        return temp;
    }

    /// <summary>结算输入（产品路径）：Battle 临时实例 + 从真实长期 Person Authority 读取 Profession 事实的接缝。</summary>
    private static object NewSettlement(object longTermCollection, bool injectLongTermLookup)
    {
        object settlement = Activator.CreateInstance(Asm("BattleSettlement"));
        if (!injectLongTermLookup) return settlement;

        var lookup = new Func<string, IPersonGrowth>(pid =>
        {
            var m = longTermCollection.GetType().GetMethods(F)
                .FirstOrDefault(x => x.Name == "FindPersonByPersonId" && x.GetParameters().Length == 1);
            return m != null ? (IPersonGrowth)m.Invoke(longTermCollection, new object[] { pid }) : null;
        });

        // 接缝可以是字段或属性（按仓库风格为 public 字段）；找不到即视为契约缺口。
        var field = settlement.GetType().GetField("LongTermPersonLookup", F);
        var prop = settlement.GetType().GetProperty("LongTermPersonLookup", F);
        Assert.IsTrue(field != null || prop != null,
            "BattleSettlement 必须提供 LongTermPersonLookup 接缝（从真实长期 Person 判定 Profession，"
            + "不得让 Battle 临时实例承载长期 Authority）");
        if (field != null) field.SetValue(settlement, lookup);
        else prop.SetValue(settlement, lookup, null);
        return settlement;
    }

    private static void ProcessSettlement(object settlement, bool victory, List<object> characters,
        List<string> deployedPersonIds, object runState, object hero = null)
    {
        var listType = typeof(List<>).MakeGenericType(CharacterType);
        var instanceList = (IList)Activator.CreateInstance(listType);
        foreach (var c in characters) instanceList.Add(c);
        InvokeMember(settlement, "ProcessSettlement", victory, instanceList, deployedPersonIds, hero, runState);
    }

    private static int PendingProfessionExpOf(RunState run, string personId)
    {
        var person = run.GetPerson(personId);
        return person != null ? person.PendingProfessionExp : 0;
    }

    private static int ProfessionExpOf(object longTermPerson) =>
        GrowthAuthority.Resolve((IPersonGrowth)longTermPerson).Exp;

    /// <summary>拒绝兑现的 sink（验证 Commit Acknowledgement：未被确认不得清零 Pending）。</summary>
    private sealed class RejectingSink : IRunRewardSink
    {
        public void AddGold(int amount) { }
        public void AddBuildingMaterial(int amount) { }
        public void AddGrowthExp(string personId, int amount) { }
        public bool TryAddProfessionExp(string personId, int amount) => false;
    }

    // ==================== 1. 机制证明：Battle 临时实例不承载长期 Profession ====================

    [Test]
    public void BattleTempInstance_CarriesNoLongTermProfession()
    {
        object collection = NewProductCollection();
        object longTerm = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(longTerm, "PersonId");
        object card = FindCardOfPerson(collection, personId);
        Assert.IsNotNull(card, "应能按 Card→Person 显式绑定找到该人物的角色卡");

        object temp = NewBattleTempInstance(card, personId);

        Assert.AreEqual(personId, (string)Get(temp, "PersonId"), "战斗临时实例与长期人物共享同一 PersonId");
        Assert.IsFalse((bool)Get(temp, "HasActiveProfession"),
            "Battle 临时实例不携带长期 ProfessionProgress（不得复制长期 Authority）");
        Assert.IsFalse(GrowthAuthority.Resolve((IPersonGrowth)temp).IsAvailable,
            "对 Battle 临时实例解析 Growth Authority 必然 IsAvailable=false");
        Assert.IsTrue(GrowthAuthority.Resolve((IPersonGrowth)longTerm).IsProfessionGrowth,
            "真实长期 Person 才是 Profession Growth Authority");
    }

    // ==================== 2. 真实链：Battle → Pending → Return Commit → 长期 EXP → UI/Save ====================

    [Test]
    public void DeployedPerson_GetsPendingProfessionExp_AndCommitsToLongTerm_VisibleToUI_AndSaveLoad()
    {
        object collection = NewProductCollection();
        object longTerm = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(longTerm, "PersonId");
        object card = FindCardOfPerson(collection, personId);
        int before = ProfessionExpOf(longTerm);

        object temp = NewBattleTempInstance(card, personId);
        var run = new RunState();
        object settlement = NewSettlement(collection, injectLongTermLookup: true);

        ProcessSettlement(settlement, true, new List<object> { temp }, new List<string> { personId }, run);

        Assert.AreEqual(10, PendingProfessionExpOf(run, personId),
            "实际参战的 Profession Person 必须产生 Battle ProfessionExp（PendingProfessionExp +10，Test 值）");

        object sink = Activator.CreateInstance(Asm("PlayerRunRewardSink"), collection);
        int committed = run.CommitPendingProfessionExp((IRunRewardSink)sink);
        Assert.AreEqual(10, committed, "Return 必须把 PendingProfessionExp 全量兑现到长期 Person");
        Assert.AreEqual(0, PendingProfessionExpOf(run, personId), "成功兑现后 Pending 清零");

        Assert.AreEqual(before + 10, ProfessionExpOf(longTerm),
            "CharacterGrowthUI 读取的 Profession EXP（GrowthAuthority.Resolve(person).Exp）必须增长 +10");

        InvokeStatic(SaveSystemType, "Save", collection);
        object loaded = InvokeStatic(SaveSystemType, "Load");
        object reloadedPerson = InvokeMember(loaded, "FindPersonByPersonId", personId);
        Assert.IsNotNull(reloadedPerson, "Load 后必须能按 PersonId 找回同一人物");
        Assert.AreEqual(before + 10, ProfessionExpOf(reloadedPerson), "Save → Load 后 Profession EXP 必须保持");
    }

    // ==================== 3. 未上阵 = 0 ====================

    [Test]
    public void UndeployedPerson_GetsNoProfessionExp()
    {
        object collection = NewProductCollection();
        object longTerm = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(longTerm, "PersonId");
        object temp = NewBattleTempInstance(FindCardOfPerson(collection, personId), personId);

        var run = new RunState();
        object settlement = NewSettlement(collection, injectLongTermLookup: true);
        ProcessSettlement(settlement, true, new List<object> { temp }, new List<string>(), run);

        Assert.AreEqual(0, PendingProfessionExpOf(run, personId), "随队但未上阵 = 0 ProfessionExp");
    }

    // ==================== 4. Hero 不误进 ProfessionExp（Legacy 通道不变） ====================

    [Test]
    public void Hero_DoesNotEnterProfessionExp_LegacyChannelUntouched()
    {
        object collection = NewProductCollection();
        var heroes = (IList)Get(collection, "OwnedHeroes");
        Assert.IsNotEmpty(heroes, "Fresh New Game 应有英雄");
        object hero = heroes[0];
        string heroPersonId = (string)Get(hero, "PersonId");

        object longTerm = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(longTerm, "PersonId");
        object temp = NewBattleTempInstance(FindCardOfPerson(collection, personId), personId);

        var run = new RunState();
        object settlement = NewSettlement(collection, injectLongTermLookup: true);
        ProcessSettlement(settlement, true, new List<object> { temp }, new List<string> { personId }, run, hero);

        Assert.AreEqual(0, PendingProfessionExpOf(run, heroPersonId), "Hero 不得进入 ProfessionExp 通道");
        Assert.AreEqual(10, run.GetPerson(heroPersonId).PendingGrowthExp,
            "Hero 仍走既有 Legacy Pending GrowthExp（M4 后语义不变）");
    }

    // ==================== 5. 两个 Person 之间不串经验 ====================

    [Test]
    public void TwoPersons_DoNotSharePendingProfessionExp()
    {
        object collection = NewProductCollection();
        var persons = new List<object>();
        foreach (var c in (IEnumerable)Get(collection, "OwnedCharacters"))
        {
            if (c != null && (bool)Get(c, "HasActiveProfession")) persons.Add(c);
            if (persons.Count == 2) break;
        }
        Assert.AreEqual(2, persons.Count, "Fresh New Game 应有至少 2 名具备 Profession 的人物");

        string pidA = (string)Get(persons[0], "PersonId");
        string pidB = (string)Get(persons[1], "PersonId");
        object tempA = NewBattleTempInstance(FindCardOfPerson(collection, pidA), pidA);
        object tempB = NewBattleTempInstance(FindCardOfPerson(collection, pidB), pidB);

        var run = new RunState();
        object settlement = NewSettlement(collection, injectLongTermLookup: true);
        ProcessSettlement(settlement, true, new List<object> { tempA, tempB }, new List<string> { pidA }, run);

        Assert.AreEqual(10, PendingProfessionExpOf(run, pidA), "上阵者应获得 +10");
        Assert.AreEqual(0, PendingProfessionExpOf(run, pidB), "未上阵的另一个人不得共享经验");
    }

    // ==================== 6. Return 重入 / 未确认不重复加 ====================

    [Test]
    public void CommitAcknowledgement_RejectingSink_KeepsPending_AndSecondCommitIsIdempotent()
    {
        object collection = NewProductCollection();
        object longTerm = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(longTerm, "PersonId");
        int before = ProfessionExpOf(longTerm);

        object temp = NewBattleTempInstance(FindCardOfPerson(collection, personId), personId);
        var run = new RunState();
        object settlement = NewSettlement(collection, injectLongTermLookup: true);
        ProcessSettlement(settlement, true, new List<object> { temp }, new List<string> { personId }, run);

        // 未确认（sink 拒绝）→ 不得乐观清零、不得写长期
        Assert.AreEqual(0, run.CommitPendingProfessionExp(new RejectingSink()));
        Assert.AreEqual(10, PendingProfessionExpOf(run, personId), "未被确认时 Pending 必须保留");
        Assert.AreEqual(before, ProfessionExpOf(longTerm), "未被确认时不得写长期");

        object sink = Activator.CreateInstance(Asm("PlayerRunRewardSink"), collection);
        Assert.AreEqual(10, run.CommitPendingProfessionExp((IRunRewardSink)sink));
        Assert.AreEqual(before + 10, ProfessionExpOf(longTerm));

        // 重入 Commit（同一 Active Run 再次 Return）不得重复加
        Assert.AreEqual(0, run.CommitPendingProfessionExp((IRunRewardSink)sink));
        Assert.AreEqual(before + 10, ProfessionExpOf(longTerm), "重复 Commit 不得重复兑现");
    }

    // 已配置长期 Authority 时，即使输入实例恰有 Profession，查询失败也不能借用它。
    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public void ConfiguredLookupFailure_DoesNotBorrowInputProfession(bool victory, bool throws)
    {
        object collection = NewProductCollection();
        object input = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(input, "PersonId");
        int before = ProfessionExpOf(input);
        object settlement = NewSettlement(collection, injectLongTermLookup: false);
        Set(settlement, "LongTermPersonLookup", new Func<string, IPersonGrowth>(pid =>
        {
            if (throws) throw new InvalidOperationException("lookup test failure");
            return null;
        }));
        var run = new RunState();

        ProcessSettlement(settlement, victory, new List<object> { input },
            new List<string> { personId }, run);

        Assert.AreEqual(0, PendingProfessionExpOf(run, personId),
            "已配置长期查询失败时，不得从输入实例借用 Profession 资格写 Pending");
        Assert.AreEqual(before, ProfessionExpOf(input), "失败判定不能写入长期人物");
        var person = run.GetPerson(personId);
        Assert.IsTrue(person == null || person.PendingGrowthExp == 0, "不能回退 Legacy EXP");
        Assert.AreEqual(victory ? 20 : 5, run.PendingGold, "该修复不改变既有金币通道");
    }

    // ==================== 7. 回归守卫：只看 Battle 临时实例 → ProfessionExp 恒为 0 ====================

    [Test]
    public void RegressionGuard_TempInstanceOnly_YieldsNoProfessionExp()
    {
        object collection = NewProductCollection();
        object longTerm = FindLongTermPersonWithProfession(collection);
        string personId = (string)Get(longTerm, "PersonId");
        object temp = NewBattleTempInstance(FindCardOfPerson(collection, personId), personId);

        var run = new RunState();
        object settlement = NewSettlement(collection, injectLongTermLookup: false); // 未接长期 Person
        ProcessSettlement(settlement, true, new List<object> { temp }, new List<string> { personId }, run);

        Assert.AreEqual(0, PendingProfessionExpOf(run, personId),
            "回归守卫：结算若只依据 Battle 临时实例判定 Profession，则 PendingProfessionExp 恒为 0 —— "
            + "这正是 Battle → Person EXP 缺口的机制，必须在产品路径接入真实长期 Person Authority。");
        Assert.IsFalse(GrowthAuthority.Resolve((IPersonGrowth)temp).IsAvailable,
            "临时实例的 Authority 不可用是机制前提（不得靠把 ProfessionProgress 复制到临时实例来绕过）");
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// V4F（Home↔Expedition Continuity Foundation）EditMode 测试共享辅助。
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，因此
/// PlayerCollection / CharacterInstance / CharacterCardData / AdventureManager / SaveSystem /
/// ExpeditionSystem / VillageRuntimeBridge / TownUI 经反射访问；
/// Village.Runtime（VillageTime / VillageState / VillageAssignmentSystem / VillageProjectSystem /
/// VillageWorldRegistry / ExpeditionRecord / ExpeditionReturnPhase / ExpeditionCatchUpResult / …）与
/// BattleStage1.Runtime（RunState / RunEndMode）可强类型访问。
/// 全部内容为 Test / Not Frozen（ws_test_* / prj_test_* / char_* 测试值）。
/// </summary>
public static class V4FTestKit
{
    public const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    public static Type Asm(string typeName) => Type.GetType(typeName + ", Assembly-CSharp")
        ?? Type.GetType("RRPG.StaticConfig." + typeName + ", StaticConfig.Runtime");

    public static object NewAsm(string typeName, params object[] ctorArgs)
    {
        return Activator.CreateInstance(Asm(typeName), ctorArgs);
    }

    public static object Get(object o, string name)
    {
        if (o == null) throw new ArgumentNullException(nameof(o), "Get target null");
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    public static void Set(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null)
        {
            f.SetValue(o, value);
            return;
        }
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "字段/属性不存在：" + name);
        p.SetValue(o, value, null);
    }

    public static object GetStaticMember(Type type, string name)
    {
        var f = type.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "静态字段/属性不存在：" + name + " @ " + type.FullName);
        return p.GetValue(null, null);
    }

    public static void SetStaticField(Type type, string name, object value)
    {
        var f = type.GetField(name, F);
        Assert.IsNotNull(f, "静态字段不存在：" + name + " @ " + type.FullName);
        f.SetValue(null, value);
    }

    public static object InvokeStatic(Type type, string method, params object[] args)
    {
        var candidates = type.GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = Pick(candidates, args);
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    public static object Call(object o, string method, params object[] args)
    {
        var candidates = o.GetType().GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = Pick(candidates, args);
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + o.GetType().FullName);
        return selected.Invoke(o, args);
    }

    private static MethodInfo Pick(List<MethodInfo> candidates, object[] args)
    {
        foreach (var m in candidates)
        {
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) return m;
        }
        return null;
    }

    // ==================== Player / Character 构造（Assembly-CSharp 反射） ====================

    public static object NewPlayerCollection()
    {
        return NewAsm("PlayerCollection");
    }
    public static void BindWorkPersons(params string[] ids)
    {
        var player = NewPlayerCollection();
        foreach (string id in ids)
        {
            var person=NewCharacter("char_001",id);Set(person,"PersonId",id);OwnedCharacters(player).Add(person);
        }
        InvokeStatic(Bridge,"SetSimulationPersonSource",player);
    }

    public static object NewCharacter(string characterId, string displayName = "测试人物", int hp = 30, int atk = 6)
    {
        return NewAsm("CharacterInstance", characterId, displayName, hp, atk);
    }

    public static object NewCharacterCard(string cardId, string characterId)
    {
        object card = typeof(ScriptableObject).GetMethod("CreateInstance", new[] { typeof(Type) })
            .Invoke(null, new object[] { Asm("CharacterCardData") });
        Set(card, "CardId", cardId);
        Set(card, "CharacterId", characterId);
        return card;
    }

    public static void AddCharacterWithCard(object collection, string templateId, string cardId,
        out object character, out string personId)
    {
        character = NewCharacter(templateId);
        object card = NewCharacterCard(cardId, templateId);
        OwnedCharacters(collection).Add(character);
        OwnedCharacterCards(collection).Add(card);
        InvokeInstance(collection, "BindCharacterCardOwner", cardId, PersonIdOf(character));
        personId = PersonIdOf(character);
    }

    public static object InvokeInstance(object o, string method, params object[] args)
    {
        MethodInfo m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    public static IList OwnedCharacters(object collection) => (IList)Get(collection, "OwnedCharacters");
    public static IList OwnedHeroes(object collection) => (IList)Get(collection, "OwnedHeroes");
    public static IList OwnedCharacterCards(object collection) => (IList)Get(collection, "OwnedCharacterCards");
    public static string PersonIdOf(object instance) => (string)Get(instance, "PersonId");
    public static int GoldOf(object collection) => (int)Get(collection, "Gold");
    public static void SetGold(object collection, int gold) => Set(collection, "Gold", gold);

    // ==================== AdventureManager / RunState ====================

    public static object NewAdventureManager(object collection, object hero)
    {
        object charCards = MakeList("CharacterCardData");
        object charDatas = MakeList("CharacterData");
        object skills = MakeList("SkillData");
        object monsters = MakeList("MonsterDefinition");

        var advType = Asm("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            Asm("PlayerCollection"),
            charCards.GetType(),
            Asm("HeroInstance"),
            charDatas.GetType(),
            skills.GetType(),
            monsters.GetType(),
            Asm("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        return ctor.Invoke(new object[] { collection, charCards, hero, charDatas, skills, monsters, null });
    }

    public static RunState RunStateOf(object adventure)
    {
        return (RunState)Get(adventure, "RunState");
    }

    public static object MakeList(string elementTypeName)
    {
        var listType = typeof(List<>).MakeGenericType(Asm(elementTypeName));
        return Activator.CreateInstance(listType);
    }

    // ==================== SaveSystem / VillageRuntimeBridge（反射） ====================

    private static Type SaveSystemType => Asm("SaveSystem");
    private static Type BridgeType => Asm("VillageRuntimeBridge");

    public static Type Bridge => BridgeType;

    public static void ResetBridge() => InvokeStatic(BridgeType, "ResetForTests");

    public static void EnsureInitialized() => InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });

    public static string SavePath() => (string)InvokeStatic(SaveSystemType, "GetSavePath");

    public static void DeleteSave()
    {
        string p = SavePath();
        if (p != null && File.Exists(p)) File.Delete(p);
    }

    public static bool Save(object collection) => (bool)InvokeStatic(SaveSystemType,
        ActiveRecord()?.Phase == ExpeditionReturnPhase.OccupancyCleared ? "SaveReturnFinal" : "Save", collection);

    public static object Load() => InvokeStatic(SaveSystemType, "Load");

    /// <summary>当前存档文件内容 SHA1（用于断言“某阶段是否真的发生了文件写入”）。</summary>
    public static string SaveHash()
    {
        string p = SavePath();
        if (string.IsNullOrEmpty(p) || !File.Exists(p)) return "<no-save>";
        using (var sha = SHA1.Create())
        {
            byte[] bytes = sha.ComputeHash(File.ReadAllBytes(p));
            var sb = new StringBuilder();
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // ==================== Bridge 便捷封装 ====================

    public static void SetExpedition(IEnumerable<string> ids) => InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)ids);

    public static void ClearExpedition() => InvokeStatic(BridgeType, "ClearExpeditionPersonIds");

    public static IReadOnlyList<string> ExpeditionIds()
    {
        return (IReadOnlyList<string>)GetStaticMember(BridgeType, "ExpeditionPersonIds");
    }

    public static IReadOnlyList<string> OccupiedIds()
    {
        return (IReadOnlyList<string>)InvokeStatic(BridgeType, "GetTemporaryOccupiedPersonIds");
    }

    public static bool BeginReturn(int days) => (bool)InvokeStatic(BridgeType, "BeginExpeditionReturn", days);

    public static bool MarkSettled() => (bool)InvokeStatic(BridgeType, "MarkExpeditionReturnSettled");

    public static ExpeditionCatchUpResult RunCatchUp()
    {
        return (ExpeditionCatchUpResult)InvokeStatic(BridgeType, "RunReturnCatchUp");
    }

    public static void CompleteSave(bool ok) => InvokeStatic(BridgeType, "CompleteExpeditionReturnSave", ok);

    public static void DiscardRecord() => InvokeStatic(BridgeType, "DiscardExpeditionReturnRecord");

    public static void SetElapsedDays(int days)
    {
        System.Func<int> provider = () => days;
        InvokeStatic(BridgeType, "SetExpeditionElapsedDaysProvider", provider);
    }

    public static void SetBeforeDayHook(Action<int> hook)
    {
        SetStaticField(BridgeType, "BeforeCatchUpDayTickTestHook", hook);
    }

    public static ExpeditionRecord ActiveRecord()
    {
        return (ExpeditionRecord)GetStaticMember(BridgeType, "ActiveReturnRecord");
    }

    public static VillageTime BridgeTime() => (VillageTime)GetStaticMember(BridgeType, "Time");

    public static VillageState BridgeState() => (VillageState)GetStaticMember(BridgeType, "State");

    public static VillageAssignmentSystem Assignments() => (VillageAssignmentSystem)GetStaticMember(BridgeType, "Assignments");

    public static VillageProjectSystem Projects() => (VillageProjectSystem)GetStaticMember(BridgeType, "Projects");

    public static VillageWorldRegistry World() => (VillageWorldRegistry)GetStaticMember(BridgeType, "World");

    public static VillageAssignmentResult Assign(string personId, string workplaceId)
    {
        return (VillageAssignmentResult)InvokeStatic(BridgeType, "TryAssignPerson", personId, workplaceId);
    }

    public static VillageProjectResult StartProject(string defId, params string[] personIds)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, personIds);
    }

    public static VillageProjectResult AddProjectParticipant(string projectId, string personId)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryAddProjectParticipant", projectId, personId);
    }

    public static void SetTime(int year, int month, int day)
    {
        var t = BridgeTime();
        var intradaySetup1 = t.ToSaveData();
        intradaySetup1.IntradayTime = null;
        intradaySetup1.Year = year;
        intradaySetup1.Month = month;
        intradaySetup1.Day = day;
        intradaySetup1.SecondsIntoDay = (0f) * VillageTime.LegacySecondsPerDay / t.SecondsPerDay;
        t.Restore(intradaySetup1);

    }
}

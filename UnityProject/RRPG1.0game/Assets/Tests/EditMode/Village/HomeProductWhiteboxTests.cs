using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RRPG.ProductHome.Whitebox.Tests
{
public sealed class HomeProductWhiteboxTests
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
    private static object Call(string type,string method,params object[] args)=>T(type).GetMethod(method,S).Invoke(null,args);
    private static object Get(object obj,string property)=>obj.GetType().GetProperty(property,I).GetValue(obj);
    [SetUp] public void Setup() { Call("VillageRuntimeBridge","ResetForTests");Call("EventRuntimeBridge","ResetForTests"); }
    [TearDown] public void Cleanup() { Call("VillageRuntimeBridge","ResetForTests");Call("EventRuntimeBridge","ResetForTests"); }
    [Test] public void Attention_EmptyAndGrowth_UsePureDomainQueryWithoutMutation()
    {
        Assert.AreEqual(0,((IList)Call("HomeAttentionProjection","Read")).Count);
        var player=Call("SaveSystem","NewGame");Call("VillageRuntimeBridge","SetSimulationPersonSource",player);
        var heroes=(IList)player.GetType().GetField("OwnedHeroes").GetValue(player);
        var hero=Activator.CreateInstance(T("HeroInstance"),"test_hero","白模成长人物",100);heroes.Add(hero);
        Call("GrowthConfigProvider","EnsureLoaded");
        hero.GetType().GetField("Exp").SetValue(hero,100000);
        string before=JsonUtility.ToJson(player)+JsonUtility.ToJson(Call("VillageRuntimeBridge","BuildSaveData"));
        var items=(IList)Call("HomeAttentionProjection","Read");Assert.Greater(items.Count,0);
        Assert.AreEqual("PersonGrowth",Get(items[0],"Intent").ToString());
        Assert.AreEqual(Get(items[0],"SubjectId"),Get(items[0],"Target"));
        Assert.AreEqual(before,JsonUtility.ToJson(player)+JsonUtility.ToJson(Call("VillageRuntimeBridge","BuildSaveData")));
    }
    [Test] public void ExplicitActivityCompletion_EmitsOnce_AndDoesNotWriteHistory()
    {
        Call("ActivityRuntimeBridge","ResetForTests");
        var feed=Activator.CreateInstance(T("HomeVillageFeed"));
        try
        {
            var day=Activator.CreateInstance(T("RRPG.AutonomousActivity.ActivityDayStamp"),1,1,2);
            var window=Activator.CreateInstance(T("RRPG.AutonomousActivity.ActivityDailyWindow"),day,new[]{"test:actor"},new[]{"test:actor"});
            Assert.AreEqual("Success",Call("ActivityRuntimeBridge","OpenDailyWindow",window).ToString());
            var candidate=Activator.CreateInstance(T("RRPG.AutonomousActivity.ActivityCandidate"),"test:key","Rest","test:provider","test:actor",null,"test:place",null,Enum.ToObject(T("RRPG.AutonomousActivity.ActivityTargetParticipation"),0));
            Assert.AreEqual("Success",Call("ActivityRuntimeBridge","TryCommitActivity","test:instance",candidate).ToString());
            Assert.AreEqual("Success",Call("ActivityRuntimeBridge","TryBeginExecution","test:instance").ToString());
            string history=JsonUtility.ToJson(Call("EventRuntimeBridge","BuildSaveData"));
            var args=new object[]{"test:instance",null};
            Assert.AreEqual("Success",Call("ActivityRuntimeBridge","TryComplete",args).ToString());
            Assert.AreEqual("Duplicate",Call("ActivityRuntimeBridge","TryComplete",args).ToString());
            Assert.AreEqual(1,((IList)Get(feed,"Recent")).Count);
            Assert.AreEqual(history,JsonUtility.ToJson(Call("EventRuntimeBridge","BuildSaveData")));
        }
        finally { ((IDisposable)feed).Dispose();Call("ActivityRuntimeBridge","ResetForTests"); }
    }
}
}

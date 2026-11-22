using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace R3A
{
public class PersonDetailProjectionTests
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Type T(string n,string a="Assembly-CSharp")=>Type.GetType(n+", "+a,true);
    static object Get(object o,string n)=>o.GetType().GetField(n,F)?.GetValue(o)??o.GetType().GetProperty(n,F)?.GetValue(o);
    static void Set(object o,string n,object v){var f=o.GetType().GetField(n,F);if(f!=null)f.SetValue(o,v);else o.GetType().GetProperty(n,F).SetValue(o,v);}
    static object Call(string t,string m,params object[] args)=>T(t).GetMethod(m,F).Invoke(null,args);
    object source,person;string id;object oldVillage,oldSocial,oldActivity;
    static string World()=>JsonUtility.ToJson(Call("VillageRuntimeBridge","BuildSaveData"))+JsonUtility.ToJson(Call("ActivityRuntimeBridge","BuildSaveData"))+JsonUtility.ToJson(Call("SocialRuntimeBridge","BuildSaveData"));
    object Read(string key,ProductHomeSnapshot home=null)=>Call("PersonDetailReadAdapter","Read",source,key,home??(ProductHomeSnapshot)Call("ProductHomeReadAdapter","Read"));
    static string[] Pages(object read)=>((IEnumerable)Get(read,"Pages")).Cast<string>().ToArray();
    [SetUp] public void Setup(){oldVillage=Call("VillageRuntimeBridge","BuildSaveData");oldSocial=Call("SocialRuntimeBridge","BuildSaveData");oldActivity=Call("ActivityRuntimeBridge","BuildSaveData");Call("VillageRuntimeBridge","ResetForTests");source=Call("SaveSystem","NewGame");Call("VillageRuntimeBridge","EnsureInitialized",new object[]{null});Call("VillageRuntimeBridge","SetSimulationPersonSource",source);((VillageTime)T("VillageRuntimeBridge").GetField("Time").GetValue(null)).Pause();person=((IList)Get(source,"OwnedCharacters"))[0];id=(string)Get(person,"PersonId");}
    [TearDown] public void Restore(){Call("VillageRuntimeBridge","ResetForTests");Call("VillageRuntimeBridge","EnsureInitialized",oldVillage);Call("SocialRuntimeBridge","Restore",oldSocial);Call("ActivityRuntimeBridge","Restore",oldActivity);}
    [Test] public void StableIdentity_NameFilter_Hero_NoProfession_Missing_AndAway()
    {
        var chars=(IList)Get(source,"OwnedCharacters");Set(chars[0],"DisplayName","同名测试");Set(chars[1],"DisplayName","同名测试");
        var ids=((IEnumerable)Call("PersonDetailReadAdapter","Roster",source,"同名测试")).Cast<string>().ToArray();Assert.AreEqual(2,ids.Length);Assert.AreNotEqual(ids[0],ids[1]);
        Set(person,"ActiveProfessionProgress",null);var empty=Read(id);Assert.IsFalse((bool)Get(empty,"CanOpenGrowth"));Assert.That(Pages(empty)[2],Does.Contain("尚未形成职业"));
        var hero=((IList)Get(source,"OwnedHeroes"))[0];var heroRead=Read((string)Get(hero,"PersonId"));Assert.That(Pages(heroRead)[0],Does.Contain("Hero"));Assert.AreEqual("尚无记录",Pages(heroRead)[4]);
        Assert.IsFalse((bool)Get(Read("missing_person"),"Exists"));
        var away=new HomePersonSnapshot(id,"同名测试","test","away",null,null,null,"无职责",VillageAvailability.Expedition,"远征占用","远征中",true);
        var unavailable=Read(id,new ProductHomeSnapshot(new[]{away},new HomePlaceSnapshot[0],new string[0]));Assert.IsFalse((bool)Get(unavailable,"CanOpenGrowth"));Assert.That(Pages(unavailable)[0],Does.Contain("Away"));
        chars.Remove(person);Assert.IsFalse((bool)Get(Read(id),"Exists"));
    }
    [Test] public void FiveTabs_DoNotWriteAuthorities_AndHistoryKeepsSameDayOrder()
    {
        var history=Activator.CreateInstance(T("PersonLifeHistory","Profession.Runtime"));
        history.GetType().GetMethod("AppendProfessionFormed").Invoke(history,new object[]{1,2,3,"profession_test_hunter"});history.GetType().GetMethod("AppendExpeditionReturned").Invoke(history,new object[]{1,2,3,1});Set(person,"LifeHistory",history);
        string before=World(),personBefore=JsonUtility.ToJson(person);var read=Read(id);for(int i=0;i<20;i++)Read(id);
        Assert.AreEqual(before,World());Assert.AreEqual(personBefore,JsonUtility.ToJson(person));var text=Pages(read)[4];Assert.Less(text.IndexOf("职业形成",StringComparison.Ordinal),text.IndexOf("撤退返回",StringComparison.Ordinal));
        Assert.Throws<NotSupportedException>(()=>((System.Collections.Generic.IList<string>)Get(read,"Pages")).Clear());
    }
    [Test] public void Relations_UseRealFacts_DirectedQueries_AndUnknownTargetCannotNavigate()
    {
        Assert.AreEqual("尚无记录",Pages(Read(id))[3]);var target=(string)Get(((IList)Get(source,"OwnedCharacters"))[1],"PersonId");
        var dto=Activator.CreateInstance(T("RRPG.Social.SocialWorldSaveData","Social.Runtime"));var ft=T("RRPG.Social.RelationalFactSaveData","Social.Runtime");var rt=T("RRPG.Social.DirectedRelationshipSaveData","Social.Runtime");
        var facts=Array.CreateInstance(ft,2);for(int i=0;i<2;i++){var fact=Activator.CreateInstance(ft);Set(fact,"SourceOccurrenceId","r3a_fixture_"+i);Set(fact,"FactType","SharedMeal");Set(fact,"ActorPersonId",id);Set(fact,"TargetPersonId",i==0?target:"unknown_target");Set(fact,"Year",1);Set(fact,"Month",1);Set(fact,"Day",1);facts.SetValue(fact,i);}Set(dto,"RelationalFacts",facts);
        var pairs=Array.CreateInstance(rt,2);for(int i=0;i<2;i++){var r=Activator.CreateInstance(rt);Set(r,"SourcePersonId",i==0?id:target);Set(r,"TargetPersonId",i==0?target:id);Set(r,"Affinity",i==0?10f:-2f);pairs.SetValue(r,i);}Set(dto,"Relationships",pairs);Call("SocialRuntimeBridge","Restore",dto);
        var read=Read(id);var rows=((IEnumerable)Get(read,"Relations")).Cast<object>().ToArray();Assert.AreEqual(2,rows.Length);var known=rows.Single(r=>(string)Get(r,"PersonId")==target);Assert.That((string)Get(known,"Text"),Does.Contain("10").And.Contain("-2"));Assert.IsTrue((bool)Get(known,"CanNavigate"));Assert.IsFalse((bool)Get(rows.Single(r=>(string)Get(r,"PersonId")=="unknown_target"),"CanNavigate"));Assert.That(Pages(read)[3],Does.Contain("neutral fallback"));
    }
}
}

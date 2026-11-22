using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using TMPro;

namespace R3A {
public class R3AGrowthTransactionPlayTests
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
    static object Get(object o,string n)=>o.GetType().GetField(n,Flags)?.GetValue(o) ?? o.GetType().GetProperty(n,Flags)?.GetValue(o);
    static void Set(object o,string n,object v){var f=o.GetType().GetField(n,Flags);if(f!=null)f.SetValue(o,v);else o.GetType().GetProperty(n,Flags).SetValue(o,v);}
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
    static object Static(string t,string n,params object[] a)=>T(t).GetMethod(n,Flags).Invoke(null,a);
    byte[] original; string savePath;
    [SetUp] public void Protect(){savePath=(string)Static("SaveSystem","GetSavePath");original=File.Exists(savePath)?File.ReadAllBytes(savePath):null;}
    [TearDown] public void Restore(){T("SaveSystem").GetField("ForceSaveFailureForTests").SetValue(null,null);if(original!=null)File.WriteAllBytes(savePath,original);else if(File.Exists(savePath))File.Delete(savePath);}
    void EnsurePlayableSave(){if(!(bool)Static("SaveSystem","HasSave"))Assert.IsTrue((bool)Static("SaveSystem","Save",Static("SaveSystem","NewGame")),"Create an isolated playable fixture only when preceding legacy tests left no save");}

    static object Find(string type)=>UnityEngine.Object.FindObjectsOfType(T(type),true).First();
    IEnumerator ContinueHome()
    {
        SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        EnsurePlayableSave();
        var menu=Find("MainMenuUI");Call(menu,"RefreshContinueButton");Assert.IsTrue(((Button)Get(menu,"btnContinue")).interactable);((Button)Get(menu,"btnContinue")).onClick.Invoke();yield return null;yield return null;
        Assert.IsTrue((bool)Get(Find("ProductHomeWorldHost"),"WorldVisible"));
    }
    [UnityTest] public IEnumerator PersonSurface_UsesOneSelection_LeavesClockRunning_AndBlocksWorld()
    {
        yield return ContinueHome();
        var host=Find("ProductHomeWorldHost");var shell=Get(host,"Shell");var view=Get(shell,"PersonDetail");var selection=Get(host,"Selection");
        var collection=Get(Find("TownUI"),"PlayerData");var people=((IList)Get(collection,"OwnedCharacters")).Cast<object>().ToArray();
        string id=(string)Get(people[0],"PersonId"),other=(string)Get(people[1],"PersonId");
        Call(selection,"SelectPerson",id);Call(shell,"OpenSurface",Enum.Parse(T("HomeSurface"),"Person"));yield return null;
        Assert.AreEqual(id,Get(view,"DisplayedPersonId"));Assert.IsTrue((bool)Get(shell,"IsPersonModalOpen"));
        Canvas.ForceUpdateCanvases();var viewRect=((Component)view).GetComponent<RectTransform>();Assert.Greater(viewRect.rect.width,1000);Assert.Greater(viewRect.lossyScale.x,.1f);var corners=new Vector3[4];((ScrollRect)Get(view,"detailScroll")).viewport.GetWorldCorners(corners);Assert.IsTrue(corners.All(c=>c.x>=0&&c.x<=Screen.width+1&&c.y>=0&&c.y<=Screen.height+1));
        var clock=T("VillageRuntimeBridge").GetField("Time").GetValue(null);long before=Convert.ToInt64(Get(Get(clock,"CurrentInstant"),"Minute"));
        yield return new WaitForSeconds(1.2f);long after=Convert.ToInt64(Get(Get(clock,"CurrentInstant"),"Minute"));Assert.Greater(after,before,"Person page must not pause simulation");
        Call(host,"HandleWorldClick",new Vector2(900,300));Assert.AreEqual(id,Get(selection,"Id"),"Modal must block world click");
        var townRoot=(GameObject)Get(Find("TownUI"),"canvasRoot");Assert.IsFalse(townRoot.GetComponent<CanvasGroup>().interactable);
        for(int i=0;i<5;i++){Call(view,"SelectTab",Enum.ToObject(T("PersonDetailTab"),i));Assert.IsNotEmpty((string)Get(view,"DisplayedText"));}
        Assert.IsTrue((bool)Call(view,"SelectPerson",other));Assert.AreEqual(other,Get(selection,"Id"));
        Set(people[0],"DisplayName","同名人物");Set(people[1],"DisplayName","同名人物");Call(view,"RefreshNow");
        ((TMP_InputField)Get(view,"search")).text="同名人物";yield return null;
        var roster=(RectTransform)Get(view,"rosterContent");Assert.AreEqual(2,roster.Cast<Transform>().Count(t=>t.gameObject.activeSelf));
        Call(shell,"TogglePause");var speed=Get(clock,"Speed");Call(view,"SelectTab",Enum.ToObject(T("PersonDetailTab"),1));Assert.AreEqual(speed,Get(clock,"Speed"));
        Call(shell,"CloseSurface");Assert.AreEqual(other,Get(selection,"Id"));Assert.IsTrue(townRoot.GetComponent<CanvasGroup>().interactable);Assert.AreEqual(speed,Get(clock,"Speed"));
    }

    [UnityTest] public IEnumerator HomeGrowth_PendingBlocksNavigation_SaveFailureIsReported_ContinuePreservesResult()
    {
        yield return ContinueHome();var host=Find("ProductHomeWorldHost");var shell=Get(host,"Shell");var view=Get(shell,"PersonDetail");var selection=Get(host,"Selection");
        var collection=Get(Find("TownUI"),"PlayerData");var people=((IList)Get(collection,"OwnedCharacters")).Cast<object>().ToArray();var person=people[0];
        var pp=Activator.CreateInstance(Type.GetType("ProfessionProgress, Profession.Runtime",true));
        Set(pp,"ProfessionId","profession_test_hunter");Set(pp,"ProfessionLevel",1);Set(pp,"ProfessionExp",10);Set(pp,"UnlockedPoolCardIds",new List<string>{"bcard_aim","bcard_sword","bcard_rally"});Set(person,"ActiveProfessionProgress",pp);
        string id=(string)Get(person,"PersonId");Call(selection,"SelectPerson",id);Call(shell,"OpenSurface",Enum.Parse(T("HomeSurface"),"Person"));Call(view,"SelectTab",Enum.Parse(T("PersonDetailTab"),"Growth"));
        Assert.IsTrue((bool)Call(view,"OpenGrowth"));var growth=Find("CharacterGrowthUI");Assert.AreEqual(id,Get(growth,"SelectedPersonId"));Call(growth,"OnLevelUpClick");yield return null;
        var offer=((IList)Get(growth,"_pendingOffer")).Cast<object>().ToArray();Assert.Greater(offer.Length,0);
        Assert.IsTrue(offer.All(c=>((IList)Get(pp,"UnlockedPoolCardIds")).Contains(Get(c,"CardId"))));Call(growth,"OnOfferPick",0);((RectTransform)Get(growth,"detailRoot")).Find("F_Back").GetComponent<Button>().onClick.Invoke();CollectionAssert.AreEqual(offer,((IList)Get(growth,"_pendingOffer")).Cast<object>().ToArray());
        Assert.IsFalse((bool)Call(view,"SelectPerson",Get(people[1],"PersonId")));Assert.IsFalse((bool)Call(view,"SelectTab",Enum.Parse(T("PersonDetailTab"),"Life")));
        Call(shell,"HandleEscape");Assert.IsTrue((bool)Get(growth,"IsOpen"));Call(shell,"CloseSurface");Assert.IsTrue((bool)Get(growth,"IsOpen"));Call(shell,"OpenSurface",Enum.Parse(T("HomeSurface"),"Management"));Assert.AreEqual("Person",Get(Get(shell,"Router"),"Surface").ToString());
        Call(growth,"Open");CollectionAssert.AreEqual(offer,((IList)Get(growth,"_pendingOffer")).Cast<object>().ToArray());Assert.AreEqual(2,Get(pp,"ProfessionLevel"));
        T("SaveSystem").GetField("ForceSaveFailureForTests").SetValue(null,new Func<bool>(()=>true));LogAssert.Expect(LogType.Warning,new Regex("Save forced to fail"));Call(growth,"OnOfferSkip");
        Assert.That(((TMP_Text)Get(growth,"infoText")).text,Does.Contain("保存未成功"));Assert.IsFalse((bool)Get(growth,"HasPendingChoice"));
        T("SaveSystem").GetField("ForceSaveFailureForTests").SetValue(null,null);Assert.IsTrue((bool)Call(view,"SaveCurrent"));Call(growth,"Hide");Assert.AreEqual("World",Get(Get(shell,"Router"),"Surface").ToString());
        var loaded=Static("SaveSystem","Load");Assert.NotNull(loaded);var restored=Call(loaded,"FindPersonByPersonId",id);Assert.AreEqual(2,Get(Get(restored,"ActiveProfessionProgress"),"ProfessionLevel"));
        Call(Find("TownUI"),"Initialize",loaded);Call(host,"RefreshNow");Call(shell,"RefreshNow");Assert.IsNull(Get(selection,"Id"),"Source replacement clears shared selection");
    }
}
}

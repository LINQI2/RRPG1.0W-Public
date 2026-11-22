using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RRPG.StaticConfig;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RRPG.Building.Tests
{
    public sealed class BuildingVisualReplacementPlayTests
    {
        private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
        private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        private static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
        private static object Static(string n,string m,params object[] a)=>T(n).GetMethod(m,S).Invoke(null,a);
        private static object Call(object o,string m,params object[] a)=>o.GetType().GetMethod(m,I).Invoke(o,a);
        private static object Get(object o,string n)=>o.GetType().GetProperty(n,I).GetValue(o);
        private string path;private byte[] original;
        [UnitySetUp] public IEnumerator Setup()
        {
            path=(string)Static("SaveSystem","GetSavePath");original=File.Exists(path)?File.ReadAllBytes(path):null;
            Static("VillageRuntimeBridge","ResetForTests");SceneManager.LoadScene("SampleScene");yield return null;yield return null;
            var player=Static("SaveSystem","NewGame");Assert.True((bool)Static("SaveSystem","Save",player));
            Call(UnityEngine.Object.FindObjectOfType(T("MainMenuUI"),true),"OnContinue");yield return null;yield return null;
            ((VillageTime)T("VillageRuntimeBridge").GetField("Time",S).GetValue(null)).Pause();
        }
        [TearDown] public void Cleanup()
        {
            Static("VillageRuntimeBridge","ResetForTests");StaticConfigRegistry.Reset();
            if(original!=null)File.WriteAllBytes(path,original);else if(File.Exists(path))File.Delete(path);
        }
        [UnityTest] public IEnumerator ExistingViewsRefreshBindingWithoutChangingWorldOrClickBounds()
        {
            var host=UnityEngine.Object.FindObjectOfType(T("ProductHomeWorldHost"),true);
            for(int i=0;i<2;i++) {object[] args={"building_farm",(float)(4+i*5),-4f,null,null};Assert.True((bool)Static("VillageRuntimeBridge","TryPlaceBuilding",args));}
            Call(host,"RefreshNow");var views=(IDictionary)Get(host,"BuildingViews");var selection=(HomeSelectionContext)Get(host,"Selection");
            var entities=views.Values.Cast<Component>().ToArray();Assert.AreEqual(2,entities.Length);
            var before=JsonUtility.ToJson(Static("VillageRuntimeBridge","BuildSaveData"));
            var positions=entities.Select(e=>e.transform.position).ToArray();var bounds=entities.Select(e=>e.GetComponent<BoxCollider2D>().size).ToArray();
            var sprites=entities.Select(e=>e.transform.Find("Body").GetComponent<SpriteRenderer>().sprite).ToArray();
            Assert.True(StaticConfigRegistry.TryGetVillagePresentation(out var db,out var diagnostic),diagnostic);
            var f=typeof(VillagePresentationDatabase).GetField("presentations",I);var list=(System.Collections.Generic.List<VillagePresentation>)f.GetValue(db);
            var originals=list.Select(e=>e.VisualAssetId).ToArray();
            try {
                foreach(var entry in list.Where(e=>e.PresentationKey.StartsWith("village.building.farm")))entry.VisualAssetId="va_village_farm_visual_b";
                Call(host,"RefreshNow");yield return null;
                Assert.AreEqual(before,JsonUtility.ToJson(Static("VillageRuntimeBridge","BuildSaveData")));
                for(int i=0;i<entities.Length;i++) {
                    Assert.AreSame(entities[i],views[Get(entities[i],"Identity")]);Assert.AreEqual(positions[i],entities[i].transform.position);Assert.AreEqual(bounds[i],entities[i].GetComponent<BoxCollider2D>().size);
                    Assert.AreNotSame(sprites[i],entities[i].transform.Find("Body").GetComponent<SpriteRenderer>().sprite);
                    Call(entities[i],"Click");Assert.AreEqual(Get(entities[i],"Identity"),selection.Id);Assert.True((bool)Call(host,"FocusSelected"));
                }
            } finally {for(int i=0;i<list.Count;i++)list[i].VisualAssetId=originals[i];Call(host,"RefreshNow");}
        }
    }
}

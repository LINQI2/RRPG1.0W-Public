using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RRPG.ProductHome.Tests
{
public sealed class VillageVisualPrototypePlayTests
{
    // Long-term technical contracts only: profile/domain separation and stable PlaceId anchors.
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
    private static object Static(string n,string m,params object[] a)=>T(n).GetMethod(m,S).Invoke(null,a);
    private static object Call(object o,string m,params object[] a)=>o.GetType().GetMethod(m,I).Invoke(o,a);
    private static object Get(object o,string n)=>o.GetType().GetProperty(n,I).GetValue(o);
    private static object Find(string n)=>UnityEngine.Object.FindObjectOfType(T(n),true);
    private object _host,_player;private string _path;private byte[] _original;
    private ProductHomeSnapshot Snapshot=>(ProductHomeSnapshot)Get(_host,"Snapshot");
    private VillageTime Clock=>(VillageTime)T("VillageRuntimeBridge").GetField("Time",S).GetValue(null);
    private string Domain()=>JsonUtility.ToJson(Static("SaveSystem","BuildSaveData",_player,Static("VillageRuntimeBridge","BuildSaveData")));
    [UnitySetUp] public IEnumerator Setup()
    {
        _path=(string)Static("SaveSystem","GetSavePath");_original=File.Exists(_path)?File.ReadAllBytes(_path):null;
        Static("VillageRuntimeBridge","ResetForTests");SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        _player=Static("SaveSystem","NewGame");
        Assert.True((bool)Static("SaveSystem","Save",_player));Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;
        _player=Get(Find("TownUI"),"PlayerData");Clock.Pause();_host=Find("ProductHomeWorldHost");Call(_host,"RefreshNow");
        Assert.False((bool)Get(_host,"VisualPrototypeEnabled"),"A whitebox remains the default");
    }
    [TearDown] public void Cleanup()
    {
        Static("VillageRuntimeBridge","ResetForTests");
        if(_original!=null)File.WriteAllBytes(_path,_original);else if(File.Exists(_path))File.Delete(_path);
    }
    [UnityTest] public IEnumerator ProfilesPreserveEntitiesFootprintsSelectionAndDomain()
    {
        foreach(var pair in new[]{new{d="building_farm",x=-8f},new{d="building_house",x=0f}})
        {object[] args={pair.d,pair.x,-4f,null,null};Assert.True((bool)Static("VillageRuntimeBridge","TryPlaceBuilding",args),(string)args[4]);}
        Call(_host,"RefreshNow");var buildings=(IDictionary)Get(_host,"BuildingViews");var entities=buildings.Values.Cast<Component>().ToArray();
        var colliders=entities.Select(x=>x.GetComponent<BoxCollider2D>()).ToArray();var positions=entities.Select(x=>x.transform.position).ToArray();
        var sizes=colliders.Select(x=>x.size).ToArray();
        var persons=((IDictionary)Get(_host,"PersonViews")).Values.Cast<Component>().ToArray();
        string domain=Domain();Call(_host,"SetVisualPrototype",true);yield return null;
        Assert.AreEqual(domain,Domain());CollectionAssert.AreEquivalent(entities,((IDictionary)Get(_host,"BuildingViews")).Values);
        CollectionAssert.AreEquivalent(persons,((IDictionary)Get(_host,"PersonViews")).Values);
        for(int i=0;i<entities.Length;i++){Assert.AreEqual(positions[i],entities[i].transform.position);Assert.AreSame(colliders[i],entities[i].GetComponent<BoxCollider2D>());Assert.AreEqual(sizes[i],colliders[i].size);Assert.True((bool)Get(entities[i],"ResolvedSharedAsset"));}
        var person=(Component)((IDictionary)Get(_host,"PersonViews"))[Snapshot.Persons[0].PersonId];var selection=(HomeSelectionContext)Get(_host,"Selection");
        person.transform.position=entities[0].transform.position+new Vector3(0,1,0);Call(person,"RefreshSelection");
        Assert.Less((int)Get(person,"DepthOrder"),(int)Get(entities[0],"DepthOrder"));
        selection.SelectPerson(Snapshot.Persons[0].PersonId);Call(person,"RefreshSelection");Assert.Greater((int)Get(person,"DepthOrder"),(int)Get(entities[0],"DepthOrder"));
        Assert.True((bool)Get(person,"LabelVisible"));selection.Clear();Call(person,"RefreshSelection");
        person.transform.position=entities[0].transform.position-new Vector3(0,1,0);Call(person,"RefreshSelection");Assert.Greater((int)Get(person,"DepthOrder"),(int)Get(entities[0],"DepthOrder"));
        Call(_host,"SetVisualPrototype",false);Assert.AreEqual(domain,Domain());
        CollectionAssert.AreEquivalent(persons,((IDictionary)Get(_host,"PersonViews")).Values);
        foreach(var view in persons)Assert.True((bool)Get(view,"LabelVisible"));
        yield return null;
        var build=Get(Get(_host,"Shell"),"Build");Call(build,"OpenCatalog");Assert.True((bool)Call(build,"Choose","building_house"));
        Call(build,"SetCandidate",new Vector2(0,0));Assert.True((bool)Get(build,"PreviewValid"));
        Call(_host,"SetVisualPrototype",true);Assert.True((bool)Get(build,"PreviewValid"));Assert.AreEqual(domain,Domain());
        Call(build,"SetCandidate",new Vector2(-8,-4));Assert.False((bool)Get(build,"PreviewValid"));
        Call(build,"Cancel");Assert.AreEqual(domain,Domain());
    }
    [UnityTest] public IEnumerator ActivityPlacesCannotReflowRegisteredWorkplaces()
    {
        Call(_host,"SetVisualPrototype",true);
        var actual=Snapshot;
        var stable=actual.Places.Where(p=>p.Kind!="ActivityPlace").ToArray();
        var baseline=new ProductHomeSnapshot(actual.Persons,stable,actual.Diagnostics,actual.Buildings);
        Call(_host,"Reconcile",baseline);
        var places=(IDictionary)Get(_host,"PlaceViews");
        var ids=new[]{"ws_farm_01","ws_kitchen_01","ws_test_01","ws_test_02"};
        var views=ids.Select(id=>(Component)places[id]).ToArray();
        var anchors=views.Select(v=>v.transform.position).ToArray();
        var sprites=views.Select(v=>v.transform.Find("Body").GetComponent<SpriteRenderer>().sprite).ToArray();
        string before=Domain();
        var tavern=new HomePlaceSnapshot("Tavern","Tavern","ActivityPlace","Actual Presence / Destination",null,
            new string[0],new string[0],"Read-only projection",true);
        var active=new ProductHomeSnapshot(actual.Persons,stable.Concat(new[]{tavern}),actual.Diagnostics,actual.Buildings);
        for(int cycle=0;cycle<3;cycle++)
        {
            Call(_host,"Reconcile",active);
            var tavernView=(Component)((IDictionary)Get(_host,"PlaceViews"))["Tavern"];
            var tavernAnchor=tavernView.transform.position;
            for(int i=0;i<ids.Length;i++)
            {
                Assert.AreSame(views[i],((IDictionary)Get(_host,"PlaceViews"))[ids[i]]);
                Assert.AreEqual(anchors[i],views[i].transform.position,ids[i]+" must not move when Tavern appears");
                Assert.AreSame(sprites[i],views[i].transform.Find("Body").GetComponent<SpriteRenderer>().sprite);
                Assert.True(views[i].transform.Find("Body").GetComponent<SpriteRenderer>().enabled);
            }
            Call(_host,"Reconcile",baseline);
            Call(_host,"Reconcile",active);
            Assert.AreEqual(tavernAnchor,((Component)((IDictionary)Get(_host,"PlaceViews"))["Tavern"]).transform.position);
        }
        Assert.AreEqual(before,Domain(),"presentation-only churn must not change simulation/save");
        Call(_host,"RefreshNow");
        Call(_host,"SetVisualPrototype",false);
        Call(_host,"SetVisualPrototype",true);
        for(int i=0;i<ids.Length;i++)Assert.AreEqual(anchors[i],views[i].transform.position,"B anchors survive A/B roundtrip");
        yield return null;
    }
}
}

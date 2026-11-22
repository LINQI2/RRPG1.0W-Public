using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored Person surface. Only repeat rows are generated; selection belongs to Home.</summary>
public sealed class PersonDetailView : MonoBehaviour
{
    public TMP_Text title,body,status;
    public TMP_InputField search;
    public Button close,growth,save;
    public Button[] tabs;
    public RectTransform rosterContent,relationContent;
    public ScrollRect rosterScroll,detailScroll;
    public PersonDetailTab ActiveTab { get; private set; }
    public string DisplayedPersonId => _host?.Selection.Kind==HomeSelectionKind.Person?_host.Selection.Id:null;
    public string DisplayedText => body?.text;
    public bool IsVisible => gameObject.activeSelf;
    private ProductHomeWorldHost _host;
    private HomeProductShell _shell;
    private PlayerCollection _source;
    private string _rosterKey,_relationsKey;
    private readonly Dictionary<string,Button> _rows=new Dictionary<string,Button>();
    public void Initialize(ProductHomeWorldHost host,HomeProductShell shell)
    {
        _host=host;_shell=shell;
        close.onClick.AddListener(shell.CloseSurface);
        search.onValueChanged.AddListener(_=>RefreshNow());
        growth.onClick.AddListener(()=>OpenGrowth());save.onClick.AddListener(()=>SaveCurrent());
        for(int i=0;i<tabs.Length;i++){int n=i;tabs[i].onClick.AddListener(()=>SelectTab((PersonDetailTab)n));}
        host.Selection.Changed+=SelectionChanged;
        gameObject.SetActive(false);
    }
    public bool SelectPerson(string id)
    {
        if(_shell.GrowthUI?.HasPendingChoice==true){status.text="请先完成成长选牌或明确跳过。";return false;}
        if(VillageRuntimeBridge.SimulationPersonSource?.FindPersonByPersonId(id)==null)return false;
        _host.Selection.SelectPerson(id);RefreshNow();return true;
    }
    public bool SelectTab(PersonDetailTab tab)
    {
        if(_shell.GrowthUI?.HasPendingChoice==true){status.text="请先完成成长选牌或明确跳过。";return false;}
        ActiveTab=tab;detailScroll.verticalNormalizedPosition=1;_relationsKey=null;RefreshNow();return true;
    }
    private void SelectionChanged(){_relationsKey=null;if(IsVisible)RefreshNow();}
    public void RefreshNow()
    {
        if(_host==null)return;
        var source=VillageRuntimeBridge.SimulationPersonSource;
        if(!ReferenceEquals(_source,source)){_source=source;search.SetTextWithoutNotify("");ActiveTab=PersonDetailTab.Overview;_rosterKey=null;_relationsKey=null;status.text="";}
        var roster=PersonDetailReadAdapter.Roster(source,search.text);
        string key=string.Join("|",roster.Select(id=>id+"="+source.FindPersonByPersonId(id).DisplayName));
        if(key!=_rosterKey)
        {
            _rosterKey=key;Clear(rosterContent);_rows.Clear();
            foreach(string id in roster)
            {
                var person=source.FindPersonByPersonId(id);string captured=id;
                var row=Row(rosterContent,"Person_"+id,person.DisplayName+" · "+(person is HeroInstance?"Hero":"人物")+"\n"+id,74,()=>SelectPerson(captured));
                _rows[id]=row;
            }
            if(roster.Count==0)Row(rosterContent,"EmptyRoster","未找到人物",60,null).interactable=false;
        }
        foreach(var pair in _rows)pair.Value.GetComponent<Image>().color=pair.Key==DisplayedPersonId?new Color(.28f,.43f,.36f,1):new Color(.14f,.24f,.24f,1);
        var snapshot=PersonDetailReadAdapter.Read(source,DisplayedPersonId,_host.Snapshot);
        title.text="人物 · "+snapshot.Name;
        body.text=snapshot.Pages[(int)ActiveTab];
        growth.gameObject.SetActive(ActiveTab==PersonDetailTab.Growth);growth.interactable=snapshot.CanOpenGrowth&&VillageRuntimeBridge.CanSaveWorld;
        save.interactable=source!=null&&VillageRuntimeBridge.CanSaveWorld&&_shell.GrowthUI?.HasPendingChoice!=true;
        for(int i=0;i<tabs.Length;i++)tabs[i].GetComponent<Image>().color=i==(int)ActiveTab?new Color(.28f,.43f,.36f,1):new Color(.14f,.24f,.24f,1);
        string relations=ActiveTab==PersonDetailTab.Relationships?string.Join("|",snapshot.Relations.Select(r=>r.PersonId+r.Text+r.Name+r.CanNavigate)):"";
        if(_relationsKey!=relations)
        {
            _relationsKey=relations;Clear(relationContent);
            if(ActiveTab==PersonDetailTab.Relationships)foreach(var r in snapshot.Relations)
            {
                string id=r.PersonId;var row=Row(relationContent,"Relation_"+id,r.Name+"\n"+r.Text,Mathf.Max(90,48+24*r.Text.Split('\n').Length),()=>SelectPerson(id));row.interactable=r.CanNavigate;
            }
        }
    }
    public bool OpenGrowth()
    {
        var source=VillageRuntimeBridge.SimulationPersonSource;
        var snapshot=PersonDetailReadAdapter.Read(source,DisplayedPersonId,_host.Snapshot);
        if(!snapshot.CanOpenGrowth||!VillageRuntimeBridge.CanSaveWorld)return false;
        var controller=_shell.GrowthUI; if(controller==null || !controller.OpenForPerson(source,DisplayedPersonId,()=>_shell.CloseSurface()))return false;
        controller.HomePersonSelected=id=>_host.Selection.SelectPerson(id);return true;
    }
    public bool SaveCurrent()
    {
        if(_shell.GrowthUI?.HasPendingChoice==true){status.text="请先完成选牌或明确跳过。";return false;}
        var source=VillageRuntimeBridge.SimulationPersonSource;
        bool saved=source!=null&&SaveSystem.Save(source);
        status.text=saved?"已保存当前世界":"保存未成功，内存变化仍保留，请稍后重试。";return saved;
    }
    static void Clear(RectTransform root){foreach(Transform child in root){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
    static Button Row(RectTransform parent,string name,string label,float height,Action action)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));go.transform.SetParent(parent,false);
        go.GetComponent<LayoutElement>().preferredHeight=height;go.GetComponent<Image>().color=new Color(.14f,.24f,.24f,1);
        var button=go.GetComponent<Button>();button.onClick.AddListener(()=>action?.Invoke());
        var text=HomeProductUI.Text(go.transform,"Label",Vector2.zero,Vector2.zero,Vector2.zero,label,20);
        var rt=text.rectTransform;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(12,6);rt.offsetMax=new Vector2(-12,-6);text.overflowMode=TextOverflowModes.Ellipsis;
        return button;
    }
    private void OnDestroy(){if(_host!=null)_host.Selection.Changed-=SelectionChanged;}
}

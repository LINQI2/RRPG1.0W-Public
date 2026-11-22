using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>B home shell inside the existing Town orchestration. Internal navigation is presentation only.</summary>
public sealed class HomeProductShell : MonoBehaviour
{
    public HomeProductRouter Router { get; }=new HomeProductRouter();
    public HomeVillageFeed Feed { get; private set; }
    public HomeBuildingPlacement Build { get; private set; }
    public IReadOnlyList<HomeAttentionItem> Attention { get; private set; }
    public bool ShellVisible => _canvas!=null && _canvas.enabled;
    public bool CompatibilityVisible { get; private set; }
    public string HudText => _date.text+" "+_speed.GetComponentInChildren<TMP_Text>().text+" "+_resources.text;
    private ProductHomeWorldHost _host;
    private TownUI _town;
    private Canvas _canvas;
    private TMP_Text _date,_resources,_feed,_surfaceTitle,_surfaceBody,_attentionText;
    private Button _pause,_speed,_attentionButton;
    private GameObject _surface,_attentionPanel;
    private PlayerCollection _source;
    private float _nextRead;
    private readonly List<Button> _navigation=new List<Button>();
    private static readonly string[] LegacyNames={"InfoText","GoldText","CharCountText","ItemCountText","BtnViewDeck","BtnExpedition","VillageHUD","BtnCharacterGrowth","BtnVillageCommitment"};

    public void Initialize(ProductHomeWorldHost host)
    {
        _host=host;_town=FindObjectOfType<TownUI>(true);Feed=new HomeVillageFeed();Attention=System.Array.AsReadOnly(new HomeAttentionItem[0]);
        _canvas=gameObject.AddComponent<Canvas>();_canvas.renderMode=RenderMode.ScreenSpaceOverlay;_canvas.sortingOrder=70;
        gameObject.AddComponent<GraphicRaycaster>();var scaler=gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var top=HomeProductUI.Panel(transform,"TopHUD",new Vector2(.5f,1),new Vector2(0,-16),new Vector2(1872,62),new Color(.03f,.065f,.075f,.87f));
        _date=HomeProductUI.Text(top,"DateClock",new Vector2(0,1),new Vector2(18,-17),new Vector2(360,36),"");
        _pause=HomeProductUI.Button(top,"Pause",new Vector2(0,1),new Vector2(385,-11),new Vector2(94,40),"暂停",TogglePause);
        _speed=HomeProductUI.Button(top,"CurrentSpeed",new Vector2(0,1),new Vector2(490,-11),new Vector2(100,40),"1×",CycleSpeed);
        _resources=HomeProductUI.Text(top,"Resources",new Vector2(1,1),new Vector2(-180,-17),new Vector2(400,36),"");_resources.alignment=TextAlignmentOptions.TopRight;
        _attentionButton=HomeProductUI.Button(top,"Attention",new Vector2(1,1),new Vector2(-14,-11),new Vector2(150,40),"关注 0",()=>_attentionPanel.SetActive(!_attentionPanel.activeSelf));
        _attentionPanel=HomeProductUI.Panel(transform,"AttentionPanel",Vector2.one,new Vector2(-24,-88),new Vector2(370,185),new Color(.035f,.075f,.095f,.96f)).gameObject;
        _attentionText=HomeProductUI.Text(_attentionPanel.transform,"ActionFacts",new Vector2(0,1),new Vector2(16,-14),new Vector2(338,105),"");
        HomeProductUI.Button(_attentionPanel.transform,"NavigateAttention",new Vector2(0,0),new Vector2(16,14),new Vector2(338,40),"查看下一项",NavigateNextAttention);
        _attentionPanel.SetActive(false);
        var nav=HomeProductUI.Rect(transform,"ProductNavigation",new Vector2(.5f,0),new Vector2(0,24),new Vector2(1080,50));
        string[] labels={"村庄","人物","管理","卡牌","历史","远征 ↗","建造"};
        for(int i=0;i<labels.Length;i++)
        {
            int index=i;
            var button=HomeProductUI.Button(nav,"Navigate"+i,Vector2.zero,new Vector2(i*155,0),new Vector2(147,50),labels[i],()=>Navigate(index));
            _navigation.Add(button);
        }
        HomeProductUI.Button(transform,"DebugCompatibility",Vector2.zero,new Vector2(24,24),new Vector2(155,38),"调试 / 对照",ToggleCompatibility);
        HomeProductUI.Button(transform,"R2VisualProfile",new Vector2(1,0),new Vector2(-24,24),new Vector2(220,38),"R2 对照：A 白模",()=>{
            _host.SetVisualPrototype(!_host.VisualPrototypeEnabled);
            transform.Find("R2VisualProfile").GetComponentInChildren<TMP_Text>().text=_host.VisualPrototypeEnabled ? "R2 对照：B 原型" : "R2 对照：A 白模";
        });
        HomeProductUI.Text(transform,"R2PrototypeNotice",new Vector2(1,0),new Vector2(-24,68),new Vector2(320,30),"TECHNICAL PROTOTYPE / NOT FINAL ART",13).alignment=TextAlignmentOptions.Right;
        _feed=HomeProductUI.Text(transform,"VillageFeed",Vector2.zero,new Vector2(24,98),new Vector2(525,108),"",19);
        _surface=HomeProductUI.Panel(transform,"HomeInternalWhitebox",new Vector2(0,1),new Vector2(24,-105),new Vector2(620,700),new Color(.035f,.07f,.085f,.94f)).gameObject;
        _surfaceTitle=HomeProductUI.Text(_surface.transform,"Title",new Vector2(0,1),new Vector2(24,-24),new Vector2(490,44),"",28);
        HomeProductUI.Button(_surface.transform,"CloseToWorld",Vector2.one,new Vector2(-18,-18),new Vector2(70,40),"关闭",CloseSurface);
        _surfaceBody=HomeProductUI.Text(_surface.transform,"WhiteboxContent",new Vector2(0,1),new Vector2(24,-95),new Vector2(572,540),"",22);
        _surface.SetActive(false);
        Build=gameObject.AddComponent<HomeBuildingPlacement>();Build.Initialize(host);
    }
    private void LateUpdate()
    {
        if(_host==null) return;
        _town=FindObjectOfType<TownUI>(true);
        _canvas.enabled=_host.WorldVisible && VillageRuntimeBridge.IsSimulationActive;
        RetirePrototype();
        if(!ShellVisible) return;
        if(Input.GetKeyDown(KeyCode.Escape) && !Build.IsOpen) CloseSurface();
        if(Time.unscaledTime>=_nextRead) { _nextRead=Time.unscaledTime+.2f;RefreshNow(); }
    }
    private void RetirePrototype()
    {
        if(_town?.canvasRoot==null) return;
        foreach(string name in LegacyNames)
        {
            var child=_town.canvasRoot.transform.Find(name);
            if(child!=null && child.gameObject.activeSelf!=CompatibilityVisible) child.gameObject.SetActive(CompatibilityVisible);
        }
    }
    public void RefreshNow()
    {
        transform.Find("R2VisualProfile").GetComponentInChildren<TMP_Text>().text=_host.VisualPrototypeEnabled ? "R2 对照：B 原型" : "R2 对照：A 白模";
        if(!ReferenceEquals(_source,VillageRuntimeBridge.SimulationPersonSource))
        { _source=VillageRuntimeBridge.SimulationPersonSource;Feed.ResetSession();Router.Close();_attentionPanel.SetActive(false); }
        Attention=HomeAttentionProjection.Read();
        var clock=VillageRuntimeBridge.Time;var resources=VillageRuntimeBridge.Resources;
        _date.text=clock==null ? "—" : $"{clock.Year}年 {clock.Month}月 {clock.Day}日   {clock.MinuteOfDay/60:00}:{clock.MinuteOfDay%60:00}";
        _pause.GetComponentInChildren<TMP_Text>().text=clock?.Speed==VillageTime.VillageTimeSpeed.Paused ? "继续" : "暂停";
        _speed.GetComponentInChildren<TMP_Text>().text=(clock?.SpeedMultiplier ?? 0).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"×";
        _resources.text=$"食物 {resources?.Food ?? 0}     食材 {resources?.Ingredients ?? 0}";
        _attentionButton.GetComponentInChildren<TMP_Text>().text="关注 "+Attention.Count;
        _attentionText.text=Attention.Count==0 ? "当前没有待处理事项" : string.Join("\n",Attention.Take(3).Select(a=>a.Subject+" · "+a.Message));
        _feed.text=string.Join("\n",Feed.Recent.Select(e=>"· "+e.Message));
        var subjects=new HashSet<string>(Attention.Where(a=>a.Intent==HomeAttentionIntent.PersonGrowth).Select(a=>a.SubjectId));
        foreach(var view in _host.PersonViews) view.Value.SetAttention(subjects.Contains(view.Key));
        _surface.SetActive(Router.Surface!=HomeSurface.World);
        _host.Inspector.SetVisible(_host.WorldVisible && Router.Surface==HomeSurface.World);
        foreach(var pair in _navigation.Select((button,index)=>new{button,index}))
            pair.button.GetComponent<Image>().color=(int)Router.Surface==pair.index ? new Color(.28f,.43f,.36f,.96f) : new Color(.14f,.24f,.24f,.94f);
        RefreshSurface();
    }
    public void OpenSurface(HomeSurface surface) { Build?.Cancel();Router.Open(surface);RefreshNow(); }
    public void CloseSurface() { Router.Close();RefreshNow(); }
    public void TogglePause()
    { var clock=VillageRuntimeBridge.Time;if(clock==null) return;if(clock.Speed==VillageTime.VillageTimeSpeed.Paused) clock.Resume();else clock.Pause();RefreshNow(); }
    public void CycleSpeed() { VillageRuntimeBridge.Time?.CycleSpeed();RefreshNow(); }
    private void Navigate(int index)
    {
        if(index==6) {Build.OpenCatalog();return;}
        Build.Cancel();
        if(index<=4) OpenSurface((HomeSurface)index);
        else if(index==5)
        {
            Router.Close();
            if(_town==null) _town=FindObjectOfType<TownUI>(true);
            _town?.EnterProductExpedition();
            _canvas.enabled=VillageRuntimeBridge.IsSimulationActive;
        }
    }
    public void ToggleCompatibility()
    { CompatibilityVisible=!CompatibilityVisible;_host.ToggleDebug();RetirePrototype(); }
    public void NavigateAttention(HomeAttentionItem item)
    {
        if(item==null || !Attention.Any(a=>a.SubjectId==item.SubjectId && a.Intent==item.Intent)) return;
        if(item.Intent==HomeAttentionIntent.PersonGrowth) { _host.Selection.SelectPerson(item.Target);OpenSurface(HomeSurface.Person); }
        else FindObjectOfType<VillageSocialEventUI>(true)?.ShowAdvancePending(item.Target);
    }
    private int _attentionCursor;
    private void NavigateNextAttention()
    { RefreshNow();if(Attention.Count>0) NavigateAttention(Attention[_attentionCursor++%Attention.Count]); }
    private void RefreshSurface()
    {
        var snapshot=_host.Snapshot;if(snapshot==null || Router.Surface==HomeSurface.World) return;
        const string note="产品白模 · 只读框架\n村庄持续运行，可使用顶部时间控制。\n\n";
        switch(Router.Surface)
        {
            case HomeSurface.Person:
                _surfaceTitle.text="人物";
                var p=snapshot.Person(_host.Selection.Id);
                _surfaceBody.text=note+(p==null ? "从世界选择一位人物，即可在同一 Home 查看。\n\n"+string.Join("、",snapshot.Persons.Select(v=>v.DisplayName)) : p.DisplayName+"\n\n职业："+p.Profession+"\n活动："+p.ActivitySummary+"\n职责："+p.AssignmentSummary+"\n\n概览 / 生活 / 职业成长 / 关系 / 人生经历\n完整页面待后续实现。");break;
            case HomeSurface.Management:
                _surfaceTitle.text="管理";_surfaceBody.text=note+"人口 / 职责与场所 / 资源\n\n"+snapshot.Persons.Count+" 位人物 · "+snapshot.Places.Count+" 个地点\n\n"+_resources.text+"\n\n集中调度页面待后续实现。";break;
            case HomeSurface.Cards:
                _surfaceTitle.text="卡牌";_surfaceBody.text=note+"角色牌 / 通用牌\n\n职业理论牌池 / 人物已解锁牌池\n\n正式卡牌浏览待后续实现。";break;
            case HomeSurface.History:
                _surfaceTitle.text="历史";_surfaceBody.text=note+"编年史 / 人物档案 / 村庄历史\n\n长期历史查询页面待后续实现。\n左下近期动态仅属于当前会话。";break;
        }
    }
    private void OnDestroy() { Feed?.Dispose(); }
}

using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class VillageWorldLayer
{
    private Canvas _onsiteCanvas;
    private GameObject _onsitePanel;
    private TMP_Text _onsiteText;
    private TMP_Text _onsiteDetail;
    private string _selectedPerson;
    private int _onsitePage;
    private readonly Button[] _personButtons=new Button[6];
    public bool OnsitePanelVisible => _onsitePanel!=null && _onsitePanel.activeSelf && _onsiteCanvas.enabled;
    public string OnsiteSummary => _onsiteText==null ? "" : _onsiteText.text;
    public void SetOnsiteVisible(bool visible) { if(_onsitePanel!=null) _onsitePanel.SetActive(visible); }
    private static RectTransform OnsiteRect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private TMP_Text OnsiteLabel(string name,Transform parent,Vector2 position,Vector2 size,int fontSize)
    {
        var rect=OnsiteRect(name,parent,position,size);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=_font;label.fontSize=fontSize;label.color=Color.white;label.raycastTarget=false;
        label.enableWordWrapping=true;return label;
    }
    private Button OnsiteButton(string name,string text,Transform parent,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var rect=OnsiteRect(name,parent,position,size);var image=rect.gameObject.AddComponent<Image>();
        image.color=new Color(.13f,.22f,.25f,.98f);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var label=OnsiteLabel("Label",rect,Vector2.zero,size,18);label.alignment=TextAlignmentOptions.Center;label.text=text;
        button.onClick.AddListener(action);return button;
    }
    private void BuildOnsite()
    {
        var root=new GameObject("IntradayOnsiteCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);_onsiteCanvas=root.GetComponent<Canvas>();
        _onsiteCanvas.renderMode=RenderMode.ScreenSpaceOverlay;_onsiteCanvas.sortingOrder=200;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        OnsiteButton("OpenOnsite","现场",root.transform,new Vector2(-330,-94),new Vector2(110,42),()=>SetOnsiteVisible(true));
        var panel=OnsiteRect("OnsitePanel",root.transform,new Vector2(-24,-148),new Vector2(650,600));
        _onsitePanel=panel.gameObject;panel.gameObject.AddComponent<Image>().color=new Color(.055f,.09f,.105f,1f);
        _onsiteText=OnsiteLabel("OnsiteClock",panel,new Vector2(-16,-12),new Vector2(600,52),22);
        OnsiteButton("CloseOnsite","关闭",panel,new Vector2(-16,-64),new Vector2(90,36),()=>SetOnsiteVisible(false));
        OnsiteButton("PreviousPage","上一页",panel,new Vector2(-120,-64),new Vector2(100,36),()=>_onsitePage=Math.Max(0,_onsitePage-1));
        OnsiteButton("NextPage","下一页",panel,new Vector2(-230,-64),new Vector2(100,36),()=>_onsitePage++);
        for(int i=0;i<_personButtons.Length;i++)
        {
            int row=i;
            _personButtons[i]=OnsiteButton("OnsitePerson"+i,"",panel,new Vector2(-16,-110-i*54),new Vector2(618,48),()=>SelectOnsitePerson(row));
        }
        _onsiteDetail=OnsiteLabel("OnsiteDetail",panel,new Vector2(-16,-442),new Vector2(600,94),18);
        OnsiteButton("LeaveOnsiteActivity","结束本人参与",panel,new Vector2(-16,-550),new Vector2(190,36),()=>
        {
            if(_selectedPerson==null) return;
            var result=VillageRuntimeBridge.TryLeaveIntradayActivity(_selectedPerson);
            _onsiteDetail.text=result==RRPG.AutonomousActivity.ActivityResult.Success ? "已结束本人参与，其他人在场活动继续。"
                : result==RRPG.AutonomousActivity.ActivityResult.MissingInstance ? "当前没有本人发起的活动。" : "当前无法结束参与，请先完成等待中的操作。";
        });
        _onsitePanel.SetActive(false);
    }
    private string[] OnsitePersons() => VillageRuntimeBridge.SimulationPersonSource==null ? new string[0]
        : VillageRuntimeBridge.BuildPersonInputs(VillageRuntimeBridge.SimulationPersonSource).Where(p=>p!=null && !string.IsNullOrWhiteSpace(p.PersonId))
            .Select(p=>p.PersonId).Distinct(StringComparer.Ordinal).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    private string OnsiteName(string id) => VillageRuntimeBridge.SimulationPersonSource==null ? "未知人物"
        : VillageRuntimeBridge.BuildPersonInputs(VillageRuntimeBridge.SimulationPersonSource).FirstOrDefault(p=>p.PersonId==id)?.DisplayName ?? "未知人物";
    private string OnsitePlace(string place)
    {
        if(string.IsNullOrEmpty(place)) return "途中 / 不在村";
        if(place.StartsWith("Home:",StringComparison.Ordinal)) return OnsiteName(place.Substring(5))+"的家";
        if(place=="Home") return "住宅";if(place=="Tavern") return "酒馆";if(place=="Workshop") return "工坊";
        var project=VillageRuntimeBridge.Projects?.ActiveProjects.FirstOrDefault(p=>p.ProjectId==place);
        if(project!=null) return VillageRuntimeBridge.Projects.GetProjectDef(project.ProjectDefId)?.DisplayName ?? "项目工地";
        return VillageRuntimeBridge.TestWorkplaceDefs.FirstOrDefault(p=>p.WorkplaceId==place)?.DisplayName ?? "村中地点";
    }
    private static string OnsiteState(string state)
    {
        switch(state) { case "Rest": return "休息";case "Drink":return "饮酒";case "Talk":return "交谈";
            case "VisitSick":return "探病";case "Help":return "帮助";default:return state; }
    }
    private void SelectOnsitePerson(int row)
    {
        var persons=OnsitePersons();int index=_onsitePage*6+row;if(index>=persons.Length) return;
        _selectedPerson=persons[index];var state=ActivityRuntimeBridge.QueryPersonActivityState(_selectedPerson);
        var presence=VillageRuntimeBridge.QueryIntradayPresence(_selectedPerson);
        var together=OnsitePersons().Where(id=>presence.PlaceId!=null && VillageRuntimeBridge.QueryIntradayPresence(id).PlaceId==presence.PlaceId).Select(OnsiteName);
        _onsiteDetail.text=OnsiteName(_selectedPerson)+"\n已知住宅："+OnsitePlace(VillageRuntimeBridge.ReceptionPolicy.KnownHome(_selectedPerson))
            +"\n同址人物："+string.Join("、",together)
            +(state.LastMissedMinute.HasValue ? "\n最近扑空："+OnsiteState(state.LastMissedActivityType)+" → "+OnsiteName(state.LastMissedTargetPersonId) : "");
    }
    private void RefreshOnsite(bool visible)
    {
        if(_onsiteCanvas==null) BuildOnsite();_onsiteCanvas.enabled=visible && (_debugOverlayVisible || _onsitePanel.activeSelf);
        if(!visible || !_onsitePanel.activeSelf) return;
        var clock=VillageRuntimeBridge.Time;var persons=OnsitePersons();_onsitePage=Math.Min(_onsitePage,Math.Max(0,(persons.Length-1)/6));
        _onsiteText.text=$"现场（Prototype） {clock.Year}/{clock.Month}/{clock.Day} {clock.MinuteOfDay/60:00}:{clock.MinuteOfDay%60:00}  第{_onsitePage+1}页";
        for(int i=0;i<6;i++)
        {
            int index=_onsitePage*6+i;_personButtons[i].gameObject.SetActive(index<persons.Length);if(index>=persons.Length) continue;
            var presence=VillageRuntimeBridge.QueryIntradayPresence(persons[index]);
            _personButtons[i].GetComponentInChildren<TMP_Text>().text=OnsiteName(persons[index])+"  "+OnsiteState(presence.State)+"\n"+OnsitePlace(presence.PlaceId)
                +(presence.DestinationPlaceId==null ? "" : " → "+OnsitePlace(presence.DestinationPlaceId));
        }
    }
}

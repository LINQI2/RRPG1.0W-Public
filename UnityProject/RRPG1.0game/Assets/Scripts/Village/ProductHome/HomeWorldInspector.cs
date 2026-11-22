using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Selection snapshot with construction and workplace commands through the runtime bridge.</summary>
public sealed class HomeWorldInspector : MonoBehaviour
{
    public string DisplayedText { get; private set; }="";
    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _text;
    private HomeSelectionContext _selection;
    private UnityEngine.UI.Button _surfaceButton;
    private GameObject _workerRows;
    private string _workerKey;
    private string _commandDiagnostic="";
    public void SetSurfaceNavigation(System.Action open)
    { _surfaceButton=Button(_panel.transform,"OpenSurface","查看人物 / 管理",new Vector2(16,-260),new Vector2(298,36),open); }
    public void Initialize(HomeSelectionContext selection,System.Action focus,System.Action toggleDebug)
    {
        _selection=selection;
        _canvas=gameObject.AddComponent<Canvas>(); _canvas.renderMode=RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder=80;
        gameObject.AddComponent<GraphicRaycaster>();
        var scaler=gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        _panel=new GameObject("ReadonlyInspector",typeof(RectTransform),typeof(Image)); _panel.transform.SetParent(transform,false);
        var rt=_panel.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(1,0); rt.pivot=new Vector2(1,0);
        rt.anchoredPosition=new Vector2(-24,100); rt.sizeDelta=new Vector2(330,350);
        _panel.GetComponent<Image>().color=new Color(.035f,.075f,.095f,.95f);
        _text=Text(_panel.transform,"Details",new Vector2(16,-16),new Vector2(298,235));
        _text.alignment=TextAlignmentOptions.TopLeft;
        _workerRows=new GameObject("BuildingCommands",typeof(RectTransform));_workerRows.transform.SetParent(_panel.transform,false);
        var rowsRt=_workerRows.GetComponent<RectTransform>();rowsRt.anchorMin=rowsRt.anchorMax=new Vector2(0,1);rowsRt.pivot=new Vector2(0,1);
        rowsRt.anchoredPosition=new Vector2(0,-190);rowsRt.sizeDelta=new Vector2(330,180);
        Button(_panel.transform,"Focus","聚焦",new Vector2(16,-302),new Vector2(140,36),focus);
        Button(_panel.transform,"Clear","取消选择",new Vector2(174,-302),new Vector2(140,36),selection.Clear);
        _panel.SetActive(false);
    }
    public void SetVisible(bool visible) { if (_canvas!=null) _canvas.enabled=visible; }
    public void Refresh(ProductHomeSnapshot snapshot)
    {
        if(snapshot==null) return;
        string next="";
        if (_selection.Kind==HomeSelectionKind.Person)
        {
            var p=snapshot.Person(_selection.Id);
            if (p!=null) next=p.DisplayName+"\n职业："+p.Profession+"\n活动："+p.ActivitySummary+
                "\n地点："+(p.Traveling ? "途中 · 尚未到达" : p.Away ? "离村 / 不可用" : PlaceName(snapshot,p.CurrentPlaceId))+
                (p.Traveling ? "\n目的地："+PlaceName(snapshot,p.DestinationPlaceId) : "")+
                "\n职责："+p.AssignmentSummary+"\n状态："+p.Availability+
                (string.IsNullOrEmpty(p.OccupancySummary) ? "" : "\n"+p.OccupancySummary);
        }
        else if (_selection.Kind==HomeSelectionKind.Place)
        {
            var p=snapshot.Place(_selection.Id);
            if (p!=null) next=p.DisplayName+"\n"+p.Status+"\n容量："+(p.Capacity.HasValue ? p.Capacity.Value.ToString() : "—")+
                "\n占用名额："+p.Occupancy+"\n现场人数："+p.OccupantPersonIds.Count+
                "\n现场人物："+string.Join("、",p.OccupantPersonIds.Select(id=>snapshot.Person(id)?.DisplayName ?? id));
        }
        else if(_selection.Kind==HomeSelectionKind.Building)
        {
            var b=snapshot.Building(_selection.Id);
            var instance=VillageRuntimeBridge.Buildings.Find(_selection.Id);
            var project=instance==null ? null : VillageRuntimeBridge.Projects?.GetActiveProject(instance.ConstructionProjectId);
            var def=project==null ? null : VillageRuntimeBridge.Projects.GetProjectDef(project.ProjectDefId);
            if(b!=null) next=b.DisplayName+"\n状态："+(b.LifecycleState==BuildingLifecycleState.UnderConstruction ? "施工中" : "已完成")
                +(b.LifecycleState==BuildingLifecycleState.UnderConstruction ? "\n进度："+(project?.Progress ?? 0)+" / "+(def?.TestProgressTarget ?? 10)
                    +"\n工人："+(project?.ParticipantPersonIds.Count ?? 0)+"（零人暂停）\n现场："+string.Join("、",snapshot.Persons.Where(p=>p.CurrentPlaceId==VillageRuntimeBridge.ConstructionPlaceId(b.InstanceId)).Select(p=>p.DisplayName))
                    : (VillageRuntimeBridge.Assignments.WorkplaceDefs.Any(w=>w.WorkplaceId.StartsWith("workplace:"+b.InstanceId+":")) ? "\n功能岗位已启用\n已有职责请从调试 / 对照的承诺入口调整。" : "\n该建筑没有岗位功能。"))+"\n"+_commandDiagnostic;
            RefreshBuildingCommands(snapshot,instance,project);
        }
        _workerRows.SetActive(_selection.Kind==HomeSelectionKind.Building);
        bool buildingSelected=_selection.Kind==HomeSelectionKind.Building;
        _panel.GetComponent<RectTransform>().sizeDelta=new Vector2(330,buildingSelected ? 490 : 350);
        _text.GetComponent<RectTransform>().sizeDelta=new Vector2(298,buildingSelected ? 170 : 235);
        foreach(string name in new[]{"Focus","Clear"}) _panel.transform.Find(name).GetComponent<RectTransform>().anchoredPosition=new Vector2(name=="Focus" ? 16 : 174,buildingSelected ? -442 : -302);
        DisplayedText=next; _panel.SetActive(next.Length>0);
        if(_surfaceButton!=null) _surfaceButton.gameObject.SetActive(_selection.Kind!=HomeSelectionKind.Building);
        if(_surfaceButton!=null) _surfaceButton.GetComponentInChildren<TMP_Text>().text=_selection.Kind==HomeSelectionKind.Person ? "查看人物 · 白模" : "打开管理 · 白模";
        if (_text.text!=next) _text.text=next;
    }
    private void RefreshBuildingCommands(ProductHomeSnapshot snapshot,BuildingInstance b,VillageProject project)
    {
        if(b==null) return;
        string key=b.InstanceId+":"+b.LifecycleState+":"+string.Join(",",project?.ParticipantPersonIds ?? new System.Collections.Generic.List<string>())
            +":"+string.Join(",",snapshot.Persons.Select(p=>p.PersonId+"="+p.AssignmentId+":"+p.Availability));
        if(key==_workerKey) return;
        _workerKey=key;
        foreach(Transform child in _workerRows.transform) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
        int row=0;
        foreach(var person in snapshot.Persons.Where(p=>!p.Away))
        {
            string id=person.PersonId;
            bool joined=project?.ParticipantPersonIds.Contains(id)==true;
            string buildingId=b.InstanceId;
            var workplace=VillageRuntimeBridge.Assignments.WorkplaceDefs.FirstOrDefault(d=>d.WorkplaceId.StartsWith("workplace:"+buildingId+":",System.StringComparison.Ordinal));
            if(b.LifecycleState==BuildingLifecycleState.Completed && (workplace==null || !string.IsNullOrEmpty(person.AssignmentId))) continue;
            Button(_workerRows.transform,"Worker_"+id,(b.LifecycleState==BuildingLifecycleState.Completed ? "分配 " : joined ? "撤出 " : "加入 ")+person.DisplayName,
                new Vector2(16+(row%2)*155,-(row/2)*38),new Vector2(145,34),()=>
                {
                    if(b.LifecycleState==BuildingLifecycleState.Completed) _commandDiagnostic=VillageRuntimeBridge.TryAssignPerson(id,workplace.WorkplaceId).ToString();
                    else _commandDiagnostic=(joined ? VillageRuntimeBridge.TryRemoveConstructionWorker(buildingId,id) : VillageRuntimeBridge.TryAddConstructionWorker(buildingId,id)).ToString();
                    _workerKey=null;
                });row++;
        }
    }
    private static string PlaceName(ProductHomeSnapshot snapshot,string id) => snapshot.Place(id)?.DisplayName ?? id ?? "—";
    private static TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1);
        rt.anchoredPosition=position; rt.sizeDelta=size;
        var text=go.GetComponent<TextMeshProUGUI>();text.font=CurrentUIFontAuthority.Default;text.fontSize=19;text.color=Color.white;
        text.raycastTarget=false; text.enableWordWrapping=true; text.overflowMode=TextOverflowModes.Ellipsis;
        return text;
    }
    private static UnityEngine.UI.Button Button(Transform parent,string name,string label,Vector2 position,Vector2 size,System.Action action)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(parent,false);
        var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;
        go.GetComponent<Image>().color=new Color(.16f,.3f,.34f);
        var button=go.GetComponent<UnityEngine.UI.Button>();button.onClick.AddListener(()=>action());
        var text=Text(go.transform,"Label",Vector2.zero,size);text.text=label;text.alignment=TextAlignmentOptions.Center;
        return button;
    }
}

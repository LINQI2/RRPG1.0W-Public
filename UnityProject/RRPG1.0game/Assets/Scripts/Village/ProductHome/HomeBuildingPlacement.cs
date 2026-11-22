using System.Collections.Generic;
using RRPG.StaticConfig;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The only conversion between saved village-local position and Home's presentation offset.</summary>
public static class BuildingWorldMapper
{
    public static Vector3 ToPresentation(Vector2 local) => ProductHomeWorldHost.WorldOrigin+new Vector3(local.x,local.y,0);
    public static Vector2 ToLocal(Vector3 presentation) => (Vector2)(presentation-ProductHomeWorldHost.WorldOrigin);
}

/// <summary>Transient catalog and ghost. No instance list, allocator, clock or save state.</summary>
public sealed class HomeBuildingPlacement : MonoBehaviour
{
    public bool IsOpen => _panel!=null && _panel.activeSelf;
    public string SelectedDefinitionId { get; private set; }
    public Vector2 Candidate { get; private set; }
    public string Diagnostic { get; private set; }="";
    public bool PreviewValid => SelectedDefinitionId!=null && _validation.Length==0;
    public bool GhostVisible => _ghost!=null && _ghost.gameObject.activeSelf;
    public IReadOnlyList<BuildingDefinition> Catalog => _definitions?.All;
    private ProductHomeWorldHost _host;
    private GameObject _panel,_rows;
    private TMP_Text _status;
    private Button _confirm;
    private SpriteRenderer _ghost;
    private GameObject _area;
    private HomePresentationResolver _resolver;
    private BuildingRegistry _definitions;
    private string _validation="";
    public void Initialize(ProductHomeWorldHost host)
    {
        _host=host;_resolver=new HomePresentationResolver();
        _panel=HomeProductUI.Panel(transform,"BuildingCatalog",new Vector2(0,1),new Vector2(24,-105),new Vector2(360,545),new Color(.035f,.07f,.085f,.96f)).gameObject;
        HomeProductUI.Text(_panel.transform,"Title",new Vector2(0,1),new Vector2(18,-18),new Vector2(320,45),"建造 · C1 白模",26);
        HomeProductUI.Text(_panel.transform,"Instructions",new Vector2(0,1),new Vector2(18,-65),new Vector2(320,78),"选择建筑后，点击世界放置。\n绿色可放置；红色不可放置。\n右键 / Esc 取消，滚轮缩放。",17);
        _rows=HomeProductUI.Rect(_panel.transform,"CatalogRows",new Vector2(0,1),new Vector2(18,-145),new Vector2(324,200)).gameObject;
        _status=HomeProductUI.Text(_panel.transform,"PlacementStatus",new Vector2(0,1),new Vector2(18,-355),new Vector2(324,78),"",18);
        _confirm=HomeProductUI.Button(_panel.transform,"ConfirmPlacement",new Vector2(0,0),new Vector2(18,64),new Vector2(154,40),"确认落地",()=>Confirm());
        HomeProductUI.Button(_panel.transform,"CancelPlacement",new Vector2(0,0),new Vector2(184,64),new Vector2(154,40),"取消 / 返回",Cancel);
        HomeProductUI.Button(_panel.transform,"SaveVillage",new Vector2(0,0),new Vector2(18,14),new Vector2(320,40),"保存村庄",()=>SaveVillage());
        _panel.SetActive(false);
        var ghost=new GameObject("BuildingPlacementGhost",typeof(SpriteRenderer));ghost.transform.SetParent(host.transform,false);ghost.layer=ProductHomeWorldHost.WorldLayer;
        _ghost=ghost.GetComponent<SpriteRenderer>();_resolver.TryResolve("village.building.ghost",out var sprite,out _);_ghost.sprite=sprite;_ghost.sortingOrder=5;ghost.SetActive(false);
        HomeBuildingFootprint.Create(ghost.transform,1,1,new Color(.65f,.85f,.7f,.8f),6);
        _area=new GameObject("BuildableAreaWhitebox",typeof(LineRenderer));_area.transform.SetParent(host.transform,false);_area.layer=ProductHomeWorldHost.WorldLayer;
        var line=_area.GetComponent<LineRenderer>();line.useWorldSpace=true;line.loop=true;line.positionCount=4;line.startWidth=line.endWidth=.035f;
        line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=new Color(.4f,.62f,.62f,.7f);line.sortingOrder=1;
        line.SetPositions(new[]{BuildingWorldMapper.ToPresentation(new Vector2(BuildingPlacementValidator.MinX,BuildingPlacementValidator.MinY)),BuildingWorldMapper.ToPresentation(new Vector2(BuildingPlacementValidator.MaxX,BuildingPlacementValidator.MinY)),BuildingWorldMapper.ToPresentation(new Vector2(BuildingPlacementValidator.MaxX,BuildingPlacementValidator.MaxY)),BuildingWorldMapper.ToPresentation(new Vector2(BuildingPlacementValidator.MinX,BuildingPlacementValidator.MaxY))});_area.SetActive(false);
    }
    public void OpenCatalog()
    {
        Cancel();_host.Shell.CloseSurface();_panel.SetActive(true);_area.SetActive(true);
        foreach(Transform child in _rows.transform) Destroy(child.gameObject);
        if(!StaticConfigRegistry.TryGetBuildings(out _definitions,out string failure)) {Diagnostic=failure;RefreshStatus();return;}
        int row=0;
        foreach(var d in _definitions.All)
        {
            string id=d.BuildingDefinitionId;
            HomeProductUI.Button(_rows.transform,"Choose_"+id,new Vector2(0,1),new Vector2(0,-row++*48),new Vector2(324,40),d.DisplayName+"   "+d.FootprintWidth+" × "+d.FootprintDepth,()=>Choose(id));
        }
        Diagnostic="请选择建筑";RefreshStatus();
    }
    public bool Choose(string id)
    {
        if(!IsOpen || _definitions==null || !_definitions.TryGetById(id,out var d)) return false;
        SelectedDefinitionId=id;_ghost.transform.localScale=new Vector3(d.FootprintWidth,d.FootprintDepth,1);_ghost.gameObject.SetActive(true);
        SetCandidate(BuildingWorldMapper.ToLocal(_host.WorldCamera.ViewCamera.transform.position));return true;
    }
    public void SetCandidate(Vector2 candidate)
    {
        Candidate=candidate;
        Diagnostic=VillageRuntimeBridge.Buildings.Preview(_definitions,SelectedDefinitionId,candidate.x,candidate.y);
        _validation=Diagnostic;
        _ghost.transform.position=BuildingWorldMapper.ToPresentation(candidate);
        _resolver.PrototypeEnabled=_host.VisualPrototypeEnabled;
        if(_resolver.PrototypeEnabled && _definitions.TryGetById(SelectedDefinitionId,out var definition))
        {
            var preview=new HomeBuildingSnapshot("preview",definition.BuildingDefinitionId,definition.DisplayName,candidate.x,candidate.y,definition.FootprintWidth,definition.FootprintDepth,BuildingLifecycleState.UnderConstruction);
            _resolver.TryResolve(_resolver.PresentationKey(preview),out var sprite,out _);_ghost.sprite=sprite;
            var size=sprite.bounds.size;float scale=definition.FootprintWidth/Mathf.Max(size.x,.001f);
            _ghost.transform.localScale=new Vector3(scale,scale,1);
            _ghost.transform.position+=new Vector3(0,-definition.FootprintDepth*.5f+size.y*scale*.5f,0);
            var footprint=_ghost.GetComponentInChildren<LineRenderer>();
            HomeBuildingFootprint.SetSize(footprint,definition.FootprintWidth/scale,definition.FootprintDepth/scale);
            footprint.transform.localPosition=new Vector3(0,(definition.FootprintDepth*.5f-size.y*scale*.5f)/scale,0);
        }
        else if(_definitions.TryGetById(SelectedDefinitionId,out var baseline))
        {
            _resolver.TryResolve("village.building.ghost",out var sprite,out _);_ghost.sprite=sprite;
            _ghost.transform.localScale=new Vector3(baseline.FootprintWidth,baseline.FootprintDepth,1);
            var footprint=_ghost.GetComponentInChildren<LineRenderer>();HomeBuildingFootprint.SetSize(footprint,1,1);footprint.transform.localPosition=Vector3.zero;
        }
        _ghost.sortingOrder=20003;
        _ghost.color=PreviewValid ? new Color(.35f,.88f,.5f,.48f) : new Color(.95f,.25f,.22f,.55f);RefreshStatus();
    }
    public void ReadInput(bool overUI)
    {
        if(!IsOpen) return;
        if(Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) {Cancel();return;}
        if(SelectedDefinitionId==null || overUI) return;
        UpdatePointer(Input.mousePosition);
        if(Input.GetMouseButtonDown(0)) Confirm();
    }
    public void UpdatePointer(Vector2 screen)
    {
        if(!IsOpen || SelectedDefinitionId==null) return;
        var point=_host.WorldCamera.ViewCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
        SetCandidate(BuildingWorldMapper.ToLocal(point));
    }
    public bool Confirm()
    {
        if(!IsOpen || SelectedDefinitionId==null) return false;
        if(!VillageRuntimeBridge.TryPlaceBuilding(SelectedDefinitionId,Candidate.x,Candidate.y,out var instance,out string failure))
        {Diagnostic=failure;_validation=failure;_ghost.color=new Color(.95f,.25f,.22f,.55f);RefreshStatus();return false;}
        _host.Selection.SelectBuilding(instance.InstanceId);_host.RefreshNow();
        SelectedDefinitionId=null;_ghost.gameObject.SetActive(false);Diagnostic="已落地 · 施工中。可选择另一座建筑。";RefreshStatus();return true;
    }
    public bool SaveVillage()
    {
        var source=VillageRuntimeBridge.SimulationPersonSource;
        bool saved=source!=null && SaveSystem.Save(source);
        Diagnostic=saved ? "已保存 · 预览不保存。" : "当前不能保存，请等待结算结束。";RefreshStatus();return saved;
    }
    public void Cancel()
    {SelectedDefinitionId=null;Diagnostic="";if(_panel!=null) _panel.SetActive(false);if(_ghost!=null) _ghost.gameObject.SetActive(false);if(_area!=null) _area.SetActive(false);}
    private void RefreshStatus()
    {
        if(_status==null) return;
        _confirm.interactable=PreviewValid;
        _status.text=PreviewValid ? "可放置 · "+_definitions.All.FindName(SelectedDefinitionId)+"\n"+(Diagnostic.Length==0 ? "范围为白模测试参数。" : Diagnostic) : Diagnostic;
    }
    private void OnDestroy()
    {
        _resolver?.Dispose();if(_ghost!=null) {Destroy(_ghost.GetComponentInChildren<LineRenderer>().sharedMaterial);Destroy(_ghost.gameObject);}
        if(_area!=null) {Destroy(_area.GetComponent<LineRenderer>().sharedMaterial);Destroy(_area);}
    }
}
/// <summary>Exact rectangular footprint; marker Sprite remains a replaceable interior visual.</summary>
public static class HomeBuildingFootprint
{
    public static LineRenderer Create(Transform parent,float width,float depth,Color color,int order)
    {
        var go=new GameObject("FootprintOutline",typeof(LineRenderer));go.transform.SetParent(parent,false);go.layer=ProductHomeWorldHost.WorldLayer;
        var line=go.GetComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=4;line.startWidth=line.endWidth=.025f;
        line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=color;line.sortingOrder=order;
        SetSize(line,width,depth);return line;
    }
    public static void SetSize(LineRenderer line,float width,float depth)
    {line.SetPositions(new[]{new Vector3(-width*.5f,-depth*.5f,0),new Vector3(width*.5f,-depth*.5f,0),new Vector3(width*.5f,depth*.5f,0),new Vector3(-width*.5f,depth*.5f,0)});}
}
internal static class HomeBuildingDefinitionText
{
    internal static string FindName(this IReadOnlyList<BuildingDefinition> all,string id)
    {foreach(var d in all) if(d.BuildingDefinitionId==id) return d.DisplayName;return "";}
}

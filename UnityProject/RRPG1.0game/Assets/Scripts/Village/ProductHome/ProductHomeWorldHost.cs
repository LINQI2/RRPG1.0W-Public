using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Persistent product world coordinator; independent of TownCanvas and debug UI.</summary>
public sealed class ProductHomeWorldHost : MonoBehaviour
{
    public const int WorldLayer=31;
    public static readonly Vector3 WorldOrigin=new Vector3(10000,0,0);
    public HomeSelectionContext Selection { get; }=new HomeSelectionContext();
    public ProductHomeSnapshot Snapshot { get; private set; }
    public HomeWorldCamera WorldCamera { get; private set; }
    public HomeWorldInspector Inspector { get; private set; }
    public HomeProductShell Shell { get; private set; }
    public bool WorldVisible { get; private set; }
    public IReadOnlyDictionary<string,HomeWorldEntityView> PersonViews => _persons;
    public IReadOnlyDictionary<string,HomeWorldEntityView> PlaceViews => _places;
    public IReadOnlyDictionary<string,HomeWorldEntityView> BuildingViews => _buildings;
    private readonly Dictionary<string,HomeWorldEntityView> _buildings=new Dictionary<string,HomeWorldEntityView>();
    private readonly Dictionary<string,HomeWorldEntityView> _persons=new Dictionary<string,HomeWorldEntityView>();
    private readonly Dictionary<string,HomeWorldEntityView> _places=new Dictionary<string,HomeWorldEntityView>();
    private readonly Dictionary<string,Vector3> _anchors=new Dictionary<string,Vector3>();
    private readonly List<RaycastResult> _uiHits=new List<RaycastResult>();
    private Transform _worldRoot;
    private HomePresentationResolver _resolver;
    private float _nextRead;
    private bool _debugVisible;
    private PlayerCollection _presentationSource;
    public bool VisualPrototypeEnabled => _resolver!=null && _resolver.PrototypeEnabled;
    private HomeVisualPrototype _prototypeGround;
    private readonly Dictionary<string,Vector3> _baselineAnchors=new Dictionary<string,Vector3>();
    private readonly Dictionary<string,Vector3> _prototypeAnchors=new Dictionary<string,Vector3>();
    private int _prototypeHomeSlots,_prototypeOtherSlots;
    /// <summary>Reversible visual candidate. Reuses the existing entities and selection; never writes domain state.</summary>
    public void SetVisualPrototype(bool enabled)
    {
        if(VisualPrototypeEnabled==enabled) return;
        if(enabled) { _baselineAnchors.Clear();foreach(var pair in _anchors) _baselineAnchors[pair.Key]=pair.Value; }
        else { _anchors.Clear();foreach(var pair in _baselineAnchors) _anchors[pair.Key]=pair.Value; }
        _resolver.PrototypeEnabled=enabled;WorldCamera.SetPrototype(enabled);
        if(enabled && _prototypeGround==null)
        {
            var ground=new GameObject("R2 Technical Prototype Ground",typeof(HomeVisualPrototype));
            ground.transform.SetParent(_worldRoot,false);ground.transform.position=WorldOrigin;
            _prototypeGround=ground.GetComponent<HomeVisualPrototype>();_prototypeGround.Initialize();
        }
        if(_prototypeGround!=null) _prototypeGround.gameObject.SetActive(enabled);
        foreach(var view in _persons.Values.Concat(_places.Values).Concat(_buildings.Values)) view.SetVisualPrototype(enabled);
        RefreshNow();
        if(Shell.Build.IsOpen && Shell.Build.SelectedDefinitionId!=null) Shell.Build.SetCandidate(Shell.Build.Candidate);
    }
    private void Awake()
    {
        _resolver=new HomePresentationResolver();
        var root=new GameObject("PersistentWorldEntities");root.transform.SetParent(transform,false);_worldRoot=root.transform;
        var camera=new GameObject("ProductWorldCamera",typeof(HomeWorldCamera));camera.transform.SetParent(transform,false);
        WorldCamera=camera.GetComponent<HomeWorldCamera>();WorldCamera.Initialize();WorldCamera.ViewCamera.enabled=false;
        var inspector=new GameObject("ProductSelectionInspector",typeof(RectTransform),typeof(HomeWorldInspector));inspector.transform.SetParent(transform,false);
        Inspector=inspector.GetComponent<HomeWorldInspector>();Inspector.Initialize(Selection,()=>FocusSelected(),ToggleDebug);Inspector.SetVisible(false);
        var shell=new GameObject("ProductHomeShell",typeof(RectTransform),typeof(HomeProductShell));shell.transform.SetParent(transform,false);
        Shell=shell.GetComponent<HomeProductShell>();Shell.Initialize(this);
        Inspector.SetSurfaceNavigation(()=>Shell.OpenSurface(Selection.Kind==HomeSelectionKind.Person ? HomeSurface.Person : HomeSurface.Management));
    }
    private void Update()
    {
        WorldVisible=VillageRuntimeBridge.IsSimulationActive && VillageRuntimeBridge.SimulationPersonSource!=null && VillageRuntimeBridge.Time!=null;
        _worldRoot.gameObject.SetActive(WorldVisible);WorldCamera.ViewCamera.enabled=WorldVisible;Inspector.SetVisible(WorldVisible && Shell.Router.Surface==HomeSurface.World);
        var debug=FindObjectOfType<VillageWorldLayer>(true);
        if (debug!=null) debug.SetDebugOverlayVisible(!WorldVisible || _debugVisible);
        if (!WorldVisible) return;
        if (Time.unscaledTime>=_nextRead) { _nextRead=Time.unscaledTime+.1f;RefreshNow(); }
        StepPresentation(Time.deltaTime);
        RefreshLabelReadability();
        bool overUI=PointerOverUI(Input.mousePosition);WorldCamera.ReadInput(overUI,Shell.Build.IsOpen);
        if(Shell.Build.IsOpen) Shell.Build.ReadInput(overUI);
        HandleWorldHover(overUI ? (HomeWorldEntityView)null : HitAt(Input.mousePosition));
        if (!Shell.Build.IsOpen && !overUI && Input.GetMouseButtonDown(0)) HandleWorldClick(Input.mousePosition);
        Inspector.Refresh(Snapshot);
    }
    /// <summary>Follow the clock's rate without advancing it or declaring arrival.</summary>
    public void StepPresentation(float driverSeconds)
    {
        var clock=VillageRuntimeBridge.Time;
        if (!VillageRuntimeBridge.IsSimulationActive || clock==null || clock.HasActiveAdvance) return;
        float seconds=driverSeconds*(float)clock.SpeedMultiplier;
        foreach (var person in _persons.Values) person.StepPresentation(seconds);
    }
    public void RefreshNow()
    {
        if (!ReferenceEquals(_presentationSource,VillageRuntimeBridge.SimulationPersonSource))
        {
            _presentationSource=VillageRuntimeBridge.SimulationPersonSource;
            _anchors.Clear();_baselineAnchors.Clear();_prototypeAnchors.Clear();
            _prototypeHomeSlots=0;_prototypeOtherSlots=0;Selection.Clear();WorldCamera.Focus(WorldOrigin);
            Shell.Build.Cancel();
        }
        Reconcile(ProductHomeReadAdapter.Read());
    }
    private void Reconcile(ProductHomeSnapshot snapshot)
    {
        Snapshot=snapshot;
        RemoveMissing(_places,new HashSet<string>(Snapshot.Places.Select(p=>p.PlaceId)));
        RemoveMissing(_persons,new HashSet<string>(Snapshot.Persons.Select(p=>p.PersonId)));
        RemoveMissing(_buildings,new HashSet<string>(Snapshot.Buildings.Select(b=>b.InstanceId)));
        foreach(var b in Snapshot.Buildings)
        {
            if(!_buildings.TryGetValue(b.InstanceId,out var view))
            {view=Create(b.InstanceId,HomeSelectionKind.Building,_resolver.PresentationKey(b));_buildings.Add(b.InstanceId,view);}
            view.ApplyBuilding(b,_resolver);
        }
        // Registered sites claim their initial slots before transient activity projections.
        foreach (var place in Snapshot.Places.OrderBy(p=>VisualPrototypeEnabled && p.IsFallback).ThenBy(p=>p.PlaceId,System.StringComparer.Ordinal))
        {
            var building=Snapshot.Buildings.FirstOrDefault(b=>place.PlaceId==VillageRuntimeBridge.ConstructionPlaceId(b.InstanceId)
                || place.PlaceId.StartsWith("workplace:"+b.InstanceId+":",System.StringComparison.Ordinal));
            if(building!=null) _anchors[place.PlaceId]=BuildingWorldMapper.ToPresentation(new Vector2(building.WorldX,building.WorldY));
            else if(VisualPrototypeEnabled)
            {
                // Identity owns the session slot, even while an activity stops referencing it.
                // Never reflow existing buildings or people destinations on snapshot churn.
                if(!_prototypeAnchors.TryGetValue(place.PlaceId,out var anchor))
                {
                    int slot=place.Kind=="Home" ? _prototypeHomeSlots++ : _prototypeOtherSlots++;
                    anchor=WorldOrigin+(place.Kind=="Home" ? new Vector3(-8.5f+(slot%6)*3.4f,4.2f-(slot/6)*2.5f,0) : new Vector3(-6+(slot%4)*4,-1.2f-(slot/4)*2.5f,0));
                    _prototypeAnchors.Add(place.PlaceId,anchor);
                }
                _anchors[place.PlaceId]=anchor;
            }
            if (!_anchors.ContainsKey(place.PlaceId)) _anchors.Add(place.PlaceId,Layout(_anchors.Count));
            if (!_places.TryGetValue(place.PlaceId,out var view))
            {
                view=Create(place.PlaceId,HomeSelectionKind.Place,_resolver.PresentationKey(place));_places.Add(place.PlaceId,view);
            }
            view.RefreshVisual(_resolver.PresentationKey(place),_resolver);
            view.ApplyPlace(place,_anchors[place.PlaceId]);
            if(VisualPrototypeEnabled) view.SetSemanticPlace(building!=null);
        }
        var personSlots=new Dictionary<string,int>();
        foreach (var person in Snapshot.Persons.OrderBy(p=>p.PersonId,System.StringComparer.Ordinal))
        {
            if (!_persons.TryGetValue(person.PersonId,out var view))
            {
                view=Create(person.PersonId,HomeSelectionKind.Person,_resolver.PresentationKey(person));_persons.Add(person.PersonId,view);
            }
            string slotPlace=person.CurrentPlaceId ?? person.DestinationPlaceId ?? "";
            personSlots.TryGetValue(slotPlace,out int slot);personSlots[slotPlace]=slot+1;
            Vector3 offset=new Vector3((slot%3-1)*1.45f,-.95f-(slot/3)*.95f,-.1f);
            if(VisualPrototypeEnabled) offset=new Vector3((slot%3-1)*.95f,-.75f-(slot/3)*.7f,-.1f);
            view.RefreshVisual(_resolver.PresentationKey(person),_resolver);
            view.ApplyPerson(person,Anchor(person.CurrentPlaceId)+offset,Anchor(person.DestinationPlaceId)+offset);
        }
        if (Selection.Kind==HomeSelectionKind.Person && Snapshot.Person(Selection.Id)==null ||
            Selection.Kind==HomeSelectionKind.Place && Snapshot.Place(Selection.Id)==null || Selection.Kind==HomeSelectionKind.Building && Snapshot.Building(Selection.Id)==null) Selection.Clear();
        foreach (var view in _persons.Values.Concat(_places.Values).Concat(_buildings.Values)) view.RefreshSelection();
        Inspector.Refresh(Snapshot);
    }
    private HomeWorldEntityView Create(string id,HomeSelectionKind kind,string key)
    {
        var go=new GameObject(kind+"_"+id,typeof(HomeWorldEntityView));go.transform.SetParent(_worldRoot,false);
        var view=go.GetComponent<HomeWorldEntityView>();view.Initialize(id,kind,Selection,_resolver,key);
        view.SetVisualPrototype(VisualPrototypeEnabled);
        if (!view.ResolvedSharedAsset) Debug.LogWarning("[ProductHome] "+view.Diagnostic);
        return view;
    }
    private static void RemoveMissing(Dictionary<string,HomeWorldEntityView> views,HashSet<string> retained)
    {
        foreach (string id in views.Keys.Where(id=>!retained.Contains(id)).ToArray()) { Destroy(views[id].gameObject);views.Remove(id); }
    }
    private static Vector3 Layout(int index)
    {
        // Presentation-only diagonal whitebox profile. Never written into Registry/Save.
        int column=index%4,row=index/4;
        return WorldOrigin+new Vector3((column-1.5f)*4f+row*.6f,3.6f-row*2.7f,0);
    }
    private Vector3 Anchor(string id) => id!=null && _anchors.TryGetValue(id,out var point) ? point : WorldOrigin+new Vector3(0,-5.8f,0);
    private static Vector3 PersonOffset(string id)
    {
        uint hash=2166136261;foreach (char c in id) hash=(hash^c)*16777619;
        return new Vector3(((int)(hash%7)-3)*.32f,((int)(hash/7%3)-1)*.26f-.15f,-.1f);
    }
    public void HandleWorldClick(Vector2 screen)
    {
        if(PointerOverUI(screen)) return;
        var hit=HitAt(screen);
        if (hit==null) Selection.Clear();else hit.Click();
        RefreshNow();
    }
    private HomeWorldEntityView HitAt(Vector2 screen)
    {
        var point=WorldCamera.ViewCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));
        return Physics2D.OverlapPointAll(point,1<<WorldLayer).Select(c=>c.GetComponentInParent<HomeWorldEntityView>())
            .Where(v=>v!=null && v.isActiveAndEnabled).OrderByDescending(v=>v.Kind==HomeSelectionKind.Person ? 2 : v.Kind==HomeSelectionKind.Building ? 1 : 0).FirstOrDefault();
    }
    private void HandleWorldHover(HomeWorldEntityView hovered)
    { foreach(var view in _persons.Values.Concat(_places.Values).Concat(_buildings.Values)) view.SetHovered(view==hovered); }
    private void RefreshLabelReadability()
    {
        var labels=new List<Rect>();
        foreach(var view in _persons.Values.OrderByDescending(v=>Selection.Kind==HomeSelectionKind.Person && Selection.Id==v.Identity).ThenBy(v=>v.Identity,System.StringComparer.Ordinal))
        {
            var point=WorldCamera.ViewCamera.WorldToScreenPoint(view.transform.position+new Vector3(0,-.5f,0));
            var rect=new Rect(point.x-65,point.y-12,130,24);
            bool readable=!VisualPrototypeEnabled || WorldCamera.ViewCamera.orthographicSize<=6 && !labels.Any(r=>r.Overlaps(rect));
            view.SetLabelReadability(readable);
            if(view.LabelVisible) labels.Add(rect);
        }
    }
    public bool FocusSelected()
    {
        HomeWorldEntityView view=null;
        if (Selection.Kind==HomeSelectionKind.Person) _persons.TryGetValue(Selection.Id,out view);
        else if (Selection.Kind==HomeSelectionKind.Place) _places.TryGetValue(Selection.Id,out view);
        else if (Selection.Kind==HomeSelectionKind.Building) _buildings.TryGetValue(Selection.Id,out view);
        if (view==null || !view.PresentationVisible) return false;
        WorldCamera.Focus(view.transform.position);return true;
    }
    public void ToggleDebug() => _debugVisible=!_debugVisible;
    public bool PointerOverUI(Vector2 screen)
    {
        if (EventSystem.current==null) return false;
        _uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},_uiHits);
        return _uiHits.Count>0;
    }
    private void OnDestroy()
    {
        var debug=FindObjectOfType<VillageWorldLayer>(true);if (debug!=null) debug.SetDebugOverlayVisible(true);
        _resolver?.Dispose();
    }
}

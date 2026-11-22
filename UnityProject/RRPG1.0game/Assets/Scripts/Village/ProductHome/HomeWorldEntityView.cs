using TMPro;
using UnityEngine;

/// <summary>One persistent view per identity; visual position cannot declare simulation arrival.</summary>
public sealed class HomeWorldEntityView : MonoBehaviour
{
    public string Identity { get; private set; }
    public HomeSelectionKind Kind { get; private set; }
    public bool ResolvedSharedAsset { get; private set; }
    public string Diagnostic { get; private set; }
    public bool IsTraveling { get; private set; }
    public bool PresentationVisible => gameObject.activeSelf;
    private SpriteRenderer _body;
    private SpriteRenderer _ring;
    private TMP_Text _name;
    private TMP_Text _hoverText;
    private SpriteRenderer _attention;
    private bool _hovered;
    private string _hoverSummary;
    public bool LabelVisible => _name.gameObject.activeSelf;
    public string LabelText => _name.text;
    public bool HoverSummaryVisible => _hoverText.gameObject.activeSelf;
    public bool AttentionVisible => _attention.enabled;
    private HomeSelectionContext _selection;
    private Vector3 _target;
    private Vector3 _travelOrigin;
    private string _destination;
    private bool _positioned;
    private Color _normal;
    private LineRenderer _footprint;
    private bool _prototype;
    private bool _readableLabel=true;
    private BoxCollider2D _spriteHit;
    public int DepthOrder => _body.sortingOrder;
    public void SetLabelReadability(bool readable) { if(_readableLabel==readable) return;_readableLabel=readable;RefreshSelection(); }
    public void SetVisualPrototype(bool enabled)
    {
        _prototype=enabled;
        _body.transform.localPosition=Vector3.zero;
        _body.transform.localScale=Kind==HomeSelectionKind.Person ? new Vector3(.38f,.62f,1) : new Vector3(2.25f,1.25f,1);
        GetComponent<BoxCollider2D>().size=Kind==HomeSelectionKind.Person ? new Vector2(.65f,.8f) : new Vector2(2.1f,1.1f);
        GetComponent<BoxCollider2D>().offset=Vector2.zero;
        _name.transform.localPosition=new Vector3(0,Kind==HomeSelectionKind.Person ? -.5f : .88f,-.05f);
        _normal=Kind==HomeSelectionKind.Person ? new Color(.92f,.89f,.68f) : new Color(.28f,.52f,.45f);
        if(_spriteHit!=null) _spriteHit.enabled=enabled;
        RefreshSelection();
    }
    public void SetSemanticPlace(bool belongsToBuilding)
    {
        if(!_prototype) return;
        // A functional place stays clickable; it does not draw a second building on the same instance.
        _body.enabled=!belongsToBuilding;
    }
    public void RefreshVisual(string key,HomePresentationResolver resolver)
    {
        ResolvedSharedAsset=resolver.TryResolve(key,out var sprite,out var diagnostic);
        if(!ResolvedSharedAsset && Diagnostic!=diagnostic) Debug.LogWarning("[ProductHome] "+diagnostic);
        Diagnostic=diagnostic;_body.sprite=sprite;_ring.sprite=sprite;_body.enabled=true;
        if(!_prototype) return;
        var size=sprite.bounds.size;
        float width=Kind==HomeSelectionKind.Person ? .72f : 2.25f;
        float height=Kind==HomeSelectionKind.Person ? 1.1f : 1.9f;
        if(Kind==HomeSelectionKind.Person) width=height*size.x/Mathf.Max(size.y,.001f);
        _body.transform.localScale=new Vector3(width/Mathf.Max(size.x,.001f),height/Mathf.Max(size.y,.001f),1);
        _body.transform.localPosition=new Vector3(0,Kind==HomeSelectionKind.Person ? .3f : .35f,0);
        _normal=Color.white;_body.color=Color.white;
        if(Kind==HomeSelectionKind.Person)
        {
            var collider=GetComponent<BoxCollider2D>();collider.size=new Vector2(width,1.1f);collider.offset=new Vector2(0,.3f);
        }
        UpdateDepth();
    }
    private void UpdateDepth()
    {
        if(!_prototype)
        {
            _body.sortingOrder=Kind==HomeSelectionKind.Person ? 10 : Kind==HomeSelectionKind.Building ? 3 : 0;
            _ring.sortingOrder=_body.sortingOrder-1;
            _name.GetComponent<MeshRenderer>().sortingOrder=20;_hoverText.GetComponent<MeshRenderer>().sortingOrder=30;
            return;
        }
        _body.sortingOrder=Mathf.Clamp(Mathf.RoundToInt(-transform.position.y*100),-15000,15000)+(Kind==HomeSelectionKind.Person ? 5 : 0);
        if(Kind==HomeSelectionKind.Person && (_hovered || _selection.Kind==Kind && _selection.Id==Identity)) _body.sortingOrder=20002;
        _ring.sortingOrder=_body.sortingOrder-1;
        _name.GetComponent<MeshRenderer>().sortingOrder=20000;
        _hoverText.GetComponent<MeshRenderer>().sortingOrder=20001;
    }
    public void Initialize(string id,HomeSelectionKind kind,HomeSelectionContext selection,HomePresentationResolver resolver,string key)
    {
        Identity=id; Kind=kind; _selection=selection;
        gameObject.layer=ProductHomeWorldHost.WorldLayer;
        var body=new GameObject("Body",typeof(SpriteRenderer)); body.transform.SetParent(transform,false); body.layer=gameObject.layer;
        _body=body.GetComponent<SpriteRenderer>();
        ResolvedSharedAsset=resolver.TryResolve(key,out var sprite,out var diagnostic);
        Diagnostic=diagnostic; _body.sprite=sprite;
        _body.sortingOrder=kind==HomeSelectionKind.Person ? 10 : 0;
        body.transform.localScale=kind==HomeSelectionKind.Person ? new Vector3(.38f,.62f,1) : new Vector3(2.25f,1.25f,1);
        _normal=kind==HomeSelectionKind.Person ? new Color(.92f,.89f,.68f) : new Color(.28f,.52f,.45f);
        _body.color=_normal;
        var ring=new GameObject("Selection",typeof(SpriteRenderer)); ring.transform.SetParent(body.transform,false); ring.layer=gameObject.layer;
        _ring=ring.GetComponent<SpriteRenderer>(); _ring.sprite=sprite; _ring.color=new Color(1,.76f,.25f);
        _ring.sortingOrder=_body.sortingOrder-1; ring.transform.localScale=Vector3.one*1.18f; _ring.enabled=false;
        var collider=gameObject.AddComponent<BoxCollider2D>();
        collider.size=kind==HomeSelectionKind.Person ? new Vector2(.65f,.8f) : new Vector2(2.1f,1.1f);
        var label=new GameObject("Name",typeof(TextMeshPro)); label.transform.SetParent(transform,false); label.layer=gameObject.layer;
        label.transform.localPosition=new Vector3(0,kind==HomeSelectionKind.Person ? -.5f : .88f,-.05f);
        _name=label.GetComponent<TextMeshPro>(); _name.font=CurrentUIFontAuthority.Default;
        _name.fontSize=kind==HomeSelectionKind.Person ? 2.2f : 2.5f;
        _name.alignment=TextAlignmentOptions.Center; _name.rectTransform.sizeDelta=new Vector2(3.2f,.55f);
        _name.enableWordWrapping=false; _name.overflowMode=TextOverflowModes.Ellipsis;
        _name.GetComponent<MeshRenderer>().sortingOrder=20; _name.color=new Color(.94f,.97f,.93f);
        if(kind==HomeSelectionKind.Person) _name.rectTransform.sizeDelta=new Vector2(1.4f,.55f);
        var hover=new GameObject("HoverAction",typeof(TextMeshPro));hover.transform.SetParent(transform,false);hover.layer=gameObject.layer;
        _hoverText=hover.GetComponent<TextMeshPro>();_hoverText.font=CurrentUIFontAuthority.Default;_hoverText.fontSize=1.8f;
        _hoverText.alignment=TextAlignmentOptions.Center;_hoverText.rectTransform.sizeDelta=new Vector2(4,.5f);
        _hoverText.transform.localPosition=new Vector3(0,kind==HomeSelectionKind.Person ? -1 : 1.4f,-.1f);
        _hoverText.GetComponent<MeshRenderer>().sortingOrder=30;_hoverText.color=new Color(.75f,.88f,.8f);
        _hoverText.enableWordWrapping=false;_hoverText.overflowMode=TextOverflowModes.Ellipsis;hover.SetActive(false);
        var attention=new GameObject("AttentionMarker",typeof(SpriteRenderer));attention.transform.SetParent(transform,false);attention.layer=gameObject.layer;
        _attention=attention.GetComponent<SpriteRenderer>();_attention.sprite=sprite;_attention.sortingOrder=25;_attention.color=new Color(1,.45f,.22f);
        attention.transform.localPosition=new Vector3(.35f,.5f,-.1f);attention.transform.localScale=new Vector3(.12f,.12f,1);_attention.enabled=false;
        RefreshSelection();
        if(kind==HomeSelectionKind.Building) _footprint=HomeBuildingFootprint.Create(transform,1,1,new Color(.7f,.58f,.35f,.85f),4);
    }
    public void ApplyPlace(HomePlaceSnapshot place,Vector3 anchor)
    {
        _name.text=place.DisplayName;_hoverSummary=place.Status; transform.position=anchor; _target=anchor; _positioned=true;RefreshSelection();
        UpdateDepth();
    }
    public void ApplyBuilding(HomeBuildingSnapshot building, HomePresentationResolver resolver)
    {
        // Re-resolve on the existing entity: Builder updates assets, never world identity or state.
        string previousDiagnostic = Diagnostic;
        ResolvedSharedAsset = resolver.TryResolve(resolver.PresentationKey(building), out var sprite, out var diagnostic);
        Diagnostic = diagnostic;
        _body.sprite = sprite;
        _ring.sprite = sprite;
        if (!ResolvedSharedAsset && previousDiagnostic != diagnostic) Debug.LogWarning("[ProductHome] " + diagnostic);
          _name.text=building.DisplayName;_hoverSummary=building.LifecycleState==BuildingLifecycleState.Completed ? "已完成" : "施工中";
        transform.position=BuildingWorldMapper.ToPresentation(new Vector2(building.WorldX,building.WorldY));
        var size = sprite.bounds.size;
        _body.transform.localScale=new Vector3(building.Width/Mathf.Max(size.x,.001f),building.Depth/Mathf.Max(size.y,.001f),1);
        _body.color=Color.white;_body.sortingOrder=3;
        if(_prototype)
        {
            float height=building.Width*size.y/Mathf.Max(size.x,.001f);
            _body.transform.localScale=new Vector3(building.Width/Mathf.Max(size.x,.001f),height/Mathf.Max(size.y,.001f),1);
            _body.transform.localPosition=new Vector3(0,-building.Depth*.5f+height*.5f,0);
            if(_spriteHit==null) _spriteHit=_body.gameObject.AddComponent<BoxCollider2D>();
            _spriteHit.size=new Vector2(size.x,size.y);_spriteHit.enabled=true;
            _body.color=building.LifecycleState==BuildingLifecycleState.Completed ? Color.white : new Color(.77f,.79f,.74f,.8f);
        }
        _ring.sortingOrder=2;GetComponent<BoxCollider2D>().size=new Vector2(building.Width,building.Depth);
        HomeBuildingFootprint.SetSize(_footprint,building.Width,building.Depth);
        _name.transform.localPosition=new Vector3(0,building.Depth*.5f+.3f,-.05f);RefreshSelection();
        UpdateDepth();
    }
    public void ApplyPerson(HomePersonSnapshot person,Vector3 anchor,Vector3 destination)
    {
        _name.text=person.DisplayName;
        _hoverSummary=person.ActivitySummary;
        gameObject.SetActive(!person.Away);
        if (!_positioned) { transform.position=anchor; _positioned=true; }
        IsTraveling=person.Traveling;
        if (person.Traveling)
        {
            if (_destination!=person.DestinationPlaceId) { _travelOrigin=transform.position; _destination=person.DestinationPlaceId; }
            // Never visually arrive before the authority: stop short while still traveling.
            _target=Vector3.Lerp(_travelOrigin,destination,.82f);
            _body.color=new Color(.6f,.8f,1);
        }
        // Arrival and activity changes update the visual target, never snap an existing person.
        // Actual presence still comes from the snapshot even while presentation catches up.
        else { _destination=null; _target=anchor; _body.color=_normal; }
        UpdateDepth();
    }
    public void StepPresentation(float seconds)
    {
        if (PresentationVisible) transform.position=Vector3.MoveTowards(transform.position,_target,Mathf.Max(0,seconds)*2f);
        UpdateDepth();
    }
    public void RefreshSelection()
    {
        bool selected=_selection.Kind==Kind && _selection.Id==Identity;
        // Building selection belongs to the footprint, independently of replaceable art.
        _ring.enabled=selected && Kind!=HomeSelectionKind.Building;
        if (_footprint != null)
        {
            _footprint.startColor=_footprint.endColor=selected ? new Color(1,.76f,.25f) : new Color(.7f,.58f,.35f,.85f);
            _footprint.startWidth=_footprint.endWidth=selected ? .065f : .025f;
            _footprint.enabled=!_prototype || selected || _hovered;
        }
        _name.gameObject.SetActive(Kind==HomeSelectionKind.Person && (!_prototype || _readableLabel) || _hovered || selected);
        _hoverText.gameObject.SetActive(_hovered);_hoverText.text=_hoverSummary;
        _name.color=_prototype ? new Color(.12f,.18f,.16f) : _hovered || selected ? Color.white : new Color(.85f,.91f,.85f);
        UpdateDepth();
    }
    public void SetHovered(bool hovered) { if(_hovered==hovered) return;_hovered=hovered;RefreshSelection(); }
    public void SetAttention(bool attention) { _attention.enabled=attention; }
    public void Click() { if (Kind==HomeSelectionKind.Person) _selection.SelectPerson(Identity); else if(Kind==HomeSelectionKind.Building) _selection.SelectBuilding(Identity);else _selection.SelectPlace(Identity); }
    private void OnDestroy(){if(_footprint!=null) Destroy(_footprint.sharedMaterial);}
}

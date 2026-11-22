using RRPG.StaticConfig;
using UnityEngine;

/// <summary>Narrow A whitebox profile. Shared VisualAsset remains the only asset authority.</summary>
public sealed class HomePresentationResolver : System.IDisposable
{
    public const string NeutralAssetId="va_village_neutral_marker";
    private Sprite _fallback;
    public bool PrototypeEnabled { get; set; }
    public string PresentationKey(HomePersonSnapshot person) => PrototypeEnabled ? "village.person.r2" : "village.person."+person.PresentationIntent;
    public string PresentationKey(HomePlaceSnapshot place)
    {
        if(!PrototypeEnabled) return "village.place."+place.Kind;
        if(place.PlaceId=="ws_kitchen_01") return "village.building.r2.building_kitchen";
        if(place.PlaceId=="ws_test_02") return "village.building.r2.building_workshop";
        return "village.place.r2."+place.Kind;
    }
    public string PresentationKey(HomeBuildingSnapshot building)
    {
        if (PrototypeEnabled) return "village.building.r2."+building.DefinitionId;
        if (StaticConfigRegistry.TryGetVillagePresentation(out var database, out _)
            && database.TryGetKey(building.DefinitionId, building.LifecycleState.ToString(), out var key)) return key;
        return "village.building.missing." + building.DefinitionId + "." + building.LifecycleState;
    }
    public bool TryResolve(string presentationKey, out Sprite sprite, out string diagnostic, string assetId=NeutralAssetId)
    {
        sprite=null; diagnostic=null;
        bool supported=!string.IsNullOrEmpty(presentationKey) &&
            (presentationKey.StartsWith("village.person.") || presentationKey.StartsWith("village.place.") || presentationKey.StartsWith("village.building."));
        if (!supported) diagnostic="Unknown home presentation key: "+presentationKey;
        else if ((PrototypeEnabled || presentationKey.StartsWith("village.building.")) && !TryBuildingAsset(presentationKey, out assetId, out diagnostic)) { }
        else if (!StaticConfigRegistry.TryGetVisualAssets(out var catalog,out diagnostic)) { }
        else if (!catalog.TryGetById(assetId,out var asset)) diagnostic=VisualAssetCatalog.BuildMissingDiagnostic(assetId);
        else if (asset.Sprite==null) diagnostic="Missing managed Sprite: "+assetId;
        else { sprite=asset.Sprite; diagnostic=null; return true; }
        if (_fallback==null) _fallback=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
        sprite=_fallback;
        return false;
    }
    private static bool TryBuildingAsset(string key, out string assetId, out string diagnostic)
    {
        assetId = null;
        if (!StaticConfigRegistry.TryGetVillagePresentation(out var database, out diagnostic)) return false;
        if (database.TryGetAssetId(key, out assetId)) return true;
        diagnostic = "Missing Village Presentation key: " + key;
        return false;
    }
    public void Dispose()
    {
        if (_fallback == null) return;
        if (Application.isPlaying) Object.Destroy(_fallback); else Object.DestroyImmediate(_fallback);
        _fallback = null;
    }
}

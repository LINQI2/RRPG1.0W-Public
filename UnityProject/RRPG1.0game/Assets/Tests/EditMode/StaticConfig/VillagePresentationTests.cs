using System;
using NUnit.Framework;
using RRPG.StaticConfig;
using UnityEngine;

namespace RRPG.StaticConfig.Tests
{
public sealed class VillagePresentationTests
{
    [Test] public void ResolverReportsMissingBuildingKeyInsteadOfUsingNeutralAsset()
    {
        StaticConfigRegistry.Reset();
        var t=Type.GetType("HomePresentationResolver, Assembly-CSharp",true);
        var resolver=Activator.CreateInstance(t);
        try {
            object[] args={"village.building.missing",null,null,"va_village_neutral_marker"};
            Assert.False((bool)t.GetMethod("TryResolve").Invoke(resolver,args));
            StringAssert.Contains("Missing Village Presentation key",(string)args[2]);
        } finally {((IDisposable)resolver).Dispose();}
    }
    [Test] public void GeneratedRootContainsBothLifecyclesAndManagedSprite()
    {
        StaticConfigRegistry.Reset();Assert.True(StaticConfigRegistry.TryGetVillagePresentation(out var db,out var diagnostic),diagnostic);
        Assert.True(StaticConfigRegistry.TryGetVisualAssets(out var assets,out diagnostic),diagnostic);
        foreach(var lifecycle in new[]{"Completed","UnderConstruction"}) {
            Assert.True(db.TryGetKey("building_farm",lifecycle,out var key));Assert.True(db.TryGetAssetId(key,out var asset));
            Assert.True(assets.TryGetById(asset,out var definition));Assert.NotNull(definition.Sprite);
        }
    }
}
}

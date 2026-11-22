using System;
using System.Reflection;
using NUnit.Framework;
using RRPG.StaticConfig;
using UnityEngine;

namespace RRPG.ProductHome.Tests
{
public sealed class VillageVisualPrototypeTests
{
    // Long-term technical contract: shared catalog and explicit fallback; no art/style assertions.
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    [Test] public void PrototypeUsesSharedGeneratedSpritesAndMissingKeyIsExplicit()
    {
        StaticConfigRegistry.Reset();var type=T("HomePresentationResolver");var resolver=Activator.CreateInstance(type);
        type.GetProperty("PrototypeEnabled").SetValue(resolver,true);
        try
        {
            foreach(var key in new[]{"village.person.r2","village.place.r2.Home","village.building.r2.building_farm","village.building.r2.building_house","village.building.r2.building_kitchen","village.building.r2.building_workshop"})
            {
                object[] args={key,null,null,"va_village_neutral_marker"};
                Assert.True((bool)type.GetMethod("TryResolve").Invoke(resolver,args),(string)args[2]);
                Assert.NotNull(args[1]);Assert.IsNull(args[2]);
            }
            object[] missing={"village.building.r2.unknown",null,null,"va_village_neutral_marker"};
            Assert.False((bool)type.GetMethod("TryResolve").Invoke(resolver,missing));Assert.NotNull(missing[1]);
            StringAssert.Contains("Missing Village Presentation key",(string)missing[2]);
        }
        finally {((IDisposable)resolver).Dispose();}
    }
}
}

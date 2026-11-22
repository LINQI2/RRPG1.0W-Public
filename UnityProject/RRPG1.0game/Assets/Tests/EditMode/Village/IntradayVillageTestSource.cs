using System;
using System.Collections;
using System.Reflection;

/// <summary>Explicit fixture persons; production never invents persons from duty records.</summary>
internal static class IntradayVillageTestSource
{
    private static Type Bridge => Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    public static object Create(params string[] ids)
    {
        object player = Activator.CreateInstance(Type.GetType("PlayerCollection, Assembly-CSharp"));
        var type = Type.GetType("CharacterInstance, Assembly-CSharp");
        var characters=(IList)player.GetType().GetField("OwnedCharacters").GetValue(player);
        foreach(string id in ids)
        {
            object person=Activator.CreateInstance(type,new object[]{"char_001",id,30,6});
            type.GetField("PersonId").SetValue(person,id);characters.Add(person);
        }
        return player;
    }
    public static void Bind(object player) => Bridge.GetMethod("SetSimulationPersonSource").Invoke(null,new[]{player});
    public static object BindDefaults()
    {var player=Create("char_001","char_002","char_003");Bind(player);return player;}
    public static object Current => Bridge.GetField("SimulationPersonSource").GetValue(null);
}

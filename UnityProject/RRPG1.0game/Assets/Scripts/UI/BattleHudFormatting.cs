using System;
using System.Globalization;
using UnityEngine;

public static class BattleHudFormatting
{
    public static string Format(string format, params object[] values)
    {
        try { return string.Format(CultureInfo.InvariantCulture, format ?? "{0}", values); }
        catch (FormatException) { Debug.LogWarning("[Battle HUD] Invalid Inspector format: " + format); return "<?>"; }
    }
}

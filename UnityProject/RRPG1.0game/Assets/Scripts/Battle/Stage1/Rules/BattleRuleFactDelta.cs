using System.Collections.Generic;

/// <summary>规则写入前后的中立事实；只保存标量，不持有 BattleUnit 或 Presentation。</summary>
public sealed class BattleRuleFactSnapshot
{
    public string UnitId;
    public int HP, MaxHP, Shield, Row, Column;
    public bool IsDown;
    public static BattleRuleFactSnapshot From(BattleRuleUnitFacts u)
    {
        return u == null ? null : new BattleRuleFactSnapshot { UnitId = u.UnitId,
            HP = u.CurrentHP, MaxHP = u.MaxHP, Shield = u.Shield, Row = u.Row, Column = u.Column, IsDown = u.IsDown };
    }
}

public sealed class BattleRuleFactDelta
{
    public BattleRuleFactSnapshot Before, After;
}

public static class BattleRuleFactCapture
{
    public static List<BattleRuleFactSnapshot> Read(IBattleRuleContext context)
    {
        var result = new List<BattleRuleFactSnapshot>();
        foreach (var u in context.GetAllies()) result.Add(BattleRuleFactSnapshot.From(u));
        foreach (var u in context.GetEnemies()) result.Add(BattleRuleFactSnapshot.From(u));
        return result;
    }

    public static List<BattleRuleFactDelta> Changes(List<BattleRuleFactSnapshot> before, IBattleRuleContext context)
    {
        var result = new List<BattleRuleFactDelta>();
        foreach (var b in before)
        {
            BattleRuleUnitFacts u;
            if (b == null || !context.TryGetUnit(b.UnitId, out u)) continue;
            var a = BattleRuleFactSnapshot.From(u);
            if (b.HP != a.HP || b.Shield != a.Shield || b.Row != a.Row || b.Column != a.Column || b.IsDown != a.IsDown)
                result.Add(new BattleRuleFactDelta { Before = b, After = a });
        }
        return result;
    }
}

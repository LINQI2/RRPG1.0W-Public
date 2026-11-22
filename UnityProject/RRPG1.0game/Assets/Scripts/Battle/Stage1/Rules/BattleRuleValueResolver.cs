/// <summary>规则与 Presentation 共用的只读数值 Authority；不做状态写入。</summary>
public static class BattleRuleValueResolver
{
    public static bool TryResolve(BattleRuleValueSource source, int param, BattleCard card,
        IBattleRuleContext context, out int value)
    {
        value = 0;
        if (source == BattleRuleValueSource.Fixed) { value = param; return true; }
        if (source != BattleRuleValueSource.CasterAttack || context == null || card == null) return false;
        string id; BattleRuleUnitFacts owner;
        if (!context.TryGetOwnerUnitId(card, out id) || !context.TryGetUnit(id, out owner) || owner == null) return false;
        value = owner.Attack; return true;
    }
    public static int Resolve(BattleRuleValueSource source, int param, BattleCard card, IBattleRuleContext context)
    {
        int value; TryResolve(source, param, card, context, out value); return value;
    }
}

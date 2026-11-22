using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class BattleCardPresentation
{
    public Sprite Art;
    public Sprite OwnerPortrait;
    public string OwnerName;
    public int ActualCost;
    public string RichDescription;
    public readonly List<string> Diagnostics = new List<string>();
}

/// <summary>有限只读模板：cost / owner.attack / step:N.value / step:N.bonus。</summary>
public static class BattleCardPresentationResolver
{
    static readonly Regex Tokens = new Regex(@"\{([^{}]+)\}");
    public static BattleCardPresentation Resolve(BattleCard card, IBattleRuleContext facts,
        PlayerCollection people, BattleCardPresentationCatalog catalog, Sprite artPlaceholder,
        Sprite ownerPlaceholder, Sprite commonOwnerIcon)
    {
        var output = new BattleCardPresentation();
        if (card == null) return output;
        output.ActualCost = card.ActualCost;
        var entry = catalog != null ? catalog.Find(card.TemplateId) : null;
        output.Art = entry != null && entry.ArtSprite != null ? entry.ArtSprite : artPlaceholder;
        if (card.CardKind == BattleCardKind.Character)
        {
            var owner = PersonVisualResolver.ResolveForPerson(people, card.OwnerPersonId,
                PersonVisualSurface.CardPortrait, ownerPlaceholder);
            output.OwnerPortrait = owner.Sprite; output.OwnerName = owner.DisplayName;
        }
        else { output.OwnerPortrait = commonOwnerIcon != null ? commonOwnerIcon : ownerPlaceholder; output.OwnerName = "通用"; }
        if (entry == null || string.IsNullOrEmpty(entry.DescriptionTemplate))
        { output.RichDescription = card.Description ?? string.Empty; return output; }
        output.RichDescription = Tokens.Replace(entry.DescriptionTemplate, match =>
        {
            int value;
            if (TryToken(match.Groups[1].Value, card, facts, out value)) return value.ToString(CultureInfo.InvariantCulture);
            output.Diagnostics.Add("Unresolved description token: " + match.Value + " (template " + card.TemplateId + ")");
            return "<?>";
        });
        return output;
    }
    public static bool TryToken(string token, BattleCard card, IBattleRuleContext facts, out int value)
    {
        value = 0;
        if (card == null) return false;
        if (token == "cost") { value = card.ActualCost; return true; }
        if (token == "owner.attack") return BattleRuleValueResolver.TryResolve(BattleRuleValueSource.CasterAttack, 0, card, facts, out value);
        var match = Regex.Match(token, @"^step:(\d+)\.(value|bonus)$");
        int index; var program = BattleCardRuleResolver.GetProgram(card);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out index) || program == null || program.Steps == null || index >= program.Steps.Count) return false;
        var step = program.Steps[index]; if (step == null) return false;
        // bonus means configured bonus magnitude, not a claim that a target-dependent condition is satisfied.
        return match.Groups[2].Value == "bonus"
            ? BattleRuleValueResolver.TryResolve(step.BonusValueSource, step.BonusValueParam, card, facts, out value)
            : BattleRuleValueResolver.TryResolve(step.ValueSource, step.ValueParam, card, facts, out value);
    }
}

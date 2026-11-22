using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Battle Card 表现消费者：单个描述 TMP，规则数值来自共享 Authority。</summary>
public class BattleCardView : MonoBehaviour
{
    public TMP_Text nameText, costText, kindText, ownerText, flagsText, descriptionText;
    public Image artImage;
    public Sprite artPlaceholder;
    public Image ownerPortraitImage;
    public Sprite ownerPortraitPlaceholder;
    public Sprite commonOwnerIcon;
    public BattleCardPresentationCatalog presentationCatalog;
    public bool showDebugMetadata;
    BattleCard _card;
    string _badge;
    Func<IBattleRuleContext> _facts;
    Func<PlayerCollection> _people;
    readonly System.Collections.Generic.HashSet<string> _diagnosed = new System.Collections.Generic.HashSet<string>();

    public void Bind(BattleCard card) { Bind(card, null); }
    public void Bind(BattleCard card, string badge) { BindPresentation(card, badge, null, null); }
    public void BindPresentation(BattleCard card, string badge, Func<IBattleRuleContext> facts, Func<PlayerCollection> people)
    {
        _card = card; _badge = badge; _facts = facts; _people = people;
        if (presentationCatalog == null) presentationCatalog = Resources.Load<BattleCardPresentationCatalog>(BattleCardPresentationCatalog.ResourcePath);
        RefreshPresentation();
    }
    void LateUpdate() { if (_card != null) RefreshPresentation(); }
    public void RefreshPresentation()
    {
        if (_card == null) return;
        var output = BattleCardPresentationResolver.Resolve(_card, _facts != null ? _facts() : null,
            _people != null ? _people() : null, presentationCatalog, artPlaceholder,
            ownerPortraitPlaceholder, commonOwnerIcon);
        Set(nameText, _card.DisplayName);
        Set(costText, output.ActualCost.ToString());
        Set(kindText, _card.CardKind == BattleCardKind.Character ? "角色牌" : "通用牌");
        Set(descriptionText, output.RichDescription);
        if (descriptionText != null) descriptionText.richText = true;
        if (artImage != null && artImage.sprite != output.Art) artImage.sprite = output.Art;
        if (ownerPortraitImage != null && ownerPortraitImage.sprite != output.OwnerPortrait) ownerPortraitImage.sprite = output.OwnerPortrait;
        Set(ownerText, output.OwnerName ?? string.Empty);
        if (ownerText != null) ownerText.enabled = showDebugMetadata;
        Set(flagsText, "cost modifier=" + _card.CostModifierTotal + " damage modifier=" + _card.DamageModifierTotal
            + " retain=" + _card.Retain + " exhaust=" + _card.IsExhaust + " " + (_badge ?? string.Empty));
        if (flagsText != null) flagsText.enabled = showDebugMetadata;
        foreach (var diagnostic in output.Diagnostics)
            if (_diagnosed.Add(diagnostic)) Debug.LogWarning("[Battle Card Presentation] " + diagnostic);
    }
    static void Set(TMP_Text host, string value) { if (host != null && host.text != value) host.text = value; }
}

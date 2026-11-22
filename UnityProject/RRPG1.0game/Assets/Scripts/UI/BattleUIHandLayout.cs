using System.Collections.Generic;
using UnityEngine;

public partial class BattleUI : MonoBehaviour
{
    private void CreateInitialPrepHandCards(Transform container)
    {
        CreateInitialHeroPrepCard(container);
        CreateInitialCharacterPrepCards(container);
        RearrangeHandCards();
    }

    private void CreateInitialHeroPrepCard(Transform container)
    {
        if (_playerHero == null || _playerHeroData == null) return;

        var heroGo = Instantiate(prepCardPrefab, container);
        heroGo.name = "HeroPrepCard_" + _playerHeroData.DisplayName;
        var heroRt = heroGo.GetComponent<RectTransform>();
        heroRt.anchorMin = new Vector2(0.5f, 0.5f);
        heroRt.anchorMax = new Vector2(0.5f, 0.5f);

        var heroBp = heroGo.GetComponent<BattlePrepCard>();
        if (heroBp == null) heroBp = heroGo.AddComponent<BattlePrepCard>();
        heroBp.Init(this);
        heroBp.SetHeroData(_playerHeroData, _playerHero);

        // Person Visual Binding V0.1：Prep 表面读取统一 Visual Binding（CharCard.prefab 的 Anchor_Visual/Body_Visual）
        PersonVisualHostBinder.ApplyForPrepPerson(heroGo, _playerData,
            _playerHero != null ? _playerHero.PersonId : null, _playerHeroData.HeroId,
            prepBodyPlaceholder);
    }

    private void CreateInitialCharacterPrepCards(Transform container)
    {
        for (int i = 0; i < _allCharCards.Count; i++)
        {
            var cardData = _allCharCards[i];
            var characterData = _allCharDatas.Find(d => d.CharacterId == cardData.CharacterId);
            if (characterData == null) continue;

            var go = Instantiate(prepCardPrefab, container);
            go.name = "PrepCard_" + cardData.CardId;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);

            var bp = go.GetComponent<BattlePrepCard>();
            if (bp == null) bp = go.AddComponent<BattlePrepCard>();
            bp.Init(this);
            bp.SetCardData(cardData, characterData, ResolvePersonDisplayName(cardData));

            // Person Visual Binding V0.1：Prep 表面读取统一 Visual Binding（卡 → PersonId → 外观键 → placeholder）
            PersonVisualHostBinder.ApplyForPrepPerson(go, _playerData, ResolveCardPersonIdForVisual(cardData),
                AppearanceKeyFallbackOfCharCard(cardData),
                prepBodyPlaceholder);
        }
    }

    private void CreateReturnedHeroPrepCard()
    {
        var host = PrepCardHost;
        if (host == null) return;
        var heroGo = Instantiate(prepCardPrefab, host);
        heroGo.name = "HeroPrepCard_" + _playerHeroData.DisplayName;
        var heroBp = heroGo.GetComponent<BattlePrepCard>();
        if (heroBp == null) heroBp = heroGo.AddComponent<BattlePrepCard>();
        heroBp.Init(this);
        heroBp.SetHeroData(_playerHeroData, _playerHero);

        PersonVisualHostBinder.ApplyForPrepPerson(heroGo, _playerData,
            _playerHero != null ? _playerHero.PersonId : null, _playerHeroData.HeroId,
            prepBodyPlaceholder);
        RearrangeHandCards();
    }

    private void CreateReturnedCharacterPrepCard(CharacterCardData cardData, CharacterData characterData)
    {
        var host = PrepCardHost;
        if (host == null) return;
        var go = Instantiate(prepCardPrefab, host);
        go.name = "PrepCard_" + cardData.CardId;
        var bp = go.GetComponent<BattlePrepCard>();
        if (bp == null) bp = go.AddComponent<BattlePrepCard>();
        bp.Init(this);
        bp.SetCardData(cardData, characterData, ResolvePersonDisplayName(cardData));

        PersonVisualHostBinder.ApplyForPrepPerson(go, _playerData, ResolveCardPersonIdForVisual(cardData),
            AppearanceKeyFallbackOfCharCard(cardData),
            prepBodyPlaceholder);
        RearrangeHandCards();
    }

    private void RearrangeHandCards()
    {
        // 正式容器与 BattleCanvas 同尺寸、同原点，保留原 Prep 布局参数及坐标语义。
        var host = PrepCardHost;
        if (host == null) return;
        var cards = new List<Transform>();
        foreach (Transform child in host)
        {
            if ((child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_")) && child.gameObject.activeSelf)
            {
                cards.Add(child);
            }
        }

        // 每次从 Prefab 读基础 Scale，避免反复重排时乘数累积。
        Vector3 baseScale = prepCardPrefab != null ? prepCardPrefab.transform.localScale : Vector3.one;
        Vector3 scale = baseScale * Mathf.Max(0.01f, prepCardScale);
        float cardWidth = 0f;
        foreach (var card in cards)
            cardWidth = Mathf.Max(cardWidth, card.GetComponent<RectTransform>().rect.width * Mathf.Abs(scale.x));
        float availableWidth = Mathf.Max(0f, host.rect.width - 2f * Mathf.Abs(prepHandCenterOffset.x));
        for (int i = 0; i < cards.Count; i++)
        {
            var rt = cards[i].GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localScale = scale;
            rt.anchoredPosition = BattleHandVisualLayout.GetPosition(cards.Count, i, availableWidth, cardWidth,
                prepHandBaseStep, prepHandMinStep, prepHandCenterOffset);
        }
    }
}

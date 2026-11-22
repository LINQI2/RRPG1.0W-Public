using TMPro;
using RRPG.StaticConfig;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WP-D｜Unit Visual Host（BattleUI partial）
///
/// 目标数据流：
///   BattleUnit（Gameplay Fact）→ 本 partial 的绑定 → UnitCard Prefab 上的 Anchor / Body / TMP_Text 宿主 → 占位 Sprite。
///
/// 换正式素材时预期动作：Import → 在 BattleUI / UnitCard Prefab 上 Assign → Inspector Tuning，不改 Battle 逻辑。
/// 本 WP 只提供可替换宿主与占位，不代表最终画风，也不构成新的玩法 Authority。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    // ---- UnitCard Prefab 视觉宿主对象名（可复用现有对象）----
    public const string HostAnchorVisual = "Anchor_Visual";     // 小人视觉锚点
    public const string HostBodyVisual = "Body_Visual";         // 完整小人身体占位图宿主
    public const string HostTextName = "Text_Name";             // 名称宿主（复用现有）
    public const string HostTextHP = "Text_HP";                 // HP 独立宿主（复用现有 Text_HP）
    public const string HostTextATK = "Text_ATK";               // 攻击力（信息用，复用现有）
    public const string HostTextAP = "Text_AP";                 // 敌方行动次数（复用现有；玩家侧留空）
    public const string HostAnchorShield = "Anchor_Shield";     // 护盾独立宿主
    public const string HostTextShield = "Text_Shield";
    public const string HostImageShieldIcon = "Image_ShieldIcon";
    public const string HostAnchorIntent = "Anchor_Intent";     // 敌方 Intent 稳定宿主
    public const string HostTextIntent = "Text_Intent";
    public const string HostImageIntentIcon = "Image_IntentIcon";
    public const string HostAnchorStatus = "Anchor_Status";     // 可空
    public const string HostAnchorEffect = "Anchor_Effect";     // 可空

    [Header("WP-D Unit Visual Host（可替换宿主；占位 Sprite 不代表最终画风）")]
    [Tooltip("我方完整小人身体占位图（未来替换为正式 Sprite）")]
    public Sprite playerBodyPlaceholder;

    [Tooltip("Shield 图标占位（Shield 数值仍由 BattleUnit.Shield 决定）")]
    public Sprite shieldIconPlaceholder;

    [Tooltip("敌方 Intent 占位图标：进攻")]
    public Sprite intentAttackPlaceholder;

    [Tooltip("敌方 Intent 占位图标：防御")]
    public Sprite intentDefendPlaceholder;

    [Tooltip("敌方 Intent 占位图标：特殊")]
    public Sprite intentSpecialPlaceholder;

    [Tooltip("我方身体占位缩放")]
    public float playerBodyScale = 1f;

    [Tooltip("敌方身体占位缩放")]
    public float enemyBodyScale = 1f;

    [Tooltip("身体占位在 Anchor_Visual 内的本地偏移")]
    public Vector2 unitBodyVisualOffset = Vector2.zero;

    [Header("Person Visual Binding V0.1（我方玩家视觉：配置唯一来源 = PersonVisualBindingCatalog 资产）")]
    [Tooltip("战前准备人物卡缺配置 / 缺素材时的中性占位图；未指定时使用通用占位")]
    public Sprite prepBodyPlaceholder;

    /// <summary>
    /// 按 BattleUnit 绑定完整视觉宿主（Body / HP / Shield / Intent）。
    /// 玩家与敌方共用同一条绑定链，但身体视觉来源不同：
    ///   我方 = Person Visual Binding（PersonVisualResolver）；
    ///   敌方 = **Enemy Presentation Authority**（M3 Slice 1D-1：EnemyPresentationResolver，唯一入口）。
    /// </summary>
    public void ApplyUnitVisualHosts(GameObject card, BattleUnit unit, bool isPlayer)
    {
        if (card == null) return;

        // M3 Slice 1D-1：敌方身体视觉只经 Resolver（MonsterId → Presentation → Sprite）；
        // BattleUI 不判断 Presentation / VisualAsset / fallback 条件（M4 起旧占位字段已物理删除）。
        if (isPlayer) ApplyPlayerUnitBodyVisual(card, unit);
        else ApplyEnemyBodyVisual(card, unit != null ? unit.SourceId : null);

        if (unit == null) return;

        SetHPHost(card, unit.CurrentHP + "/" + unit.MaxHP
            + (unit.IsDown ? " 倒地" : unit.InDeathDoor ? " 濒死" : ""));
        ValueHost(card, card.transform.Find(HostTextHP)?.GetComponent<TMP_Text>(), true)
            .SetImmediate(unit.CurrentHP, unit.MaxHP);
        SetShieldHost(card, unit.Shield);

        if (!isPlayer)
        {
            bool combatCapable = !unit.IsDown && unit.CurrentHP > 0;
            SetIntentPresentationHost(card, combatCapable && _battleSystem != null ? _battleSystem.GetEnemyIntent(unit) : null);
        }
    }

    // ==================== M3 Slice 1D-1：敌方身体视觉（Enemy Presentation Authority） ====================
    //   MonsterId → EnemyPresentationResolver.Resolve(monsterId) → Sprite → Anchor_Visual/Body_Visual
    //
    //   硬约束：
    //     - Prep（战前布阵敌方格）与 Battle（战斗单位卡）**共用同一个 Resolver 实例**（下方字段）；
    //     - BattleUI 只负责「传入 MonsterId + 显示返回的 Sprite」：不判断 Presentation 缺失 /
    //       VisualAsset 缺失 / fallback 条件（M4 起旧占位字段已物理删除，不再存在旧读取路径）；
    //     - 缺失数据时的 fallback sprite 与 WarnOnce 诊断**只由 Resolver 内部**完成（M3 Slice 1C 契约）；
    //     - 仍使用既有 Body_Visual 宿主与 Inspector 表面参数（enemyBodyScale / unitBodyVisualOffset）。

    private EnemyPresentationResolver _enemyPresentationResolver;

    /// <summary>
    /// 敌方身体的**唯一** Resolver（lazy 取得；Prep 与 Battle 必须走同一实例）。
    /// 数据来源 = StaticConfigRegistry → EnemyPresentationDatabase / VisualAssetCatalog（生成根）。
    /// </summary>
    public EnemyPresentationResolver PresentationResolver
    {
        get
        {
            if (_enemyPresentationResolver == null)
            {
                _enemyPresentationResolver = EnemyPresentationResolver.FromStaticConfig();
            }
            return _enemyPresentationResolver;
        }
    }

    /// <summary>
    /// 敌方身体视觉绑定（**Prep 与 Battle 的唯一入口**）：MonsterId → Resolver → Sprite。
    /// 返回是否写入了 Body_Visual 宿主（card 为空 / 宿主缺失时为 false）。
    /// 本方法不做任何数据判断：缺失数据的 fallback 与诊断都在 Resolver 内部完成。
    /// </summary>
    public bool ApplyEnemyBodyVisual(GameObject card, string monsterId)
    {
        if (card == null) return false;

        Sprite sprite = PresentationResolver.Resolve(monsterId);
        return PersonVisualHostBinder.ApplyBodyVisual(card, sprite, unitBodyVisualOffset, enemyBodyScale);
    }

    // ==================== Person Visual Binding V0.1（我方玩家视觉） ====================
    //   Current PlayerUnit → 当前 PersonId → AppearanceKey → Visual Binding → UnitCard/Anchor_Visual/Body_Visual
    //
    //   边界（硬约束）：
    //     - 只改 Presentation Binding：不写 Row / Column / Board Occupancy / Formation / Active Roster /
    //       Pending Formation / Shared Deck / Caster / Range / Adjacent / Move / Swap / Shared AP；
    //     - 不再让所有玩家 Character / Hero 强制共用一个 player placeholder；
    //     - 未配置 / 缺素材 → 既有 playerBodyPlaceholder（行为与 WP-D 一致，不隐藏身体）；
    //     - Sprite 使用既有 Body_Visual 宿主；PreserveAspect 保持；只提供最小 Scale / Offset。

    /// <summary>我方单位身体视觉（Battle 表现入口；unit 为 null 时等价于 placeholder）。</summary>
    public bool ApplyPlayerUnitBodyVisual(GameObject card, BattleUnit unit)
    {
        if (card == null) return false;

        string personId = GetPersonIdOfUnit(unit);
        // SourceId 只作**外观键**兜底（Character = CharacterId / Hero = HeroId），不代表人物身份。
        string appearanceKeyFallback = unit != null ? unit.SourceId : null;
        return ApplyPlayerBodyVisual(card, personId, appearanceKeyFallback);
    }

    /// <summary>
    /// 我方身体视觉（战前布阵尚无 BattleUnit 时，用卡 → PersonId / 外观键）。
    /// 返回是否命中配置绑定（false = 使用既有 placeholder）。
    /// </summary>
    public bool ApplyPlayerBodyVisual(GameObject card, string personId, string appearanceKeyFallback)
    {
        if (card == null) return false;

        bool exactPerson = _playerData != null && !string.IsNullOrEmpty(personId)
            && (_playerData.GetCharacterByPersonId(personId) != null
                || _playerData.GetHeroByPersonId(personId) != null);

        PersonVisualResult result;
        if (exactPerson)
        {
            result = PersonVisualResolver.ResolveForPerson(
                _playerData, personId, PersonVisualSurface.Battle, playerBodyPlaceholder);
        }
        else if (_playerData != null)
        {
            // 有长期集合却无 exact Person 时仅用中性占位，不能拿模板图伪装身份。
            result = PersonVisualResolver.ResolveForAppearanceKey(
                null, PersonVisualSurface.Battle, playerBodyPlaceholder);
        }
        else
        {
            string key = !string.IsNullOrEmpty(appearanceKeyFallback)
                ? appearanceKeyFallback
                : PersonVisualResolver.ResolveAppearanceKey(_playerData, personId);
            result = PersonVisualResolver.ResolveForAppearanceKey(
                key, PersonVisualSurface.Battle, playerBodyPlaceholder);
        }

        // 既有 WP-D Inspector 表面调参保持：未命中配置时 Scale = playerBodyScale / Offset = unitBodyVisualOffset。
        float scale = playerBodyScale * (result.UsedFallback ? 1f : result.Scale);
        Vector2 offset = unitBodyVisualOffset + (result.UsedFallback ? Vector2.zero : result.Offset);
        return PersonVisualHostBinder.ApplyBodyVisual(card, result.Sprite, offset, scale);
    }

    /// <summary>HP 独立宿主刷新（HP 数值仍是 BattleUnit.CurrentHP / MaxHP）</summary>
    public void SetHPHost(GameObject card, string hpText)
    {
        if (card == null) return;
        var text = card.transform.Find(HostTextHP)?.GetComponent<TMP_Text>();
        if (text != null && text.text != hpText) text.text = hpText;
    }

    /// <summary>
    /// Shield 独立宿主刷新（Shield 仍是 BattleUnit.Shield；不再只拼进 HP 字符串）。
    /// Shield ≤ 0 时整块 Anchor_Shield 隐藏，避免占位虚空。
    /// </summary>
    public void SetShieldHost(GameObject card, int shield)
    {
        if (card == null) return;

        var anchor = card.transform.Find(HostAnchorShield);
        if (anchor == null)
        {
            // 宿主缺失时不回退到 HP 字符串拼接（避免重新把 Shield 混进 HP Host），只提示一次。
            WarnMissingHostOnce(HostAnchorShield);
            return;
        }

        bool visible = shield > 0;
        if (anchor.gameObject.activeSelf != visible) anchor.gameObject.SetActive(visible);

        var icon = anchor.Find(HostImageShieldIcon)?.GetComponent<Image>();
        if (icon != null && shieldIconPlaceholder != null && icon.sprite != shieldIconPlaceholder)
            icon.sprite = shieldIconPlaceholder;

        var text = anchor.Find(HostTextShield)?.GetComponent<TMP_Text>();
        if (text != null)
        {
            string value = visible ? shield.ToString() : string.Empty;
            if (text.text != value) text.text = value;
        }
    }

    /// <summary>
    /// 敌方 Intent 稳定宿主刷新。intentText 为 null / 空串时清空。
    /// 仍只显示 Current 允许的粗粒度 Intent，不泄露目标与 AI 逻辑。
    /// </summary>
    public void SetIntentHost(GameObject card, string intentText)
    {
        if (card == null) return;

        var anchor = card.transform.Find(HostAnchorIntent);
        if (anchor == null)
        {
            WarnMissingHostOnce(HostAnchorIntent);
            return;
        }

        var text = anchor.Find(HostTextIntent)?.GetComponent<TMP_Text>();
        if (text != null)
        {
            string value = intentText ?? string.Empty;
            if (text.text != value) text.text = value;
        }

        var icon = anchor.Find(HostImageIntentIcon)?.GetComponent<Image>();
        if (icon != null)
        {
            var sprite = IntentIconFor(intentText);
            if (sprite != null && icon.sprite != sprite) icon.sprite = sprite;
            if (icon.enabled != (sprite != null)) icon.enabled = sprite != null;
        }
    }

    /// <summary>Current Intent Type → Sprite。Intent 没有已结算数值事实，因此数值宿主保持空白。</summary>
    public void SetIntentPresentationHost(GameObject card, EnemyIntent intent)
    {
        if (card == null) return;
        var anchor = card.transform.Find(HostAnchorIntent);
        if (anchor == null) { WarnMissingHostOnce(HostAnchorIntent); return; }
        var text = anchor.Find(HostTextIntent)?.GetComponent<TMP_Text>();
        if (text != null) text.text = string.Empty;
        var icon = anchor.Find(HostImageIntentIcon)?.GetComponent<Image>();
        if (icon == null) return;
        Sprite sprite = intent == null ? null : intent.ActionType == EnemyActionType.Attack ? intentAttackPlaceholder
            : intent.ActionType == EnemyActionType.Defend ? intentDefendPlaceholder : intentSpecialPlaceholder;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    /// <summary>按当前粗粒度 Intent 文本取占位图标（占位未绑定时返回 null，只显示文字）</summary>
    private Sprite IntentIconFor(string intentText)
    {
        if (string.IsNullOrEmpty(intentText)) return null;
        if (intentText.Contains("进攻")) return intentAttackPlaceholder;
        if (intentText.Contains("防御")) return intentDefendPlaceholder;
        if (intentText.Contains("特殊")) return intentSpecialPlaceholder;
        return null;
    }

    /// <summary>取当前敌方单位已锁定的粗粒度 Intent 文本（不含具体目标）</summary>
    private string GetEnemyIntentTextOf(BattleUnit unit)
    {
        if (_battleSystem == null || unit == null) return null;
        var intent = _battleSystem.GetEnemyIntent(unit);
        return intent != null ? EnemyIntentDisplayName(intent.ActionType) : null;
    }

    private readonly System.Collections.Generic.HashSet<string> _missingHostWarned =
        new System.Collections.Generic.HashSet<string>();

    private void WarnMissingHostOnce(string hostName)
    {
        if (_missingHostWarned.Contains(hostName)) return;
        _missingHostWarned.Add(hostName);
        Debug.LogWarning("[BattleUI] WP-D 视觉宿主缺失：" + hostName + "（UnitCard Prefab 未绑定；请通过 Unity Editor 补齐）");
    }
}

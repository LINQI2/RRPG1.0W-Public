using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Implementation-only V0.1；只存 Presentation，不复制 Gameplay 配置。</summary>
[CreateAssetMenu(menuName = "RRPG/Presentation/Battle Card Catalog")]
public sealed class BattleCardPresentationCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public string TemplateId;
        public Sprite ArtSprite;
        [TextArea(2, 8)] public string DescriptionTemplate;
    }
    public List<Entry> entries = new List<Entry>();
    public const string ResourcePath = "ScriptableObjects/Presentation/BattleCardPresentationCatalog";
    public Entry Find(string templateId)
    {
        return entries.Find(e => e != null && e.TemplateId == templateId);
    }
}

[Serializable] public sealed class BattleKeywordDefinition
{
    public string KeywordId;
    public string DisplayName;
    [TextArea] public string Description;
}

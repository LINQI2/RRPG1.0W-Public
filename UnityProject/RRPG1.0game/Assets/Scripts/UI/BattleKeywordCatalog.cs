using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RRPG/Presentation/Battle Keywords")]
public sealed class BattleKeywordCatalog : ScriptableObject
{
    public List<BattleKeywordDefinition> keywords = new List<BattleKeywordDefinition>();
    public const string ResourcePath = "ScriptableObjects/Presentation/BattleKeywordCatalog";
    public BattleKeywordDefinition Find(string id) { return keywords.Find(k => k != null && k.KeywordId == id); }
    public static string LinkId(string id) { return "keyword:" + id; }
}

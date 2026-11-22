using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trait 目录：人物 → 默认 Trait 映射（初始分配用）。
/// </summary>
[CreateAssetMenu(fileName = "NewTraitCatalog", menuName = "RRPG/人物成长/Trait 目录（测试值）")]
public class TraitCatalog : ScriptableObject
{
    [System.Serializable]
    public class PersonTraitEntry
    {
        public string PersonId;
        public TraitData Trait;
    }

    public List<PersonTraitEntry> DefaultTraits = new List<PersonTraitEntry>();

    public TraitData GetDefaultTrait(string personId)
    {
        if (DefaultTraits == null || string.IsNullOrEmpty(personId)) return null;
        foreach (var e in DefaultTraits)
        {
            if (e != null && e.Trait != null && e.PersonId == personId) return e.Trait;
        }
        return null;
    }
}

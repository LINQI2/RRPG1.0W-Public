using System;
using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    [Serializable]
    public sealed class VillagePresentation
    {
        public string PresentationKey;
        public string VisualAssetId;
    }

    [Serializable]
    public sealed class BuildingPresentationBinding
    {
        public string BuildingDefinitionId;
        public string Lifecycle;
        public string PresentationKey;
    }

    /// <summary>Generated presentation mappings only. Assets belong to the shared VisualAssetCatalog.</summary>
    public sealed class VillagePresentationDatabase : ScriptableObject
    {
        [SerializeField] private List<VillagePresentation> presentations = new List<VillagePresentation>();
        [SerializeField] private List<BuildingPresentationBinding> buildings = new List<BuildingPresentationBinding>();
        public int PresentationCount => presentations.Count;
        public int BindingCount => buildings.Count;

        public void ApplyGeneratedContent(List<VillagePresentation> entries, List<BuildingPresentationBinding> bindings)
        {
            presentations = entries;
            buildings = bindings;
        }

        public bool TryGetKey(string definitionId, string lifecycle, out string key)
        {
            key = null;
            foreach (var binding in buildings)
                if (binding.BuildingDefinitionId == definitionId && binding.Lifecycle == lifecycle)
                { key = binding.PresentationKey; return true; }
            return false;
        }

        public bool TryGetAssetId(string key, out string assetId)
        {
            assetId = null;
            foreach (var entry in presentations)
                if (entry.PresentationKey == key) { assetId = entry.VisualAssetId; return true; }
            return false;
        }
    }
}

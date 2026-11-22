using System;
namespace RRPG.StaticConfig
{
    [Serializable]
    public sealed class BuildingDefinition
    {
        public string BuildingDefinitionId;
        public string DisplayName;
        public string Category;
        public int FootprintWidth;
        public int FootprintDepth;
    }
}

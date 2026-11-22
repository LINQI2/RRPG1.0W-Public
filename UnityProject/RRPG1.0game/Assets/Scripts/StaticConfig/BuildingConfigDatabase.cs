using System.Collections.Generic;
using UnityEngine;
namespace RRPG.StaticConfig
{
    public sealed class BuildingConfigDatabase : ScriptableObject
    {
        [SerializeField] private List<BuildingDefinition> buildings=new List<BuildingDefinition>();
        [SerializeField] private List<BuildingFunctionBinding> functions=new List<BuildingFunctionBinding>();
        public IReadOnlyList<BuildingDefinition> All => buildings.AsReadOnly();
        public int Count => buildings.Count;
        public void ApplyGeneratedContent(List<BuildingDefinition> definitions)
        { new BuildingRegistry(definitions);buildings=definitions ?? new List<BuildingDefinition>(); }
        public void ApplyGeneratedFunctions(List<BuildingFunctionBinding> bindings)
        {new BuildingRegistry(buildings,bindings);functions=bindings ?? new List<BuildingFunctionBinding>();}
        public BuildingRegistry CreateRegistry() => new BuildingRegistry(buildings,functions);
    }
}

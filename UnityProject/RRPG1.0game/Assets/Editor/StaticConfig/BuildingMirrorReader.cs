using System.Collections.Generic;
using System.Globalization;
using System.IO;
namespace RRPG.StaticConfig.EditorTools
{
    internal static class BuildingMirrorReader
    {
        internal static List<BuildingFunctionBinding> ReadFunctions(string dir)
        {
            var result=new List<BuildingFunctionBinding>();
            if(!File.Exists(Path.Combine(dir,"BuildingFunctionBinding.json"))) return result;
            foreach(var r in AutonomousLifeMirrorReader.ReadTable(dir,"BuildingFunctionBinding",new[]{"BuildingDefinitionId","FunctionSlotId","FunctionType","FunctionProfileId"}))
                result.Add(new BuildingFunctionBinding { BuildingDefinitionId=AutonomousLifeMirrorReader.Cell(r,"BuildingDefinitionId"),
                    FunctionSlotId=AutonomousLifeMirrorReader.Cell(r,"FunctionSlotId"),FunctionType=AutonomousLifeMirrorReader.Cell(r,"FunctionType"),
                    FunctionProfileId=AutonomousLifeMirrorReader.Cell(r,"FunctionProfileId") });
            new BuildingRegistry(Read(dir),result);
            return result;
        }
        internal static List<BuildingDefinition> Read(string dir)
        {
            // Missing domain is an explicit empty generated database; Runtime catalog diagnoses it.
            if(!File.Exists(Path.Combine(dir,"BuildingDefinition.json"))) return new List<BuildingDefinition>();
            var definitions=new List<BuildingDefinition>();
            foreach(var r in AutonomousLifeMirrorReader.ReadTable(dir,"BuildingDefinition",new[]{"BuildingDefinitionId","DisplayName","Category","FootprintWidth","FootprintDepth"}))
                definitions.Add(new BuildingDefinition {
                    BuildingDefinitionId=AutonomousLifeMirrorReader.Cell(r,"BuildingDefinitionId"),
                    DisplayName=AutonomousLifeMirrorReader.Cell(r,"DisplayName"),
                    Category=AutonomousLifeMirrorReader.Cell(r,"Category"),
                    FootprintWidth=int.Parse(AutonomousLifeMirrorReader.Cell(r,"FootprintWidth"),CultureInfo.InvariantCulture),
                    FootprintDepth=int.Parse(AutonomousLifeMirrorReader.Cell(r,"FootprintDepth"),CultureInfo.InvariantCulture)});
            new BuildingRegistry(definitions);
            return definitions;
        }
    }
}

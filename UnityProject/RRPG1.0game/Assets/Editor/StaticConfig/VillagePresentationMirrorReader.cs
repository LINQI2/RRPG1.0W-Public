using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RRPG.StaticConfig.EditorTools
{
    internal static class VillagePresentationMirrorReader
    {
        internal sealed class Payload
        {
            internal readonly List<VillagePresentation> Presentations = new List<VillagePresentation>();
            internal readonly List<BuildingPresentationBinding> Buildings = new List<BuildingPresentationBinding>();
        }

        internal static Payload Read(string dir, IEnumerable<BuildingDefinition> definitions, IEnumerable<VisualAssetDefinition> assets)
        {
            var result = new Payload();
            bool hasKeys = File.Exists(Path.Combine(dir, "VillagePresentation.json"));
            bool hasBindings = File.Exists(Path.Combine(dir, "BuildingPresentationBinding.json"));
            if (!hasKeys && !hasBindings) return result;
            if (!hasKeys || !hasBindings) throw new InvalidDataException("Village Presentation Mirror incomplete: both tables are required.");
            foreach (var row in AutonomousLifeMirrorReader.ReadTable(dir, "VillagePresentation", new[] { "PresentationKey", "VisualAssetId" }))
                result.Presentations.Add(new VillagePresentation {
                    PresentationKey = AutonomousLifeMirrorReader.Cell(row, "PresentationKey"),
                    VisualAssetId = AutonomousLifeMirrorReader.Cell(row, "VisualAssetId") });
            foreach (var row in AutonomousLifeMirrorReader.ReadTable(dir, "BuildingPresentationBinding", new[] { "BuildingDefinitionId", "Lifecycle", "PresentationKey" }))
                result.Buildings.Add(new BuildingPresentationBinding {
                    BuildingDefinitionId = AutonomousLifeMirrorReader.Cell(row, "BuildingDefinitionId"),
                    Lifecycle = AutonomousLifeMirrorReader.Cell(row, "Lifecycle"),
                    PresentationKey = AutonomousLifeMirrorReader.Cell(row, "PresentationKey") });
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var assetIds = new HashSet<string>(assets.Select(a => a.VisualAssetId), StringComparer.Ordinal);
            foreach (var entry in result.Presentations)
                if (string.IsNullOrEmpty(entry.PresentationKey) || !keys.Add(entry.PresentationKey) || !assetIds.Contains(entry.VisualAssetId))
                    throw new InvalidDataException("Invalid Village Presentation key / VisualAsset reference: " + entry.PresentationKey);
            var definitionIds = new HashSet<string>(definitions.Select(d => d.BuildingDefinitionId), StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in result.Buildings)
                if (!definitionIds.Contains(entry.BuildingDefinitionId) || !keys.Contains(entry.PresentationKey)
                    || (entry.Lifecycle != "UnderConstruction" && entry.Lifecycle != "Completed")
                    || !identities.Add(entry.BuildingDefinitionId + "\n" + entry.Lifecycle))
                    throw new InvalidDataException("Invalid Building Presentation binding: " + entry.BuildingDefinitionId + "/" + entry.Lifecycle);
            return result;
        }
    }
}

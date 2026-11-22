using System;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    public sealed class SocialConfigTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "rrpg-al4-config-" + Guid.NewGuid().ToString("N"));
        private readonly CompileResult baseline;
        public SocialConfigTests()
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "Development", "Config", "RRPG_Social.xlsx"))) repo = repo.Parent;
            Assert.NotNull(repo);
            Directory.CreateDirectory(Path.Combine(root, "Development", "Config"));
            foreach (var name in new[] { "RRPG_AutonomousLife.xlsx", "RRPG_Social.xlsx" })
                File.Copy(Path.Combine(repo.FullName, "Development", "Config", name), Path.Combine(root, "Development", "Config", name));
            baseline = CompilerRunner.Run(CompilerPaths.Resolve(root), "build", true);
            Assert.True(baseline.Success, string.Join("\n", baseline.Diagnostics.Select(d => d.Message)));
        }
        [Fact] public void BuildIsReproducibleAndValidatePasses()
        {
            var dir = baseline.Paths.GeneratedDir;
            var before = Directory.GetFiles(dir).ToDictionary(Path.GetFileName, File.ReadAllBytes);
            Assert.True(CompilerRunner.Run(CompilerPaths.Resolve(root), "build", true).Success);
            foreach (var entry in before) Assert.Equal(entry.Value, File.ReadAllBytes(Path.Combine(dir, entry.Key)));
            Assert.True(CompilerRunner.Run(CompilerPaths.Resolve(root), "validate", false).Success);
        }
        [Theory] [InlineData("SourceRole", "Nobody")] [InlineData("TargetRole", "Nobody")]
        [InlineData("RelationshipChannel", "Trust")] [InlineData("Strength", "Huge")]
        [InlineData("Polarity", "Unknown")] [InlineData("FactType", "missing")]
        public void InvalidRuleRejected(string field, string value)
        { baseline.CompiledSheets.Single(s => s.Spec.SheetName == "RelationshipInterpretationRule").Rows[0].Get(field).Value = value; Invalid(); }
        [Theory] [InlineData("AffinityDeltaWeak")] [InlineData("AffinityDeltaMedium")] [InlineData("AffinityDeltaStrong")]
        public void RequiredCalibration(string key)
        { baseline.CompiledSheets.Single(s => s.Spec.SheetName == "SocialCalibration").Rows.RemoveAll(r => Convert.ToString(r.Get("CalibrationKey").Value) == key); Invalid(); }
        [Theory] [InlineData(0.0)] [InlineData(-1.0)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
        public void InvalidCalibration(double value)
        { baseline.CompiledSheets.Single(s => s.Spec.SheetName == "SocialCalibration").Rows[0].Get("Value").Value = value; Invalid(); }
        [Fact] public void DuplicateCalibrationRejected()
        { var rows = baseline.CompiledSheets.Single(s => s.Spec.SheetName == "SocialCalibration").Rows; rows.Add(rows[0]); Invalid(); }
        [Fact] public void DuplicatePairRuleRejected()
        { var rows = baseline.CompiledSheets.Single(s => s.Spec.SheetName == "RelationshipInterpretationRule").Rows; rows.Add(rows[0]); Invalid(); }
        [Fact] public void ProjectionRequiresRule()
        { baseline.CompiledSheets.Single(s => s.Spec.SheetName == "RelationshipInterpretationRule").Rows.Clear(); Invalid(); }
        [Theory] [InlineData("ActivityType", "Unknown")] [InlineData("PersistentRelationalFactType", "missing")]
        public void ProjectionCrossWorkbookReferenceRejected(string field, string value)
        {
            baseline.CompiledSheets.Single(s => s.Spec.SheetName == "ActivitySocialProjection").Rows[0].Get(field).Value = value;
            var bag = new DiagnosticBag(); CrossReferenceValidator.ValidateAndSort(baseline.CompiledSheets, bag); Assert.True(bag.HasErrors); Invalid();
        }
        [Fact] public void MissingSocialTableRejected()
        { baseline.CompiledSheets.RemoveAll(s => s.Spec.SheetName == "SocialCalibration"); Invalid(); }
        private void Invalid() { var bag = new DiagnosticBag(); SocialValidator.Validate(baseline.CompiledSheets, bag); Assert.True(bag.HasErrors); }
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

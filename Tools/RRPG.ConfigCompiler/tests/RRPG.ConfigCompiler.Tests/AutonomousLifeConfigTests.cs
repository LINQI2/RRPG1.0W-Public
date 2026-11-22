using System;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;
namespace RRPG.ConfigCompiler.Tests
{
    public sealed class AutonomousLifeConfigTests : IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"rrpg-al3-config-tests-"+Guid.NewGuid().ToString("N"));
        private readonly CompileResult baseline;
        public AutonomousLifeConfigTests()
        {
            var repo=new DirectoryInfo(AppContext.BaseDirectory);
            while(repo!=null&&!File.Exists(Path.Combine(repo.FullName,"Development","Config","RRPG_AutonomousLife.xlsx"))) repo=repo.Parent;
            Assert.NotNull(repo);
            Directory.CreateDirectory(Path.Combine(root,"Development","Config"));
            File.Copy(Path.Combine(repo.FullName,"Development","Config","RRPG_AutonomousLife.xlsx"),Path.Combine(root,"Development","Config","RRPG_AutonomousLife.xlsx"));
            File.Copy(Path.Combine(repo.FullName,"Development","Config","RRPG_Social.xlsx"),Path.Combine(root,"Development","Config","RRPG_Social.xlsx"));
            baseline=CompilerRunner.Run(CompilerPaths.Resolve(root),"build",true);
            Assert.True(baseline.Success,string.Join("\n",baseline.Diagnostics.Select(d=>d.Message)));
        }
        [Fact] public void RealWorkbookBuildValidateReproducible()
        {
            Assert.Equal(16,baseline.CompiledSheets.Count);
            var before=Directory.GetFiles(Path.Combine(root,"Development","Generated","StaticConfig")).ToDictionary(p=>Path.GetFileName(p),File.ReadAllText);
            var next=CompilerRunner.Run(CompilerPaths.Resolve(root),"build",true);Assert.True(next.Success);
            foreach(var entry in before)Assert.Equal(entry.Value,File.ReadAllText(Path.Combine(root,"Development","Generated","StaticConfig",entry.Key)));
            Assert.True(CompilerRunner.Run(CompilerPaths.Resolve(root),"validate",false).Success);
        }
        [Theory] [InlineData("PersonBehavior","Value",2.0)] [InlineData("PersonPreference","Value",-2.0)]
        [InlineData("BehaviorChannel","NeutralValue",1.0)] [InlineData("AutonomousLifeCalibration","Value",0.0)]
        public void InvalidNumericDataRejected(string sheet,string field,double value)
        {baseline.CompiledSheets.Single(s=>s.Spec.SheetName==sheet).Rows[0].Get(field).Value=value;AssertInvalid();}
        [Theory] [InlineData("ActivityContextBinding","SourceDomain","Unknown")] [InlineData("ActivityRelationshipBinding","Key","Trust")]
        [InlineData("ActivityPreferenceBinding","Key","unknown.preference")] [InlineData("ActivityProvider","ContextDomain","Unknown")]
        [InlineData("ActivityRepetition","Direction",1L)] [InlineData("ActivityBehaviorBinding","Strength","Impossible")]
        public void GovernedSemanticDataRejected(string sheet,string field,object value)
        {baseline.CompiledSheets.Single(s=>s.Spec.SheetName==sheet).Rows[0].Get(field).Value=value;AssertInvalid();}
        [Fact] public void MissingTableRejected()
        {baseline.CompiledSheets.RemoveAll(s=>s.Spec.SheetName=="Activity");AssertInvalid();}
        [Fact] public void DuplicateIdentityRejected()
        {var s=baseline.CompiledSheets.Single(t=>t.Spec.SheetName=="PersonBehavior");s.Rows.Add(s.Rows[0]);var bag=new DiagnosticBag();CrossReferenceValidator.ValidateAndSort(baseline.CompiledSheets,bag);Assert.True(bag.HasErrors);}
        [Fact] public void MissingRequiredColumnRejected()
        {baseline.CompiledSheets.Single(s=>s.Spec.SheetName=="Activity").Spec.Fields.RemoveAll(f=>f.Field=="BaseDesire");AssertInvalid();}
        [Theory]
        [InlineData("InjuryLight")]
        [InlineData("InjuryHeavy")]
        [InlineData("InjuryDisabled")]
        public void MissingInjuryCalibrationRejected(string key)
        {
            baseline.CompiledSheets.Single(s => s.Spec.SheetName == "AutonomousLifeCalibration").Rows.RemoveAll(r => Convert.ToString(r.Get("CalibrationKey").Value) == key);
            AssertInvalid();
        }

        [Theory]
        [InlineData("InjuryLight")]
        [InlineData("InjuryHeavy")]
        [InlineData("InjuryDisabled")]
        public void InjuryCalibrationMagnitudeValidated(string key)
        {
            var row = baseline.CompiledSheets.Single(s => s.Spec.SheetName == "AutonomousLifeCalibration").Rows.Single(r => Convert.ToString(r.Get("CalibrationKey").Value) == key);
            foreach (var value in new[] { -.1, 1.1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                row.Get("Value").Value = value;
                AssertInvalid();
            }
            foreach (var value in new[] { 0.0, 1.0 })
            {
                row.Get("Value").Value = value;
                var bag = new DiagnosticBag();
                AutonomousLifeValidator.Validate(baseline.CompiledSheets, bag);
                Assert.False(bag.HasErrors, string.Join("\n", bag.Items.Select(d => d.Message)));
            }
        }

        private void AssertInvalid() {var bag=new DiagnosticBag();AutonomousLifeValidator.Validate(baseline.CompiledSheets,bag);Assert.True(bag.HasErrors);}
        public void Dispose() {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}

using System.IO;
using RRPG.ConfigCompiler.Core;
using Xunit;
namespace RRPG.ConfigCompiler.Tests
{
    public class BuildingConfigTests
    {
        private static TestSheet Sheet(int width=2,int depth=2) => new TestSheet("BuildingDefinition")
            .Col("BuildingDefinitionId","id","").Col("DisplayName","string","").Col("Category","string","")
            .Col("FootprintWidth","int",">=1").Col("FootprintDepth","int",">=1").Row("house","房屋","Housing",width,depth);
        [Fact] public void FormalSchemaDeterministicMirrorAndIdentity()
        {
            using(var root=new TestRoot())
            {
                root.WriteWorkbook("RRPG_Buildings.xlsx",Sheet());
                Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);
                var path=Path.Combine(root.GeneratedDir,"BuildingDefinition.json");var bytes=File.ReadAllBytes(path);
                Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);Assert.Equal(bytes,File.ReadAllBytes(path));
                Assert.True(CompilerRunner.Run(root.Paths,"validate",false).Success);
                Assert.Contains("\"BuildingDefinitionId\": \"house\"",File.ReadAllText(path));
            }
        }
        [Theory][InlineData(0,2)][InlineData(2,0)] public void InvalidFootprintDoesNotPublish(int width,int depth)
        {using(var root=new TestRoot()){root.WriteWorkbook("RRPG_Buildings.xlsx",Sheet(width,depth));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);Assert.False(File.Exists(Path.Combine(root.GeneratedDir,"BuildingDefinition.json")));}}
        [Fact] public void DuplicateDefinitionIsRejected()
        {using(var root=new TestRoot()){root.WriteWorkbook("RRPG_Buildings.xlsx",Sheet().Row("house","另一房屋","Housing",2,2));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);}}
        private static TestSheet Binding(string definition="house",string type="Workplace",string profile="ws_farm_01") => new TestSheet("BuildingFunctionBinding")
            .Col("BuildingDefinitionId","ref:BuildingDefinition","key").Col("FunctionSlotId","string","key")
            .Col("FunctionType","string","").Col("FunctionProfileId","string","").Row(definition,"main",type,profile);
        [Fact] public void FunctionReferencesAndDeterministicMirror()
        {using(var root=new TestRoot()){root.WriteWorkbook("RRPG_Buildings.xlsx",Sheet(),Binding());Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);
            var path=Path.Combine(root.GeneratedDir,"BuildingFunctionBinding.json");var first=File.ReadAllBytes(path);Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);Assert.Equal(first,File.ReadAllBytes(path));}}
        [Theory][InlineData("missing","Workplace","ws_farm_01")][InlineData("house","Storage","ws_farm_01")][InlineData("house","Workplace","missing")]
        public void InvalidFunctionBindingDoesNotPublish(string definition,string type,string profile)
        {using(var root=new TestRoot()){root.WriteWorkbook("RRPG_Buildings.xlsx",Sheet(),Binding(definition,type,profile));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);Assert.False(File.Exists(Path.Combine(root.GeneratedDir,"BuildingFunctionBinding.json")));}}
        [Fact] public void DuplicateFunctionSlotFails()
        {using(var root=new TestRoot()){root.WriteWorkbook("RRPG_Buildings.xlsx",Sheet(),Binding().Row("house","main","Workplace","ws_farm_01"));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);}}
    }
}

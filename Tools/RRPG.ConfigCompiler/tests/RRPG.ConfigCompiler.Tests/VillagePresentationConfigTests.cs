using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    public sealed class VillagePresentationConfigTests
    {
        private static TestSheet Definition() => new TestSheet("BuildingDefinition")
            .Col("BuildingDefinitionId","id","").Col("DisplayName","string","").Col("Category","string","")
            .Col("FootprintWidth","int",">=1").Col("FootprintDepth","int",">=1").Row("farm","Farm","Test",3,2);
        private static TestSheet Assets() => new TestSheet("VisualAsset").Col("VisualAssetId","id","")
            .Col("SourceFile","string","").Col("ImportPreset","string","allowed=sprite_point_100")
            .Row("a","Village/a.png","sprite_point_100").Row("b","Village/b.png","sprite_point_100");
        private static TestSheet Key(string asset="a") => new TestSheet("VillagePresentation")
            .Col("PresentationKey","id","").Col("VisualAssetId","ref:VisualAsset","").Row("village.building.farm",asset);
        private static TestSheet Binding(string definition="farm",string lifecycle="Completed",string key="village.building.farm") => new TestSheet("BuildingPresentationBinding")
            .Col("BuildingDefinitionId","ref:BuildingDefinition","key")
            .Col("Lifecycle","string","key;allowed=UnderConstruction|Completed")
            .Col("PresentationKey","ref:VillagePresentation","").Row(definition,lifecycle,key);
        private static void Seed(TestRoot root, TestSheet key=null, TestSheet binding=null)
        {
            root.WriteWorkbook("RRPG_Buildings.xlsx",Definition());root.WriteWorkbook("RRPG_VisualAssets.xlsx",Assets());
            root.WriteWorkbook("RRPG_VillagePresentation.xlsx",key??Key(),binding??Binding());
            root.WriteSourceAsset("Village/a.png","png-a");root.WriteSourceAsset("Village/b.png","png-b");
        }
        [Fact] public void BindingReplacementPreservesSourcesDefinitionsAndAssetManifest()
        {
            using(var root=new TestRoot()) {
                Seed(root);Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);
                var before=Directory.GetFiles(root.GeneratedDir,"*.json").ToDictionary(Path.GetFileName,File.ReadAllText);
                root.WriteWorkbook("RRPG_VillagePresentation.xlsx",Key("b"),Binding());Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);
                foreach(var pair in before) if(pair.Key!="VillagePresentation.json") Assert.Equal(pair.Value,File.ReadAllText(Path.Combine(root.GeneratedDir,pair.Key)));
            }
        }
        [Theory][InlineData("missing","Completed","village.building.farm")][InlineData("farm","Invalid","village.building.farm")][InlineData("farm","Completed","missing")]
        public void InvalidBindingFailsBeforePublishing(string definition,string lifecycle,string key)
        {using(var root=new TestRoot()){Seed(root,binding:Binding(definition,lifecycle,key));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);Assert.False(Directory.Exists(root.GeneratedDir));}}
        [Fact] public void MissingSharedAssetFailsClosed()
        {using(var root=new TestRoot()){Seed(root,key:Key("missing"));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);Assert.False(Directory.Exists(root.GeneratedDir));}}
        [Fact] public void DuplicateDefinitionLifecycleIsRejected()
        {using(var root=new TestRoot()){Seed(root,binding:Binding().Row("farm","Completed","village.building.farm"));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);}}
        [Fact] public void MissingSourcePreservesPreviouslyPublishedMirrors()
        {
            using(var root=new TestRoot()){Seed(root);Assert.True(CompilerRunner.Run(root.Paths,"build",true).Success);
                var before=Directory.GetFiles(root.GeneratedDir,"*.json").ToDictionary(Path.GetFileName,File.ReadAllBytes);
                File.Delete(Path.Combine(root.Root,"Development/SourceAssets/Village/a.png"));Assert.False(CompilerRunner.Run(root.Paths,"build",true).Success);
                foreach(var pair in before) Assert.Equal(pair.Value,File.ReadAllBytes(Path.Combine(root.GeneratedDir,pair.Key)));}
        }
    }
}

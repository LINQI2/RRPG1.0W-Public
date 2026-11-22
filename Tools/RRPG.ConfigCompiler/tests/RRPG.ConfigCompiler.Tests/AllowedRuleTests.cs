using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// T4 扩展语法：Row 3 <c>allowed=V1|V2</c>（声明式取值域）的契约。
    /// 语义边界刻意收窄：只适用于 string 列，且默认值本身必须是集合成员。
    /// </summary>
    public class AllowedRuleTests
    {
        [Fact]
        public void AllowedRule_UnknownMember_IsHardError_WithCellLocation()
        {
            using (var root = new TestRoot())
            {
                TestSupport.WriteSourceArt(root, "Enemy/a.png");
                root.WriteWorkbook("RRPG_T4.xlsx",
                    TestSupport.MonsterSheet(),
                    TestSupport.AllowedValueSheet(new object[] { "art_a", "Enemy/a.png", "Sprite" }));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);

                var diag = result.Diagnostics.First(d => d.Code == DiagnosticCodes.DatAllowedViolation);
                Assert.Equal("SampleTool", diag.Sheet);
                Assert.Equal("ImportPreset", diag.Column);
                Assert.Equal(5, diag.Row);
                Assert.Contains("allowed=UISprite", diag.Message);
            }
        }

        [Fact]
        public void AllowedRule_AcceptsDeclaredMember()
        {
            using (var root = new TestRoot())
            {
                TestSupport.WriteSourceArt(root, "Enemy/a.png");
                root.WriteWorkbook("RRPG_T4.xlsx",
                    TestSupport.MonsterSheet(),
                    TestSupport.AllowedValueSheet(new object[] { "art_a", "Enemy/a.png", "UISprite" }));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
            }
        }

        [Fact]
        public void AllowedRule_OnNonTextColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("MaxHP", "int", "allowed=10|20", "最大生命值")
                    .Row("mon_001", 10));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.SchAllowedNotApplicable, TestSupport.ErrorCodes(result));
            }
        }

        [Fact]
        public void AllowedRule_DefaultValueMustBeMember()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Faction", "string", "default=enemy;allowed=neutral|ally", "阵营")
                    .Row("mon_001", ""));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.SchAllowedDefaultNotMember, TestSupport.ErrorCodes(result));
            }
        }

        [Fact]
        public void AllowedRule_EmptyMember_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Faction", "string", "allowed=neutral||ally", "阵营")
                    .Row("mon_001", "neutral"));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.SchAllowedEmptyMember, TestSupport.ErrorCodes(result));
            }
        }

        [Fact]
        public void AllowedRule_MissingValue_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Faction", "string", "allowed=", "阵营")
                    .Row("mon_001", "neutral"));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.SchRuleInvalidOperand, TestSupport.ErrorCodes(result));
            }
        }
    }
}

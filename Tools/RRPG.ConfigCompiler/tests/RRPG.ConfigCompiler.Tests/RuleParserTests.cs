using System.Collections.Generic;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>Rule / Default 行（Row 3）与 Type（Row 2）解析器测试 —— spike-a-rules/2。</summary>
    public class RuleParserTests
    {
        private static (FieldSpec Spec, List<Diagnostic> Diags) Parse(string type, string rules)
        {
            var bag = new DiagnosticBag();
            var spec = RuleParser.Parse(type, rules, 1, RawSheet.TypeRow, RawSheet.RuleRow,
                new DiagBag(bag, DiagContext.Of("Authoring/Test.xlsx", "Test", 0)), "TestField");
            return (spec, bag.Items.ToList());
        }

        [Fact]
        public void GrammarVersion_IsV3()
        {
            Assert.Equal("spike-a-rules/3", RuleParser.GrammarVersion);
        }

        [Fact]
        public void Parses_AllSupportedTypeTokens()
        {
            Assert.Equal(FieldKind.Id, Parse("id", "").Spec.Kind);
            Assert.Equal(FieldKind.Text, Parse("string", "").Spec.Kind);
            Assert.Equal(FieldKind.Int, Parse("int", "").Spec.Kind);
            Assert.Equal(FieldKind.Float, Parse("float", "").Spec.Kind);
            Assert.Equal(FieldKind.Bool, Parse("bool", "").Spec.Kind);

            var refSpec = Parse("ref:FixtureParent", "unique").Spec;
            Assert.Equal(FieldKind.Ref, refSpec.Kind);
            Assert.Equal("FixtureParent", refSpec.TypeArgument);
        }

        [Fact]
        public void EmptyRuleCell_DefaultsToRequired_WithoutDiagnostics()
        {
            var (spec, diags) = Parse("int", "");
            Assert.Empty(diags);
            Assert.True(spec.Required);
            Assert.False(spec.Optional);
        }

        [Fact]
        public void Id_IsIntrinsicallyRequired_AndIsPrimaryKey()
        {
            var (spec, diags) = Parse("id", "");
            Assert.Empty(diags);
            Assert.True(spec.Required);
            Assert.True(spec.IsPrimaryKey);
            Assert.False(spec.KeyDeclared);
            Assert.False(spec.IsCompositeKeyComponent);
        }

        [Fact]
        public void KeyToken_DeclaresCompositeKeyComponent()
        {
            var (spec, diags) = Parse("ref:FixtureParent", "key");
            Assert.Empty(diags);
            Assert.True(spec.KeyDeclared);
            Assert.True(spec.IsCompositeKeyComponent);
            Assert.False(spec.IsPrimaryKey);
        }

        [Fact]
        public void Ref_IsIntrinsicallyRequired()
        {
            var (spec, _) = Parse("ref:FixtureParent", "");
            Assert.True(spec.Required);
            Assert.False(spec.IsPrimaryKey);
            Assert.False(spec.KeyDeclared);
        }

        [Fact]
        public void Optional_MakesFieldNotRequired()
        {
            var (spec, diags) = Parse("string", "optional");
            Assert.Empty(diags);
            Assert.True(spec.Optional);
            Assert.False(spec.Required);
        }

        [Fact]
        public void DefaultToken_MakesFieldNotRequired()
        {
            var (spec, diags) = Parse("string", "default=enemy");
            Assert.Empty(diags);
            Assert.True(spec.HasDefault);
            Assert.Equal("enemy", spec.DefaultRaw);
            Assert.False(spec.Required);
        }

        [Fact]
        public void RequiredToken_IsRejected_WithV3Hint()
        {
            var (_, diags) = Parse("int", "required");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchRequiredTokenRemoved);
            Assert.Equal(Severity.Error, d.Severity);
            Assert.Equal("required", d.Rule);
            Assert.Contains("默认", d.Message);
        }

        [Fact]
        public void UniqueToken_IsRejected_WithKeyHint()
        {
            var (_, diags) = Parse("int", "unique");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchUniqueTokenRemoved);
            Assert.Equal(Severity.Error, d.Severity);
            Assert.Equal("unique", d.Rule);
            Assert.Contains("key", d.Message);
            Assert.Contains("尚未", d.Message);   // 明确说明"non-key 单列 unique constraint 未设计"
        }

        [Fact]
        public void LegacyDefaultArrow_IsRejected_WithDefaultEqualsHint()
        {
            var (_, diags) = Parse("string", "default=>enemy");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchLegacyDefaultArrow);
            Assert.Equal(Severity.Error, d.Severity);
            Assert.Equal("default=>enemy", d.Rule);
            Assert.Contains("default=enemy", d.Message);
        }

        [Fact]
        public void IdWithKeyToken_IsHardError()
        {
            var (_, diags) = Parse("id", "key");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchIdWithKey);
            Assert.Equal(Severity.Error, d.Severity);
            Assert.Equal("key", d.Rule);
        }

        [Fact]
        public void EnumType_IsRejected()
        {
            var (_, diags) = Parse("enum:FixtureKind", "");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchTypeUnknown);
            Assert.Equal("enum:FixtureKind", d.Rule);
        }

        [Fact]
        public void UnknownRuleToken_IsHardError()
        {
            var (_, diags) = Parse("int", "uniquee");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchRuleUnknownToken);
            Assert.Equal(Severity.Error, d.Severity);
            Assert.Equal("uniquee", d.Rule);
            Assert.Equal("TestField", d.Column);
        }

        [Fact]
        public void DuplicateRuleToken_IsHardError()
        {
            var (_, diags) = Parse("int", "key;key");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchRuleDuplicateToken);
            Assert.Equal(Severity.Error, d.Severity);
        }

        [Fact]
        public void OptionalWithDefault_IsConflict()
        {
            var (_, diags) = Parse("bool", "optional;default=true");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleConflict);
        }

        [Fact]
        public void DefaultOnId_IsConflict()
        {
            var (_, diags) = Parse("id", "default=x");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleConflict);
        }

        [Fact]
        public void DefaultOnRef_IsConflict()
        {
            var (_, diags) = Parse("ref:FixtureParent", "default=P_A");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleConflict);
        }

        [Fact]
        public void OptionalOnId_IsConflict()
        {
            var (_, diags) = Parse("id", "optional");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleConflict);
        }

        [Fact]
        public void WhitespaceAfterOperator_IsHardError()
        {
            var (_, diags) = Parse("int", ">= 1");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchRuleOperatorWhitespace);
            Assert.Equal(">=", d.Rule);
        }

        [Fact]
        public void DefaultWithWhitespace_IsHardError()
        {
            var (_, diags) = Parse("string", "default= enemy");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleOperatorWhitespace);
        }

        [Fact]
        public void MinGreaterThanMax_IsConflict()
        {
            var (_, diags) = Parse("int", ">=10;<=1");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleConflict);
        }

        [Fact]
        public void RangeOnString_IsNotApplicable()
        {
            var (_, diags) = Parse("string", ">=1");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleNotApplicable);
        }

        [Fact]
        public void DefaultTypeMismatch_IsHardError()
        {
            var (_, diags) = Parse("int", "default=enemy");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchRuleDefaultTypeMismatch);
        }

        [Fact]
        public void TypeTokenInRuleRow_IsHardError()
        {
            var (_, diags) = Parse("string", "int");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchTypeTokenInRuleRow);
        }

        [Fact]
        public void MultipleTypeTokens_IsHardError()
        {
            var (_, diags) = Parse("int float", "");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchTypeMultiple);
        }

        [Fact]
        public void UnknownTypeToken_IsHardError()
        {
            var (_, diags) = Parse("integer", "");
            var d = Assert.Single(diags, x => x.Code == DiagnosticCodes.SchTypeUnknown);
            Assert.Equal("integer", d.Rule);
        }

        [Fact]
        public void EmptyTypeCell_IsHardError()
        {
            var (_, diags) = Parse("", "");
            Assert.Contains(diags, x => x.Code == DiagnosticCodes.SchTypeRowEmpty);
        }

        [Fact]
        public void SplitTokens_KeepsOrderAndDropsEmptyParts()
        {
            var tokens = RuleParser.SplitTokens(" key ;>=1;\n  <=10 ;; optional ");
            Assert.Equal(new[] { "key", ">=1", "<=10", "optional" }, tokens);
        }

        [Fact]
        public void ColumnLetter_MapsBeyondZ()
        {
            Assert.Equal("A", RuleParser.ColumnLetter(1));
            Assert.Equal("H", RuleParser.ColumnLetter(8));
            Assert.Equal("AA", RuleParser.ColumnLetter(27));
        }
    }
}

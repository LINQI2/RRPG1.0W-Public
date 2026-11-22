using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>数据校验：required / unique / min-max / 空ID / 重复ID / 非法 int。</summary>
    public class DataValidationTests
    {
        internal static CompileResult Build(TestRoot root)
        {
            return CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
        }

        [Fact]
        public void RequiredEmpty_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("Name", "string", "")
                    .Row("mon_001", null));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatRequiredEmpty);
            }
        }

        [Fact]
        public void EmptyId_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "unique")
                    .Col("MaxHP", "int", "required")
                    .Row(null, 20));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatIdEmpty);
            }
        }

        [Fact]
        public void DuplicateKey_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "unique")
                    .Col("MaxHP", "int", "required")
                    .Row("mon_001", 20)
                    .Row("mon_001", 30));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatKeyDuplicate);
            }
        }

        [Fact]
        public void InvalidInt_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("MaxHP", "int", "")
                    .Row("mon_001", "二十"));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatTypeMismatch);
            }
        }

        [Fact]
        public void FloatValueInIntColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "unique")
                    .Col("MaxHP", "int", "required")
                    .Row("mon_001", 20.5));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatTypeMismatch);
            }
        }

        [Fact]
        public void MinAndMaxViolations_AreHardErrors()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("MaxHP", "int", ">=10;<=100")
                    .Row("mon_001", 5)
                    .Row("mon_002", 500));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatMinViolation);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatMaxViolation);
            }
        }
    }
}

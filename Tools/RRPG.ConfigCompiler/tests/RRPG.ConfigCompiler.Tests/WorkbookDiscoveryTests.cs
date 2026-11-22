using System.Collections.Generic;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>Workbook 发现：找 xlsx、跳过临时/隐藏文件、确定性排序、无工作簿报错。</summary>
    public class WorkbookDiscoveryTests
    {
        private static IEnumerable<KeyValuePair<string, string>> Roots(string authoringDir)
        {
            yield return new KeyValuePair<string, string>("Authoring", authoringDir);
        }

        [Fact]
        public void Discover_FindsXlsx_AndSkipsTempLockFile()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Monsters.xlsx", Fixtures.MinimalMonsterSheet());
                root.WriteTempLockFile("~$RRPG_Monsters.xlsx");
                root.WriteTempLockFile("_draft.xlsx");

                var bag = new DiagnosticBag();
                var found = WorkbookDiscovery.Discover(Roots(root.AuthoringDir), bag);

                var only = Assert.Single(found);
                Assert.Equal("Authoring/RRPG_Monsters.xlsx", only.DisplayPath);

                // ~$ 临时锁文件必须被跳过并留下可审计诊断
                Assert.Contains(bag.Items, d => d.Code == DiagnosticCodes.WbTempFileSkipped);
            }
        }

        [Fact]
        public void Discover_NoWorkbook_IsLegalM0State()
        {
            using (var root = new TestRoot())
            {
                var bag = new DiagnosticBag();
                var found = WorkbookDiscovery.Discover(Roots(root.AuthoringDir), bag);

                // M0：还没有任何已迁移领域是合法状态（FDS §3.2 / §7），只记 Warning，不阻断。
                Assert.Empty(found);
                Assert.Contains(bag.Items, d => d.Code == DiagnosticCodes.WbNoWorkbookFound);
                Assert.False(bag.HasErrors);
                Assert.Equal(1, bag.WarningCount);
            }
        }

        [Fact]
        public void Discover_OrdersByDisplayPath()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("b_second.xlsx", Fixtures.MinimalMonsterSheet("Second"));
                root.WriteWorkbook("a_first.xlsx", Fixtures.MinimalMonsterSheet("First"));

                var bag = new DiagnosticBag();
                var found = WorkbookDiscovery.Discover(Roots(root.AuthoringDir), bag);

                Assert.Equal(2, found.Count);
                Assert.Equal("Authoring/a_first.xlsx", found[0].DisplayPath);
                Assert.Equal("Authoring/b_second.xlsx", found[1].DisplayPath);
            }
        }
    }
}

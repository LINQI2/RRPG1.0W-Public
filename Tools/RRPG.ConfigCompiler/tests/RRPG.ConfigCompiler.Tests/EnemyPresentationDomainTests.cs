using System.IO;
using System.Linq;
using System.Text;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// M3 Slice 1A：EnemyPresentation / VisualAsset 领域接线。
    ///
    /// 断言的是「领域契约」而不是实现细节：
    ///   - Mirror 必须产出 EnemyPresentation.json / VisualAsset.json / AssetManifest.json；
    ///   - SourceFile 事实（存在 / 路径 / 扩展名 / 预设）不成立时 **Hard Error + 0 更新**；
    ///   - 同输入同输出（逐字节），源图内容变化必须让 Mirror 变化（换图 = 重新 build）。
    /// </summary>
    public class EnemyPresentationDomainTests
    {
        private const string SourceFile = "Enemy/test_body.png";

        [Fact]
        public void Domain_ProducesMirrorFiles_AndManifest()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset(SourceFile, "png-bytes-v1");
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.MinimalMonsterSheet(),
                    Fixtures.EnemyPresentationSheet(Fixtures.DefaultEnemyPresentationRows()),
                    Fixtures.VisualAssetSheet("va_enemy_body_placeholder", SourceFile));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.True(run.Success, string.Join(" | ", run.Diagnostics.Select(d => d.ToString())));

                Assert.True(File.Exists(root.GeneratedFile("EnemyPresentation.json")));
                Assert.True(File.Exists(root.GeneratedFile("VisualAsset.json")));
                Assert.True(File.Exists(root.GeneratedFile("AssetManifest.json")));

                string enemy = root.ReadGenerated("EnemyPresentation.json");
                Assert.Contains("\"rowCount\": 2", enemy);
                Assert.Contains("\"MonsterId\": \"mon_001\"", enemy);
                Assert.Contains("\"VisualAssetId\": \"va_enemy_body_placeholder\"", enemy);

                string asset = root.ReadGenerated("VisualAsset.json");
                Assert.Contains("\"SourceFile\": \"Enemy/test_body.png\"", asset);
                Assert.Contains("\"ImportPreset\": \"sprite_point_100\"", asset);

                string manifest = root.ReadGenerated("AssetManifest.json");
                Assert.Contains("\"assetCount\": 1", manifest);
                Assert.Contains("\"VisualAssetId\": \"va_enemy_body_placeholder\"", manifest);
                Assert.Contains("\"SourceFile\": \"Enemy/test_body.png\"", manifest);
                Assert.Contains("\"SourceSha256\": \"", manifest);
                Assert.Contains("\"ManagedAssetPath\": \"Assets/Generated/StaticConfig/Assets/Enemy/test_body.png\"", manifest);
                Assert.DoesNotContain("D:/", manifest);   // 绝不泄露机器绝对路径
                Assert.DoesNotContain("C:/", manifest);
            }
        }

        [Fact]
        public void WithoutVisualAssetSheet_NoManifest_AndNoExtraOutput()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.True(run.Success, string.Join(" | ", run.Diagnostics.Select(d => d.ToString())));
                Assert.Null(run.AssetManifest);
                Assert.False(File.Exists(root.GeneratedFile("AssetManifest.json")));
                Assert.False(File.Exists(root.GeneratedFile("VisualAsset.json")));
            }
        }

        [Fact]
        public void VisualAsset_MissingSourceColumns_IsShapeError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.VisualAssetSheetWithoutSourceColumns());

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatVisualAssetShape);
                Assert.False(File.Exists(root.GeneratedFile("VisualAsset.json")));   // Hard Error → 0 更新
                Assert.False(File.Exists(root.GeneratedFile("AssetManifest.json")));
            }
        }

        [Fact]
        public void VisualAsset_SourceFileNotOnDisk_IsHardError()
        {
            using (var root = new TestRoot())
            {
                // 故意不放源图
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.VisualAssetSheet("va_missing", "Enemy/does_not_exist.png"));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatSourceArtMissing);
                Assert.False(File.Exists(root.GeneratedFile("AssetManifest.json")));
            }
        }

        [Fact]
        public void VisualAsset_InvalidImportPreset_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset(SourceFile);

                // 已知预设之外的取值：第一道防线是 XLSX Row 3 的 allowed=（DAT012），
                // 第二道防线是 Compiler 的预设表（DAT015）—— 本测试用「没写 allowed=」的语料覆盖第二道。
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.VisualAssetSheetWithoutAllowedRule("va_bad_preset", SourceFile, "sprite_bilinear_999"));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatUnsupportedImportPreset);
                Assert.False(File.Exists(root.GeneratedFile("AssetManifest.json")));
            }
        }

        [Fact]
        public void VisualAsset_PresetOutsideDeclaredAllowedSet_IsAllowedViolation()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset(SourceFile);

                // 声明式取值域（Row 3 allowed=）是主防线：写错值应报 DAT012，而不是静默通过。
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.VisualAssetSheet("va_bad_preset", SourceFile, "sprite_bilinear_999"));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatAllowedViolation);
                Assert.False(File.Exists(root.GeneratedFile("AssetManifest.json")));
            }
        }

        [Fact]
        public void VisualAsset_PathEscape_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.VisualAssetSheet("va_escape", "../../UnityProject/escape.png"));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatSourceArtPathEscape);
            }
        }

        [Fact]
        public void VisualAsset_NonPngExtension_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset("Enemy/raw.jpg");
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.VisualAssetSheet("va_jpg", "Enemy/raw.jpg"));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatSourceArtExtension);
            }
        }

        [Fact]
        public void EnemyPresentation_UnresolvedRefs_AreHardErrors()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset(SourceFile);
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.MinimalMonsterSheet(),
                    Fixtures.VisualAssetSheet("va_enemy_body_placeholder", SourceFile),
                    Fixtures.EnemyPresentationSheet(
                        new object[] { "mon_001", "va_enemy_body_placeholder" },
                        new object[] { "mon_999_ghost", "va_nope" }));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(run.Success);
                Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void Manifest_IsDeterministic_ByteIdentical_AndUntouchedOnRepeat()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset(SourceFile, "png-bytes-v1");
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.MinimalMonsterSheet(),
                    Fixtures.EnemyPresentationSheet(Fixtures.DefaultEnemyPresentationRows()),
                    Fixtures.VisualAssetSheet("va_enemy_body_placeholder", SourceFile));

                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success, string.Join(" | ", first.Diagnostics.Select(d => d.ToString())));

                string manifestPath = root.GeneratedFile("AssetManifest.json");
                byte[] bytes1 = File.ReadAllBytes(manifestPath);
                System.DateTime mtime1 = File.GetLastWriteTimeUtc(manifestPath);

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success);

                Assert.Equal(bytes1, File.ReadAllBytes(manifestPath));
                Assert.Equal(mtime1, File.GetLastWriteTimeUtc(manifestPath));
                Assert.Equal("unchanged", second.Outputs.Single(o => o.Name == "AssetManifest.json").Action);

                // LF / 无 BOM / 无时间戳字段
                Assert.DoesNotContain((byte)0x0D, bytes1);
                Assert.False(bytes1.Length >= 3 && bytes1[0] == 0xEF && bytes1[1] == 0xBB && bytes1[2] == 0xBF);
                string text = Encoding.UTF8.GetString(bytes1);
                Assert.DoesNotContain("timestamp", text);
                Assert.DoesNotContain("builtAt", text);
            }
        }

        [Fact]
        public void SourceImageContentChange_MakesMirrorStale_UntilRebuild()
        {
            using (var root = new TestRoot())
            {
                root.WriteSourceAsset(SourceFile, "png-bytes-v1");
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.MinimalMonsterSheet(),
                    Fixtures.EnemyPresentationSheet(Fixtures.DefaultEnemyPresentationRows()),
                    Fixtures.VisualAssetSheet("va_enemy_body_placeholder", SourceFile));

                var built = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(built.Success, string.Join(" | ", built.Diagnostics.Select(d => d.ToString())));
                string before = root.ReadGenerated("AssetManifest.json");

                // 换图（内容变、路径不变）：Mirror 必须判定为 stale（validate 只读，不写盘）
                root.WriteSourceAsset(SourceFile, "png-bytes-v2");
                var stale = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.False(stale.Success);
                Assert.Contains(stale.Diagnostics, d => d.Code == DiagnosticCodes.MirrorStale);

                // 重新 build 之后恢复一致
                var rebuilt = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(rebuilt.Success, string.Join(" | ", rebuilt.Diagnostics.Select(d => d.ToString())));
                Assert.Equal("updated", rebuilt.Outputs.Single(o => o.Name == "AssetManifest.json").Action);
                Assert.NotEqual(before, root.ReadGenerated("AssetManifest.json"));

                var clean = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.True(clean.Success, string.Join(" | ", clean.Diagnostics.Select(d => d.ToString())));
            }
        }
    }
}

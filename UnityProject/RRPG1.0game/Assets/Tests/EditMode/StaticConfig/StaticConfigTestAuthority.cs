using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// 测试专用的 **Authority 读取器**（WP-Encounter-1A-R）。
    ///
    /// 为什么需要它：长期 Regression Test 应该保护**系统契约**，而不是把制作人当时的
    /// 内容快照（「一定有 3 只怪」「第三只一定叫 mon_003_wolf」）当成永远不变的规则。
    /// 制作人只要正常增加第 7 / 第 20 / 第 100 只怪，快照型断言就会红灯 —— 那不是回归。
    ///
    /// 因此本类把「当前正式内容」从 **Canonical Mirror** 推导出来：
    ///   - Mirror 是 Authoring XLSX 的确定性重建结果，其正确性由 `rrpg-config validate` 证明
    ///     （`Authoring == Mirror`，见 FDS §7.2 R5）；
    ///   - 测试改为断言「生成根 == Mirror」这种**一致性契约**，内容数量变化时自动跟随。
    ///
    /// 边界：本类只读 Mirror（只读文件），不写任何东西、不修改 Authority、不进入产品运行路径。
    /// 需要固定输入才能验证的算法用例（例如「BattleSystem 按 list 顺序部署」）应当自建
    /// **测试 Fixture** 定义，而不是引用生产内容。
    /// </summary>
    internal static class StaticConfigTestAuthority
    {
        /// <summary>Mirror 文件的绝对路径（Development/Generated/StaticConfig/&lt;file&gt;）。</summary>
        public static string MirrorPath(string fileName)
        {
            return Path.Combine(
                RRPG.StaticConfig.EditorTools.StaticConfigBuilder.RepoRoot(),
                RRPG.StaticConfig.EditorTools.StaticConfigBuilder.MirrorRelativeDir
                    .Replace('/', Path.DirectorySeparatorChar),
                fileName);
        }

        /// <summary>读取 Mirror 文件文本（不存在即失败：Mirror 是测试的证据来源）。</summary>
        public static string ReadMirror(string fileName)
        {
            string path = MirrorPath(fileName);
            Assert.IsTrue(File.Exists(path), "缺少 Canonical Mirror：" + fileName + "（请先运行 RRPG_一键导表.cmd）");
            return File.ReadAllText(path, new UTF8Encoding(false));
        }

        /// <summary>
        /// 当前正式 Monster 集合，顺序 = Canonical 顺序（从 <c>Monster.json</c> 的 rows 派生）。
        ///
        /// 这是**内容一致性测试**的期望来源：内容由制作人维护，测试只要求「Registry / 生成根
        /// 必须与 Mirror 一致」，不要求数量或某个具体 ID 永远不变。
        /// </summary>
        public static List<string> AuthorityMonsterIds()
        {
            string rows = RowsBlock(ReadMirror("Monster.json"), "Monster.json");
            var ids = new List<string>();

            const string key = "\"MonsterId\": \"";
            int at = 0;
            while (true)
            {
                at = rows.IndexOf(key, at, StringComparison.Ordinal);
                if (at < 0) break;

                int start = at + key.Length;
                int close = rows.IndexOf('"', start);
                Assert.Greater(close, start, "Monster.json 的 MonsterId 字面量必须闭合");

                ids.Add(rows.Substring(start, close - start));
                at = close;
            }

            Assert.Greater(ids.Count, 0, "当前 Authority 至少应有 1 只怪物（否则本组契约测试失去意义）");
            return ids;
        }

        /// <summary>Mirror 中某张表的 rowCount（与 Builder 消费的是同一份产物）。</summary>
        public static int MirrorRowCount(string sheetName)
        {
            string json = ReadMirror(sheetName + ".json");
            const string key = "\"rowCount\": ";

            int at = json.IndexOf(key, StringComparison.Ordinal);
            Assert.Greater(at, -1, sheetName + ".json 必须含 rowCount");

            int start = at + key.Length;
            int end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;

            Assert.Greater(end, start, sheetName + ".json 的 rowCount 必须是数字");
            return int.Parse(json.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>取出 rows 数组的正文（不含外层方括号）。</summary>
        private static string RowsBlock(string json, string fileName)
        {
            int anchor = json.IndexOf("\"rows\": [", StringComparison.Ordinal);
            Assert.Greater(anchor, -1, fileName + " 必须包含 rows 数组");

            int close = json.LastIndexOf(']');
            Assert.Greater(close, anchor, fileName + " 的 rows 数组必须完整");

            return json.Substring(anchor, close - anchor);
        }
    }
}

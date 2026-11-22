using System.Collections.Generic;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// M0 测试语料（**只存在于测试工程内**，FDS §7 / 用户 M0 §6）。
    ///
    /// 命名刻意与任何正式领域无关（Sample / Second）：M0 不建立任何正式领域，
    /// 因此测试证明的是**通用工具链**语义，而不是某个领域的业务字段。
    /// </summary>
    internal static class SampleSheets
    {
        /// <summary>单列主键（id）Sheet：3 行，含 int / string 两类字段与 &gt;= 规则。</summary>
        public static TestSheet Sample()
        {
            return new TestSheet("Sample")
                .Col("SampleId", "id", "", "样例ID")
                .Col("Title", "string", "", "标题")
                .Col("Amount", "int", ">=0", "数量")
                .Row("sample_001", "甲", 20)
                .Row("sample_002", "乙", 15)
                .Row("sample_003", "丙", 24);
        }

        /// <summary>复合主键（key）Sheet：用于验证复合身份 + 物理行序无关的确定性排序。</summary>
        public static TestSheet Second()
        {
            return new TestSheet("Second")
                .Col("GroupId", "string", "key", "组ID")
                .Col("Seq", "int", "key", "序号")
                .Col("Note", "string", "optional", "备注")
                .Row("g1", 2, "second")
                .Row("g1", 1, "first");
        }

        /// <summary>两个 Sheet 的工作簿（默认验收语料）。</summary>
        public static TestSheet[] Workbook()
        {
            return new List<TestSheet> { Sample(), Second() }.ToArray();
        }
    }
}

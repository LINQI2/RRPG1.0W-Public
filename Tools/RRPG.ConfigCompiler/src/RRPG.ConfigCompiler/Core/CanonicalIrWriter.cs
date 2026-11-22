using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Canonical Mirror 写出（FDS §7：Generated Review Mirror，进 Git、只由 Compiler 生成）。
    ///
    /// 定位声明（必须随产物一起出现在文件首部，FDS §7.2 R1）：
    ///   `_generated.doNotEdit = true` + tool + formatVersion + sourceSha256（输入指纹，R4）。
    ///
    /// 格式约束：
    ///   - UTF-8（无 BOM）、LF 换行、2 空格缩进（由 JsonWriterLite 保证）；
    ///   - 顶层键顺序固定；行内键顺序 = **机器字段名升序**（不排序的旧行为见 v2 说明）；
    ///   - rows 已按 key 排序（与 Excel 物理行序无关）；
    ///   - 不写入任何时间戳 / 机器路径 / 构建耗时（否则产生无意义 diff）。
    ///
    /// v2（T4.1，Producer UX）：Canonical 输出**与列序、中文表头完全无关**：
    ///   - columns 按机器字段名（Ordinal）排序；
    ///   - rows 的键同样按机器字段名排序（不再按物理列序）；
    ///   - 不再写入 header / sourceColumn —— 它们描述「作者怎么排版」，不是配置语义；
    ///   - 中文表头与行备注留在 XLSX（唯一人工 Authoring Authority）。
    ///
    /// 结果：**把列换个位置、插一列、改中文表头，Canonical 文件逐字节不变**（由列序测试固定）。
    /// </summary>
    public static class CanonicalIrWriter
    {
        /// <summary>Mirror 格式版本（Wave 0 V0.1 冻结）。</summary>
        public const string IrVersion = "rrpg-static-config-mirror/1";

        /// <summary>生成工具标识（写入 <c>_generated.tool</c>）。</summary>
        public const string ToolName = "RRPG.ConfigCompiler";

        /// <summary>输出文件相对名（例如 "Monster.json"）。</summary>
        public static string OutputName(CompiledSheet sheet)
        {
            return sheet.Spec.SheetName + ".json";
        }

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string Write(CompiledSheet sheet)
        {
            // 正文（不含 _generated 头）：确定性，且与列序 / 中文表头 / 行备注无关。
            var bodyWriter = new JsonWriterLite();
            bodyWriter.BeginObject();
            WriteBody(bodyWriter, sheet);
            bodyWriter.EndObject();
            string body = bodyWriter.ToText();

            string bodySha256 = OutputCommitter.Sha256(Utf8NoBom.GetBytes(body));

            var w = new JsonWriterLite();
            w.BeginObject();

            // FDS §7.2 R1：首部 DO NOT EDIT 标识。
            // FDS §7.2 R4：输入指纹 = 当前 Authoring 的**语义内容**指纹（与排版无关），
            // 供 validate 做 mirror-stale 判定；纯排版变化（换列序 / 改中文表头 / 加行备注）
            // 不产生 Mirror churn。
            w.Name("_generated");
            w.BeginObject();
            w.Name("doNotEdit");
            w.Value(true);
            w.Member("tool", ToolName);
            w.Member("formatVersion", IrVersion);
            w.Member("sourceSha256", bodySha256);
            w.Member("notice",
                "本文件由 RRPG ConfigCompiler 生成，禁止手工编辑；"
                + "唯一人工 Authoring Authority 是 XLSX（Development/Config）。");
            w.EndObject();

            WriteBody(w, sheet);
            w.EndObject();
            return w.ToText();
        }

        /// <summary>Canonical 正文（不含 _generated 头）：只描述配置语义。</summary>
        private static void WriteBody(JsonWriterLite w, CompiledSheet sheet)
        {
            var spec = sheet.Spec;

            w.Member("irVersion", IrVersion);

            w.Name("source");
            w.BeginObject();
            w.Member("workbook", spec.Workbook);
            w.Member("sheet", spec.SheetName);
            w.Member("machineFieldRow", RawSheet.MachineFieldRow);
            w.Member("typeRow", RawSheet.TypeRow);
            w.Member("ruleRow", RawSheet.RuleRow);
            w.Member("displayHeaderRow", RawSheet.DisplayHeaderRow);
            w.Member("firstDataRow", RawSheet.FirstDataRow);
            w.EndObject();

            w.Name("key");
            w.BeginArray();
            foreach (var f in spec.KeyFields) w.Value(f.Field);
            w.EndArray();

            w.Name("columns");
            w.BeginArray();
            foreach (var f in spec.Fields.OrderBy(x => x.Field, StringComparer.Ordinal))
            {
                w.BeginObject();
                w.Member("field", f.Field);
                w.Member("type", f.RawType);
                w.Member("key", spec.KeyFields.Contains(f));

                w.Name("rules");
                w.BeginArray();
                foreach (var token in f.RuleTokens) w.Value(token);
                w.EndArray();

                w.EndObject();
            }
            w.EndArray();

            w.Member("rowCount", sheet.Rows.Count);

            w.Name("rows");
            w.BeginArray();
            foreach (var row in sheet.Rows)
            {
                w.BeginObject();
                foreach (var cell in row.Cells.OrderBy(c => c.Field.Field, StringComparer.Ordinal))
                {
                    WriteCell(w, cell);
                }
                w.EndObject();
            }
            w.EndArray();
        }

        private static void WriteCell(JsonWriterLite w, CompiledCell cell)
        {
            w.Name(cell.Field.Field);

            if (cell.Value == null) { w.Null(); return; }

            if (cell.Value is long l) { w.Value(l); return; }
            if (cell.Value is double d) { w.Value(d); return; }
            if (cell.Value is bool b) { w.Value(b); return; }

            w.Value(cell.Value.ToString());
        }
    }
}

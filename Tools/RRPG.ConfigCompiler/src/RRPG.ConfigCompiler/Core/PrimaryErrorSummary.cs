using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// T4.1：把「首要错误」翻译成制作人能直接看懂的一段话（中文）。
    ///
    /// 包含：工作簿 / 工作表 / 行号 / 中文字段名 + 机器字段名 / 问题 / 怎么修。
    /// 只用于**控制台 UX**（Producer Console）：不改变任何产物，也不改变 BuildReport 的既有内容。
    /// </summary>
    public static class PrimaryErrorSummary
    {
        public static string[] Build(CompileResult result)
        {
            if (result == null) return new string[0];

            var diag = result.Diagnostics.FirstOrDefault(d => d.Severity == Severity.Error);
            if (diag == null) return new string[0];

            var lines = new List<string>();
            lines.Add("首要错误（先说这一条，其它错误见报告）：");
            lines.Add("  工作簿 ：" + (diag.Workbook ?? "-"));
            lines.Add("  工作表 ：" + (string.IsNullOrEmpty(diag.Sheet) ? "(整本工作簿)" : diag.Sheet));
            if (diag.Row > 0) lines.Add("  行号   ：" + diag.Row);
            if (!string.IsNullOrEmpty(diag.Column)) lines.Add("  列     ：" + diag.Column);

            string field = ResolveField(result, diag);
            if (!string.IsNullOrEmpty(field)) lines.Add("  字段   ：" + field);

            lines.Add("  错误码 ：" + diag.Code);
            lines.Add("  问题   ：" + diag.Message);

            string fix = Suggest(diag.Code);
            if (!string.IsNullOrEmpty(fix)) lines.Add("  怎么修 ：" + fix);

            return lines.ToArray();
        }

        /// <summary>把诊断里的列字母翻译成「中文字段名（机器字段名）」，供制作人定位。</summary>
        public static string ResolveField(CompileResult result, Diagnostic diag)
        {
            if (result == null || diag == null || string.IsNullOrEmpty(diag.Column)) return null;

            foreach (var sheet in result.CompiledSheets)
            {
                var spec = sheet.Spec;
                if (!string.IsNullOrEmpty(diag.Sheet) && !string.Equals(spec.SheetName, diag.Sheet, StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(diag.Workbook) && !string.Equals(spec.Workbook, diag.Workbook, StringComparison.Ordinal)) continue;

                var byLetter = spec.Fields.FirstOrDefault(f => string.Equals(f.ColumnLetter, diag.Column, StringComparison.OrdinalIgnoreCase));
                if (byLetter != null) return Format(byLetter);

                var byName = spec.Fields.FirstOrDefault(f => string.Equals(f.Field, diag.Column, StringComparison.Ordinal));
                if (byName != null) return Format(byName);
            }

            return null;
        }

        private static string Format(FieldSpec field)
        {
            string header = string.IsNullOrEmpty(field.DisplayHeader) ? "(无中文表头)" : field.DisplayHeader;
            return header + "（字段名 " + field.Field + "，物理列 " + field.ColumnLetter + "）";
        }

        /// <summary>按错误码给出「怎么修」的一句话（Producer Console 用）。</summary>
        public static string Suggest(string code)
        {
            switch (code)
            {
                case DiagnosticCodes.WbFileUnreadable:
                case DiagnosticCodes.WbOpenXmlInvalid:
                    return "这本表正被别的程序占用（通常是 Excel 还开着）或文件损坏。请保存并关闭 Excel，然后重新导入。";

                case DiagnosticCodes.WbTempFileSkipped:
                    return "检测到 Excel 临时锁文件（~$ 开头）：说明这本表还开着。请保存并关闭 Excel，然后重新导入。";

                case DiagnosticCodes.SchRequiredFieldMissing:
                    return "把该字段加回业务列（机器字段名必须完全一致，大小写敏感）。如果它被挪进了备注列，请挪回来。";

                case DiagnosticCodes.SchUnrecognizedColumn:
                case DiagnosticCodes.SchDataWithoutMachineField:
                    return "这一列没有机器字段名。二选一：① 在 Row 1 补上机器字段名；② 如果它只是给人看的备注，就在 Row 2 写 ignore。";

                case DiagnosticCodes.SchDuplicateMachineField:
                    return "同一个机器字段名在一行里出现了两次：请改名或删掉其中一个。";

                case DiagnosticCodes.SchNoKeyColumn:
                    return "这个表没有身份列：需要有 id 列（单列主键）或 key 列（复合身份）。";

                case DiagnosticCodes.SchIdWithKey:
                    return "已有 id 列时不要再写 key：身份只能声明一次。";

                case DiagnosticCodes.SchTypeUnknown:
                    return "类型写错了。允许：id / string / int / float / bool / ref:表名。";

                case DiagnosticCodes.SchRuleUnknownToken:
                    return "规则写错了。Row 3 留空 = 必填（不要写 1）；允许：optional / key / default=值 / >=n / <=n / allowed=值1|值2。";

                case DiagnosticCodes.SchAllowedNotApplicable:
                    return "allowed= 只能用在 string 列上。";

                case DiagnosticCodes.SchAllowedDefaultNotMember:
                    return "默认值本身也必须是 allowed 集合里的值。";

                case DiagnosticCodes.DatRequiredEmpty:
                    return "这一格是必填的（Row 3 没写 optional / default=）。";

                case DiagnosticCodes.DatIdEmpty:
                    return "ID 列不能为空。";

                case DiagnosticCodes.DatTypeMismatch:
                    return "值的类型不对：int 写整数、float 写小数、bool 写 true/false。";

                case DiagnosticCodes.DatMinViolation:
                case DiagnosticCodes.DatMaxViolation:
                    return "数值超出了 Row 3 声明的范围（>= / <=）。";

                case DiagnosticCodes.DatKeyDuplicate:
                    return "同一个身份出现了两行：每个 ID 只能有一行。";

                case DiagnosticCodes.DatRefUnresolved:
                    return "被引用的 ID 在目标表里不存在：检查拼写，或先在目标表里补上这一行。";

                case DiagnosticCodes.DatRefUnknownSheet:
                    return "ref: 后面的表名不存在：检查表名拼写。";

                case DiagnosticCodes.DatAllowedViolation:
                    return "值不在该列声明的允许集合里：改成集合中的值，或先在 Row 3 的 allowed= 里声明它。";

                case DiagnosticCodes.DatSourceArtMissing:
                    return "源图文件不存在：把图片放进 Development/SourceAssets/ 下（例如 Enemy/slime.png），并检查 SourceFile 拼写。";

                case DiagnosticCodes.DatSourceArtPathEscape:
                    return "SourceFile 必须写成相对 Development/SourceAssets 的路径（例如 Enemy/slime.png），不能是绝对路径，也不能出现 ../。";

                case DiagnosticCodes.DatSourceArtExtension:
                    return "M3 只支持 .png 源图。";

                case DiagnosticCodes.DatUnsupportedImportPreset:
                    return "ImportPreset 不在受支持列表里：当前支持 sprite_point_100（与 Row 3 的 allowed= 是同一份声明）。";

                case DiagnosticCodes.DatVisualAssetShape:
                    return "VisualAsset 表需要三列：VisualAssetId / SourceFile / ImportPreset。";

                case DiagnosticCodes.DatFormulaNotAllowed:
                    return "配置单元格不能写公式：请改成静态值。";

                case DiagnosticCodes.WbNoWorkbookFound:
                    return "在 Authoring 目录里找不到任何 .xlsx。";

                default:
                    return null;
            }
        }
    }
}

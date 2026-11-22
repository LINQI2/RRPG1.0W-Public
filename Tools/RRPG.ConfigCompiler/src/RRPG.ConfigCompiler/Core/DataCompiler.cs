using System;
using System.Collections.Generic;
using System.Globalization;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 数据编译：RawSheet.Rows + SheetSpec → CompiledSheet.Rows。
    ///
    /// 每个单元格的判定顺序（确定性）：
    ///   1. 公式 → Hard Error（不允许缓存值进入配置）；
    ///   2. 空 + 有 default → 取默认值；
    ///   3. 空 + required → Hard Error（id 列使用专用诊断码）；
    ///   4. 空 + optional → null；
    ///   5. 有值 → 按类型解析 → 范围校验。
    ///
    /// 类型解析全部使用 InvariantCulture（不受本机 culture 影响）。
    /// </summary>
    public static class DataCompiler
    {
        public static CompiledSheet Compile(RawSheet raw, SheetSpec spec, DiagnosticBag bag)
        {
            var sheet = new CompiledSheet { Spec = spec };

            foreach (var row in raw.Rows)
            {
                var ctx = DiagContext.Of(raw.WorkbookDisplayPath, raw.SheetName, row.ExcelRow);
                var compiled = new CompiledRow { Sheet = spec, ExcelRow = row.ExcelRow };

                foreach (var field in spec.Fields)
                {
                    var cell = new CompiledCell { Field = field, Raw = row.Get(field.ColumnIndex) };

                    if (row.FormulaColumns.Contains(field.ColumnIndex))
                    {
                        bag.Error(DiagnosticCodes.DatFormulaNotAllowed,
                            "单元格是公式：配置列必须是静态值（不读取公式缓存值，避免陈旧结果静默入库）。",
                            ctx, field.Field);
                        cell.Value = cell.Raw;
                        compiled.Cells.Add(cell);
                        continue;
                    }

                    if (cell.Raw.Length == 0)
                    {
                        if (field.HasDefault)
                        {
                            cell.Value = ParseValue(field, field.DefaultRaw, ctx, bag, out bool okDefault);
                            cell.FromDefault = okDefault;
                            if (!okDefault) cell.Value = null;
                        }
                        else if (field.Kind == FieldKind.Id)
                        {
                            bag.Error(DiagnosticCodes.DatIdEmpty, "id 列不允许为空。", ctx, field.Field);
                            cell.Value = null;
                        }
                        else if (field.Required)
                        {
                            bag.Error(DiagnosticCodes.DatRequiredEmpty,
                                "required 列为空（该列声明为必填）。", ctx, field.Field);
                            cell.Value = null;
                        }
                        else
                        {
                            cell.Value = null; // optional 空值
                        }

                        compiled.Cells.Add(cell);
                        continue;
                    }

                    cell.Value = ParseValue(field, cell.Raw, ctx, bag, out bool ok);
                    if (!ok) cell.Value = null;

                    CheckRange(field, cell, ctx, bag);
                    compiled.Cells.Add(cell);
                }

                sheet.Rows.Add(compiled);
                sheet.PhysicalRowOrder.Add(row.ExcelRow);
            }

            return sheet;
        }

        private static object ParseValue(FieldSpec field, string text, DiagContext ctx,
            DiagnosticBag bag, out bool ok)
        {
            ok = true;

            switch (field.Kind)
            {
                case FieldKind.Int:
                {
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        return l;
                    bag.Error(DiagnosticCodes.DatTypeMismatch,
                        "无法解析为 int：" + text, ctx, field.Field);
                    ok = false;
                    return null;
                }

                case FieldKind.Float:
                {
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return d;
                    bag.Error(DiagnosticCodes.DatTypeMismatch,
                        "无法解析为 float：" + text, ctx, field.Field);
                    ok = false;
                    return null;
                }

                case FieldKind.Bool:
                {
                    if (bool.TryParse(text, out bool b)) return b;
                    bag.Error(DiagnosticCodes.DatTypeMismatch,
                        "无法解析为 bool：" + text + "（允许 true / false）。", ctx, field.Field);
                    ok = false;
                    return null;
                }

                case FieldKind.Text:
                {
                    // T4 扩展：声明式取值域（Row 3 = allowed=V1|V2）。
                    if (field.HasAllowedSet && !field.AllowedValues.Contains(text))
                    {
                        bag.Error(DiagnosticCodes.DatAllowedViolation,
                            "值 '" + text + "' 不在本列声明的允许集合内（allowed=" + field.AllowedRaw + "）。"
                            + "若是拼写错误请直接改正；若是新的合法取值，请先在 Row 3 的 allowed= 中声明。",
                            ctx, field.Field, "allowed=" + field.AllowedRaw);
                        ok = false;
                        return null;
                    }
                    return text;
                }

                default:
                    return text; // id / ref（enum 已不再是合法类型；引用由跨表校验处理）
            }
        }

        private static void CheckRange(FieldSpec field, CompiledCell cell, DiagContext ctx, DiagnosticBag bag)
        {
            if (!field.IsNumeric || cell.Value == null) return;

            double value = cell.Value is long l ? l : (double)cell.Value;

            if (field.HasMin && value < field.MinValue)
            {
                bag.Error(DiagnosticCodes.DatMinViolation,
                    "值 " + value.ToString("R", CultureInfo.InvariantCulture)
                    + " 小于下界 " + field.MinValue.ToString("R", CultureInfo.InvariantCulture) + "。",
                    ctx, field.Field);
            }

            if (field.HasMax && value > field.MaxValue)
            {
                bag.Error(DiagnosticCodes.DatMaxViolation,
                    "值 " + value.ToString("R", CultureInfo.InvariantCulture)
                    + " 大于上界 " + field.MaxValue.ToString("R", CultureInfo.InvariantCulture) + "。",
                    ctx, field.Field);
            }
        }
    }
}

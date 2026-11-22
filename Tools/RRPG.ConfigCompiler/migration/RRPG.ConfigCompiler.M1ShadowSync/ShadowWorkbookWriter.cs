using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace RRPG.ConfigCompiler.M1ShadowSync
{
    /// <summary>
    /// Shadow Workbook 生成（MIGRATION ONLY · M1 CLOSED）。
    ///
    /// **M2 起本类不可达**：`sync` 已冻结（见 <see cref="Program"/> 的 RunSync），CSV → XLSX 同步不再允许；
    /// 本类保留仅为 M1 迁移证据与后续工具退役参考，**不得**重新接上执行路径。
    /// 其写出的 导航 Sheet 文案属 M1 时代产物；当前内容 Authority 是
    /// `Development/Config/RRPG_Monsters.xlsx`（由制作人维护，不由本工具生成）。
    ///
    /// 确定性规则：
    ///   - 列序 = Legacy CSV 字段序（身份 → 名称 → 5 个 int → 阵营）；不增删字段；
    ///   - A 列 = 说明栏（A1 机器字段 / A2 数据类型 / A3 规则 / A4 中文说明 / A5+ 行备注），业务字段从 B 列开始；
    ///   - 行 1 机器字段 / 行 2 类型 / 行 3 规则（Monster **只要求必填**，不引入 Spike 实验性范围限制，见用户 M1 §4）
    ///     / 行 4 中文表头 / 行 5+ 数据；
    ///   - 无时间戳 / 无随机 / 无样式依赖 → 相同 Legacy 输入 → 语义相同 → **不重写文件**（no-op）。
    /// </summary>
    public static class ShadowWorkbookWriter
    {
        public const string MonsterSheet = "Monster";
        public const string EnemyPresentationSheet = "EnemyPresentation";
        public const string VisualAssetSheet = "VisualAsset";
        public const string NavSheet = "00_导航";

        private static readonly string[] MonsterHeaders =
        {
            "怪物ID", "名称", "最大生命值", "攻击力", "攻击范围", "攻击次数", "行动力", "阵营"
        };

        private static readonly string[] MonsterTypes =
        {
            "id", "string", "int", "int", "int", "int", "int", "string"
        };

        private static readonly string[] MonsterRowNotes =
        {
            "Legacy: Development/Excel/Monster.csv（Current Monster Authoring Authority）",
            "Legacy: Development/Excel/Monster.csv",
            "Legacy: Development/Excel/Monster.csv",
        };

        public static List<string> MonsterNavText()
        {
            return new List<string>
            {
                "RRPG_Monsters.xlsx —— M1 SHADOW / DO NOT EDIT AS PRODUCT CONTENT",
                string.Empty,
                "Current Monster Authoring Authority = Development/Excel/Monster.csv（本文件**不是** Authority）",
                "本文件由 M1 迁移专用工具从 Monster.csv 确定性生成；人工修改不会成为产品内容，",
                "且会在下一次 sync 被覆盖。制作人的正式内容修改只能在 M2 Authority Cutover 之后进入本文件。",
                string.Empty,
                "本工作簿有哪些 Sheet：",
                "    Monster —— 怪物数值（当前 8 字段：MonsterId / Name / MaxHP / Attack / AttackRange / "
                    + "AttackCount / ActionPoints / Faction），一行一个怪物",
                string.Empty,
                "列怎么用：",
                "    A 列是给你看的「说明栏」，不参与导出：",
                "        A1 = 机器字段   A2 = 数据类型   A3 = 规则   A4 = 中文说明   A5 起 = 这一行的备注",
                "    业务字段从 B 列开始。以下操作都不改变导出结果：",
                "        · 调整列的先后顺序   · 插入备注列（该列行 2 写 ignore 或 note）",
                "        · 修改行 4 的中文说明 · 在 A 列写行备注",
                string.Empty,
                "导入入口（Shadow 链，只用于验证，不是正式内容发布）：",
                "    双击  RRPG_检查配置.cmd（只检查）   /   RRPG_一键导表.cmd（检查并生成）",
            };
        }

        public static List<string> EnemyPresentationNavText()
        {
            return new List<string>
            {
                "RRPG_EnemyPresentation.xlsx —— M1 SHADOW / NOT CURRENT PRESENTATION AUTHORITY",
                string.Empty,
                "当前**没有** per-Monster 视觉配置的 Current Authority：",
                "    产品对敌方视觉只有单一占位（enemyBodyPlaceholder），不存在「每只怪物用哪张图」的正式事实。",
                "因此本工作簿在 M1 只建立**最小 Shadow Schema 骨架**，数据行为 0：不得发明正式视觉内容。",
                "真正的 Enemy Presentation Authority 在 **M3** 才切换。",
                string.Empty,
                "本工作簿有哪些 Sheet（当前均为**仅表头、0 数据**）：",
                "    EnemyPresentation —— 怪物 → 视觉资源绑定（M3 起才有数据）",
                "    VisualAsset       —— 视觉资源的机器身份与源文件（M3 起才有数据）",
                string.Empty,
                "列怎么用（与 Monster 工作簿相同）：",
                "    A1 = 机器字段   A2 = 数据类型   A3 = 规则   A4 = 中文说明   A5 起 = 行备注；业务字段从 B 列开始。",
            };
        }

        public sealed class SyncOutcome
        {
            public bool Changed;
            public string MonsterPath;
            public string EnemyPresentationPath;
            public List<string> Actions = new List<string>();

            /// <summary>需要重写的原因（诊断用；no-op 时为空）。</summary>
            public List<string> Reasons = new List<string>();
        }

        /// <summary>生成 / 更新两个 Shadow Workbook；语义未变化时不重写（no-op）。</summary>
        public static SyncOutcome Sync(string authoringDir, List<MonsterRow> rows)
        {
            var outcome = new SyncOutcome
            {
                MonsterPath = Path.Combine(authoringDir, "RRPG_Monsters.xlsx"),
                EnemyPresentationPath = Path.Combine(authoringDir, "RRPG_EnemyPresentation.xlsx")
            };

            Directory.CreateDirectory(authoringDir);

            outcome.Changed |= WriteMonsterWorkbook(outcome.MonsterPath, rows, outcome.Actions, outcome.Reasons);
            outcome.Changed |= WritePresentationWorkbook(outcome.EnemyPresentationPath, outcome.Actions, outcome.Reasons);
            return outcome;
        }

        private static string[][] MonsterColumns()
        {
            return new[]
            {
                new[] { "MonsterId", MonsterTypes[0], string.Empty, MonsterHeaders[0] },
                new[] { "Name", MonsterTypes[1], string.Empty, MonsterHeaders[1] },
                new[] { "MaxHP", MonsterTypes[2], string.Empty, MonsterHeaders[2] },
                new[] { "Attack", MonsterTypes[3], string.Empty, MonsterHeaders[3] },
                new[] { "AttackRange", MonsterTypes[4], string.Empty, MonsterHeaders[4] },
                new[] { "AttackCount", MonsterTypes[5], string.Empty, MonsterHeaders[5] },
                new[] { "ActionPoints", MonsterTypes[6], string.Empty, MonsterHeaders[6] },
                new[] { "Faction", MonsterTypes[7], string.Empty, MonsterHeaders[7] },
            };
        }

        private static string[][] PresentationColumns()
        {
            return new[]
            {
                new[] { "MonsterId", "ref:Monster", "key", "怪物（引用 Monster.MonsterId）" },
                new[] { "VisualAssetId", "ref:VisualAsset", string.Empty, "视觉资源（引用 VisualAsset）" },
            };
        }

        private static string[][] VisualAssetColumns()
        {
            return new[]
            {
                new[] { "VisualAssetId", "id", string.Empty, "视觉资源ID" },
            };
        }

        private static bool WriteMonsterWorkbook(string path, List<MonsterRow> rows, List<string> actions,
            List<string> reasons)
        {
            string[][] columns = MonsterColumns();
            var dataRows = rows.Select(r => r.AsValues()).ToList();
            bool existed = File.Exists(path);
            var why = new List<string>();

            if (IsMonsterWorkbookUpToDate(path, columns, dataRows, why))
            {
                actions.Add("unchanged: RRPG_Monsters.xlsx（语义与 Legacy CSV 一致，不重写）");
                return false;
            }

            reasons.AddRange(why.Select(r => "RRPG_Monsters.xlsx: " + r));

            using (var wb = new XLWorkbook())
            {
                WriteNavSheet(wb, MonsterNavText());

                var ws = wb.AddWorksheet(MonsterSheet);
                WriteAnnotationColumn(ws, MonsterRowNotes);
                WriteHeaderedColumns(ws, columns, dataRows);
                ws.SheetView.FreezeRows(4);
                ws.Column(1).Width = 52;

                wb.SaveAs(path);
            }

            actions.Add((existed ? "updated: " : "created: ") + "RRPG_Monsters.xlsx"
                + "（" + rows.Count + " 条 × " + columns.Length + " 字段）");
            return true;
        }

        private static bool WritePresentationWorkbook(string path, List<string> actions, List<string> reasons)
        {
            string[][] presentation = PresentationColumns();
            string[][] visual = VisualAssetColumns();
            var empty = new List<string[]>();
            bool existed = File.Exists(path);
            var why = new List<string>();

            if (IsPresentationWorkbookUpToDate(path, presentation, visual, why))
            {
                actions.Add("unchanged: RRPG_EnemyPresentation.xlsx（Shadow 骨架未变化，不重写）");
                return false;
            }

            reasons.AddRange(why.Select(r => "RRPG_EnemyPresentation.xlsx: " + r));

            using (var wb = new XLWorkbook())
            {
                WriteNavSheet(wb, EnemyPresentationNavText());

                var ws1 = wb.AddWorksheet(EnemyPresentationSheet);
                WriteAnnotationColumn(ws1, null);
                WriteHeaderedColumns(ws1, presentation, empty);
                ws1.SheetView.FreezeRows(4);

                var ws2 = wb.AddWorksheet(VisualAssetSheet);
                WriteAnnotationColumn(ws2, null);
                WriteHeaderedColumns(ws2, visual, empty);
                ws2.SheetView.FreezeRows(4);

                wb.SaveAs(path);
            }

            actions.Add((existed ? "updated: " : "created: ") + "RRPG_EnemyPresentation.xlsx"
                + "（Shadow 骨架；Presentation / VisualAsset 数据 = 0）");
            return true;
        }

        // ---------------------------------------------------------------- 内部写出

        private static void WriteNavSheet(XLWorkbook wb, List<string> lines)
        {
            var ws = wb.AddWorksheet(NavSheet);
            for (int i = 0; i < lines.Count; i++) ws.Cell(i + 1, 1).Value = lines[i];
            ws.Column(1).Width = 110;
        }

        /// <summary>A 列说明栏（A1 机器字段 / A2 数据类型 / A3 规则 / A4 中文说明 / A5+ 行备注）。</summary>
        private static void WriteAnnotationColumn(IXLWorksheet ws, string[] rowNotes)
        {
            ws.Cell(1, 1).Value = "机器字段";
            ws.Cell(2, 1).Value = "数据类型";
            ws.Cell(3, 1).Value = "规则";
            ws.Cell(4, 1).Value = "中文说明";

            if (rowNotes == null) return;
            for (int i = 0; i < rowNotes.Length; i++)
            {
                if (!string.IsNullOrEmpty(rowNotes[i])) ws.Cell(5 + i, 1).Value = rowNotes[i];
            }
        }

        private static void WriteHeaderedColumns(IXLWorksheet ws, string[][] columns, List<string[]> dataRows)
        {
            for (int c = 0; c < columns.Length; c++)
            {
                int column = c + 2;   // 业务字段从 B 列开始
                ws.Cell(1, column).Value = columns[c][0];
                ws.Cell(2, column).Value = columns[c][1];
                ws.Cell(3, column).Value = columns[c][2];
                ws.Cell(4, column).Value = columns[c][3];
            }

            for (int r = 0; r < dataRows.Count; r++)
            {
                string[] row = dataRows[r];
                for (int c = 0; c < row.Length && c < columns.Length; c++)
                {
                    string text = row[c] ?? string.Empty;
                    int number;
                    if (columns[c][1] == "int"
                        && int.TryParse(text, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out number))
                    {
                        ws.Cell(5 + r, c + 2).Value = number;
                    }
                    else
                    {
                        ws.Cell(5 + r, c + 2).Value = text;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- no-op 判定（语义比较）

        private static bool IsMonsterWorkbookUpToDate(string path, string[][] columns, List<string[]> dataRows,
            List<string> why)
        {
            if (!File.Exists(path))
            {
                why.Add("文件不存在（首次生成）");
                return false;
            }

            try
            {
                using (var wb = new XLWorkbook(path))
                {
                    if (!wb.TryGetWorksheet(NavSheet, out var nav)) { why.Add("缺少 Sheet " + NavSheet); return false; }
                    if (!SameLines(nav, MonsterNavText(), why)) return false;

                    if (!wb.TryGetWorksheet(MonsterSheet, out var ws)) { why.Add("缺少 Sheet " + MonsterSheet); return false; }
                    if (!SameColumns(ws, columns, why)) return false;
                    if (!SameRows(ws, columns, dataRows, why)) return false;
                    return true;
                }
            }
            catch (Exception ex)
            {
                why.Add("读取失败：" + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static bool IsPresentationWorkbookUpToDate(string path, string[][] presentation, string[][] visual,
            List<string> why)
        {
            if (!File.Exists(path))
            {
                why.Add("文件不存在（首次生成）");
                return false;
            }

            try
            {
                using (var wb = new XLWorkbook(path))
                {
                    if (!wb.TryGetWorksheet(NavSheet, out var nav)) { why.Add("缺少 Sheet " + NavSheet); return false; }
                    if (!SameLines(nav, EnemyPresentationNavText(), why)) return false;

                    if (!wb.TryGetWorksheet(EnemyPresentationSheet, out var ws1))
                    {
                        why.Add("缺少 Sheet " + EnemyPresentationSheet); return false;
                    }
                    if (!SameColumns(ws1, presentation, why)) return false;
                    if (!SameRows(ws1, presentation, new List<string[]>(), why)) return false;

                    if (!wb.TryGetWorksheet(VisualAssetSheet, out var ws2))
                    {
                        why.Add("缺少 Sheet " + VisualAssetSheet); return false;
                    }
                    if (!SameColumns(ws2, visual, why)) return false;
                    if (!SameRows(ws2, visual, new List<string[]>(), why)) return false;
                    return true;
                }
            }
            catch (Exception ex)
            {
                why.Add("读取失败：" + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static bool SameLines(IXLWorksheet ws, List<string> expected, List<string> why)
        {
            for (int i = 0; i < expected.Count; i++)
            {
                if (!Compare("导航第 " + (i + 1) + " 行", expected[i], Cell(ws, i + 1, 1), why)) return false;
            }
            return Compare("导航后首空行", string.Empty, Cell(ws, expected.Count + 1, 1), why);
        }

        private static bool SameColumns(IXLWorksheet ws, string[][] columns, List<string> why)
        {
            for (int c = 0; c < columns.Length; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    if (!Compare("列 " + columns[c][0] + " 行 " + (r + 1), columns[c][r], Cell(ws, r + 1, c + 2), why))
                        return false;
                }
            }
            return Compare("表头后首空列", string.Empty, Cell(ws, 1, columns.Length + 2), why);
        }

        private static bool SameRows(IXLWorksheet ws, string[][] columns, List<string[]> dataRows, List<string> why)
        {
            for (int r = 0; r < dataRows.Count; r++)
            {
                for (int c = 0; c < columns.Length; c++)
                {
                    if (!Compare("数据 " + (r + 5) + " 行 " + columns[c][0],
                            dataRows[r][c] ?? string.Empty, Cell(ws, 5 + r, c + 2), why)) return false;
                }
            }
            return Compare("数据后首空行", string.Empty, Cell(ws, 5 + dataRows.Count, 2), why);
        }

        private static bool Compare(string label, string expected, string actual, List<string> why)
        {
            // 行首缩进是导航文案的一部分（必须保留）；只忽略行尾空白（Excel 侧可能追加）。
            string e = (expected ?? string.Empty).TrimEnd();
            string a = (actual ?? string.Empty).TrimEnd();
            if (string.Equals(e, a, StringComparison.Ordinal)) return true;

            if (why != null && why.Count < 6)
            {
                why.Add(label + "：期望 '" + e + "'，实际 '" + a + "'");
            }
            return false;
        }

        private static string Cell(IXLWorksheet ws, int row, int column)
        {
            var cell = ws.Cell(row, column);
            if (cell == null || cell.IsEmpty()) return string.Empty;
            string text = cell.GetString();
            return text == null ? string.Empty : text;   // 不做 Trim：行首缩进属于语义
        }
    }
}

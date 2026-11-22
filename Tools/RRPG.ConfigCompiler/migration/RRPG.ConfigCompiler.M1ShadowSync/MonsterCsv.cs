using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.M1ShadowSync
{
    /// <summary>一条 Monster 记录（Legacy 8 字段；字段顺序与 Monster.csv 表头一致）。</summary>
    public sealed class MonsterRow
    {
        public string MonsterId;
        public string Name;
        public int MaxHP;
        public int Attack;
        public int AttackRange;
        public int AttackCount;
        public int ActionPoints;
        public string Faction;

        public string[] AsValues()
        {
            return new[]
            {
                MonsterId, Name,
                MaxHP.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Attack.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AttackRange.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AttackCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ActionPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Faction
            };
        }
    }

    /// <summary>
    /// Current Monster Authoring Authority 读取器：<c>Development/Excel/Monster.csv</c>。
    ///
    /// 事实（M1 只读核对，2026-09-22）：
    ///   - UTF-8 **with BOM**（已实测）；文件末**无**换行；
    ///   - 表头 = 8 字段：MonsterId,Name,MaxHP,Attack,AttackRange,AttackCount,ActionPoints,Faction；
    ///   - 3 条数据：mon_001 / mon_002 / mon_003_wolf。
    ///
    /// 读取必须容错：BOM、CRLF/LF、末行无换行、中文。
    /// 解析失败（字段数不符 / int 非法 / ID 重复 / ID 空）= Hard Error（调用方必须停）。
    /// </summary>
    public static class MonsterCsv
    {
        public static readonly string[] ExpectedHeader =
        {
            "MonsterId", "Name", "MaxHP", "Attack", "AttackRange", "AttackCount", "ActionPoints", "Faction"
        };

        public static List<MonsterRow> Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Legacy Monster.csv 不存在：" + path);

            string text = File.ReadAllText(path, new UTF8Encoding(true));
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);   // BOM 兜底

            var lines = new List<string>();
            foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (raw.Trim().Length == 0) continue;   // 容忍空行与末尾换行
                lines.Add(raw);
            }

            if (lines.Count < 2) throw new InvalidDataException("Monster.csv 至少需要表头 + 1 行数据。");

            string[] header = SplitCsv(lines[0]);
            if (header.Length != ExpectedHeader.Length
                || !header.Select((h, i) => h == ExpectedHeader[i]).All(x => x))
            {
                throw new InvalidDataException("Monster.csv 表头不是预期的 8 字段："
                    + string.Join(",", header));
            }

            var rows = new List<MonsterRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 1; i < lines.Count; i++)
            {
                string[] cells = SplitCsv(lines[i]);
                if (cells.Length != ExpectedHeader.Length)
                {
                    throw new InvalidDataException("Monster.csv 第 " + (i + 1) + " 行字段数 = " + cells.Length
                        + "（期望 " + ExpectedHeader.Length + "）：" + lines[i]);
                }

                var row = new MonsterRow
                {
                    MonsterId = cells[0].Trim(),
                    Name = cells[1].Trim(),
                    MaxHP = ParseInt(cells[2], lines[i], 3, i + 1),
                    Attack = ParseInt(cells[3], lines[i], 4, i + 1),
                    AttackRange = ParseInt(cells[4], lines[i], 5, i + 1),
                    AttackCount = ParseInt(cells[5], lines[i], 6, i + 1),
                    ActionPoints = ParseInt(cells[6], lines[i], 7, i + 1),
                    Faction = cells[7].Trim()
                };

                if (row.MonsterId.Length == 0)
                {
                    throw new InvalidDataException("Monster.csv 第 " + (i + 1) + " 行 MonsterId 为空。");
                }
                if (!seen.Add(row.MonsterId))
                {
                    throw new InvalidDataException("Monster.csv MonsterId 重复：" + row.MonsterId);
                }

                rows.Add(row);
            }

            return rows;
        }

        private static int ParseInt(string cell, string line, int column, int rowNumber)
        {
            int value;
            if (!int.TryParse(cell.Trim(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidDataException("Monster.csv 第 " + rowNumber + " 行第 " + column
                    + " 列不是合法整数：'" + cell + "'（行：" + line + "）");
            }
            return value;
        }

        /// <summary>极简 CSV 拆分（本文件无引号 / 无逗号转义；出现引号即视为异常）。</summary>
        private static string[] SplitCsv(string line)
        {
            if (line.IndexOf('"') >= 0)
            {
                throw new InvalidDataException("Monster.csv 出现引号字段（当前 Authority 不含转义语法）：" + line);
            }
            return line.Split(',');
        }
    }
}

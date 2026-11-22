using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// 从 Canonical Mirror 的 <c>Monster.json</c> 读出 MonsterDefinition（Editor-only）。
    ///
    /// 行式读取的理由：Mirror 由我们自己的 JsonWriterLite **确定性**产出（每个成员独占一行、
    /// 2 空格缩进、字符串带引号、整数为字面量），因此面向该格式严格解析即可 ——
    /// 不引入第三方 JSON 依赖，也不用 JsonUtility（rows 的键是动态字段名）。
    ///
    /// 校验（任一失败 = Hard Error，Builder 必须**在写入前**停）：
    ///   _generated.doNotEdit = true / formatVersion 匹配 / source.sheet = Monster /
    ///   列恰好为 8 个当前字段 / 每行字段类型正确 / MonsterId 非空且不重复。
    /// </summary>
    internal static class MonsterMirrorReader
    {
        public static readonly string[] ExpectedFields =
        {
            "MonsterId", "Name", "MaxHP", "Attack", "AttackRange", "AttackCount", "ActionPoints", "Faction"
        };

        private static readonly string[] IntegerFields =
        {
            "MaxHP", "Attack", "AttackRange", "AttackCount", "ActionPoints"
        };

        public sealed class Payload
        {
            public string FormatVersion;
            public string SourceSha256;
            public List<MonsterDefinition> Monsters = new List<MonsterDefinition>();
        }

        public static Payload Read(string json, out string failure)
        {
            failure = string.Empty;

            if (json == null || json.IndexOf("\"_generated\"", StringComparison.Ordinal) < 0)
            {
                failure = "Monster.json 缺少 _generated 头（Mirror 必须由 Compiler 生成）。";
                return null;
            }
            if (json.IndexOf("\"doNotEdit\": true", StringComparison.Ordinal) < 0)
            {
                failure = "Monster.json 缺少 _generated.doNotEdit = true。";
                return null;
            }

            string format = ExtractString(json, "\"formatVersion\": ");
            if (!string.Equals(format, RRPGStaticConfigRoot.SupportedFormatVersion, StringComparison.Ordinal))
            {
                failure = "Monster.json formatVersion 不匹配：期望 '"
                    + RRPGStaticConfigRoot.SupportedFormatVersion + "'，实际 '" + format + "'。";
                return null;
            }

            string sheet = ExtractString(json, "\"sheet\": ");
            if (!string.Equals(sheet, "Monster", StringComparison.Ordinal))
            {
                failure = "Monster.json 的 source.sheet 不是 Monster：'" + sheet + "'。";
                return null;
            }

            foreach (string field in ExpectedFields)
            {
                if (json.IndexOf("\"field\": \"" + field + "\"", StringComparison.Ordinal) < 0)
                {
                    failure = "Monster.json 缺少字段列：" + field;
                    return null;
                }
            }

            int fieldCount = CountOccurrences(json, "\"field\": ");
            if (fieldCount != ExpectedFields.Length)
            {
                failure = "Monster.json 列数不是 " + ExpectedFields.Length + "（实际 " + fieldCount
                    + "）：M1 只允许当前 8 字段，不得凭空扩充 Schema。";
                return null;
            }

            var payload = new Payload
            {
                FormatVersion = format,
                SourceSha256 = ExtractString(json, "\"sourceSha256\": ")
            };

            var rowsBlock = ExtractRowsBlock(json, out failure);
            if (rowsBlock == null) return null;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = new Dictionary<string, string>(StringComparer.Ordinal);
            int rowNumber = 0;

            foreach (string rawLine in rowsBlock.Split('\n'))
            {
                string line = rawLine.Trim();

                if (line.StartsWith("}", StringComparison.Ordinal))
                {
                    if (current.Count == 0) continue;

                    var definition = BuildDefinition(current, ref rowNumber, ref failure);
                    if (definition == null) return null;

                    if (!seen.Add(definition.MonsterId))
                    {
                        failure = "Monster.json 出现重复 MonsterId：" + definition.MonsterId;
                        return null;
                    }

                    payload.Monsters.Add(definition);
                    current.Clear();
                    continue;
                }

                int colon = line.IndexOf("\": ", StringComparison.Ordinal);
                if (colon < 1) continue;

                string key = line.Substring(1, colon - 1);
                string value = line.Substring(colon + 3).TrimEnd(',');
                current[key] = value;
            }

            if (payload.Monsters.Count == 0)
            {
                failure = "Monster.json 没有任何数据行。";
                return null;
            }

            return payload;
        }

        private static MonsterDefinition BuildDefinition(Dictionary<string, string> cells, ref int rowNumber,
            ref string failure)
        {
            rowNumber++;

            string id = Unquote(cells, "MonsterId");
            if (string.IsNullOrEmpty(id))
            {
                failure = "Monster.json 第 " + rowNumber + " 行 MonsterId 为空。";
                return null;
            }

            var definition = new MonsterDefinition
            {
                MonsterId = id,
                Name = Unquote(cells, "Name"),
                Faction = Unquote(cells, "Faction")
            };

            definition.MaxHP = RequireInt(cells, IntegerFields[0], rowNumber, ref failure);
            definition.Attack = RequireInt(cells, IntegerFields[1], rowNumber, ref failure);
            definition.AttackRange = RequireInt(cells, IntegerFields[2], rowNumber, ref failure);
            definition.AttackCount = RequireInt(cells, IntegerFields[3], rowNumber, ref failure);
            definition.ActionPoints = RequireInt(cells, IntegerFields[4], rowNumber, ref failure);

            if (!string.IsNullOrEmpty(failure)) return null;
            return definition;
        }

        private static int RequireInt(Dictionary<string, string> cells, string field, int rowNumber,
            ref string failure)
        {
            string raw;
            if (!cells.TryGetValue(field, out raw))
            {
                failure = "Monster.json 第 " + rowNumber + " 行缺少字段 " + field + "。";
                return 0;
            }

            int value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                failure = "Monster.json 第 " + rowNumber + " 行字段 " + field + " 不是整数：'" + raw + "'。";
                return 0;
            }
            return value;
        }

        private static string Unquote(Dictionary<string, string> cells, string field)
        {
            string raw;
            if (!cells.TryGetValue(field, out raw)) return null;

            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            {
                return raw.Substring(1, raw.Length - 2);
            }
            return raw;
        }

        private static string ExtractString(string json, string prefix)
        {
            int start = json.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return null;

            start += prefix.Length;
            if (start >= json.Length || json[start] != '"') return null;

            int end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        private static string ExtractRowsBlock(string json, out string failure)
        {
            failure = string.Empty;

            int anchor = json.IndexOf("\"rows\": [", StringComparison.Ordinal);
            if (anchor < 0)
            {
                failure = "Monster.json 缺少 rows 数组。";
                return null;
            }

            int start = json.IndexOf('[', anchor);
            int end = json.LastIndexOf(']');
            if (start < 0 || end <= start)
            {
                failure = "Monster.json 的 rows 数组不完整。";
                return null;
            }

            return json.Substring(start + 1, end - start - 1);
        }
    }
}

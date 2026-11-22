using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// Canonical Mirror 的**行式严格解析**工具（Editor-only；M3 Slice 1B 新增领域共用）。
    ///
    /// 为什么不引入 JSON 依赖 / 不用 JsonUtility：
    ///   Mirror 由我们自己的 <c>JsonWriterLite</c> **确定性**产出（每个成员独占一行、2 空格缩进、
    ///   字符串带引号、整数为字面量），因此面向该格式严格解析即可 —— 与 M1 的
    ///   <c>MonsterMirrorReader</c> 采用同一套做法（M2 文件保持不变，本类是新增领域的共享工具）。
    ///
    /// 硬约束：解析失败必须给出**可操作的中文失败原因**（Builder 在任何写入之前停下）。
    /// </summary>
    internal static class MirrorJsonText
    {
        /// <summary>校验 <c>_generated</c> 头并取出 formatVersion / sourceSha256。</summary>
        public static bool TryReadHeader(string json, string fileName, out string formatVersion,
            out string sourceSha256, out string failure)
        {
            formatVersion = null;
            sourceSha256 = null;
            failure = string.Empty;

            if (string.IsNullOrEmpty(json) || json.IndexOf("\"_generated\"", StringComparison.Ordinal) < 0)
            {
                failure = fileName + " 缺少 _generated 头（Mirror 必须由 Compiler 生成）。";
                return false;
            }
            if (json.IndexOf("\"doNotEdit\": true", StringComparison.Ordinal) < 0)
            {
                failure = fileName + " 缺少 _generated.doNotEdit = true。";
                return false;
            }

            formatVersion = ExtractString(json, "\"formatVersion\": ");
            if (!string.Equals(formatVersion, RRPGStaticConfigRoot.SupportedFormatVersion, StringComparison.Ordinal))
            {
                failure = fileName + " formatVersion 不匹配：期望 '"
                    + RRPGStaticConfigRoot.SupportedFormatVersion + "'，实际 '" + formatVersion + "'。";
                return false;
            }

            sourceSha256 = ExtractString(json, "\"sourceSha256\": ") ?? string.Empty;
            return true;
        }

        /// <summary>校验 source.sheet 名（防止把别的工作簿的 JSON 放进来）。</summary>
        public static bool TryReadSheet(string json, string fileName, string expectedSheet, out string failure)
        {
            failure = string.Empty;

            string sheet = ExtractString(json, "\"sheet\": ");
            if (!string.Equals(sheet, expectedSheet, StringComparison.Ordinal))
            {
                failure = fileName + " 的 source.sheet 不是 " + expectedSheet + "：'" + sheet + "'。";
                return false;
            }
            return true;
        }

        /// <summary>校验列集合恰好等于期望字段（既不多也不少；顺序无关）。</summary>
        public static bool TryReadColumns(string json, string fileName, string[] expectedFields, out string failure)
        {
            failure = string.Empty;

            foreach (string field in expectedFields)
            {
                if (json.IndexOf("\"field\": \"" + field + "\"", StringComparison.Ordinal) < 0)
                {
                    failure = fileName + " 缺少字段列：" + field;
                    return false;
                }
            }

            int fieldCount = CountOccurrences(json, "\"field\": ");
            if (fieldCount != expectedFields.Length)
            {
                failure = fileName + " 列数不是 " + expectedFields.Length + "（实际 " + fieldCount
                    + "）：Schema 变更必须同步 Builder（不得静默忽略未知列）。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 解析 <c>rowCount</c> + 数组为「字段名 → 原始值文本」的字典列表。
        /// 严格性：rowCount 必须与实际解析出的行数一致（镜像半损坏必须失败）。
        /// </summary>
        public static bool TryReadRows(string json, string fileName, string countKey, string arrayKey,
            out int declaredRowCount, out List<Dictionary<string, string>> rows, out string failure)
        {
            rows = new List<Dictionary<string, string>>();
            failure = string.Empty;

            bool hasCount;
            declaredRowCount = ExtractInt(json, "\"" + countKey + "\": ", out hasCount);
            if (!hasCount)
            {
                failure = fileName + " 缺少 " + countKey + "。";
                return false;
            }

            string block;
            if (!TryExtractBlock(json, "\"" + arrayKey + "\": [", fileName, out block, out failure)) return false;

            var current = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string rawLine in block.Split('\n'))
            {
                string line = rawLine.Trim();

                if (line.StartsWith("}", StringComparison.Ordinal))
                {
                    if (current.Count == 0) continue;
                    rows.Add(current);
                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    continue;
                }

                int colon = line.IndexOf("\": ", StringComparison.Ordinal);
                if (colon < 1) continue;

                string key = line.Substring(1, colon - 1);
                string value = line.Substring(colon + 3).TrimEnd(',');
                current[key] = value;
            }

            if (rows.Count != declaredRowCount)
            {
                failure = fileName + " 行数与 rowCount 不一致：声明 " + declaredRowCount + "，实际 " + rows.Count
                    + "（Mirror 半损坏 / 被人工编辑）。";
                return false;
            }
            return true;
        }

        /// <summary>取出一个数组的文本块（不含外层方括号）。</summary>
        public static bool TryExtractBlock(string json, string anchor, string fileName, out string block,
            out string failure)
        {
            block = null;
            failure = string.Empty;

            int at = json.IndexOf(anchor, StringComparison.Ordinal);
            if (at < 0)
            {
                failure = fileName + " 缺少数组成员：" + anchor.Trim('"');
                return false;
            }

            int start = json.IndexOf('[', at);
            int close = json.LastIndexOf(']');
            if (start < 0 || close <= start)
            {
                failure = fileName + " 的 " + anchor.Trim('"') + " 数组不完整。";
                return false;
            }

            block = json.Substring(start + 1, close - start - 1);
            return true;
        }

        public static string ExtractString(string json, string prefix)
        {
            int start = json.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return null;

            start += prefix.Length;
            if (start >= json.Length || json[start] != '"') return null;

            int end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }

        public static int ExtractInt(string json, string prefix, out bool found)
        {
            found = false;
            int start = json.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return 0;

            start += prefix.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (end == start) return 0;

            int value;
            if (!int.TryParse(json.Substring(start, end - start), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out value)) return 0;

            found = true;
            return value;
        }

        public static int CountOccurrences(string text, string pattern)
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

        /// <summary>去掉字符串字面量的引号（非字符串值原样返回）。</summary>
        public static string Unquote(string raw)
        {
            if (raw == null) return null;

            // Mirror 对「可选列为空」写出的是裸 null 字面量（JsonWriterLite.Null）。
            // 它必须读成「没有值」，否则会被当成字符串 "null" 静默进入下游
            // （WP-Encounter-1A 实测：Query 位被判成「带了 FixedMonsterId=null」）。
            if (string.Equals(raw, "null", StringComparison.Ordinal)) return null;

            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            {
                return raw.Substring(1, raw.Length - 2);
            }
            return raw;
        }

        public static string Unquote(IReadOnlyDictionary<string, string> cells, string field)
        {
            string raw;
            return cells.TryGetValue(field, out raw) ? Unquote(raw) : null;
        }

        public static bool TryInt(IReadOnlyDictionary<string, string> cells, string field, out int value)
        {
            value = 0;
            string raw;
            if (!cells.TryGetValue(field, out raw)) return false;

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>读取 bool 单元格（Mirror 写出的是裸字面量 true / false；也接受 1 / 0）。</summary>
        public static bool TryBool(IReadOnlyDictionary<string, string> cells, string field, out bool value)
        {
            value = false;
            string raw;
            if (!cells.TryGetValue(field, out raw)) return false;

            if (string.Equals(raw, "true", StringComparison.Ordinal)) { value = true; return true; }
            if (string.Equals(raw, "false", StringComparison.Ordinal)) { value = false; return true; }
            if (raw == "1") { value = true; return true; }
            if (raw == "0") { value = false; return true; }
            return false;
        }
    }
}

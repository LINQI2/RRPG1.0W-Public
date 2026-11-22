using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Rule / Default 行（Row 3）与 Type（Row 2）解析器。
    ///
    /// 语法版本：spike-a-rules/3（T1.2 Schema Semantics Cleanup；T2 Baseline Candidate）
    ///
    /// Row 2（Type）：恰好一个类型 token
    ///   id | string | int | float | bool | ref:SheetName
    ///
    /// Row 3（Rule / Default）：空格 / 制表符 / 分号 / 换行分隔的 token 序列
    ///   optional            允许为空（IR 中为 null）
    ///   key                 复合身份 component（仅"没有 id 列"的 Sheet 使用）
    ///   default=VALUE       空单元格确定性取默认值（= 后必须紧跟值）
    ///   >=N / <=N           数值范围（仅 int / float；必须紧跟数字）
    ///
    /// 身份语义（v3 收敛后只有一套）：
    ///   - <c>id</c> = 单列主键：天然 required + non-empty + Sheet 内唯一 + 排序键；**不得**再写 key；
    ///   - <b>没有 id 的 Sheet</b>用 <c>key</c> 声明复合身份：component 各自**允许重复**，
    ///     但整体必须唯一；Canonical 输出顺序 = 复合 key 升序；物理行序无语义。
    ///
    /// v3 与 v2 的差别（v2 不静默沿用；见 Evidence/RuleGrammarV3.md）：
    ///   - <c>unique</c> 已删除：v2 把 unique 当"参与复合 key"用，与人类/数据建模语义不符 → 出现即 Hard Error（提示用 key）；
    ///   - <c>default=&gt;VALUE</c> 改为 <c>default=VALUE</c>（更短、无 token 解析歧义）→ 旧写法出现即 Hard Error；
    ///   - 内部模型不再保留"unique 字段集合自动组成 key"的歧义路径。
    ///
    /// 硬约束（Hard Error）：
    ///   - 未知 token；同一 token 重复；矛盾组合（optional+default、id/ref+optional、id/ref+default、min>max、
    ///     范围规则类型不适用）；required / unique / default=&gt; / enum: 等已删除写法；
    ///   - id 列再写 key（两种身份声明叠加）。
    /// </summary>
    public static class RuleParser
    {
        public const string GrammarVersion = "spike-a-rules/3";

        private static readonly string[] Separators = new[] { ";", "\n", "\r", "\t", " " };

        private static readonly Regex OperatorWhitespace = new Regex(
            @"(default=|>=|<=)[ \t]", RegexOptions.CultureInvariant);

        /// <summary>
        /// 解析一列的 Type / Rule。永远返回 FieldSpec（不返回 null），错误通过 bag 报告，
        /// 让同一次运行能一次性列出该 Sheet 的全部问题（不 fail-fast 单点报错）。
        /// </summary>
        public static FieldSpec Parse(string rawType, string rawRules, int columnIndex,
            int typeRow, int ruleRow, DiagBag bag, string field)
        {
            var spec = new FieldSpec
            {
                RawType = (rawType ?? string.Empty).Trim(),
                Field = field,
                ColumnIndex = columnIndex,
                ColumnLetter = ColumnLetter(columnIndex)
            };

            ParseType(spec, bag, typeRow);
            ParseRules(spec, rawRules, bag, ruleRow);
            Finalize(spec, bag, ruleRow);
            return spec;
        }

        // ---------------- Type 行 ----------------

        private static void ParseType(FieldSpec spec, DiagBag bag, int typeRow)
        {
            string raw = spec.RawType;

            if (string.IsNullOrEmpty(raw))
            {
                bag.Error(DiagnosticCodes.SchTypeRowEmpty,
                    "Row " + typeRow + "（Type）为空：每一列都必须声明类型。",
                    column: spec.Field, row: typeRow);
                spec.Kind = FieldKind.Text;
                return;
            }

            var tokens = SplitTokens(raw);

            if (tokens.Count > 1)
            {
                bag.Error(DiagnosticCodes.SchTypeMultiple,
                    "Row " + typeRow + "（Type）声明了多个类型 token（" + string.Join(" ", tokens)
                    + "）：一列只能有一个类型。", column: spec.Field, row: typeRow, rule: raw);
                spec.Kind = FieldKind.Text;
                return;
            }

            string token = tokens.Count == 1 ? tokens[0] : string.Empty;
            spec.RawType = token;

            if (token == "id") { spec.Kind = FieldKind.Id; spec.Required = true; return; }
            if (token == "string") { spec.Kind = FieldKind.Text; return; }
            if (token == "int") { spec.Kind = FieldKind.Int; return; }
            if (token == "float") { spec.Kind = FieldKind.Float; return; }
            if (token == "bool") { spec.Kind = FieldKind.Bool; return; }

            if (token.StartsWith("ref:", StringComparison.Ordinal))
            {
                spec.Kind = FieldKind.Ref;
                spec.TypeArgument = token.Substring(4).Trim();
                spec.Required = true; // ref 隐含 required（引用必须存在）
                if (spec.TypeArgument.Length == 0)
                {
                    bag.Error(DiagnosticCodes.SchTypeUnknown,
                        "Row " + typeRow + "（Type）的 ref: 缺少目标 Sheet 名。",
                        column: spec.Field, row: typeRow, rule: token);
                }
                return;
            }

            if (token.StartsWith("enum:", StringComparison.Ordinal))
            {
                bag.Error(DiagnosticCodes.SchTypeUnknown,
                    "enum: 在 spike-a-rules/2 不受支持（v1 的\"只识别不校验成员\"属假安全，已在 T1.1 移除）。"
                    + "请改用 string，或等待未来独立的 enum Authority 实验。",
                    column: spec.Field, row: typeRow, rule: token);
                spec.Kind = FieldKind.Text;
                return;
            }

            bag.Error(DiagnosticCodes.SchTypeUnknown,
                "Row " + typeRow + "（Type）出现未知类型 token '" + token
                + "'（v2 允许：id / string / int / float / bool / ref:X）。",
                column: spec.Field, row: typeRow, rule: token);
            spec.Kind = FieldKind.Text;
        }

        // ---------------- Rule / Default 行 ----------------

        private static void ParseRules(FieldSpec spec, string rawRules, DiagBag bag, int ruleRow)
        {
            string raw = rawRules ?? string.Empty;

            if (OperatorWhitespace.IsMatch(raw))
            {
                var m = OperatorWhitespace.Match(raw);
                bag.Error(DiagnosticCodes.SchRuleOperatorWhitespace,
                    "操作符 '" + m.Groups[1].Value + "' 后不允许空白：必须写成 " + m.Groups[1].Value + "值。",
                    column: spec.Field, row: ruleRow, rule: m.Groups[1].Value);
            }

            var tokens = SplitTokens(raw);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var token in tokens)
            {
                if (!seen.Add(token))
                {
                    bag.Error(DiagnosticCodes.SchRuleDuplicateToken,
                        "规则 token '" + token + "' 重复声明。", column: spec.Field, row: ruleRow, rule: token);
                    continue;
                }

                spec.RuleTokens.Add(token);
                ApplyToken(spec, token, bag, ruleRow);
            }
        }

        private static void ApplyToken(FieldSpec spec, string token, DiagBag bag, int ruleRow)
        {
            switch (token)
            {
                case "required":
                    bag.Error(DiagnosticCodes.SchRequiredTokenRemoved,
                        "spike-a-rules/3 已删除 required token：字段默认就是必填。"
                        + "如需允许为空请写 optional（或 default=VALUE）。",
                        column: spec.Field, row: ruleRow, rule: token);
                    return;
                case "unique":
                    bag.Error(DiagnosticCodes.SchUniqueTokenRemoved,
                        "spike-a-rules/3 不支持 unique token。"
                        + "如果你的意图是「参与复合身份 / 排序键」，请使用 key；"
                        + "真正的 non-key 单列 unique constraint 尚未在 Spike v3 中设计。",
                        column: spec.Field, row: ruleRow, rule: token);
                    return;
                case "optional":
                    spec.Optional = true;
                    return;
                case "key":
                    spec.KeyDeclared = true;
                    return;
                case "id":
                case "string":
                case "int":
                case "float":
                case "bool":
                    bag.Error(DiagnosticCodes.SchTypeTokenInRuleRow,
                        "Row " + ruleRow + "（Rule）不允许出现类型 token '" + token + "'：类型只能写在 Row 2。",
                        column: spec.Field, row: ruleRow, rule: token);
                    return;
            }

            if (token.StartsWith("ref:", StringComparison.Ordinal))
            {
                bag.Error(DiagnosticCodes.SchTypeTokenInRuleRow,
                    "Row " + ruleRow + "（Rule）不允许出现类型 token '" + token + "'：类型只能写在 Row 2。",
                    column: spec.Field, row: ruleRow, rule: token);
                return;
            }

            if (token.StartsWith("default=>", StringComparison.Ordinal))
            {
                bag.Error(DiagnosticCodes.SchLegacyDefaultArrow,
                    "spike-a-rules/3 已将 default=>VALUE 改为 default=VALUE（更短、无 token 解析歧义）："
                    + "请把 'default=>" + token.Substring("default=>".Length) + "' 写成 'default="
                    + token.Substring("default=>".Length) + "'。",
                    column: spec.Field, row: ruleRow, rule: token);
                return;
            }

            if (token.StartsWith("default=", StringComparison.Ordinal))
            {
                string value = token.Substring("default=".Length);
                if (value.Length == 0)
                {
                    bag.Error(DiagnosticCodes.SchRuleInvalidOperand,
                        "default= 后必须紧跟默认值（例如 default=enemy）。",
                        column: spec.Field, row: ruleRow, rule: token);
                    return;
                }
                spec.HasDefault = true;
                spec.DefaultRaw = value;
                return;
            }

            if (token.StartsWith("allowed=", StringComparison.Ordinal))
            {
                string raw = token.Substring("allowed=".Length);
                if (raw.Length == 0)
                {
                    bag.Error(DiagnosticCodes.SchRuleInvalidOperand,
                        "allowed= 后必须紧跟允许值（例如 allowed=UISprite；多个值用 | 分隔且不要加空格）。",
                        column: spec.Field, row: ruleRow, rule: token);
                    return;
                }

                if (spec.HasAllowedSet)
                {
                    bag.Error(DiagnosticCodes.SchRuleConflict,
                        "allowed= 只能声明一次（已声明 allowed=" + spec.AllowedRaw + "）。",
                        column: spec.Field, row: ruleRow, rule: token);
                    return;
                }

                spec.HasAllowedSet = true;
                spec.AllowedRaw = raw;

                foreach (var part in raw.Split('|'))
                {
                    string member = part.Trim();
                    if (member.Length == 0)
                    {
                        bag.Error(DiagnosticCodes.SchAllowedEmptyMember,
                            "allowed= 出现空成员（检查 '|' 两侧，例如 allowed=A||B）。",
                            column: spec.Field, row: ruleRow, rule: token);
                        continue;
                    }

                    if (spec.AllowedValues.Contains(member))
                    {
                        bag.Error(DiagnosticCodes.SchRuleDuplicateToken,
                            "allowed= 成员 '" + member + "' 重复声明。",
                            column: spec.Field, row: ruleRow, rule: token);
                        continue;
                    }

                    spec.AllowedValues.Add(member);
                }
                return;
            }

            if (token.StartsWith(">=", StringComparison.Ordinal))
            {
                spec.MinRaw = token.Substring(2);
                return;
            }

            if (token.StartsWith("<=", StringComparison.Ordinal))
            {
                spec.MaxRaw = token.Substring(2);
                return;
            }

            if (token == "default=>" || token == "default=" || token == ">=" || token == "<=" || token == "allowed=")
            {
                bag.Error(DiagnosticCodes.SchRuleInvalidOperand,
                    "操作符 '" + token + "' 缺少值。", column: spec.Field, row: ruleRow, rule: token);
                return;
            }

            if (IsPlainNumber(token))
            {
                bag.Error(DiagnosticCodes.SchRuleUnknownToken,
                    "Row " + ruleRow + "（规则）写了数字 '" + token + "'。"
                    + "Row 3 留空就表示这个字段必填，不需要写任何东西；"
                    + "如果要限制数值范围，请写 >=1 / <=N（例如 >=1;<=100）。",
                    column: spec.Field, row: ruleRow, rule: token);
                return;
            }

            bag.Error(DiagnosticCodes.SchRuleUnknownToken,
                "未知规则 token '" + token + "'（rrpg-rules/1 允许：optional / key / "
                + "default=VALUE / >=N / <=N / allowed=V1|V2）。", column: spec.Field, row: ruleRow, rule: token);
        }

        /// <summary>
        /// T4.2：纯数字 token。制作人很容易在 Row 3（规则）里手写一个 1 ——
        /// 那不是合法规则（留空即必填），需要单独的、看得懂的提示，而不是笼统的「未知 token」。
        /// </summary>
        private static bool IsPlainNumber(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (!char.IsDigit(token[i])) return false;
            }

            return true;
        }

        // ---------------- 冲突与语义检查 ----------------

        private static void Finalize(FieldSpec spec, DiagBag bag, int ruleRow)
        {
            bool impliedRequired = spec.Kind == FieldKind.Id || spec.Kind == FieldKind.Ref;

            if (spec.Required && spec.Optional)
            {
                bag.Error(DiagnosticCodes.SchRuleConflict,
                    "required 与 optional 不能同时声明。",
                    column: spec.Field, row: ruleRow, rule: "required+optional");
            }

            if (impliedRequired && spec.Optional)
            {
                bag.Error(DiagnosticCodes.SchRuleConflict,
                    "类型 " + spec.RawType + " 隐含必填，不能再声明 optional。",
                    column: spec.Field, row: ruleRow, rule: "optional");
            }

            // v2：optional 与 default= 同时对同一列描述"空值怎么处理"，语义冲突。
            if (spec.Optional && spec.HasDefault)
            {
                bag.Error(DiagnosticCodes.SchRuleConflict,
                    "optional 与 default= 不能同时声明：前者允许空值，后者在空值时填默认值。",
                    column: spec.Field, row: ruleRow, rule: "optional+default=" + spec.DefaultRaw);
            }

            // v3：id 列已经是单列主键，再写 key = 两种身份声明叠加
            if (spec.Kind == FieldKind.Id && spec.KeyDeclared)
            {
                bag.Error(DiagnosticCodes.SchIdWithKey,
                    "id 列已经是单列主键（required + Sheet 内唯一 + 排序键），不得再写 key。"
                    + "如果该 Sheet 需要复合身份，请移除 id 列并改用 key 列。",
                    column: spec.Field, row: ruleRow, rule: "key");
            }

            // v2 默认必填：没有 optional / default=> 的字段必须有值（无需显式写 required）
            if (impliedRequired)
            {
                spec.Required = true;
                spec.Optional = false;
            }
            else if (spec.Optional || spec.HasDefault)
            {
                spec.Required = false;
            }
            else
            {
                spec.Required = true;
            }

            if (spec.MinRaw != null || spec.MaxRaw != null)
            {
                if (!spec.IsNumeric)
                {
                    bag.Error(DiagnosticCodes.SchRuleNotApplicable,
                        ">= / <= 只适用于 int / float（当前类型：" + spec.RawType + "）。",
                        column: spec.Field, row: ruleRow, rule: spec.MinRaw != null ? ">=" : "<=");
                }
                else
                {
                    spec.HasMin = TryParseNumeric(spec.MinRaw, spec.Kind, out double min, bag, spec, ruleRow, ">=");
                    if (spec.HasMin) spec.MinValue = min;

                    spec.HasMax = TryParseNumeric(spec.MaxRaw, spec.Kind, out double max, bag, spec, ruleRow, "<=");
                    if (spec.HasMax) spec.MaxValue = max;

                    if (spec.HasMin && spec.HasMax && spec.MinValue > spec.MaxValue)
                    {
                        bag.Error(DiagnosticCodes.SchRuleConflict,
                            "下界 " + spec.MinValue.ToString(CultureInfo.InvariantCulture)
                            + " 大于上界 " + spec.MaxValue.ToString(CultureInfo.InvariantCulture) + "。",
                            column: spec.Field, row: ruleRow, rule: spec.MinRaw + "+" + spec.MaxRaw);
                    }
                }
            }

            if (spec.HasDefault)
            {
                if (spec.Kind == FieldKind.Id)
                {
                    bag.Error(DiagnosticCodes.SchRuleConflict,
                        "id 列不允许 default（身份不能被默认值创造）。",
                        column: spec.Field, row: ruleRow, rule: "default=" + spec.DefaultRaw);
                }
                else if (spec.Kind == FieldKind.Ref)
                {
                    bag.Error(DiagnosticCodes.SchRuleConflict,
                        "ref 列不允许 default（引用必须显式书写）。",
                        column: spec.Field, row: ruleRow, rule: "default=" + spec.DefaultRaw);
                }
                else if (!IsDefaultCompatible(spec))
                {
                    bag.Error(DiagnosticCodes.SchRuleDefaultTypeMismatch,
                        "默认值 '" + spec.DefaultRaw + "' 与类型 " + spec.RawType + " 不符。",
                        column: spec.Field, row: ruleRow, rule: "default=" + spec.DefaultRaw);
                }
            }

            // T4 扩展：allowed=V1|V2 只对 string（Text）列生效
            // （id 的唯一性、ref 的目标存在性、数值的范围各有专门机制，不允许叠加第三套声明）。
            if (spec.HasAllowedSet)
            {
                if (spec.Kind != FieldKind.Text)
                {
                    bag.Error(DiagnosticCodes.SchAllowedNotApplicable,
                        "allowed= 只适用于 string 列（当前类型：" + spec.RawType + "）。",
                        column: spec.Field, row: ruleRow, rule: "allowed=" + spec.AllowedRaw);
                }

                if (spec.HasDefault && spec.DefaultRaw != null && spec.AllowedValues.Count > 0
                    && !spec.AllowedValues.Contains(spec.DefaultRaw))
                {
                    bag.Error(DiagnosticCodes.SchAllowedDefaultNotMember,
                        "默认值 '" + spec.DefaultRaw + "' 不在 allowed 集合内：默认值本身也必须是合法取值。",
                        column: spec.Field, row: ruleRow, rule: "default=" + spec.DefaultRaw);
                }
            }
        }

        private static bool IsDefaultCompatible(FieldSpec spec)
        {
            switch (spec.Kind)
            {
                case FieldKind.Int:
                    return long.TryParse(spec.DefaultRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case FieldKind.Float:
                    return double.TryParse(spec.DefaultRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case FieldKind.Bool:
                    return bool.TryParse(spec.DefaultRaw, out _);
                default:
                    return !string.IsNullOrEmpty(spec.DefaultRaw);
            }
        }

        private static bool TryParseNumeric(string raw, FieldKind kind, out double value,
            DiagBag bag, FieldSpec spec, int ruleRow, string rule)
        {
            value = 0;
            if (raw == null) return false;

            if (kind == FieldKind.Int)
            {
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                {
                    bag.Error(DiagnosticCodes.SchRuleInvalidOperand,
                        rule + " 的操作数必须是整数（当前：" + raw + "）。",
                        column: spec.Field, row: ruleRow, rule: rule + raw);
                    return false;
                }
                value = l;
                return true;
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                bag.Error(DiagnosticCodes.SchRuleInvalidOperand,
                    rule + " 的操作数必须是数值（当前：" + raw + "）。",
                    column: spec.Field, row: ruleRow, rule: rule + raw);
                return false;
            }
            value = d;
            return true;
        }

        /// <summary>规则行 token 切分（确定性；保留原顺序）。</summary>
        public static List<string> SplitTokens(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;

            foreach (var part in raw.Split(Separators, StringSplitOptions.None))
            {
                string t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list;
        }

        /// <summary>1-based 列号 → Excel 列字母。</summary>
        public static string ColumnLetter(int columnIndex)
        {
            string s = string.Empty;
            int n = columnIndex;
            while (n > 0)
            {
                int rem = (n - 1) % 26;
                s = (char)('A' + rem) + s;
                n = (n - 1) / 26;
            }
            return s;
        }
    }
}

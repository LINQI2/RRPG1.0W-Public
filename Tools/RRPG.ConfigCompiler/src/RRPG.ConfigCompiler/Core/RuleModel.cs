using System.Collections.Generic;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>字段类型（Row 2 = Type 行解析结果）。</summary>
    public enum FieldKind
    {
        Id = 0,
        Text = 1,
        Int = 2,
        Float = 3,
        Bool = 4,
        Ref = 5,
        Enum = 6
    }

    /// <summary>
    /// 一个列的 Schema（Row 1 机器字段 + Row 2 类型 + Row 3 规则 + Row 4 中文 Header）。
    /// 这是 Compiler 内部唯一 Authority；不再从别处推断列语义。
    /// </summary>
    public sealed class FieldSpec
    {
        /// <summary>Row 1：机器字段名 = JSON key（唯一权威）。</summary>
        public string Field;

        /// <summary>Row 4：高视觉权重中文 Header（仅人读；缺失只告警）。</summary>
        public string DisplayHeader;

        /// <summary>Row 2 原始类型 token（如 id / int / ref:Entity）。</summary>
        public string RawType;

        public FieldKind Kind;

        /// <summary>ref:X 的 X；其它类型为 null。</summary>
        public string TypeArgument;

        public bool Required;
        public bool Optional;

        /// <summary>
        /// Row 3 显式声明了 <c>key</c>（复合身份 component）。
        /// v3 语义：id 列已经是单列主键，**不得**再写 key（SCH023 Hard Error）。
        /// </summary>
        public bool KeyDeclared;

        public bool HasDefault;
        public string DefaultRaw;

        public string MinRaw;
        public string MaxRaw;
        public double MinValue;
        public double MaxValue;
        public bool HasMin;
        public bool HasMax;

        /// <summary>
        /// Row 3 声明了 <c>allowed=V1|V2</c>（允许值集合；T4 新增，只允许用在 string 列）。
        /// 语义：单元格值必须命中集合，否则 Hard Error（DAT012_ALLOWED_VIOLATION）。
        /// 这是**声明式**的取值域：集合写在作者看得见的 Row 3，不藏在 C# 常量里。
        /// </summary>
        public bool HasAllowedSet;

        /// <summary>allowed= 的原始文本（诊断与 IR 溯源用）。</summary>
        public string AllowedRaw;

        /// <summary>集合成员（保持作者书写顺序；空成员已单独报错、重复成员已按重复 token 报错）。</summary>
        public List<string> AllowedValues = new List<string>();

        /// <summary>Row 3 的原始 token 列表（保持作者顺序；已做过重复/冲突检查）。</summary>
        public List<string> RuleTokens = new List<string>();

        /// <summary>1-based 列号与 Excel 列字母（诊断与 IR 溯源用）。</summary>
        public int ColumnIndex;
        public string ColumnLetter;

        public bool IsNumeric { get { return Kind == FieldKind.Int || Kind == FieldKind.Float; } }

        /// <summary>该列是否为单列主键（仅 id 类型）。</summary>
        public bool IsPrimaryKey { get { return Kind == FieldKind.Id; } }

        /// <summary>该列是否为复合身份 component（显式 key，且不是 id 列）。</summary>
        public bool IsCompositeKeyComponent
        {
            get { return KeyDeclared && Kind != FieldKind.Id; }
        }
    }

    /// <summary>
    /// 一个数据 Sheet 的 Schema。
    ///
    /// 身份语义（v3，唯一一套）：
    ///   - 有 id 列 → 单列主键（id 天然 required + unique + 排序键）；
    ///   - 无 id 列 → 显式 key 列组成复合身份（component 各自可重复，整体必须唯一）；
    ///   - 二者只能存在一种；同时出现 = Hard Error（SCH023）。
    /// </summary>
    public sealed class SheetSpec
    {
        /// <summary>Sheet 名（= Canonical IR 的实体名与输出文件名）。</summary>
        public string SheetName;

        /// <summary>相对仓库根的 workbook 路径（正斜杠）。</summary>
        public string Workbook;

        public List<FieldSpec> Fields = new List<FieldSpec>();

        /// <summary>
        /// 身份列 + Canonical 输出排序键（同一件事，不再分开）：
        /// 有 id → [id]；无 id → 声明了 key 的列（按声明顺序）。
        /// </summary>
        public List<FieldSpec> KeyFields = new List<FieldSpec>();

        /// <summary>该 Sheet 的 ref 列（子表关系来源）。</summary>
        public List<FieldSpec> RefFields = new List<FieldSpec>();

        /// <summary>身份类型（CompositeKey / PrimaryKey / Missing）。</summary>
        public SheetIdentityKind Identity;

        public bool IsChildSheet { get { return RefFields.Count > 0; } }

        public int ColumnCount { get { return Fields.Count; } }
    }

    /// <summary>Sheet 身份类型（v3）。</summary>
    public enum SheetIdentityKind
    {
        /// <summary>没有 id 也没有 key：无法确定身份（Hard Error）。</summary>
        Missing = 0,

        /// <summary>id 列：单列主键。</summary>
        PrimaryKey = 1,

        /// <summary>一个或多个 key 列：复合身份（component 各自可重复）。</summary>
        CompositeKey = 2
    }

    /// <summary>单个单元格的编译结果（含来源与默认值标记，便于诊断与报告）。</summary>
    public sealed class CompiledCell
    {
        public FieldSpec Field;

        /// <summary>原始文本（作者在 Excel 中看到的内容）。</summary>
        public string Raw;

        /// <summary>是否使用了 default（原始为空 → 取默认值）。</summary>
        public bool FromDefault;

        /// <summary>类型化值：string / int(long) / double / bool / null（optional 空值）。</summary>
        public object Value;
    }

    /// <summary>一行数据（已通过 schema + data 校验）。</summary>
    public sealed class CompiledRow
    {
        public SheetSpec Sheet;
        public int ExcelRow;
        public List<CompiledCell> Cells = new List<CompiledCell>();

        public CompiledCell Get(string field)
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].Field.Field == field) return Cells[i];
            }
            return null;
        }
    }

    /// <summary>一个 Sheet 的完整编译结果。</summary>
    public sealed class CompiledSheet
    {
        public SheetSpec Spec;
        public List<CompiledRow> Rows = new List<CompiledRow>();

        /// <summary>物理行顺序（Excel 中的顺序）；Canonical 输出必须按 KeyFields 排序。</summary>
        public List<int> PhysicalRowOrder = new List<int>();
    }
}

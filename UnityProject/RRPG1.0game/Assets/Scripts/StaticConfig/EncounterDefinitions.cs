using System;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础）的 Runtime 只读记录集合。
    ///
    /// 边界（本轮明确不做）：
    ///   - 这些类型是**生成物**（Canonical Mirror → Unity Builder → EncounterConfigDatabase），不是人工 Authoring 对象；
    ///   - 它们只描述「一场遭遇怎么组成」的静态事实，**不**生成敌人、不做威胁预算求解、不做最终阵型求解；
    ///   - 字段名 = Mirror（JSON）字段名，一一对应，不在这里改变语义。
    ///
    /// 身份语义：
    ///   - Encounter / EncounterSlot / EncounterPool：单列主键；
    ///   - EncounterSlotRule：复合身份 = SlotId + RuleMode + Dimension + Value；
    ///   - EncounterPoolEntry：复合身份 = PoolId + EncounterId；
    ///   - MonsterEncounterProfile：主键 = MonsterId（一怪一行）；
    ///   - MonsterRole：复合身份 = MonsterId + Role（同一只怪可以有多行）。
    ///
    /// 未冻结提示：ThreatBudgetMin / Max、ThreatCost、RandomWeight、Weight、MinDepth / MaxDepth
    /// 当前全部是 **Test / Not Frozen 测试数值**，不构成正式平衡。
    /// </summary>
    [Serializable]
    public sealed class EncounterDefinition
    {
        /// <summary>身份（Canonical 输出按它排序）。</summary>
        public string EncounterId;

        /// <summary>中文名称（仅供展示；不参与生成逻辑）。</summary>
        public string DisplayName;

        /// <summary>是否参与抽取。</summary>
        public bool Enabled;

        /// <summary>最低威胁预算（Test / Not Frozen）。</summary>
        public int ThreatBudgetMin;

        /// <summary>最高威胁预算（Test / Not Frozen；不得小于最低值）。</summary>
        public int ThreatBudgetMax;

        /// <summary>是否允许同种怪重复出现。</summary>
        public bool AllowDuplicateMonster;

        /// <summary>字段数常量（与 Mirror 的 6 列契约一致；用于校验）。</summary>
        public const int FieldCount = 6;

        public override string ToString()
        {
            return EncounterId + "(" + DisplayName + ") enabled=" + Enabled
                + " budget=" + ThreatBudgetMin + "~" + ThreatBudgetMax
                + " dup=" + AllowDuplicateMonster;
        }
    }

    /// <summary>组合需求位：一位 = 该遭遇需要「若干只符合条件的怪」。</summary>
    [Serializable]
    public sealed class EncounterSlotDefinition
    {
        /// <summary>身份。</summary>
        public string SlotId;

        /// <summary>所属遭遇模板（必须命中 EncounterDefinition.EncounterId）。</summary>
        public string EncounterId;

        /// <summary>选怪方式：FixedMonster / Query。</summary>
        public string SelectionMode;

        /// <summary>固定怪物ID（仅 SelectionMode = FixedMonster 时有值）。</summary>
        public string FixedMonsterId;

        /// <summary>最少数量。</summary>
        public int CountMin;

        /// <summary>最多数量（不得小于 CountMin）。</summary>
        public int CountMax;

        /// <summary>站位模式：Any / Row / Exact。</summary>
        public string PositionMode;

        /// <summary>指定排：Front / Middle / Back（Row / Exact 时有值）。</summary>
        public string Row;

        /// <summary>指定列：Left / Center / Right（Exact 时有值）。</summary>
        public string Column;

        /// <summary>字段数常量（与 Mirror 的 9 列契约一致；用于校验）。</summary>
        public const int FieldCount = 9;

        public override string ToString()
        {
            return SlotId + " → " + EncounterId + " [" + SelectionMode + " " + CountMin + "~" + CountMax
                + " pos=" + PositionMode + "/" + Row + "/" + Column + "]";
        }
    }

    /// <summary>组合需求位筛选规则（Value 的含义取决于同一行的 Dimension）。</summary>
    [Serializable]
    public sealed class EncounterSlotRuleDefinition
    {
        public string SlotId;

        /// <summary>规则方式：AnyOf / Require / Exclude。</summary>
        public string RuleMode;

        /// <summary>筛选维度：Role / Tier / MonsterId。</summary>
        public string Dimension;

        /// <summary>筛选值（取值域由 Dimension 决定；合法性由 Compiler 校验）。</summary>
        public string Value;

        /// <summary>字段数常量（与 Mirror 的 4 列契约一致；用于校验）。</summary>
        public const int FieldCount = 4;

        public override string ToString()
        {
            return SlotId + " " + RuleMode + " " + Dimension + "=" + Value;
        }
    }

    /// <summary>遭遇池（命名容器；本轮刻意保持最小）。</summary>
    [Serializable]
    public sealed class EncounterPoolDefinition
    {
        public string PoolId;
        public string DisplayName;
        public bool Enabled;

        /// <summary>字段数常量（与 Mirror 的 3 列契约一致；用于校验）。</summary>
        public const int FieldCount = 3;

        public override string ToString()
        {
            return PoolId + "(" + DisplayName + ") enabled=" + Enabled;
        }
    }

    /// <summary>遭遇池条目：池里有哪些遭遇、权重、适用远征深度区间。</summary>
    [Serializable]
    public sealed class EncounterPoolEntryDefinition
    {
        public string PoolId;
        public string EncounterId;

        /// <summary>抽取权重（Test / Not Frozen；必须 &gt; 0）。</summary>
        public int Weight;

        /// <summary>最低远征深度（Test / Not Frozen）。</summary>
        public int MinDepth;

        /// <summary>最高远征深度（Test / Not Frozen；不得小于最低值）。</summary>
        public int MaxDepth;

        /// <summary>字段数常量（与 Mirror 的 5 列契约一致；用于校验）。</summary>
        public const int FieldCount = 5;

        public override string ToString()
        {
            return PoolId + " / " + EncounterId + " weight=" + Weight + " depth=" + MinDepth + "~" + MaxDepth;
        }
    }

    /// <summary>怪物遭遇画像（写在 Monster 工作簿；一怪一行）。</summary>
    [Serializable]
    public sealed class MonsterEncounterProfileDefinition
    {
        public string MonsterId;

        /// <summary>威胁值（Test / Not Frozen；必须 &gt; 0）。</summary>
        public int ThreatCost;

        /// <summary>怪物级别：Normal / Elite / Boss。</summary>
        public string Tier;

        /// <summary>基础随机权重（Test / Not Frozen；必须 &gt; 0）。</summary>
        public int RandomWeight;

        /// <summary>字段数常量（与 Mirror 的 4 列契约一致；用于校验）。</summary>
        public const int FieldCount = 4;

        public override string ToString()
        {
            return MonsterId + " threat=" + ThreatCost + " tier=" + Tier + " weight=" + RandomWeight;
        }
    }

    /// <summary>怪物战斗定位（同一只怪可以有多行：Melee + Tank）。</summary>
    [Serializable]
    public sealed class MonsterRoleDefinition
    {
        public string MonsterId;

        /// <summary>战斗定位：Melee / Ranged / Tank / Support。</summary>
        public string Role;

        /// <summary>字段数常量（与 Mirror 的 2 列契约一致；用于校验）。</summary>
        public const int FieldCount = 2;

        public override string ToString()
        {
            return MonsterId + " → " + Role;
        }
    }
}

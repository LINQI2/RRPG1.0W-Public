using System;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// WP-Event-1A：Event 领域（统一事件内容基础）的 Runtime 只读记录集合。
    ///
    /// 边界（本轮明确不做）：
    ///   - 这些类型是**生成物**（Canonical Mirror → Unity Builder → EventConfigDatabase），不是人工 Authoring 对象；
    ///   - 它们只描述「一个事件被配置成什么样」的静态事实，**不**求解事件、**不**绑定人物、
    ///     **不**判断条件、**不**执行效果、**不**渲染模板（属后续技术检查点 1B / 1C / 1D）；
    ///   - 字段名 = Mirror（JSON）字段名，一一对应，不在这里改变语义；
    ///   - 可选整数参数（<c>IntValue</c>）用「值 + 是否存在」两个成员表达：
    ///     Unity 不能序列化 <c>int?</c>，而「Mirror 里是 null」与「值是 0」是两件不同的事
    ///     （例如 VillageResourceDelta 要求 IntValue ≠ 0，0 是非法值而不是「没有参数」）。
    ///
    /// 身份语义：
    ///   - Event / EventPresentation：单列主键（EventPresentation 的身份就是 EventId → 一个事件恰好一条表现）；
    ///   - EventTrigger / EventRole / EventRoleCondition / EventCondition / EventOption /
    ///     EventOptionCondition / EventOutcome / EventEffect：各自 id（全局唯一）。
    ///
    /// 未冻结提示：权重、冷却值、条件阈值、效果数值与文案当前全部是 **Test / Not Frozen** 配置内容。
    /// </summary>
    [Serializable]
    public sealed class EventDefinition
    {
        /// <summary>身份（Canonical 输出按它排序）。</summary>
        public string EventId;

        /// <summary>制作人识别名称（仅供展示）。</summary>
        public string DisplayName;

        /// <summary>是否启用（未启用 = 制作人草稿，允许不完整）。</summary>
        public bool Enabled;

        /// <summary>候选抽取权重（Test / Not Frozen；必须 &gt; 0）。</summary>
        public int Weight;

        /// <summary>重复规则：Repeatable / OncePerExpedition / OncePerWorld。</summary>
        public string RepeatPolicy;

        /// <summary>冷却类型：None / VillageDays / ExpeditionNodes。</summary>
        public string CooldownType;

        /// <summary>冷却值（CooldownType = None 时为 0）。</summary>
        public int CooldownValue;

        /// <summary>字段数常量（与 Mirror 的 7 列契约一致；用于校验）。</summary>
        public const int FieldCount = 7;

        public override string ToString()
        {
            return EventId + "(" + DisplayName + ") enabled=" + Enabled + " weight=" + Weight
                + " repeat=" + RepeatPolicy + " cooldown=" + CooldownType + "/" + CooldownValue;
        }
    }

    /// <summary>触发时机（一个事件可以有多条；正式触发时机只能来自同一个 Domain）。</summary>
    [Serializable]
    public sealed class EventTriggerDefinition
    {
        public string TriggerId;

        /// <summary>所属事件（必须命中 EventDefinition.EventId）。</summary>
        public string EventId;

        /// <summary>触发时机：VillageDaySettled / VillageRecognitionRaised / VillageProjectCompleted /
        /// ExpeditionEventNodeReached / ExpeditionBattleResolved / ExpeditionReturned / ManualDebug。</summary>
        public string TriggerType;

        /// <summary>字段数常量（与 Mirror 的 3 列契约一致；用于校验）。</summary>
        public const int FieldCount = 3;

        public override string ToString()
        {
            return TriggerId + " → " + EventId + " @ " + TriggerType;
        }
    }

    /// <summary>事件角色（V0.1：一个 Role 最终只绑定一个 Person；不支持 Group Role）。</summary>
    [Serializable]
    public sealed class EventRoleDefinition
    {
        public string RoleId;
        public string EventId;

        /// <summary>中文角色名（仅供展示 / 模板使用）。</summary>
        public string RoleName;

        /// <summary>人物选择方式：ContextPerson / VillagePerson / ExpeditionPartyMember / FixedPerson。</summary>
        public string SelectorType;

        /// <summary>Context 来源键（仅 ContextPerson 有值）。</summary>
        public string SourceKey;

        /// <summary>固定故事NPC（仅 FixedPerson 有值）。</summary>
        public string FixedPersonId;

        /// <summary>字段数常量（与 Mirror 的 6 列契约一致；用于校验）。</summary>
        public const int FieldCount = 6;

        public override string ToString()
        {
            return RoleId + "(" + RoleName + ") " + SelectorType + " @ " + EventId;
        }
    }

    /// <summary>事件角色筛选条件（同一个 Role 下：全部条件 = AND）。</summary>
    [Serializable]
    public sealed class EventRoleConditionDefinition
    {
        public string ConditionId;

        /// <summary>所属角色（必须命中 EventRoleDefinition.RoleId）。</summary>
        public string RoleId;

        /// <summary>条件类型：PersonInVillage / PersonOnExpedition / PersonAvailable。</summary>
        public string ConditionType;

        public string TargetKey;

        /// <summary>可选整数参数；<see cref="HasIntValue"/>=false 表示 Mirror 里是 null。</summary>
        public bool HasIntValue;
        public int IntValue;

        public string StringValue;

        /// <summary>字段数常量（与 Mirror 的 6 列契约一致；用于校验）。</summary>
        public const int FieldCount = 6;

        public override string ToString()
        {
            return ConditionId + " → " + RoleId + " " + ConditionType;
        }
    }

    /// <summary>事件成立条件（同一 GroupId 内 = AND；不同 GroupId = OR）。</summary>
    [Serializable]
    public sealed class EventConditionDefinition
    {
        public string ConditionId;
        public string EventId;

        /// <summary>条件组（同一组内 AND；组之间 OR）。</summary>
        public int GroupId;

        /// <summary>V0.1 条件类型（Context / Flag / History / Person / VillageResource / Expedition 六类）。</summary>
        public string ConditionType;

        /// <summary>针对哪个事件角色（Person* 类条件必填；其它类型为空）。</summary>
        public string SubjectRoleId;

        public string TargetKey;

        /// <summary>可选整数参数；<see cref="HasIntValue"/>=false 表示 Mirror 里是 null。</summary>
        public bool HasIntValue;
        public int IntValue;

        public string StringValue;

        /// <summary>字段数常量（与 Mirror 的 8 列契约一致；用于校验）。</summary>
        public const int FieldCount = 8;

        public override string ToString()
        {
            return ConditionId + " → " + EventId + " group=" + GroupId + " " + ConditionType;
        }
    }

    /// <summary>事件表现（一个事件恰好一条：身份 = EventId）。</summary>
    [Serializable]
    public sealed class EventPresentationDefinition
    {
        /// <summary>身份：所属事件（一个事件只能有一条表现）。</summary>
        public string EventId;

        /// <summary>展示渠道：JourneyCard / VillageModal（必须与事件 Domain 匹配）。</summary>
        public string PresentationChannel;

        /// <summary>标题模板（V0.1 只允许 {RoleId.DisplayName} 与 {Context.Key}）。</summary>
        public string TitleTemplate;

        /// <summary>正文模板（同上）。</summary>
        public string BodyTemplate;

        /// <summary>插图 VisualAssetId（空 = 没有插图；非空必须命中共享 VisualAsset）。</summary>
        public string IllustrationAssetId;

        /// <summary>紧迫性：Timely / Ignorable。</summary>
        public string Urgency;

        /// <summary>打开是否暂停（语义由未来 Runtime 解释；1A 不消费）。</summary>
        public bool PauseOnOpen;

        /// <summary>字段数常量（与 Mirror 的 7 列契约一致；用于校验）。</summary>
        public const int FieldCount = 7;

        public override string ToString()
        {
            return EventId + " " + PresentationChannel + " urgency=" + Urgency
                + " illustration=" + (string.IsNullOrEmpty(IllustrationAssetId) ? "<none>" : IllustrationAssetId);
        }
    }

    /// <summary>事件选项（同一事件内 SortOrder 唯一）。</summary>
    [Serializable]
    public sealed class EventOptionDefinition
    {
        public string OptionId;
        public string EventId;

        /// <summary>显示顺序（同一事件内唯一）。</summary>
        public int SortOrder;

        /// <summary>选项文案模板。</summary>
        public string LabelTemplate;

        /// <summary>辅助说明模板（可空）。</summary>
        public string SubLabelTemplate;

        /// <summary>条件不满足时的表现：Hide / Disable。</summary>
        public string UnavailableMode;

        /// <summary>字段数常量（与 Mirror 的 6 列契约一致；用于校验）。</summary>
        public const int FieldCount = 6;

        public override string ToString()
        {
            return OptionId + " → " + EventId + " order=" + SortOrder + " " + UnavailableMode;
        }
    }

    /// <summary>选项可用条件（同 GroupId 内 AND；不同 GroupId = OR；无条件 = 默认可用）。</summary>
    [Serializable]
    public sealed class EventOptionConditionDefinition
    {
        public string ConditionId;
        public string OptionId;
        public int GroupId;
        public string ConditionType;
        public string SubjectRoleId;
        public string TargetKey;

        /// <summary>可选整数参数；<see cref="HasIntValue"/>=false 表示 Mirror 里是 null。</summary>
        public bool HasIntValue;
        public int IntValue;

        public string StringValue;

        /// <summary>字段数常量（与 Mirror 的 8 列契约一致；用于校验）。</summary>
        public const int FieldCount = 8;

        public override string ToString()
        {
            return ConditionId + " → " + OptionId + " group=" + GroupId + " " + ConditionType;
        }
    }

    /// <summary>事件结果（一个 Option 至少一个 Outcome；多个 Outcome = 未来按 Weight 随机）。</summary>
    [Serializable]
    public sealed class EventOutcomeDefinition
    {
        public string OutcomeId;
        public string OptionId;

        /// <summary>结果权重（Test / Not Frozen；必须 &gt; 0）。</summary>
        public int Weight;

        /// <summary>结果文本模板（可空）。</summary>
        public string ResultTextTemplate;

        /// <summary>字段数常量（与 Mirror 的 4 列契约一致；用于校验）。</summary>
        public const int FieldCount = 4;

        public override string ToString()
        {
            return OutcomeId + " → " + OptionId + " weight=" + Weight;
        }
    }

    /// <summary>事件效果（同一 Outcome 内 Order 唯一；StartBattle 必须 Order 最大且最多一个）。</summary>
    [Serializable]
    public sealed class EventEffectDefinition
    {
        public string EffectId;
        public string OutcomeId;

        /// <summary>执行顺序（同一结果内唯一）。</summary>
        public int Order;

        /// <summary>效果类型：SetFlag / ClearFlag / VillageResourceDelta / GrantPersonRest /
        /// SupplyDelta / AdvanceExpeditionTime / StartBattle。</summary>
        public string EffectType;

        /// <summary>目标事件角色（GrantPersonRest 必填；其它类型为空）。</summary>
        public string TargetRoleId;

        /// <summary>目标键（Flag 名 / 村庄资源名 / EncounterPool.PoolId，随 EffectType 解释）。</summary>
        public string TargetKey;

        /// <summary>可选整数参数；<see cref="HasIntValue"/>=false 表示 Mirror 里是 null。</summary>
        public bool HasIntValue;
        public int IntValue;

        public string StringValue;

        /// <summary>字段数常量（与 Mirror 的 8 列契约一致；用于校验）。</summary>
        public const int FieldCount = 8;

        public override string ToString()
        {
            return EffectId + " → " + OutcomeId + " order=" + Order + " " + EffectType
                + " key=" + (string.IsNullOrEmpty(TargetKey) ? "<none>" : TargetKey)
                + " role=" + (string.IsNullOrEmpty(TargetRoleId) ? "<none>" : TargetRoleId);
        }
    }
}

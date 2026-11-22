using System;
using System.Collections.Generic;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 实体的**必需机器字段**（Producer UX；M1 起逐领域登记）。
    ///
    /// 为什么需要它：允许「插列 / 换列 / 加备注列」之后，制作人可能把某个关键字段
    /// 误放进备注列或改名 —— 那样虽然语法上仍然「合法」，但会静默丢掉一个必需字段。
    /// 因此每个**已知实体**显式声明它必须有哪些机器字段；缺失即 Hard Error。
    ///
    /// 注意（边界）：
    ///   - 这里的检查只针对「必需字段是否还在」；它**不**意味着「新字段会被自动理解」。
    ///     新增一个全新的业务字段仍然可能需要 downstream Runtime / Builder 支持（Schema Evolution）。
    ///   - 未知实体（制作人新加的 Sheet）不做必需字段检查，但仍受身份（id / key）与类型规则约束。
    /// </summary>
    public static class EntityRequirements
    {
        private static readonly Dictionary<string, string[]> Required =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "VillagePresentation", new[] { "PresentationKey", "VisualAssetId" } },
                { "BuildingPresentationBinding", new[] { "BuildingDefinitionId", "Lifecycle", "PresentationKey" } },
                { "BuildingDefinition", new[] { "BuildingDefinitionId", "DisplayName", "Category", "FootprintWidth", "FootprintDepth" } },
                // M1（Monster Shadow）：登记 Monster 的必需机器字段。
                // 规则刻意**极简**：只有身份必需 —— 不引入 Spike 的实验性 Gameplay 范围限制
                // （例如「Attack <= 100000」），也不重新设计 Monster Gameplay Schema（用户 M1 §4）。
                { "Monster", new[] { "MonsterId" } },

                // M3 Slice 1A（Enemy Presentation）：绑定表的两列都是身份/关系的一部分，
                // 缺任一列都会让「怪物 → 视觉资源」的绑定静默消失。
                { "EnemyPresentation", new[] { "MonsterId", "VisualAssetId" } },

                // WP-Encounter-1A（遭遇生成静态配置基础）：只登记「丢了这一列，遭遇语义就静默改变」的字段。
                // 刻意不登记 DisplayName / AllowDuplicateMonster 之类可由默认语义吸收的列。
                { "Encounter", new[] { "EncounterId", "ThreatBudgetMin", "ThreatBudgetMax" } },
                { "EncounterSlot", new[]
                    {
                        "SlotId", "EncounterId", "SelectionMode", "FixedMonsterId",
                        "CountMin", "CountMax", "PositionMode", "Row", "Column"
                    } },
                { "EncounterSlotRule", new[] { "SlotId", "RuleMode", "Dimension", "Value" } },
                { "EncounterPool", new[] { "PoolId" } },
                { "EncounterPoolEntry", new[] { "PoolId", "EncounterId", "Weight", "MinDepth", "MaxDepth" } },
                { "MonsterEncounterProfile", new[] { "MonsterId", "ThreatCost", "Tier", "RandomWeight" } },
                { "MonsterRole", new[] { "MonsterId", "Role" } },

                // M3 共享视觉资源表（WP-Event-1A 起 Authority 独立到 RRPG_VisualAssets.xlsx）：
                // 三列都是「丢了就会静默改变语义」的关系（身份 / 源文件 / 导入预设）。
                { "VisualAsset", new[] { "VisualAssetId", "SourceFile", "ImportPreset" } },

                // WP-Event-1A（统一事件内容基础）：登记「丢了这一列，事件语义就静默改变 / 约束消失」的字段。
                // 刻意不登记 DisplayName / RoleName / 文案模板之类可由必填规则覆盖的展示列。
                { "Event", new[] { "EventId", "Enabled", "Weight", "RepeatPolicy", "CooldownType", "CooldownValue" } },
                { "EventTrigger", new[] { "TriggerId", "EventId", "TriggerType" } },
                { "EventRole", new[] { "RoleId", "EventId", "SelectorType" } },
                { "EventRoleCondition", new[] { "ConditionId", "RoleId", "ConditionType" } },
                { "EventCondition", new[] { "ConditionId", "EventId", "GroupId", "ConditionType" } },
                { "EventPresentation", new[] { "EventId", "PresentationChannel", "TitleTemplate", "BodyTemplate", "Urgency" } },
                { "EventOption", new[] { "OptionId", "EventId", "SortOrder", "LabelTemplate", "UnavailableMode" } },
                { "EventOptionCondition", new[] { "ConditionId", "OptionId", "GroupId", "ConditionType" } },
                { "EventOutcome", new[] { "OutcomeId", "OptionId", "Weight" } },
                { "EventEffect", new[] { "EffectId", "OutcomeId", "Order", "EffectType" } },
            };

        public static bool TryGetRequired(string entityName, out string[] requiredFields)
        {
            return Required.TryGetValue(entityName ?? string.Empty, out requiredFields);
        }
    }
}

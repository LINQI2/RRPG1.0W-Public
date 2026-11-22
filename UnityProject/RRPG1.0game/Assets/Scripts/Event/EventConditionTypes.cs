using System;

namespace RRPG.Event
{
    /// <summary>
    /// ConditionType 词表（WP-Event-1A V0.1 冻结集合；`EventCondition` 与 `EventOptionCondition` 共用）。
    ///
    /// 边界：本类只提供常量与分类判断；**取值域本身仍由 Compiler 强制**。
    /// Resolver 遇到未知类型时返回明确 Runtime 诊断（`InvalidRuntimeConfig`），
    /// 绝不默认 true / false 后继续。
    /// </summary>
    public static class EventConditionTypes
    {
        // Context
        public const string ContextStringEquals = "ContextStringEquals";
        public const string ContextIntAtLeast = "ContextIntAtLeast";
        public const string ContextIntAtMost = "ContextIntAtMost";

        // Event World / History
        public const string FlagSet = "FlagSet";
        public const string FlagNotSet = "FlagNotSet";
        public const string EventNotSeen = "EventNotSeen";
        public const string EventSeenCountAtLeast = "EventSeenCountAtLeast";

        // Person
        public const string PersonExists = "PersonExists";
        public const string PersonInVillage = "PersonInVillage";
        public const string PersonOnExpedition = "PersonOnExpedition";
        public const string PersonAvailable = "PersonAvailable";

        // Village
        public const string VillageResourceAtLeast = "VillageResourceAtLeast";
        public const string VillageResourceAtMost = "VillageResourceAtMost";

        // Expedition
        public const string ExpeditionDepthAtLeast = "ExpeditionDepthAtLeast";
        public const string ExpeditionDepthAtMost = "ExpeditionDepthAtMost";
        public const string SupplyAtLeast = "SupplyAtLeast";
        public const string SupplyAtMost = "SupplyAtMost";
        public const string PartySizeAtLeast = "PartySizeAtLeast";
        public const string PartySizeAtMost = "PartySizeAtMost";

        public static readonly string[] All =
        {
            ContextStringEquals, ContextIntAtLeast, ContextIntAtMost,
            FlagSet, FlagNotSet, EventNotSeen, EventSeenCountAtLeast,
            PersonExists, PersonInVillage, PersonOnExpedition, PersonAvailable,
            VillageResourceAtLeast, VillageResourceAtMost,
            ExpeditionDepthAtLeast, ExpeditionDepthAtMost,
            SupplyAtLeast, SupplyAtMost, PartySizeAtLeast, PartySizeAtMost
        };

        /// <summary>角色筛选条件（EventRoleCondition）允许的 3 个类型。</summary>
        public static readonly string[] RoleConditionOnly =
        {
            PersonInVillage, PersonOnExpedition, PersonAvailable
        };

        public static bool IsKnown(string conditionType)
        {
            if (string.IsNullOrEmpty(conditionType)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], conditionType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static bool IsRoleConditionType(string conditionType)
        {
            if (string.IsNullOrEmpty(conditionType)) return false;
            for (int i = 0; i < RoleConditionOnly.Length; i++)
            {
                if (string.Equals(RoleConditionOnly[i], conditionType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static bool IsPersonConditionType(string conditionType)
        {
            return string.Equals(conditionType, PersonExists, StringComparison.Ordinal)
                || string.Equals(conditionType, PersonInVillage, StringComparison.Ordinal)
                || string.Equals(conditionType, PersonOnExpedition, StringComparison.Ordinal)
                || string.Equals(conditionType, PersonAvailable, StringComparison.Ordinal);
        }

        public static string KnownListText()
        {
            return string.Join(" / ", All);
        }
    }
}

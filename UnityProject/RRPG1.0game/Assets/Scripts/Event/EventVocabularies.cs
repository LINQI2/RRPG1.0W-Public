using System;

namespace RRPG.Event
{
    /// <summary>
    /// Event 求解的资源键（V0.1 只允许这两个；Compiler 已阻止恢复旧 Wood / Stone / Gold Authority）。
    /// </summary>
    public static class EventResourceKeys
    {
        public const string Ingredients = "Ingredients";
        public const string Food = "Food";
    }

    /// <summary>SelectorType 词表（与 `EventRole.SelectorType` 的 allowed 集合一致）。</summary>
    public static class EventSelectorTypes
    {
        public const string ContextPerson = "ContextPerson";
        public const string VillagePerson = "VillagePerson";
        public const string ExpeditionPartyMember = "ExpeditionPartyMember";
        public const string FixedPerson = "FixedPerson";

        public static readonly string[] All =
        {
            ContextPerson, VillagePerson, ExpeditionPartyMember, FixedPerson
        };

        public static bool IsKnown(string selectorType)
        {
            if (string.IsNullOrEmpty(selectorType)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], selectorType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static string KnownListText()
        {
            return string.Join(" / ", All);
        }
    }

    /// <summary>RepeatPolicy 词表（与 `Event.RepeatPolicy` 的 allowed 集合一致）。</summary>
    public static class EventRepeatPolicies
    {
        public const string Repeatable = "Repeatable";
        public const string OncePerExpedition = "OncePerExpedition";
        public const string OncePerWorld = "OncePerWorld";

        public static readonly string[] All = { Repeatable, OncePerExpedition, OncePerWorld };

        public static bool IsKnown(string policy)
        {
            if (string.IsNullOrEmpty(policy)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], policy, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static string KnownListText()
        {
            return string.Join(" / ", All);
        }
    }

    /// <summary>CooldownType 词表（与 `Event.CooldownType` 的 allowed 集合一致）。</summary>
    public static class EventCooldownTypes
    {
        public const string None = "None";
        public const string VillageDays = "VillageDays";
        public const string ExpeditionNodes = "ExpeditionNodes";

        public static readonly string[] All = { None, VillageDays, ExpeditionNodes };

        public static bool IsKnown(string cooldownType)
        {
            if (string.IsNullOrEmpty(cooldownType)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], cooldownType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static string KnownListText()
        {
            return string.Join(" / ", All);
        }
    }
}

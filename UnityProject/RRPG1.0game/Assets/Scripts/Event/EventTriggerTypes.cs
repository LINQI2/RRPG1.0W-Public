using System;

namespace RRPG.Event
{
    /// <summary>
    /// 触发时机词表（WP-Event-1A Authoring Authority 的运行时镜像）。
    ///
    /// 边界：
    ///   - 这里的字符串**必须**与 `RRPG_Events.xlsx / EventTrigger.TriggerType` 的 allowed 集合一致
    ///     （Compiler 是取值域 Authority；本类只提供运行时比较用的常量与 Domain 推导）；
    ///   - `ManualDebug` 不是正式触发时机：它不参与 Village / Expedition Domain 推导，也不应被产品触发；
    ///   - 未知字符串（配置被人工改坏 / 编译器守卫被绕过）→ Resolver 返回 `UnknownTrigger`，
    ///     绝不静默当作某个已知触发时机。
    /// </summary>
    public static class EventTriggerTypes
    {
        // Village
        public const string VillageDaySettled = "VillageDaySettled";
        public const string VillageRecognitionRaised = "VillageRecognitionRaised";
        public const string VillageProjectCompleted = "VillageProjectCompleted";

        // Expedition
        public const string ExpeditionEventNodeReached = "ExpeditionEventNodeReached";
        public const string ExpeditionBattleResolved = "ExpeditionBattleResolved";
        public const string ExpeditionReturned = "ExpeditionReturned";

        // 开发测试（不是正式触发时机）
        public const string ManualDebug = "ManualDebug";

        /// <summary>全部已知触发时机（诊断文案顺序 = 编译期词表顺序）。</summary>
        public static readonly string[] All =
        {
            VillageDaySettled, VillageRecognitionRaised, VillageProjectCompleted,
            ExpeditionEventNodeReached, ExpeditionBattleResolved, ExpeditionReturned,
            ManualDebug
        };

        public static readonly string[] Village =
        {
            VillageDaySettled, VillageRecognitionRaised, VillageProjectCompleted
        };

        public static readonly string[] Expedition =
        {
            ExpeditionEventNodeReached, ExpeditionBattleResolved, ExpeditionReturned
        };

        public static bool IsKnown(string triggerType)
        {
            if (string.IsNullOrEmpty(triggerType)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], triggerType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>推导 Domain（Village / Expedition）；`ManualDebug` 与未知值返回 null（不推导）。</summary>
        public static string ResolveDomain(string triggerType)
        {
            for (int i = 0; i < Village.Length; i++)
            {
                if (string.Equals(Village[i], triggerType, StringComparison.Ordinal)) return "Village";
            }
            for (int i = 0; i < Expedition.Length; i++)
            {
                if (string.Equals(Expedition[i], triggerType, StringComparison.Ordinal)) return "Expedition";
            }
            return null;
        }

        /// <summary>诊断用：全部已知触发时机的展示文本。</summary>
        public static string KnownListText()
        {
            return string.Join(" / ", All);
        }
    }
}

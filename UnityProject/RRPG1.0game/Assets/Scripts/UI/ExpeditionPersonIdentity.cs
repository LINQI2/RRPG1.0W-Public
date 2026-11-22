using System.Collections.Generic;

/// <summary>
/// ExpeditionPersonDetailRow —— 人物详情行（**只读展示用**）。
/// 由 ExpeditionUI 按准备界面候选集构造，不携带任何 Authority。
/// PersonId 为空 = 该候选缺少显式 Card→Person 绑定（不得用模板 ID 猜人物身份）。
/// </summary>
public class ExpeditionPersonDetailRow
{
    public string PersonId;
    public string DisplayName;
    public bool IsHero;
    public bool IsSelected;          // 只读展示：当前是否已选入出征队伍（查看详情不得改变选择）
    public string UnavailableReason; // 既有不可出征原因（可为空）
}


/// <summary>
/// ExpeditionPersonIdentity —— **V4 Acceptance Prep** 的最小只读「远征选人身份信息」组合器。
///
/// 目的：让玩家在 Expedition Party Selection 中，选择具体 Person 时能读到少量**现有长期人物事实**
/// （五问 #4：是否有理由考虑「他是谁」），而不是只看战斗数值。
///
/// 只读复用现有 Authority（**不新增状态 / Save / Authority / 规则**）：
///   - 当前 Village 职责：复用 `VillageCommitmentView.BuildPersonRows` 的既有权威合成
///     （Workplace / Project / 空闲 / 远征 / 不可用），本类不复制任何 Occupancy / Assignment 规则；
///   - 长期健康：复用既有 `Injury` 字段（Character / Hero 均有），只做可读文案映射；
///   - 远征经历：复用 Character 的 `PersonLifeHistory`（`ExpeditionReturned` 计数 + 最近一次 Outcome 派生）；
///     Hero 没有 LifeHistory → **不显示该行**（不为此扩 Hero LifeHistory）。
///
/// 全部文案均为 Test / Not Frozen；布局不冻结。
/// </summary>
public static class ExpeditionPersonIdentity
{
    /// <summary>
    /// 组合卡片附加文本（多行；无任何可用信息时返回空串，调用方保持原卡片文本）。
    /// </summary>
    public static string BuildCardSuffix(PlayerCollection collection, string personId)
    {
        if (collection == null || string.IsNullOrEmpty(personId)) return string.Empty;

        var lines = new List<string>();

        string duty = ResolveDutyText(collection, personId);
        if (!string.IsNullOrEmpty(duty)) lines.Add("职责：" + duty);

        string injury = ResolveInjuryText(collection, personId);
        if (!string.IsNullOrEmpty(injury)) lines.Add("长期状态：" + injury);

        string expedition = ResolveExpeditionText(collection, personId);
        if (!string.IsNullOrEmpty(expedition)) lines.Add(expedition);

        return lines.Count == 0 ? string.Empty : "\n" + string.Join("\n", lines);
    }

    /// <summary>
    /// 组合「可查看的远征候选人」详情文本（全部候选；每行：人物名 / 职责 / 长期状态 / 远征经历）。
    /// Hero：只显示 人物名 / 职责 / 长期状态；**不显示虚构的 Expedition Experience**（Hero 无 LifeHistory）。
    /// 纯只读展示，不改变任何选择 / 状态。
    /// </summary>
    public static string BuildDetailText(PlayerCollection collection, IList<ExpeditionPersonDetailRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== 可查看的远征候选人（只读 / Test / Not Frozen）===");

        if (rows == null || rows.Count == 0)
        {
            sb.AppendLine("（当前准备界面没有可显示的候选人）");
            return sb.ToString().TrimEnd('\n');
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            string name = string.IsNullOrEmpty(row.DisplayName) ? "(未知候选)" : row.DisplayName;
            sb.Append("· ").Append(name);
            if (row.IsHero) sb.Append("（英雄）");
            if (row.IsSelected) sb.Append("〔已选入出征队伍〕");
            sb.AppendLine();

            if (string.IsNullOrEmpty(row.PersonId))
            {
                sb.AppendLine("  （无法读取长期人物信息：该角色卡缺少显式 Card→Person 绑定）");
            }
            else
            {
                string duty = ResolveDutyText(collection, row.PersonId);
                sb.AppendLine("  职责：" + (string.IsNullOrEmpty(duty) ? "—" : duty));

                string injury = ResolveInjuryText(collection, row.PersonId);
                sb.AppendLine("  长期状态：" + (string.IsNullOrEmpty(injury) ? "—" : injury));

                // Hero 不伪造 Expedition Experience（无 LifeHistory → 不显示该行）
                if (!row.IsHero)
                {
                    string expedition = ResolveExpeditionText(collection, row.PersonId);
                    if (!string.IsNullOrEmpty(expedition)) sb.AppendLine("  " + expedition);
                }
            }

            if (!string.IsNullOrEmpty(row.UnavailableReason))
                sb.AppendLine("  〔不可出征：" + row.UnavailableReason + "〕");

            if (i < rows.Count - 1) sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// 当前 Village 职责（只读；复用 VillageCommitmentView 既有合成，不自行判定占用 / 容量 / 进度）。
    /// 找不到该 Person → 空串（不猜测）。
    /// </summary>
    public static string ResolveDutyText(PlayerCollection collection, string personId)
    {
        if (collection == null || string.IsNullOrEmpty(personId)) return string.Empty;

        var views = VillageRuntimeBridge.GetPersonAvailability(collection);
        var time = VillageRuntimeBridge.Time;
        var rows = VillageCommitmentView.BuildPersonRows(
            views,
            VillageRuntimeBridge.Assignments,
            VillageRuntimeBridge.Projects,
            VillageRuntimeBridge.SocialResponse,
            time != null ? time.Year : 0,
            time != null ? time.Month : 0,
            time != null ? time.Day : 0);

        if (rows == null) return string.Empty;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || row.PersonId != personId) continue;
            switch (row.Status)
            {
                case VillageCommitmentView.PersonCommitmentStatus.Working:
                    return string.IsNullOrEmpty(row.WorkplaceDisplayName) ? "在岗" : row.WorkplaceDisplayName;
                case VillageCommitmentView.PersonCommitmentStatus.InProject:
                    return string.IsNullOrEmpty(row.ActiveProjectDisplayName)
                        ? "投入工程" : "投入工程：" + row.ActiveProjectDisplayName;
                case VillageCommitmentView.PersonCommitmentStatus.Expedition:
                    return "远征中";
                case VillageCommitmentView.PersonCommitmentStatus.Unavailable:
                    return "暂不可用";
                default:
                    return "空闲";
            }
        }
        return string.Empty;
    }

    /// <summary>长期健康状态可读文案（复用既有 Injury 字段；Character 优先，其次 Hero）。</summary>
    public static string ResolveInjuryText(PlayerCollection collection, string personId)
    {
        if (collection == null || string.IsNullOrEmpty(personId)) return string.Empty;

        var character = collection.GetCharacterByPersonId(personId);
        if (character != null) return InjuryDisplayName(character.Injury);

        var hero = collection.GetHeroByPersonId(personId);
        if (hero != null) return InjuryDisplayName(hero.Injury);

        return string.Empty;
    }

    /// <summary>
    /// 远征经历最小摘要（仅 Character；Hero 无 LifeHistory → 返回空串，不新增 Hero LifeHistory）。
    /// 「最近一次」按事件日期取最大（同日取最后写入的一条），只读派生、不写回。
    /// </summary>
    public static string ResolveExpeditionText(PlayerCollection collection, string personId)
    {
        if (collection == null || string.IsNullOrEmpty(personId)) return string.Empty;

        var character = collection.GetCharacterByPersonId(personId);
        if (character == null || character.LifeHistory == null) return string.Empty;

        int count = character.LifeHistory.CountExpeditionReturned();
        if (count <= 0) return "远征经历：无";

        int lastOutcome = -1;
        long lastKey = long.MinValue;
        var events = character.LifeHistory.Events;
        if (events != null)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null || e.Type != PersonLifeEventType.ExpeditionReturned) continue;
                long key = e.Year * 10000L + e.Month * 100L + e.Day;
                if (key >= lastKey)
                {
                    lastKey = key;
                    lastOutcome = e.Outcome;
                }
            }
        }

        return "远征经历：" + count + " 次（最近：" + ReturnOutcomeDisplayName(lastOutcome) + "）";
    }

    /// <summary>Injury 可读文案（Test / Not Frozen；不改变任何 Injury 语义）。</summary>
    public static string InjuryDisplayName(InjuryStatus injury)
    {
        switch (injury)
        {
            case InjuryStatus.LightInjury: return "轻伤";
            case InjuryStatus.HeavyInjury: return "重伤";
            case InjuryStatus.Disabled: return "残疾";
            default: return "正常";
        }
    }

    /// <summary>Return Outcome 可读文案（Outcome = RunEndMode 枚举序，Test / Not Frozen）。</summary>
    public static string ReturnOutcomeDisplayName(int outcome)
    {
        switch (outcome)
        {
            case (int)RunEndMode.SafeReturn: return "安全返程";
            case (int)RunEndMode.Retreat: return "主动撤退";
            case (int)RunEndMode.Defeat: return "失败撤出";
            default: return "未知";
        }
    }
}

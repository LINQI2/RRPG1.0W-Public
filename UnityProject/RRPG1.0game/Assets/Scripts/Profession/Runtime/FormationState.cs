using System.Collections.Generic;

/// <summary>
/// M6-A Formation Runtime Foundation — 具体 Person 的 Profession Formation 算法派生状态
/// （Person-owned；唯一宿主 = 普通 CharacterInstance。Hero 不拥有 FormationState）。
///
/// 职责边界（GameSpec Person-Profession-Growth §12.7 / §12.16）：
///   - FormationState 只保存 Formation 算法需要的派生状态：
///     Strength / 相关活动中断信息（InactiveDays）/ 累计有效相关活动日（ActiveCareerDays）/
///     Stability / Candidate / 最近 Day End 的 Formation Target 与 Ambiguity 判定。
///   - 它不复制 Life History，也不每天重放全部人生历史重新计算。
///   - FormationState 与 PersonLifeHistory 严格分离：
///       PersonLifeHistory = 人生事实 Source of Record（只追加事实，如 ProfessionFormed）；
///       FormationState    = 算法派生状态（可持续演化、可被算法改写）。
///
/// 数值尺度：Strength 使用统一语义尺度（0..StrengthMax 参考尺度）；
/// 所有阈值 / 速率 / 曲线参数均不在本类冻结，见 FormationTestConfig（Test / Not Frozen）。
/// 本类不直接由 JsonUtility 序列化；存档镜像见 SaveData（FormationStateSaveData）。
/// </summary>
public class FormationState
{
    /// <summary>
    /// 该 Person 已拥有 Formation 状态的全部 Profession 条目（Person×Profession 独立）。
    /// 一个 Person 各 Profession 完全独立演化；同 Profession 不同 Person 也完全独立。
    /// </summary>
    public List<ProfessionFormationEntry> Entries = new List<ProfessionFormationEntry>();

    /// <summary>
    /// 是否已进入 Formation Window（§12.9：第一个 Candidate 出现后进入）。
    /// Window 打开后保持；Window 内 Candidate 可进出、Leader 可改变、Stability 可建立或回落。
    /// </summary>
    public bool FormationWindowOpen;

    /// <summary>
    /// 最近一次 Day End 解析出的明确 Formation Target（ProfessionId）；无 Target / Ambiguous 时为 null。
    /// 注意：本字段与 IsAmbiguous 是算法最近一次 Day End 的结果（派生状态，随 Save/Load 连续）。
    /// </summary>
    public string FormationTargetProfessionId;

    /// <summary>最近一次 Day End 是否处于 Ambiguous（多个 Candidate 且无明显 Leader，§12.11）。</summary>
    public bool IsAmbiguous;

    public bool IsEmpty => Entries == null || Entries.Count == 0;

    public ProfessionFormationEntry FindEntry(string professionId)
    {
        if (string.IsNullOrEmpty(professionId) || Entries == null) return null;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Entries[i].ProfessionId == professionId) return Entries[i];
        }
        return null;
    }

    public ProfessionFormationEntry GetOrAddEntry(string professionId)
    {
        var existing = FindEntry(professionId);
        if (existing != null) return existing;
        var created = new ProfessionFormationEntry { ProfessionId = professionId };
        if (Entries == null) Entries = new List<ProfessionFormationEntry>();
        Entries.Add(created);
        return created;
    }
}

/// <summary>
/// 单个 Profession 的 Person-owned Formation 派生状态。
///
/// Strength：该 Person 在该 Profession 的 Formation Strength（统一语义尺度；数值未冻结）。
/// ActiveCareerDays：累计「有效相关活动日」（真实生活/工作产生 Evidence 的天数）；
///                    用作慢 → 快 → 慢成长曲线的阶段输入。
/// InactiveDays：自该 Profession 上一次有效相关活动之后的中断天数；用于慢 → 快 → 慢衰减。
///               衰减只依据本 Profession 的中断时长，不因正在做另一个 Profession 而额外加速。
/// Stability：身份结论已稳定存在多久（§12.12）；不参与 Strength 计算。
/// IsCandidate：最近一次 Day End 的 Candidate 判定（带轻度滞后，§12.8）。
/// </summary>
public class ProfessionFormationEntry
{
    public string ProfessionId;

    /// <summary>Formation Strength（统一语义尺度，参考 0..FormationTestConfig.StrengthMax；Test / Not Frozen）。</summary>
    public float Strength;

    /// <summary>累计有效相关活动日（该 Profession 收到真实 Evidence 的天数累计）。</summary>
    public int ActiveCareerDays;

    /// <summary>自该 Profession 最近一次有效相关活动以来的中断天数（每天无活动 +1）。</summary>
    public int InactiveDays;

    /// <summary>Formation Stability（持续一致 Target 时增长；Ambiguous / 转向时回落；Test / Not Frozen）。</summary>
    public float Stability;

    /// <summary>最近一次 Day End 判定的 Candidate 状态（可逆、可同时存在多个、轻度滞后）。</summary>
    public bool IsCandidate;
}

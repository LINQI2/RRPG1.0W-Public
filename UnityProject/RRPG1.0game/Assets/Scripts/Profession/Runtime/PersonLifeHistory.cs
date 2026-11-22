using System.Collections.Generic;

/// <summary>
/// M6 最小 Person-owned Life History Foundation（GameSpec §12.7）。
///
/// Life History = 人生事实 Source of Record：记录这个 Person 一生实际发生过什么。
/// 本轮只可靠持久记录 ProfessionFormed(Date, ProfessionId)（§12.15「写入 Life History」）。
/// 不建设：完整 Daily Experience / Diary / Timeline UI / Narrative System /
///         Family History / 通用 Event Graph / 每日重放重算。
///
/// 职责分离红线：
///   - PersonLifeHistory 只追加不可变人生事实（Formation 算法不得改写已发生事实）；
///   - FormationState 只保存算法派生状态；Life History 不复制 FormationState，
///     FormationState 也不复制 Life History。
///
/// 存档镜像见 SaveData（PersonLifeHistorySaveData）。旧档缺失 → 空 History（合法）。
/// </summary>
public class PersonLifeHistory
{
    public List<PersonLifeEvent> Events = new List<PersonLifeEvent>();

    /// <summary>该 Person 是否已记录「正式形成 ProfessionId」事件（exactly-once 判重用）。</summary>
    public bool HasProfessionFormed(string professionId)
    {
        return CountProfessionFormed(professionId) > 0;
    }

    /// <summary>该 Person 已记录「正式形成 ProfessionId」的次数（正常应为 0 或 1；防御性计数）。</summary>
    public int CountProfessionFormed(string professionId)
    {
        if (string.IsNullOrEmpty(professionId) || Events == null) return 0;
        int count = 0;
        for (int i = 0; i < Events.Count; i++)
        {
            var e = Events[i];
            if (e != null && e.Type == PersonLifeEventType.ProfessionFormed && e.ProfessionId == professionId)
                count++;
        }
        return count;
    }

    /// <summary>追加一条 ProfessionFormed 人生事实（调用方保证语义上的 exactly-once）。</summary>
    public void AppendProfessionFormed(int year, int month, int day, string professionId)
    {
        if (Events == null) Events = new List<PersonLifeEvent>();
        Events.Add(new PersonLifeEvent
        {
            Type = PersonLifeEventType.ProfessionFormed,
            Year = year,
            Month = month,
            Day = day,
            ProfessionId = professionId
        });
    }

    /// <summary>
    /// V4 Gate 2：追加一条「参与远征并以某 Return Outcome 返回」的最小人生事实
    /// （Type = ExpeditionReturned；Outcome = RunEndMode 枚举序：SafeReturn=0 / Retreat=1 / Defeat=2）。
    /// 调用方（VillageReturnPersonConsequence）以 ExpeditionRecord 会话标记保证 exactly-once；
    /// 本方法只负责记录，不做判重、不写解释与评价。
    /// </summary>
    public void AppendExpeditionReturned(int year, int month, int day, int outcome)
    {
        if (Events == null) Events = new List<PersonLifeEvent>();
        Events.Add(new PersonLifeEvent
        {
            Type = PersonLifeEventType.ExpeditionReturned,
            Year = year,
            Month = month,
            Day = day,
            Outcome = outcome
        });
    }

    /// <summary>已记录的 ExpeditionReturned 条数（诊断 / 测试用；正常应为 0 或 1 per 远征）。</summary>
    public int CountExpeditionReturned()
    {
        if (Events == null) return 0;
        int count = 0;
        for (int i = 0; i < Events.Count; i++)
        {
            var e = Events[i];
            if (e != null && e.Type == PersonLifeEventType.ExpeditionReturned) count++;
        }
        return count;
    }
}

/// <summary>最小人生事件类型（M6 落 ProfessionFormed；V4 Gate 2 追加 ExpeditionReturned，旧枚举序不回改）。</summary>
public enum PersonLifeEventType
{
    /// <summary>正式形成（首次获得）某 Profession（0）。</summary>
    ProfessionFormed = 0,

    /// <summary>
    /// V4 Gate 2：参与过一次 Expedition 并以某 Return Outcome 返回（1）。
    /// 最小事实：Date = 回到村庄当日；Outcome = RunEndMode 枚举序。
    /// </summary>
    ExpeditionReturned = 1
}

/// <summary>单条人生事实（追加式；只读语义由调用方遵守）。</summary>
public class PersonLifeEvent
{
    public PersonLifeEventType Type;
    public int Year;
    public int Month;
    public int Day;

    /// <summary>ProfessionFormed 事件的 ProfessionId（其它类型不适用时为 null）。</summary>
    public string ProfessionId;

    /// <summary>
    /// V4 Gate 2：ExpeditionReturned 事件的 Return Outcome（RunEndMode 枚举序，Test / Not Frozen）；
    /// 其它类型不使用。旧档缺失 → 0（NotSet / SafeReturn 序值，不回改既有事件语义）。
    /// </summary>
    public int Outcome;
}

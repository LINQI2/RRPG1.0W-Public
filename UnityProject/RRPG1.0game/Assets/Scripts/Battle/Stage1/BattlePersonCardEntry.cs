using System;

/// <summary>
/// Battle V0.1 运行时"人物牌条目"（Run/Battle 层，纯逻辑结构）
///
/// 职责：把"静态牌模板"与"具体人物归属 + 本Run份数"解耦后，在运行时拼装为一条
/// 可用于构筑本场牌库的条目。
///
/// 设计依据（Battle GameSpec V0.1）：
///   - 角色牌绑定"具体的人"：同一角色牌模板可被多个具体人物各自拥有，
///     归属用 PersonId（Person/Character Instance ID，未来唯一实例ID）表达。
///   - Run 牌组份数不属于静态模板：同一模板在不同 Run、不同人物可拥有不同数量，
///     由本条目按 Run 状态构造，不得靠修改全局 SO 表示 Run 牌组状态。
///
/// 通用牌条目：PersonId 留空（null/空字符串），Copies 表示本 Run 该通用牌份数。
/// </summary>
[Serializable]
public class BattlePersonCardEntry
{
    /// <summary>静态牌模板</summary>
    public BattleCardData Template;

    /// <summary>
    /// 具体人物唯一实例ID（未来 Person/Character Instance ID）。
    /// 角色牌必填；通用牌留空。
    /// </summary>
    public string PersonId;

    /// <summary>本 Run 该人物拥有该模板的份数（默认1，不写入静态 SO）</summary>
    public int Copies = 1;

    public BattlePersonCardEntry()
    {
    }

    public BattlePersonCardEntry(BattleCardData template, string personId, int copies = 1)
    {
        Template = template;
        PersonId = personId;
        Copies = copies;
    }
}

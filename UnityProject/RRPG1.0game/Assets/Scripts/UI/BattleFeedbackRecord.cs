/// <summary>表现消费的标量结果。请求值永不作为实际伤害/治疗。</summary>
public enum BattleFeedbackType { CardCommit, ActionPoints, ActorAction, Damage, Heal, Shield, Move, Swap, EnemyAction, TurnEnd, Formation }

public sealed class BattleFeedbackRecord
{
    public BattleFeedbackType Type;
    public string SourceUnitId, TargetUnitId, CardInstanceId, CardTemplateId;
    public int RequestedValue, HPBefore, HPAfter, MaxHP, ShieldBefore, ShieldAfter;
    public int APBefore, APAfter, APMax;
    public int RowBefore, ColumnBefore, RowAfter, ColumnAfter;
    public bool BecameDown, Applied;
    public EnemyActionType IntentType;
    public int HPDelta => HPAfter - HPBefore;
    public int ShieldDelta => ShieldAfter - ShieldBefore;

    public static BattleFeedbackRecord From(BattleFeedbackType type, string source,
        BattleRuleFactDelta change, int requested = 0, BattleCard card = null)
    {
        var b = change.Before; var a = change.After;
        return new BattleFeedbackRecord { Type = type, SourceUnitId = source, TargetUnitId = a.UnitId,
            CardInstanceId = card != null ? card.CardId : null, CardTemplateId = card != null ? card.TemplateId : null,
            RequestedValue = requested, HPBefore = b.HP, HPAfter = a.HP, MaxHP = a.MaxHP,
            ShieldBefore = b.Shield, ShieldAfter = a.Shield, RowBefore = b.Row, RowAfter = a.Row,
            ColumnBefore = b.Column, ColumnAfter = a.Column, BecameDown = !b.IsDown && a.IsDown, Applied = true };
    }
}

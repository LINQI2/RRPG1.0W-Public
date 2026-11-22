using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle HUD Presenter（Battle Presentation Cutover / Layout V0.1 阶段A）
///
/// 职责：只把**已经存在的 Battle 事实**显示到可替换的 TMP_Text 宿主上：
///   - 回合数 ← <see cref="BattleV1Runtime.TurnNumber"/>；
///   - 抽牌堆 / 弃牌堆数量 ← <see cref="SharedDeck"/> 的公开只读计数。
///
/// 硬约束（本组件不构成第二套 HUD Authority）：
///   - 只**读** BattleV1Runtime 的公开只读属性；不创建牌、不抽牌、不写任何 Gameplay 状态；
///   - 不订阅、不修改 SharedDeck / SharedActionPointPool / Hand / Formation / BattleSystem；
///   - 共享行动力文本宿主仍由 `BattleV1Runtime.txtSharedActionPoint` 承担（本组件不重复写）；
///   - 宿主引用、位置、字号、颜色、标签文字全部由场景 / Inspector 决定，不在代码里冻结任何布局数值。
///
/// 为什么不订阅事件：正式路径每场战斗都会重建 SharedDeck / SharedActionPointPool，
/// 订阅会引入过期引用风险；本组件只做**只读轮询 + 变化检测**，值不变时不重写字符串（无每帧 GC 压力）。
/// </summary>
public class BattleHudPresenter : MonoBehaviour
{
    [Header("数据来源（只读；不构成第二套 Authority）")]
    [Tooltip("正式战斗运行时（共享牌库 / 共享行动力 / 回合数）。未绑定时自动取本 GameObject 上的组件")]
    public BattleV1Runtime v1Runtime;

    [Header("HUD TMP_Text 宿主（全部可替换；未绑定的那一行不刷新）")]
    [Tooltip("回合数宿主")]
    public TMP_Text turnHost;

    [Tooltip("抽牌堆数量宿主")]
    public TMP_Text drawPileHost;

    [Tooltip("弃牌堆数量宿主")]
    public TMP_Text discardPileHost;

    [Header("标签文字（Inspector 可改；代码不冻结文案）")]
    public string turnLabel = "";
    public string turnFormat = "{0}";
    public string drawPileLabel = "";
    public string drawPileFormat = "{0}";
    public string discardPileLabel = "";
    public string discardPileFormat = "{0}";

    private int _lastTurn = int.MinValue;
    private int _lastDraw = int.MinValue;
    private int _lastDiscard = int.MinValue;

    private void Awake()
    {
        ResolveRuntime();
    }

    private void OnEnable()
    {
        ResolveRuntime();
        Invalidate();
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    /// <summary>失效缓存，使下一次 Refresh 一定重写宿主文本（重新启用 / 换场后使用）。</summary>
    public void Invalidate()
    {
        _lastTurn = int.MinValue;
        _lastDraw = int.MinValue;
        _lastDiscard = int.MinValue;
    }

    private void ResolveRuntime()
    {
        if (v1Runtime == null) v1Runtime = GetComponent<BattleV1Runtime>();
    }

    /// <summary>把当前 Battle 事实刷到宿主（只读；值未变化时不动任何 UI）。</summary>
    public void Refresh()
    {
        if (v1Runtime == null) return;
        if (v1Runtime.IsPresentationDeferred) return;

        int turn = v1Runtime.TurnNumber;
        if (turn != _lastTurn)
        {
            _lastTurn = turn;
            SetText(turnHost, turnLabel + BattleHudFormatting.Format(turnFormat, turn));
        }

        var deck = v1Runtime.Deck;
        int draw = deck != null ? deck.DrawPileCount : 0;
        if (draw != _lastDraw)
        {
            _lastDraw = draw;
            SetText(drawPileHost, drawPileLabel + BattleHudFormatting.Format(drawPileFormat, draw));
        }

        int discard = deck != null ? deck.DiscardPileCount : 0;
        if (discard != _lastDiscard)
        {
            _lastDiscard = discard;
            SetText(discardPileHost, discardPileLabel + BattleHudFormatting.Format(discardPileFormat, discard));
        }
    }

    private static void SetText(TMP_Text host, string value)
    {
        if (host == null) return;
        if (host.text != value) host.text = value;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle V0.1 Gate 0：《补位》等需要选择“我方合法空格”时使用的临时格子标记表现。
///
/// 负责：
///   - 在合法空格上显示低成本可点击标记（临时状态文字 / 高亮）；
///   - 点击时把所选格子回报给 BattleUI 的目标选择宿主。
///
/// 不负责：
///   - 合法性判断（由 BattleCardRuleResolver 提供合法目标集合）；
///   - 空间写入（由 BattleField.TryMoveUnit 完成）。
///
/// 表现层级：Test / Not Frozen（Gate 0 只要求测试者可识别合法 Move 目标，不是正式美术）。
/// </summary>
public class BattleRuleCellMarker : MonoBehaviour
{
    [Tooltip("标记文本（临时低美术成本）")]
    public TMP_Text labelText;

    private System.Action _onClick;

    /// <summary>绑定点击回调（每次创建时调用一次）</summary>
    public void Bind(System.Action onClick)
    {
        _onClick = onClick;

        var button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();

        var graphic = GetComponent<Image>();
        if (graphic != null && button.targetGraphic == null) button.targetGraphic = graphic;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (_onClick != null) _onClick();
        });
    }

    /// <summary>设置显示文本</summary>
    public void SetLabel(string text)
    {
        if (labelText != null) labelText.text = text;
    }
}

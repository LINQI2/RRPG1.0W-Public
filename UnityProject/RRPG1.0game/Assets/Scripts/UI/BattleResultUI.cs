using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 战斗结算界面
///
/// 负责：显示战斗结果、角色伤亡、经验金币获取
/// 不负责：结算逻辑
///
/// 启动时自动隐藏，战斗结束后由 BattleUI 激活显示
/// </summary>
public class BattleResultUI : MonoBehaviour
{
    [Header("UI 元素")]
    public TMP_Text titleText;
    public TMP_Text resultText;
    public Button btnReturn;

    [Header("结算界面的 Canvas（启动时隐藏，BattleUI 激活）")]
    public GameObject canvasRoot;

    private Button _boundReturnButton;
    private Action _onReturn;

    void Start()
    {
        // 初始隐藏（只隐藏子 Canvas，保持自身 active 以便接收消息）
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 显示结算结果
    /// </summary>
    public void ShowResult(BattleSettlement settlement, Action onReturn)
    {
        // 激活 Canvas（优先 canvasRoot 字段，否则找子 Canvas）
        if (canvasRoot != null)
        {
            canvasRoot.SetActive(true);
        }
        else
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                canvas.gameObject.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text = settlement.IsVictory ? "🎉 战斗胜利" : "💀 战斗失败";
            titleText.color = settlement.IsVictory ? Color.green : Color.red;
        }

        if (resultText != null)
        {
            // Stage 6B（人工验收修复）：Gold / GrowthExp 属于本次远征暂存收益（安全返程才进入长期账户），
            // 不得以 Legacy BaseGold/BaseExp 的“已永久获得”语义展示（问题 50 数值未冻结）。
            // 故不显示具体金币/经验数值，仅展示角色情况与“暂存收益”说明。
            string info;

            if (settlement.IsVictory)
            {
                info = "=== 角色情况 ===\n";
                foreach (var cr in settlement.CharacterResults)
                {
                    string status;
                    if (cr.IsDead)
                        status = "💀 死亡";
                    else if (cr.Injury == InjuryStatus.LightInjury)
                        status = "轻伤";
                    else if (cr.Injury == InjuryStatus.HeavyInjury)
                        status = "重伤";
                    else if (cr.Injury == InjuryStatus.Disabled)
                        status = "残疾";
                    else if (cr.WasDeployed)
                        status = "存活";
                    else
                        status = "未上阵";

                    info += cr.DisplayName + "：" + status + "\n";
                }

                info += "\n战斗收益已计入本次远征暂存（安全返程后结算）";
            }
            else
            {
                info = "=== 角色情况 ===\n";
                foreach (var cr in settlement.CharacterResults)
                {
                    if (cr.IsDead)
                        info += cr.DisplayName + "：💀 死亡\n";
                    else if (cr.WasDeployed)
                        info += cr.DisplayName + "：存活\n";
                    else
                        info += cr.DisplayName + "：未上阵\n";
                }

                info += "\n本次远征暂存收益将按退出方式结算（撤退/全灭损失比例未冻结）";
            }

            resultText.text = info;
        }

        // Replace only this view's callback, including when its button is reassigned.
        if (_boundReturnButton != null)
            _boundReturnButton.onClick.RemoveListener(OnReturnClicked);
        _onReturn = onReturn;
        _boundReturnButton = btnReturn;
        if (_boundReturnButton != null)
            _boundReturnButton.onClick.AddListener(OnReturnClicked);
    }

    private void OnReturnClicked() => _onReturn?.Invoke();

    private void OnDestroy()
    {
        if (_boundReturnButton != null)
            _boundReturnButton.onClick.RemoveListener(OnReturnClicked);
        _onReturn = null;
    }

    /// <summary>
    /// 隐藏结算界面（供 BattleUI 回调使用）
    /// </summary>
    public void Hide()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }
}

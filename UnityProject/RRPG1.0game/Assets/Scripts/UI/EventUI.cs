using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 事件界面
///
/// 负责：显示随机事件内容、处理事件结果
/// 不负责：事件配置（后续通过 CSV/配置系统管理）
///
/// 事件类型（硬编码占位）：
///   1. 发现金币 — 获得随机金币
///   2. 发现道具 — 获得随机道具
///   3. 全员受伤 — 全体角色受到轻微伤害（伤残加重）
///   4. 意外收获 — 随机获得正面效果
///
/// 使用方式：
///   1. 场景中挂 EventUI 组件（默认隐藏）
///   2. 点击问号节点时调用 ShowEvent()
///   3. 玩家查看事件结果后点击关闭
/// </summary>
public class EventUI : MonoBehaviour
{
    [Header("UI 元素")]
    public GameObject canvasRoot;
    public TMP_Text eventTitleText;
    public TMP_Text eventDescText;
    public TMP_Text eventResultText;
    public TMP_Text infoText;
    public Button btnClose;

    /// <summary>玩家数据引用</summary>
    private PlayerCollection _playerData;

    /// <summary>关闭回调</summary>
    private System.Action _onClose;

    void Start()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        if (btnClose != null)
            btnClose.onClick.AddListener(OnClose);
    }

    /// <summary>
    /// 初始化并显示事件
    /// </summary>
    public void ShowEvent(PlayerCollection playerData, System.Action onClose)
    {
        _playerData = playerData;
        _onClose = onClose;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        // 随机触发事件
        TriggerRandomEvent();
    }

    /// <summary>
    /// 随机触发一个事件
    /// </summary>
    private void TriggerRandomEvent()
    {
        if (eventTitleText == null || eventDescText == null || eventResultText == null) return;

        float r = Random.value;

        if (r < 0.3f)
        {
            // 30% — 发现金币
            int gold = Random.Range(10, 31);
            _playerData.Gold += gold;
            eventTitleText.text = "发现金币";
            eventDescText.text = "你发现了一袋被遗弃的金币！";
            eventResultText.text = "获得 " + gold + " 金币";
            Log("发现 " + gold + " 金币");
        }
        else if (r < 0.55f)
        {
            // 25% — 发现道具
            var allItems = DataManager.Instance.AllItems;
            if (allItems.Count > 0)
            {
                var item = allItems[Random.Range(0, allItems.Count)];
                _playerData.OwnedItemCards.Add(item);
                eventTitleText.text = "发现道具";
                eventDescText.text = "你在路边发现了一个包裹。";
                eventResultText.text = "获得 " + (item.DisplayName ?? item.CardId);
                Log("获得道具 " + (item.DisplayName ?? item.CardId));
            }
            else
            {
                // 降级
                eventTitleText.text = "空箱子";
                eventDescText.text = "箱子是空的...";
                eventResultText.text = "什么都没获得";
            }
        }
        else if (r < 0.75f)
        {
            // 20% — 全员受伤
            int injuredCount = 0;
            foreach (var ci in _playerData.OwnedCharacters)
            {
                if (ci.IsDead) continue;
                if (ci.Injury == InjuryStatus.Normal)
                {
                    ci.Injury = InjuryStatus.LightInjury;
                    injuredCount++;
                }
                else if (ci.Injury == InjuryStatus.LightInjury)
                {
                    ci.Injury = InjuryStatus.HeavyInjury;
                    injuredCount++;
                }
                // HeavyInjury/Disabled 不变
            }
            eventTitleText.text = "陷阱！";
            eventDescText.text = "你踩中了隐藏陷阱！";
            eventResultText.text = injuredCount + " 名角色受伤";
            Log(injuredCount + " 名角色受伤");
        }
        else
        {
            // 25% — 什么都没发生
            eventTitleText.text = "虚惊一场";
            eventDescText.text = "前方传来奇怪的声音...但什么也没有。";
            eventResultText.text = "什么都没发生";
            Log("无事发生");
        }
    }

    /// <summary>
    /// 关闭事件界面
    /// </summary>
    public void OnClose()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        _onClose?.Invoke();
    }

    private void Log(string msg)
    {
        if (infoText != null)
            infoText.text = msg + "\n" + infoText.text;
        Debug.Log("[EventUI] " + msg);
    }
}
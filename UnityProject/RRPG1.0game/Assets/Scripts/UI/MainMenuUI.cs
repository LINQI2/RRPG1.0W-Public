using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面
///
/// 负责：显示新游戏/继续游戏/删除存档选项
/// 不负责：存档实际读写、领地逻辑
///
/// 启动时显示主界面，玩家选择后进入领地
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("UI 元素")]
    public GameObject canvasRoot;

    [Header("按钮")]
    public Button btnNewGame;
    public Button btnContinue;
    public Button btnDeleteSave;

    [Header("反馈文本")]
    public TMP_Text infoText;

    [Header("引用")]
    public TownUI townUI;
    public HeroSelectionUI heroSelectionUI;

    void OnEnable()
    {
        // 主界面重新成为前台产品门禁时同样回到 Village sim=false（返回主菜单等路径）。
        VillageRuntimeBridge.SetSimulationActive(false);
    }

    void Start()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        if (townUI != null)
            townUI.gameObject.SetActive(false);

        // Village Simulation 权威状态只在“领地”产品上下文中为 true。
        // 主界面（本对象）是外层产品门禁：无论 TownUI.Start 是否先于本对象执行过，
        // 只要主界面处于前台，就必须回到 sim=false —— World 视图 / Village 时间都随之停止，
        // 不通过 TownCanvas / VillageHUD active 重新绑定（Runtime truth 与 UI 生命周期解耦）。
        VillageRuntimeBridge.SetSimulationActive(false);

        BindButtons();
        RefreshContinueButton();
    }

    void BindButtons()
    {
        if (btnNewGame != null)
            btnNewGame.onClick.AddListener(OnNewGame);
        if (btnContinue != null)
            btnContinue.onClick.AddListener(OnContinue);
        if (btnDeleteSave != null)
            btnDeleteSave.onClick.AddListener(OnDeleteSave);
    }

    void RefreshContinueButton()
    {
        bool hasSave = SaveSystem.HasSave();
        if (btnContinue != null)
        {
            btnContinue.interactable = hasSave;
        }
    }

    void Log(string msg)
    {
        if (infoText != null)
            infoText.text = msg + "\n" + infoText.text;
    }

    /// <summary>
    /// 新游戏：显示英雄选择界面
    /// </summary>
    void OnNewGame()
    {
        if (!VillageRuntimeBridge.CanSaveWorld) { Log("世界推进/远征未完成，不能替换存档。"); return; }
        if (heroSelectionUI != null)
        {
            if (canvasRoot != null)
                canvasRoot.SetActive(false);
            heroSelectionUI.Show();
        }
        else
        {
            // 没有英雄选择界面时，走原来的直接 NewGame 逻辑
            SaveSystem.DeleteSave();
            PlayerCollection collection = SaveSystem.NewGame();
            SaveSystem.Save(collection);
            EnterTown(collection);
        }
    }

    /// <summary>
    /// 继续游戏：读取存档，进入领地
    /// </summary>
    void OnContinue()
    {
        if (!VillageRuntimeBridge.CanSaveWorld) { Log("世界推进/远征未完成，不能替换存档。"); return; }
        if (!SaveSystem.HasSave())
        {
            Log("没有存档");
            return;
        }

        PlayerCollection collection = SaveSystem.Load();
        if (collection == null)
        {
            Log("读取存档失败");
            return;
        }

        EnterTown(collection);
    }

    /// <summary>
    /// 删除存档：清空存档文件，刷新按钮状态
    /// </summary>
    void OnDeleteSave()
    {
        if (!VillageRuntimeBridge.CanSaveWorld) { Log("世界推进/远征未完成，不能替换存档。"); return; }
        SaveSystem.DeleteSave();
        RefreshContinueButton();
        Log("存档已删除");
    }

    /// <summary>
    /// 隐藏主界面，初始化并显示领地
    /// </summary>
    void EnterTown(PlayerCollection collection)
    {
        if (townUI == null)
        {
            Debug.LogError("[MainMenuUI] townUI 未绑定");
            return;
        }

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        townUI.Initialize(collection);
        townUI.gameObject.SetActive(true);
    }
}
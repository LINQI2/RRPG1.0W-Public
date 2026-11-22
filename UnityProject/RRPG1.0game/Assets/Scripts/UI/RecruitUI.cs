using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 招募界面 —— 【Legacy / NOT a Current Person Source】（Initial Person &amp; Profession Bootstrap 标记）
///
/// 本轮产品路径核对：本类的 ShowRecruit 没有任何产品调用方。
///   - Expedition 的 Recruit 节点属占位内容节点，进入后直接按 Success 结算并开放后继推进（不打开本界面）；
///   - AdventureManager 的 NodeAction.Recruit 无任何 UI 消费者；
/// 因此本类**不是** Current 产品可达的人物创建入口。
///
/// 禁止把本类当作 Current Person 创建入口：
///   它从 DataManager.AllCharacters（战斗/模板池）随机创建 CharacterInstance，不经过
///   PersonDefinition / PersonBootstrapper，产物既没有 PersonDefinition provenance、也没有开局 Profession，
///   属第二套人物来源。若未来要恢复招募玩法，必须先把候选来源改为 PersonDefinition（或明确的招募定义）
///   再重新接线，不得沿用当前实现。
///
/// 本轮只做显式隔离标记，不修改行为、不扩大开发范围。
///
/// 负责：显示可招募的角色列表，玩家选择一名免费加入队伍
/// 不负责：角色配置（从 DataManager 读取已有角色）
/// </summary>
/// 招募规则：
///   - 从所有 CharacterData 中随机抽取 3 个作为本次可招募选项（可重复出现）
///   - 玩家选择一名加入队伍（免费）
///   - 新角色默认等级 1，无经验
///   - 与 PlayerCollection 交互：添加 CharacterInstance + CharacterCardData
///
/// 使用方式：
///   1. 场景中挂 RecruitUI 组件（默认隐藏）
///   2. 点击招募节点时调用 ShowRecruit()
///   3. 玩家点击角色卡片选择加入
/// </summary>
public class RecruitUI : MonoBehaviour
{
    [Header("UI 元素")]
    public GameObject canvasRoot;
    public GameObject recruitItemPrefab;
    public RectTransform recruitContainer;
    public Text infoText;
    public Button btnClose;

    [Header("卡片布局")]
    public Vector3 cardScale = Vector3.one;
    public float cardSpacing = 200f;
    public float rowSpacing = 250f;
    public int cardsPerRow = 4;
    public Vector2 startPosition = new Vector2(-400, 0);

    /// <summary>本次可招募的角色列表（随机抽取，可重复）</summary>
    private List<CharacterData> _recruitOptions = new List<CharacterData>();

    /// <summary>玩家数据引用</summary>
    private PlayerCollection _playerData;

    /// <summary>关闭回调</summary>
    private System.Action _onClose;

    /// <summary>最后招募的角色卡（供ExpeditionUI同步用）</summary>
    public CharacterCardData LastRecruitedCard { get; private set; }

    /// <summary>生成的卡片列表</summary>
    private List<GameObject> _itemCards = new List<GameObject>();

    /// <summary>本次招募选项数量</summary>
    private const int OPTION_COUNT = 3;

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
    /// 初始化并显示招募界面
    /// 从所有角色池中随机抽取3个作为选项（可重复）
    /// </summary>
    public void ShowRecruit(PlayerCollection playerData, System.Action onClose)
    {
        _playerData = playerData;
        _onClose = onClose;

        // 从所有角色中随机抽取 OPTION_COUNT 个作为本次选项
        BuildRecruitOptions();

        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        RefreshDisplay();
        Log("选择一名角色加入队伍");
    }

    /// <summary>
    /// 构建本次招募选项
    /// 从所有角色池中随机抽取 OPTION_COUNT 个
    /// 不检查玩家是否已拥有——同一个角色可以多次招募
    /// </summary>
    private void BuildRecruitOptions()
    {
        _recruitOptions.Clear();
        var allChars = DataManager.Instance.AllCharacters;
        if (allChars == null || allChars.Count == 0)
        {
            Log("无可招募的角色数据");
            return;
        }

        // 随机抽取 OPTION_COUNT 个
        for (int i = 0; i < OPTION_COUNT; i++)
        {
            int idx = Random.Range(0, allChars.Count);
            _recruitOptions.Add(allChars[idx]);
        }
    }

    /// <summary>
    /// 刷新招募显示
    /// </summary>
    private void RefreshDisplay()
    {
        foreach (var card in _itemCards)
            Destroy(card);
        _itemCards.Clear();

        if (recruitItemPrefab == null || recruitContainer == null)
        {
            Log("recruitItemPrefab 或 recruitContainer 未绑定");
            return;
        }

        if (_recruitOptions.Count == 0)
        {
            Log("暂无可用招募角色");
            return;
        }

        int cols = Mathf.Max(1, cardsPerRow);

        for (int i = 0; i < _recruitOptions.Count; i++)
        {
            var cd = _recruitOptions[i];
            var go = Instantiate(recruitItemPrefab, recruitContainer, false);
            go.name = "RecruitItem_" + cd.DisplayName + "_" + i;
            go.transform.localScale = cardScale;

            int row = i / cols;
            int col = i % cols;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(
                startPosition.x + col * cardSpacing,
                startPosition.y - row * rowSpacing
            );

            // 设置角色信息文本
            var texts = go.GetComponentsInChildren<Text>();
            foreach (var t in texts)
            {
                if (t.name == "Text_Name") t.text = cd.DisplayName;
                else if (t.name == "Text_HP") t.text = "HP: " + cd.BaseHP;
                else if (t.name == "Text_ATK") t.text = "ATK: " + cd.BaseAttack;
                else if (t.name == "Text_Desc")
                {
                    string skillInfo = "";
                    if (cd.InitialSkillIds != null && cd.InitialSkillIds.Length > 0)
                    {
                        var skills = cd.InitialSkillIds.Select(id => DataManager.Instance.GetSkillById(id))
                            .Where(s => s != null).Select(s => s.Name);
                        skillInfo = "技能: " + string.Join(", ", skills);
                    }
                    t.text = skillInfo;
                }
            }

            // 绑定招募按钮（用索引而非 CharacterId，防止同ID冲突）
            int optionIndex = i;
            var btns = go.GetComponentsInChildren<Button>();
            foreach (var btn in btns)
            {
                if (btn.name == "BtnRecruit")
                    btn.onClick.AddListener(() => OnRecruit(optionIndex));
            }

            _itemCards.Add(go);
        }
    }

    /// <summary>
    /// 招募指定角色
    /// </summary>
    private void OnRecruit(int optionIndex)
    {
        if (_playerData == null) return;
        if (optionIndex < 0 || optionIndex >= _recruitOptions.Count) return;

        var cd = _recruitOptions[optionIndex];
        if (cd == null)
        {
            Log("招募失败：未找到角色数据");
            return;
        }

        // 创建角色实例（等级1，无经验）
        var newInstance = new CharacterInstance(cd.CharacterId, cd.DisplayName, cd.BaseHP, cd.BaseAttack);
        GrowthDefaults.EnsureSeeded(newInstance, cd.CharacterId); // V0.1：新人物入册时播种初始 Starter 1 槽 + Unlocked Pool 测试种子（按模板 ID 播种）

        // 创建角色卡（用GUID保证唯一性）
        var newCard = ScriptableObject.CreateInstance<CharacterCardData>();
        newCard.CardId = "card_" + cd.CharacterId + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
        newCard.CharacterId = cd.CharacterId;

        // 保存以供外部同步
        LastRecruitedCard = newCard;

        // 添加到玩家数据
        _playerData.OwnedCharacters.Add(newInstance);
        _playerData.OwnedCharacterCards.Add(newCard);

        // M1：新招募卡与其具体人物实例显式绑定（同模板多实例从创建起即可区分）
        _playerData.BindCharacterCardOwner(newCard.CardId, newInstance.PersonId);

        Log("招募成功！" + cd.DisplayName + " 加入了队伍");

        // 招募后关闭界面
        _playerData = null;
        OnClose();
    }

    /// <summary>
    /// 关闭招募界面
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
        Debug.Log("[RecruitUI] " + msg);
    }
}
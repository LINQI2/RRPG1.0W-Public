using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 数据管理器
///
/// 负责：从 Resources 加载所有 SO 配置数据，提供全局访问
/// 不负责：数据校验、运行时数据变更
///
/// 所有 SO 资源放在 Assets/Resources/ScriptableObjects/ 下
/// 通过 Resources.LoadAll 在游戏启动时加载
///
/// 批量加载使用方式：
///   DataManager.Instance.GetAllCharacters()
///   DataManager.Instance.GetCharacterById("char_001")
/// </summary>
public class DataManager
{
    private static DataManager _instance;
    public static DataManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new DataManager();
            return _instance;
        }
    }

    public List<CharacterData> AllCharacters { get; private set; }
    public List<HeroData> AllHeroes { get; private set; }
    // M4（Monster Legacy Physical Retirement，FDS §10.5）：Monster 路径（AllMonsters / _monsterMap /
    // Monster 静态 SO 的 Resources.LoadAll / GetMonsterById）已物理删除。
    // Monster Definition Authority = StaticConfigRegistry（生成根 → MonsterConfigDatabase → MonsterRegistry）。
    public List<SkillData> AllSkills { get; private set; }
    public List<StatusData> AllStatuses { get; private set; }
    public List<CharacterCardData> AllCharCards { get; private set; }
    public List<ItemCardData> AllItems { get; private set; }
    public List<BattleCardData> AllBattleCards { get; private set; }

    /// <summary>
    /// 全部固定初始人物定义（PersonDefinition；来源 Resources/ScriptableObjects/Persons 的 PersonDefinitionCatalog）。
    /// 只读配置数据；New Game 的人物创建只从本配置读取（不在代码写死人数或名单）。
    /// </summary>
    public List<PersonDefinition> AllPersonDefinitions { get; private set; }

    /// <summary>
    /// New Game 初始名册（InitialRosterOrder &gt; 0，按顺序升序）。
    /// 配置 N 行 → 开局 N 人；扩大/缩小名册只改配置，不改代码。
    /// </summary>
    public List<PersonDefinition> InitialPersonRoster { get; private set; }

    /// <summary>全部职业模板定义（ProfessionDefinition；来源 Resources/ScriptableObjects/Professions）。</summary>
    public List<ProfessionDefinition> AllProfessionDefinitions { get; private set; }

    /// <summary>
    /// 职业模板 Registry（构造即全量校验；非法配置 → null + 显式错误日志）。
    /// New Game Bootstrap 只从这里取得职业模板权威（不新建第二套 Registry）。
    /// </summary>
    public ProfessionDefinitionRegistry ProfessionRegistry { get; private set; }

    private Dictionary<string, CharacterData> _charMap;
    private Dictionary<string, HeroData> _heroMap;
    private Dictionary<string, SkillData> _skillMap;
    private Dictionary<string, StatusData> _statusMap;
    private Dictionary<string, CharacterCardData> _cardMap;
    private Dictionary<string, ItemCardData> _itemMap;
    private Dictionary<string, BattleCardData> _battleCardMap;
    private Dictionary<string, PersonDefinition> _personDefinitionMap;

    private DataManager()
    {
        LoadAll();
    }

    /// <summary>
    /// 加载所有 SO 资源
    /// </summary>
    private void LoadAll()
    {
        AllCharacters = Resources.LoadAll<CharacterData>("ScriptableObjects/Characters").ToList();
        AllHeroes = Resources.LoadAll<HeroData>("ScriptableObjects/Heroes").ToList();
        AllSkills = Resources.LoadAll<SkillData>("ScriptableObjects/Skills").ToList();
        AllStatuses = Resources.LoadAll<StatusData>("ScriptableObjects/Statuses").ToList();
        AllCharCards = Resources.LoadAll<CharacterCardData>("ScriptableObjects/Cards").ToList();
        AllItems = Resources.LoadAll<ItemCardData>("ScriptableObjects/Cards").ToList();
        AllBattleCards = Resources.LoadAll<BattleCardData>("ScriptableObjects/BattleCards").ToList();

        // Initial Person & Profession Bootstrap：人物定义与职业模板目录（CSV → SO 管线产出）。
        LoadPersonDefinitions();
        LoadProfessionDefinitions();

        WarnDuplicateIds(AllCharacters, c => c.CharacterId, "CharacterData");
        WarnDuplicateIds(AllHeroes, h => h.HeroId, "HeroData");
        WarnDuplicateIds(AllSkills, s => s.SkillId, "SkillData");
        WarnDuplicateIds(AllStatuses, s => s.StatusId, "StatusData");
        WarnDuplicateIds(AllCharCards, c => c.CardId, "CharacterCardData");
        WarnDuplicateIds(AllItems, i => i.CardId, "ItemCardData");
        WarnDuplicateIds(AllBattleCards, c => c.CardId, "BattleCardData");

        _charMap = AllCharacters.GroupBy(c => c.CharacterId).Select(g => g.First()).ToDictionary(c => c.CharacterId);
        _heroMap = AllHeroes.GroupBy(h => h.HeroId).Select(g => g.First()).ToDictionary(h => h.HeroId);
        _skillMap = AllSkills.GroupBy(s => s.SkillId).Select(g => g.First()).ToDictionary(s => s.SkillId);
        _statusMap = AllStatuses.GroupBy(s => s.StatusId).Select(g => g.First()).ToDictionary(s => s.StatusId);
        _cardMap = AllCharCards.GroupBy(c => c.CardId).Select(g => g.First()).ToDictionary(c => c.CardId);
        _itemMap = AllItems.GroupBy(i => i.CardId).Select(g => g.First()).ToDictionary(i => i.CardId);
        _battleCardMap = AllBattleCards.GroupBy(c => c.CardId).Select(g => g.First()).ToDictionary(c => c.CardId);

    }

    private void WarnDuplicateIds<T>(IEnumerable<T> items, System.Func<T, string> getId, string label) where T : Object
    {
        foreach (var group in items.GroupBy(getId).Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1))
        {
            string assetNames = string.Join(", ", group.Select(item => item.name));
            Debug.LogWarning($"[DataManager] Duplicate {label} id '{group.Key}' in Resources/ScriptableObjects: {assetNames}. First asset is used for runtime lookup.");
        }
    }

    /// <summary>重复 PersonDefinitionId 告警（PersonDefinition 不是 UnityEngine.Object，不能复用 SO 版 WarnDuplicateIds）。</summary>
    private void WarnDuplicatePersonDefinitionIds()
    {
        var seen = new HashSet<string>();
        var reported = new HashSet<string>();
        foreach (var def in AllPersonDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.PersonDefinitionId)) continue;
            if (seen.Add(def.PersonDefinitionId)) continue;
            if (reported.Add(def.PersonDefinitionId))
            {
                Debug.LogWarning("[DataManager] Duplicate PersonDefinition id '" + def.PersonDefinitionId
                    + "' in Resources/ScriptableObjects/Persons: first definition is used for runtime lookup.");
            }
        }
    }

    /// <summary>
    /// 加载全部 PersonDefinitionCatalog，合并为 AllPersonDefinitions + 初始名册 + 按 ID 查询表。
    /// 配置为空是合法状态（此时 New Game 走 Legacy 兼容路径，不猜测人物定义）。
    /// </summary>
    private void LoadPersonDefinitions()
    {
        AllPersonDefinitions = new List<PersonDefinition>();
        var catalogs = Resources.LoadAll<PersonDefinitionCatalog>("ScriptableObjects/Persons");
        foreach (var catalog in catalogs)
        {
            if (catalog == null || catalog.Definitions == null) continue;
            foreach (var def in catalog.Definitions)
            {
                if (def != null) AllPersonDefinitions.Add(def);
            }
        }

        WarnDuplicatePersonDefinitionIds();

        _personDefinitionMap = new Dictionary<string, PersonDefinition>();
        foreach (var def in AllPersonDefinitions)
        {
            if (string.IsNullOrEmpty(def.PersonDefinitionId)) continue;
            if (!_personDefinitionMap.ContainsKey(def.PersonDefinitionId))
                _personDefinitionMap[def.PersonDefinitionId] = def;
        }

        InitialPersonRoster = PersonDefinitionCatalog.BuildInitialRoster(AllPersonDefinitions);

        if (InitialPersonRoster.Count == 0)
        {
            Debug.LogWarning("[DataManager] 未找到任何属于初始名册的 PersonDefinition（InitialRosterOrder > 0）；"
                + "New Game 将回退到 Legacy 卡册路径。请检查 Development/Excel/Person.csv 是否已导入。");
        }
    }

    /// <summary>
    /// 加载全部 ProfessionDefinitionCatalog，并构造唯一 ProfessionDefinitionRegistry。
    /// 非法配置（空 ID / 空 CardId / Initial ⊄ Full / 重复 ProfessionId）→ Registry 为 null + 显式错误日志，
    /// 绝不用「取首个」继续；此时 Bootstrap 保持开局无职业并告警。
    /// </summary>
    private void LoadProfessionDefinitions()
    {
        AllProfessionDefinitions = new List<ProfessionDefinition>();
        var catalogs = Resources.LoadAll<ProfessionDefinitionCatalog>("ScriptableObjects/Professions");
        foreach (var catalog in catalogs)
        {
            if (catalog == null || catalog.Definitions == null) continue;
            foreach (var def in catalog.Definitions)
            {
                if (def != null) AllProfessionDefinitions.Add(def);
            }
        }

        ProfessionRegistry = null;
        if (AllProfessionDefinitions.Count == 0)
        {
            Debug.LogWarning("[DataManager] 未找到任何 ProfessionDefinition（Resources/ScriptableObjects/Professions）；"
                + "开局初始 Profession 不可用。请检查 Development/Excel/Profession.csv 是否已导入。");
            return;
        }

        try
        {
            ProfessionRegistry = ProfessionDefinitionRegistry.Build(AllProfessionDefinitions);
        }
        catch (ProfessionDefinitionValidationException ex)
        {
            Debug.LogError("[DataManager] ProfessionDefinition 配置不合法，Registry 未建立：\n" + ex.Message);
            ProfessionRegistry = null;
        }
    }

    /// <summary>按 PersonDefinitionId 查询固定初始人物定义（不存在/空返回 null；不猜、不取近似项）。</summary>
    public PersonDefinition GetPersonDefinitionById(string personDefinitionId)
    {
        if (string.IsNullOrEmpty(personDefinitionId) || _personDefinitionMap == null) return null;
        _personDefinitionMap.TryGetValue(personDefinitionId, out var def);
        return def;
    }

    public CharacterData GetCharacterById(string id)
    {
        _charMap.TryGetValue(id, out var data);
        return data;
    }

    public HeroData GetHeroById(string id)
    {
        _heroMap.TryGetValue(id, out var data);
        return data;
    }

    public SkillData GetSkillById(string id)
    {
        _skillMap.TryGetValue(id, out var data);
        return data;
    }

    public StatusData GetStatusById(string id)
    {
        _statusMap.TryGetValue(id, out var data);
        return data;
    }

    public CharacterCardData GetCardById(string id)
    {
        _cardMap.TryGetValue(id, out var data);
        return data;
    }

    public ItemCardData GetItemById(string id)
    {
        _itemMap.TryGetValue(id, out var data);
        return data;
    }

    /// <summary>
    /// 按 ID 获取 Battle V0.1 战斗牌配置
    /// </summary>
    public BattleCardData GetBattleCardById(string id)
    {
        _battleCardMap.TryGetValue(id, out var data);
        return data;
    }
}

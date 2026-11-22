using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CSV 导入工具
///
/// 负责：读取 Development/Excel/*.csv 生成所有 SO 资产
/// 原则：CSV 表头与 SO 字段名一致，支持增量导入，一行出错不影响其他行
///
/// Initial Person &amp; Profession Bootstrap 扩展：
///   - Profession.csv → ProfessionDefinitionCatalog（Resources/ScriptableObjects/Professions）
///   - Person.csv     → PersonDefinitionCatalog（Resources/ScriptableObjects/Persons）
///   - 既有角色卡 SO 补 PersonDefinitionId（卡 → 人物定义静态链接）
/// 仍是同一条 CSV → SO 管线，不引入 XLSX Reader / JSON 中间层 / 第二套配置系统。
///
/// 使用方式：Unity 菜单 → Tools → 导入 CSV 数据（或 batchmode -executeMethod ExcelImporter.ImportAll）
/// </summary>
public class ExcelImporter
{
    [MenuItem("Tools/导入 CSV 数据")]
    public static void ImportAll()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string csvDir = Path.Combine(projectRoot, "Development", "Excel");
        string soBase = "Assets/Resources/ScriptableObjects";

        // 先导入职业模板：人物定义需要校验 InitialProfessionId 是否真实存在。
        ImportProfessions(Path.Combine(csvDir, "Profession.csv"), Path.Combine(soBase, "Professions"));
        ImportPersons(Path.Combine(csvDir, "Person.csv"), Path.Combine(soBase, "Persons"));

        ImportCharacters(Path.Combine(csvDir, "Character.csv"), Path.Combine(soBase, "Characters"));
        ImportSkills(Path.Combine(csvDir, "Skill.csv"), Path.Combine(soBase, "Skills"));
        ImportStatuses(Path.Combine(csvDir, "Status.csv"), Path.Combine(soBase, "Statuses"));
        ImportItemCards(Path.Combine(csvDir, "ItemCard.csv"), Path.Combine(soBase, "Cards"));

        // M4（Monster Legacy Physical Retirement，FDS §9 Wave 1F-F3）：Monster CSV → SO 导入分支与
        // MonsterData 类型已**物理删除**；Monster Definition Authority = Development/Config/RRPG_Monsters.xlsx
        //（经 Compiler → Canonical Mirror → Unity Builder → StaticConfigRegistry）。
        // 其余领域导入（Profession / Person / Character / Skill / Status / ItemCard）不受影响。

        // 角色卡 → 人物定义 静态链接（卡资产上的 PersonDefinitionId）。
        LinkCharacterCardsToPersonDefinitions();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ 全部 CSV 导入完成！");
    }

    /// <summary>
    /// 最小范围入口（Skill-only）：只重导入 Skill.csv → Skills SO。
    /// 避免整包 ImportAll 再次改写其它生成资产（本轮导入副作用修复专用）。
    /// </summary>
    public static void ImportSkillsOnly()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string csvDir = Path.Combine(projectRoot, "Development", "Excel");
        ImportSkills(Path.Combine(csvDir, "Skill.csv"), "Assets/Resources/ScriptableObjects/Skills");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Skill.csv 重导入完成（Skill-only）。");
    }

    // ======================================================================
    // Profession（Initial Person &amp; Profession Bootstrap）
    // ======================================================================

    /// <summary>
    /// Profession.csv → ProfessionDefinitionCatalog（唯一职业模板资产）。
    /// 非法行 / 重复 ProfessionId 必须明确报错并跳过该行，不破坏其它合法行；复用 ProfessionDefinitionValidator。
    /// </summary>
    static void ImportProfessions(string csvPath, string soDir)
    {
        if (!File.Exists(csvPath)) { Debug.LogWarning($"找不到 {csvPath}，跳过"); return; }
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return;
        var h = lines[0].Split(',');

        var definitions = new List<ProfessionDefinition>();
        var seenIds = new HashSet<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            try
            {
                var c = ParseLine(lines[i]);
                string id = GetCell(c, h, "ProfessionId");
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[CSV] 职业第{i + 1}行: ProfessionId 为空，跳过该行");
                    continue;
                }
                if (!seenIds.Add(id))
                {
                    Debug.LogError($"[CSV] 职业第{i + 1}行: 重复 ProfessionId '{id}'，跳过该行（不静默取首个）");
                    continue;
                }

                var def = new ProfessionDefinition
                {
                    ProfessionId = id,
                    FullCardPool = SplitIdList(GetCell(c, h, "FullCardPool")),
                    InitialUnlockedCards = SplitIdList(GetCell(c, h, "InitialUnlockedCards")),
                    InitialStarterBuild = ParseStarterBuild(GetCell(c, h, "InitialStarterBuild")),
                    ProfessionTraitId = GetCell(c, h, "ProfessionTraitId")
                };

                if (!ProfessionDefinitionValidator.TryValidate(def, out List<string> errors))
                {
                    foreach (var e in errors) Debug.LogError($"[CSV] 职业第{i + 1}行（{id}）: {e}");
                    continue;
                }

                definitions.Add(def);
            }
            catch (System.Exception ex) { Debug.LogError($"[CSV] 职业第{i + 1}行: {ex.Message}"); }
        }

        var catalog = CreateOrGetAsset<ProfessionDefinitionCatalog>($"{soDir}/ProfessionDefinitionCatalog.asset");
        catalog.Definitions = definitions;
        EditorUtility.SetDirty(catalog);
        Debug.Log($"[CSV] Profession 导入完成：合法定义 {definitions.Count} 条"
            + "（配置多少合法行，Runtime 就支持多少 ProfessionDefinition）。");
    }

    // ======================================================================
    // Person（Initial Person &amp; Profession Bootstrap）
    // ======================================================================

    /// <summary>
    /// Person.csv → PersonDefinitionCatalog（唯一人物定义资产）。
    /// 逐行校验（并报告具体行）：
    ///   PersonDefinitionId 唯一/非空、DisplayName 非空、CombatDefinitionId 可解析、CardDefinitionId 可解析（若配置）、
    ///   InitialProfessionId 存在、InitialRosterOrder 合法且不重复、override 池 ⊆ 该职业 FullCardPool。
    /// 非法行只跳过该行，不断言整个配置；绝不根据旧 CharacterId 自动生成职业。
    /// </summary>
    static void ImportPersons(string csvPath, string soDir)
    {
        if (!File.Exists(csvPath)) { Debug.LogWarning($"找不到 {csvPath}，跳过"); return; }
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return;
        var h = lines[0].Split(',');

        var professionById = LoadProfessionDefinitionsById();
        var combatIds = LoadAssetIds<CharacterData>(x => x.CharacterId);
        var cardIds = LoadAssetIds<CharacterCardData>(x => x.CardId);
        var battleCardIds = LoadAssetIds<BattleCardData>(x => x.CardId);

        var definitions = new List<PersonDefinition>();
        var seenIds = new HashSet<string>();
        var seenOrders = new HashSet<int>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            try
            {
                var c = ParseLine(lines[i]);
                string id = GetCell(c, h, "PersonDefinitionId");
                string displayName = GetCell(c, h, "DisplayName");
                string combatId = GetCell(c, h, "CombatDefinitionId");
                string cardId = GetCell(c, h, "CardDefinitionId");
                string professionId = GetCell(c, h, "InitialProfessionId");
                string orderText = GetCell(c, h, "InitialRosterOrder");

                var rowErrors = new List<string>();

                if (string.IsNullOrEmpty(id)) rowErrors.Add("PersonDefinitionId 为空");
                else if (!seenIds.Add(id)) rowErrors.Add($"重复 PersonDefinitionId '{id}'（不静默取首个）");

                int rosterOrder = 0;
                if (!string.IsNullOrEmpty(orderText) && !int.TryParse(orderText, out rosterOrder))
                    rowErrors.Add($"InitialRosterOrder 不是整数：'{orderText}'");
                if (rosterOrder < 0) rowErrors.Add("InitialRosterOrder 不能为负数");
                if (rosterOrder > 0 && !seenOrders.Add(rosterOrder))
                    rowErrors.Add($"重复 InitialRosterOrder {rosterOrder}（初始名册顺序必须唯一）");

                if (!string.IsNullOrEmpty(combatId) && !combatIds.Contains(combatId))
                    rowErrors.Add($"CombatDefinitionId '{combatId}' 找不到对应战斗定义（CharacterData）");
                if (!string.IsNullOrEmpty(cardId) && !cardIds.Contains(cardId))
                    rowErrors.Add($"CardDefinitionId '{cardId}' 找不到对应角色卡定义（CharacterCardData）");

                InitialProfessionConfig initialProfession = BuildInitialProfessionFromRow(c, h, id, rowErrors);
                ApplyInitialProfessionCrossChecks(initialProfession, professionById, battleCardIds, id, rowErrors);

                var definition = new PersonDefinition
                {
                    PersonDefinitionId = id,
                    DisplayName = displayName,
                    CombatDefinitionId = combatId,
                    CardDefinitionId = cardId,
                    InitialProfession = initialProfession,
                    InitialRosterOrder = rosterOrder
                };
                if (!PersonDefinitionValidator.TryValidate(definition, out List<string> errors))
                    rowErrors.AddRange(errors);

                if (rowErrors.Count > 0)
                {
                    foreach (var e in rowErrors)
                        Debug.LogError($"[CSV] 人物第{i + 1}行（{id}）: {e}");
                    continue; // 一行错误：只跳过该行，不破坏其它合法行
                }

                definitions.Add(definition);
            }
            catch (System.Exception ex) { Debug.LogError($"[CSV] 人物第{i + 1}行: {ex.Message}"); }
        }

        var catalog = CreateOrGetAsset<PersonDefinitionCatalog>($"{soDir}/PersonDefinitionCatalog.asset");
        catalog.Definitions = definitions;
        EditorUtility.SetDirty(catalog);

        int rosterCount = definitions.Count(d => d.IsInInitialRoster);
        Debug.Log($"[CSV] Person 导入完成：合法定义 {definitions.Count} 条，其中初始名册 {rosterCount} 人"
            + "（配置 N 行 → New Game 创建 N 人）。");
    }

    /// <summary>解析一行 InitialProfession 配置（未配置 ProfessionId → null = 合法「开局无职业」）。</summary>
    static InitialProfessionConfig BuildInitialProfessionFromRow(List<string> c, string[] h, string personId,
        List<string> rowErrors)
    {
        string professionId = GetCell(c, h, "InitialProfessionId");
        if (string.IsNullOrEmpty(professionId)) return null;

        var config = new InitialProfessionConfig
        {
            ProfessionId = professionId,
            InitialLevel = ParseIntOrDefault(GetCell(c, h, "InitialLevel"), 1),
            InitialExp = ParseIntOrDefault(GetCell(c, h, "InitialExp"), 0),
            InitialTraitLevel = ParseIntOrDefault(GetCell(c, h, "InitialTraitLevel"), 1)
        };

        string poolOverride = GetCell(c, h, "InitialUnlockedPoolOverride");
        if (!string.IsNullOrEmpty(poolOverride))
        {
            config.HasUnlockedPoolOverride = true;
            config.InitialUnlockedPoolOverride = SplitIdList(poolOverride);
        }

        string starterOverride = GetCell(c, h, "InitialStarterBuildOverride");
        if (!string.IsNullOrEmpty(starterOverride))
        {
            config.HasStarterBuildOverride = true;
            config.InitialStarterBuildOverride = ParseStarterBuild(starterOverride);
        }

        return config;
    }

    /// <summary>
    /// 跨定义校验：InitialProfessionId 必须存在；override 池必须 ⊆ 该职业 FullCardPool；
    /// Starter override 引用到的战斗卡模板缺失只告警（内容可能尚未导入，不阻断人物创建）。
    /// </summary>
    static void ApplyInitialProfessionCrossChecks(InitialProfessionConfig config,
        Dictionary<string, ProfessionDefinition> professionById, HashSet<string> battleCardIds,
        string personId, List<string> rowErrors)
    {
        if (config == null) return;

        if (!professionById.TryGetValue(config.ProfessionId, out ProfessionDefinition profession))
        {
            rowErrors.Add($"InitialProfessionId '{config.ProfessionId}' 在 ProfessionDefinitionCatalog 中不存在");
            return;
        }

        if (config.HasUnlockedPoolOverride)
        {
            foreach (var poolCardId in config.InitialUnlockedPoolOverride)
            {
                if (profession.FullCardPool == null || !profession.FullCardPool.Contains(poolCardId))
                {
                    rowErrors.Add($"InitialUnlockedPoolOverride 的 '{poolCardId}' 不在职业 "
                        + config.ProfessionId + " 的 FullCardPool 中");
                }
            }
        }

        if (config.HasStarterBuildOverride)
        {
            foreach (var slot in config.InitialStarterBuildOverride)
            {
                if (slot != null && !string.IsNullOrEmpty(slot.CardId) && !battleCardIds.Contains(slot.CardId))
                {
                    Debug.LogWarning($"[CSV] 人物 {personId}：InitialStarterBuildOverride 的 '{slot.CardId}'"
                        + " 找不到战斗卡模板（该 Starter 条目运行时会被过滤）。");
                }
            }
        }
    }

    /// <summary>
    /// 角色卡 SO 补 PersonDefinitionId（卡 → 人物定义 静态链接）；
    /// 只写 CardDefinitionId 能解析到的既有卡资产，不新建卡、不改 CardId / CharacterId。
    /// </summary>
    static void LinkCharacterCardsToPersonDefinitions()
    {
        var personById = new Dictionary<string, PersonDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:PersonDefinitionCatalog"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PersonDefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog == null || catalog.Definitions == null) continue;
            foreach (var def in catalog.Definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.PersonDefinitionId)) continue;
                personById[def.PersonDefinitionId] = def;
            }
        }

        var cardByCardId = new Dictionary<string, CharacterCardData>();
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterCardData"))
        {
            var card = AssetDatabase.LoadAssetAtPath<CharacterCardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (card == null || string.IsNullOrEmpty(card.CardId)) continue;
            cardByCardId[card.CardId] = card;
        }

        int linked = 0;
        foreach (var def in personById.Values)
        {
            if (string.IsNullOrEmpty(def.CardDefinitionId)) continue;
            if (!cardByCardId.TryGetValue(def.CardDefinitionId, out CharacterCardData card)) continue;
            if (card.PersonDefinitionId == def.PersonDefinitionId) continue;
            card.PersonDefinitionId = def.PersonDefinitionId;
            EditorUtility.SetDirty(card);
            linked++;
        }

        if (linked > 0) Debug.Log($"[CSV] 角色卡 → PersonDefinition 链接更新 {linked} 张。");
    }

    // ======================================================================
    // Character
    // ======================================================================
    static void ImportCharacters(string csvPath, string soDir)
    {
        if (!File.Exists(csvPath)) { Debug.LogWarning($"找不到 {csvPath}，跳过"); return; }
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return;
        var h = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var c = ParseLine(lines[i]);
                var data = ScriptableObject.CreateInstance<CharacterData>();
                data.CharacterId = c[Idx(h,"CharacterId")];
                data.DisplayName = c[Idx(h,"DisplayName")];
                data.BaseHP = int.Parse(c[Idx(h,"BaseHP")]);
                data.BaseAttack = int.Parse(c[Idx(h,"BaseAttack")]);
                data.AttackRange = int.Parse(c[Idx(h,"AttackRange")]);
                data.AttackCount = int.Parse(c[Idx(h,"AttackCount")]);
                data.ActionPoints = int.Parse(c[Idx(h,"ActionPoints")]);
                data.Faction = c[Idx(h,"Faction")];
                string s = c[Idx(h,"InitialSkillIds")].Trim();
                data.InitialSkillIds = string.IsNullOrEmpty(s) ? new string[0] : s.Split(';');
                SaveAsset(data, $"{soDir}/{data.CharacterId}_{data.DisplayName}.asset");
            }
            catch (System.Exception ex) { Debug.LogError($"[CSV] 角色第{i+1}行: {ex.Message}"); }
        }
    }

    // ======================================================================
    // Skill
    // ======================================================================
    static void ImportSkills(string csvPath, string soDir)
    {
        if (!File.Exists(csvPath)) { Debug.LogWarning($"找不到 {csvPath}，跳过"); return; }
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return;
        var h = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var c = ParseLine(lines[i]);
                var data = ScriptableObject.CreateInstance<SkillData>();
                data.SkillId = c[Idx(h,"SkillId")];
                data.Name = c[Idx(h,"Name")];
                data.Trigger = ParseEnum<TriggerType>(c[Idx(h,"Trigger")]);
                data.CostActionPoints = int.Parse(c[Idx(h,"CostActionPoints")]);
                data.SkillRange = int.Parse(c[Idx(h,"SkillRange")]);

                // 解析最多 2 个效果
                var effects = new List<EffectData>();
                string et1 = c[Idx(h,"EffectType_1")];
                if (!string.IsNullOrEmpty(et1))
                {
                    effects.Add(new EffectData
                    {
                        EffectType = ParseEnum<EffectType>(et1),
                        Value = int.Parse(c[Idx(h,"Value_1")]),
                        TargetRule = ParseEnum<TargetRule>(c[Idx(h,"TargetRule_1")]),
                        StatusId = c[Idx(h,"StatusId_1")].Trim()
                    });
                }
                string et2 = c[Idx(h,"EffectType_2")];
                if (!string.IsNullOrEmpty(et2))
                {
                    // StatusId_2：与 StatusId_1 对称的显式列。旧 CSV 无该列（或该行未填）时按空处理，
                    // 保持既有行为不变（不猜状态、不改动其它行的结果）。
                    int status2Index = Idx(h, "StatusId_2");
                    string status2 = status2Index >= 0 && status2Index < c.Count
                        ? (c[status2Index] ?? string.Empty).Trim()
                        : string.Empty;

                    effects.Add(new EffectData
                    {
                        EffectType = ParseEnum<EffectType>(et2),
                        Value = int.Parse(c[Idx(h,"Value_2")]),
                        TargetRule = ParseEnum<TargetRule>(c[Idx(h,"TargetRule_2")]),
                        StatusId = status2
                    });
                }
                data.Effects = effects.ToArray();
                SaveAsset(data, $"{soDir}/{data.SkillId}_{data.Name}.asset");
            }
            catch (System.Exception ex) { Debug.LogError($"[CSV] 技能第{i+1}行: {ex.Message}"); }
        }
    }

    // ======================================================================
    // Status
    // ======================================================================
    static void ImportStatuses(string csvPath, string soDir)
    {
        if (!File.Exists(csvPath)) { Debug.LogWarning($"找不到 {csvPath}，跳过"); return; }
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return;
        var h = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var c = ParseLine(lines[i]);
                var data = ScriptableObject.CreateInstance<StatusData>();
                data.StatusId = c[Idx(h,"StatusId")];
                data.Name = c[Idx(h,"Name")];
                data.Duration = int.Parse(c[Idx(h,"Duration")]);
                data.OnExpireStatusId = c[Idx(h,"OnExpireStatusId")].Trim();
                string et = c[Idx(h,"EffectType_1")];
                if (!string.IsNullOrEmpty(et))
                {
                    data.Effects = new[] { new EffectData
                    {
                        EffectType = ParseEnum<EffectType>(et),
                        Value = int.Parse(c[Idx(h,"Value_1")]),
                        TargetRule = ParseEnum<TargetRule>(c[Idx(h,"TargetRule_1")])
                    }};
                }
                SaveAsset(data, $"{soDir}/{data.StatusId}_{data.Name}.asset");
            }
            catch (System.Exception ex) { Debug.LogError($"[CSV] 状态第{i+1}行: {ex.Message}"); }
        }
    }

    // ======================================================================
    // Monster：已于 M4 物理删除（FDS §9 Wave 1F-F3）
    //   Monster Definition Authority = Development/Config/RRPG_Monsters.xlsx
    //   （经 Compiler → Canonical Mirror → Unity Builder → StaticConfigRegistry）
    // ======================================================================

    // ======================================================================
    // ItemCard
    // ======================================================================
    static void ImportItemCards(string csvPath, string soDir)
    {
        if (!File.Exists(csvPath)) { Debug.LogWarning($"找不到 {csvPath}，跳过"); return; }
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return;
        var h = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var c = ParseLine(lines[i]);
                var data = ScriptableObject.CreateInstance<ItemCardData>();
                data.CardId = c[Idx(h,"CardId")];
                data.DisplayName = c[Idx(h,"DisplayName")];
                data.CostTP = int.Parse(c[Idx(h,"CostTP")]);
                string et = c[Idx(h,"EffectType_1")];
                if (!string.IsNullOrEmpty(et))
                {
                    data.Effects = new[] { new EffectData
                    {
                        EffectType = ParseEnum<EffectType>(et),
                        Value = int.Parse(c[Idx(h,"Value_1")]),
                        TargetRule = ParseEnum<TargetRule>(c[Idx(h,"TargetRule_1")]),
                        StatusId = c[Idx(h,"StatusId_1")].Trim()
                    }};
                }
                // 解析目标类型（默认 Ally）
                string tt = c[Idx(h,"TargetType")].Trim();
                data.TargetType = string.IsNullOrEmpty(tt) ? ItemTargetType.Ally : ParseEnum<ItemTargetType>(tt);
                SaveAsset(data, $"{soDir}/{data.CardId}_{data.DisplayName}.asset");
            }
            catch (System.Exception ex) { Debug.LogError($"[CSV] 道具第{i+1}行: {ex.Message}"); }
        }
    }

    // ======================================================================
    // 工具方法
    // ======================================================================
    // ======================================================================
    // Initial Person &amp; Profession Bootstrap：CSV 解析辅助
    // ======================================================================

    /// <summary>按表头取单元格（表头不存在返回空串；不抛异常，便于逐行报告错误）。</summary>
    static string GetCell(List<string> cells, string[] headers, string key)
    {
        int idx = Idx(headers, key);
        if (idx < 0 || cells == null || idx >= cells.Count) return string.Empty;
        return (cells[idx] ?? string.Empty).Trim();
    }

    /// <summary>解析 ';' 分隔的 ID 列表（空项丢弃；与既有 InitialSkillIds 分隔约定一致）。</summary>
    static List<string> SplitIdList(string cell)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(cell)) return result;
        foreach (var part in cell.Split(';'))
        {
            string id = part.Trim();
            if (!string.IsNullOrEmpty(id)) result.Add(id);
        }
        return result;
    }

    /// <summary>解析 Starter 构建：'cardId:proficiency;cardId:proficiency'（省略 ':proficiency' 时默认 1）。</summary>
    static List<StarterSlotEntry> ParseStarterBuild(string cell)
    {
        var result = new List<StarterSlotEntry>();
        if (string.IsNullOrEmpty(cell)) return result;

        foreach (var part in cell.Split(';'))
        {
            string entry = part.Trim();
            if (string.IsNullOrEmpty(entry)) continue;

            string cardId = entry;
            int proficiency = 1;
            int sep = entry.IndexOf(':');
            if (sep >= 0)
            {
                cardId = entry.Substring(0, sep).Trim();
                string p = entry.Substring(sep + 1).Trim();
                if (!string.IsNullOrEmpty(p) && !int.TryParse(p, out proficiency)) proficiency = 1;
            }

            if (string.IsNullOrEmpty(cardId)) continue;
            result.Add(new StarterSlotEntry(cardId, proficiency));
        }
        return result;
    }

    /// <summary>整数解析（空/非法 → 默认值；数值语义的非法由 PersonDefinitionValidator 负责报错）。</summary>
    static int ParseIntOrDefault(string cell, int fallback)
    {
        if (string.IsNullOrEmpty(cell)) return fallback;
        return int.TryParse(cell, out int value) ? value : fallback;
    }

    /// <summary>取得既有资产（保留 GUID）或新建资产（CSV 为唯一内容来源，重复导入幂等覆盖）。</summary>
    static T CreateOrGetAsset<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    /// <summary>收集某类资产的全部非空 ID（用于 Person.csv 的跨定义引用校验）。</summary>
    static HashSet<string> LoadAssetIds<T>(System.Func<T, string> getId) where T : Object
    {
        var ids = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            string id = getId(asset);
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }
        return ids;
    }

    /// <summary>读取当前 ProfessionDefinitionCatalog 内的全部定义（按 ProfessionId 索引）。</summary>
    static Dictionary<string, ProfessionDefinition> LoadProfessionDefinitionsById()
    {
        var result = new Dictionary<string, ProfessionDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:ProfessionDefinitionCatalog"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ProfessionDefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog == null || catalog.Definitions == null) continue;
            foreach (var def in catalog.Definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.ProfessionId)) continue;
                result[def.ProfessionId] = def;
            }
        }
        return result;
    }

    static int Idx(string[] headers, string key)
    {
        for (int i = 0; i < headers.Length; i++)
            if (headers[i].Trim() == key) return i;
        return -1;
    }

    static List<string> ParseLine(string line)
    {
        var r = new List<string>();
        bool q = false; string cur = "";
        foreach (char ch in line)
        {
            if (ch == '"') { q = !q; continue; }
            if (ch == ',' && !q) { r.Add(cur); cur = ""; continue; }
            cur += ch;
        }
        r.Add(cur);
        return r;
    }

    static T ParseEnum<T>(string val) where T : struct
    {
        T result;
        System.Enum.TryParse(val, true, out result);
        return result;
    }

    static void SaveAsset<T>(T data, string path) where T : ScriptableObject
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(data, path);
        EditorUtility.SetDirty(data);
    }
}
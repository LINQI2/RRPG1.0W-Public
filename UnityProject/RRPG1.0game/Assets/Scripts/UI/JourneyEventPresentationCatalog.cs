using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JourneyEventPresentationCatalog —— **Journey Event Illustration Binding V0.1 唯一配置面**
///（Implementation-only / 可替换 / Not Frozen；**不得**被表述为长期美术 Authority
/// 或正式 Event Content Authority）。
///
/// 它是什么：
///   - 一张「PresentationKey → Illustration」表，集中一处配置，被 Journey 统一 Event Card 读取；
///   - 制作人新增一张事件插图 = 导入 1 张 PNG + 在本资产 Entries +1 行 + 填 PresentationKey + 拖入 Illustration；
///   - 不需要每次去 Scene 修改 `ExpeditionJourneyRoot`。
///
/// 它不是什么（硬约束）：
///   - **不是** EventId / EncounterDefinition Authority：V0.1 的表现键来源只是当前稳定 NodeId（临时），
///     不冻结 `NodeId == EventId`，也不建立 Encounter Pool / Event Definition；
///   - **不是** Event Content Authority：不得把 Title / Body / Options / 奖励 / 条件 / 概率 / 文案建进本 Catalog；
///   - 不是最终长期方案：未来可由 `Excel Event Table → EventDefinition / EncounterDefinition
///     → PresentationKey / Illustration reference` 提供同一张表，而无需改动 `JourneyEventCardConfig` /
///     `JourneyEventCardView` 的解析契约。
///
/// 为什么用轻量 ScriptableObject（T0 Implementation Decision，与 Person Visual Binding 同构）：
///   - 一处维护，不要求把同一张表复制到多个 Scene / Prefab；
///   - 通过固定 Resources 路径读取，不做脆弱的 Runtime 场景对象搜索；
///   - 非程序人员可在 Inspector 手工配置；
///   - 缺失该资产时全部消费者走类别级 fallback（不阻断任何玩法 / 推进流程）。
/// </summary>
[CreateAssetMenu(fileName = "JourneyEventPresentationCatalog", menuName = "RRPG/Journey Event Presentation Catalog")]
public class JourneyEventPresentationCatalog : ScriptableObject
{
    /// <summary>
    /// 正式读取路径（`Assets/Resources/` 之后的相对路径，不含扩展名）。
    /// 这是唯一入口；消费者不得自行维护第二份事件插图配置表。
    /// </summary>
    public const string ResourcePath = "ScriptableObjects/Presentation/JourneyEventPresentationCatalog";

    [Tooltip("每条 = 一个 PresentationKey 的事件卡插图。V0.1 的 PresentationKey 填当前稳定 NodeId。")]
    [SerializeField] private JourneyEventPresentationEntry[] entries = new JourneyEventPresentationEntry[0];

    private Dictionary<string, JourneyEventPresentationEntry> _index;

    /// <summary>全部配置行（只读）。</summary>
    public IReadOnlyList<JourneyEventPresentationEntry> Entries
    {
        get { return entries ?? (entries = new JourneyEventPresentationEntry[0]); }
    }

    /// <summary>配置行数量（诊断 / 测试用）。</summary>
    public int EntryCount { get { return entries != null ? entries.Length : 0; } }

    // ==================== 单例读取（Resources 固定路径） ====================

    private static JourneyEventPresentationCatalog _instance;
    private static bool _resolved;
    private static bool _missingWarned;

    /// <summary>
    /// 当前生效的配置面。资产不存在时返回 null
    /// （调用方必须走类别级 fallback，不得抛异常、不得阻断流程）。
    /// </summary>
    public static JourneyEventPresentationCatalog Instance
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                _instance = Resources.Load<JourneyEventPresentationCatalog>(ResourcePath);
                if (_instance == null && !_missingWarned)
                {
                    _missingWarned = true;
                    Debug.LogWarning("[JourneyEventIllustration] 未找到配置资产 Resources/" + ResourcePath
                        + ".asset → 所有事件卡使用类别级 fallback 图（游戏流程不受影响）。");
                }
            }
            return _instance;
        }
    }

    /// <summary>测试 / 迁移用：注入一个内存配置面（null = 恢复按 Resources 路径读取）。</summary>
    public static void SetInstanceForTests(JourneyEventPresentationCatalog catalog)
    {
        _instance = catalog;
        _resolved = true;
        _missingWarned = true; // 注入期间不再输出「资产缺失」告警
    }

    /// <summary>测试 / 迁移用：清除缓存，下次读取重新走 Resources。</summary>
    public static void ResetForTests()
    {
        _instance = null;
        _resolved = false;
        _missingWarned = false;
    }

    /// <summary>测试 / 迁移用：直接设置配置行（仅内存注入，不写盘、不进入正式产品路径）。</summary>
    public void SetEntriesForTests(JourneyEventPresentationEntry[] rows)
    {
        entries = rows ?? new JourneyEventPresentationEntry[0];
        _index = null;
    }

    // ==================== 查询 ====================

    /// <summary>按表现键取配置行；未配置 / 键为空 → null（不猜、不塌缩到其它键）。</summary>
    public JourneyEventPresentationEntry FindEntry(string presentationKey)
    {
        if (string.IsNullOrEmpty(presentationKey)) return null;
        EnsureIndex();

        JourneyEventPresentationEntry entry;
        return _index.TryGetValue(presentationKey, out entry) ? entry : null;
    }

    /// <summary>TryGet 形式（与仓库既有 TryGetById 风格一致）。</summary>
    public bool TryGetEntry(string presentationKey, out JourneyEventPresentationEntry entry)
    {
        entry = FindEntry(presentationKey);
        return entry != null;
    }

    private void EnsureIndex()
    {
        if (_index != null) return;

        _index = new Dictionary<string, JourneyEventPresentationEntry>();
        if (entries == null) return;

        // 配置安全诊断（本方法在每次索引构建时执行一次 → 诊断天然去重；不自动删除 / 修改制作人配置）：
        //   - PresentationKey 为空的行：绝不进入索引（永不可被解析命中），并明确告警（不静默）；
        //   - 重复 PresentationKey：保留第一条（确定性），并明确指出「生效行 / 被忽略行」。
        var firstRowByKey = new Dictionary<string, int>();
        var emptyKeyRows = new List<int>();
        var nullRows = new List<int>();

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];

            if (e == null)
            {
                nullRows.Add(i);
                continue;
            }

            if (string.IsNullOrEmpty(e.PresentationKey))
            {
                // 空键行不会有任何事件卡能解析到它：跳过 + 诊断（制作人最容易误配的一种情况）。
                emptyKeyRows.Add(i);
                continue;
            }

            int firstRow;
            if (firstRowByKey.TryGetValue(e.PresentationKey, out firstRow))
            {
                Debug.LogWarning("[JourneyEventIllustration] 配置资产 " + name + " 存在重复 PresentationKey："
                    + e.PresentationKey + "（生效：行 " + firstRow + "；已忽略：行 " + i
                    + "）—— 解析始终使用第一条，请检查是否误配。", this);
                continue;
            }

            firstRowByKey.Add(e.PresentationKey, i);
            _index.Add(e.PresentationKey, e);
        }

        if (nullRows.Count > 0 || emptyKeyRows.Count > 0)
        {
            Debug.LogWarning("[JourneyEventIllustration] 配置资产 " + name + " 存在无效配置行："
                + (emptyKeyRows.Count > 0 ? "PresentationKey 为空（行 " + JoinRows(emptyKeyRows) + "）；" : string.Empty)
                + (nullRows.Count > 0 ? "空条目（行 " + JoinRows(nullRows) + "）；" : string.Empty)
                + "这些行被忽略，不会被任何事件卡的插图解析使用，请补全 PresentationKey 或删除该行。", this);
        }
    }

    /// <summary>行号列表 → 可读文本（仅用于配置诊断）。</summary>
    private static string JoinRows(List<int> rows)
    {
        if (rows == null || rows.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(rows[i]);
        }
        return sb.ToString();
    }

    private void OnValidate()
    {
        _index = null;
    }

    private void OnEnable()
    {
        _index = null;
    }
}

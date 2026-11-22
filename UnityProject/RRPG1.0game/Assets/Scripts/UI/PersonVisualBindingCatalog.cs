using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PersonVisualBindingCatalog —— **Person Visual Binding V0.1 唯一配置面**
///（Implementation-only / 可替换 / Not Frozen；**不得**被表述为长期美术 Authority）。
///
/// 它是什么：
///   - 一张「AppearanceKey → 外观配置」表，集中一处配置，被 Journey / Battle / Prep / Hero Selection 共同读取；
///   - 制作人新增一个人物外观 = 导入 1 张 PNG + 在本资产新增 1 行 + 填 DefaultBodySprite（可选填 surface override）。
///
/// 它不是什么（硬约束）：
///   - **不是** Person Identity Authority：不回答「这个人是谁」，身份真相仍只来自 PersonId；
///   - 不是最终长期方案：不叫 PersonVisualCatalog / 换装系统 / Spine / Animator / 美术生产 Authority；
///     可以整体替换为更正式的美术数据架构，而无需改动消费者解析契约；
///   - 不承载 Animator / Spine / 换装 / 表情 / Atlas / Addressables / Layout Profile。
///
/// 为什么用轻量 ScriptableObject（T0 Implementation Decision）：
///   - 一处新增 AppearanceKey，不要求把同一张表复制到多个 Scene / Prefab；
///   - 通过固定 Resources 路径读取，不做脆弱的 Runtime 场景对象搜索；
///   - 非程序人员可在 Inspector 手工配置；
///   - 缺失该资产时全部消费者走各自的 placeholder（不阻断任何玩法 / 选人流程）。
/// </summary>
[CreateAssetMenu(fileName = "PersonVisualBindingCatalog", menuName = "RRPG/Person Visual Binding Catalog")]
public class PersonVisualBindingCatalog : ScriptableObject
{
    /// <summary>
    /// 正式读取路径（`Assets/Resources/` 之后的相对路径，不含扩展名）。
    /// 这是唯一入口；消费者不得自行维护第二份人物质表。
    /// </summary>
    public const string ResourcePath = "ScriptableObjects/Presentation/PersonVisualBindingCatalog";

    [Tooltip("每条 = 一个 AppearanceKey 的外观配置。新增人物外观只需在此新增一行。")]
    [SerializeField] private PersonVisualBindingEntry[] entries = new PersonVisualBindingEntry[0];

    private Dictionary<string, PersonVisualBindingEntry> _index;

    /// <summary>全部配置行（只读）。</summary>
    public IReadOnlyList<PersonVisualBindingEntry> Entries
    {
        get { return entries ?? (entries = new PersonVisualBindingEntry[0]); }
    }

    /// <summary>配置行数量（诊断 / 测试用）。</summary>
    public int EntryCount { get { return entries != null ? entries.Length : 0; } }

    // ==================== 单例读取（Resources 固定路径） ====================

    private static PersonVisualBindingCatalog _instance;
    private static bool _resolved;
    private static bool _missingWarned;

    /// <summary>
    /// 当前生效的配置面。资产不存在时返回 null（调用方必须走 placeholder，不得抛异常、不得阻断流程）。
    /// </summary>
    public static PersonVisualBindingCatalog Instance
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                _instance = Resources.Load<PersonVisualBindingCatalog>(ResourcePath);
                if (_instance == null && !_missingWarned)
                {
                    _missingWarned = true;
                    Debug.LogWarning("[PersonVisualBinding] 未找到配置资产 Resources/" + ResourcePath
                        + ".asset → 所有表面使用各自 placeholder（人物视觉不显示正式素材，游戏流程不受影响）。");
                }
            }
            return _instance;
        }
    }

    /// <summary>测试 / 迁移用：注入一个内存配置面（null = 恢复按 Resources 路径读取）。</summary>
    public static void SetInstanceForTests(PersonVisualBindingCatalog catalog)
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
    public void SetEntriesForTests(PersonVisualBindingEntry[] rows)
    {
        entries = rows ?? new PersonVisualBindingEntry[0];
        _index = null;
    }

    // ==================== 查询 ====================

    /// <summary>按外观键取配置行；未配置 / 键为空 → null（不猜、不塌缩到其它键）。</summary>
    public PersonVisualBindingEntry FindEntry(string appearanceKey)
    {
        if (string.IsNullOrEmpty(appearanceKey)) return null;
        EnsureIndex();

        PersonVisualBindingEntry entry;
        return _index.TryGetValue(appearanceKey, out entry) ? entry : null;
    }

    /// <summary>TryGet 形式（与仓库既有 TryGetById 风格一致）。</summary>
    public bool TryGetEntry(string appearanceKey, out PersonVisualBindingEntry entry)
    {
        entry = FindEntry(appearanceKey);
        return entry != null;
    }

    private void EnsureIndex()
    {
        if (_index != null) return;

        _index = new Dictionary<string, PersonVisualBindingEntry>();
        if (entries == null) return;

        // 配置安全诊断（本方法在每次索引构建时执行一次 → 诊断天然去重；不自动删除 / 修改制作人配置）：
        //   - AppearanceKey 为空的行：绝不进入索引（永不可被解析命中），并明确告警（不静默）；
        //   - 重复 AppearanceKey：保留第一条（确定性），并明确指出「生效行 / 被忽略行」。
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

            if (string.IsNullOrEmpty(e.AppearanceKey))
            {
                // 空键行不会有任何人物能解析到它：跳过 + 诊断（这是制作人最容易误配的一种情况）。
                emptyKeyRows.Add(i);
                continue;
            }

            int firstRow;
            if (firstRowByKey.TryGetValue(e.AppearanceKey, out firstRow))
            {
                Debug.LogWarning("[PersonVisualBinding] 配置资产 " + name + " 存在重复 AppearanceKey："
                    + e.AppearanceKey + "（生效：行 " + firstRow + "；已忽略：行 " + i
                    + "）—— 解析始终使用第一条，请检查是否误配。", this);
                continue;
            }

            firstRowByKey.Add(e.AppearanceKey, i);
            _index.Add(e.AppearanceKey, e);
        }

        if (nullRows.Count > 0 || emptyKeyRows.Count > 0)
        {
            Debug.LogWarning("[PersonVisualBinding] 配置资产 " + name + " 存在无效配置行："
                + (emptyKeyRows.Count > 0 ? "AppearanceKey 为空（行 " + JoinRows(emptyKeyRows) + "）；" : string.Empty)
                + (nullRows.Count > 0 ? "空条目（行 " + JoinRows(nullRows) + "）；" : string.Empty)
                + "这些行被忽略，不会被任何人物的视觉解析使用，请补全 AppearanceKey 或删除该行。", this);
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

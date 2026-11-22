using TMPro;
using System.Collections.Generic;
using RRPG.Encounter;
using RRPG.StaticConfig;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prep 阶段一只敌人的**显示项**（WP-Encounter-1C §10 / §11）。
/// 数据来源只有 Active EnemyRoster + Monster 定义目录；不在这里重新抽怪、不重新计算逻辑站位。
/// </summary>
public sealed class EnemyPrepSlot
{
    /// <summary>本场身份（= BattleUnit.UnitId；同种怪多只互不相同）。</summary>
    public string EnemyInstanceId;

    /// <summary>模板身份（名称 / 数值 / 美术 / AI 全部由它解析）。</summary>
    public string MonsterId;

    /// <summary>逻辑排（由 1B 冻结）。</summary>
    public BattleRow Row;

    /// <summary>逻辑列（由 1B 冻结）。</summary>
    public BattleColumn Column;

    public string DisplayName;
    public int MaxHP;
    public int Attack;

    public override string ToString()
    {
        return EnemyInstanceId + " = " + MonsterId + " @ " + Row + "/" + Column;
    }
}

/// <summary>
/// BattleUI 的 **EnemyRoster 适配**（WP-Encounter-1C §10 / §11 / §12 / §14）。
///
/// 职责分离（本 WP 的核心）：
///   - **本场身份 / 数量 / 站位** ← Active EnemyRoster（唯一 Authority，Prep 与 Battle 共用同一份）；
///   - **模板身份（名称 / 数值 / 美术 / AI）** ← MonsterId → 正式 Monster 定义 / Enemy Presentation。
///
/// 兼容：`_enemyRoster == null` 时保持既有 Legacy / Demo 行为（`_enemyMonsters` 目录驱动），
/// 以便旧演示路径与既有回归测试不受影响；产品路径（ExpeditionUI）必定先绑定 Active EnemyRoster。
/// </summary>
public partial class BattleUI
{
    /// <summary>本场冻结的敌方阵容（产品路径）；null = 非产品（Legacy / Demo）路径。</summary>
    private EnemyRoster _enemyRoster;

    /// <summary>
    /// 产品路径是否**已表态**（ExpeditionUI 调用过 <see cref="BindActiveEnemyRoster"/>）。
    ///
    /// 为什么需要它：`_enemyRoster == null` 有两种完全不同的含义 ——
    ///   - 从未绑定（Legacy / Demo 路径）→ 允许旧目录驱动行为（既有回归测试）；
    ///   - 已绑定但为 null（遭遇生成失败）→ **明确失败**，禁止回退整个 Monster Registry。
    /// 一旦为 true，Prep / Battle 都只认 Active EnemyRoster。
    /// </summary>
    private bool _activeEnemyRosterBound;

    /// <summary>本场敌方阵容（只读；诊断 / 测试用）。null 表示当前未由产品路径绑定。</summary>
    public EnemyRoster ActiveEnemyRoster { get { return _enemyRoster; } }

    /// <summary>是否处于「产品路径 / Active EnemyRoster 权威」模式（只读；诊断用）。</summary>
    public bool IsEnemyRosterMode { get { return _activeEnemyRosterBound; } }

    /// <summary>解绑 Active EnemyRoster（Demo / Legacy 路径入口使用；产品路径不得调用）。</summary>
    private void ClearActiveEnemyRosterBinding()
    {
        _enemyRoster = null;
        _activeEnemyRosterBound = false;
    }

    /// <summary>
    /// 绑定本场 Active EnemyRoster（产品路径唯一入口；由 ExpeditionUI 在进入 Battle 前调用）。
    ///
    /// 绑定后：Prep 显示、Battle 敌方生成、敌方站位全部只消费这一份；
    /// 既不遍历整个 Monster 目录决定「有几只怪」，也不只部署前 3 只。
    /// 传 null = 本次遭遇生成失败（明确失败；调用方不得再回退到目录驱动）。
    /// </summary>
    public void BindActiveEnemyRoster(EnemyRoster roster)
    {
        _enemyRoster = roster;
        _activeEnemyRosterBound = true;

        if (roster == null)
        {
            Debug.LogError("[BattleUI] BindActiveEnemyRoster 收到 null：本场敌方阵容未冻结"
                + "（遭遇生成失败 / 未初始化）→ 拒绝开战，不回退整个 Monster Registry。");
            return;
        }

        Debug.Log("[BattleUI] 已绑定本场敌方阵容：" + roster);
    }

    /// <summary>
    /// **Prep 敌方显示计划**（Prep 唯一数据源）：按 Active EnemyRoster 生成显示项。
    ///
    /// 纯函数（不写 UI、不写战斗状态）：同一份 roster 无论 Prep 重建多少次都得到同一列表，
    /// 因此「Prep 敌人数 = 开战后敌人数」与「Prep 与 Battle 的 EnemyInstanceId / MonsterId / 排 / 列一致」
    /// 可以在测试里逐项比对。
    ///
    /// 顺序 = 稳定显示顺序（排升序 → 列升序），使排内视觉顺序与 1B 冻结的 Left→Center→Right 规则一致。
    /// </summary>
    public static bool TryBuildEnemyRosterPrepPlan(EnemyRoster roster,
        IReadOnlyList<MonsterDefinition> monsterCatalog,
        out List<EnemyPrepSlot> plan, out string diagnostic)
    {
        plan = new List<EnemyPrepSlot>();

        if (roster == null)
        {
            diagnostic = "本场敌方阵容未冻结（EnemyRoster 为 null）：无法生成战前敌人显示。";
            return false;
        }

        var entries = roster.Entries;
        if (entries == null || entries.Count == 0)
        {
            diagnostic = "本场敌方阵容为空（" + roster.EncounterId + "）：不显示任何敌人（也不得开战）。";
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
            {
                diagnostic = "敌方阵容第 " + i + " 项为 null。";
                return false;
            }

            MonsterDefinition definition;
            if (!EnemyRosterBattleMapping.TryResolveDefinition(monsterCatalog, entry.MonsterId, out definition))
            {
                diagnostic = "敌方阵容引用的 MonsterId 在正式 Monster 定义中不存在：" + entry.MonsterId
                    + "（InstanceId=" + entry.EnemyInstanceId + "）。";
                return false;
            }

            plan.Add(new EnemyPrepSlot
            {
                EnemyInstanceId = entry.EnemyInstanceId,
                MonsterId = entry.MonsterId,
                Row = EnemyRosterBattleMapping.ToBattleRow(entry.Row),
                Column = EnemyRosterBattleMapping.ToBattleColumn(entry.Column),
                DisplayName = definition.Name,
                MaxHP = definition.MaxHP,
                Attack = definition.Attack
            });
        }

        // 稳定显示顺序：排 → 列（排内即 Left → Center → Right）
        plan.Sort(ComparePrepSlots);

        diagnostic = string.Empty;
        return true;
    }

    /// <summary>用当前已绑定的 Active EnemyRoster 生成 Prep 显示计划（实例入口）。</summary>
    public bool TryBuildEnemyPrepPlan(out List<EnemyPrepSlot> plan, out string diagnostic)
    {
        return TryBuildEnemyRosterPrepPlan(_enemyRoster, _enemyMonsters, out plan, out diagnostic);
    }
    private static int ComparePrepSlots(EnemyPrepSlot a, EnemyPrepSlot b)
    {
        if (a == null || b == null) return 0;

        int row = ((int)a.Row).CompareTo((int)b.Row);
        if (row != 0) return row;

        int column = ((int)a.Column).CompareTo((int)b.Column);
        if (column != 0) return column;

        return string.CompareOrdinal(a.EnemyInstanceId ?? string.Empty, b.EnemyInstanceId ?? string.Empty);
    }

    /// <summary>
    /// 开战前预检：本场敌方阵容是否可完整入场（不得跳过任何一只、不得缩小阵容）。
    /// 失败 = 保持战前准备阶段，不进入战斗。
    /// </summary>
    private bool TryValidateEnemyRosterForBattle(out string diagnostic)
    {
        List<BattleSystem.EnemyUnitPlan> plan;
        return BattleSystem.TryBuildEnemyUnitPlan(_enemyMonsters, _enemyRoster, out plan, out diagnostic);
    }

    /// <summary>
    /// 战前准备阶段按 Active EnemyRoster 生成敌人卡（§10 / §11）。
    ///
    /// 关键点：
    ///   - 每只敌人进**它自己的逻辑排**（Front / Middle / Back），不再全部堆在 Back 排；
    ///   - 卡名 = <c>EnemyPrep_&lt;EnemyInstanceId&gt;</c>（同种怪多只也不重名、不互相覆盖）；
    ///   - 排内视觉顺序 = 排 → 列（Left → Center → Right），与 1B 冻结规则一致；
    ///   - 名称 / HP / ATK / Sprite 全部经 MonsterId（模板身份）解析。
    /// </summary>
    private void CreateEnemyPrepCardsFromActiveRoster()
    {
        List<EnemyPrepSlot> plan;
        string diagnostic;
        if (!TryBuildEnemyPrepPlan(out plan, out diagnostic))
        {
            Debug.LogError("[BattleUI] 战前准备无法显示本场敌人：" + diagnostic + "（不显示任何敌人，也不允许开战）");
            return;
        }

        for (int i = 0; i < plan.Count; i++)
        {
            var slot = plan[i];
            var rowRect = GetRowRect(false, slot.Row);
            if (rowRect == null || _rowCards == null || !_rowCards.ContainsKey(rowRect)) continue;

            var go = Instantiate(unitCardPrefab, rowRect, false);
            go.transform.localScale = monsterCardScale;
            go.name = "EnemyPrep_" + slot.EnemyInstanceId;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;   // WP-D：排内视觉位置统一由 Formation Presentation 计算

            var tn = go.transform.Find("Text_Name")?.GetComponent<TMP_Text>();
            var th = go.transform.Find("Text_HP")?.GetComponent<TMP_Text>();
            var ta = go.transform.Find("Text_ATK")?.GetComponent<TMP_Text>();
            if (tn != null) tn.text = slot.DisplayName;
            if (th != null) th.text = slot.MaxHP + "/" + slot.MaxHP;
            if (ta != null) ta.text = slot.Attack + "";

            // M3 Slice 1D-1 / 1C §12：敌方身体视觉仍走**同一个** Resolver（MonsterId → Sprite）；
            // 绝不改成 EnemyInstanceId → Sprite（实例身份不是美术模板身份）。
            ApplyEnemyBodyVisual(go, slot.MonsterId);

            _rowCards[rowRect].Add(go);
        }

        // 战前 / 开战共用同一排内视觉入口（1 人居中 / 2 人左右 / 3 人左中右；不重叠）
        ApplyFormationPresentationToAllRows();
    }
}

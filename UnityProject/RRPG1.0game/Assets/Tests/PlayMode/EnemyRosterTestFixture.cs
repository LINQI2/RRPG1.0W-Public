using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// **测试侧 EnemyRoster Fixture**（WP-Event-1D-R · PlayMode Regression Baseline Repair）。
///
/// 只服务「需要**确定性**敌方阵容」的 PlayMode 测试：构造**明确合法**的 `RRPG.Encounter.EnemyRoster`
/// （总数 1..9、每格唯一、每排 ≤3、所有 MonsterId 必须能在正式 Monster 目录解析），
/// 并由测试经**产品正式入口** `BattleUI.BindActiveEnemyRoster(roster)` 绑定给本场战斗。
///
/// 为什么需要它（本 WP 修复的根因）：
///   - 旧测试把 `StaticMonsterTestData.Build()`（= **整个 Monster Registry**，当前 6 只）当作
///     「本场敌人」注入 `BattleUI.InitFromExpedition` 的 Legacy 列表路径；
///   - 那**不是**当前一场战斗的 Enemy Authority：Legacy 路径只把前 3 只放进 Enemy Board，
///     其余单位仍留在 `EnemyUnits`（Row/Column 为默认排）——既违反「每只敌人必须有唯一合法站位」，
///     又会因 `BattleSystem.GetBlockingRowCount`（按单位自身 Row 统计阻挡排）计入阻挡，污染 Range 判定。
///
/// 边界（刻意）：
///   - 本 Fixture **只组装数据**：不生成 / 不重抽 / 不复制怪物数值（数值仍由 MonsterId → 正式定义解析）、
///     不改任何产品站位规则；
///   - 正式产品路径仍由 Encounter Generator 产出 Roster（见 `EncounterProductPathPlayTests` 的完整产品链）；
///   - Monster 目录在本 Fixture 只作**模板身份解析源**（MonsterId → 名称 / 数值 / 美术 / AI）。
/// </summary>
public static class EnemyRosterTestFixture
{
    private const string EncounterAssembly = "Encounter.Runtime";

    /// <summary>
    /// 构造合法 EnemyRoster：<paramref name="cells"/> = 交替的 row/column 序列（0..2）；
    /// MonsterId 由 <paramref name="monsterIds"/> 轮转取用（同种怪多只时实例身份仍唯一）。
    /// 非法形状（空 / 奇数 / 重复格 / 越界 / &gt;9 / 每排 &gt;3 / MonsterId 为空）直接 `Assert.Fail` ——
    /// Fixture 自身必须自洽，不得把非法阵容交给产品。
    /// </summary>
    public static object NewRoster(string encounterId, IList<string> monsterIds, params int[] cells)
    {
        Assert.IsNotNull(monsterIds, "Fixture 需要至少一个 MonsterId（来自正式 Monster 目录）");
        Assert.Greater(monsterIds.Count, 0, "Fixture 需要至少一个 MonsterId（来自正式 Monster 目录）");
        Assert.IsNotNull(cells, "Fixture 需要站位序列");
        Assert.AreEqual(0, cells.Length % 2, "Fixture 站位序列必须是 row/column 成对");

        int count = cells.Length / 2;
        Assert.GreaterOrEqual(count, 1, "Fixture 至少 1 只敌人（不得构造 0 敌人的战斗）");
        Assert.LessOrEqual(count, 9, "Fixture 不得超过敌方 3×3 容量（9 只）");

        var cellsSeen = new HashSet<string>();
        var perRow = new Dictionary<int, int>();
        for (int i = 0; i < count; i++)
        {
            int row = cells[i * 2];
            int column = cells[i * 2 + 1];
            Assert.IsTrue(row >= 0 && row <= 2, "Fixture 排越界：" + row);
            Assert.IsTrue(column >= 0 && column <= 2, "Fixture 列越界：" + column);
            Assert.IsTrue(cellsSeen.Add(row + ":" + column), "Fixture 出现重复格：" + row + ":" + column);

            int inRow;
            perRow.TryGetValue(row, out inRow);
            perRow[row] = inRow + 1;
            Assert.LessOrEqual(perRow[row], 3, "Fixture 第 " + row + " 排超过 3 只");
        }

        var entryType = EntryType();
        var listType = typeof(List<>).MakeGenericType(entryType);
        var entries = (IList)Activator.CreateInstance(listType);

        for (int i = 0; i < count; i++)
        {
            string monsterId = monsterIds[i % monsterIds.Count];
            Assert.IsFalse(string.IsNullOrEmpty(monsterId),
                "Fixture MonsterId 为空（必须来自正式 Monster 定义目录）");

            entries.Add(NewEntry(entryType, "fixture_enemy_" + (i + 1), monsterId, cells[i * 2], cells[i * 2 + 1]));
        }

        var rosterType = RosterType();
        var roster = Activator.CreateInstance(rosterType);
        rosterType.GetField("EncounterId").SetValue(roster, encounterId);
        rosterType.GetField("PoolId").SetValue(roster, string.Empty);
        rosterType.GetField("Seed").SetValue(roster, 0);
        rosterType.GetField("TotalThreat").SetValue(roster, 0);
        rosterType.GetField("Entries").SetValue(roster, entries);
        return roster;
    }

    /// <summary>正式 Monster 目录（`StaticConfigRegistry.Monsters.All`；敌方模板身份的唯一天然来源）。</summary>
    public static IList Catalog()
    {
        return StaticMonsterTestData.Build();
    }

    /// <summary>取正式目录中第 index 个 MonsterId（保持目录身份，不自造 id）。</summary>
    public static string MonsterIdAt(IList catalog, int index)
    {
        Assert.IsNotNull(catalog, "Monster 目录不可用（StaticConfigRegistry.Monsters）");
        Assert.Greater(catalog.Count, 0, "Monster 目录为空");

        var definition = catalog[index % catalog.Count];
        var field = definition.GetType().GetField("MonsterId");
        Assert.IsNotNull(field, "MonsterDefinition.MonsterId 字段不存在");
        return (string)field.GetValue(definition);
    }

    private static object NewEntry(Type entryType, string enemyInstanceId, string monsterId, int row, int column)
    {
        var entry = Activator.CreateInstance(entryType);
        entryType.GetField("EnemyInstanceId").SetValue(entry, enemyInstanceId);
        entryType.GetField("MonsterId").SetValue(entry, monsterId);
        SetEnum(entryType, entry, "Row", row);
        SetEnum(entryType, entry, "Column", column);
        entryType.GetField("SlotId").SetValue(entry, "fixture");
        return entry;
    }

    private static void SetEnum(Type type, object target, string fieldName, int value)
    {
        var field = type.GetField(fieldName);
        Assert.IsNotNull(field, "EnemyRoster 字段不存在：" + fieldName);
        field.SetValue(target, Enum.ToObject(field.FieldType, value));
    }

    private static Type EntryType()
    {
        var type = Type.GetType("RRPG.Encounter.GeneratedEnemyEntry, " + EncounterAssembly, false);
        Assert.IsNotNull(type, "未找到 RRPG.Encounter.GeneratedEnemyEntry（" + EncounterAssembly + "）");
        return type;
    }

    private static Type RosterType()
    {
        var type = Type.GetType("RRPG.Encounter.EnemyRoster, " + EncounterAssembly, false);
        Assert.IsNotNull(type, "未找到 RRPG.Encounter.EnemyRoster（" + EncounterAssembly + "）");
        return type;
    }
}

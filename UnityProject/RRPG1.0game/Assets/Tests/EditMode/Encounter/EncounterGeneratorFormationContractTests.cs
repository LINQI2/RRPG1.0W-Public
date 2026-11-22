using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RRPG.Encounter;
using RRPG.StaticConfig.EditorTools;

namespace RRPG.Encounter.Tests
{
    /// <summary>
    /// 敌方排 / 列规则与**玩家侧已冻结规则**的一致性守卫（WP-Encounter-1B §21）。
    ///
    /// 为什么用反射：<c>BattleFormationPlan</c> 位于预定义程序集 Assembly-CSharp
    /// （既有测试同样按 <c>Assembly-CSharp</c> 解析它），本程序集无法编译期引用；
    /// 搬移它会破坏既有玩家侧测试，因此这里改为**逐值比对**，保证两侧不会静默分叉。
    ///
    /// 同时提供源码级 / asmdef 级守卫：Encounter 领域不得使用引擎全局随机 / Guid / 时间（§11 / §17）。
    /// </summary>
    public class EncounterGeneratorFormationContractTests
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        [Test]
        public void EnemyColumnRule_MatchesBattleFormationPlan_ValueForValue()
        {
            var type = Type.GetType("BattleFormationPlan, Assembly-CSharp");
            Assert.IsNotNull(type, "BattleFormationPlan 必须存在于 Assembly-CSharp（玩家侧冻结规则）");

            var columnIndexForSlot = type.GetMethod("ColumnIndexForSlot", PublicStatic);
            var slotCount = type.GetMethod("SlotCount", PublicStatic);
            var maxUnitsPerRow = type.GetField("MaxUnitsPerRow", PublicStatic);
            var rowCount = type.GetField("RowCount", PublicStatic);

            Assert.IsNotNull(columnIndexForSlot, "BattleFormationPlan.ColumnIndexForSlot 必须存在");
            Assert.IsNotNull(slotCount, "BattleFormationPlan.SlotCount 必须存在");
            Assert.IsNotNull(maxUnitsPerRow, "BattleFormationPlan.MaxUnitsPerRow 必须存在");
            Assert.IsNotNull(rowCount, "BattleFormationPlan.RowCount 必须存在");

            Assert.AreEqual((int)maxUnitsPerRow.GetValue(null), EnemyBoardFormation.MaxUnitsPerRow,
                "一排列数必须与玩家侧一致（3）");
            Assert.AreEqual((int)rowCount.GetValue(null), EnemyBoardFormation.RowCount,
                "排数必须与玩家侧一致（3）");

            for (int unitsInRow = 0; unitsInRow <= 3; unitsInRow++)
            {
                int expectedSlots = (int)slotCount.Invoke(null, new object[] { unitsInRow });
                Assert.AreEqual(expectedSlots, EnemyBoardFormation.ColumnCount(unitsInRow),
                    "该排的列数必须与玩家侧一致（unitsInRow=" + unitsInRow + "）");

                for (int slot = 0; slot < 3; slot++)
                {
                    int expected = (int)columnIndexForSlot.Invoke(null, new object[] { slot, unitsInRow });
                    Assert.AreEqual(expected, EnemyBoardFormation.ColumnIndexForSlot(slot, unitsInRow),
                        "排内槽位→列 必须与玩家侧一致（slot=" + slot + ", unitsInRow=" + unitsInRow + "）");
                }
            }
        }

        [Test]
        public void EnemyBoardFormation_OneTwoThreeRules_AreTheFrozenRules()
        {
            CollectionAssert.AreEqual(new[] { EnemyRosterColumn.Center },
                EnemyBoardFormation.RequiredColumns(1), "1 人 → Center");
            CollectionAssert.AreEqual(new[] { EnemyRosterColumn.Left, EnemyRosterColumn.Right },
                EnemyBoardFormation.RequiredColumns(2), "2 人 → Left + Right");
            CollectionAssert.AreEqual(
                new[] { EnemyRosterColumn.Left, EnemyRosterColumn.Center, EnemyRosterColumn.Right },
                EnemyBoardFormation.RequiredColumns(3), "3 人 → Left + Center + Right");

            Assert.IsFalse(EnemyBoardFormation.IsLegalColumn(1, EnemyRosterColumn.Left), "1 人排不得用 Left");
            Assert.IsFalse(EnemyBoardFormation.IsLegalColumn(1, EnemyRosterColumn.Right), "1 人排不得用 Right");
            Assert.IsTrue(EnemyBoardFormation.IsLegalColumn(1, EnemyRosterColumn.Center), "1 人排必须用 Center");
            Assert.IsFalse(EnemyBoardFormation.IsLegalColumn(2, EnemyRosterColumn.Center), "2 人排不得用 Center");
            Assert.IsTrue(EnemyBoardFormation.IsLegalColumn(3, EnemyRosterColumn.Center), "3 人排可以用 Center");

            Assert.IsFalse(EnemyBoardFormation.IsLegalRowCount(4), "每排上限必须仍是 3");
            Assert.AreEqual(9, EnemyBoardFormation.MaxEnemyUnits, "敌方物理容量必须仍是 9");
        }

        [Test]
        public void EncounterRuntime_HasNoEngineRandom_GuidOrTime()
        {
            string dir = Path.Combine(StaticConfigBuilder.RepoRoot(),
                "UnityProject", "RRPG1.0game", "Assets", "Scripts", "Encounter");
            Assert.IsTrue(Directory.Exists(dir), "Encounter 领域目录必须存在：" + dir);

            string[] forbidden = { "UnityEngine.Random", "System.Guid", "Guid.NewGuid", "DateTime.Now", "DateTime.UtcNow" };
            string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 0, "必须真的扫描到 Encounter 领域源文件");

            foreach (string file in files)
            {
                string code = StripComments(File.ReadAllText(file));
                foreach (string symbol in forbidden)
                {
                    Assert.IsFalse(code.Contains(symbol),
                        "Encounter 领域不得使用 " + symbol + "（会破坏可复现性）：" + Path.GetFileName(file));
                }
            }
        }

        [Test]
        public void EncounterRuntime_AssemblyDeclaresNoEngineReferences()
        {
            string asmdef = Path.Combine(StaticConfigBuilder.RepoRoot(),
                "UnityProject", "RRPG1.0game", "Assets", "Scripts", "Encounter", "Encounter.Runtime.asmdef");
            Assert.IsTrue(File.Exists(asmdef), "必须存在 Encounter.Runtime.asmdef：" + asmdef);

            string text = File.ReadAllText(asmdef);
            StringAssert.Contains("\"noEngineReferences\": true", text,
                "Encounter 领域必须保持无引擎依赖（机器上阻止误用 UnityEngine 全局状态）");
        }

        private static string StripComments(string source)
        {
            var builder = new System.Text.StringBuilder();
            foreach (string line in source.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                builder.AppendLine(line);
            }
            return builder.ToString();
        }

    }
}

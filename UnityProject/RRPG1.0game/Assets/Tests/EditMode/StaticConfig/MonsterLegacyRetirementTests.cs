using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RRPG.StaticConfig.EditorTools;
using UnityEngine;
using UnityEngine.TestTools;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// **M4 Monster Legacy Physical Retirement — 关闭契约测试**（FDS §9 Wave 1F / §10.5；M3-R2）。
    ///
    /// 覆盖 M4 Ready for Review 的机器可判定验收项：
    ///   M4-1：敌方定义 Authority 不可用时，**既有开战入口**拒绝本次战斗初始化
    ///         （不置 _battleStarted → 不出现 0 敌人 / 半初始化战斗）；
    ///   M4-2：旧 `enemyBodyPlaceholder` 字段与 Scene 序列化条目**已物理删除**（M3-R2）；
    ///   M4-3：Legacy 资产 / 类型 / 入口**已物理删除**（`Monster.csv`、Monster SO 目录、`MonsterData`、
    ///         `DataManager.AllMonsters`），并保留可操作的退役诊断。
    ///
    /// BattleUI 位于预定义程序集 Assembly-CSharp（本测试程序集不编译期引用）→ 一律反射访问。
    /// </summary>
    public class MonsterLegacyRetirementTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        private const string SceneRepoRelativePath = "UnityProject/RRPG1.0game/Assets/Scenes/SampleScene.scene";

        private static Type BattleUIType()
        {
            return Type.GetType("BattleUI, Assembly-CSharp");
        }

        // ---------------- M4-1：缺 Authority → 既有开战入口拒绝半初始化 ----------------

        [Test]
        public void M4_1_BattleUI_RefusesStart_WhenEnemyAuthorityUnavailable()
        {
            var type = BattleUIType();
            Assert.IsNotNull(type, "BattleUI 未找到（Assembly-CSharp）");

            var go = new GameObject("M4Test_BattleUI");
            try
            {
                var ui = go.AddComponent(type);
                Assert.IsNotNull(ui, "BattleUI 组件创建失败");

                // 模拟「生成根缺失 / 未导表」：敌方定义列表为空（产品路径下 = StaticConfigRegistry.Monsters == null）
                var field = type.GetField("_enemyMonsters", F);
                Assert.IsNotNull(field, "BattleUI._enemyMonsters 字段未找到");
                var monsterDefinition = Type.GetType("RRPG.StaticConfig.MonsterDefinition, StaticConfig.Runtime");
                Assert.IsNotNull(monsterDefinition, "MonsterDefinition 类型未找到");
                var emptyList = (System.Collections.IList)Activator.CreateInstance(
                    typeof(System.Collections.Generic.List<>).MakeGenericType(monsterDefinition));
                field.SetValue(ui, emptyList);

                var onStartBattle = type.GetMethod("OnStartBattle", F);
                Assert.IsNotNull(onStartBattle, "BattleUI.OnStartBattle 未找到");

                LogAssert.Expect(LogType.Warning, new Regex("OnStartBattle 拒绝开战"));
                onStartBattle.Invoke(ui, null);

                var started = (bool)type.GetProperty("IsBattleStarted", F).GetValue(ui);
                Assert.IsFalse(started,
                    "M4：敌方定义不可用时不得开始战斗（既有入口拒绝，保持战前准备，不半初始化）");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---------------- M4-2：enemyBodyPlaceholder 字段 + Scene 条目已删除 ----------------

        [Test]
        public void M4_2_LegacyEnemyPlaceholder_Removed_FromCodeAndScene()
        {
            var type = BattleUIType();
            Assert.IsNotNull(type, "BattleUI 未找到（Assembly-CSharp）");
            Assert.IsNull(type.GetField("enemyBodyPlaceholder", F),
                "M4：BattleUI.enemyBodyPlaceholder 字段必须已物理删除");

            string scenePath = Path.Combine(StaticConfigBuilder.RepoRoot(),
                SceneRepoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(scenePath), "Scene 必须存在：" + scenePath);

            string scene = File.ReadAllText(scenePath);
            Assert.IsFalse(scene.Contains("enemyBodyPlaceholder"),
                "M4：Scene 不得再保留 enemyBodyPlaceholder 序列化条目（该引用指向的生成资产已不存在，属悬空引用）");
        }

        // ---------------- M4-3：Legacy 资产 / 类型 / 入口已删除 ----------------

        [Test]
        public void M4_3_LegacyAssetsAndType_ArePhysicallyRemoved()
        {
            string repo = StaticConfigBuilder.RepoRoot();
            string assets = Path.Combine(repo, "UnityProject", "RRPG1.0game", "Assets");

            Assert.IsFalse(File.Exists(Path.Combine(repo, "Development", "Excel", "Monster.csv")),
                "M4：Development/Excel/Monster.csv 必须已删除");
            Assert.IsFalse(Directory.Exists(Path.Combine(assets, "Resources", "ScriptableObjects", "Monsters")),
                "M4：Monster SO 目录（含 .meta）必须已删除");
            Assert.IsFalse(File.Exists(Path.Combine(assets, "ScriptableObjects", "Monsters", "MonsterData.cs")),
                "M4：MonsterData.cs 类型必须已删除");
            Assert.IsNull(Type.GetType("MonsterData, Assembly-CSharp"),
                "M4：MonsterData 类型必须已从 Assembly-CSharp 消失");

            var dataManager = Type.GetType("DataManager, Assembly-CSharp");
            Assert.IsNotNull(dataManager, "DataManager 必须保留（仍服务 Character / Skill / Status 等领域）");
            Assert.IsNull(dataManager.GetProperty("AllMonsters", F),
                "M4：DataManager.AllMonsters 必须已删除（仅删 Monster 路径，DataManager 本体保留）");
            Assert.IsNull(dataManager.GetMethod("GetMonsterById", F),
                "M4：DataManager.GetMonsterById 必须已删除");

            Assert.IsNotEmpty(StaticConfigRegistry.LegacyFallbackRetiredNote,
                "M4：退役说明常量必须存在（Root 缺失诊断必须可操作）");
        }
    }
}

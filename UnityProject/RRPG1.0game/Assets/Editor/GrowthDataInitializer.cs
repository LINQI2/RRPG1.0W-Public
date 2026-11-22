using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 人物长期成长测试数据生成工具（演示/测试值，未冻结）。
///
/// 使用方式：菜单 Tools/人物成长测试数据；也可由编辑器加载时自动生成（见下方 InitializeOnLoad）。
/// 正式经验曲线 / 等级上限 / Starter 解锁等级 / Trait 数值 / Proficiency 上限均未冻结，
/// 本工具生成的资产仅用于验证产品闭环，不得作为正式数值。
/// 等级 → 节点 映射由 GrowthTableConfig 资产数据驱动，不在代码硬编码。
///
/// 说明：在 Test Runner 等特殊编辑器上下文中 AssetDatabase.CreateAsset 对 asmdef 类型
/// 可能写入 m_Script: {fileID: 0}（脚本引用失效）；因此测试数据改为编辑器正常加载时
/// （InitializeOnLoad + delayCall）自动幂等生成，测试只做存在性校验。
/// </summary>
public static class GrowthDataInitializer
{
    [InitializeOnLoadMethod]
    private static void AutoSeedTestDataOnLoad()
    {
        EditorApplication.delayCall += CreateTestData;
    }

    [MenuItem("Tools/人物成长测试数据")]
    public static void CreateTestData()
    {
        string basePath = "Assets/Resources/ScriptableObjects/Growth";

        // ---- 经验曲线（测试值）----
        CreateOrReplace<GrowthCurveConfig>($"{basePath}/TestGrowthCurve.asset", c =>
        {
            c.MaxLevel = 10;
            c.ProficiencyCap = 5;
            c.ExpRequiredToLevel = new[] { 10, 15, 20, 25, 30, 40, 50, 60, 80 };
        });

        // ---- Trait（测试值：每人唯一专属 Trait）----
        var traits = new Dictionary<string, TraitData>();
        string[] persons = { "char_001", "char_002", "char_003", "char_004", "hero_001" };
        string[] names = { "战士", "猎人", "医师", "村长", "青鹿" };
        string[] traitNames = { "坚韧", "猎手本能", "医者仁心", "鼓舞", "鹿灵" };
        for (int i = 0; i < persons.Length; i++)
        {
            string id = "trait_" + persons[i];
            traits[id] = CreateOrReplace<TraitData>($"{basePath}/Traits/{id}.asset", t =>
            {
                t.TraitId = id;
                t.DisplayName = traitNames[i] + "（" + names[i] + "）";
                t.Description = "【测试值】" + names[i] + " 的专属被动，正式效果未冻结。";
                t.MaxLevel = 3;
            });
        }

        // ---- Trait 目录（人物 → 默认 Trait）----
        CreateOrReplace<TraitCatalog>($"{basePath}/TestTraitCatalog.asset", cat =>
        {
            cat.DefaultTraits = new List<TraitCatalog.PersonTraitEntry>();
            foreach (var p in persons)
            {
                if (traits.TryGetValue("trait_" + p, out var tr))
                    cat.DefaultTraits.Add(new TraitCatalog.PersonTraitEntry { PersonId = p, Trait = tr });
            }
        });

        // ---- 成长节点（测试表：数据驱动，不在代码硬编码 Lv2/Lv5/Lv10）----
        var nodeLv2 = CreateOrReplace<GrowthNodeConfig>($"{basePath}/Nodes/GrowthNode_Lv2_CardGrowth.asset", n =>
        {
            n.Level = 2;
            n.NodeType = GrowthNodeType.CardGrowth;
        });

        var nodeLv3 = CreateOrReplace<GrowthNodeConfig>($"{basePath}/Nodes/GrowthNode_Lv3_StarterSlotUnlock.asset", n =>
        {
            n.Level = 3;
            n.NodeType = GrowthNodeType.StarterSlotUnlock;
        });

        var nodeLv4 = CreateOrReplace<GrowthNodeConfig>($"{basePath}/Nodes/GrowthNode_Lv4_TraitUpgrade.asset", n =>
        {
            n.Level = 4;
            n.NodeType = GrowthNodeType.TraitUpgrade;
        });

        var nodeLv5 = CreateOrReplace<GrowthNodeConfig>($"{basePath}/Nodes/GrowthNode_Lv5_UnlockedPoolExpand.asset", n =>
        {
            n.Level = 5;
            n.NodeType = GrowthNodeType.UnlockedPoolExpand;
            // 演示：向 Unlocked Pool 扩展全部演示角色牌（每个人物至少获得 1 张此前未解锁的新牌）
            n.ExpandCardIds = new[] { "bcard_aim", "bcard_sword", "bcard_heal", "bcard_rally" };
        });

        // ---- 成长表 ----
        CreateOrReplace<GrowthTableConfig>($"{basePath}/TestGrowthTable.asset", t =>
        {
            t.TableId = "test_table";
            t.Nodes = new List<GrowthNodeConfig> { nodeLv2, nodeLv3, nodeLv4, nodeLv5 };
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ 人物成长测试数据生成完成（测试值，未冻结）");
    }

    private static T CreateOrReplace<T>(string path, System.Action<T> setFields) where T : ScriptableObject
    {
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        // 幂等创建：已存在则复用（避免删除重建导致 m_Script 引用丢失/不稳定）。
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var asset = ScriptableObject.CreateInstance<T>();
        setFields(asset);
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }
}

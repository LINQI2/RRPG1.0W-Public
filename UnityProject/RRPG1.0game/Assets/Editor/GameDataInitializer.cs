using UnityEngine;
using UnityEditor;

/// <summary>
/// 游戏数据初始化工具
///
/// 负责：生成 MVP 阶段所有 SO 资产文件到 Resources/ScriptableObjects/
/// 原则：与 BattleUI.SetupDemoData() 数据一致，已验证可运行
///
/// 使用方式：Unity 菜单 → Tools → 初始化游戏数据
/// </summary>
public class GameDataInitializer
{
    [MenuItem("Tools/初始化游戏数据")]
    static void InitializeGameData()
    {
        string basePath = "Assets/Resources/ScriptableObjects";

        // ============================================
        // 第一批：CharacterData / HeroData（Monster 定义已于 M4 移出本初始化器：走 StaticConfig）
        // ============================================

        // --- 角色 ---
        CreateAndSet<CharacterData>($"{basePath}/Characters/char_001_战士.asset", c =>
        {
            c.CharacterId = "char_001"; c.DisplayName = "战士";
            c.BaseHP = 30; c.BaseAttack = 6;
            c.AttackRange = 1; c.AttackCount = 1; c.ActionPoints = 1; c.Faction = "player";
            c.InitialSkillIds = new[] { "sk_001" };
        });

        CreateAndSet<CharacterData>($"{basePath}/Characters/char_002_猎人.asset", c =>
        {
            c.CharacterId = "char_002"; c.DisplayName = "猎人";
            c.BaseHP = 22; c.BaseAttack = 7;
            c.AttackRange = 2; c.AttackCount = 1; c.ActionPoints = 1; c.Faction = "player";
        });

        CreateAndSet<CharacterData>($"{basePath}/Characters/char_003_医师.asset", c =>
        {
            c.CharacterId = "char_003"; c.DisplayName = "医师";
            c.BaseHP = 25; c.BaseAttack = 4;
            c.AttackRange = 1; c.AttackCount = 1; c.ActionPoints = 1; c.Faction = "player";
            c.InitialSkillIds = new[] { "sk_002" };
        });

        CreateAndSet<CharacterData>($"{basePath}/Characters/char_004_村长.asset", c =>
        {
            c.CharacterId = "char_004"; c.DisplayName = "村长";
            c.BaseHP = 30; c.BaseAttack = 2;
            c.AttackRange = 1; c.AttackCount = 1; c.ActionPoints = 1; c.Faction = "player";
            c.InitialSkillIds = new[] { "sk_003" };
        });

        // --- 英雄 ---
        CreateAndSet<HeroData>($"{basePath}/Heroes/hero_001_青鹿.asset", c =>
        {
            c.HeroId = "hero_001"; c.DisplayName = "青鹿";
            c.BaseHP = 35; c.BaseAttack = 8;
            c.AttackRange = 1; c.AttackCount = 1; c.ActionPoints = 1; c.Faction = "player";
        });

        // --- 怪物：M4 物理清理已完成（原 Monster seed 代码块已删除）---
        // Monster Definition Authority = Development/Config/RRPG_Monsters.xlsx（经 StaticConfigRegistry 读取）。
        // 其余初始化逻辑（Character / Hero / Skill / Status / ItemCard / …）不受影响。

        // ============================================
        // 第二批：SkillData / StatusData
        // ============================================

        CreateAndSet<SkillData>($"{basePath}/Skills/sk_001_劈砍.asset", c =>
        {
            c.SkillId = "sk_001"; c.Name = "劈砍";
            c.Trigger = TriggerType.Active; c.CostActionPoints = 1;
            c.Effects = new[] { new EffectData { EffectType = EffectType.Damage, Value = 8, TargetRule = TargetRule.Single } };
        });

        CreateAndSet<SkillData>($"{basePath}/Skills/sk_002_治疗.asset", c =>
        {
            c.SkillId = "sk_002"; c.Name = "治疗";
            c.Trigger = TriggerType.Active; c.CostActionPoints = 1;
            c.Effects = new[] { new EffectData { EffectType = EffectType.Heal, Value = 6, TargetRule = TargetRule.Self } };
        });

        CreateAndSet<SkillData>($"{basePath}/Skills/sk_003_鼓舞.asset", c =>
        {
            c.SkillId = "sk_003"; c.Name = "鼓舞";
            c.Trigger = TriggerType.TurnStart; c.CostActionPoints = 0;
            c.Effects = new[] { new EffectData { EffectType = EffectType.AddAttack, Value = 2, TargetRule = TargetRule.AdjacentAlly } };
        });

        // --- 状态 ---
        CreateAndSet<StatusData>($"{basePath}/Statuses/st_tipsy_微醺.asset", c =>
        {
            c.StatusId = "status_tipsy"; c.Name = "微醺"; c.Duration = 1;
            c.OnExpireStatusId = "status_hungover";
            c.Effects = new[] { new EffectData { EffectType = EffectType.AddAttack, Value = 5, TargetRule = TargetRule.Self } };
        });

        CreateAndSet<StatusData>($"{basePath}/Statuses/st_hungover_醉醺.asset", c =>
        {
            c.StatusId = "status_hungover"; c.Name = "醉醺"; c.Duration = 1;
            c.Effects = new[] { new EffectData { EffectType = EffectType.SubAttack, Value = 6, TargetRule = TargetRule.Self } };
        });

        // ============================================
        // 第三批：CharacterCardData / ItemCardData
        // ============================================

        string[] charIds = { "char_001", "char_002", "char_003", "char_004" };
        string[] names = { "战士", "猎人", "医师", "村长" };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            CreateAndSet<CharacterCardData>($"{basePath}/Cards/ccard_00{idx+1}_{names[idx]}卡.asset", c =>
            {
                c.CardId = $"ccard_00{idx+1}";
                c.CharacterId = charIds[idx];
            });
        }

        CreateAndSet<ItemCardData>($"{basePath}/Cards/icard_001_治疗药水.asset", c =>
        {
            c.CardId = "icard_001"; c.DisplayName = "治疗药水"; c.CostTP = 1;
            c.Effects = new[] { new EffectData { EffectType = EffectType.Heal, Value = 8, TargetRule = TargetRule.Self } };
        });

        CreateAndSet<ItemCardData>($"{basePath}/Cards/icard_002_炸药.asset", c =>
        {
            c.CardId = "icard_002"; c.DisplayName = "炸药"; c.CostTP = 2;
            c.Effects = new[] { new EffectData { EffectType = EffectType.Damage, Value = 12, TargetRule = TargetRule.Single } };
        });

        CreateAndSet<ItemCardData>($"{basePath}/Cards/icard_003_酒.asset", c =>
        {
            c.CardId = "icard_003"; c.DisplayName = "酒"; c.CostTP = 1;
            c.Effects = new[] { new EffectData { EffectType = EffectType.AddBuff, Value = 0, TargetRule = TargetRule.Self, StatusId = "status_tipsy" } };
        });

        // ============================================
        // Core
        // ============================================
        CreateAndSet<InjuryProbabilityConfig>($"{basePath}/Core/InjuryProbabilityConfig.asset", c =>
        {
            c.LightInjuryMax = 20; c.HeavyInjuryMax = 60; c.DisabledMin = 61;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("✅ 游戏数据初始化完成！");
    }

    /// <summary>
    /// 创建 SO 并设置字段
    /// ScriptableObject 必须通过 CreateInstance 创建，不能用 new
    /// </summary>
    static void CreateAndSet<T>(string path, System.Action<T> setFields) where T : ScriptableObject
    {
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        var asset = ScriptableObject.CreateInstance<T>();
        setFields(asset);
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
    }
}
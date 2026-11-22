using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 已退休的 Stage1 初始化入口兼容壳。当前场景与 Prefab 已由产品作者化链路维护；
/// 保留旧 public 方法名供历史脚本明确失败，禁止重新安装原型 HUD 或覆盖现有绑定。
/// </summary>
public static class BattleStage1Editor
{
    public static void CreateDefaultBattleCardAssets() => RejectRetiredEntry(nameof(CreateDefaultBattleCardAssets));
    public static void IntegrateV1Runtime() => RejectRetiredEntry(nameof(IntegrateV1Runtime));
    public static void BatchIntegrateV1() => RejectRetiredEntry(nameof(BatchIntegrateV1));

    internal static void RejectRetiredEntry(string entry)
    {
        // 显式失败，避免旧批处理把空操作当作初始化成功；不退出交互式 Editor。
        throw new NotSupportedException("[Battle Authoring] " + entry +
            " 已退休：旧 Stage1/2/3A 初始化会覆盖当前场景、Prefab 或演示牌内容。" +
            "请使用 RRPG/Battle Authoring/检查当前战斗绑定（只读），按当前 GameSpec 与作者化工具维护具体资产。");
    }

    [MenuItem("RRPG/Battle Authoring/检查当前战斗绑定（只读）")]
    private static void AuditCurrentBindingsMenu() => Debug.Log(AuditCurrentBindings());

    /// <summary>只审计当前活动的正式场景，不打开、保存、创建或自动修复对象，不调用业务 getter。</summary>
    public static string AuditCurrentBindings()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.scene")
            return "[Battle Authoring] 请先打开 SampleScene；审计未切换场景。";

        var views = new List<BattleUI>();
        foreach (var root in scene.GetRootGameObjects())
            views.AddRange(root.GetComponentsInChildren<BattleUI>(true));
        if (views.Count != 1)
            return "[Battle Authoring] BattleUI 数量应为 1，实际为 " + views.Count + "；未修改对象。";

        var issues = new List<string>();
        var ui = new SerializedObject(views[0]);
        var hand = ReadReference(ui, "handContainer", issues);
        var prep = ReadReference(ui, "prepCardContainer", issues);
        var runtime = ReadReference(ui, "v1Runtime", issues) as BattleV1Runtime;
        if (hand != null && hand == prep) issues.Add("战前人物牌与战中行动牌使用了同一容器");
        if (runtime != null)
        {
            var binding = new SerializedObject(runtime);
            var runtimeHand = ReadReference(binding, "handContainer", issues);
            ReadReference(binding, "battleCardPrefab", issues);
            ReadReference(binding, "txtSharedActionPoint", issues);
            if (runtime.gameObject != views[0].gameObject) issues.Add("BattleV1Runtime 未挂在 BattleUI 宿主上");
            if (runtimeHand != hand) issues.Add("BattleUI 与 BattleV1Runtime 的行动牌容器不一致");
        }
        return issues.Count == 0
            ? "[Battle Authoring] 当前战斗关键绑定检查通过（只读，非完整引用或运行验证）。"
            : "[Battle Authoring] 当前战斗绑定待处理（未修改）：\n- " + string.Join("\n- ", issues);
    }

    private static UnityEngine.Object ReadReference(SerializedObject owner, string name, List<string> issues)
    {
        var property = owner.FindProperty(name);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            issues.Add(owner.targetObject.GetType().Name + "." + name + " 字段不存在或类型不匹配");
            return null;
        }
        var value = property.objectReferenceValue;
        if (value == null) issues.Add(owner.targetObject.GetType().Name + "." + name + " 未绑定或引用失效");
        return value;
    }
}



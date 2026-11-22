using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Presentation authoring only. Draws existing serialized values without changing Scene bindings.</summary>
[CustomEditor(typeof(BattleUI)), CanEditMultipleObjects]
public sealed class BattleAuthoringInspector : Editor
{
    private bool _bindings;
    private bool _details;
    private bool _diagnostics;
    private static readonly HashSet<string> Main = new HashSet<string>
    {
        "unitCardScale", "monsterCardScale", "prepCardScale", "prepHandCenterOffset", "prepHandBaseStep", "prepHandMinStep",
        "singleUnitVisualOffset", "twoUnitVisualSpacing", "threeUnitVisualSpacing", "rowLocalVisualOffset", "dragPreviewOffset",
        "feedbackEnabled", "feedbackProfile", "feedbackPlayer", "actionPointPresenter"
    };
    private static readonly HashSet<string> Bindings = new HashSet<string>
    {
        "unitCardPrefab", "prepCardPrefab", "uiFont", "playerFrontRow", "playerMiddleRow", "playerBackRow",
        "enemyFrontRow", "enemyMiddleRow", "enemyBackRow", "handContainer", "btnCancelOp", "btnStartBattle", "btnEndTurn", "v1Runtime"
    };
    private static readonly HashSet<string> Hidden = new HashSet<string>
    {
        "m_Script", "itemCardRoot", "btnAttackOp", "btnSkillOp", "btnGuardOp", "_deployedUnitsDebug", "infoText",
        "rowVisualTintEnabled", "playerRowVisualColor", "enemyRowVisualColor"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("BattleUI：人物单位、Prep 人物手牌、Formation。正式 Battle 行动牌大小与间距在同对象的 BattleV1Runtime。", MessageType.Info);
        Group("Unit Presentation");
        Field("unitCardScale", "Player Unit Scale");
        Field("monsterCardScale", "Monster Unit Scale");
        Group("Prep Character Hand — 相对 BattleCanvas 中心");
        Field("prepCardScale", "Prep Card Scale");
        Field("prepHandCenterOffset", "Prep Hand Center Offset");
        Field("prepHandBaseStep", "Prep Hand Base Step");
        Field("prepHandMinStep", "Prep Hand Min Step");
        Group("Formation Presentation");
        Field("singleUnitVisualOffset", "Single Unit Offset");
        Field("twoUnitVisualSpacing", "Two Unit Spacing");
        Field("threeUnitVisualSpacing", "Three Unit Spacing");
        Field("rowLocalVisualOffset", "Row Local Offset");
        Field("dragPreviewOffset", "Drag Preview Offset");
        Group("Combat Feedback");
        Field("feedbackEnabled", "Feedback Enabled");
        Field("feedbackProfile", "Feedback Profile");
        Field("feedbackPlayer", "Sequence Player (Optional Runtime Host)");
        Field("actionPointPresenter", "AP Value Presenter (Optional Override)");
        _details = EditorGUILayout.Foldout(_details, "Advanced Presentation — Body / Background / Intent", true);
        if (_details)
        {
            var p = serializedObject.GetIterator();
            bool enter = true;
            while (p.NextVisible(enter))
            {
                enter = false;
                if (!Main.Contains(p.name) && !Bindings.Contains(p.name) && !Hidden.Contains(p.name))
                    EditorGUILayout.PropertyField(p, true);
            }
        }
        _bindings = EditorGUILayout.Foldout(_bindings, "Internal — Scene / Prefab Bindings", true);
        if (_bindings)
        {
            foreach (var name in Bindings) Field(name);
        }
        _diagnostics = EditorGUILayout.Foldout(_diagnostics, "Debug / Diagnostics", true);
        if (_diagnostics)
        {
            Field("infoText");
            Field("rowVisualTintEnabled");
            Field("playerRowVisualColor");
            Field("enemyRowVisualColor");
            using (new EditorGUI.DisabledScope(true)) Field("_deployedUnitsDebug");
        }
        serializedObject.ApplyModifiedProperties();
    }

    private static void Group(string name) { EditorGUILayout.Space(); EditorGUILayout.LabelField(name, EditorStyles.boldLabel); }
    private void Field(string name, string label = null)
    {
        var p = serializedObject.FindProperty(name);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? p.displayName, p.tooltip), true);
    }
}

[CustomEditor(typeof(BattleV1Runtime)), CanEditMultipleObjects]
public sealed class BattleActionHandAuthoringInspector : Editor
{
    private bool _bindings;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("正式 Battle Action Cards Only。Minimum Scale 是 Base Scale 的比例。关闭 Scale By Count 时最终缩放等于 Base Scale。", MessageType.Info);
        Field("battleCardBaseScale", "Battle Card Base Scale");
        Field("handBaseStep", "Hand Base Step");
        Field("handMinStep", "Hand Min Step");
        Field("handCenterOffset", "Hand Center Offset");
        Field("handScaleByCount", "Scale By Count");
        Field("handMinimumScale", "Minimum Scale (Ratio)");
        _bindings = EditorGUILayout.Foldout(_bindings, "Internal — Bindings / Existing Rule Configuration", true);
        if (_bindings)
            foreach (var name in new[] { "handContainer", "battleCardPrefab", "txtSharedActionPoint", "maxActionPoints", "baseDrawCount" }) Field(name);
        serializedObject.ApplyModifiedProperties();
    }
    private void Field(string name, string label = null)
    {
        var p = serializedObject.FindProperty(name);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? p.displayName, p.tooltip), true);
    }
}

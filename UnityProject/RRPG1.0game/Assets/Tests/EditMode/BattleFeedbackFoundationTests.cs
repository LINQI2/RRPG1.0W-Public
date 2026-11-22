using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>长期回归：捕获真实 Authority Delta、刷新批次与可替换数值显示契约。</summary>
namespace RRPG.Feedback.Tests
{
public class BattleFeedbackFoundationTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static object Get(object o, string n) => o.GetType().GetField(n, F)?.GetValue(o) ?? o.GetType().GetProperty(n, F).GetValue(o);
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    private static object E(string n, int i) => Enum.ToObject(T(n), i);
    private object Unit(string id, bool enemy, int hp = 20)
    {
        return Activator.CreateInstance(T("BattleUnit"), id, id, E("UnitType", enemy ? 2 : 0), hp, 10, 1, 6, 1, enemy ? "enemy" : "player");
    }
    private object System(out object p, out object e)
    {
        var config = ScriptableObject.CreateInstance(T("InjuryProbabilityConfig")); _owned.Add(config);
        var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(T("CharacterInstance")));
        var system = Activator.CreateInstance(T("BattleSystem"), list, null, config);
        var data = Call(system, "GetBattleData");
        p = Unit("p", false, 100); e = Unit("e", true);
        ((IList)Get(data, "PlayerUnits")).Add(p); ((IList)Get(data, "EnemyUnits")).Add(e);
        Call(Get(data, "PlayerField"), "Deploy", p, E("BattleRow", 0), E("BattleColumn", 1));
        Call(Get(data, "EnemyField"), "Deploy", e, E("BattleRow", 0), E("BattleColumn", 1));
        return system;
    }
    private IBattleRuleContext Context(object system) => (IBattleRuleContext)Activator.CreateInstance(T("BattleRuleContextRuntime"), system, null, null);
    private BattleCard Card(BattleRuleOperation op, int value, string owner = "p")
    {
        var program = new BattleCardRuleProgram { Trigger = BattleRuleTrigger.OnPlay };
        program.Steps.Add(new BattleCardRuleStep { Operation = op, TargetSource = BattleRuleTargetSource.ManualTarget,
            ValueSource = BattleRuleValueSource.Fixed, ValueParam = value });
        return new BattleCard { CardId = "instance", TemplateId = "template", OwnerPersonId = owner, RuleProgram = program };
    }
    private BattleRuleStepOutcome Execute(object system, BattleCard card, BattleRuleTargetRef target)
    {
        var context = Context(system);
        return BattleCardRuleExecutor.Execute(BattleCardRuleResolver.BuildPlan(card, context, target), context).Outcomes[0];
    }
    [TearDown] public void Cleanup() { foreach (var o in _owned) if(o != null) UnityEngine.Object.DestroyImmediate(o); _owned.Clear(); }

    [TestCase(0, false, 10, 10, 0)]
    [TestCase(3, false, 10, 13, 0)]
    [TestCase(3, true, 10, 18, 0)]
    [TestCase(0, false, 50, 0, 0)]
    public void Damage_CapturesAuthoritativeShieldDefendClamp(int shield, bool defend, int requested, int hpAfter, int shieldAfter)
    {
        object p,e; var system = System(out p,out e); Set(e,"Shield",shield);
        if(defend) Call(system,"ExecuteEnemyIntent",e,new EnemyIntent { ActionType=EnemyActionType.Defend });
        var outcome=Execute(system,Card(BattleRuleOperation.Damage,requested),BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts { UnitId="e" }));
        Assert.AreEqual(requested,outcome.Value);
        Assert.AreEqual("e",outcome.TargetRef.UnitId);
        var delta=outcome.FactChanges[0]; Assert.AreEqual(20,delta.Before.HP); Assert.AreEqual(shield,delta.Before.Shield);
        Assert.AreEqual(hpAfter,delta.After.HP); Assert.AreEqual(shieldAfter,delta.After.Shield);
        if(hpAfter==0) { Assert.IsFalse(delta.Before.IsDown); Assert.IsTrue(delta.After.IsDown); Assert.AreEqual(true,Get(e,"IsDown")); Assert.IsNull(Call(Get(Call(system,"GetBattleData"),"EnemyField"),"GetUnitAt",E("BattleRow",0),E("BattleColumn",1))); }
    }
    [Test] public void Overheal_ReportsOnlyActualRestore()
    {
        object p,e;var system=System(out p,out e);Set(p,"CurrentHP",98);
        var delta=Execute(system,Card(BattleRuleOperation.Heal,10),BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts { UnitId="p" })).FactChanges[0];
        Assert.AreEqual(98,delta.Before.HP);Assert.AreEqual(100,delta.After.HP);
    }
    [Test] public void Shield_ReportsActualIncrease()
    {
        object p,e;var system=System(out p,out e);Set(p,"Shield",2);
        var delta=Execute(system,Card(BattleRuleOperation.Shield,7),BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts { UnitId="p" })).FactChanges[0];
        Assert.AreEqual(2,delta.Before.Shield);Assert.AreEqual(9,delta.After.Shield);Assert.AreEqual(delta.Before.HP,delta.After.HP);
    }
    [Test] public void Move_CapturesOwnerCoordinates()
    {
        object p,e;var system=System(out p,out e);
        var delta=Execute(system,Card(BattleRuleOperation.Move,0),BattleRuleTargetRef.ForCell(1,0,null)).FactChanges[0];
        Assert.AreEqual("p",delta.After.UnitId);Assert.AreEqual(0,delta.Before.Row);Assert.AreEqual(1,delta.Before.Column);
        Assert.AreEqual(1,delta.After.Row);Assert.AreEqual(0,delta.After.Column);
    }
    [Test] public void Swap_CapturesBothUnits()
    {
        object p,e;var system=System(out p,out e);var q=Unit("q",false);
        var data=Call(system,"GetBattleData");((IList)Get(data,"PlayerUnits")).Add(q);
        Call(Get(data,"PlayerField"),"Deploy",q,E("BattleRow",1),E("BattleColumn",0));
        var changes=Execute(system,Card(BattleRuleOperation.Swap,0),BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts { UnitId="q" })).FactChanges;
        Assert.AreEqual(2,changes.Count);Assert.AreEqual(1,changes[0].After.Row);Assert.AreEqual(0,changes[1].After.Row);
    }
    [Test] public void EnemyResults_FollowActualExecutionAndTargets()
    {
        object p,e;var system=System(out p,out e);var q=Unit("e2",true);
        var data=Call(system,"GetBattleData");((IList)Get(data,"EnemyUnits")).Add(q);
        Call(Get(data,"EnemyField"),"Deploy",q,E("BattleRow",0),E("BattleColumn",0));
        Call(system,"ExecuteTurnPart2");var actions=(IList)Get(system,"LastEnemyActions");Assert.AreEqual(2,actions.Count);
        Assert.AreEqual("e",Get(actions[0],"EnemyUnitId"));Assert.AreEqual("e2",Get(actions[1],"EnemyUnitId"));
        Assert.AreEqual("p",Get(actions[0],"TargetUnitId"));Assert.AreEqual(10,Get(actions[0],"RequestedValue"));
        Assert.AreEqual(100,((BattleRuleFactSnapshot)Get(actions[0],"Before")).HP);
        Assert.AreEqual(90,((BattleRuleFactSnapshot)Get(actions[1],"Before")).HP);
        Assert.AreEqual(80,((BattleRuleFactSnapshot)Get(actions[1],"After")).HP);
    }
    [Test] public void NestedBatch_DisposeIsIdempotentAndExceptionSafe()
    {
        var go=new GameObject("Batch");_owned.Add(go);var runtime=go.AddComponent(T("BattleV1Runtime"));
        var outer=(IDisposable)Call(runtime,"BeginPresentationBatch");var inner=(IDisposable)Call(runtime,"BeginPresentationBatch");
        Assert.AreEqual(true,Get(runtime,"IsPresentationDeferred"));inner.Dispose();inner.Dispose();Assert.AreEqual(true,Get(runtime,"IsPresentationDeferred"));
        // 终层 Refresh 需要真实宿主，避免无绑定警告。
        var hand=new GameObject("Hand",typeof(RectTransform));_owned.Add(hand);Set(runtime,"handContainer",hand.GetComponent<RectTransform>());
        var prefab=new GameObject("Card",typeof(RectTransform));_owned.Add(prefab);Set(runtime,"battleCardPrefab",prefab);
        try { using(outer) { throw new InvalidOperationException(); } } catch(InvalidOperationException) { }
        Assert.AreEqual(false,Get(runtime,"IsPresentationDeferred"));
    }
    [Test] public void ValuePresenter_LinearRadialAndAnimateHaveSameFinalValue()
    {
        var go=new GameObject("Value",typeof(RectTransform));_owned.Add(go);var presenter=go.AddComponent(T("BattleValuePresenter"));
        var slider=go.AddComponent<Slider>();var image=go.AddComponent<Image>();image.type=Image.Type.Filled;
        Set(presenter,"Linear",slider);Set(presenter,"Radial",image);Call(presenter,"SetImmediate",3f,4f);
        Assert.AreEqual(.75f,slider.normalizedValue);Assert.AreEqual(slider.normalizedValue,image.fillAmount);
        var animate=(IEnumerator)T("BattleValuePresenter").GetMethod("AnimateTo",new[]{typeof(float),typeof(float),typeof(float)}).Invoke(presenter,new object[]{2f,4f,0f});
        while(animate.MoveNext()) { }
        Assert.AreEqual(.5f,slider.normalizedValue);Assert.AreEqual(.5f,image.fillAmount);
    }
    [Test] public void SpeedOnlyChangesDuration_InstantTweenHasNoYield()
    {
        var go=new GameObject("Clock");_owned.Add(go);var player=go.AddComponent(T("BattleFeedbackPlayer"));
        Assert.AreEqual(2f,Call(player,"Duration",2f));Set(player,"PlaybackSpeed",2f);Assert.AreEqual(1f,Call(player,"Duration",2f));
        Set(player,"Instant",true);float final=0;var tween=(IEnumerator)Call(player,"Tween",2f,new Action<float>(v=>final=v));
        Assert.IsFalse(tween.MoveNext());Assert.AreEqual(1f,final);Assert.AreEqual(0f,Call(player,"Duration",2f));
    }
}
}

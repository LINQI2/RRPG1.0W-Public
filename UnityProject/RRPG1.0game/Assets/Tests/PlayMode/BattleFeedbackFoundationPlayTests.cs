using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RRPG.Feedback.Tests
{
/// <summary>真实 MainMenu → Town → Expedition Confirm → Journey → Battle 产品入口回归。</summary>
public class BattleFeedbackFoundationPlayTests
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
    static object Get(object o,string n)
    {
        Assert.IsNotNull(o,"读取宿主为空："+n);var field=o.GetType().GetField(n,F);
        if(field!=null)return field.GetValue(o);var property=o.GetType().GetProperty(n,F);
        Assert.IsNotNull(property,"缺少事实成员："+n);return property.GetValue(o);
    }
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Invoke(object o,MethodInfo method,object[] args)
    {
        var parameters=method.GetParameters();var supplied=new object[parameters.Length];
        for(int i=0;i<supplied.Length;i++)supplied[i]=i<args.Length?args[i]:parameters[i].DefaultValue;
        return method.Invoke(o,supplied);
    }
    static object Call(object o,string n,params object[] args)=>Invoke(o,o.GetType().GetMethod(n,F),args);
    static object Static(string t,string n,params object[] args)=>Invoke(null,T(t).GetMethod(n,F),args);
    static Component Find(string n)=>UnityEngine.Object.FindObjectsOfType(T(n),true).Cast<Component>().First();
    object _ui,_runtime,_system;
    Component _player;
    readonly List<object> _cards=new List<object>();

    IEnumerator Enter()
    {
        Static("VillageRuntimeBridge","ResetForTests");
        SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        var collection=Static("SaveSystem","NewGame",new object[]{null});
        Static("SaveSystem","Save",collection);
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;
        collection=Get(Find("TownUI"),"PlayerData");
        Call(Find("TownUI"),"OnExpedition");yield return null;yield return null;
        var expedition=Find("ExpeditionUI");_ui=Find("BattleUI");
        _cards.Clear();
        foreach(var card in (IEnumerable)Get(collection,"OwnedCharacterCards"))
        {
            Call(expedition,"OnCharacterCardClick",Get(card,"CardId"));
            var selected=(IEnumerable)Get(Get(expedition,"_expedition"),"SelectedCharacters");
            if(selected.Cast<object>().Any(c=>(string)Get(c,"CardId")== (string)Get(card,"CardId")))_cards.Add(card);
            if(_cards.Count==3)break;
        }
        var resources=T("VillageRuntimeBridge").GetField("Resources",F).GetValue(null);
        Call(resources,"AddFood",100);
        Call(expedition,"OnConfirmExpedition");yield return null;
        var adventure=Get(expedition,"_currentAdventure");Assert.IsNotNull(adventure);
        _cards.Clear();_cards.AddRange(((IEnumerable)Call(adventure,"GetPlayerChars")).Cast<object>());
        Assert.IsNotEmpty(_cards,"真实远征必须包含通过可用性检查的人物");
        var progression=Get(adventure,"Progression");object battleNode=null;
        foreach(var node in (IEnumerable)Call(progression,"GetAvailableNextNodes"))
        {
            var kind=Get(node,"NodeKind").ToString();
            if(kind=="Battle"||kind=="Elite"||kind=="Boss"){battleNode=node;break;}
        }
        Assert.IsNotNull(battleNode,"本产品地图需提供首层战斗入口");
        Assert.AreEqual(true,Call(expedition,"HandleNodeClick",adventure,Get(battleNode,"NodeId"),_ui),"产品节点进入被拒绝");yield return null;yield return null;
        Assert.IsTrue((bool)Get(_ui,"_initializedFromExpedition"),"产品节点必须初始化 BattleUI");
        var dataManager=T("DataManager").GetProperty("Instance",F).GetValue(null);
        foreach(var card in _cards)
            Assert.AreEqual(true,Call(_ui,"PlaceUnit",card,Call(dataManager,"GetCharacterById",Get(card,"CharacterId")),Get(_ui,"playerFrontRow")));
        foreach(var selected in (IEnumerable)Get(_ui,"_selectedChars"))
        {
            string id=(string)Get(selected,"CharacterId");
            Assert.IsTrue(((IEnumerable)Get(_ui,"_allCharDatas")).Cast<object>().Any(d=>(string)Get(d,"CharacterId")==id),"产品 Battle 缺少战斗定义："+id);
            Assert.IsTrue(((IDictionary)Get(_ui,"_charInstByCardId")).Contains(Get(selected,"CardId")),"产品 Battle 缺少人物实例："+Get(selected,"CardId"));
        }
        Call(_ui,"OnStartBattle");yield return null;yield return null;
        _runtime=Get(_ui,"v1Runtime");_system=Get(_ui,"_battleSystem");
        float deadline=Time.realtimeSinceStartup+15;
        while((BattleV1TurnFlow.PhaseType)Get(_runtime,"TurnPhase")!=BattleV1TurnFlow.PhaseType.PlayerTurn)
        {Assert.Less(Time.realtimeSinceStartup,deadline,"正式开战必须进入玩家回合");yield return null;}
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn,Get(_runtime,"TurnPhase"));
        _player=(Component)Call(_ui,"EnsureFeedbackPlayer");
        Set(_player,"Instant",false);Set(_player,"PlaybackSpeed",1f);
    }
    IEnumerator Wait()
    {
        float timeout=Time.realtimeSinceStartup+15;
        while((bool)Get(_ui,"IsFeedbackPlaying"))
        {Assert.Less(Time.realtimeSinceStartup,timeout,"反馈必须结束并释放输入/刷新批次");yield return null;}
        yield return null;
    }
    SharedDeck Deck=>(SharedDeck)Get(_runtime,"Deck");
    SharedActionPointPool AP=>(SharedActionPointPool)Get(_runtime,"ActionPoints");
    RectTransform Hand=>(RectTransform)Get(_runtime,"handContainer");
    object Enemy()=>((IEnumerable)Get(Call(_system,"GetBattleData"),"EnemyUnits")).Cast<object>().First(u=>!(bool)Get(u,"IsDown"));
    GameObject UnitView(object unit)=>((GameObject)Call(_ui,"FeedbackUnitView",Get(unit,"UnitId")));
    BattleCard AddCard(bool exhaust=false)
    {
        // 测试固定输入；仍交给正式 UI/牌堆/Authority，不建立第二流程。
        var card=new BattleCard{CardId="feedback_contract",TemplateId="feedback_contract",Cost=1,
            Effect=BattleCardEffectType.Damage,EffectValue=6,TargetScope=BattleCardTargetScope.EnemyUnit,
            IsExhaust=exhaust,DisplayName="Feedback Contract",RangeModifier=6,
            OwnerPersonId=Deck.Hand.Concat(Deck.DrawPile).First(c=>!string.IsNullOrEmpty(c.OwnerPersonId)).OwnerPersonId};
        card.RuleProgram=new BattleCardRuleProgram { Trigger=BattleRuleTrigger.OnPlay,ManualTargetKind=BattleRuleManualTargetKind.EnemyUnit };
        card.RuleProgram.Steps.Add(new BattleCardRuleStep { Operation=BattleRuleOperation.Damage,
            TargetSource=BattleRuleTargetSource.ManualTarget,ValueSource=BattleRuleValueSource.Fixed,ValueParam=6 });
        Deck.Hand.Add(card);Call(_runtime,"RefreshAll");return card;
    }
    void Click(BattleCard card,object enemy)
    {
        Hand.Find("V1Card_"+card.CardId).GetComponent<Button>().onClick.Invoke();
        UnitView(enemy).GetComponent<Button>().onClick.Invoke();
    }

    [UnityTest] public IEnumerator Product_SelectCancelCommit_APDamageDownFormation_EnemyNewIntent()
    {
        yield return Enter();
        var enemy=Enemy();Set(enemy,"CurrentHP",5);Set(enemy,"Shield",3);Call(_ui,"RefreshFieldUI");
        var card=AddCard();yield return null;
        var host=Hand.Find("V1Card_"+card.CardId) as RectTransform;var position=host.anchoredPosition;
        host.GetComponent<Button>().onClick.Invoke();Assert.AreNotEqual(position,host.anchoredPosition);
        host.GetComponent<Button>().onClick.Invoke();Assert.AreEqual(position,host.anchoredPosition);
        int ap=AP.Current;Click(card,enemy);
        Assert.AreEqual(ap-1,AP.Current);Assert.AreEqual(2,Get(enemy,"CurrentHP"));Assert.AreEqual(0,Get(enemy,"Shield"));
        Assert.IsTrue((bool)Get(_ui,"IsFeedbackPlaying"));Assert.IsNotNull(host);
        Assert.IsFalse(Deck.Hand.Contains(card));Assert.IsTrue(Deck.DiscardPile.Contains(card));
        Assert.IsTrue((bool)Get(_runtime,"IsPresentationDeferred"));
        Call(_ui,"OnEndTurn");Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn,Get(_runtime,"TurnPhase"),"Commit 播放时 EndTurn 输入必须被锁定");
        Call(_runtime,"RefreshAll");yield return null;Assert.IsNotNull(host,"Card Commit 期间不能被自动/显式 Refresh 提前销毁");
        yield return Wait();Assert.IsTrue(host==null);
        var records=((IEnumerable)Get(_ui,"LastFeedbackRecords")).Cast<object>().ToList();
        var damage=records.First(r=>Get(r,"Type").ToString()=="Damage");
        Assert.AreEqual(-3,Get(damage,"HPDelta"));Assert.AreEqual(-3,Get(damage,"ShieldDelta"));
        var apRecord=records.First(r=>Get(r,"Type").ToString()=="ActionPoints");
        Assert.AreEqual(ap,Get(apRecord,"APBefore"));Assert.AreEqual(ap-1,Get(apRecord,"APAfter"));
        AP.RestoreToMax();card=AddCard(true);yield return null;Click(card,enemy);
        Assert.AreEqual(true,Get(enemy,"IsDown"));Assert.IsTrue(UnitView(enemy).activeSelf,"Gameplay 已 Down，Cue 尚未结束应保留 View");
        Assert.IsTrue(Deck.ExhaustPile.Contains(card));yield return Wait();Assert.IsFalse(UnitView(enemy).activeSelf);
        var dataAfterDown=Call(_system,"GetBattleData");
        var visible=((IEnumerable)Get(dataAfterDown,"PlayerUnits")).Cast<object>().Concat(((IEnumerable)Get(dataAfterDown,"EnemyUnits")).Cast<object>())
            .Select(UnitView).Where(v=>v!=null && v.activeSelf).ToDictionary(v=>v,v=>v.transform.position);
        Call(_ui,"RepositionAllUnitCards");
        foreach(var view in visible)Assert.AreEqual(view.Value,view.Key.transform.position,"完成后的 Formation 必须已落在原 Authoring 计算终位");
        // 保留其它存活敌人；真实结束回合按实际 EnemyUnits 顺序捕获。
        if(!(bool)Get(Call(_system,"GetBattleData"),"IsBattleOver"))
        {
            int turn=(int)Get(_runtime,"TurnNumber");Call(_ui,"OnEndTurn");
            Assert.AreEqual(turn,Get(_runtime,"TurnNumber"),"敌方 Cue 完成前不能进入新玩家回合");
            var actions=((IEnumerable)Get(_system,"LastEnemyActions")).Cast<object>().ToList();
            var cues=((IEnumerable)Get(_ui,"LastFeedbackRecords")).Cast<object>().Where(r=>Get(r,"Type").ToString()=="EnemyAction").ToList();
            CollectionAssert.AreEqual(actions.Select(a=>Get(a,"EnemyUnitId")).ToArray(),cues.Select(a=>Get(a,"SourceUnitId")).ToArray());
            yield return Wait();yield return null;
            if(!(bool)Get(Call(_system,"GetBattleData"),"IsBattleOver"))
            {Assert.AreEqual(turn+1,Get(_runtime,"TurnNumber"));Assert.Greater((int)Call(_system,"GetLockedEnemyIntentCount"),0);}
        }
    }

    [UnityTest] public IEnumerator EnabledInstantAbsent_IdenticalGameplayAndCardPiles()
    {
        yield return Enter();var enemy=Enemy();var originalDraw=Deck.DrawPile.ToArray();var originalHand=Deck.Hand.ToArray();
        var originalDiscard=Deck.DiscardPile.ToArray();var originalExhaust=Deck.ExhaustPile.ToArray();
        string expected=null;
        for(int mode=0;mode<8;mode++)
        {
            Deck.DrawPile.Clear();Deck.DrawPile.AddRange(originalDraw);Deck.Hand.Clear();Deck.Hand.AddRange(originalHand);
            Deck.DiscardPile.Clear();Deck.DiscardPile.AddRange(originalDiscard);Deck.ExhaustPile.Clear();Deck.ExhaustPile.AddRange(originalExhaust);
            Set(enemy,"CurrentHP",Mathf.Min((int)Get(enemy,"MaxHP"),20));Set(enemy,"Shield",3);
            AP.RestoreToMax();Set(_ui,"feedbackEnabled",mode%4!=2);Set(_player,"Instant",mode%4==1);
            Set(_player,"PlaybackSpeed",mode%4==3?2f:1f);
            var card=AddCard();if(mode>=4)card.RuleProgram=null;
            yield return null;Click(card,enemy);yield return Wait();
            var data=Call(_system,"GetBattleData");
            string state=string.Join("|",((IEnumerable)Get(data,"PlayerUnits")).Cast<object>().Concat(((IEnumerable)Get(data,"EnemyUnits")).Cast<object>())
                .Select(u=>Get(u,"UnitId")+":"+Get(u,"CurrentHP")+":"+Get(u,"Shield")+":"+Get(u,"Row")+":"+Get(u,"Column")+":"+Get(u,"IsDown")))
                +"/"+AP.Current+"/"+string.Join(",",Deck.Hand.Select(c=>c.CardId))+"/"+string.Join(",",Deck.DrawPile.Select(c=>c.CardId))
                +"/"+string.Join(",",Deck.DiscardPile.Select(c=>c.CardId))+"/"+string.Join(",",Deck.ExhaustPile.Select(c=>c.CardId))+"/"+Get(data,"IsBattleOver");
            if(expected==null)expected=state;else Assert.AreEqual(expected,state,"播放模式不应改变 Gameplay State");
        }
    }

    [UnityTest] public IEnumerator SkipAndDisable_ReleaseTransactionAndFinalizeViews()
    {
        yield return Enter();var enemy=Enemy();Set(enemy,"MaxHP",100);Set(enemy,"CurrentHP",100);
        var card=AddCard();yield return null;Click(card,enemy);
        int hp=(int)Get(enemy,"CurrentHP");Call(_player,"SkipCurrent");yield return Wait();
        Assert.AreEqual(hp,Get(enemy,"CurrentHP"));Assert.IsFalse((bool)Get(_runtime,"IsPresentationDeferred"));
        AP.RestoreToMax();card=AddCard();yield return null;Click(card,enemy);
        ((Behaviour)_player).enabled=false;yield return null;
        Assert.IsFalse((bool)Get(_ui,"IsFeedbackPlaying"));Assert.IsFalse((bool)Get(_runtime,"IsPresentationDeferred"));
        Assert.IsTrue(Deck.DiscardPile.Contains(card));
        // 牌主自身在本次结算 Down 后，source 仍必须是 Commit 前的真实单位，人物牌过滤仍即时执行。
        ((Behaviour)_player).enabled=true;Set(_player,"Instant",true);AP.RestoreToMax();card=AddCard();
        card.RuleProgram.ManualTargetKind=BattleRuleManualTargetKind.None;
        card.RuleProgram.Steps[0].TargetSource=BattleRuleTargetSource.CardOwner;card.RuleProgram.Steps[0].ValueParam=1000;
        var caster=Call(_ui,"GetV1CardCaster",card);string source=(string)Get(caster,"UnitId");
        yield return null;Hand.Find("V1Card_"+card.CardId).GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue((bool)Get(caster,"IsDown"),((TMPro.TMP_Text)Get(_ui,"infoText")).text);
        var down=((IEnumerable)Get(_ui,"LastFeedbackRecords")).Cast<object>().First(r=>(bool)Get(r,"BecameDown"));
        Assert.AreEqual(source,Get(down,"SourceUnitId"));
        Assert.IsFalse(Deck.Hand.Concat(Deck.DrawPile).Concat(Deck.DiscardPile).Concat(Deck.ExhaustPile).Any(c=>c.OwnerPersonId==card.OwnerPersonId));
    }

    [UnityTest] public IEnumerator Enemy_NormalInstantAbsent2x_IdenticalFactsAndFinalState()
    {
        yield return Enter();
        // 固定在同一正式战场，暂停宿主的下一回合推进，逐次比较相同 EnemyTurn 输入。
        ((MonoBehaviour)_ui).StopAllCoroutines();
        var data=Call(_system,"GetBattleData");
        var allies=((IEnumerable)Get(data,"PlayerUnits")).Cast<object>().ToList();
        var enemies=((IEnumerable)Get(data,"EnemyUnits")).Cast<object>().ToList();
        var draw=Deck.DrawPile.ToArray();var hand=Deck.Hand.ToArray();
        var discard=Deck.DiscardPile.ToArray();var exhaust=Deck.ExhaustPile.ToArray();
        string expected=null;
        for(int mode=0;mode<4;mode++)
        {
            foreach(var unit in allies.Concat(enemies))
            {
                Set(unit,"MaxHP",100);Set(unit,"CurrentHP",100);Set(unit,"IsDown",false);
                Set(unit,"Shield",allies.Contains(unit)?3:0);Set(unit,"Attack",4);Set(unit,"AttackRange",6);
                ((IList)Get(unit,"StatusList")).Clear();
            }
            var intents=(IDictionary)Get(_system,"_lockedEnemyIntents");intents.Clear();
            foreach(var enemy in enemies)intents.Add(Get(enemy,"UnitId"),new EnemyIntent { ActionType=EnemyActionType.Attack,DamageMultiplier=1 });
            Deck.DrawPile.Clear();Deck.DrawPile.AddRange(draw);Deck.Hand.Clear();Deck.Hand.AddRange(hand);
            Deck.DiscardPile.Clear();Deck.DiscardPile.AddRange(discard);Deck.ExhaustPile.Clear();Deck.ExhaustPile.AddRange(exhaust);
            Get(_runtime,"_turnFlow").GetType().GetProperty("Phase",F).SetValue(Get(_runtime,"_turnFlow"),BattleV1TurnFlow.PhaseType.PlayerTurn);
            AP.RestoreToMax();Set(_ui,"feedbackEnabled",mode!=2);Set(_player,"Instant",mode==1);Set(_player,"PlaybackSpeed",mode==3?2f:1f);
            Call(_runtime,"RefreshAll");Call(_ui,"RefreshFieldUI");
            Call(_ui,"ExecuteEnemyWithFeedback");yield return Wait();
            var actions=((IEnumerable)Get(_system,"LastEnemyActions")).Cast<object>().ToList();
            Assert.AreEqual(enemies.Count,actions.Count);
            string state=string.Join("|",allies.Concat(enemies).Select(u=>Get(u,"UnitId")+":"+Get(u,"CurrentHP")+":"+Get(u,"Shield")+":"+Get(u,"Row")+":"+Get(u,"Column")+":"+Get(u,"IsDown")))
                +"/"+AP.Current+"/"+Get(data,"IsBattleOver")+"/"+string.Join(",",Deck.Hand.Select(c=>c.CardId))+"/"+string.Join(",",Deck.DrawPile.Select(c=>c.CardId))
                +"/"+string.Join(",",Deck.DiscardPile.Select(c=>c.CardId))+"/"+string.Join(",",Deck.ExhaustPile.Select(c=>c.CardId))
                +"/"+string.Join("|",actions.Select(a=>Get(a,"EnemyUnitId")+":"+Get(a,"TargetUnitId")+":"+Get(a,"RequestedValue")+":"+((BattleRuleFactSnapshot)Get(a,"Before")).HP+":"+((BattleRuleFactSnapshot)Get(a,"After")).HP));
            if(expected==null)expected=state;else Assert.AreEqual(expected,state,"Enemy Playback 只能改变时长，不能改变实际结果/顺序/目标/牌堆");
        }
    }
}
}

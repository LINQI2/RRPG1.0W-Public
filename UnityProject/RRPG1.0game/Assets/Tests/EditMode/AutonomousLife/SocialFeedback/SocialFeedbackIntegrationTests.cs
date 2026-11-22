using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;
using RRPG.AutonomousSocialFeedback;
using RRPG.Social;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEngine;

namespace RRPG.Tests.SocialFeedback
{
    /// <summary>Real product adapters with isolated world/save state; no scene objects or producer assets.</summary>
    public partial class SocialFeedbackIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static Type TypeOf(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(string type, string method, params object[] args) => TypeOf(type).GetMethods(Flags)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length
                && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).Invoke(null, args);
        private static object Get(string type, string member) => TypeOf(type).GetField(member, Flags)?.GetValue(null) ?? TypeOf(type).GetProperty(member, Flags)?.GetValue(null);
        private static void Set(string type, string member, object value) => TypeOf(type).GetField(member, Flags).SetValue(null, value);
        private static object Player() => Activator.CreateInstance(TypeOf("PlayerCollection"));
        private static object AddPerson(object player, string id, string definition, string injury = "Normal")
        {
            var person = Activator.CreateInstance(TypeOf("CharacterInstance"), "char_001", "Test Person", 30, 6);
            TypeOf("CharacterInstance").GetField("PersonId", Flags).SetValue(person, id);
            TypeOf("CharacterInstance").GetField("PersonDefinitionId", Flags).SetValue(person, definition);
            var f = TypeOf("CharacterInstance").GetField("Injury", Flags);
            var p = TypeOf("CharacterInstance").GetProperty("Injury", Flags);
            var t = f?.FieldType ?? p.PropertyType;
            if (f != null) f.SetValue(person, Enum.Parse(t, injury)); else p.SetValue(person, Enum.Parse(t, injury));
            ((IList)(TypeOf("PlayerCollection").GetField("OwnedCharacters", Flags)?.GetValue(player) ?? TypeOf("PlayerCollection").GetProperty("OwnedCharacters", Flags).GetValue(player))).Add(person);
            return person;
        }
        private static SocialWorldSaveData SocialSave() => (SocialWorldSaveData)Call("SocialRuntimeBridge", "BuildSaveData");
        private static SocialFeedbackWorldSaveData FeedbackSave() => (SocialFeedbackWorldSaveData)Call("AutonomousSocialFeedbackRuntimeBridge", "BuildSaveData");
        private static float Affinity(string source, string target) => ((DirectedRelationshipState)Call("SocialRuntimeBridge", "QueryRelationship", source, target)).Affinity;
        private static ActivityDailyWindow Window => (ActivityDailyWindow)Call("ActivityRuntimeBridge", "QueryCurrentWindow");
        private static ActivityResult Complete(string id)
        {
            var args = new object[] { id, null };
            return (ActivityResult)TypeOf("AutonomousSocialFeedbackRuntimeBridge").GetMethod("TryComplete", Flags).Invoke(null, args);
        }
        private static void Start(string id = "visit", string type = "VisitSick", string target = "wang")
        {
            Call("ActivityRuntimeBridge", "OpenDailyWindow", new ActivityDailyWindow(new ActivityDayStamp(1,1,2), new[] { "zhang" }, new[] { "zhang", "wang" }));
            Assert.AreEqual(ActivityResult.Success, Call("ActivityRuntimeBridge", "TryCommitActivity", id, new ActivityCandidate(id, type, "test", "zhang", target)));
            Assert.AreEqual(ActivityResult.Success, Call("ActivityRuntimeBridge", "TryBeginExecution", id));
        }
        private static SocialFeedbackWorldSaveData Pending(string id = "activity:pending", string status = "Pending", string fault = "") => new SocialFeedbackWorldSaveData
        {
            Outbox = new[] { new SocialFeedbackEntrySaveData { SourceOccurrenceId=id,FactType="relational.visited_sick_person",ActorPersonId="zhang",TargetPersonId="wang",Year=1,Month=1,Day=1,Status=status,FaultCode=fault } }
        };
        private readonly Dictionary<string, object> original = new Dictionary<string, object>();
        private object source, hook;
        private string savePath;
        private byte[] save;
        private static readonly string[] Worlds = { "SocialRuntimeBridge", "ActivityRuntimeBridge", "AutonomousSelectionRuntimeBridge", "AutonomousSocialFeedbackRuntimeBridge", "EventRuntimeBridge" };
        [SetUp] public void Isolate()
        {
            original.Clear(); foreach (var name in Worlds) original[name] = Call(name, "BuildSaveData");
            original["VillageRuntimeBridge"] = Call("VillageRuntimeBridge", "BuildSaveData");
            source = Get("VillageRuntimeBridge", "SimulationPersonSource"); hook = Get("SaveSystem", "ForceSaveFailureForTests");
            savePath = (string)Call("SaveSystem", "GetSavePath"); save = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
            Set("SaveSystem", "ForceSaveFailureForTests", null);
            foreach (var name in Worlds.Where(n => n != "EventRuntimeBridge")) Call(name, "ResetForTests");
            Call("VillageRuntimeBridge", "ResetForTests"); Call("VillageRuntimeBridge", "EnsureInitialized", new object[] { null });
            Call("VillageRuntimeBridge", "SetSimulationPersonSource", Player());
            StaticConfigRegistry.Reset();
        }
        [TearDown] public void Restore()
        {
            try
            {
                foreach (var name in Worlds.Where(n => n != "EventRuntimeBridge")) Call(name, "ResetForTests");
                Call("VillageRuntimeBridge", "ResetForTests"); Call("VillageRuntimeBridge", "EnsureInitialized", original["VillageRuntimeBridge"]);
                foreach (var name in Worlds) Call(name, "Restore", original[name]);
                Call("VillageRuntimeBridge", "SetSimulationPersonSource", source); Set("SaveSystem", "ForceSaveFailureForTests", hook);
                StaticConfigRegistry.Reset();
            }
            finally { if (save != null) File.WriteAllBytes(savePath, save); else if (File.Exists(savePath)) File.Delete(savePath); }
        }
        [Test] public void B_ProductionEntryCannotChooseInterpreter()
        {
            var methods = TypeOf("SocialRuntimeBridge").GetMethods(Flags).Where(m => m.Name == "CommitRelationalFact").ToArray();
            Assert.AreEqual(1, methods.Length); Assert.AreEqual(1, methods[0].GetParameters().Length);
            Assert.AreEqual(SocialCommitResult.Success, Call("SocialRuntimeBridge", "CommitRelationalFact", SocialFeedbackDomainTests.Fact()));
            Assert.AreEqual(8, Affinity("wang", "zhang")); Assert.AreEqual(0, Affinity("zhang", "wang"));
        }
        [Test] public void B_GeneratedChainBuilderRepeatNoOp()
        {
            Assert.IsTrue(StaticConfigRegistry.TryGetSocialConfig(out var social, out var diagnostic), diagnostic);
            Assert.AreEqual(2, social.RelationalFactType.Count); Assert.AreEqual(2, social.RelationshipInterpretationRule.Count);
            Assert.IsTrue(StaticConfigRegistry.TryGetAutonomousLifeConfig(out var life, out diagnostic), diagnostic);
            Assert.AreEqual(2, life.ActivitySocialProjection.Count);
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success); Assert.IsTrue(StaticConfigBuilder.Build(false).NoOp);
        }
        [TestCase("Rest")] [TestCase("Drink")] [TestCase("Talk")]
        public void E_NoFeedbackCompletesWithoutCanonicalFact(string type) { Start(type:type,target:null);Assert.AreEqual(ActivityResult.Success,Complete("visit"));Assert.IsEmpty(SocialSave().RelationalFacts);Assert.IsEmpty(FeedbackSave().Outbox); }
        [TestCase("VisitSick")] [TestCase("Help")]
        public void E_CompleteEnqueueDrainAndDuplicateNoDoubleConsumption(string type)
        {
            Start(type:type);Assert.AreEqual(ActivityResult.Success,Complete("visit"));Assert.AreEqual(1,SocialSave().RelationalFacts.Length);
            Assert.AreEqual("activity:visit",SocialSave().RelationalFacts[0].SourceOccurrenceId);Assert.AreEqual(8,Affinity("wang","zhang"));
            Assert.AreEqual(ActivityResult.Duplicate,Complete("visit"));Assert.AreEqual(8,Affinity("wang","zhang"));Assert.IsEmpty(FeedbackSave().Outbox);
        }
        [Test] public void E_InvalidTargetPrepareDoesNotComplete() {Start(target:null);Assert.AreEqual(ActivityResult.InvalidCandidate,Complete("visit"));Assert.AreEqual(ActivityLifecycleState.Executing,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","visit")).State);Assert.IsEmpty(SocialSave().RelationalFacts);Assert.IsEmpty(FeedbackSave().Outbox);}
        [Test] public void E_NoMajorDoesNotProduceFact()
        {
            var player = Player(); AddPerson(player, "zhang", "prototype.zhang_san");
            Call("VillageRuntimeBridge", "SetSimulationPersonSource", player);
            Set("AutonomousSelectionRuntimeBridge", "PlannerForTests", new DecisionPlanner(new EmptyProvider()));
            ((VillageTime)Get("VillageRuntimeBridge", "Time")).TickDay();
            var result = (DecisionRoundResult)Get("AutonomousSelectionRuntimeBridge", "LastResult");
            Assert.AreEqual(DecisionDailyResult.Resolved, result.Result); Assert.AreEqual(1, result.Plan.Trace.Count);
            Assert.IsTrue(result.Plan.Trace[0].Order.Single().IsNoMajor); Assert.IsEmpty(result.Executions);
            Assert.IsEmpty(SocialSave().RelationalFacts); Assert.IsEmpty(FeedbackSave().Outbox);
        }
        private sealed class EmptyProvider : IDecisionCandidateProvider
        {
            public IEnumerable<ActivityCandidate> Collect(DecisionSnapshot snapshot, string actor) => Enumerable.Empty<ActivityCandidate>();
        }
        [Test] public void E_HeadlessThirtyDaysUsesActualSelectionLifecycleFeedbackChain()
        {
            var player = Player(); AddPerson(player, "zhang", "prototype.zhang_san"); AddPerson(player, "wang", "prototype.wang_wu", "HeavyInjury");
            Call("VillageRuntimeBridge", "SetSimulationPersonSource", player);
            var time = (VillageTime)Get("VillageRuntimeBridge", "Time"); int visits = 0;
            for (int day = 0; day < 30; day++)
            {
                time.TickDay();
                while(time.HasActiveAdvance) Assert.AreEqual(WorldAdvanceOutcome.Completed,time.ResumeAdvance(time.AdvanceSession.RequestId,10000).Outcome);
                var result = (DecisionRoundResult)Get("AutonomousSelectionRuntimeBridge", "LastResult");
                Assert.AreEqual(DecisionDailyResult.Resolved, result.Result);
                foreach (var execution in result.Executions)
                {
                    Assert.AreEqual(ActivityResult.Success, execution.Result);
                    Assert.AreEqual("Commit",execution.Stage);Assert.IsNull(execution.Completion,"Selection 只安排，不伪造即时完成");
                }
                var facts=SocialSave().RelationalFacts;
                foreach(var fact in facts.Skip(visits))
                {
                    Assert.AreEqual("relational.visited_sick_person",fact.FactType);
                    StringAssert.StartsWith("activity:village.intraday|",fact.SourceOccurrenceId);
                    Assert.AreEqual("zhang",fact.ActorPersonId);Assert.AreEqual("wang",fact.TargetPersonId);
                }
                Assert.GreaterOrEqual(facts.Length,visits);visits=facts.Length;
                Assert.AreEqual(visits,facts.Select(f=>f.SourceOccurrenceId).Distinct(StringComparer.Ordinal).Count());
                Assert.IsEmpty(FeedbackSave().Outbox);
            }
            Assert.Greater(visits, 0); Assert.AreEqual(visits, SocialSave().RelationalFacts.Length);
            Assert.AreEqual(8 * visits, Affinity("wang", "zhang")); Assert.AreEqual(0, Affinity("zhang", "wang"));
        }
        [Test] public void E_InvalidConfiguredMappingDoesNotComplete()
        {
            Start();Assert.IsTrue(StaticConfigRegistry.TryGetAutonomousLifeConfig(out var db,out _));var row=db.ActivitySocialProjection.First(r=>r.ActivityType=="VisitSick");var before=row.PersistentRelationalFactType;
            try {row.PersistentRelationalFactType="missing";Assert.AreEqual(ActivityResult.InvalidCandidate,Complete("visit"));Assert.AreEqual(ActivityLifecycleState.Executing,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","visit")).State);}
            finally {row.PersistentRelationalFactType=before;}
        }
        [Test] public void E_DrainConflictKeepsActivityCompletedAndFaultedOutbox()
        {
            Assert.AreEqual(SocialCommitResult.Success,Call("SocialRuntimeBridge","CommitRelationalFact",SocialFeedbackDomainTests.Fact("activity:visit","relational.helped_person")));
            Start();Assert.AreEqual(ActivityResult.Success,Complete("visit"));Assert.AreEqual(ActivityLifecycleState.Completed,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","visit")).State);
            Assert.AreEqual("Faulted",FeedbackSave().Outbox[0].Status);Assert.AreEqual("SourceOccurrenceConflict",FeedbackSave().Outbox[0].FaultCode);Assert.AreEqual(1,SocialSave().RelationalFacts.Length);
            Assert.IsFalse(((FeedbackDrainResult)Call("AutonomousSocialFeedbackRuntimeBridge","DrainBarrier")).Settled);
        }
        [TestCase(false)] [TestCase(true)] public void E_FailedInterruptedNoFact(bool interrupted)
        {Start();Call("ActivityRuntimeBridge",interrupted?"TryInterrupt":"TryFail","visit");Assert.AreEqual(ActivityResult.IllegalTransition,Complete("visit"));Assert.IsEmpty(SocialSave().RelationalFacts);}
        [Test] public void D_RealSaveLoadPendingFaultedNoReplayVersionOne()
        {
            Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());Assert.IsTrue((bool)Call("SaveSystem","SaveWithVillage",Player(),null));
            StringAssert.Contains("\"SaveVersion\": 1",File.ReadAllText(savePath));Call("AutonomousSocialFeedbackRuntimeBridge","ResetForTests");
            Assert.IsNotNull(Call("SaveSystem","Load"));Assert.AreEqual("Pending",FeedbackSave().Outbox[0].Status);Assert.IsEmpty(SocialSave().RelationalFacts);Assert.IsNull(Window);
            Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending(status:"Faulted",fault:"InterpreterFailure"));Assert.IsTrue((bool)Call("SaveSystem","SaveWithVillage",Player(),null));
            Call("AutonomousSocialFeedbackRuntimeBridge","ResetForTests");Assert.IsNotNull(Call("SaveSystem","Load"));Assert.AreEqual("Faulted",FeedbackSave().Outbox[0].Status);
        }
        [Test] public void D_WorldAtoBReplacement()
        {Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending("activity:B"));Assert.IsTrue((bool)Call("SaveSystem","SaveWithVillage",Player(),null));Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending("activity:A"));Assert.IsNotNull(Call("SaveSystem","Load"));Assert.AreEqual("activity:B",FeedbackSave().Outbox.Single().SourceOccurrenceId);}
        [Test] public void D_InvalidLoadPreflightPreservesAllSocialState()
        {
            Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());Call("SocialRuntimeBridge","CommitRelationalFact",SocialFeedbackDomainTests.Fact());
            File.WriteAllText(savePath,"{\"SaveVersion\":1,\"Player\":{\"Gold\":7},\"SocialFeedbackWorld\":{\"Outbox\":[{\"SourceOccurrenceId\":\"activity:bad\",\"Status\":\"Delivered\"}]}}");
            Assert.IsNull(Call("SaveSystem","Load"));Assert.AreEqual("activity:pending",FeedbackSave().Outbox.Single().SourceOccurrenceId);Assert.AreEqual(8,Affinity("wang","zhang"));
        }
        [Test] public void D_LegacyAndNewGameEmpty()
        {Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());File.WriteAllText(savePath,"{\"SaveVersion\":1,\"Player\":{\"Gold\":7}}");Assert.IsNotNull(Call("SaveSystem","Load"));Assert.IsEmpty(FeedbackSave().Outbox);Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());Assert.IsNotNull(Call("SaveSystem","NewGame"));Assert.IsEmpty(FeedbackSave().Outbox);}
        [TestCase("Direct")] [TestCase("Update")] [TestCase("Jump")] [TestCase("Catchup")]
        public void F_SharedDailyBarrierSettlesBeforeSelection(string path)
        {
            Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());var time=(VillageTime)Get("VillageRuntimeBridge","Time");int days=0;
            time.DayTicked+=_=>{days++;Assert.AreEqual(8,Affinity("wang","zhang"));Assert.IsEmpty(FeedbackSave().Outbox);Assert.IsNotNull(Window);Assert.AreEqual(DecisionDailyResult.Resolved,((DecisionRoundResult)Get("AutonomousSelectionRuntimeBridge","LastResult")).Result);};
            if(path=="Direct")time.TickDay();if(path=="Update")time.Update(time.SecondsPerDay);if(path=="Jump")time.JumpToNextMonth();
            if(path=="Catchup"){Assert.IsTrue((bool)Call("VillageRuntimeBridge","BeginExpeditionReturn",3));Assert.IsTrue((bool)Call("VillageRuntimeBridge","MarkExpeditionReturnSettled"));Assert.AreEqual(ExpeditionCatchUpResult.Completed,Call("VillageRuntimeBridge","RunReturnCatchUp"));}
            Assert.Greater(days,0);Assert.AreEqual(1,SocialSave().RelationalFacts.Length);
        }
        [Test] public void F_FaultedBarrierRetainsOldWindowAndDoesNotResolveNewDay()
        {
            var time=(VillageTime)Get("VillageRuntimeBridge","Time");time.TickDay();var before=Window;var round=(AutonomousDecisionWorldSaveData)Call("AutonomousSelectionRuntimeBridge","BuildSaveData");
            Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending(status:"Faulted",fault:"InterpreterFailure"));Assert.DoesNotThrow(()=>time.TickDay());Assert.AreSame(before,Window);
            Assert.AreEqual("SocialFeedbackUnsettled",Get("VillageRuntimeBridge","LastActivityDailyPhaseResult").ToString());Assert.AreEqual(round.LastResolvedDay,((AutonomousDecisionWorldSaveData)Call("AutonomousSelectionRuntimeBridge","BuildSaveData")).LastResolvedDay);
        }
        [Test] public void F_PendingConflictBecomesFaultedAndBlocks()
        {Call("SocialRuntimeBridge","CommitRelationalFact",SocialFeedbackDomainTests.Fact("activity:pending","relational.helped_person"));Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());((VillageTime)Get("VillageRuntimeBridge","Time")).TickDay();Assert.IsNull(Window);Assert.AreEqual("Faulted",FeedbackSave().Outbox[0].Status);}
        [Test] public void F_ExplicitSelectionHostCannotBypassBarrier()
        {
            Call("ActivityRuntimeBridge","OpenDailyWindow",new ActivityDailyWindow(new ActivityDayStamp(1,1,2),new string[0],new string[0]));
            Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending(status:"Faulted",fault:"InterpreterFailure"));
            var result=(DecisionRoundResult)Call("AutonomousSelectionRuntimeBridge","RunDailyPhase",new ActivityDayStamp(1,1,2),Player(),true);
            Assert.AreEqual(DecisionDailyResult.InvalidPhase,result.Result);Assert.AreEqual("SocialFeedbackUnsettled",result.Diagnostic);Assert.IsNull(result.Plan);
        }
        [Test] public void F_SnapshotSeesSettledRelationship()
        {
            var player=Player();AddPerson(player,"zhang","prototype.zhang_san");AddPerson(player,"wang","prototype.wang_wu");
            Call("VillageRuntimeBridge","SetSimulationPersonSource",player);Call("AutonomousSocialFeedbackRuntimeBridge","Restore",Pending());
            ((VillageTime)Get("VillageRuntimeBridge","Time")).TickDay();var result=(DecisionRoundResult)Get("AutonomousSelectionRuntimeBridge","LastResult");
            Assert.AreEqual(DecisionDailyResult.Resolved,result.Result);Assert.AreEqual(8,result.Plan.Snapshot.Affinity("wang","zhang"));
        }
        [Test] public void G_CompletionLeavesOtherDomainsUntouched()
        {
            var events=JsonUtility.ToJson(Call("EventRuntimeBridge","BuildSaveData"));var village=JsonUtility.ToJson(Call("VillageRuntimeBridge","BuildSaveData"));
            Start();Assert.AreEqual(ActivityResult.Success,Complete("visit"));
            Assert.AreEqual(events,JsonUtility.ToJson(Call("EventRuntimeBridge","BuildSaveData")));Assert.AreEqual(village,JsonUtility.ToJson(Call("VillageRuntimeBridge","BuildSaveData")));
        }
        [Test] public void H_HistoricalReciprocityRealSaveLoadControlWorlds()
        {
            var player=Player();AddPerson(player,"zhang","prototype.zhang_san");AddPerson(player,"wang","prototype.wang_wu","HeavyInjury");
            Start();Assert.AreEqual(ActivityResult.Success,Complete("visit"));Assert.AreEqual(8,Affinity("wang","zhang"));Assert.IsTrue((bool)Call("SaveSystem","SaveWithVillage",player,null));
            Call("SocialRuntimeBridge","ResetForTests");var loaded=Call("SaveSystem","Load");Assert.IsNotNull(loaded);Assert.AreEqual(8,Affinity("wang","zhang"));var history=SocialSave();
            Call("ActivityRuntimeBridge","OpenDailyWindow",new ActivityDailyWindow(new ActivityDayStamp(1,1,3),new[]{"zhang","wang"},new[]{"zhang","wang"}));
            Set("AutonomousSelectionRuntimeBridge","ContextFixtureForTests",(Func<IEnumerable<ContextSignal>>)(()=>new[]{new ContextSignal("Target.HelpNeedSeverity","TestContext","Target","zhang",1)}));
            var b=(DecisionSnapshot)Call("AutonomousSelectionRuntimeBridge","BuildSnapshot",loaded);
            Call("SocialRuntimeBridge","ResetForTests");var a=(DecisionSnapshot)Call("AutonomousSelectionRuntimeBridge","BuildSnapshot",loaded);
            var candidate=new ActivityCandidate("help_zhang","Help","prototype.help","wang","zhang","Workshop");var ea=DesireEvaluator.Evaluate(a,candidate);var eb=DesireEvaluator.Evaluate(b,candidate);
            Assert.Greater(eb.Breakdown.Families["Relationship"],ea.Breakdown.Families["Relationship"]);Assert.Greater(eb.Breakdown.Score,ea.Breakdown.Score);Assert.Greater(eb.Weight,ea.Weight);
            Call("SocialRuntimeBridge","Restore",new SocialWorldSaveData{Relationships=history.Relationships,RelationalFacts=new RelationalFactSaveData[0]});var stateOnly=DesireEvaluator.Evaluate((DecisionSnapshot)Call("AutonomousSelectionRuntimeBridge","BuildSnapshot",loaded),candidate);
            Assert.AreEqual(eb.Breakdown.Score,stateOnly.Breakdown.Score);Assert.AreEqual(eb.Weight,stateOnly.Weight); // Raw Fact is never double-counted.
        }
    }
}

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;
using RRPG.StaticConfig;

namespace RRPG.Tests.AutonomousSelection
{
    public class SelectionIntegrationTests : SelectionIntegrationTestKit
    {
        private static Type Selection => Asm("AutonomousSelectionRuntimeBridge");
        private DecisionRoundResult Result => (DecisionRoundResult)Get(Selection, "LastResult");

        private AutonomousDecisionWorldSaveData DecisionSave() => (AutonomousDecisionWorldSaveData)Call(Selection, "BuildSaveData");
        private object Bind(bool prototype = true)
        {
            var player = Player();
            var a = AddCharacter(player, "A");
            var b = AddCharacter(player, "B");
            if (prototype)
            {
                Asm("CharacterInstance").GetField("PersonDefinitionId").SetValue(a, "prototype.zhang_san");
                Asm("CharacterInstance").GetField("PersonDefinitionId").SetValue(b, "prototype.zhao_liu");
            }

            Call(Village, "SetSimulationPersonSource", player);
            return player;
        }

        private void ResolveEmpty()
        {
            Call(Village, "SetSimulationPersonSource", Player());
            Time.TickDay();
            Assert.AreEqual(DecisionDailyResult.Resolved, Result.Result);
        }

        [Test]
        public void A_GeneratedChainAndProfileProvenance()
        {
            Assert.IsTrue(StaticConfigRegistry.TryGetAutonomousLifeConfig(out var db, out var diagnostic), diagnostic);
            Assert.AreEqual(2, db.BehaviorChannel.Count);
            Assert.AreEqual(8, db.PersonBehavior.Count);
            Assert.AreEqual(3, db.Place.Count);
            Assert.AreEqual(5, db.Activity.Count);
            Bind();
            Time.TickDay();
            Assert.AreEqual(DecisionDailyResult.Resolved, Result.Result);
            var p = Result.Plan.Snapshot.Person("A");
            Assert.AreEqual("A", p.PersonId);
            Assert.AreEqual("prototype.zhang_san", p.PersonDefinitionId);
            Assert.AreEqual(.8, p.Behavior.Query(Result.Plan.Snapshot.Catalog.Channels.First()), 1e-8);
        }

        [Test]
        public void A_LegacyMissingDefinitionNeutralAndDiagnostic()
        {
            Bind(false);
            Time.TickDay();
            var p = Result.Plan.Snapshot.Person("A");
            Assert.IsNotEmpty(p.Diagnostic);
            foreach (var d in Result.Plan.Snapshot.Catalog.Channels)
                Assert.AreEqual(d.NeutralValue, p.Behavior.Query(d));
            Assert.AreEqual(0, p.Preferences.Query("leisure.drink"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void G_DirectAndUpdate(bool update)
        {
            Bind();
            if (update)
                Time.Update(Time.SecondsPerDay);
            else
                Time.TickDay();
            Assert.AreEqual(DecisionDailyResult.Resolved, Result.Result);
            Assert.AreEqual(Window.DayStamp.Day,DecisionSave().LastResolvedDay);
            Assert.IsTrue(Window.IsIntraday);Assert.Less(DecisionSave().LastOpportunityMinute,Time.CurrentInstant.Minute);
            Assert.AreEqual(Window.DayStamp, Result.Plan.Snapshot.Day);
        }

        [Test]
        public void G_JumpMonthSharedPhase()
        {
            Bind();
            int days = 0;
            Time.DayTicked += _ =>
            {
                days++;
                Assert.AreEqual(DecisionDailyResult.Resolved, Result.Result);
                Assert.Less(DecisionSave().LastOpportunityMinute,Time.CurrentInstant.Minute);
            };
            Time.JumpToNextMonth();
            while(Time.HasActiveAdvance) Assert.AreEqual(WorldAdvanceOutcome.Completed,Time.ResumeAdvance(Time.AdvanceSession.RequestId,10000).Outcome);
            Assert.AreEqual(31, days);
        }

        [Test]
        public void G_ReturnCatchupAwayBlockedHomeResolves()
        {
            Bind();
            Call(Village, "SetExpeditionPersonIds", new object[] { new[] { "A" } });
            Assert.IsTrue((bool)Call(Village, "BeginExpeditionReturn", 3));
            Assert.IsTrue((bool)Call(Village, "MarkExpeditionReturnSettled"));
            int days = 0;
            Time.DayTicked += _ =>
            {
                days++;
                Assert.AreEqual(DecisionDailyResult.Resolved, Result.Result);
                Assert.IsFalse(Result.Plan.Snapshot.Window.ActorEligiblePersonIds.Contains("A"));
                Assert.IsTrue(Result.Plan.Snapshot.Window.ActorEligiblePersonIds.Contains("B"));
            };
            Assert.AreEqual(ExpeditionCatchUpResult.Completed, Call(Village, "RunReturnCatchUp"));
            Assert.AreEqual(3, days);
        }

        [Test]
        public void G_MissingSourceRetainsWindowDoesNotResolveNewDay()
        {
            Bind();
            Time.TickDay();
            var old = Window;
            var day = DecisionSave().LastResolvedDay;
            Call(Village, "SetSimulationPersonSource", new object[] { null });
            Time.TickDay();
            Assert.AreSame(old, Window);
            Assert.AreEqual(DecisionDailyResult.MissingPersonSource, Result.Result);
            Assert.AreEqual(day, DecisionSave().LastResolvedDay);
        }

        [Test]
        public void G_StaleWindowBlockedEvenNonNull()
        {
            var source = Bind();
            Time.TickDay();
            Call(Selection, "ResetForTests");
            Assert.AreEqual(DecisionDailyResult.StaleWindow, ((DecisionRoundResult)Call(Selection, "RunDailyPhase", new ActivityDayStamp(1, 1, 3), source, true)).Result);
        }

        [Test]
        public void G_PlanningFailureDoesNotEscapeDayTick()
        {
            Bind();
            Set(Selection, "PlannerForTests", new DecisionPlanner(new SelectionTestData.Provider((s, a) => throw new InvalidOperationException("fixture"))));
            Assert.DoesNotThrow(() => Time.TickDay());
            Assert.AreEqual(DecisionDailyResult.PlanningFailure, Result.Result);
            Assert.IsFalse(DecisionSave().HasLastResolvedDay);
            Assert.IsEmpty(Snapshot().PersonActivityStates);
            Assert.IsNull(Call(Host, "QueryClaim", "A"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void F_RealSaveRoundtripNoReplay(bool explicitVillage)
        {
            var p = Player();
            Call(Village,"SetSimulationPersonSource",p);Time.TickDay();
            var before = DecisionSave();
            Assert.IsTrue((bool)(explicitVillage ? Call(Save, "SaveWithVillage", p, Call(Village,"BuildSaveData")) : Call(Save, "Save", p)));
            StringAssert.Contains("\"AutonomousDecisionWorld\"", File.ReadAllText(savePath));
            Call(Selection, "ResetForTests");
            Assert.IsNotNull(Call(Save, "Load"));
            Assert.AreEqual(before.LastResolvedDay, DecisionSave().LastResolvedDay);
            Assert.IsNull(Window);
            Assert.IsNull(Result);
            Call(Host, "OpenDailyWindow", new ActivityDailyWindow(new ActivityDayStamp(before.LastResolvedYear, before.LastResolvedMonth, before.LastResolvedDay), new[] { "A", "B" }, new[] { "A", "B" }));
            Assert.AreEqual(DecisionDailyResult.AlreadyResolved, ((DecisionRoundResult)Call(Selection, "RunDailyPhase", Window.DayStamp, p, true)).Result);
        }

        [Test]
        public void F_EmptyRoundExactlyOnceAfterLoad()
        {
            ResolveEmpty();
            var day = Window.DayStamp;
            Call(Save, "Save", Player());
            Call(Selection, "ResetForTests");
            Call(Save, "Load");
            Call(Host, "OpenDailyWindow", new ActivityDailyWindow(day, new string[0], new string[0]));
            Assert.AreEqual(DecisionDailyResult.AlreadyResolved, ((DecisionRoundResult)Call(Selection, "RunDailyPhase", day, Player(), true)).Result);
        }

        [Test]
        public void F_LegacyMissingDecisionWorldIsEmpty()
        {
            ResolveEmpty();
            File.WriteAllText(savePath, "{\"SaveVersion\":1,\"Player\":{\"Gold\":7}}");
            Assert.IsNotNull(Call(Save, "Load"));
            Assert.IsFalse(DecisionSave().HasLastResolvedDay);
            Assert.IsNull(Window);
        }

        [Test]
        public void F_NewGameResets()
        {
            ResolveEmpty();
            Assert.IsNotNull(Call(Save, "NewGame"));
            Assert.IsFalse(DecisionSave().HasLastResolvedDay);
        }

        [Test]
        public void F_ReplacementBToA()
        {
            ResolveEmpty();
            Call(Save, "Save", Player());
            Time.TickDay();
            Assert.AreEqual(2, DecisionSave().LastResolvedDay);
            Call(Save, "Load");
            Assert.AreEqual(1, DecisionSave().LastResolvedDay);
        }

        [TestCase("{invalid")]
        [TestCase("{\"SaveVersion\":999,\"Player\":{}}")]
        [TestCase("{\"SaveVersion\":1,\"Player\":null}")]
        [TestCase(null)]
        public void F_FailedLoadRetains(string json)
        {
            ResolveEmpty();
            var before = DecisionSave().LastResolvedDay;
            if (json == null)
            {
                if (File.Exists(savePath))
                    File.Delete(savePath);
            }
            else
                File.WriteAllText(savePath, json);
            Assert.IsNull(Call(Save, "Load"));
            Assert.AreEqual(before, DecisionSave().LastResolvedDay);
        }

        [Test]
        public void F_PlayerRestoreFailureRetainsDecision()
        {
            ResolveEmpty();
            var before = DecisionSave().LastResolvedDay;
            File.WriteAllText(savePath, "{\"SaveVersion\":1,\"Player\":{\"Heroes\":[{\"HeroId\":\"test_restore_failure\"}]},\"AutonomousDecisionWorld\":{}}");
            var type = Asm("DataManager");
            var manager = type.GetProperty("Instance", Flags).GetValue(null);
            var field = type.GetField("_heroMap", Flags);
            var previous = field.GetValue(manager);
            try
            {
                field.SetValue(manager, null);
                Assert.IsNull(Call(Save, "Load"));
                Assert.AreEqual(before, DecisionSave().LastResolvedDay);
            }
            finally
            {
                field.SetValue(manager, previous);
            }
        }


        [TestCase(0, 1, 1)]
        [TestCase(1, 13, 1)]
        [TestCase(1, 1, 32)]
        public void F_InvalidDecisionBlockFailsLoadAndRetainsWorld(int year, int month, int day)
        {
            ResolveEmpty();
            var before = DecisionSave();
            var trace = Result;
            var window = Window;
            var invalid = new AutonomousDecisionWorldSaveData
            {
                HasLastResolvedDay = true,
                LastResolvedYear = year,
                LastResolvedMonth = month,
                LastResolvedDay = day
            };
            Assert.IsFalse((bool)Call(Selection, "Restore", invalid));
            Assert.AreSame(trace, Result);
            File.WriteAllText(savePath, "{\"SaveVersion\":1,\"Player\":{\"Gold\":7},\"AutonomousDecisionWorld\":" + UnityEngine.JsonUtility.ToJson(invalid) + "}");
            Assert.IsNull(Call(Save, "Load"));
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(before), UnityEngine.JsonUtility.ToJson(DecisionSave()));
            Assert.AreSame(trace, Result);
            Assert.AreSame(window, Window);
            Assert.AreEqual(1, Save.GetField("CurrentSaveVersion").GetRawConstantValue());
        }

        private static AutonomousLifeConfigDatabase CopyConfigWithCalibration(System.Collections.Generic.List<AutonomousLifeCalibrationConfigRow> calibration)
        {
            Assert.IsTrue(StaticConfigRegistry.TryGetAutonomousLifeConfig(out var source, out var diagnostic), diagnostic);
            var copy = UnityEngine.ScriptableObject.CreateInstance<AutonomousLifeConfigDatabase>();
            copy.ApplyGeneratedContent(source.BehaviorChannel.ToList(), source.PersonBehavior.ToList(), source.PersonPreference.ToList(),
                source.Place.ToList(), source.Activity.ToList(), source.ActivityProvider.ToList(), source.ActivityBehaviorBinding.ToList(),
                source.ActivityPreferenceBinding.ToList(), source.ActivityRelationshipBinding.ToList(), source.ActivityContextBinding.ToList(),
                source.ActivityRepetition.ToList(), calibration);
            return copy;
        }

        private static System.Collections.Generic.List<AutonomousLifeCalibrationConfigRow> CalibrationCopy()
        {
            Assert.IsTrue(StaticConfigRegistry.TryGetAutonomousLifeConfig(out var source, out var diagnostic), diagnostic);
            return source.AutonomousLifeCalibration.Select(r => new AutonomousLifeCalibrationConfigRow { CalibrationKey = r.CalibrationKey, Value = r.Value }).ToList();
        }

        [TestCase("InjuryLight")]
        [TestCase("InjuryHeavy")]
        [TestCase("InjuryDisabled")]
        public void A_HostRejectsMissingInjuryCalibrationBeforePersonEvaluation(string key)
        {
            var rows = CalibrationCopy();
            rows.RemoveAll(r => r.CalibrationKey == key);
            var copy = CopyConfigWithCalibration(rows);
            try
            {
                var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => Call(Asm("AutonomousSelectionConfigAdapter"), "BuildCatalog", copy));
                Assert.IsInstanceOf<ArgumentException>(error.InnerException);
                StringAssert.Contains(key, error.InnerException.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [TestCase("InjuryLight")]
        [TestCase("InjuryHeavy")]
        [TestCase("InjuryDisabled")]
        public void A_HostValidatesInjuryMagnitudeAndAcceptsEndpoints(string key)
        {
            var rows = CalibrationCopy();
            var row = rows.Single(r => r.CalibrationKey == key);
            var copy = CopyConfigWithCalibration(rows);
            try
            {
                foreach (var value in new[] { -.1, 1.1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    row.Value = value;
                    var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => Call(Asm("AutonomousSelectionConfigAdapter"), "BuildCatalog", copy));
                    Assert.IsInstanceOf<ArgumentException>(error.InnerException);
                    StringAssert.Contains(key, error.InnerException.Message);
                }
                foreach (var value in new[] { 0.0, 1.0 })
                {
                    row.Value = value;
                    Assert.DoesNotThrow(() => Call(Asm("AutonomousSelectionConfigAdapter"), "BuildCatalog", copy));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [TestCase("InjuryLight")]
        [TestCase("InjuryHeavy")]
        [TestCase("InjuryDisabled")]
        public void A_BuilderReaderRejectsIncompleteOrInvalidInjuryCalibration(string key)
        {
            var dir = Path.Combine(Path.GetTempPath(), "rrpg-al3-injury-reader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var repo = new DirectoryInfo(UnityEngine.Application.dataPath);
                while (repo != null && !Directory.Exists(Path.Combine(repo.FullName, "Development", "Generated", "StaticConfig")))
                    repo = repo.Parent;
                Assert.IsNotNull(repo);
                foreach (var file in Directory.GetFiles(Path.Combine(repo.FullName, "Development", "Generated", "StaticConfig"), "*.json"))
                    File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
                var path = Path.Combine(dir, "AutonomousLifeCalibration.json");
                var original = File.ReadAllText(path);
                var reader = typeof(RRPG.StaticConfig.EditorTools.StaticConfigBuilder).Assembly.GetType("RRPG.StaticConfig.EditorTools.AutonomousLifeMirrorReader", true).GetMethod("Read", Flags);
                Assert.DoesNotThrow(() => reader.Invoke(null, new object[] { dir }));
                File.WriteAllText(path, original.Replace("\"" + key + "\"", "\"Missing_" + key + "\""));
                var missing = Assert.Throws<System.Reflection.TargetInvocationException>(() => reader.Invoke(null, new object[] { dir }));
                StringAssert.Contains(key, missing.InnerException.Message);
                foreach (var value in new[] { "-0.1", "1.1" })
                {
                    var invalid = System.Text.RegularExpressions.Regex.Replace(original,
                        "(\"CalibrationKey\"\\s*:\\s*\"" + key + "\"\\s*,\\s*\"Value\"\\s*:\\s*)[-0-9.eE+]+",
                        m => m.Groups[1].Value + value);
                    Assert.AreNotEqual(original, invalid);
                    File.WriteAllText(path, invalid);
                    var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => reader.Invoke(null, new object[] { dir }));
                    StringAssert.Contains(key, error.InnerException.Message);
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void F_SaveVersionOne()
        {
            Assert.AreEqual(1, Save.GetField("CurrentSaveVersion").GetRawConstantValue());
        }

        [Test]
        public void H_ReadOnlyOtherAuthorities()
        {
            var source = Bind();
            Call(Host, "OpenDailyWindow", new ActivityDailyWindow(SelectionTestData.Day(), new[] { "A", "B" }, new[] { "A", "B" }));
            var social = UnityEngine.JsonUtility.ToJson(Call(Asm("SocialRuntimeBridge"), "BuildSaveData"));
            var events = UnityEngine.JsonUtility.ToJson(Call(Asm("EventRuntimeBridge"), "BuildSaveData"));
            var before = UnityEngine.JsonUtility.ToJson(source);
            var assignment = ((VillageAssignmentSystem)Get(Village, "Assignments")).GetRecord("A");
            var r = (DecisionRoundResult)Call(Selection, "RunDailyPhase", Window.DayStamp, source, true);
            Assert.AreEqual(DecisionDailyResult.Resolved, r.Result);
            Assert.AreEqual(social, UnityEngine.JsonUtility.ToJson(Call(Asm("SocialRuntimeBridge"), "BuildSaveData")));
            Assert.AreEqual(events, UnityEngine.JsonUtility.ToJson(Call(Asm("EventRuntimeBridge"), "BuildSaveData")));
            Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(source));
            Assert.AreSame(assignment, ((VillageAssignmentSystem)Get(Village, "Assignments")).GetRecord("A"));
            Assert.IsTrue(r.Executions.All(e => e.Result == ActivityResult.Success && e.Completion != null));
        }
    }
}

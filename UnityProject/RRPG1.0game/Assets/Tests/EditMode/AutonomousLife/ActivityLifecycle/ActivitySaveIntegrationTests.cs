using System;
using System.IO;
using NUnit.Framework;
using RRPG.AutonomousActivity;
namespace RRPG.Tests.ActivityLifecycle
{
    public class ActivitySaveIntegrationTests : ActivityIntegrationTestKit
    {
        [TestCase(false)] [TestCase(true)]
        public void D1_AllSaveEntrypointsRoundTrip(bool explicitVillage)
        {
            Seed(); Assert.IsTrue((bool)(explicitVillage ? Call(Save,"SaveWithVillage",Player(),null) : Call(Save,"Save",Player())));
            StringAssert.Contains("\"ActivityWorld\"",File.ReadAllText(savePath));
            Call(Host,"ResetForTests"); Assert.IsNotNull(Call(Save,"Load"));
            Assert.AreEqual(ActivityLifecycleTests.Day(),((PersonActivityState)Call(Host,"QueryPersonActivityState","A")).LastMajorParticipationDay);
        }
        [Test] public void D2_SameDayParticipationSurvivesLoadEvenExplicitReopen()
        {
            Seed(); Assert.IsTrue((bool)Call(Save,"Save",Player())); Call(Host,"ResetForTests"); Assert.IsNotNull(Call(Save,"Load")); Open();
            Assert.AreEqual(ActivityResult.AlreadyParticipated,Call(Host,"TryCommitActivity","second",ActivityLifecycleTests.Candidate()));
            Assert.IsNull(Call(Host,"QueryClaim","A"));
        }
        [Test] public void D3_LegacySaveWithoutActivityWorld()
        {
            Seed(); File.WriteAllText(savePath,"{\"SaveVersion\":1,\"Player\":{\"Gold\":7}}");
            Assert.IsNotNull(Call(Save,"Load")); Assert.IsEmpty(Snapshot().PersonActivityStates); Assert.IsNull(Window);
        }
        [Test] public void D4_NewGameExplicitlyResetsActivityWorld()
        {
            Seed(); Assert.IsNotNull(Call(Save,"NewGame")); Assert.IsEmpty(Snapshot().PersonActivityStates); Assert.IsNull(Window);
        }
        [Test] public void D5_LoadBReplacesA()
        {
            Seed("B"); Call(Save,"Save",Player()); Call(Host,"ResetForTests"); Seed("A"); Assert.IsNotNull(Call(Save,"Load"));
            Assert.AreEqual(1,Snapshot().PersonActivityStates.Length); Assert.AreEqual("B",Snapshot().PersonActivityStates[0].PersonId);
        }
        [TestCase("{invalid_json")]
        [TestCase("{\"SaveVersion\":999,\"Player\":{}}")]
        [TestCase("{\"SaveVersion\":1}")]
        [TestCase("{\"SaveVersion\":1,\"Player\":null}")]
        [TestCase(null)]
        public void D6_FailedLoadRetainsCurrentWorldAndWindow(string json)
        {
            Seed(); var before=Window; Call(Host,"OpenDailyWindow",new ActivityDailyWindow(new ActivityDayStamp(1,1,3),new[]{"B"},new[]{"B"}));
            Call(Host,"TryCommitActivity","inflight",ActivityLifecycleTests.Candidate("B")); before=Window;
            if(json==null){if(File.Exists(savePath))File.Delete(savePath);}else File.WriteAllText(savePath,json);
            Assert.IsNull(Call(Save,"Load")); Assert.AreSame(before,Window);
            Assert.AreEqual("A",Snapshot().PersonActivityStates[0].PersonId); Assert.AreEqual("inflight",Call(Host,"QueryClaim","B"));
        }
        [Test] public void D6_PlayerRestoreFailureRetainsActivity()
        {
            Seed(); var before=Window;
            File.WriteAllText(savePath,"{\"SaveVersion\":1,\"Player\":{\"Heroes\":[{\"HeroId\":\"test_restore_failure\"}]},\"ActivityWorld\":{}}");
            var managerType=Asm("DataManager"); var manager=managerType.GetProperty("Instance",Flags).GetValue(null);
            var field=managerType.GetField("_heroMap",Flags); var previous=field.GetValue(manager);
            try{field.SetValue(manager,null);Assert.IsNull(Call(Save,"Load"));Assert.AreSame(before,Window);Assert.AreEqual("A",Snapshot().PersonActivityStates[0].PersonId);}
            finally{field.SetValue(manager,previous);}
        }
        [Test] public void D7_SaveVersionRemainsOne()
        {
            Assert.AreEqual(1,Save.GetField("CurrentSaveVersion").GetRawConstantValue()); Call(Save,"Save",Player());
            StringAssert.Contains("\"SaveVersion\": 1",File.ReadAllText(savePath));
        }
        [Test] public void D8_LoadNeverReopensWindowOrReplaysInflight()
        {
            Seed(); Call(Host,"OpenDailyWindow",new ActivityDailyWindow(new ActivityDayStamp(1,1,3),new[]{"B"},new[]{"B"}));
            Call(Host,"TryCommitActivity","pending",ActivityLifecycleTests.Candidate("B")); Call(Save,"Save",Player());
            Assert.IsNotNull(Call(Save,"Load")); Assert.IsNull(Window); Assert.IsNull(Call(Host,"QueryClaim","B")); Assert.IsNull(Call(Host,"QueryActivityInstance","pending"));
            Assert.AreEqual(1,Snapshot().PersonActivityStates.Length);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using UnityEngine;
namespace RRPG.Tests.ActivityLifecycle
{
    public class ActivityCrossDomainTests : ActivityIntegrationTestKit
    {
        [Test] public void E1_LifecycleDoesNotWriteSocialEventPersonAssignmentOrProject()
        {
            var player=Player(); AddCharacter(player,"A"); Call(Village,"SetSimulationPersonSource",player);
            string person=JsonUtility.ToJson(player);
            string village=JsonUtility.ToJson(Call(Village,"BuildSaveData"));
            string social=JsonUtility.ToJson(Call(Asm("SocialRuntimeBridge"),"BuildSaveData"));
            string events=JsonUtility.ToJson(Call(Asm("EventRuntimeBridge"),"BuildSaveData"));
            int objects=Resources.FindObjectsOfTypeAll<GameObject>().Length;
            var random=UnityEngine.Random.state;
            Seed(); Assert.AreEqual(person,JsonUtility.ToJson(player)); Assert.AreEqual(village,JsonUtility.ToJson(Call(Village,"BuildSaveData")));
            Assert.AreEqual(social,JsonUtility.ToJson(Call(Asm("SocialRuntimeBridge"),"BuildSaveData")));
            Assert.AreEqual(events,JsonUtility.ToJson(Call(Asm("EventRuntimeBridge"),"BuildSaveData")));
            Assert.AreEqual(objects,Resources.FindObjectsOfTypeAll<GameObject>().Length); Assert.AreEqual(random,UnityEngine.Random.state);
        }
        [Test] public void E2_PureAssemblyHasNoCrossDomainOrEngineReferences()
        {
            var names=typeof(ActivityWorldState).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
            Assert.IsFalse(names.Any(n=>n.StartsWith("Unity")||n.Contains("Village")||n.Contains("Social")||n.Contains("Event")||n.Contains("Person")));
            string path=Path.Combine(Application.dataPath,"Scripts/AutonomousLife/Activity/Runtime");
            foreach(var file in Directory.GetFiles(path,"*.cs"))
            {
                string code=File.ReadAllText(file);
                foreach(var forbidden in new[]{"Guid.NewGuid","DateTime.Now","DateTime.UtcNow","new Random","UnityEngine","SocialRuntimeBridge","EventRuntimeBridge"})
                    StringAssert.DoesNotContain(forbidden,code,file);
            }
        }
        private sealed class TestProvider : IActivityProvider
        {
            public System.Collections.Generic.IEnumerable<ActivityCandidate> CollectCandidates(ActivityDailyWindow window,string actor)
                => new[]{ActivityLifecycleTests.Candidate(actor)};
        }
        [Test] public void E3_ProviderQueryDoesNotCommitOrChangeWindow()
        {
            Open(); var before=Window; var provider=new TestProvider();
            Assert.AreEqual("A",provider.CollectCandidates(before,"A").Single().ActorPersonId);
            Assert.AreSame(before,Window); Assert.IsEmpty(Snapshot().PersonActivityStates); Assert.IsNull(Call(Host,"QueryClaim","A"));
        }
    }
}

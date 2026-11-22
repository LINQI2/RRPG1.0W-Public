using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RRPG.Social;

namespace RRPG.Tests.AutonomousLife
{
    /// <summary>Real SaveSystem integration. Tests preserve the user's on-disk save byte-for-byte.</summary>
    public class SocialSaveIntegrationTests
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private string savePath;
        private byte[] originalSave;
        private SocialWorldSaveData originalSocial;
        private object originalVillage;
        private object originalEvent;
        private object originalFailureHook;

        private static Type Host => Type.GetType("SocialRuntimeBridge, Assembly-CSharp", true);
        private static Type Save => Type.GetType("SaveSystem, Assembly-CSharp", true);
        private static object Call(Type type, string method, params object[] args)
            => type.GetMethod(method, Static).Invoke(null, args);
        private static SocialWorldSaveData Snapshot() => (SocialWorldSaveData)Call(Host, "BuildSaveData");

        [SetUp]
        public void SetUp()
        {
            savePath = (string)Call(Save, "GetSavePath");
            originalSave = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
            originalSocial = Snapshot();
            originalFailureHook = Save.GetField("ForceSaveFailureForTests", Static).GetValue(null);
            Save.GetField("ForceSaveFailureForTests", Static).SetValue(null, null);
            var village = Type.GetType("VillageRuntimeBridge, Assembly-CSharp", true);
            originalVillage = Call(village, "BuildSaveData");
            var events = Type.GetType("EventRuntimeBridge, Assembly-CSharp", true);
            originalEvent = Call(events, "BuildSaveData");
            Call(village, "ResetForTests");
            Call(Host, "ResetForTests");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Call(Host, "Restore", originalSocial);
                var village = Type.GetType("VillageRuntimeBridge, Assembly-CSharp", true);
                Call(village, "ResetForTests");
                Call(village, "EnsureInitialized", originalVillage);
                Call(Type.GetType("EventRuntimeBridge, Assembly-CSharp", true), "Restore", originalEvent);
                Save.GetField("ForceSaveFailureForTests", Static).SetValue(null, originalFailureHook);
            }
            finally
            {
                if (originalSave != null)
                {
                    File.WriteAllBytes(savePath, originalSave);
                    CollectionAssert.AreEqual(originalSave, File.ReadAllBytes(savePath));
                }
                else if (File.Exists(savePath)) File.Delete(savePath);
            }
        }

        private static void Seed(string id = "world_A", string actor = "A", string target = "B")
        {
            var world = new SocialWorldState();
            var result = new SocialCommitService(world, new TestInterpreter()).Commit(SocialTestKit.Fact(id, actor, target));
            Assert.AreEqual(SocialCommitResult.Success, result);
            Call(Host, "Restore", SocialWorldPersistence.Build(world));
        }
        private static object EmptyPlayer() => Activator.CreateInstance(Type.GetType("PlayerCollection, Assembly-CSharp", true));
        private static void AssertEmpty()
        {
            Assert.AreEqual(0, Snapshot().RelationalFacts.Length);
            Assert.AreEqual(0, Snapshot().Relationships.Length);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void D1_RealSaveLoadRoundTripAllSaveEntrypoints(bool explicitVillage)
        {
            Seed();
            bool saved = explicitVillage
                ? (bool)Call(Save, "SaveWithVillage", EmptyPlayer(), null)
                : (bool)Call(Save, "Save", EmptyPlayer());
            Assert.IsTrue(saved);
            string json = File.ReadAllText(savePath);
            StringAssert.Contains("\"SocialWorld\"", json);
            StringAssert.Contains("\"world_A\"", json);
            Call(Host, "ResetForTests");
            Assert.IsNotNull(Call(Save, "Load"));
            Assert.AreEqual("world_A", Snapshot().RelationalFacts[0].SourceOccurrenceId);
            Assert.AreEqual(10f, Snapshot().Relationships[0].Affinity);
            Assert.AreEqual(SocialCommitResult.Duplicate,
                (SocialCommitResult)Call(Host, "CommitRelationalFact", SocialTestKit.Fact("world_A")));
        }
        [Test] public void D2_LegacySaveWithoutSocialWorldLoadsEmpty()
        {
            Seed();
            File.WriteAllText(savePath, "{\"SaveVersion\":1,\"Player\":{\"Gold\":7}}");
            Assert.IsNotNull(Call(Save, "Load"));
            AssertEmpty();
            Assert.AreEqual(0f, ((DirectedRelationshipState)Call(Host, "QueryRelationship", "A", "B")).Affinity);
        }
        [Test] public void D3_NewGameClearsSocialWorld()
        {
            Seed();
            Assert.IsNotNull(Call(Save, "NewGame"));
            AssertEmpty();
        }
        [Test] public void D4_LoadWorldBReplacesWorldA()
        {
            Seed("world_B", "C", "D");
            Assert.IsTrue((bool)Call(Save, "Save", EmptyPlayer()));
            Call(Host, "ResetForTests"); Seed();
            Assert.IsNotNull(Call(Save, "Load"));
            Assert.AreEqual(1, Snapshot().RelationalFacts.Length);
            Assert.AreEqual("world_B", Snapshot().RelationalFacts[0].SourceOccurrenceId);
            Assert.AreEqual(0f, ((DirectedRelationshipState)Call(Host, "QueryRelationship", "B", "A")).Affinity);
            Assert.AreEqual(10f, ((DirectedRelationshipState)Call(Host, "QueryRelationship", "D", "C")).Affinity);
        }
        [Test] public void D5_SaveVersionRemainsOne()
        {
            Assert.AreEqual(1, Save.GetField("CurrentSaveVersion", Static).GetRawConstantValue());
            Assert.IsTrue((bool)Call(Save, "Save", EmptyPlayer()));
            var dtoType = Type.GetType("SaveData, Assembly-CSharp", true);
            var dto = UnityEngine.JsonUtility.FromJson(File.ReadAllText(savePath), dtoType);
            Assert.AreEqual(1, dtoType.GetField("SaveVersion").GetValue(dto));
        }
        [TestCase("{invalid_json")]
        [TestCase("{\"SaveVersion\":999,\"Player\":{}}")]
        [TestCase("{\"SaveVersion\":1}")]
        [TestCase("{\"SaveVersion\":1,\"Player\":null}")]
        [TestCase("{\"SaveVersion\":1,\"Other\":{\"Player\":{}}}")]
        [TestCase("{\"SaveVersion\":1,\"Other\":\"\\\"Player\\\":{}\"}")]
        [TestCase("{\"SaveVersion\":1,\"Player\":{},\"Player\":{}}")]
        public void D6_FailedLoadRetainsSocialWorld(string json)
        {
            Seed();
            File.WriteAllText(savePath, json);
            Assert.IsNull(Call(Save, "Load"));
            Assert.AreEqual("world_A", Snapshot().RelationalFacts[0].SourceOccurrenceId);
            Assert.AreEqual(10f, Snapshot().Relationships[0].Affinity);
        }
        [Test] public void D6_PlayerRestoreFailureRetainsSocialWorld()
        {
            Seed();
            File.WriteAllText(savePath,
                "{\"SaveVersion\":1,\"Player\":{\"Heroes\":[{\"HeroId\":\"test_restore_failure\"}]},\"SocialWorld\":{}}");
            // Test-only reflection fault injection; restore the exact DataManager field in finally.
            var managerType = Type.GetType("DataManager, Assembly-CSharp", true);
            var manager = managerType.GetProperty("Instance", Static).GetValue(null);
            var mapField = managerType.GetField("_heroMap", BindingFlags.Instance | BindingFlags.NonPublic);
            var previous = mapField.GetValue(manager);
            try
            {
                mapField.SetValue(manager, null);
                Assert.IsNull(Call(Save, "Load"));
                Assert.AreEqual("world_A", Snapshot().RelationalFacts[0].SourceOccurrenceId);
                Assert.AreEqual(10f, Snapshot().Relationships[0].Affinity);
            }
            finally { mapField.SetValue(manager, previous); }
        }
        [Test] public void EscapedRootPlayerMemberRemainsCompatible()
        {
            File.WriteAllText(savePath, "{\"SaveVersion\":1,\"Pla\\u0079er\":{\"Gold\":7}}");
            Assert.IsNotNull(Call(Save, "Load"));
        }
        [Test] public void HostReadsAndSaveSnapshotNeverInitializePersistentWorld()
        {
            var current = Host.GetField("current", Static);
            current.SetValue(null, null); // Local test seam; production API exposes no mutable world.
            Assert.AreEqual(0f, ((DirectedRelationshipState)Call(Host, "QueryRelationship", "A", "B")).Affinity);
            Call(Host, "QueryFacts"); Snapshot();
            Assert.IsNull(current.GetValue(null));
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using UnityEngine;

namespace RRPG.Tests.AutonomousSelection
{
    public abstract class SelectionIntegrationTestKit
    {
        protected const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        protected static Type Asm(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        protected static Type Village => Asm("VillageRuntimeBridge");
        protected static Type Host => Asm("ActivityRuntimeBridge");
        protected static Type Save => Asm("SaveSystem");

        protected static object Call(Type type, string method, params object[] args)
        {
            var methods = type.GetMethods(Flags).Where(m => m.Name == method && m.GetParameters().Length == args.Length);
            var selected = methods.First(m => m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
            return selected.Invoke(null, args);
        }

        protected static object Get(Type type, string member) => type.GetField(member, Flags)?.GetValue(null) ?? type.GetProperty(member, Flags)?.GetValue(null);
        protected static void Set(Type type, string member, object value) => type.GetField(member, Flags).SetValue(null, value);
        protected static ActivityDailyWindow Window => (ActivityDailyWindow)Call(Host, "QueryCurrentWindow");

        protected static ActivityWorldSaveData Snapshot() => (ActivityWorldSaveData)Call(Host, "BuildSaveData");
        protected static object Player() => Activator.CreateInstance(Asm("PlayerCollection"));
        protected static object AddCharacter(object collection, string id, bool dead = false)
        {
            var character = Activator.CreateInstance(Asm("CharacterInstance"), "test_character", "Test Person", 30, 6);
            Asm("CharacterInstance").GetField("PersonId", Flags)?.SetValue(character, id);
            var personProperty = Asm("CharacterInstance").GetProperty("PersonId", Flags);
            if (personProperty != null)
                personProperty.SetValue(character, id);
            Asm("CharacterInstance").GetField("IsDead", Flags)?.SetValue(character, dead);
            var deadProperty = Asm("CharacterInstance").GetProperty("IsDead", Flags);
            if (deadProperty != null)
                deadProperty.SetValue(character, dead);
            var owned = Asm("PlayerCollection").GetField("OwnedCharacters", Flags)?.GetValue(collection) ?? Asm("PlayerCollection").GetProperty("OwnedCharacters", Flags).GetValue(collection);
            ((IList)owned).Add(character);
            return character;
        }

        protected static void Open(string actor = "A") => Call(Host, "OpenDailyWindow", new ActivityDailyWindow(SelectionTestData.Day(), new[] { actor }, new[] { actor }));
        protected static void Seed(string actor = "A")
        {
            Open(actor);
            Assert.AreEqual(ActivityResult.Success, Call(Host, "TryCommitActivity", "fixed_" + actor, SelectionTestData.Candidate(actor, "Rest", null)));
            Assert.AreEqual(ActivityResult.Success, Call(Host, "TryBeginExecution", "fixed_" + actor));
            var args = new object[]
            {
                "fixed_" + actor,
                null
            };
            Assert.AreEqual(ActivityResult.Success, Host.GetMethod("TryComplete", Flags).Invoke(null, args));
        }

        protected VillageTime Time => (VillageTime)Get(Village, "Time");

        private object originalVillage, originalEvents, originalSource, originalHook;
        private ActivityWorldSaveData originalActivity;
        private RRPG.Social.SocialWorldSaveData originalSocial;
        protected string savePath;
        private byte[] originalSave;
        private object originalDecision;
        [SetUp]
        public void PreserveWorld()
        {
            originalDecision = Call(Asm("AutonomousSelectionRuntimeBridge"), "BuildSaveData");
            Call(Asm("AutonomousSelectionRuntimeBridge"), "ResetForTests");
            savePath = (string)Call(Save, "GetSavePath");
            originalSave = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
            originalVillage = Call(Village, "BuildSaveData");
            originalSource = Get(Village, "SimulationPersonSource");
            originalEvents = Call(Asm("EventRuntimeBridge"), "BuildSaveData");
            originalSocial = (RRPG.Social.SocialWorldSaveData)Call(Asm("SocialRuntimeBridge"), "BuildSaveData");
            originalActivity = Snapshot();
            originalHook = Get(Save, "ForceSaveFailureForTests");
            Set(Save, "ForceSaveFailureForTests", null);
            Call(Village, "ResetForTests");
            Call(Village, "EnsureInitialized", new object[] { null });
            Call(Host, "ResetForTests");
        }

        [TearDown]
        public void RestoreWorld()
        {
            try
            {
                Call(Village, "ResetForTests");
                Call(Village, "EnsureInitialized", originalVillage);
                Call(Village, "SetSimulationPersonSource", originalSource);
                Call(Asm("EventRuntimeBridge"), "Restore", originalEvents);
                Call(Asm("SocialRuntimeBridge"), "Restore", originalSocial);
                Call(Host, "Restore", originalActivity);
                Call(Asm("AutonomousSelectionRuntimeBridge"), "ResetForTests");
                Call(Asm("AutonomousSelectionRuntimeBridge"), "Restore", originalDecision);
                Set(Save, "ForceSaveFailureForTests", originalHook);
            }
            finally
            {
                if (originalSave != null)
                    File.WriteAllBytes(savePath, originalSave);
                else if (File.Exists(savePath))
                    File.Delete(savePath);
            }
        }
    }
}

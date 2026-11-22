using System;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using UnityEngine;

namespace RRPG.Tests.AutonomousSelection
{
    public class IntradayPathEquivalenceTests : SelectionIntegrationTestKit
    {
        private string RunWorld(string path)
        {
            Call(Village,"ResetForTests");Call(Village,"EnsureInitialized",new object[]{null});
            Call(Asm("SocialRuntimeBridge"),"ResetForTests");Call(Asm("EventRuntimeBridge"),"Restore",new object[]{null});
            var source=Player();
            foreach(string id in new[]{"away","zhang","wang"})
            {
                var person=AddCharacter(source,id);
                Asm("CharacterInstance").GetField("CharacterId").SetValue(person,"char_001");
                Asm("CharacterInstance").GetField("PersonDefinitionId").SetValue(person,id=="wang"?"prototype.wang_wu":"prototype.zhang_san");
                if(id=="wang")
                {
                    var field=Asm("CharacterInstance").GetField("Injury");
                    field.SetValue(person,Enum.Parse(field.FieldType,"HeavyInjury"));
                }
            }
            Call(Village,"SetSimulationPersonSource",source);Call(Village,"SetExpeditionPersonIds",new object[]{new[]{"away"}});
            ((VillageResourceState)Get(Village,"Resources")).AddFood(100);
            const long target=31*1440;
            if(path=="normal" || path=="fast")
            {
                Time.SecondsPerDay=1440;
                if(path=="fast")Time.SetSpeed(VillageTime.VillageTimeSpeed.Fast);
                while(Time.CurrentInstant.Minute<target) Time.Update(path=="fast"?.25f:1f);
                Time.SetSpeed(VillageTime.VillageTimeSpeed.Normal);
            }
            else if(path=="jump")
            {
                Time.JumpToNextMonth();
                while(Time.HasActiveAdvance) Assert.AreEqual(WorldAdvanceOutcome.Completed,Time.ResumeAdvance(Time.AdvanceSession.RequestId,10000).Outcome);
            }
            else
            {
                Assert.IsTrue((bool)Call(Village,"BeginExpeditionReturn",31));Assert.IsTrue((bool)Call(Village,"MarkExpeditionReturnSettled"));
                object result;
                int attempts=0;
                do{result=Call(Village,"RunReturnCatchUp");Assert.Less(++attempts,100);}while((ExpeditionCatchUpResult)result==ExpeditionCatchUpResult.Yielded);
                Assert.AreEqual(ExpeditionCatchUpResult.Completed,result);
            }
            Assert.AreEqual(target,Time.CurrentInstant.Minute);
            var facts=(RRPG.Social.SocialWorldSaveData)Call(Asm("SocialRuntimeBridge"),"BuildSaveData");Assert.Greater(facts.RelationalFacts.Length,0);
            Assert.IsNull(Call(Host,"QueryClaim","away"));
            string resultJson=JsonUtility.ToJson(Call(Village,"BuildSaveData"))+"\n"+JsonUtility.ToJson(Snapshot())
                +"\n"+JsonUtility.ToJson(facts)+"\n"+JsonUtility.ToJson(Call(Asm("AutonomousSelectionRuntimeBridge"),"BuildSaveData"))
                +"\n"+JsonUtility.ToJson(Call(Asm("AutonomousSocialFeedbackRuntimeBridge"),"BuildSaveData"))+"\n"+JsonUtility.ToJson(source);
            return resultJson;
        }
        [Test] public void NormalFastJumpReturn_ShareClockAndProduceIdenticalPersistentWorlds()
        {
            string normal=RunWorld("normal");
            foreach(string path in new[]{"fast","jump","return"}) Assert.AreEqual(normal,RunWorld(path),path);
        }
    }
}

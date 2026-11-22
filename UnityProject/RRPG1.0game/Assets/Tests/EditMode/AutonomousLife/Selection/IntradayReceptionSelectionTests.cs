using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;

namespace RRPG.Tests.AutonomousSelection
{
    public class IntradayReceptionSelectionTests
    {
        private DecisionSnapshot Snapshot(long minute,long? missed=null)
        {
            var old=SelectionTestData.Catalog();
            var catalog=new SelectionCatalog(old.Channels,old.Activities,new[]{new PrototypeProviderDefinition("talk","Talk","Home",true,null,null,ActivityTargetParticipation.ReceptionAtArrival)},old.Places,old.Calibration);
            return new DecisionSnapshot(new ActivityDailyWindow(new ActivityDayStamp(1,1,1),new[]{"A"},new[]{"A"},"op:"+minute,minute),
                new[]{new DecisionPersonSnapshot("A",null,missedType:"Talk",missedTarget:"T",missedMinute:missed),new DecisionPersonSnapshot("T",null)},null,null,catalog,120);
        }
        [Test] public void ReceptionCandidate_TargetUnavailableInSnapshot_DoesNotClaimOrRemoteAccept()
        {
            var s=Snapshot(420);var c=new PrototypeActivityProvider().Collect(s,"A").Single();
            Assert.AreEqual("T",c.TargetPersonId);CollectionAssert.AreEqual(new[]{"A"},DesireEvaluator.Participants(c));
            Assert.AreEqual("Eligible",DesireEvaluator.Eligibility(s,c));
            Assert.AreEqual(AcceptanceReason.ReceptionDeferred,new MinimalAcceptance().Evaluate(s,new Proposal(s.RoundIdentity,c)).Reason);
        }
        [Test] public void MissedSameTargetType_SuppressedUntilConfiguredCooldown_NotHistoryUtility()
        {
            Assert.IsEmpty(new PrototypeActivityProvider().Collect(Snapshot(539,420),"A"));
            Assert.AreEqual(1,new PrototypeActivityProvider().Collect(Snapshot(540,420),"A").Count());
        }
    }
}

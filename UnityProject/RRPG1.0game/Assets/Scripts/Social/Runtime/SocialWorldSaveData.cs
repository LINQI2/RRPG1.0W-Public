using System;
namespace RRPG.Social
{
    [Serializable]
    public sealed class SocialWorldSaveData
    {
        public RelationalFactSaveData[] RelationalFacts;
        public DirectedRelationshipSaveData[] Relationships;
    }
    [Serializable]
    public sealed class RelationalFactSaveData
    {
        public string SourceOccurrenceId;
        public string FactType;
        public string ActorPersonId;
        public string TargetPersonId;
        public int Year;
        public int Month;
        public int Day;
    }
    [Serializable]
    public sealed class DirectedRelationshipSaveData
    {
        public string SourcePersonId;
        public string TargetPersonId;
        // V0.1 Test / Not Frozen relationship channel.
        public float Affinity;
    }
}

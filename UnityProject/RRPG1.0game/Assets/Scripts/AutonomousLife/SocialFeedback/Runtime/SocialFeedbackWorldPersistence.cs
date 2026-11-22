using System;
using System.Collections.Generic;
using RRPG.Social;

namespace RRPG.AutonomousSocialFeedback
{
    [Serializable]
    public sealed class SocialFeedbackWorldSaveData
    {
        public SocialFeedbackEntrySaveData[] Outbox;
    }
    [Serializable]
    public sealed class SocialFeedbackEntrySaveData
    {
        public string SourceOccurrenceId;
        public string FactType;
        public string ActorPersonId;
        public string TargetPersonId;
        public int Year;
        public int Month;
        public int Day;
        public string Status;
        public string FaultCode;
    }
    public static class SocialFeedbackWorldPersistence
    {
        public static SocialFeedbackWorldSaveData Build(SocialFeedbackWorldState world)
        {
            var rows = new List<SocialFeedbackEntrySaveData>();
            if (world != null) foreach (var e in world.Entries)
                rows.Add(new SocialFeedbackEntrySaveData { SourceOccurrenceId = e.Fact.SourceOccurrenceId,
                    FactType = e.Fact.FactType, ActorPersonId = e.Fact.ActorPersonId, TargetPersonId = e.Fact.TargetPersonId,
                    Year = e.Fact.Year, Month = e.Fact.Month, Day = e.Fact.Day, Status = e.Status.ToString(), FaultCode = e.FaultCode });
            return new SocialFeedbackWorldSaveData { Outbox = rows.ToArray() };
        }
        public static bool IsValid(SocialFeedbackWorldSaveData data) => TryRead(data, out _);
        public static bool Restore(SocialFeedbackWorldState world, SocialFeedbackWorldSaveData data)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!TryRead(data, out var entries)) return false;
            world.Replace(entries);
            return true;
        }
        private static bool TryRead(SocialFeedbackWorldSaveData data, out List<SocialFeedbackEntry> entries)
        {
            entries = new List<SocialFeedbackEntry>();
            if (data?.Outbox == null) return true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in data.Outbox)
            {
                if (row == null) return false;
                var fact = new RelationalFactRecord(row.SourceOccurrenceId, row.FactType, row.ActorPersonId, row.TargetPersonId, row.Year, row.Month, row.Day);
                if (!SocialFeedbackWorldState.Valid(fact) || !ids.Add(row.SourceOccurrenceId)
                    || (row.Status != "Pending" && row.Status != "Faulted")
                    || (row.Status == "Pending" && !string.IsNullOrEmpty(row.FaultCode))
                    || (row.Status == "Faulted" && string.IsNullOrWhiteSpace(row.FaultCode))) return false;
                entries.Add(new SocialFeedbackEntry(fact, row.Status == "Pending" ? FeedbackStatus.Pending : FeedbackStatus.Faulted, row.FaultCode ?? ""));
            }
            return true;
        }
    }
}

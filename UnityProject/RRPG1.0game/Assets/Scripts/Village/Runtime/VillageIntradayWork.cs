using System;
using System.Collections.Generic;

/// <summary>Injectable prototype schedule; minutes are read from VillageTime, never advanced here.</summary>
public sealed class VillageWorkSchedule
{
    public readonly int MorningStart, Lunch, AfternoonStart, WorkEnd, CommuteMinutes;
    public readonly double FulfilledFraction;
    public VillageWorkSchedule(int morningStart = 480, int lunch = 720, int afternoonStart = 780,
        int workEnd = 1020, int commuteMinutes = 15, double fulfilledFraction = 1)
    {
        if (morningStart < 0 || morningStart >= lunch || lunch >= afternoonStart || afternoonStart >= workEnd
            || workEnd >= 1440 || commuteMinutes < 0 || commuteMinutes > morningStart
            || double.IsNaN(fulfilledFraction) || fulfilledFraction <= 0 || fulfilledFraction > 1)
            throw new ArgumentException("Invalid work schedule calibration");
        MorningStart=morningStart;Lunch=lunch;AfternoonStart=afternoonStart;WorkEnd=workEnd;
        CommuteMinutes=commuteMinutes;FulfilledFraction=fulfilledFraction;
    }
    public bool IsWorking(int minute) => minute >= MorningStart && minute < Lunch
        || minute >= AfternoonStart && minute < WorkEnd;
}

[Serializable] public sealed class VillageWorkBatchSaveData
{
    public string WorkplaceId;
    public long LaborNumerator;
    public bool InputPaid;
}
[Serializable] public sealed class VillageWorkEvidenceSaveData
{
    public string PersonId, WorkplaceId;
    public int Minutes;
}
[Serializable] public sealed class VillageWorkArrivalSaveData
{
    public string PersonId, Destination;
    public long ReadyMinute;
}
[Serializable] public sealed class VillageIntradayWorkSaveData
{
    public int SchemaVersion = 1;
    public long LastLunchMinute = -1;
    public List<VillageWorkBatchSaveData> Batches = new List<VillageWorkBatchSaveData>();
    public List<VillageWorkEvidenceSaveData> Evidence = new List<VillageWorkEvidenceSaveData>();
    public List<VillageWorkArrivalSaveData> Arrivals = new List<VillageWorkArrivalSaveData>();
    public List<string> UnderfedAtLunch = new List<string>();
}

/// <summary>Workplace batches, actual labor and noon evidence; no Person/Clock authority duplication.</summary>
public sealed class VillageIntradayWork
{
    private VillageIntradayWorkSaveData _state = new VillageIntradayWorkSaveData();
    public VillageWorkSchedule Schedule { get; }
    public long LastLunchMinute => _state.LastLunchMinute;
    public VillageIntradayWork(VillageWorkSchedule schedule = null) { Schedule = schedule ?? new VillageWorkSchedule(); }
    public static bool Validate(VillageIntradayWorkSaveData data, out string error)
    {
        error = null;
        if (data == null) return true;
        if (data.SchemaVersion != 1 || data.LastLunchMinute < -1 || data.LastLunchMinute > WorldInstant.MaximumMinute)
            { error = "InvalidIntradayWorkHeader"; return false; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (data.Batches != null) foreach (var b in data.Batches)
            if (b == null || string.IsNullOrWhiteSpace(b.WorkplaceId) || !keys.Add(b.WorkplaceId)
                || b.LaborNumerator < 0 || b.LaborNumerator >= 480)
                { error = "InvalidWorkBatch"; return false; }
        keys.Clear();
        if (data.Evidence != null) foreach (var e in data.Evidence)
            if (e == null || string.IsNullOrWhiteSpace(e.PersonId) || string.IsNullOrWhiteSpace(e.WorkplaceId)
                || e.Minutes <= 0 || e.Minutes > 480 || !keys.Add(e.PersonId + "\n" + e.WorkplaceId))
                { error = "InvalidWorkEvidence"; return false; }
        keys.Clear();
        if (data.Arrivals != null) foreach (var a in data.Arrivals)
            if (a == null || string.IsNullOrWhiteSpace(a.PersonId) || string.IsNullOrWhiteSpace(a.Destination)
                || a.ReadyMinute < 0 || a.ReadyMinute > WorldInstant.MaximumMinute || !keys.Add(a.PersonId))
                { error = "InvalidWorkArrival"; return false; }
        keys.Clear();
        if (data.UnderfedAtLunch != null) foreach (string id in data.UnderfedAtLunch)
            if (string.IsNullOrWhiteSpace(id) || !keys.Add(id)) { error = "InvalidLunchEvidence"; return false; }
        return true;
    }
    public bool Restore(VillageIntradayWorkSaveData data)
    {
        if (!Validate(data, out _)) return false;
        _state = Copy(data ?? new VillageIntradayWorkSaveData()); return true;
    }
    public VillageIntradayWorkSaveData ToSaveData() => Copy(_state);
    private static VillageIntradayWorkSaveData Copy(VillageIntradayWorkSaveData data)
    {
        var copy = new VillageIntradayWorkSaveData { LastLunchMinute = data.LastLunchMinute };
        if (data.Batches != null) foreach (var b in data.Batches) copy.Batches.Add(new VillageWorkBatchSaveData
            { WorkplaceId = b.WorkplaceId, LaborNumerator = b.LaborNumerator, InputPaid = b.InputPaid });
        if (data.Evidence != null) foreach (var e in data.Evidence) copy.Evidence.Add(new VillageWorkEvidenceSaveData
            { PersonId = e.PersonId, WorkplaceId = e.WorkplaceId, Minutes = e.Minutes });
        if (data.Arrivals != null) foreach (var a in data.Arrivals) copy.Arrivals.Add(new VillageWorkArrivalSaveData
            { PersonId = a.PersonId, Destination = a.Destination, ReadyMinute = a.ReadyMinute });
        if (data.UnderfedAtLunch != null) copy.UnderfedAtLunch.AddRange(data.UnderfedAtLunch);
        copy.Batches.Sort((a,b) => StringComparer.Ordinal.Compare(a.WorkplaceId,b.WorkplaceId));
        copy.Evidence.Sort((a,b) => { int c = StringComparer.Ordinal.Compare(a.PersonId,b.PersonId);
            return c != 0 ? c : StringComparer.Ordinal.Compare(a.WorkplaceId,b.WorkplaceId); });
        copy.Arrivals.Sort((a,b) => StringComparer.Ordinal.Compare(a.PersonId,b.PersonId));
        copy.UnderfedAtLunch.Sort(StringComparer.Ordinal); return copy;
    }
    public bool Arrived(string personId, string destination, long now)
    {
        var a = _state.Arrivals.Find(x => x.PersonId == personId);
        if (a == null) { a = new VillageWorkArrivalSaveData { PersonId = personId }; _state.Arrivals.Add(a); }
        if (a.Destination != destination) { a.Destination = destination; a.ReadyMinute = checked(now + Schedule.CommuteMinutes); }
        return now >= a.ReadyMinute;
    }
    public bool HasArrived(string personId, string destination, long now)
    {
        var a = _state.Arrivals.Find(x => x.PersonId == personId);
        return a != null && a.Destination == destination && now >= a.ReadyMinute;
    }
    public void Leave(string personId) => _state.Arrivals.RemoveAll(x => x.PersonId == personId);
    public WorldInstant NextBoundary(WorldInstant from)
    {
        long baseMinute = from.Minute - from.MinuteOfDay, next = baseMinute + 1440;
        foreach (int offset in new[] { Schedule.MorningStart - Schedule.CommuteMinutes, Schedule.MorningStart,
            Schedule.Lunch, Schedule.AfternoonStart, Schedule.WorkEnd })
            if (baseMinute + offset > from.Minute) next = Math.Min(next, baseMinute + offset);
        foreach (var a in _state.Arrivals) if (a.ReadyMinute > from.Minute) next = Math.Min(next,a.ReadyMinute);
        return new WorldInstant(next);
    }
    public long MinutesUntilBatch(VillageResourceState resources, VillageResourceWorkplaceDef def, int workers)
    {
        if (workers <= 0) return long.MaxValue;
        int rate = def.Role == VillageResourceRole.Farm ? def.PerWorkerDailyOutput : def.ProcessingUnitsPerWorkerPerDay;
        if (rate <= 0) return long.MaxValue;
        var batch = _state.Batches.Find(b => b.WorkplaceId == def.WorkplaceId);
        if (def.Role == VillageResourceRole.Kitchen && (batch == null || !batch.InputPaid)
            && resources.Ingredients < def.InputIngredientsPerUnit) return long.MaxValue;
        long remaining = 480 - (batch?.LaborNumerator ?? 0);
        return Math.Max(1,(remaining + (long)workers * rate - 1) / ((long)workers * rate));
    }
    public bool CanProcess(VillageResourceState resources, VillageResourceWorkplaceDef def)
    {
        var batch = _state.Batches.Find(b => b.WorkplaceId == def.WorkplaceId);
        if (def.Role == VillageResourceRole.Farm) return def.PerWorkerDailyOutput > 0;
        if (def.InputIngredientsPerUnit <= 0 || def.OutputFoodPerUnit <= 0 || def.ProcessingUnitsPerWorkerPerDay <= 0) return false;
        return batch != null && batch.InputPaid
            || resources.Ingredients >= def.InputIngredientsPerUnit;
    }
    /// <summary>Reserve the first batch input at interval start, before any interval output exists.</summary>
    public bool TryBeginBatch(VillageResourceState resources, VillageResourceWorkplaceDef def)
    {
        if (!CanProcess(resources,def)) return false;
        if (def.Role == VillageResourceRole.Farm) return true;
        var batch = _state.Batches.Find(b => b.WorkplaceId == def.WorkplaceId);
        if (batch != null && batch.InputPaid) return true;
        if (!resources.TryConsumeIngredients(def.InputIngredientsPerUnit)) return false;
        if (batch == null) { batch = new VillageWorkBatchSaveData { WorkplaceId = def.WorkplaceId }; _state.Batches.Add(batch); }
        batch.InputPaid = true; return true;
    }
    public void Work(string personId, string workplaceId, int minutes)
    {
        if (minutes <= 0) return;
        var evidence = _state.Evidence.Find(e => e.PersonId == personId && e.WorkplaceId == workplaceId);
        if (evidence == null) { evidence = new VillageWorkEvidenceSaveData { PersonId = personId, WorkplaceId = workplaceId }; _state.Evidence.Add(evidence); }
        evidence.Minutes = checked(evidence.Minutes + minutes);
    }
    public List<VillageDailyExecutionFact> TakeDailyEvidence()
    {
        var result = new List<VillageDailyExecutionFact>();
        foreach (var e in ToSaveData().Evidence) result.Add(new VillageDailyExecutionFact
            { PersonId = e.PersonId, WorkplaceId = e.WorkplaceId, WorkedMinutes = e.Minutes });
        _state.Evidence.Clear(); return result;
    }
    public void ProcessBatch(VillageResourceState resources, VillageResourceWorkplaceDef def, int workerMinutes)
    {
        if (resources == null || def == null || workerMinutes <= 0) return;
        int rate = def.Role == VillageResourceRole.Farm ? def.PerWorkerDailyOutput : def.ProcessingUnitsPerWorkerPerDay;
        if (rate <= 0) return;
        var batch = _state.Batches.Find(b => b.WorkplaceId == def.WorkplaceId);
        if (batch == null) { batch = new VillageWorkBatchSaveData { WorkplaceId = def.WorkplaceId }; _state.Batches.Add(batch); }
        long available = checked((long)workerMinutes * rate);
        while (available > 0)
        {
            if (def.Role == VillageResourceRole.Kitchen && !batch.InputPaid)
            {
                if (!resources.TryConsumeIngredients(def.InputIngredientsPerUnit)) return;
                batch.InputPaid = true;
            }
            long used = Math.Min(available,480 - batch.LaborNumerator);
            batch.LaborNumerator += used; available -= used;
            if (batch.LaborNumerator < 480) return;
            batch.LaborNumerator = 0;
            if (def.Role == VillageResourceRole.Farm) resources.AddIngredients(1);
            else { resources.AddFood(def.OutputFoodPerUnit); batch.InputPaid = false; }
        }
    }
    public VillageResourceDayResult Lunch(long minute, VillageResourceState resources,
        IReadOnlyList<IFulfillmentHost> persons, int demand, int emergencyPerShare, int hungerThreshold)
    {
        if (_state.LastLunchMinute == minute) return null;
        if (minute % 1440 != Schedule.Lunch || resources == null) throw new ArgumentException("Invalid lunch boundary");
        var hosts = new SortedDictionary<string, IFulfillmentHost>(StringComparer.Ordinal);
        if (persons != null) foreach (var p in persons)
            if (p != null && !string.IsNullOrWhiteSpace(p.PersonId)) hosts[p.PersonId] = p;
        var result = new VillageResourceDayResult();
        long total = checked((long)Math.Max(0,demand) * hosts.Count);
        int food = (int)Math.Min(total,resources.Food);
        long missing = total - food;
        int ingredients = emergencyPerShare > 0 && demand > 0
            ? (int)Math.Min(resources.Ingredients,(missing * emergencyPerShare + demand - 1) / demand) : 0;
        double fraction = total == 0 ? 1 : Math.Min(1,(food + (double)ingredients * demand / Math.Max(1,emergencyPerShare)) / total);
        resources.TryConsumeFood(food); resources.TryConsumeIngredients(ingredients);
        _state.LastLunchMinute = minute; _state.UnderfedAtLunch.Clear();
        foreach (var host in hosts.Values)
        {
            if (fraction + 1e-12 >= Schedule.FulfilledFraction) { host.Hungry = false; host.ConsecutiveUnderfedDays = 0; }
            else { host.ConsecutiveUnderfedDays = host.ConsecutiveUnderfedDays == int.MaxValue ? int.MaxValue : host.ConsecutiveUnderfedDays + 1;
                host.Hungry = host.ConsecutiveUnderfedDays >= Math.Max(1,hungerThreshold); _state.UnderfedAtLunch.Add(host.PersonId); }
        }
        result.FoodConsumed = food; result.EmergencyIngredientsConsumed = ingredients;
        result.PersonsUnderfed = _state.UnderfedAtLunch.Count; result.PersonsFoodFed = hosts.Count - result.PersonsUnderfed;
        return result;
    }
    public void ApplyExpeditionNutrition(string personId, long returnMinute, IFulfillmentHost person, bool supplied)
    {
        if (!supplied || person == null || person.PersonId != personId || _state.LastLunchMinute < 0
            || _state.LastLunchMinute / 1440 != returnMinute / 1440 || !_state.UnderfedAtLunch.Remove(personId)) return;
        person.Hungry = false; person.ConsecutiveUnderfedDays = 0;
    }
}

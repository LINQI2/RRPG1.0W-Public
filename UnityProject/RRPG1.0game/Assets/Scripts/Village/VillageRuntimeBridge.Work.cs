using System;
using System.Collections.Generic;
using System.Linq;

public static partial class VillageRuntimeBridge
{
    public static VillageIntradayWork IntradayWork { get; private set; }
    private static int _observedProjectCompletions;
    private static VillageResourceDayResult _workDayProduction = new VillageResourceDayResult();

    private static List<string> PresentWorkPersons()
    {
        var result = new List<string>();
        if (SimulationPersonSource == null) return result;
        foreach (var p in VillagePersonAvailability.Resolve(BuildPersonInputs(SimulationPersonSource),_expeditionPersonIds))
            if (p.Availability == VillageAvailability.InVillage && !_unavailablePersonIds.Contains(p.PersonId)) result.Add(p.PersonId);
        result.Sort(StringComparer.Ordinal); return result;
    }
    private static string WorkDestination(string personId)
    {
        var p=Projects?.GetActiveProjectOfPerson(personId);
        return p==null ? Assignments?.GetWorkplaceId(personId) : ProjectDestination(p.ProjectId);
    }
    public static bool IsExecutingWork(string personId) => Time != null && IntradayWork != null
        && IntradayWork.Schedule.IsWorking(Time.MinuteOfDay) && PresentWorkPersons().Contains(personId)
        && ActivityRuntimeBridge.QueryClaim(personId)==null
        && IntradayWork.HasArrived(personId,WorkDestination(personId),Time.CurrentInstant.Minute);
    public static string GetWorkExecutionLabel(string personId)
    {
        if (_expeditionPersonIds.Contains(personId)) return "远征中（保留职责）";
        if (!PresentWorkPersons().Contains(personId)) return "不可用";
        if (string.IsNullOrEmpty(WorkDestination(personId))) return "空闲";
        if (Time.MinuteOfDay >= IntradayWork.Schedule.Lunch && Time.MinuteOfDay < IntradayWork.Schedule.AfternoonStart) return "午休";
        if (!IntradayWork.Schedule.IsWorking(Time.MinuteOfDay)) return "下班 / 休息";
        if (!IsExecutingWork(personId)) return "通勤";
        return Projects.GetActiveProjectOfPerson(personId) != null ? "项目劳动" : "岗位劳动";
    }
    public static string GetDepartureOccupancyReason(string personId)
    {
        if (_expeditionPersonIds.Contains(personId)) return "正在远征中（Temporary Occupancy）";
        if (_unavailablePersonIds.Contains(personId)) return "暂不可用（Unavailable）";
        return ""; // Project membership is retained while its execution pauses for expedition.
    }
    public static void ApplyReturnedExpeditionNutrition(PlayerCollection persons, bool supplied)
    {
        if (!supplied || persons == null || IntradayWork == null || Time == null || _returnRecord == null
            || _returnRecord.Phase != ExpeditionReturnPhase.CatchUpComplete) return;
        foreach (string id in _expeditionPersonIds)
        {
            IFulfillmentHost host = persons.GetCharacterByPersonId(id) as IFulfillmentHost
                ?? persons.GetHeroByPersonId(id) as IFulfillmentHost;
            IntradayWork.ApplyExpeditionNutrition(id,Time.CurrentInstant.Minute,host,true);
        }
    }

    private static List<string> ArrivedWorkers(long minute, bool prepare)
    {
        var workers = new List<string>();
        var present = new HashSet<string>(PresentWorkPersons(),StringComparer.Ordinal);
        if (prepare) foreach (var a in IntradayWork.ToSaveData().Arrivals) if (!present.Contains(a.PersonId)) IntradayWork.Leave(a.PersonId);
        foreach (var id in present)
        {
            if(ActivityRuntimeBridge.QueryClaim(id)!=null) continue;
            string destination = WorkDestination(id);
            if (string.IsNullOrEmpty(destination)) { if (prepare) IntradayWork.Leave(id); continue; }
            if (prepare ? IntradayWork.Arrived(id,destination,minute) : IntradayWork.HasArrived(id,destination,minute)) workers.Add(id);
        }
        return workers;
    }
    private static WorldInstant? NextWorkBoundary(WorldInstant from, WorldInstant target)
    {
        if (IntradayWork == null) return null;
        int minute = from.MinuteOfDay;
        var schedule = IntradayWork.Schedule;
        var workers = ArrivedWorkers(from.Minute,false);
        long next = IntradayWork.NextBoundary(from).Minute;
        if (schedule.IsWorking(minute))
        {
            int ingredients = Resources.Ingredients;
            var batches = IntradayWork.ToSaveData().Batches;
            foreach (var def in _resourceWorkplaceDefs.OrderBy(d=>d.WorkplaceId,StringComparer.Ordinal))
            {
                int count = 0;
                foreach (var id in workers) if (Projects.GetActiveProjectOfPerson(id) == null
                    && Assignments.GetWorkplaceId(id) == def.WorkplaceId) count++;
                if(count<=0 || !IntradayWork.CanProcess(Resources,def)) continue;
                if(def.Role==VillageResourceRole.Kitchen && !batches.Any(b=>b.WorkplaceId==def.WorkplaceId && b.InputPaid))
                {
                    if(ingredients<def.InputIngredientsPerUnit) continue;
                    ingredients-=def.InputIngredientsPerUnit;
                }
                long duration = IntradayWork.MinutesUntilBatch(Resources,def,count);
                if (duration != long.MaxValue) next = Math.Min(next,checked(from.Minute + duration));
            }
            foreach (var project in Projects.ActiveProjects)
            {
                var def = Projects.GetProjectDef(project.ProjectDefId); int count = 0;
                foreach (var id in project.ParticipantPersonIds) if (workers.Contains(id)) count++;
                long rate = (long)count * def.TestProgressPerParticipantPerDay;
                if (rate > 0)
                {
                    long remaining = (long)(def.TestProgressTarget - project.Progress) * 480 - project.LaborRemainder;
                    next = Math.Min(next,checked(from.Minute + Math.Max(1,(remaining + rate - 1) / rate)));
                }
            }
        }
        return next <= target.Minute ? new WorldInstant(next) : (WorldInstant?)null;
    }
    private static void CompleteWorkInterval(WorldInstant from, WorldInstant to)
    {
        if (IntradayWork == null || !IntradayWork.Schedule.IsWorking(from.MinuteOfDay)) return;
        int minutes = checked((int)(to.Minute - from.Minute));
        var workers = ArrivedWorkers(from.Minute,true);
        var projectWorkers = new List<string>();
        var assignments = new Dictionary<string,List<string>>(StringComparer.Ordinal);
        foreach (var id in workers)
        {
            if (Projects.GetActiveProjectOfPerson(id) != null) { projectWorkers.Add(id); continue; }
            string workplace = Assignments.GetWorkplaceId(id);
            if (workplace == null) continue;
            if (!assignments.TryGetValue(workplace,out var ids)) assignments[workplace] = ids = new List<string>();
            ids.Add(id);
        }
        var processing = new HashSet<string>(StringComparer.Ordinal);
        // Same deterministic input allocation as NextWorkBoundary; only actual arrived workers can pay.
        foreach (var def in _resourceWorkplaceDefs.OrderBy(d=>d.WorkplaceId,StringComparer.Ordinal))
            if (assignments.ContainsKey(def.WorkplaceId))
            {
                int ingredients=Resources.Ingredients;
                if(IntradayWork.TryBeginBatch(Resources,def)) processing.Add(def.WorkplaceId);
                _workDayProduction.IngredientsConsumedByKitchen+=ingredients-Resources.Ingredients;
            }
        int completionsBefore=Projects.CompletedProjects.Count;
        Projects.OnWorkInterval(minutes,projectWorkers);
        ObserveProjectCompletions();
        if(Projects.CompletedProjects.Count!=completionsBefore) RefreshWorkIntent();
        foreach (var pair in assignments.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            var def = _resourceWorkplaceDefs.Find(d => d.WorkplaceId == pair.Key);
            if (def != null && !processing.Contains(pair.Key)) continue;
            if (def != null)
            {
                int ingredients=Resources.Ingredients,food=Resources.Food;
                IntradayWork.ProcessBatch(Resources,def,checked(minutes * pair.Value.Count));
                if (def.Role == VillageResourceRole.Farm) _workDayProduction.IngredientsProduced += Resources.Ingredients-ingredients;
                else { _workDayProduction.FoodProduced += Resources.Food-food;
                    _workDayProduction.IngredientsConsumedByKitchen += ingredients-Resources.Ingredients; }
            }
            foreach (var id in pair.Value) IntradayWork.Work(id,pair.Key,minutes);
        }
    }
    private static void CompleteWorkBoundary(WorldInstant boundary)
    {
        if (IntradayWork == null) return;
        if (boundary.MinuteOfDay == IntradayWork.Schedule.MorningStart - IntradayWork.Schedule.CommuteMinutes
            || boundary.MinuteOfDay == IntradayWork.Schedule.MorningStart || boundary.MinuteOfDay == IntradayWork.Schedule.AfternoonStart)
            ArrivedWorkers(boundary.Minute,true);
        foreach(var id in ArrivedWorkers(boundary.Minute,false))
            if(IntradayWork.Schedule.IsWorking(boundary.MinuteOfDay))
                ActivityRuntimeBridge.RecordActualPresence(id,WorkDestination(id),boundary.Minute);
        if (boundary.MinuteOfDay == IntradayWork.Schedule.Lunch)
        {
            if (SimulationPersonSource == null) { WorldAdvanceDiagnostic = "MissingLunchPersonSource"; return; }
            var persons = BuildInVillageFulfillmentHosts();
            persons.RemoveAll(p => _unavailablePersonIds.Contains(p.PersonId));
            LastResourceDayResult = IntradayWork.Lunch(boundary.Minute,Resources,persons,
                ResourceFulfillmentThresholdPerPerson,ResourceEmergencyIngredientsPerShare,ResourceRequiredConsecutiveUnderfedDays);
            LastResourceInVillageCount = persons.Count;
        }
        if (boundary.MinuteOfDay == IntradayWork.Schedule.WorkEnd)
            foreach (var a in IntradayWork.ToSaveData().Arrivals) IntradayWork.Leave(a.PersonId);
    }
    private static void RefreshWorkIntent()
    {
        if (IntradayWork != null && Time != null && Time.MinuteOfDay >= IntradayWork.Schedule.MorningStart - IntradayWork.Schedule.CommuteMinutes
            && Time.MinuteOfDay < IntradayWork.Schedule.WorkEnd) ArrivedWorkers(Time.CurrentInstant.Minute,true);
    }
    private static void ObserveProjectCompletions()
    {
        if (Projects == null) return;
        for (; _observedProjectCompletions < Projects.CompletedProjects.Count; _observedProjectCompletions++)
        {
            var p = Projects.CompletedProjects[_observedProjectCompletions];
            if(FinalizeConstruction(p)) continue;
            var def = Projects.GetProjectDef(p.ProjectDefId);
            World?.AppendCompletedProjectSite(p.ProjectId,p.ProjectDefId,def?.DisplayName ?? p.ProjectDefId);
            VillageCapabilityGrant.GrantForCompletedProject(p.ParticipantPersonIds,p.ProjectDefId);
        }
    }
    private static void FinishWorkDayDiagnostics()
    {
        if (LastResourceDayResult == null || IntradayWork.LastLunchMinute < 0
            || IntradayWork.LastLunchMinute / 1440 != (Time.CurrentInstant.Minute - 1) / 1440)
            LastResourceDayResult = new VillageResourceDayResult();
        LastResourceDayResult.IngredientsProduced = _workDayProduction.IngredientsProduced;
        LastResourceDayResult.FoodProduced = _workDayProduction.FoodProduced;
        LastResourceDayResult.IngredientsConsumedByKitchen = _workDayProduction.IngredientsConsumedByKitchen;
        _workDayProduction = new VillageResourceDayResult();
    }
}

using RRPG.StaticConfig;
using System;
using System.Collections.Generic;
using System.Linq;
public static partial class VillageRuntimeBridge
{
    private static bool _stagingBuildingLoad;
    internal sealed class BuildingRestorePlan
    {
        internal BuildingRuntime Buildings;
        internal VillageAssignmentSystem Assignments;
        internal VillageProjectSystem Projects;
        internal VillageWorldRegistry World;
        internal VillageIntradayWork Work;
        internal List<VillageResourceWorkplaceDef> ResourceWorkplaces;
    }
    public static string ConstructionPlaceId(string buildingId) => "construction:"+buildingId;
    public static string BuildingWorkplaceId(string buildingId,string slot) => "workplace:"+buildingId+":"+slot;
    private static BuildingInstance BuildingForProject(string projectId) => Buildings.All.FirstOrDefault(b=>b.ConstructionProjectId==projectId);
    public static string ProjectDestination(string projectId) => BuildingForProject(projectId) is BuildingInstance b ? ConstructionPlaceId(b.InstanceId) : projectId;

    private static List<VillageWorkplaceDef> FunctionWorkplaces(BuildingInstance b,BuildingRegistry definitions)
    {
        var result=new List<VillageWorkplaceDef>();
        foreach(var f in definitions.FunctionBindings.Where(f=>f.BuildingDefinitionId==b.BuildingDefinitionId))
        {
            var profile=_testWorkplaces.FirstOrDefault(d=>d.WorkplaceId==f.FunctionProfileId);
            if(profile==null) throw new InvalidOperationException("Unknown Workplace profile: "+f.FunctionProfileId);
            result.Add(new VillageWorkplaceDef { WorkplaceId=BuildingWorkplaceId(b.InstanceId,f.FunctionSlotId),
                DisplayName=definitions.All.First(d=>d.BuildingDefinitionId==b.BuildingDefinitionId).DisplayName+" · "+b.InstanceId,
                Capacity=profile.Capacity,TestContributionPerDay=profile.TestContributionPerDay });
        }
        return result;
    }
    private static List<VillageResourceWorkplaceDef> FunctionResourceWorkplaces(BuildingInstance b,BuildingRegistry definitions)
    {
        var result=new List<VillageResourceWorkplaceDef>();
        foreach(var f in definitions.FunctionBindings.Where(f=>f.BuildingDefinitionId==b.BuildingDefinitionId))
        {
            var profile=_resourceWorkplaceProfiles.FirstOrDefault(d=>d.WorkplaceId==f.FunctionProfileId);
            if(profile==null) continue; // A legal non-resource Workplace remains a normal Workplace.
            result.Add(new VillageResourceWorkplaceDef { WorkplaceId=BuildingWorkplaceId(b.InstanceId,f.FunctionSlotId),
                Role=profile.Role,PerWorkerDailyOutput=profile.PerWorkerDailyOutput,
                InputIngredientsPerUnit=profile.InputIngredientsPerUnit,OutputFoodPerUnit=profile.OutputFoodPerUnit,
                ProcessingUnitsPerWorkerPerDay=profile.ProcessingUnitsPerWorkerPerDay });
        }
        return result;
    }
    public static VillageProjectResult TryAddConstructionWorker(string buildingId,string personId)
    {
        if(!GuardWorldWrite() || !CanSaveWorld) return VillageProjectResult.PersonCurrentlyUnavailable;
        EnsureInitialized(null);
        var b=Buildings.Find(buildingId);
        if(b==null || b.LifecycleState!=BuildingLifecycleState.UnderConstruction) return VillageProjectResult.UnknownProject;
        if(!PresentWorkPersons().Contains(personId)) return VillageProjectResult.PersonCurrentlyUnavailable;
        if(!StaticConfigRegistry.TryGetBuildings(out var defs,out _)) return VillageProjectResult.UnknownProjectDef;
        FunctionWorkplaces(b,defs); // Validate completion inputs before accepting a commitment.
        bool created=false;
        if(string.IsNullOrEmpty(b.ConstructionProjectId))
        {
            var result=Projects.TryCreateProject("prj_test_01",out var p);
            if(result!=VillageProjectResult.Success) return result;
            b.ConstructionProjectId=p.ProjectId;created=true;
        }
        var add=TryAddProjectParticipant(b.ConstructionProjectId,personId);
        if(add!=VillageProjectResult.Success && created)
        {Projects.TryRemoveActiveProject(b.ConstructionProjectId);b.ConstructionProjectId=null;}
        World.EnsureConstructionPlace(b.InstanceId,defs.All.First(d=>d.BuildingDefinitionId==b.BuildingDefinitionId).DisplayName);
        return add;
    }
    public static VillageProjectResult TryRemoveConstructionWorker(string buildingId,string personId)
    {
        if(!GuardWorldWrite() || !CanSaveWorld) return VillageProjectResult.PersonCurrentlyUnavailable;
        var b=Buildings.Find(buildingId);
        if(b==null || string.IsNullOrEmpty(b.ConstructionProjectId)) return VillageProjectResult.UnknownProject;
        bool wasWorker=Projects.GetActiveProject(b.ConstructionProjectId)?.ParticipantPersonIds.Contains(personId)==true;
        var result=Projects.TryRemoveParticipant(b.ConstructionProjectId,personId);
        if(result==VillageProjectResult.Success && wasWorker) {IntradayWork.Leave(personId);RefreshWorkIntent();}
        return result;
    }
    private static bool FinalizeConstruction(VillageProject p)
    {
        var b=BuildingForProject(p.ProjectId);
        if(b==null) return false;
        if(!StaticConfigRegistry.TryGetBuildings(out var definitions,out string diagnostic)) throw new InvalidOperationException(diagnostic);
        var functions=FunctionWorkplaces(b,definitions);
        var resourceFunctions=FunctionResourceWorkplaces(b,definitions);
        foreach(var f in functions)
        {
            var existing=Assignments.GetWorkplaceDef(f.WorkplaceId);
            if(existing!=null && (existing.Capacity!=f.Capacity || existing.TestContributionPerDay!=f.TestContributionPerDay))
                throw new InvalidOperationException("Conflicting building workplace: "+f.WorkplaceId);
        }
        foreach(var f in functions) {Assignments.TryRegisterWorkplace(f);World.EnsureWorkplacePlace(f.WorkplaceId,f.DisplayName);}
        b.LifecycleState=BuildingLifecycleState.Completed;b.ConstructionProjectId=null;
        foreach(var f in resourceFunctions)
            if(!_resourceWorkplaceDefs.Any(d=>d.WorkplaceId==f.WorkplaceId)) _resourceWorkplaceDefs.Add(f);
        World.RetireConstructionPlace(b.InstanceId);
        foreach(var id in p.ParticipantPersonIds) IntradayWork.Leave(id);
        return true;
    }

    internal static bool TryPrepareBuildingWorld(VillageSaveData save,BuildingRuntime buildings,out BuildingRestorePlan plan,out string diagnostic)
    {
        plan=null;diagnostic="";
        try
        {
            BuildingRegistry defs=null;
            if(buildings.All.Count>0 && !StaticConfigRegistry.TryGetBuildings(out defs,out diagnostic)) return false;
            var projects=new VillageProjectSystem(_testProjects);projects.Restore(save?.Projects);
            var assignments=new VillageAssignmentSystem(Assignments?.WorkplaceDefs.Where(d=>!d.WorkplaceId.StartsWith("workplace:",StringComparison.Ordinal)).ToList() ?? _testWorkplaces);
            var world=new VillageWorldRegistry();world.EnsureWorkplacePlaces(assignments.WorkplaceDefs);world.Restore(save?.World);
            var bound=new HashSet<string>(StringComparer.Ordinal);
            var resources=VillageTestResourceWorkplaces.CreateDefaults();
            foreach(var b in buildings.All)
            {
                if(!string.IsNullOrEmpty(b.ConstructionProjectId))
                {
                    if(!bound.Add(b.ConstructionProjectId)) throw new InvalidOperationException("Duplicate ConstructionProject binding");
                    var source=save?.Projects?.ActiveProjects?.SingleOrDefault(p=>p.ProjectId==b.ConstructionProjectId);
                    if(source==null || source.ProjectDefId!="prj_test_01" || source.Progress<0 || source.LaborRemainder<0 || source.LaborRemainder>=480
                        || source.ParticipantPersonIds==null || source.ParticipantPersonIds.Count>2
                        || source.ParticipantPersonIds.Any(string.IsNullOrEmpty) || source.ParticipantPersonIds.Distinct().Count()!=source.ParticipantPersonIds.Count)
                        throw new InvalidOperationException("Invalid ConstructionProject binding / state");
                    var active=projects.GetActiveProject(b.ConstructionProjectId);
                    if(active==null)
                    {
                        if(!projects.IsProjectCompleted(b.ConstructionProjectId)) throw new InvalidOperationException("Missing ConstructionProject");
                        b.LifecycleState=BuildingLifecycleState.Completed;b.ConstructionProjectId=null;
                    }
                    else if(!active.ParticipantPersonIds.SequenceEqual(source.ParticipantPersonIds)) throw new InvalidOperationException("Conflicting construction workers");
                }
                if(b.LifecycleState==BuildingLifecycleState.Completed)
                {
                    foreach(var f in FunctionWorkplaces(b,defs)) {assignments.TryRegisterWorkplace(f);world.EnsureWorkplacePlace(f.WorkplaceId,f.DisplayName);}
                    resources.AddRange(FunctionResourceWorkplaces(b,defs));
                }
                else world.EnsureConstructionPlace(b.InstanceId,defs.All.First(d=>d.BuildingDefinitionId==b.BuildingDefinitionId).DisplayName);
            }
            // Dynamic references must never be silently discarded by the legacy restore policy.
            foreach(var r in save?.Assignments?.Records ?? new List<VillageAssignmentSaveData>())
                if(r!=null && r.WorkplaceId!=null && r.WorkplaceId.StartsWith("workplace:",StringComparison.Ordinal)
                    && assignments.GetWorkplaceDef(r.WorkplaceId)==null) throw new InvalidOperationException("Unknown runtime Workplace assignment: "+r.WorkplaceId);
            assignments.Restore(save?.Assignments);
            foreach(var r in save?.Assignments?.Records ?? new List<VillageAssignmentSaveData>())
                if(r?.WorkplaceId!=null && r.WorkplaceId.StartsWith("workplace:",StringComparison.Ordinal)
                    && assignments.GetWorkplaceId(r.PersonId)!=r.WorkplaceId)
                    throw new InvalidOperationException("Runtime Workplace assignment could not be restored: "+r.PersonId);
            foreach(var d in assignments.WorkplaceDefs)
                if(d.WorkplaceId.StartsWith("workplace:",StringComparison.Ordinal) && assignments.GetOccupantCount(d.WorkplaceId)>d.Capacity)
                    throw new InvalidOperationException("Runtime Workplace capacity exceeded: "+d.WorkplaceId);
            foreach(var batch in save?.IntradayWork?.Batches ?? new List<VillageWorkBatchSaveData>())
                if(batch.WorkplaceId.StartsWith("workplace:",StringComparison.Ordinal))
                {
                    var resource=resources.FirstOrDefault(d=>d.WorkplaceId==batch.WorkplaceId);
                    if(resource==null) throw new InvalidOperationException("Unknown runtime Resource Workplace batch: "+batch.WorkplaceId);
                    if(resource.Role==VillageResourceRole.Farm && batch.InputPaid
                        || resource.Role==VillageResourceRole.Kitchen && batch.LaborNumerator>0 && !batch.InputPaid)
                        throw new InvalidOperationException("Invalid runtime Resource Workplace batch: "+batch.WorkplaceId);
                }
            var work=new VillageIntradayWork();
            if(!work.Restore(save?.IntradayWork)) throw new InvalidOperationException("Invalid IntradayWork");
            plan=new BuildingRestorePlan {Buildings=buildings,Assignments=assignments,Projects=projects,World=world,Work=work,ResourceWorkplaces=resources};return true;
        }
        catch(Exception ex) {diagnostic=ex.Message;return false;}
    }
    internal static void CommitBuildingWorld(BuildingRestorePlan plan)
    {Buildings=plan.Buildings;Assignments=plan.Assignments;Projects=plan.Projects;World=plan.World;IntradayWork=plan.Work;
        _resourceWorkplaceDefs.Clear();_resourceWorkplaceDefs.AddRange(plan.ResourceWorkplaces);
        _observedProjectCompletions=Projects.CompletedProjects.Count;}
    public static BuildingRuntime Buildings { get; private set; }=new BuildingRuntime();
    public static bool TryPlaceBuilding(string definitionId,float x,float y,out BuildingInstance instance,out string diagnostic)
    {
        instance=null;
        if(!GuardWorldWrite() || !CanSaveWorld) { diagnostic="村庄正在结算或远征中";return false; }
        if(!StaticConfigRegistry.TryGetBuildings(out var definitions,out diagnostic)) return false;
        if(!Buildings.TryPlace(definitions,definitionId,x,y,out instance,out diagnostic)) return false;
        EnsureInitialized(null);World.EnsureConstructionPlace(instance.InstanceId,definitions.All.First(d=>d.BuildingDefinitionId==definitionId).DisplayName);return true;
    }
    internal static bool TryPrepareBuildings(VillageBuildingsSaveData data,out BuildingRuntime prepared,out string diagnostic)
    {
        BuildingRegistry definitions=null;
        if(data?.Instances!=null && data.Instances.Count>0 && !StaticConfigRegistry.TryGetBuildings(out definitions,out diagnostic))
        {prepared=null;return false;}
        return BuildingRuntime.TryPrepareRestore(data,definitions,out prepared,out diagnostic);
    }
    internal static void CommitBuildingsRestore(BuildingRuntime prepared) { Buildings=prepared; }
    internal static void ResetBuildingsForNewGame()
    {
        Buildings=new BuildingRuntime();
        _resourceWorkplaceDefs.Clear();_resourceWorkplaceDefs.AddRange(VillageTestResourceWorkplaces.CreateDefaults());
        if(Assignments==null) return;
        Assignments=new VillageAssignmentSystem(_testWorkplaces);
        Projects=new VillageProjectSystem(_testProjects);
        World=new VillageWorldRegistry();World.EnsureWorkplacePlaces(Assignments.WorkplaceDefs);
        IntradayWork?.Restore(null);_observedProjectCompletions=0;
    }
}

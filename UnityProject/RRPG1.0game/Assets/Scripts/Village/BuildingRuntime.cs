using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RRPG.StaticConfig;

public sealed class BuildingInstance
{
    public string InstanceId { get; }
    public string BuildingDefinitionId { get; }
    public float WorldX { get; }
    public float WorldY { get; }
    public BuildingLifecycleState LifecycleState { get; internal set; }
    public string ConstructionProjectId { get; internal set; }
    internal BuildingInstance(string id,string definition,float x,float y,BuildingLifecycleState state)
    { InstanceId=id;BuildingDefinitionId=definition;WorldX=x;WorldY=y;LifecycleState=state; }
    internal BuildingInstanceSaveData ToSaveData() => new BuildingInstanceSaveData {InstanceId=InstanceId,
        BuildingDefinitionId=BuildingDefinitionId,WorldX=WorldX,WorldY=WorldY,LifecycleState=LifecycleState,ConstructionProjectId=ConstructionProjectId};
}

/// <summary>C1 whitebox area, Test / Not Final. Centered axis-aligned footprints. No grid.</summary>
public static class BuildingPlacementValidator
{
    public const float MinX=-12,MaxX=12,MinY=-10,MaxY=8;
    internal static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    public static string Validate(BuildingRegistry definitions,string definitionId,float x,float y,IEnumerable<BuildingInstance> existing)
    {
        if(definitions==null || !definitions.TryGetById(definitionId,out var d)) return "建筑定义不可用";
        if(!Finite(x) || !Finite(y)) return "位置无效";
        if(x-d.FootprintWidth*.5f<MinX || x+d.FootprintWidth*.5f>MaxX || y-d.FootprintDepth*.5f<MinY || y+d.FootprintDepth*.5f>MaxY)
            return "占地超出建造范围";
        foreach(var b in existing)
        {
            if(!definitions.TryGetById(b.BuildingDefinitionId,out var other)) return "已有建筑定义不可用";
            if(Math.Abs((double)x-b.WorldX)<((double)d.FootprintWidth+other.FootprintWidth)*.5
                && Math.Abs((double)y-b.WorldY)<((double)d.FootprintDepth+other.FootprintDepth)*.5) return "占地与已有建筑重叠";
        }
        return string.Empty;
    }
}

/// <summary>Sole building instance owner. Placement revalidates current facts before allocating identity.</summary>
public sealed class BuildingRuntime
{
    private readonly List<BuildingInstance> _instances=new List<BuildingInstance>();
    private long _nextSequence=1;
    public IReadOnlyList<BuildingInstance> All => _instances.AsReadOnly();
    public BuildingInstance Find(string id) => _instances.FirstOrDefault(b=>b.InstanceId==id);
    public string Preview(BuildingRegistry definitions,string definitionId,float x,float y) => BuildingPlacementValidator.Validate(definitions,definitionId,x,y,_instances);
    public bool TryPlace(BuildingRegistry definitions,string definitionId,float x,float y,out BuildingInstance instance,out string diagnostic)
    {
        instance=null;diagnostic=Preview(definitions,definitionId,x,y);
        if(diagnostic.Length>0) return false;
        if(_nextSequence==long.MaxValue) { diagnostic="建筑身份分配已耗尽";return false; }
        instance=new BuildingInstance("building_instance_"+_nextSequence.ToString(CultureInfo.InvariantCulture),definitionId,x,y,BuildingLifecycleState.UnderConstruction);
        _nextSequence++;_instances.Add(instance);return true;
    }
    public VillageBuildingsSaveData ToSaveData() => new VillageBuildingsSaveData {NextSequence=_nextSequence,Instances=_instances.Select(b=>b.ToSaveData()).ToList()};
    public static bool TryPrepareRestore(VillageBuildingsSaveData data,BuildingRegistry definitions,out BuildingRuntime prepared,out string diagnostic)
    {
        prepared=null;diagnostic="";
        var candidate=new BuildingRuntime();
        if(data==null) { prepared=candidate;return true; }
        if(data.NextSequence<1 || data.Instances==null) {diagnostic="Invalid Buildings allocator/list";return false;}
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var b in data.Instances)
        {
            const string prefix="building_instance_";
            if(b==null || string.IsNullOrEmpty(b.InstanceId) || !b.InstanceId.StartsWith(prefix,StringComparison.Ordinal)
                || !long.TryParse(b.InstanceId.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out long sequence)
                || sequence<1 || b.InstanceId!=prefix+sequence.ToString(CultureInfo.InvariantCulture) || sequence>=data.NextSequence || !ids.Add(b.InstanceId)
                || !BuildingPlacementValidator.Finite(b.WorldX) || !BuildingPlacementValidator.Finite(b.WorldY)
                || !Enum.IsDefined(typeof(BuildingLifecycleState),b.LifecycleState))
            {diagnostic="Invalid BuildingInstance facts / allocator";return false;}
            if(definitions==null || !definitions.TryGetById(b.BuildingDefinitionId,out _))
            {diagnostic="Unknown BuildingDefinitionId: "+b.BuildingDefinitionId;return false;}
            if(b.LifecycleState==BuildingLifecycleState.Completed && !string.IsNullOrEmpty(b.ConstructionProjectId))
            {diagnostic="Completed building retains construction binding";return false;}
            candidate._instances.Add(new BuildingInstance(b.InstanceId,b.BuildingDefinitionId,b.WorldX,b.WorldY,b.LifecycleState) {ConstructionProjectId=b.ConstructionProjectId});
        }
        candidate._nextSequence=data.NextSequence;prepared=candidate;return true;
    }
}

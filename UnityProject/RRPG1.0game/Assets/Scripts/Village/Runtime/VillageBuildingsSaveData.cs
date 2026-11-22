using System;
using System.Collections.Generic;
public enum BuildingLifecycleState { UnderConstruction, Completed }
[Serializable]
public sealed class VillageBuildingsSaveData
{
    public long NextSequence=1;
    public List<BuildingInstanceSaveData> Instances=new List<BuildingInstanceSaveData>();
}
[Serializable]
public sealed class BuildingInstanceSaveData
{
    public string InstanceId;
    public string BuildingDefinitionId;
    public float WorldX;
    public float WorldY;
    public BuildingLifecycleState LifecycleState;
    public string ConstructionProjectId;
}

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct ChunksGlobalData : IComponentData
{
    public NativeHashMap<int2, Entity> Chunks;

    public JobHandle currentMeshHandle;
}

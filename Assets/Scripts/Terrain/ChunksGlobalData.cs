using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ChunksGlobalData : IComponentData
{
    public NativeHashMap<int2, Entity> Chunks;
}

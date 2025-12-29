using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct ChunkData : IComponentData
{
    // Normals and UVs not neeeded yet, since it's only being used for collider generation
    public NativeList<float3> Vertices;
    public NativeList<int> Triangles;
}
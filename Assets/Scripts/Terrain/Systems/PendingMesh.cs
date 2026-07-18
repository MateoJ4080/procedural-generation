using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct PendingMesh
{
    public Entity Entity;
    public JobHandle Handle;

    public NativeArray<Block> Blocks;

    public NativeList<float3> Vertices;
    public NativeList<float2> UVs;
    public NativeList<int> Triangles;
    public NativeList<float3> Normals;

    public NativeArray<Block> LeftArray;
    public NativeArray<Block> RightArray;
    public NativeArray<Block> BackArray;
    public NativeArray<Block> FrontArray;
}

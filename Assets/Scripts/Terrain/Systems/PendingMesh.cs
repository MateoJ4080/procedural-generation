using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct PendingMesh
{
    public Entity Entity;
    public JobHandle Handle;

    public NativeArray<Block> Blocks;

    // Render
    public NativeList<float3> RenderVertices;
    public NativeList<float2> RenderUVs;
    public NativeList<int> RenderTriangles;
    public NativeList<float3> RenderNormals;

    // Collider
    public NativeList<float3> ColliderVertices;
    public NativeList<int> ColliderTriangles;

    // Adjacent chunks
    public NativeArray<Block> LeftArray;
    public NativeArray<Block> RightArray;
    public NativeArray<Block> BackArray;
    public NativeArray<Block> FrontArray;

    public readonly void Dispose()
    {
        Blocks.Dispose();

        RenderVertices.Dispose();
        RenderUVs.Dispose();
        RenderTriangles.Dispose();
        RenderNormals.Dispose();

        if (LeftArray.IsCreated) LeftArray.Dispose();
        if (RightArray.IsCreated) RightArray.Dispose();
        if (FrontArray.IsCreated) FrontArray.Dispose();
        if (BackArray.IsCreated) BackArray.Dispose();
    }
}



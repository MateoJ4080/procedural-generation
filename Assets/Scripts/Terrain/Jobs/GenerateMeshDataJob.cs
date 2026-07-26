using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

[BurstCompile]
public struct GenerateMeshDataJob : IJob
{
    public int2 ChunkPos;
    public NativeArray<Block> BufferAsArray; // DynamicBuffer can't be used in jobs; NativeArray provides native blittable memory (needed by the Job System)
    public int Width;
    public int Height;
    public int Depth;

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

    public NativeList<Color32> RenderColors;

    private static readonly float2[] TopUVs =
    {
    new(0.5f, 0.75f),
    new(0.5f, 1f),
    new(0.625f, 1f),
    new(0.625f, 0.75f)
};

    private static readonly float2[] SideUVs =
    {
        new(0.875f, 0.5f),
        new(0.875f, 0.75f),
        new(1f, 0.75f),
        new(1f, 0.5f)
    };

    private static readonly ProfilerMarker ExecuteMarker = new("AddFacesJob Execute");

    public void Execute()
    {
        using (ExecuteMarker.Auto())
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        int bufferIndex = x + y * Width + z * Width * Height;
                        var block = BufferAsArray[bufferIndex];

                        if (block.Type == 0) continue;

                        bool right = IsAir(x + 1, y, z);
                        bool left = IsAir(x - 1, y, z);
                        bool top = IsAir(x, y + 1, z);
                        bool bottom = IsAir(x, y - 1, z);
                        bool front = IsAir(x, y, z + 1);
                        bool back = IsAir(x, y, z - 1);

                        AddVisibleFaces(new int3(x, y, z), right, left, top, bottom, front, back);
                    }
                }
            }
        }
    }

    bool IsAir(int x, int y, int z)
    {
        int index;

        if (x < 0)
        {
            if (LeftArray.Length == 0) return true;
            index = x + Width + y * Width + z * Width * Height;
            return LeftArray[index].Type == 0;
        }

        if (x >= Width)
        {
            if (RightArray.Length == 0) return true;
            index = x - Width + y * Width + z * Width * Height;
            return RightArray[index].Type == 0;
        }

        if (z < 0)
        {
            if (BackArray.Length == 0) return true;
            index = x + y * Width + (z + Depth) * Width * Height;
            return BackArray[index].Type == 0;
        }

        if (z >= Depth)
        {
            if (FrontArray.Length == 0) return true;
            index = x + y * Width + (z - Depth) * Width * Height;
            return FrontArray[index].Type == 0;
        }

        if (y < 0 || y >= Height) return true;

        index = x + y * Width + z * Width * Height;
        return BufferAsArray[index].Type == 0;
    }

    void AddVisibleFaces(int3 pos, bool right, bool left, bool top, bool bottom, bool front, bool back)
    {
        // "pos" is the position of the vertex at the bottom back left of the block, not its center
        if (top) AddTopFace(pos);
        if (bottom) AddBottomFace(pos);
        if (right) AddRightFace(pos);
        if (left) AddLeftFace(pos);
        if (front) AddFrontFace(pos);
        if (back) AddBackFace(pos);
    }

    private void AddTopFace(int3 pos)
    {
        // Render
        int renderStart = RenderVertices.Length;

        RenderVertices.Add(pos + new float3(0, 1, 0));
        RenderVertices.Add(pos + new float3(0, 1, 1));
        RenderVertices.Add(pos + new float3(1, 1, 1));
        RenderVertices.Add(pos + new float3(1, 1, 0));

        // *Has to be in same order as vertices to have the right orientation*
        AddUVs(TopUVs);
        AddQuad(renderStart);
        AddNormals(new float3(0, 1, 0));

        // Collider
        int colliderStart = ColliderVertices.Length;

        ColliderVertices.Add(pos + new float3(0, 1, 0));
        ColliderVertices.Add(pos + new float3(0, 1, 1));
        ColliderVertices.Add(pos + new float3(1, 1, 1));
        ColliderVertices.Add(pos + new float3(1, 1, 0));

        AddColliderQuad(colliderStart);
    }

    private void AddBottomFace(int3 pos)
    {
        // Render
        int renderStart = RenderVertices.Length;

        RenderVertices.Add(pos + new float3(0, 0, 0));
        RenderVertices.Add(pos + new float3(1, 0, 0));
        RenderVertices.Add(pos + new float3(1, 0, 1));
        RenderVertices.Add(pos + new float3(0, 0, 1));

        // *Has to be in same order as vertices to have the right orientation*
        AddUVs(SideUVs);
        AddQuad(renderStart);
        AddNormals(new float3(0, -1, 0));
    }

    private void AddRightFace(int3 pos)
    {
        // Render
        int renderStart = RenderVertices.Length;

        RenderVertices.Add(pos + new float3(1, 0, 0));
        RenderVertices.Add(pos + new float3(1, 1, 0));
        RenderVertices.Add(pos + new float3(1, 1, 1));
        RenderVertices.Add(pos + new float3(1, 0, 1));

        // *Has to be in same order as vertices to have the right orientation*
        AddUVs(SideUVs);
        AddQuad(renderStart);
        AddNormals(new float3(1, 0, 0));

        // Collider
        if (RightArray.Length != 0 || pos.x != Depth - 1)
        {
            int colliderStart = ColliderVertices.Length;

            ColliderVertices.Add(pos + new float3(1, 0, 0));
            ColliderVertices.Add(pos + new float3(1, 1, 0));
            ColliderVertices.Add(pos + new float3(1, 1, 1));
            ColliderVertices.Add(pos + new float3(1, 0, 1));

            AddColliderQuad(colliderStart);
        }
    }

    private void AddLeftFace(int3 pos)
    {
        // Render
        int renderStart = RenderVertices.Length;

        RenderVertices.Add(pos + new float3(0, 0, 1));
        RenderVertices.Add(pos + new float3(0, 1, 1));
        RenderVertices.Add(pos + new float3(0, 1, 0));
        RenderVertices.Add(pos + new float3(0, 0, 0));

        // *Has to be in same order as vertices to have the right orientation*
        AddUVs(SideUVs);
        AddQuad(renderStart);
        AddNormals(new float3(-1, 0, 0));

        // Collider
        if (LeftArray.Length != 0 || pos.x != 0)
        {
            int colliderStart = ColliderVertices.Length;

            ColliderVertices.Add(pos + new float3(0, 0, 1));
            ColliderVertices.Add(pos + new float3(0, 1, 1));
            ColliderVertices.Add(pos + new float3(0, 1, 0));
            ColliderVertices.Add(pos + new float3(0, 0, 0));

            AddColliderQuad(colliderStart);
        }
    }

    private void AddFrontFace(int3 pos)
    {
        // Render
        var renderStart = RenderVertices.Length;

        RenderVertices.Add(pos + new float3(1, 0, 1));
        RenderVertices.Add(pos + new float3(1, 1, 1));
        RenderVertices.Add(pos + new float3(0, 1, 1));
        RenderVertices.Add(pos + new float3(0, 0, 1));

        // *Has to be in same order as vertices to have the right orientation*
        AddUVs(SideUVs);
        AddQuad(renderStart);
        AddNormals(new float3(0, 0, 1));

        // Collider
        if (FrontArray.Length != 0 || pos.z != Depth - 1)
        {
            int colliderStart = ColliderVertices.Length;

            ColliderVertices.Add(pos + new float3(1, 0, 1));
            ColliderVertices.Add(pos + new float3(1, 1, 1));
            ColliderVertices.Add(pos + new float3(0, 1, 1));
            ColliderVertices.Add(pos + new float3(0, 0, 1));

            AddColliderQuad(colliderStart);
        }
    }

    private void AddBackFace(int3 pos)
    {
        // Render
        int renderStart = RenderVertices.Length;

        RenderVertices.Add(pos + new float3(0, 0, 0));
        RenderVertices.Add(pos + new float3(0, 1, 0));
        RenderVertices.Add(pos + new float3(1, 1, 0));
        RenderVertices.Add(pos + new float3(1, 0, 0));

        // *Has to be in same order as vertices to have the right orientation*
        AddUVs(SideUVs);
        AddQuad(renderStart);
        AddNormals(new float3(0, 0, -1));

        // Collider
        if (BackArray.Length != 0 || pos.z != 0)
        {
            int colliderStart = ColliderVertices.Length;

            ColliderVertices.Add(pos + new float3(0, 0, 0));
            ColliderVertices.Add(pos + new float3(0, 1, 0));
            ColliderVertices.Add(pos + new float3(1, 1, 0));
            ColliderVertices.Add(pos + new float3(1, 0, 0));

            AddColliderQuad(colliderStart);
        }
    private void AddUVs(float2[] uvs)
    {
        RenderUVs.Add(uvs[0]);
        RenderUVs.Add(uvs[1]);
        RenderUVs.Add(uvs[2]);
        RenderUVs.Add(uvs[3]);
    }

    private void AddQuad(int start)
    {
        RenderTriangles.Add(start + 0);
        RenderTriangles.Add(start + 1);
        RenderTriangles.Add(start + 2);
        RenderTriangles.Add(start + 0);
        RenderTriangles.Add(start + 2);
        RenderTriangles.Add(start + 3);
    }

    private void AddNormals(float3 normal)
    {
        RenderNormals.Add(normal);
        RenderNormals.Add(normal);
        RenderNormals.Add(normal);
        RenderNormals.Add(normal);
    }

    private void AddColliderQuad(int start)
    {
        ColliderTriangles.Add(start + 0);
        ColliderTriangles.Add(start + 1);
        ColliderTriangles.Add(start + 2);
        ColliderTriangles.Add(start + 0);
        ColliderTriangles.Add(start + 2);
        ColliderTriangles.Add(start + 3);
    }
}



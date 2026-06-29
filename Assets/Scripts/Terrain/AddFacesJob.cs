using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct AddFacesJob : IJob
{
    [ReadOnly] public NativeArray<Block> Buffer; // DynamicBuffer can't be used in jobs; NativeArray provides native blittable memory (needed by the Job System)
    public int Width;
    public int Height;
    public int Depth;

    // Adjacent chunks
    public NativeArray<Block> LeftArray;
    public NativeArray<Block> RightArray;
    public NativeArray<Block> BackArray;
    public NativeArray<Block> FrontArray;

    public NativeList<float3> Vertices;
    public NativeList<float2> UVs;
    public NativeList<int> Triangles;
    public NativeList<float3> Normals;

    public void Execute()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    int bufferIndex = x + y * Width + z * Width * Height;
                    var block = Buffer[bufferIndex];

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
        return Buffer[index].Type == 0;
    }

    void AddVisibleFaces(int3 pos, bool right, bool left, bool top, bool bottom, bool front, bool back)
    {
        // "pos" is the position of the vertex at the bottom back left of the block, not its center
        // Vertices of the faces might not be set in sync with the UVs. Verify later.

        int start = Vertices.Length;

        if (right)
        {
            Vertices.Add(pos + new float3(1, 0, 0)); // Bottom left of this face
            Vertices.Add(pos + new float3(1, 1, 0)); // Top left
            Vertices.Add(pos + new float3(1, 1, 1)); // Top right
            Vertices.Add(pos + new float3(1, 0, 1)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            UVs.Add(new float2(0.875f, 0.5f));
            UVs.Add(new float2(0.875f, 0.75f));
            UVs.Add(new float2(1, 0.75f));
            UVs.Add(new float2(1, 0.5f));

            AddQuad(start);
            AddNormals(new float3(1, 0, 0));
            start += 4;
        }

        if (left)
        {
            Vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
            Vertices.Add(pos + new float3(0, 1, 1)); // Top left
            Vertices.Add(pos + new float3(0, 1, 0)); // Top right
            Vertices.Add(pos + new float3(0, 0, 0)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            UVs.Add(new float2(0.875f, 0.5f));
            UVs.Add(new float2(0.875f, 0.75f));
            UVs.Add(new float2(1, 0.75f));
            UVs.Add(new float2(1, 0.5f));

            AddQuad(start);
            AddNormals(new float3(-1, 0, 0));
            start += 4;
        }

        if (top)
        {
            Vertices.Add(pos + new float3(0, 1, 0)); // Bottom left of this face
            Vertices.Add(pos + new float3(0, 1, 1)); // Top left
            Vertices.Add(pos + new float3(1, 1, 1)); // Top right
            Vertices.Add(pos + new float3(1, 1, 0)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            UVs.Add(new float2(0.5f, 0.75f));
            UVs.Add(new float2(0.5f, 1));
            UVs.Add(new float2(0.625f, 1));
            UVs.Add(new float2(0.625f, 0.75f));

            AddQuad(start);
            AddNormals(new float3(0, 1, 0));
            start += 4;
        }

        if (bottom)
        {
            Vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
            Vertices.Add(pos + new float3(0, 0, 0)); // Top left
            Vertices.Add(pos + new float3(1, 0, 0)); // Top right
            Vertices.Add(pos + new float3(1, 0, 1)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            UVs.Add(new float2(0.875f, 0.5f));
            UVs.Add(new float2(0.875f, 0.75f));
            UVs.Add(new float2(1, 0.75f));
            UVs.Add(new float2(1, 0.5f));

            AddQuad(start);
            AddNormals(new float3(0, -1, 0));
            start += 4;
        }

        if (front)
        {
            Vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
            Vertices.Add(pos + new float3(1, 0, 1)); // Top left
            Vertices.Add(pos + new float3(1, 1, 1)); // Top right
            Vertices.Add(pos + new float3(0, 1, 1)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            UVs.Add(new float2(0.875f, 0.5f));
            UVs.Add(new float2(0.875f, 0.75f));
            UVs.Add(new float2(1, 0.75f));
            UVs.Add(new float2(1, 0.5f));

            AddQuad(start);
            AddNormals(new float3(0, 0, 1));
            start += 4;
        }

        if (back)
        {
            Vertices.Add(pos + new float3(1, 0, 0)); // Bottom left of this face
            Vertices.Add(pos + new float3(0, 0, 0)); // Top left
            Vertices.Add(pos + new float3(0, 1, 0)); // Top right
            Vertices.Add(pos + new float3(1, 1, 0)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            UVs.Add(new float2(0.875f, 0.5f));
            UVs.Add(new float2(0.875f, 0.75f));
            UVs.Add(new float2(1, 0.75f));
            UVs.Add(new float2(1, 0.5f));

            AddQuad(start);
            AddNormals(new float3(0, 0, -1));
        }
    }


    private void AddQuad(int start)
    {
        Triangles.Add(start + 0);
        Triangles.Add(start + 1);
        Triangles.Add(start + 2);
        Triangles.Add(start + 0);
        Triangles.Add(start + 2);
        Triangles.Add(start + 3);
    }

    private void AddNormals(float3 normal)
    {
        Normals.Add(normal);
        Normals.Add(normal);
        Normals.Add(normal);
        Normals.Add(normal);
    }
}

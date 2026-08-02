using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

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
        if ((x < 0 || x >= Width) && (z < 0 || z >= Depth))
            return true;

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
        // if (bottom) AddBottomFace(pos);
        if (right) AddRightFace(pos);
        if (left) AddLeftFace(pos);
        if (front) AddFrontFace(pos);
        if (back) AddBackFace(pos);
    }

    private void AddTopFace(int3 pos)
    {
        int3[] coords =
        {
          new (pos + new float3(0, 1, 0)),
          new (pos + new float3(0, 1, 1)),
          new (pos + new float3(1, 1, 1)),
          new (pos + new float3(1, 1, 0)),
        };

        int renderStart = RenderVertices.Length;
        int colliderStart = ColliderVertices.Length;

        AddRenderVertices(coords);
        AddQuad(renderStart);
        AddUVs(TopUVs);
        AddNormals(new float3(0, 1, 0));
        AddColliderVertices(coords);
        AddColliderQuad(colliderStart);

        // AO
        if (DebugSettings.AmbientOcclusion)
        {
            byte ao0 = VertexAO(
                IsAir(pos.x - 1, pos.y + 1, pos.z),
                IsAir(pos.x, pos.y + 1, pos.z - 1),
                IsAir(pos.x - 1, pos.y + 1, pos.z - 1));

            byte ao1 = VertexAO(
                IsAir(pos.x - 1, pos.y + 1, pos.z),
                IsAir(pos.x, pos.y + 1, pos.z + 1),
                IsAir(pos.x - 1, pos.y + 1, pos.z + 1));

            byte ao2 = VertexAO(
                IsAir(pos.x + 1, pos.y + 1, pos.z),
                IsAir(pos.x, pos.y + 1, pos.z + 1),
                IsAir(pos.x + 1, pos.y + 1, pos.z + 1));

            byte ao3 = VertexAO(
                IsAir(pos.x + 1, pos.y + 1, pos.z),
                IsAir(pos.x, pos.y + 1, pos.z - 1),
                IsAir(pos.x + 1, pos.y + 1, pos.z - 1));

            RenderColors.Add(new Color32(ao0, ao0, ao0, 255));
            RenderColors.Add(new Color32(ao1, ao1, ao1, 255));
            RenderColors.Add(new Color32(ao2, ao2, ao2, 255));
            RenderColors.Add(new Color32(ao3, ao3, ao3, 255));
        }
    }

    private void AddBottomFace(int3 pos)
    {
        int3[] coords =
        {
            new (pos + new float3(0, 0, 0)),
            new (pos + new float3(1, 0, 0)),
            new (pos + new float3(1, 0, 1)),
            new (pos + new float3(0, 0, 1)),
        };

        int renderStart = RenderVertices.Length;
        AddRenderVertices(coords);
        AddQuad(renderStart);
        AddUVs(SideUVs);
        AddNormals(new float3(0, -1, 0));

        RenderColors.Add(new Color32(255, 255, 255, 255));
        RenderColors.Add(new Color32(255, 255, 255, 255));
        RenderColors.Add(new Color32(255, 255, 255, 255));
        RenderColors.Add(new Color32(255, 255, 255, 255));
    }

    private void AddRightFace(int3 pos)
    {
        int3[] coords =
        {
            new (pos + new float3(1, 0, 0)),
            new (pos + new float3(1, 1, 0)),
            new (pos + new float3(1, 1, 1)),
            new (pos + new float3(1, 0, 1)),
        };

        int renderStart = RenderVertices.Length;
        int colliderStart = ColliderVertices.Length;

        AddRenderVertices(coords);
        AddQuad(renderStart);
        AddUVs(SideUVs);
        AddNormals(new float3(1, 0, 0));
        if (RightArray.Length != 0 || pos.x != Depth - 1)
        {
            AddColliderVertices(coords);
            AddColliderQuad(colliderStart);
        }

        // AO
        if (DebugSettings.AmbientOcclusion)
        {
            byte ao0 = VertexAO(
                IsAir(pos.x + 1, pos.y - 1, pos.z),
                IsAir(pos.x + 1, pos.y, pos.z - 1),
                IsAir(pos.x + 1, pos.y - 1, pos.z - 1));

            byte ao1 = VertexAO(
                IsAir(pos.x + 1, pos.y + 1, pos.z),
                IsAir(pos.x + 1, pos.y, pos.z - 1),
                IsAir(pos.x + 1, pos.y + 1, pos.z - 1));

            byte ao2 = VertexAO(
                IsAir(pos.x + 1, pos.y + 1, pos.z),
                IsAir(pos.x + 1, pos.y, pos.z + 1),
                IsAir(pos.x + 1, pos.y + 1, pos.z + 1));

            byte ao3 = VertexAO(
                IsAir(pos.x + 1, pos.y - 1, pos.z),
                IsAir(pos.x + 1, pos.y, pos.z + 1),
                IsAir(pos.x + 1, pos.y - 1, pos.z + 1));

            RenderColors.Add(new Color32(ao0, ao0, ao0, 255));
            RenderColors.Add(new Color32(ao1, ao1, ao1, 255));
            RenderColors.Add(new Color32(ao2, ao2, ao2, 255));
            RenderColors.Add(new Color32(ao3, ao3, ao3, 255));
        }
    }

    private void AddLeftFace(int3 pos)
    {
        int3[] coords =
        {
            new (pos + new float3(0, 0, 1)),
            new (pos + new float3(0, 1, 1)),
            new (pos + new float3(0, 1, 0)),
            new (pos + new float3(0, 0, 0)),
        };

        int renderStart = RenderVertices.Length;
        int colliderStart = ColliderVertices.Length;

        AddRenderVertices(coords);
        AddQuad(renderStart);
        AddUVs(SideUVs);
        AddNormals(new float3(-1, 0, 0));
        if (LeftArray.Length != 0 || pos.x != 0)
        {
            AddColliderVertices(coords);
            AddColliderQuad(colliderStart);
        }

        // AO
        if (DebugSettings.AmbientOcclusion)
        {
            byte ao0 = VertexAO(
                IsAir(pos.x - 1, pos.y - 1, pos.z),
                IsAir(pos.x - 1, pos.y, pos.z + 1),
                IsAir(pos.x - 1, pos.y - 1, pos.z + 1));

            byte ao1 = VertexAO(
                IsAir(pos.x - 1, pos.y + 1, pos.z),
                IsAir(pos.x - 1, pos.y, pos.z + 1),
                IsAir(pos.x - 1, pos.y + 1, pos.z + 1));

            byte ao2 = VertexAO(
                IsAir(pos.x - 1, pos.y + 1, pos.z),
                IsAir(pos.x - 1, pos.y, pos.z - 1),
                IsAir(pos.x - 1, pos.y + 1, pos.z - 1));

            byte ao3 = VertexAO(
                IsAir(pos.x - 1, pos.y - 1, pos.z),
                IsAir(pos.x - 1, pos.y, pos.z - 1),
                IsAir(pos.x - 1, pos.y - 1, pos.z - 1));

            RenderColors.Add(new Color32(ao0, ao0, ao0, 255));
            RenderColors.Add(new Color32(ao1, ao1, ao1, 255));
            RenderColors.Add(new Color32(ao2, ao2, ao2, 255));
            RenderColors.Add(new Color32(ao3, ao3, ao3, 255));
        }
    }

    private void AddFrontFace(int3 pos)
    {
        int3[] coords =
        {
            new (pos + new float3(1, 0, 1)),
            new (pos + new float3(1, 1, 1)),
            new (pos + new float3(0, 1, 1)),
            new (pos + new float3(0, 0, 1)),
        };

        var renderStart = RenderVertices.Length;
        int colliderStart = ColliderVertices.Length;

        AddRenderVertices(coords);
        AddQuad(renderStart);
        AddUVs(SideUVs);
        AddNormals(new float3(0, 0, 1));
        if (LeftArray.Length != 0 || pos.x != 0)
        {
            AddColliderVertices(coords);
            AddColliderQuad(colliderStart);
        }

        // AO
        if (DebugSettings.AmbientOcclusion)
        {
            byte ao0 = VertexAO(
                IsAir(pos.x, pos.y - 1, pos.z + 1),
                IsAir(pos.x + 1, pos.y, pos.z + 1),
                IsAir(pos.x + 1, pos.y - 1, pos.z + 1));

            byte ao1 = VertexAO(
                IsAir(pos.x, pos.y + 1, pos.z + 1),
                IsAir(pos.x + 1, pos.y, pos.z + 1),
                IsAir(pos.x + 1, pos.y + 1, pos.z + 1));

            byte ao2 = VertexAO(
                IsAir(pos.x, pos.y + 1, pos.z + 1),
                IsAir(pos.x - 1, pos.y, pos.z + 1),
                IsAir(pos.x - 1, pos.y + 1, pos.z + 1));

            byte ao3 = VertexAO(
                IsAir(pos.x, pos.y - 1, pos.z + 1),
                IsAir(pos.x - 1, pos.y, pos.z + 1),
                IsAir(pos.x - 1, pos.y - 1, pos.z + 1));

            RenderColors.Add(new Color32(ao0, ao0, ao0, 255));
            RenderColors.Add(new Color32(ao1, ao1, ao1, 255));
            RenderColors.Add(new Color32(ao2, ao2, ao2, 255));
            RenderColors.Add(new Color32(ao3, ao3, ao3, 255));
        }
    }

    private void AddBackFace(int3 pos)
    {
        int3[] coords =
        {
            new (pos + new float3(0, 0, 0)),
            new (pos + new float3(0, 1, 0)),
            new (pos + new float3(1, 1, 0)),
            new (pos + new float3(1, 0, 0)),
        };

        int renderStart = RenderVertices.Length;
        int colliderStart = ColliderVertices.Length;

        AddRenderVertices(coords);
        AddQuad(renderStart);
        AddUVs(SideUVs);
        AddNormals(new float3(0, 0, -1));
        if (BackArray.Length != 0 || pos.z != 0)
        {
            AddColliderVertices(coords);
            AddColliderQuad(colliderStart);
        }

        // AO
        if (DebugSettings.AmbientOcclusion)
        {
            byte ao0 = VertexAO(
                IsAir(pos.x, pos.y - 1, pos.z - 1),
                IsAir(pos.x - 1, pos.y, pos.z - 1),
                IsAir(pos.x - 1, pos.y - 1, pos.z - 1));

            byte ao1 = VertexAO(
                IsAir(pos.x, pos.y + 1, pos.z - 1),
                IsAir(pos.x - 1, pos.y, pos.z - 1),
                IsAir(pos.x - 1, pos.y + 1, pos.z - 1));

            byte ao2 = VertexAO(
                IsAir(pos.x, pos.y + 1, pos.z - 1),
                IsAir(pos.x + 1, pos.y, pos.z - 1),
                IsAir(pos.x + 1, pos.y + 1, pos.z - 1));

            byte ao3 = VertexAO(
                IsAir(pos.x, pos.y - 1, pos.z - 1),
                IsAir(pos.x + 1, pos.y, pos.z - 1),
                IsAir(pos.x + 1, pos.y - 1, pos.z - 1));

            RenderColors.Add(new Color32(ao0, ao0, ao0, 255));
            RenderColors.Add(new Color32(ao1, ao1, ao1, 255));
            RenderColors.Add(new Color32(ao2, ao2, ao2, 255));
            RenderColors.Add(new Color32(ao3, ao3, ao3, 255));
        }
    }

    private void AddRenderVertices(int3[] coords)
    {
        RenderVertices.Add(coords[0]);
        RenderVertices.Add(coords[1]);
        RenderVertices.Add(coords[2]);
        RenderVertices.Add(coords[3]);
    }

    private void AddColliderVertices(int3[] coords)
    {
        ColliderVertices.Add(coords[0]);
        ColliderVertices.Add(coords[1]);
        ColliderVertices.Add(coords[2]);
        ColliderVertices.Add(coords[3]);
    }

    // *Need to be in same order as vertices to have the right orientation*
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

    // Ambient Oclussion
    static readonly byte[] AO =
    {
        150,
        180,
        220,
        255
    };

    byte VertexAO(bool side1, bool side2, bool corner)
    {
        // side == true means there's air
        bool s1 = !side1;
        bool s2 = !side2;
        bool c = !corner;

        if (s1 && s2) return AO[0];

        return AO[3 - (Convert.ToInt32(s1) + Convert.ToInt32(s2) + Convert.ToInt32(c))];
    }
}



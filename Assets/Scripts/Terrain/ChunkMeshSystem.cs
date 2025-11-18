using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;

// UpdateAfter to wait for the chunk data
[UpdateAfter(typeof(ChunkGenerationSystem))]
public partial class ChunkMeshSystem : SystemBase
{
    [SerializeField] private Material _sharedMaterial;

    protected override void OnCreate()
    {
        _sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var atlas = Resources.Load<Texture2D>("WSUUw");
        _sharedMaterial.mainTexture = atlas;

        if (atlas == null) Debug.Log("Atlas not found");
        else Debug.Log("Atlas found");

        if (_sharedMaterial == null) Debug.LogError("Shader not found");
        else Debug.Log("Material created: " + _sharedMaterial.name);
    }


    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var meshesToAdd = new List<(Entity entity, Mesh mesh, RenderMeshArray renderArray)>();


        foreach (var (chunk, buffer, entity) in SystemAPI.Query<RefRO<Chunk>, DynamicBuffer<Block>>()
            .WithEntityAccess()
            .WithNone<MaterialMeshInfo>())
        {
            var width = chunk.ValueRO.Width;
            var height = chunk.ValueRO.Height;
            var depth = chunk.ValueRO.Depth;

            var vertices = new NativeList<float3>(Allocator.TempJob); // TempJob allocation: safe for 4 frames
            var uvs = new NativeList<float2>(Allocator.TempJob);
            var triangles = new NativeList<int>(Allocator.TempJob);
            var normals = new NativeList<float3>(Allocator.TempJob);

            var addFacesJob = new AddFacesJob
            {
                Buffer = buffer.AsNativeArray(),

                Width = width,
                Height = height,
                Depth = depth,

                Vertices = vertices,
                UVs = uvs,
                Triangles = triangles,
                Normals = normals
            };

            var handle = addFacesJob.Schedule();
            handle.Complete();

            if (vertices.Length > 0)
            {
                ecb.SetName(entity, "ChunkMesh_" + entity.Index);

                // Generate mesh
                var mesh = new Mesh();
                mesh.name = $"ChunkMesh_{entity.Index}";
                mesh.SetVertices(vertices.AsArray());
                mesh.SetTriangles(triangles.AsArray().ToArray(), 0);
                mesh.SetNormals(normals.AsArray());
                mesh.SetUVs(0, uvs.AsArray());
                mesh.RecalculateBounds();

                // Show mesh
                var renderArray = new RenderMeshArray(new[] { _sharedMaterial }, new[] { mesh });

                meshesToAdd.Add((entity, mesh, renderArray));
            }

            vertices.Dispose();
            uvs.Dispose();
            triangles.Dispose();
            normals.Dispose();
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();

        var desc = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false);

        foreach (var item in meshesToAdd)
        {
            var renderMeshArray = new RenderMeshArray(new[] { _sharedMaterial }, new[] { item.mesh });
            RenderMeshUtility.AddComponents(item.entity, EntityManager, desc, renderMeshArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));


            var transform = EntityManager.GetComponentData<LocalTransform>(item.entity);
            EntityManager.SetComponentData(item.entity, transform);
        }
        meshesToAdd.Clear();
    }

    protected override void OnDestroy()
    {
        if (_sharedMaterial != null)
        {
            Object.Destroy(_sharedMaterial);
        }
    }

    [BurstCompile]
    public struct AddFacesJob : IJob
    {
        [ReadOnly] public NativeArray<Block> Buffer; // DynamicBuffer can't be used in jobs; NativeArray provides native blittable memory (needed by the Job System)
        public int Width;
        public int Height;
        public int Depth;

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
                        int index = x + y * Width + z * Width * Height;
                        var block = Buffer[index];

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
            if (x < 0 || y < 0 || z < 0 || x >= Width || y >= Height || z >= Depth)
                return true;

            int index = x + y * Width + z * Width * Height;
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
}
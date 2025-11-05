using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;

// UpdateAfter to wait for the chunk data
[UpdateAfter(typeof(ChunkGenerationSystem))]
public partial class ChunkMeshSystem : SystemBase
{
    [SerializeField] private Material _sharedMaterial;
    private bool debugObjectInstantiated = false;

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

            var vertices = new NativeList<float3>(Allocator.Temp);
            var uvs = new NativeList<float2>(Allocator.Temp);
            var triangles = new NativeList<int>(Allocator.Temp);
            var normals = new NativeList<float3>(Allocator.Temp);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        int index = x + y * width + z * width * height;
                        var block = buffer[index];

                        if (block.Type == 0) continue;

                        bool right = IsAir(buffer, width, height, depth, x + 1, y, z);
                        bool left = IsAir(buffer, width, height, depth, x - 1, y, z);
                        bool top = IsAir(buffer, width, height, depth, x, y + 1, z);
                        bool bottom = IsAir(buffer, width, height, depth, x, y - 1, z);
                        bool front = IsAir(buffer, width, height, depth, x, y, z + 1);
                        bool back = IsAir(buffer, width, height, depth, x, y, z - 1);

                        AddVisibleFaces(vertices, triangles, normals, uvs, new int3(x, y, z),
                            right, left, top, bottom, front, back);
                    }
                }
            }

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

                // Debug
                if (!debugObjectInstantiated)
                {
                    Debug.Log("Generating debug GameObject");
                    var go = new GameObject("DebugMesh", typeof(MeshFilter), typeof(MeshRenderer));
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = _sharedMaterial;
                    go.transform.position = new Vector3(0, 0, 25f);

                    debugObjectInstantiated = true;
                }
            }

            vertices.Dispose();
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

    private bool IsAir(DynamicBuffer<Block> buffer, int width, int height, int depth, int x, int y, int z)
    {
        // Out of bounds is considered air
        if (x < 0 || y < 0 || z < 0 || x >= width || y >= height || z >= depth)
            return true;

        int index = x + y * width + z * width * height;
        return buffer[index].Type == 0;
    }

    private void AddVisibleFaces(NativeList<float3> vertices, NativeList<int> triangles,
        NativeList<float3> normals, NativeList<float2> uvs, int3 pos,
        bool right, bool left, bool top, bool bottom, bool front, bool back)
    {
        // "pos" is the position of the vertex at the bottom back left of the block, not its center
        // Vertices of the faces might not be set in sync with the UVs. Verify later.

        int start = vertices.Length;

        if (right)
        {
            vertices.Add(pos + new float3(1, 0, 0)); // Bottom left of this face
            vertices.Add(pos + new float3(1, 1, 0)); // Top left
            vertices.Add(pos + new float3(1, 1, 1)); // Top right
            vertices.Add(pos + new float3(1, 0, 1)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            uvs.Add(new float2(0.875f, 0.5f));
            uvs.Add(new float2(0.875f, 0.75f));
            uvs.Add(new float2(1, 0.75f));
            uvs.Add(new float2(1, 0.5f));

            AddQuad(triangles, start);
            AddNormals(normals, new float3(1, 0, 0));
            start += 4;
        }

        if (left)
        {
            vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
            vertices.Add(pos + new float3(0, 1, 1)); // Top left
            vertices.Add(pos + new float3(0, 1, 0)); // Top right
            vertices.Add(pos + new float3(0, 0, 0)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            uvs.Add(new float2(0.875f, 0.5f));
            uvs.Add(new float2(0.875f, 0.75f));
            uvs.Add(new float2(1, 0.75f));
            uvs.Add(new float2(1, 0.5f));

            AddQuad(triangles, start);
            AddNormals(normals, new float3(-1, 0, 0));
            start += 4;
        }

        if (top)
        {
            vertices.Add(pos + new float3(0, 1, 0)); // Bottom left of this face
            vertices.Add(pos + new float3(0, 1, 1)); // Top left
            vertices.Add(pos + new float3(1, 1, 1)); // Top right
            vertices.Add(pos + new float3(1, 1, 0)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            uvs.Add(new float2(0.5f, 0.75f));
            uvs.Add(new float2(0.5f, 1));
            uvs.Add(new float2(0.625f, 1));
            uvs.Add(new float2(0.625f, 0.75f));

            AddQuad(triangles, start);
            AddNormals(normals, new float3(0, 1, 0));
            start += 4;
        }

        if (bottom)
        {
            vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
            vertices.Add(pos + new float3(0, 0, 0)); // Top left
            vertices.Add(pos + new float3(1, 0, 0)); // Top right
            vertices.Add(pos + new float3(1, 0, 1)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            uvs.Add(new float2(0.875f, 0.5f));
            uvs.Add(new float2(0.875f, 0.75f));
            uvs.Add(new float2(1, 0.75f));
            uvs.Add(new float2(1, 0.5f));

            AddQuad(triangles, start);
            AddNormals(normals, new float3(0, -1, 0));
            start += 4;
        }

        if (front)
        {
            vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
            vertices.Add(pos + new float3(1, 0, 1)); // Top left
            vertices.Add(pos + new float3(1, 1, 1)); // Top right
            vertices.Add(pos + new float3(0, 1, 1)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            uvs.Add(new float2(0.875f, 0.5f));
            uvs.Add(new float2(0.875f, 0.75f));
            uvs.Add(new float2(1, 0.75f));
            uvs.Add(new float2(1, 0.5f));

            AddQuad(triangles, start);
            AddNormals(normals, new float3(0, 0, 1));
            start += 4;
        }

        if (back)
        {
            vertices.Add(pos + new float3(1, 0, 0)); // Bottom left of this face
            vertices.Add(pos + new float3(0, 0, 0)); // Top left
            vertices.Add(pos + new float3(0, 1, 0)); // Top right
            vertices.Add(pos + new float3(1, 1, 0)); // Bottom right

            // *Has to be in same order as vertices to have the right orientation*
            uvs.Add(new float2(0.875f, 0.5f));
            uvs.Add(new float2(0.875f, 0.75f));
            uvs.Add(new float2(1, 0.75f));
            uvs.Add(new float2(1, 0.5f));

            AddQuad(triangles, start);
            AddNormals(normals, new float3(0, 0, -1));
        }
    }

    private void AddQuad(NativeList<int> triangles, int start)
    {
        triangles.Add(start + 0);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private void AddNormals(NativeList<float3> normals, float3 normal)
    {
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
    }
}
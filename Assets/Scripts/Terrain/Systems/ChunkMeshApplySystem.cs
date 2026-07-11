using Unity.Entities;
using UnityEngine;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Profiling;

public partial class ChunkMeshApplySystem : SystemBase
{
    private static readonly ProfilerMarker CreateColliderMarker = new("ChunkMeshApply.CreateCollider");

    private UnityEngine.Material _sharedMaterial;

    protected override void OnCreate()
    {
        _sharedMaterial = new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Lit"));
        var atlas = Resources.Load<Texture2D>("WSUUw");

        if (_sharedMaterial == null)
        {
            Debug.LogError("Shader not found");
            return;
        }
        if (atlas == null)
        {
            Debug.Log("Atlas not found");
            return;
        }

        _sharedMaterial.mainTexture = atlas;
    }

    protected override void OnDestroy()
    {
        if (_sharedMaterial != null)
            Object.Destroy(_sharedMaterial);
    }

    public void Apply(
    Entity entity,
    JobHandle handle,
    NativeList<float3> vertices,
    NativeList<int> triangles,
    NativeList<float3> normals,
    NativeList<float2> uvs)
    {
        handle.Complete();

        // Check if it exists, otherwise SharedVertices may not be null and try to work with a null _pendingMeshData.
        // Remember entities can be destroyed within RegenerateAllChunks in ChunkGenerationSystem
        if (!EntityManager.Exists(entity) || vertices.Length == 0)
            return;

        Mesh mesh;

        mesh = new Mesh();
        mesh.name = $"ChunkMesh_{entity.Index}";
        mesh.SetVertices(vertices.AsArray());
        mesh.SetTriangles(triangles.AsArray().ToArray(), 0);
        mesh.SetNormals(normals.AsArray());
        mesh.SetUVs(0, uvs.AsArray());
        mesh.RecalculateBounds();

        var desc = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.Off,
            receiveShadows: false
        );

        var renderMeshArray = new RenderMeshArray(
            new[] { _sharedMaterial },
            new[] { mesh }
        );

        RenderMeshUtility.AddComponents(
            entity,
            EntityManager,
            desc,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
        );

        using (CreateColliderMarker.Auto())
        {
            var trianglesInt3 = new NativeArray<int3>(triangles.Length / 3, Allocator.Temp);

            for (int i = 0; i < trianglesInt3.Length; i++)
            {
                trianglesInt3[i] = new int3(
                    triangles[i * 3],
                    triangles[i * 3 + 1],
                    triangles[i * 3 + 2]);
            }

            var collider = Unity.Physics.MeshCollider.Create(vertices.AsArray(), trianglesInt3);

            trianglesInt3.Dispose();

            var physicsCollider = new PhysicsCollider
            {
                Value = collider
            };

            if (EntityManager.HasComponent<PhysicsCollider>(entity))
                EntityManager.SetComponentData(entity, physicsCollider);
            else
                EntityManager.AddComponentData(entity, physicsCollider);
        }

        // If LocalTransform already exists, refresh it to ensure correct render
        if (EntityManager.HasComponent<LocalTransform>(entity))
        {
            var transform = EntityManager.GetComponentData<LocalTransform>(entity);
            EntityManager.SetComponentData(entity, transform);
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        ecb.SetName(entity, $"ChunkMesh_{entity.Index}");
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    protected override void OnUpdate() { }
}
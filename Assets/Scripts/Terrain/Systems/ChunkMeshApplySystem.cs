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
using System.Collections.Generic;

public partial class ChunkMeshApplySystem : SystemBase
{
    private static readonly ProfilerMarker CreateColliderMarker = new("ChunkMeshApply.CreateCollider");

    private UnityEngine.Material _sharedMaterial;

    private Queue<PendingMesh> _colliderQueue = new();

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

    public void Apply(PendingMesh pending)
    {
        // Check if it entity exists, otherwise vertices may try to work with a null one. They could've been destroyed in ChunkGenerationSystem > RegenerateAllChunks
        if (!EntityManager.Exists(pending.Entity) || pending.Vertices.Length == 0)
            return;

        Mesh mesh;

        mesh = new Mesh();
        mesh.name = $"ChunkMesh_{pending.Entity.Index}";
        mesh.SetVertices(pending.Vertices.AsArray());
        mesh.SetTriangles(pending.Triangles.AsArray().ToArray(), 0);
        mesh.SetNormals(pending.Normals.AsArray());
        mesh.SetUVs(0, pending.UVs.AsArray());
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
            pending.Entity,
            EntityManager,
            desc,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
        );

        // Delay collider creation to avoid creating multiple expensive colliders in the same frame
        _colliderQueue.Enqueue(pending);

        // If LocalTransform already exists, refresh it to ensure correct render
        if (EntityManager.HasComponent<LocalTransform>(pending.Entity))
        {
            var transform = EntityManager.GetComponentData<LocalTransform>(pending.Entity);
            EntityManager.SetComponentData(pending.Entity, transform);
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        ecb.SetName(pending.Entity, $"ChunkMesh_{pending.Entity.Index}");
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void CreateCollider(PendingMesh pending)
    {
        using (CreateColliderMarker.Auto())
        {
            var trianglesInt3 = new NativeArray<int3>(pending.Triangles.Length / 3, Allocator.Temp);

            for (int i = 0; i < trianglesInt3.Length; i++)
            {
                trianglesInt3[i] = new int3(
                    pending.Triangles[i * 3],
                    pending.Triangles[i * 3 + 1],
                    pending.Triangles[i * 3 + 2]);
            }

            var collider = Unity.Physics.MeshCollider.Create(
                pending.Vertices.AsArray(),
                trianglesInt3
            );

            trianglesInt3.Dispose();

            var physicsCollider = new PhysicsCollider
            {
                Value = collider
            };

            if (EntityManager.HasComponent<PhysicsCollider>(pending.Entity))
                EntityManager.SetComponentData(pending.Entity, physicsCollider);
            else
                EntityManager.AddComponentData(pending.Entity, physicsCollider);

            pending.Dispose();

            var transform = SystemAPI.GetComponent<LocalTransform>(pending.Entity);
            Debug.Log($"Collider generated for {transform.Position}");
        }
    }

    protected override void OnUpdate()
    {
        // Create only one collider per frame to prevent frame spikes
        if (_colliderQueue.Count > 0)
        {
            var pending = _colliderQueue.Dequeue();

            if (EntityManager.Exists(pending.Entity))
            {
                CreateCollider(pending);
            }
        }
    }
}
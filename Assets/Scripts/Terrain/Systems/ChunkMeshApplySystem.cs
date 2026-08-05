using Unity.Entities;
using UnityEngine;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

public partial class ChunkMeshApplySystem : SystemBase
{
    private Material _sharedMaterial;

    protected override void OnCreate()
    {
        var shader = Shader.Find("Shader Graphs/VoxelAO_Shader");
        _sharedMaterial = new Material(shader);
        var atlas = Resources.Load<Texture2D>("terrain-atlas-01");

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
        // Check if the entity exists. Otherwise vertices may try to work with a null one, since they could've been destroyed in ChunkGenerationSystem > RegenerateAllChunks
        if (!EntityManager.Exists(pending.Entity) || pending.RenderVertices.Length == 0)
            return;

        Mesh mesh;

        mesh = new Mesh();
        mesh.name = $"ChunkMesh_{pending.Entity.Index}";
        mesh.SetVertices(pending.RenderVertices.AsArray());
        mesh.SetTriangles(pending.RenderTriangles.AsArray().ToArray(), 0);
        mesh.SetNormals(pending.RenderNormals.AsArray());
        mesh.SetUVs(0, pending.RenderUVs.AsArray());
        if (DebugSettings.AmbientOcclusion) mesh.SetColors(pending.RenderColors.AsArray());
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        if (DebugSettings.ShowNormals)
        {
            var chunkTransform = EntityManager.GetComponentData<LocalTransform>(pending.Entity);
            float3 chunkPos = chunkTransform.Position;
            for (int i = 0; i < pending.RenderVertices.Length; i++)
            {
                Debug.DrawRay(
                    (Vector3)(chunkPos + pending.RenderVertices[i]),
                    (Vector3)pending.RenderNormals[i] * 0.2f,
                    Color.red,
                    1000f
                );
            }
        }

        var desc = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.On,
            receiveShadows: true
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
    protected override void OnUpdate() { }
}
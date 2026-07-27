using Unity.Entities;
using UnityEngine;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Transforms;

public partial class ChunkMeshApplySystem : SystemBase
{
    private Material _sharedMaterial;

    protected override void OnCreate()
    {
        var shader = Shader.Find("Shader Graphs/VoxelAO_Shader");
        _sharedMaterial = new Material(shader);
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
        if (DebugSettings.AmbientOclussion) mesh.SetColors(pending.RenderColors.AsArray());
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
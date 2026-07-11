using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.UIElements.Experimental;

[UpdateAfter(typeof(ChunkGenerationSystem))]
public partial struct ChunkRebuildSystem : ISystem
{
    private NativeHashMap<int2, Entity> _chunks;

    void OnCreate(ref SystemState state)
    {
        _chunks = SystemAPI.GetSingleton<ChunksGlobalData>().Chunks;
    }

    readonly void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (chunkData, entity) in SystemAPI.Query<RefRW<ChunkData>>().WithEntityAccess())
        {
            if (chunkData.ValueRW.IsRefreshing)
            {
                // Meshes without a MaterialMeshInfo are rebuilt in ChunkMeshSystem
                ecb.RemoveComponent<MaterialMeshInfo>(entity);
                chunkData.ValueRW.IsRefreshing = false;
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
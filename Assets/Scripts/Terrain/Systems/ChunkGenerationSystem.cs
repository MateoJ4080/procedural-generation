using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public partial struct ChunkGenerationSystem : ISystem
{
    private TerrainConfig _lastConfig;
    private NativeHashMap<int2, Entity> _loadedChunks;
    private int2 _lastPlayerChunk;
    private bool _hasLastPlayerChunk;

    public void OnCreate(ref SystemState state)
    {
        _loadedChunks = new NativeHashMap<int2, Entity>(100, Allocator.Persistent);

        // Create entity to hold global chunks data
        Entity e = state.EntityManager.CreateEntity();
        state.EntityManager.SetName(e, "ChunksGlobalData");
        state.EntityManager.AddComponentData(e, new ChunksGlobalData { Chunks = _loadedChunks });
    }

    public void OnUpdate(ref SystemState state)
    {
        // Regenerate terrain in runtime if TerrainConfig values are changed
        if (SystemAPI.TryGetSingleton<TerrainConfig>(out var terrainConfig))
        {
            if (!terrainConfig.Equals(_lastConfig))
            {
                RegenerateAllChunks(ref state, terrainConfig);
                _lastConfig = terrainConfig;
            }
        }

        if (!SystemAPI.HasSingleton<PlayerTag>())
        {
            UnityEngine.Debug.Log("PlayerTag doesn't exist yet and chunk creation depends on its existence");
            return;
        }

        Entity player = SystemAPI.GetSingletonEntity<PlayerTag>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(player).Position;

        int2 playerChunk = new int2((int)(playerPos.x / 16), (int)(playerPos.z / 16));

        if (_hasLastPlayerChunk && playerChunk.Equals(_lastPlayerChunk))
            return;

        _lastPlayerChunk = playerChunk;
        _hasLastPlayerChunk = true;

        int loadRadius = 7;

        // Load new chunks based on player position
        for (int dx = -loadRadius; dx <= loadRadius; dx++)
        {
            for (int dz = -loadRadius; dz <= loadRadius; dz++)
            {
                int2 chunkCoord = playerChunk + new int2(dx, dz);

                if (!_loadedChunks.ContainsKey(chunkCoord))
                {
                    var entity = state.EntityManager.CreateEntity();

                    state.EntityManager.AddComponentData(entity, new Chunk { Width = 16, Height = 16, Depth = 16 });
                    state.EntityManager.AddComponentData(entity, new LocalTransform
                    {
                        Position = new float3(chunkCoord.x * 16, 0, chunkCoord.y * 16),
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });
                    state.EntityManager.AddComponentData(entity, new ChunkData { ChunkCoord = chunkCoord });
                    state.EntityManager.AddComponentData(entity, new ChunkTag());
                    state.EntityManager.AddSharedComponent(entity, new PhysicsWorldIndex(0));

                    var blocks = new NativeArray<Block>(16 * 16 * 16, Allocator.TempJob);
                    var chunkHeightJob = new ChunkHeightJob
                    {
                        chunkCoord = chunkCoord,
                        config = terrainConfig,
                        blocks = blocks
                    };
                    chunkHeightJob.ScheduleParallel(16 * 16, 16, default).Complete();

                    var buffer = state.EntityManager.AddBuffer<Block>(entity);
                    buffer.ResizeUninitialized(16 * 16 * 16);
                    buffer.CopyFrom(blocks);
                    blocks.Dispose();

                    _loadedChunks.TryAdd(chunkCoord, entity);

                    // Save chunks map in single global data component
                    Entity chunkGlobalDataEntity = SystemAPI.GetSingletonEntity<ChunksGlobalData>();
                    state.EntityManager.SetComponentData(chunkGlobalDataEntity, new ChunksGlobalData { Chunks = _loadedChunks });

                    RegenerateAdjacentChunks(chunkCoord, ref state);
                }
            }
        }
        var chunksToUnload = new NativeList<int2>(Allocator.Temp);

        foreach (var chunk in _loadedChunks)
        {
            if (!ShouldBeLoaded(chunk.Key, playerChunk, loadRadius))
                chunksToUnload.Add(chunk.Key);
        }

        foreach (var chunkCoord in chunksToUnload)
        {
            RegenerateAdjacentChunks(chunkCoord, ref state);

            state.EntityManager.DestroyEntity(_loadedChunks[chunkCoord]);
            _loadedChunks.Remove(chunkCoord);
        }

        chunksToUnload.Dispose();
    }

    // To do: regenerate only the specific adjacent face instead of the whole chunk
    private void RegenerateAdjacentChunks(int2 chunkCoord, ref SystemState state)
    {
        RefreshChunk(chunkCoord + new int2(-1, 0), ref state);
        RefreshChunk(chunkCoord + new int2(1, 0), ref state);
        RefreshChunk(chunkCoord + new int2(0, -1), ref state);
        RefreshChunk(chunkCoord + new int2(0, 1), ref state);
    }

    private void RefreshChunk(int2 chunkCoord, ref SystemState state)
    {
        if (!_loadedChunks.TryGetValue(chunkCoord, out Entity chunk))
            return;

        var chunkData = SystemAPI.GetComponent<ChunkData>(chunk);
        chunkData.IsRefreshing = true;
        state.EntityManager.SetComponentData(chunk, chunkData);
    }

    private bool ShouldBeLoaded(int2 chunkCoord, int2 playerChunk, int loadRadius)
    {
        return math.abs(chunkCoord.x - playerChunk.x) <= loadRadius &&
               math.abs(chunkCoord.y - playerChunk.y) <= loadRadius;
    }

    // Clear chunks list if TerrainConfig is updated, allowing new generation in the OnUpdate
    private void RegenerateAllChunks(ref SystemState state, TerrainConfig config)
    {
        foreach (var entity in _loadedChunks.GetValueArray(Allocator.Temp))
        {
            state.EntityManager.DestroyEntity(entity);
        }

        _loadedChunks.Clear();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_loadedChunks.IsCreated) _loadedChunks.Dispose();
    }
}
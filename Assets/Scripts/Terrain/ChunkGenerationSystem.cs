using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ChunkGenerationSystem : ISystem
{
    private TerrainConfig lastConfig;
    private NativeHashMap<int2, Entity> chunks;

    public void OnCreate(ref SystemState state)
    {
        chunks = new NativeHashMap<int2, Entity>(100, Allocator.Persistent);

        // Create entity to hold global chunks data
        Entity e = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(e, new ChunksGlobalData { Chunks = chunks });
    }

    public void OnUpdate(ref SystemState state)
    {
        // Regenerate terrain in runtime if TerrainConfig values are changed
        if (SystemAPI.TryGetSingleton<TerrainConfig>(out var config))
        {
            if (!config.Equals(lastConfig))
            {
                RegenerateAllChunks(ref state, config);
                lastConfig = config;
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
        int loadRadius = 2;

        // Load new chunks based on player's position
        for (int dx = -loadRadius; dx <= loadRadius; dx++)
        {
            for (int dz = -loadRadius; dz <= loadRadius; dz++)
            {
                int2 chunkCoord = playerChunk + new int2(dx, dz);

                if (!chunks.ContainsKey(chunkCoord))
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

                    var blocks = new NativeArray<Block>(16 * 16 * 16, Allocator.TempJob);

                    var job = new ChunkHeightJob
                    {
                        chunkCoord = chunkCoord,
                        config = config,
                        blocks = blocks
                    };

                    job.Schedule().Complete();

                    var buffer = state.EntityManager.AddBuffer<Block>(entity);
                    buffer.ResizeUninitialized(16 * 16 * 16);
                    buffer.CopyFrom(blocks);
                    blocks.Dispose();

                    chunks.TryAdd(chunkCoord, entity);

                    // Save chunks map in single global data component
                    Entity chunkGlobalDataEntity = SystemAPI.GetSingletonEntity<ChunksGlobalData>();
                    state.EntityManager.SetComponentData(chunkGlobalDataEntity, new ChunksGlobalData { Chunks = chunks });

                    UnityEngine.Debug.Log("Generated chunks: " + chunks.Count);
                }
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (chunks.IsCreated) chunks.Dispose();
    }

    // Clear chunks list if TerrainConfig is updated, allowing new generation in the OnUpdate
    private void RegenerateAllChunks(ref SystemState state, TerrainConfig config)
    {
        foreach (var entity in chunks.GetValueArray(Allocator.Temp))
        {
            state.EntityManager.DestroyEntity(entity);
        }

        chunks.Clear();
    }
}

[BurstCompile]
public struct ChunkHeightJob : IJob
{
    public int2 chunkCoord;
    public TerrainConfig config;
    public NativeArray<Block> blocks;

    public void Execute()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                float nx = (x + chunkCoord.x * 16) * config.Frequency;
                float nz = (z + chunkCoord.y * 16) * config.Frequency;

                float height = 0f;
                float amp = config.Amplitude;
                float freq = 1f;

                for (int o = 0; o < config.Octaves; o++)
                {
                    height += amp * noise.snoise(new float2(nx * freq, nz * freq));
                    amp *= config.Persistence;
                    freq *= config.Lacunarity;
                }

                height = height * 0.5f + 0.5f;
                int maxY = (int)math.floor(height * 16);

                for (int y = 0; y < 16; y++)
                {
                    int index = x + y * 16 + z * 16 * 16;
                    blocks[index] = new Block { Type = (byte)(y <= maxY ? 1 : 0) };
                }
            }
        }
    }
}

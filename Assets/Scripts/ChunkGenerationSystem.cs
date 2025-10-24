using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;

[BurstCompile]
public partial struct ChunkGenerationSystem : ISystem
{
    private Entity playerEntity;
    private bool playerFound;
    private TerrainConfig lastConfig;


    private NativeHashMap<int2, Entity> chunks;

    public void OnCreate(ref SystemState state)
    {
        chunks = new NativeHashMap<int2, Entity>(100, Allocator.Persistent);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainConfig>(out var config))
            return;

        if (!config.Equals(lastConfig))
        {
            RegenerateAllChunks(ref state, config);
            lastConfig = config;
        }

        if (!playerFound)
        {
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
            {
                playerEntity = entity;
                playerFound = true;
                break;
            }

            if (!playerFound)
            {
                UnityEngine.Debug.Log("Player doesn't exist yet and chunk creation depends on its existence");
                return;
            }
        }

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        int2 playerChunk = new int2((int)(playerPos.x / 16), (int)(playerPos.z / 16));

        int loadRadius = 2;

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

                    var buffer = state.EntityManager.AddBuffer<Block>(entity);
                    buffer.ResizeUninitialized(16 * 16 * 16);

                    // More information about these parameters on the README!
                    float frequency = config.Frequency;   // Controls how "wide" or "narrow" the hills are
                    float amplitude = config.Amplitude;    // Maximum height of the terrain
                    int octaves = config.Octaves;           // Number of noise layers
                    float persistence = config.Persistence;  // Influence of each successive layer
                    float lacunarity = config.Lacunarity;     // Frequency multiplier per layer

                    for (int x = 0; x < 16; x++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            float nx = (x + chunkCoord.x * 16) * frequency;
                            float nz = (z + chunkCoord.y * 16) * frequency;

                            float height = 0f;
                            float amp = amplitude;
                            float freq = 1f;

                            for (int o = 0; o < octaves; o++)
                            {
                                height += amp * noise.snoise(new float2(nx * freq, nz * freq));
                                amp *= persistence;
                                freq *= lacunarity;
                            }

                            height = height * 0.5f + 0.5f; // normalizes to 0-1
                            int maxY = (int)math.floor(height * 16);

                            for (int y = 0; y < 16; y++)
                            {
                                int index = x + y * 16 + z * 16 * 16;
                                buffer[index] = new Block { Type = (byte)(y <= maxY ? 1 : 0) };
                            }
                        }
                    }

                    chunks.TryAdd(chunkCoord, entity);
                }
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (chunks.IsCreated) chunks.Dispose();
    }

    private void RegenerateAllChunks(ref SystemState state, TerrainConfig config)
    {
        UnityEngine.Debug.Log("Regenerating chunks");
        foreach (var entity in chunks.GetValueArray(Allocator.Temp))
        {
            state.EntityManager.DestroyEntity(entity);
        }
        chunks.Clear();
    }
}

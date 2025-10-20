using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ChunkGenerationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        int w = 16, h = 16, d = 16;

        state.EntityManager.AddComponentData(entity, new Chunk { Width = w, Height = h, Depth = d });
        state.EntityManager.AddComponentData(entity, new LocalTransform { Position = float3.zero, Rotation = quaternion.identity, Scale = 1f });

        var buffer = state.EntityManager.AddBuffer<Block>(entity);
        buffer.ResizeUninitialized(w * h * d);

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < d; z++)
            {
                float nx = x * 0.1f;
                float nz = z * 0.1f;
                float height = noise.snoise(new float2(nx, nz)) * 0.5f + 0.5f;
                int maxY = (int)math.floor(height * h);

                for (int y = 0; y < h; y++)
                {
                    int index = x + y * w + z * w * h;
                    buffer[index] = new Block { Type = (byte)(y <= maxY ? 1 : 0) };
                }
            }
        }
    }
}

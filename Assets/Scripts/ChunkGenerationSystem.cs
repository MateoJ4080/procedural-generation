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

        state.EntityManager.AddComponentData(entity, new LocalTransform
        {
            Position = float3.zero,
            Rotation = quaternion.identity,
            Scale = 1f
        });

        var buffer = state.EntityManager.AddBuffer<Block>(entity);
        buffer.ResizeUninitialized(w * h * d);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = new Block { Type = 1 };
        }
    }
}

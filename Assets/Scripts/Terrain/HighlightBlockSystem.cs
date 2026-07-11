using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct HighlightBlockSystem : ISystem
{
    private Entity highlight;

    public void OnUpdate(ref SystemState state)
    {
        if (highlight == Entity.Null)
            highlight = SystemAPI.GetSingletonEntity<HighlightTag>();

        var hovered = SystemAPI.GetSingletonRW<HoveredBlock>();

        var transform = SystemAPI.GetComponentRW<LocalTransform>(highlight);

        if (hovered.ValueRO.Chunk == Entity.Null)
        {
            transform.ValueRW.Position = new float3(10000, -10000, 10000);
            return;
        }

        transform.ValueRW.Position = (float3)hovered.ValueRO.BlockPosition + 0.5f;
    }
}
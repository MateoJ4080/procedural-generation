using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public partial struct BlockDetectionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;

        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        var viewEntity = SystemAPI.GetSingletonEntity<PlayerViewTag>();
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

        var viewTransform = em.GetComponentData<LocalToWorld>(viewEntity);
        var characterData = em.GetComponentData<FirstPersonCharacterComponent>(playerEntity);

        float3 start = viewTransform.Position;
        float3 end = start + viewTransform.Forward * characterData.DetectionRange;

        RaycastInput input = new RaycastInput()
        {
            Start = start,
            End = end,
            Filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = ~PhysicsLayers.Player,
                GroupIndex = 0
            }
        };

        var hovered = SystemAPI.GetSingletonRW<HoveredBlock>();

        if (collisionWorld.CastRay(input, out RaycastHit hit))
        {

            hovered.ValueRW.Chunk = hit.Entity;
            float3 pointInsideBlock = hit.Position - hit.SurfaceNormal * 0.001f; // Make sure an adjacent block isn't registered instead (the raycast actually ends in the block right before the hovered one)
            int3 blockPos = (int3)math.floor(pointInsideBlock);

            hovered.ValueRW.BlockPosition = blockPos;
        }
        else
        {
            hovered.ValueRW.Chunk = Entity.Null;
        }

        UnityEngine.Debug.DrawLine(start, end, UnityEngine.Color.red, 1f);
    }
}
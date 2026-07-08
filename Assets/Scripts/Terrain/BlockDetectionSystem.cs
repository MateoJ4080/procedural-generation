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

        if (collisionWorld.CastRay(input, out RaycastHit hit))
        {
            UnityEngine.Debug.Log($"Hit entity: {hit.Entity}");
        }

        UnityEngine.Debug.DrawLine(start, end, UnityEngine.Color.red, 1f);
    }
}
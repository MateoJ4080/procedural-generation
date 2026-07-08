using Unity.Entities;
using Unity.Physics;

public partial struct SetCollisionFilterSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var collider in SystemAPI.Query<RefRW<PhysicsCollider>>().WithAll<PlayerTag>())
        {
            var filter = collider.ValueRO.Value.Value.GetCollisionFilter();

            filter.BelongsTo = PhysicsLayers.Player;

            collider.ValueRW.Value.Value.SetCollisionFilter(filter);
        }

        state.Enabled = false;
    }
}
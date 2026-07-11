using Unity.Entities;
using Unity.Transforms;

public partial struct GravitySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var dt = SystemAPI.Time.DeltaTime;

        foreach (var (gravityData, lt, entity) in SystemAPI.Query<RefRO<GravityData>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            var t = lt.ValueRO;

            t.Position.y += gravityData.ValueRO.Gravity * dt;

            lt.ValueRW = t;
        }
    }
}

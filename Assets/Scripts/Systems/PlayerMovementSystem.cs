using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Moves the player based on input along X and Z axes
public partial struct PlayerMovementSystem : ISystem
{
    public EntityManager em;

    public void OnUpdate(ref SystemState state)
    {
        em = state.EntityManager;

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var moveInput = SystemAPI.GetComponent<PlayerMoveInput>(playerEntity);
        var speed = SystemAPI.GetComponent<PlayerSpeed>(playerEntity).Value;

        var localTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var deltaTime = state.WorldUnmanaged.Time.DeltaTime;

        var forward = math.forward(localTransform.Rotation);
        var right = math.mul(localTransform.Rotation, new float3(1, 0, 0));
        var move = (right * moveInput.Value.x + forward * moveInput.Value.y) * deltaTime * speed;

        localTransform.Position += move;

        em.SetComponentData(playerEntity, localTransform);
    }
}

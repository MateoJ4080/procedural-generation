using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Moves the player based on input along X and Z axes
public partial struct PlayerControllerSystem : ISystem
{
    public EntityManager em;
    private float yaw;

    public void OnUpdate(ref SystemState state)
    {
        em = state.EntityManager;

        Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);

        // Movement
        PlayerMoveInput moveInput = SystemAPI.GetComponent<PlayerMoveInput>(playerEntity);
        float speed = SystemAPI.GetComponent<PlayerSpeed>(playerEntity).Value;
        float deltaTime = state.WorldUnmanaged.Time.DeltaTime;

        float3 forward = math.forward(playerTransform.Rotation);
        float3 right = math.mul(playerTransform.Rotation, new float3(1, 0, 0));

        var move = (right * moveInput.Value.x + forward * moveInput.Value.y) * deltaTime * speed;
        playerTransform.Position += move;

        // Rotation
        float sensitivity = SystemAPI.GetComponent<CameraSettings>(playerEntity).Sensitivity;
        var lookInput = SystemAPI.GetComponent<CameraLookInput>(playerEntity).Value;

        yaw += lookInput.x * sensitivity * SystemAPI.Time.DeltaTime;

        // quaternion.Euler doesn't work the same as Quaternion.Euler, it excepts radians instead of degrees
        playerTransform.Rotation = quaternion.Euler(0, math.radians(yaw), 0);

        state.EntityManager.SetComponentData(playerEntity, playerTransform);
        em.SetComponentData(playerEntity, playerTransform);
    }
}

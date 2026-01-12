using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

// Moves the player based on input along X and Z axes
public partial struct PlayerControllerSystem : ISystem
{
    private float yaw;

    public void OnUpdate(ref SystemState state)
    {
        Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        PhysicsVelocity velocity = SystemAPI.GetComponent<PhysicsVelocity>(playerEntity);

        // Movement
        PlayerMoveInput moveInput = SystemAPI.GetComponent<PlayerMoveInput>(playerEntity);
        float speed = SystemAPI.GetComponent<PlayerSpeed>(playerEntity).Value;

        float3 forward = math.forward(playerTransform.Rotation);
        float3 right = math.mul(playerTransform.Rotation, new float3(1, 0, 0));

        var moveDir = right * moveInput.Value.x + forward * moveInput.Value.y;

        // Apply movement through physics (dynamic body)
        velocity.Linear.x = moveDir.x * speed;
        velocity.Linear.z = moveDir.z * speed;

        // Rotation
        float sensitivity = SystemAPI.GetComponent<CameraSettings>(playerEntity).Sensitivity;
        var lookInput = SystemAPI.GetComponent<CameraLookInput>(playerEntity).Value;

        yaw += lookInput.x * sensitivity * SystemAPI.Time.DeltaTime;

        // quaternion.Euler doesn't work the same as Quaternion.Euler, it excepts radians instead of degrees
        playerTransform.Rotation = quaternion.Euler(0, math.radians(yaw), 0);

        SystemAPI.SetComponent(playerEntity, velocity);
        SystemAPI.SetComponent(playerEntity, playerTransform);
    }
}

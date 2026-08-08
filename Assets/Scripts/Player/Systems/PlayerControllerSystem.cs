using Unity.Entities;
public partial struct PlayerControllerSystem : ISystem
{
    public readonly void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

        var moveInput = SystemAPI.GetComponent<PlayerMoveInput>(playerEntity);
        var lookInput = SystemAPI.GetComponent<CameraLookInput>(playerEntity).Value;

        SystemAPI.SetComponent(playerEntity, new FirstPersonPlayerInputs
        {
            MoveInput = moveInput.Value,
            LookInput = lookInput
        });
    }
}

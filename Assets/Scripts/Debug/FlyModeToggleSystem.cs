using Unity.Burst;
using Unity.Entities;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct FlyModeToggleSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer>().Build());
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!Keyboard.current.fKey.wasPressedThisFrame)
            return;

        foreach (var player in SystemAPI.Query<RefRO<FirstPersonPlayer>>())
        {
            Entity characterEntity = player.ValueRO.ControlledCharacter;

            FirstPersonCharacterComponent character =
                SystemAPI.GetComponent<FirstPersonCharacterComponent>(characterEntity);

            character.FlyMode = !character.FlyMode;

            SystemAPI.SetComponent(characterEntity, character);
            UnityEngine.Debug.Log(character.FlyMode);
        }
    }
}
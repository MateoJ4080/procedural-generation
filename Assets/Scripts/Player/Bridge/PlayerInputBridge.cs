using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class PlayerInputBridge : MonoBehaviour
{
    public InputSystem_Actions controls;

    private EntityManager em;
    private Entity playerEntity;
    private bool initialized;

    void Awake()
    {
        em = World.DefaultGameObjectInjectionWorld.EntityManager;

        controls = new InputSystem_Actions();
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Update()
    {
        if (!initialized)
        {
            var query = em.CreateEntityQuery(typeof(PlayerTag));

            if (query.IsEmpty)
                return;

            playerEntity = query.GetSingletonEntity();
            initialized = true;
        }

        float2 moveInput = (float2)controls.Player.Move.ReadValue<Vector2>();
        float2 lookInput = (float2)controls.Player.Look.ReadValue<Vector2>();

        em.SetComponentData(playerEntity, new PlayerMoveInput { Value = moveInput });
        em.SetComponentData(playerEntity, new CameraLookInput { Value = lookInput });
    }
}
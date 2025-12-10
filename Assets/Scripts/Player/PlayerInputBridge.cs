using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class PlayerInputBridge : MonoBehaviour
{
    public InputSystem_Actions controls;

    private EntityManager em;
    private Entity playerEntity;

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

    void Start()
    {
        playerEntity = em.CreateEntityQuery(typeof(PlayerTag)).GetSingletonEntity();
    }

    void Update()
    {
        float2 moveInput = (float2)controls.Player.Move.ReadValue<Vector2>();
        float2 lookInput = (float2)controls.Player.Look.ReadValue<Vector2>();

        em.SetComponentData(playerEntity, new PlayerMoveInput { Value = moveInput });
        em.SetComponentData(playerEntity, new CameraLookInput { Value = lookInput });
    }
}

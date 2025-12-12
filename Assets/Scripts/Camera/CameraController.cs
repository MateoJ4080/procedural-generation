using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 1;

    private float pitch;
    private float yaw;

    private InputSystem_Actions controls;

    EntityManager em;
    Entity player;

    void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    {
        // Share camera settings to player to rotate at same speed
        em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var q = em.CreateEntityQuery(typeof(CameraSettings));
        if (!q.IsEmpty) player = q.GetSingletonEntity();
        em.SetComponentData(player, new CameraSettings { Sensitivity = sensitivity });

    }

    void Update()
    {
        // Movement
        if (player != Entity.Null)
        {
            LocalToWorld p = em.GetComponentData<LocalToWorld>(player);
            transform.position = p.Position;
        }

        // Rotation
        Vector2 lookInput = controls.Player.Look.ReadValue<Vector2>();

        pitch -= lookInput.y * sensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -90f, 90f);
        yaw += lookInput.x * sensitivity * Time.deltaTime;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }
}

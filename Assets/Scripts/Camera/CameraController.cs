using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.5f;

    private float pitch = 0;
    private float yaw = 0;

    private InputSystem_Actions controls;

    void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Update()
    {
        Vector2 lookInput = controls.Player.Look.ReadValue<Vector2>();
        lookInput *= sensitivity * 0.01f;

        pitch -= lookInput.y;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        yaw += lookInput.x;

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}

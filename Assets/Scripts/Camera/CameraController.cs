using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.5f;
    [SerializeField] private Transform player;

    float xRotation = 0;

    private InputSystem_Actions playerControls;

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        playerControls.Enable();
    }

    void OnDisable()
    {
        playerControls.Disable();
    }

    void Update()
    {
        Vector2 lookInput = playerControls.Player.Look.ReadValue<Vector2>();
        lookInput *= sensitivity * 0.01f;

        xRotation -= lookInput.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        player.Rotate(Vector3.up * lookInput.x);
    }
}

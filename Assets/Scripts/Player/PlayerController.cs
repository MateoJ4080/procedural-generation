using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private CharacterController cc;
    private InputSystem_Actions controls;

    [SerializeField] float gravity = -9.81f;
    [SerializeField] float speed = 1;
    private Vector3 velocity;
    private Vector2 moveInput;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        controls.Enable();
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
    }

    void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
        controls.Disable();
    }

    void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        Vector3 direction = transform.TransformDirection(new Vector3(moveInput.x, 0, moveInput.y));

        cc.Move(speed * Time.deltaTime * direction);

        if (cc.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class SimplePlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField] private bool useRigidbodyWhenAvailable = true;
    [SerializeField] private string shootLogMessage = "Player Shoot input";
    [SerializeField] private string interceptLogMessage = "Player Intercept input";

    private Rigidbody2D playerRigidbody;
    private Vector2 moveInput;

    public Vector2 MoveInput => moveInput;
    public bool IsMoving => moveInput.sqrMagnitude > 0f;
    public bool WasShootPressedThisFrame { get; private set; }
    public bool WasInterceptPressedThisFrame { get; private set; }

    private void Awake()
    {
        if (useRigidbodyWhenAvailable)
        {
            TryGetComponent(out playerRigidbody);
        }
    }

    private void Update()
    {
        ReadMoveInput();
        ReadActionInput();

        if (playerRigidbody == null)
        {
            MoveTransform(Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (playerRigidbody != null)
        {
            MoveRigidbody(Time.fixedDeltaTime);
        }
    }

    private void ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        moveInput = NormalizeMoveInput(horizontal, vertical);
#else
        moveInput = NormalizeMoveInput(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));
#endif
    }

    private void ReadActionInput()
    {
        WasShootPressedThisFrame = false;
        WasInterceptPressedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            WasShootPressedThisFrame = true;
            Debug.Log(shootLogMessage, this);
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            WasInterceptPressedThisFrame = true;
            Debug.Log(interceptLogMessage, this);
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            WasShootPressedThisFrame = true;
            Debug.Log(shootLogMessage, this);
        }

        if (Input.GetMouseButtonDown(1))
        {
            WasInterceptPressedThisFrame = true;
            Debug.Log(interceptLogMessage, this);
        }
#endif
    }

    private static Vector2 NormalizeMoveInput(float horizontal, float vertical)
    {
        Vector2 input = new Vector2(horizontal, vertical);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void MoveTransform(float deltaTime)
    {
        transform.position += (Vector3)(moveInput * moveSpeed * deltaTime);
    }

    private void MoveRigidbody(float deltaTime)
    {
        playerRigidbody.MovePosition(playerRigidbody.position + moveInput * moveSpeed * deltaTime);
    }
}

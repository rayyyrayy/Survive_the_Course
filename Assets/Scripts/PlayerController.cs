using UnityEngine;using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector3 input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Update()
    {
        Vector2 keyboardInput = Vector2.zero;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float h = 0f;
            float v = 0f;

            if (keyboard.leftArrowKey.isPressed)
                h -= 1f;
            if (keyboard.rightArrowKey.isPressed)
                h += 1f;
            if (keyboard.downArrowKey.isPressed)
                v -= 1f;
            if (keyboard.upArrowKey.isPressed)
                v += 1f;

            keyboardInput = new Vector2(h, v);
        }

        Vector2 joystickInput = Vector2.zero;
        if (VirtualJoystick.Instance != null)
            joystickInput = VirtualJoystick.Instance.InputVector;

        // Combine inputs but clamp so total magnitude never exceeds 1.
        Vector2 combined = keyboardInput + joystickInput;
        if (combined.sqrMagnitude > 1f)
            combined = combined.normalized;

        input = new Vector3(combined.x, 0f, combined.y);
    }


    private void FixedUpdate()
    {
        Vector3 velocity = input * moveSpeed;
        Vector3 move = velocity * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Enemy"))
        {
            GameManager.Instance?.HandlePlayerHit();
        }
    }
}

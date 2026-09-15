using UnityEngine;

public class CarJumpMechanic : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private KeyCode jumpKey = KeyCode.K;
    [SerializeField] private Vector3 jumpDirection = Vector3.up;
    [SerializeField, Min(0f)] private float jumpForce = 10f;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    [Header("Wheel Ground Check")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;
    [SerializeField] private LayerMask ground;

    private bool canJump;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (targetRigidbody == null)
        {
            Debug.LogWarning($"{nameof(CarJumpMechanic)} requires a target Rigidbody.", this);
        }

        if (frontLeftWheelCollider == null || frontRightWheelCollider == null ||
            rearLeftWheelCollider == null || rearRightWheelCollider == null)
        {
            Debug.LogWarning($"{nameof(CarJumpMechanic)} requires all four WheelColliders.", this);
        }
    }

    private void Start()
    {
        canJump = IsGrounded();
    }

    private void Update()
    {
        bool isGrounded = IsGrounded();

        if (!canJump && isGrounded)
        {
            canJump = true;
        }

        if (Input.GetKeyDown(jumpKey))
        {
            Debug.Log($"Jump button {jumpKey} pressed.", this);

            if (canJump && isGrounded)
            {
                Jump();
            }
        }
    }

    private void Jump()
    {
        if (targetRigidbody == null)
        {
            Debug.LogWarning($"{nameof(CarJumpMechanic)} requires a target Rigidbody.", this);
            return;
        }

        Vector3 direction = jumpDirection.sqrMagnitude > 0f
            ? jumpDirection.normalized
            : Vector3.up;

        targetRigidbody.AddForce(direction * jumpForce, forceMode);
        canJump = false;
        Debug.Log("Car jump force applied.", this);
    }

    private bool IsGrounded()
    {
        return WheelTouchesGround(frontLeftWheelCollider) ||
               WheelTouchesGround(frontRightWheelCollider) ||
               WheelTouchesGround(rearLeftWheelCollider) ||
               WheelTouchesGround(rearRightWheelCollider);
    }

    private bool WheelTouchesGround(WheelCollider wheelCollider)
    {
        if (wheelCollider == null || !wheelCollider.GetGroundHit(out WheelHit hit))
        {
            return false;
        }

        return hit.collider != null &&
               (ground.value & (1 << hit.collider.gameObject.layer)) != 0;
    }
}

// This script checks four WheelColliders against the Ground LayerMask. The jump key applies one impulse while grounded,
// locks additional jumps in the air, and unlocks jumping when any assigned wheel lands on an approved ground surface.

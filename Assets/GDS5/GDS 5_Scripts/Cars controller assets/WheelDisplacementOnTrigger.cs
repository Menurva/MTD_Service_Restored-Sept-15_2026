using UnityEngine;

public class WheelDisplacementOnTrigger : MonoBehaviour
{
    private enum WheelMode
    {
        Normal,
        Reduced,
        Expanded
    }

    [Header("Car Speed")]
    [SerializeField] private PrometeoCarController carController;
    [SerializeField] private int displacedMaxSpeed = 120;
    [SerializeField] private int displacedMaxReverseSpeed = 60;
    [SerializeField] private int displacedAccelerationMultiplier = 5;

    [Header("Wheel Meshes")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider[] colliderTargets = new WheelCollider[4];

    [Header("Wheel Scale")]
    [SerializeField] private float reducedScaleMultiplier = 0.75f;
    [SerializeField] private float growScaleMultiplier = 2f;

    [Header("Effects")]
    [SerializeField] private Transform frontLeftEffect;
    [SerializeField] private Transform frontRightEffect;
    [SerializeField] private Transform rearLeftEffect;
    [SerializeField] private Transform rearRightEffect;

    [Header("Expanded Local Positions")]
    [SerializeField] private Vector3 frontLeftTargetLocalPosition;
    [SerializeField] private Vector3 frontRightTargetLocalPosition;
    [SerializeField] private Vector3 rearLeftTargetLocalPosition;
    [SerializeField] private Vector3 rearRightTargetLocalPosition;

    private readonly Transform[] wheelTargets = new Transform[4];
    private readonly Vector3[] originalWheelLocalPositions = new Vector3[4];
    private readonly Vector3[] originalWheelScales = new Vector3[4];
    private readonly Vector3[] originalColliderLocalPositions = new Vector3[4];
    private readonly float[] originalColliderRadii = new float[4];
    private readonly Transform[] effectTargets = new Transform[4];
    private readonly Vector3[] originalEffectLocalPositions = new Vector3[4];
    private readonly Vector3[] expandedLocalPositions = new Vector3[4];

    private int originalMaxSpeed;
    private int originalMaxReverseSpeed;
    private int originalAccelerationMultiplier;
    private WheelMode currentMode;

    private void Start()
    {
        BuildTargetCollections();
        StoreOriginalCarSpeed();
        StoreOriginalWheelValues();
        WarnForMissingAssignments();
        ApplyMode(WheelMode.Normal);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SetMode(currentMode == WheelMode.Reduced ? WheelMode.Normal : WheelMode.Reduced);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            SetMode(currentMode == WheelMode.Expanded ? WheelMode.Normal : WheelMode.Expanded);
        }
    }

    private void BuildTargetCollections()
    {
        wheelTargets[0] = frontLeftWheel;
        wheelTargets[1] = frontRightWheel;
        wheelTargets[2] = rearLeftWheel;
        wheelTargets[3] = rearRightWheel;

        effectTargets[0] = frontLeftEffect;
        effectTargets[1] = frontRightEffect;
        effectTargets[2] = rearLeftEffect;
        effectTargets[3] = rearRightEffect;

        expandedLocalPositions[0] = frontLeftTargetLocalPosition;
        expandedLocalPositions[1] = frontRightTargetLocalPosition;
        expandedLocalPositions[2] = rearLeftTargetLocalPosition;
        expandedLocalPositions[3] = rearRightTargetLocalPosition;
    }

    private void StoreOriginalCarSpeed()
    {
        if (carController == null)
        {
            return;
        }

        originalMaxSpeed = carController.maxSpeed;
        originalMaxReverseSpeed = carController.maxReverseSpeed;
        originalAccelerationMultiplier = carController.accelerationMultiplier;
    }

    private void StoreOriginalWheelValues()
    {
        for (int i = 0; i < wheelTargets.Length; i++)
        {
            if (wheelTargets[i] != null)
            {
                originalWheelLocalPositions[i] = wheelTargets[i].localPosition;
                originalWheelScales[i] = wheelTargets[i].localScale;
            }

            if (HasColliderAt(i))
            {
                originalColliderLocalPositions[i] = colliderTargets[i].transform.localPosition;
                originalColliderRadii[i] = colliderTargets[i].radius;
            }

            if (effectTargets[i] != null)
            {
                originalEffectLocalPositions[i] = effectTargets[i].localPosition;
            }
        }
    }

    private void SetMode(WheelMode nextMode)
    {
        currentMode = nextMode;
        ApplyMode(currentMode);
    }

    private void ApplyMode(WheelMode mode)
    {
        float scaleMultiplier = 1f;

        if (mode == WheelMode.Reduced)
        {
            scaleMultiplier = reducedScaleMultiplier;
        }
        else if (mode == WheelMode.Expanded)
        {
            scaleMultiplier = growScaleMultiplier;
        }

        bool useExpandedPosition = mode == WheelMode.Expanded;

        for (int i = 0; i < wheelTargets.Length; i++)
        {
            ApplyWheelMesh(i, scaleMultiplier, useExpandedPosition);
            ApplyWheelCollider(i, scaleMultiplier, useExpandedPosition);
            ApplyEffect(i, useExpandedPosition);
        }

        ApplyCarSpeed(useExpandedPosition);
    }

    private void ApplyWheelMesh(int index, float scaleMultiplier, bool useExpandedPosition)
    {
        Transform wheel = wheelTargets[index];

        if (wheel == null)
        {
            return;
        }

        wheel.localScale = originalWheelScales[index] * scaleMultiplier;
        wheel.localPosition = useExpandedPosition ? expandedLocalPositions[index] : originalWheelLocalPositions[index];
    }

    private void ApplyWheelCollider(int index, float scaleMultiplier, bool useExpandedPosition)
    {
        if (!HasColliderAt(index))
        {
            return;
        }

        WheelCollider wheelCollider = colliderTargets[index];
        wheelCollider.radius = originalColliderRadii[index] * scaleMultiplier;
        wheelCollider.transform.localPosition = useExpandedPosition
            ? expandedLocalPositions[index]
            : originalColliderLocalPositions[index];
    }

    private void ApplyEffect(int index, bool useExpandedPosition)
    {
        Transform effectTarget = effectTargets[index];

        if (effectTarget == null)
        {
            return;
        }

        Vector3 displacementOffset = expandedLocalPositions[index] - originalWheelLocalPositions[index];
        effectTarget.localPosition = useExpandedPosition
            ? originalEffectLocalPositions[index] + displacementOffset
            : originalEffectLocalPositions[index];
    }

    private void ApplyCarSpeed(bool useExpandedValues)
    {
        if (carController == null)
        {
            return;
        }

        carController.maxSpeed = useExpandedValues ? displacedMaxSpeed : originalMaxSpeed;
        carController.maxReverseSpeed = useExpandedValues ? displacedMaxReverseSpeed : originalMaxReverseSpeed;
        carController.accelerationMultiplier = useExpandedValues
            ? displacedAccelerationMultiplier
            : originalAccelerationMultiplier;
    }

    private bool HasColliderAt(int index)
    {
        return colliderTargets != null
            && index >= 0
            && index < colliderTargets.Length
            && colliderTargets[index] != null;
    }

    private void WarnForMissingAssignments()
    {
        if (carController == null)
        {
            Debug.LogWarning("Car Controller is not assigned. Wheel transformation will work, but car speed will not change.", this);
        }

        for (int i = 0; i < wheelTargets.Length; i++)
        {
            if (wheelTargets[i] == null)
            {
                Debug.LogWarning($"Wheel mesh {i + 1} is not assigned on WheelDisplacementOnTrigger.", this);
            }

            if (!HasColliderAt(i))
            {
                Debug.LogWarning($"WheelCollider {i + 1} is not assigned on WheelDisplacementOnTrigger.", this);
            }
        }
    }
}

// This component keeps wheel scale, collider radius, displacement, effects, and car-performance changes in one three-state mechanic.

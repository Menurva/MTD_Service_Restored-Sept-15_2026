using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bomb : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionForce = 500f;
    [SerializeField, Min(0f)] private float _explosionDamage = 100f;
    [SerializeField] private GameObject _particles;

    [Header("Pickup and Release")]
    [SerializeField] private Collider _pickupTrigger;
    [SerializeField, Min(0f)] private float _releaseFuseSeconds = 3f;

    private Rigidbody _bombRigidbody;
    private HingeJoint _hingeJoint;
    private BombHitchPoint _currentHitchPoint;
    private bool _isAttached;
    private bool _isArmed;
    private bool _hasExploded;

    public event Action<Bomb> Exploded;

    private void Awake()
    {
        _bombRigidbody = GetComponent<Rigidbody>();
        _hingeJoint = GetComponent<HingeJoint>();

        if (_hingeJoint != null)
        {
            Destroy(_hingeJoint);
            _hingeJoint = null;
        }

        FindPickupTriggerIfNeeded();

        if (_pickupTrigger != null)
        {
            _pickupTrigger.isTrigger = true;
            _pickupTrigger.enabled = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(Bomb)} requires a trigger Collider for pickup.", this);
        }
    }

    private void Update()
    {
        if (!_isAttached || _hasExploded)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Explode();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            ReleaseAndArm();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isAttached || _isArmed || _hasExploded)
        {
            return;
        }

        Rigidbody carRigidbody = other.attachedRigidbody;
        if (carRigidbody == null)
        {
            return;
        }

        BombHitchPoint hitchPoint = carRigidbody.GetComponentInChildren<BombHitchPoint>();
        if (hitchPoint == null || !hitchPoint.TryReserve(this))
        {
            return;
        }

        AttachToCar(hitchPoint, carRigidbody);
    }

    private void AttachToCar(BombHitchPoint hitchPoint, Rigidbody carRigidbody)
    {
        _currentHitchPoint = hitchPoint;
        _isAttached = true;

        _hingeJoint = gameObject.AddComponent<HingeJoint>();
        _hingeJoint.anchor = new Vector3(0f, 0.5f, 0f);
        _hingeJoint.axis = Vector3.right;

        transform.SetParent(null, true);
        transform.rotation = hitchPoint.transform.rotation;
        transform.position = hitchPoint.transform.position - transform.TransformVector(_hingeJoint.anchor);

        _bombRigidbody.linearVelocity = carRigidbody.linearVelocity;
        _bombRigidbody.angularVelocity = carRigidbody.angularVelocity;

        _hingeJoint.autoConfigureConnectedAnchor = false;
        _hingeJoint.connectedBody = carRigidbody;
        _hingeJoint.connectedAnchor = carRigidbody.transform.InverseTransformPoint(hitchPoint.transform.position);
        _hingeJoint.enableCollision = false;

        if (_pickupTrigger != null)
        {
            _pickupTrigger.enabled = false;
        }
    }

    private void ReleaseAndArm()
    {
        DisconnectFromCar();
        _isArmed = true;
        StartCoroutine(ExplodeAfterDelay());
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(_releaseFuseSeconds);
        Explode();
    }

    private void DisconnectFromCar()
    {
        if (_hingeJoint != null)
        {
            Destroy(_hingeJoint);
            _hingeJoint = null;
        }

        if (_currentHitchPoint != null)
        {
            _currentHitchPoint.Release(this);
            _currentHitchPoint = null;
        }

        _isAttached = false;
    }

    private void Explode()
    {
        if (_hasExploded)
        {
            return;
        }

        _hasExploded = true;
        DisconnectFromCar();
        Exploded?.Invoke(this);

        Collider[] surroundingObjects = Physics.OverlapSphere(transform.position, _explosionRadius);
        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();
        HashSet<DestructibleWall> affectedWalls = new HashSet<DestructibleWall>();

        foreach (Collider surroundingObject in surroundingObjects)
        {
            DestructibleWall wall = surroundingObject.GetComponentInParent<DestructibleWall>();
            if (wall != null && affectedWalls.Add(wall))
            {
                wall.TakeExplosionDamage(
                    _explosionDamage,
                    transform.position,
                    _explosionForce,
                    _explosionRadius);
            }

            Rigidbody affectedBody = surroundingObject.attachedRigidbody;
            if (affectedBody == null || affectedBody == _bombRigidbody || !affectedBodies.Add(affectedBody))
            {
                continue;
            }

            affectedBody.AddExplosionForce(
                _explosionForce,
                transform.position,
                _explosionRadius,
                1f);
        }

        if (_particles != null)
        {
            Instantiate(_particles, transform.position, Quaternion.identity);
            Debug.Log($"[{nameof(Bomb)}] {name}: fractured bomb replacement spawned ({_particles.name}).", this);
        }
        else
        {
            Debug.LogWarning($"[{nameof(Bomb)}] {name}: fractured bomb replacement is not assigned.", this);
        }

        Destroy(gameObject);
    }

    private void FindPickupTriggerIfNeeded()
    {
        if (_pickupTrigger != null)
        {
            return;
        }

        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider candidate in colliders)
        {
            if (candidate.isTrigger)
            {
                _pickupTrigger = candidate;
                return;
            }
        }
    }

    private void OnDestroy()
    {
        if (_currentHitchPoint != null)
        {
            _currentHitchPoint.Release(this);
        }
    }

    private void OnValidate()
    {
        _explosionRadius = Mathf.Max(0f, _explosionRadius);
        _explosionForce = Mathf.Max(0f, _explosionForce);
        _explosionDamage = Mathf.Max(0f, _explosionDamage);
        _releaseFuseSeconds = Mathf.Max(0f, _releaseFuseSeconds);
    }
}

// Bomb attaches to the car, announces its one confirmed explosion, damages nearby walls, pushes movable objects, creates its fractured replacement, and destroys itself.
// BombVFXandSFX listens to the Exploded event so gameplay force and presentation each have one source of truth.

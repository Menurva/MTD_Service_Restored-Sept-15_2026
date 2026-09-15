using System;
using UnityEngine;

public class BreakableSecond : MonoBehaviour
{
    [SerializeField] private GameObject _replacementObject;
    [SerializeField] private float _breakforce = 2f;
    [SerializeField] private float _collisionmultiplier = 100f;
    [SerializeField] private bool _preserveReplacementScale;
    [SerializeField] private bool _broken;

    public event Action<BreakableSecond> Broken;

    public bool IsBroken => _broken;
    public Vector3 BreakPosition { get; private set; }
    public Vector3 BreakNormal { get; private set; } = Vector3.up;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnCollisionEnter(Collision collision)
    {
        if (_broken) return;
        if (collision.relativeVelocity.magnitude >= _breakforce)
        {
            if (_replacementObject == null)
            {
                Debug.LogWarning($"{nameof(BreakableSecond)} has no replacement object assigned.", this);
                return;
            }

            _broken = true;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                BreakPosition = contact.point;
                BreakNormal = contact.normal;
            }
            else
            {
                BreakPosition = transform.position;
                BreakNormal = Vector3.up;
            }

            Vector3 originalWorldScale = transform.lossyScale;
            var replacement = Instantiate(_replacementObject, transform.position, transform.rotation);
            if (!_preserveReplacementScale)
            {
                replacement.transform.localScale = originalWorldScale;
            }

            var rbs = replacement.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.AddExplosionForce(collision.relativeVelocity.magnitude * _collisionmultiplier, BreakPosition, 2f);
            }

            Broken?.Invoke(this);
            Destroy(gameObject);
        }
    }
}

// BreakableSecond replaces a strong-hit object, pushes its fractured pieces from the impact point, and reports the confirmed break.
// BreakPosition and BreakNormal let listeners place destruction effects at the collision without changing the existing Broken event.

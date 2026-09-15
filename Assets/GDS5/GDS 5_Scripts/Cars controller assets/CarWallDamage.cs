using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarWallDamage : MonoBehaviour
{
    private Rigidbody carRigidbody;

    private void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        DestructibleWall wall = collision.collider.GetComponentInParent<DestructibleWall>();
        if (wall == null)
        {
            return;
        }

        Vector3 impactPosition = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);

        wall.TakeCarImpactDamage(
            collision.relativeVelocity.magnitude,
            impactPosition);
    }

    private void OnTriggerEnter(Collider other)
    {
        DestructibleWall wall = other.GetComponentInParent<DestructibleWall>();
        if (wall == null || carRigidbody == null)
        {
            return;
        }

        wall.TakeCarImpactDamage(
            carRigidbody.linearVelocity.magnitude,
            other.ClosestPoint(transform.position));
    }
}

// CarWallDamage reads collisions or trigger entries from the car Rigidbody and forwards each wall impact to the matching DestructibleWall parent.

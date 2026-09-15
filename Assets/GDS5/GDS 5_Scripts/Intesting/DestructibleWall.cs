using CodeMonkey.HealthSystemCM;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(HealthSystemComponent))]
public class DestructibleWall : MonoBehaviour
{
    [Header("Wall Versions")]
    [SerializeField] private GameObject normalWall;
    [FormerlySerializedAs("fracturedPrefab")]
    [SerializeField] private GameObject fracturedWall;

    [Header("Destruction Rules")]
    [SerializeField] private bool destroyOnFirstCarImpact = true;
    [SerializeField] private bool destroyOnExplosion = true;

    [Header("Car Damage")]
    [SerializeField, Min(0f)] private float minimumCarImpactSpeed = 3f;
    [SerializeField, Min(0f)] private float carDamageMultiplier = 5f;
    [SerializeField, Min(0f)] private float carDamageCooldown = 0.2f;
    [SerializeField, Min(0f)] private float carFragmentForceMultiplier = 25f;
    [SerializeField, Min(0.01f)] private float carFragmentForceRadius = 2f;

    private HealthSystem healthSystem;
    private Collider[] wallColliders;
    private bool isDestroyed;
    private float nextCarDamageTime;

    private void Start()
    {
        HealthSystemComponent healthComponent = GetComponent<HealthSystemComponent>();
        healthSystem = healthComponent.GetHealthSystem();
        wallColliders = GetComponentsInChildren<Collider>(true);

        if (normalWall == null)
        {
            Debug.LogWarning(
                $"{nameof(DestructibleWall)} requires the normal wall child to be assigned.",
                this);
        }

        if (fracturedWall == null)
        {
            Debug.LogWarning(
                $"{nameof(DestructibleWall)} requires the matching fractured wall to be assigned.",
                this);
            return;
        }

        if (UsesPreplacedFracturedWall())
        {
            fracturedWall.SetActive(false);

            if (normalWall != null && normalWall != gameObject)
            {
                normalWall.SetActive(true);
            }
        }
    }

    public void TakeCarImpactDamage(float impactSpeed, Vector3 impactPosition)
    {
        if (isDestroyed || healthSystem == null || impactSpeed < minimumCarImpactSpeed)
        {
            return;
        }

        if (Time.time < nextCarDamageTime)
        {
            return;
        }

        nextCarDamageTime = Time.time + carDamageCooldown;

        float damage = destroyOnFirstCarImpact
            ? healthSystem.GetHealth()
            : impactSpeed * carDamageMultiplier;
        float fragmentForce = impactSpeed * carFragmentForceMultiplier;

        ApplyDamage(damage, impactPosition, fragmentForce, carFragmentForceRadius);
    }

    public void TakeExplosionDamage(
        float maximumDamage,
        Vector3 explosionPosition,
        float explosionForce,
        float explosionRadius)
    {
        if (isDestroyed || healthSystem == null)
        {
            return;
        }

        float damage;
        if (destroyOnExplosion)
        {
            damage = healthSystem.GetHealth();
        }
        else
        {
            float distance = FindClosestDistance(explosionPosition);
            float damagePercentage = explosionRadius > 0f
                ? 1f - Mathf.Clamp01(distance / explosionRadius)
                : 1f;
            damage = maximumDamage * damagePercentage;
        }

        ApplyDamage(damage, explosionPosition, explosionForce, explosionRadius);
    }

    private float FindClosestDistance(Vector3 position)
    {
        float closestDistance = float.MaxValue;

        foreach (Collider wallCollider in wallColliders)
        {
            if (wallCollider == null ||
                IsPartOfFracturedWall(wallCollider.transform))
            {
                continue;
            }

            Vector3 closestPoint = wallCollider.ClosestPoint(position);
            float distance = Vector3.Distance(position, closestPoint);
            closestDistance = Mathf.Min(closestDistance, distance);
        }

        return closestDistance == float.MaxValue
            ? Vector3.Distance(position, transform.position)
            : closestDistance;
    }

    private void ApplyDamage(
        float damage,
        Vector3 forcePosition,
        float fragmentForce,
        float fragmentForceRadius)
    {
        if (damage <= 0f)
        {
            return;
        }

        healthSystem.Damage(damage);
        if (healthSystem.IsDead())
        {
            BreakWall(forcePosition, fragmentForce, fragmentForceRadius);
        }
    }

    private void BreakWall(
        Vector3 forcePosition,
        float fragmentForce,
        float fragmentForceRadius)
    {
        if (isDestroyed || fracturedWall == null)
        {
            return;
        }

        isDestroyed = true;
        GameObject activeFracturedWall;

        if (UsesPreplacedFracturedWall())
        {
            DisableNormalWallColliders();

            if (normalWall != null && normalWall != gameObject)
            {
                normalWall.SetActive(false);
            }

            fracturedWall.SetActive(true);
            activeFracturedWall = fracturedWall;
        }
        else
        {
            activeFracturedWall = Instantiate(
                fracturedWall,
                transform.position,
                transform.rotation,
                transform.parent);
        }

        Rigidbody[] fragments = activeFracturedWall.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody fragment in fragments)
        {
            fragment.AddExplosionForce(
                fragmentForce,
                forcePosition,
                fragmentForceRadius,
                1f);
        }

        if (!UsesPreplacedFracturedWall())
        {
            Destroy(gameObject);
        }
    }

    private void DisableNormalWallColliders()
    {
        foreach (Collider wallCollider in wallColliders)
        {
            if (wallCollider != null && !IsPartOfFracturedWall(wallCollider.transform))
            {
                wallCollider.enabled = false;
            }
        }
    }

    private bool UsesPreplacedFracturedWall()
    {
        return fracturedWall != null && fracturedWall.scene.IsValid();
    }

    private bool IsPartOfFracturedWall(Transform candidate)
    {
        return fracturedWall != null &&
               (candidate == fracturedWall.transform || candidate.IsChildOf(fracturedWall.transform));
    }

    private void OnValidate()
    {
        minimumCarImpactSpeed = Mathf.Max(0f, minimumCarImpactSpeed);
        carDamageMultiplier = Mathf.Max(0f, carDamageMultiplier);
        carDamageCooldown = Mathf.Max(0f, carDamageCooldown);
        carFragmentForceMultiplier = Mathf.Max(0f, carFragmentForceMultiplier);
        carFragmentForceRadius = Mathf.Max(0.01f, carFragmentForceRadius);
    }
}

// DestructibleWall keeps one health value, accepts car and bomb damage, and switches each normal wall to its assigned fractured version.

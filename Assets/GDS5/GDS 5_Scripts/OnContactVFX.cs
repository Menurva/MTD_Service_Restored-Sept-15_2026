using System.Collections.Generic;
using UnityEngine;

public class OnContactVFX : MonoBehaviour
{
    [Header("Fractured Walls")]
    [SerializeField] private Collider[] wallColliders;

    [Header("Explosion Visual")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField, Min(0.1f)] private float vfxLifetime = 10f;

    [Header("Explosion Sound")]
    [SerializeField] private AudioSource explosionAudioSource;
    [SerializeField] private AudioClip sfxAudioClip;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private readonly Dictionary<BreakableSecond, Collider> subscribedWalls =
        new Dictionary<BreakableSecond, Collider>();
    private readonly HashSet<BreakableSecond> wallsWithPlayedEffects =
        new HashSet<BreakableSecond>();

    private void Awake()
    {
        if (wallColliders == null || wallColliders.Length == 0)
        {
            Debug.LogWarning($"[OnContactVFX] {name}: no Wall Colliders are assigned.", this);
            return;
        }

        foreach (Collider wallCollider in wallColliders)
        {
            if (wallCollider == null)
            {
                Debug.LogWarning($"[OnContactVFX] {name}: Wall Colliders contains an empty entry.", this);
                continue;
            }

            BreakableSecond breakableWall = wallCollider.GetComponent<BreakableSecond>();
            if (breakableWall == null)
            {
                Debug.LogWarning(
                    $"[OnContactVFX] {name}: '{wallCollider.name}' has no BreakableSecond component.",
                    this);
                continue;
            }

            if (subscribedWalls.ContainsKey(breakableWall))
            {
                Debug.LogWarning(
                    $"[OnContactVFX] {name}: '{wallCollider.name}' is assigned more than once.",
                    this);
                continue;
            }

            subscribedWalls.Add(breakableWall, wallCollider);
            breakableWall.Broken += HandleWallBroken;
        }

        if (effectSpawnPoint == null)
            Debug.LogWarning($"[OnContactVFX] {name}: Effect Spawn Point is not assigned; the impact position will be used.", this);

        if (explosionAudioSource == null)
            Debug.LogWarning($"[OnContactVFX] {name}: Explosion Audio Source is not assigned.", this);
    }

    private void OnDestroy()
    {
        foreach (BreakableSecond wall in subscribedWalls.Keys)
        {
            if (wall != null)
                wall.Broken -= HandleWallBroken;
        }
    }

    private void HandleWallBroken(BreakableSecond brokenWall)
    {
        if (!isActiveAndEnabled || brokenWall == null)
            return;

        if (!subscribedWalls.TryGetValue(brokenWall, out Collider wallCollider))
        {
            Debug.LogWarning($"[OnContactVFX] {name}: an unassigned wall reported a break.", this);
            return;
        }

        if (!wallsWithPlayedEffects.Add(brokenWall))
        {
            Debug.Log($"[OnContactVFX] {name}: effects for '{wallCollider.name}' already played.", this);
            return;
        }

        Vector3 position = brokenWall.BreakPosition;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, brokenWall.BreakNormal);
        if (effectSpawnPoint != null)
        {
            position += effectSpawnPoint.position - transform.position;
            rotation = effectSpawnPoint.rotation;
        }

        Debug.Log(
            $"[OnContactVFX] {name}: '{wallCollider.name}' fractured. Playing effects " +
            $"({wallsWithPlayedEffects.Count}/{subscribedWalls.Count}).",
            this);

        if (vfxPrefab != null)
        {
            GameObject effect = Instantiate(vfxPrefab, position, rotation);
            effect.SetActive(true);
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                particles.Play(false);
            Destroy(effect, Mathf.Max(0.1f, vfxLifetime));
            Debug.Log($"[OnContactVFX] {name}: VFX spawned ({vfxPrefab.name}).", this);
        }
        else
        {
            Debug.LogWarning($"[OnContactVFX] {name}: VFX Prefab is not assigned.", this);
        }

        if (explosionAudioSource != null && sfxAudioClip != null)
        {
            explosionAudioSource.PlayOneShot(sfxAudioClip, sfxVolume);
            Debug.Log($"[OnContactVFX] {name}: SFX started for fractured wall '{wallCollider.name}'.", this);
        }
        else if (explosionAudioSource == null)
        {
            Debug.LogWarning($"[OnContactVFX] {name}: Explosion Audio Source is not assigned.", this);
        }
        else
        {
            Debug.LogWarning($"[OnContactVFX] {name}: SFX Audio Clip is not assigned.", this);
        }
    }
}

// OnContactVFX listens to the Broken event from each assigned wall's BreakableSecond component.
// Touching a wall does nothing; each wall plays its VFX and SFX only after it successfully creates its fractured replacement.
// The movable spawn point provides a visible position offset and rotation; its AudioSource controls spatial sound and distance.
// VFX Lifetime removes the independently spawned effect after the selected number of seconds.

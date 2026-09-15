using UnityEngine;

public class BombVFXandSFX : MonoBehaviour
{
    [Header("Bomb Event")]
    [SerializeField] private Bomb bomb;

    [Header("Explosion Visual")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField, Min(0.1f)] private float vfxLifetime = 10f;

    [Header("Explosion Sound")]
    [SerializeField] private AudioSource explosionAudioSource;
    [SerializeField] private AudioClip sfxAudioClip;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private bool hasPlayed;

    private void Awake()
    {
        if (bomb == null)
            bomb = GetComponent<Bomb>();

        if (bomb != null)
            bomb.Exploded += HandleBombExploded;
        else
            Debug.LogWarning($"[{nameof(BombVFXandSFX)}] {name}: Bomb is not assigned.", this);

        if (effectSpawnPoint == null)
            Debug.LogWarning($"[{nameof(BombVFXandSFX)}] {name}: Effect Spawn Point is not assigned.", this);

        if (explosionAudioSource == null)
            Debug.LogWarning($"[{nameof(BombVFXandSFX)}] {name}: Explosion Audio Source is not assigned.", this);
    }

    private void OnDestroy()
    {
        if (bomb != null)
            bomb.Exploded -= HandleBombExploded;
    }

    private void HandleBombExploded(Bomb explodedBomb)
    {
        if (hasPlayed)
        {
            Debug.Log($"[{nameof(BombVFXandSFX)}] {name}: repeat explosion event ignored.", this);
            return;
        }

        hasPlayed = true;
        Transform spawnPoint = effectSpawnPoint != null ? effectSpawnPoint : transform;
        Vector3 position = spawnPoint.position;
        Quaternion rotation = spawnPoint.rotation;

        Debug.Log($"[{nameof(BombVFXandSFX)}] {name}: explosion confirmed; playing VFX and SFX.", this);
        PlayVfx(position, rotation);
        PlaySfx(position, rotation);
    }

    private void PlayVfx(Vector3 position, Quaternion rotation)
    {
        if (vfxPrefab == null)
        {
            Debug.LogWarning($"[{nameof(BombVFXandSFX)}] {name}: VFX Prefab is not assigned.", this);
            return;
        }

        GameObject effect = Instantiate(vfxPrefab, position, rotation);
        effect.SetActive(true);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            particles.Play(false);

        Destroy(effect, Mathf.Max(0.1f, vfxLifetime));
        Debug.Log($"[{nameof(BombVFXandSFX)}] {name}: VFX spawned ({vfxPrefab.name}).", this);
    }

    private void PlaySfx(Vector3 position, Quaternion rotation)
    {
        if (explosionAudioSource == null || sfxAudioClip == null)
        {
            string missingField = explosionAudioSource == null
                ? "Explosion Audio Source"
                : "SFX Audio Clip";
            Debug.LogWarning($"[{nameof(BombVFXandSFX)}] {name}: {missingField} is not assigned.", this);
            return;
        }

        AudioSource audioCopy = Instantiate(explosionAudioSource, position, rotation);
        audioCopy.transform.SetParent(null);
        audioCopy.playOnAwake = false;
        audioCopy.clip = sfxAudioClip;
        audioCopy.volume *= sfxVolume;
        audioCopy.loop = false;
        audioCopy.Play();

        float playbackDuration = sfxAudioClip.length /
            Mathf.Max(Mathf.Abs(audioCopy.pitch), 0.01f);
        Destroy(audioCopy.gameObject, playbackDuration + 0.1f);
        Debug.Log($"[{nameof(BombVFXandSFX)}] {name}: SFX started ({sfxAudioClip.name}).", this);
    }
}

// BombVFXandSFX listens for Bomb.Exploded and creates the assigned VFX and a temporary AudioSource once.
// Both copies are independent of the bomb, so they finish after the intact bomb object is destroyed.
// Move Effect Spawn Point and edit the source AudioSource to control placement, volume, blend, and sound range.

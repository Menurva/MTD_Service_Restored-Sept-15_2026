using System.Collections.Generic;
using UnityEngine;

public class QuestReceiveTrigger : MonoBehaviour
{
    [Header("Required Scene References")]
    [SerializeField] private DemolitionChallengeManager challengeManager;
    [SerializeField] private Rigidbody playerCarRigidbody;

    private readonly HashSet<Collider> overlappingPlayerColliders = new HashSet<Collider>();
    private Collider triggerCollider;
    private bool mustExitBeforeReopening;
    private bool questAccepted;

    public Collider TriggerCollider => triggerCollider;
    public Rigidbody PlayerCarRigidbody => playerCarRigidbody;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogError(
                $"{nameof(QuestReceiveTrigger)} requires a Collider with Is Trigger enabled.",
                this);
            enabled = false;
            return;
        }

        Debug.Log($"Quest trigger ready at '{name}'.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterPlayerOverlap(other);
    }

    // OnTriggerStay recovers the quest if the car began inside the volume or an entry event was missed.
    private void OnTriggerStay(Collider other)
    {
        RegisterPlayerOverlap(other);
    }

    private void RegisterPlayerOverlap(Collider other)
    {
        if (questAccepted || !BelongsToPlayerCar(other))
        {
            return;
        }

        overlappingPlayerColliders.Add(other);

        if (!mustExitBeforeReopening &&
            challengeManager != null)
        {
            challengeManager.TryOpenQuestPanel(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!BelongsToPlayerCar(other))
        {
            return;
        }

        overlappingPlayerColliders.Remove(other);

        if (overlappingPlayerColliders.Count == 0)
        {
            mustExitBeforeReopening = false;

            if (challengeManager != null)
            {
                challengeManager.NotifyQuestTriggerExited(this);
            }
        }
    }

    private bool BelongsToPlayerCar(Collider other)
    {
        if (other == null || playerCarRigidbody == null)
        {
            return false;
        }

        return other.attachedRigidbody == playerCarRigidbody ||
               other.transform == playerCarRigidbody.transform ||
               other.transform.IsChildOf(playerCarRigidbody.transform);
    }

    public void RequireExitBeforeReopening()
    {
        mustExitBeforeReopening = true;
    }

    public void CompleteQuest()
    {
        questAccepted = true;
        overlappingPlayerColliders.Clear();

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    private void OnValidate()
    {
        Collider assignedCollider = GetComponent<Collider>();
        if (assignedCollider != null && !assignedCollider.isTrigger)
        {
            Debug.LogWarning(
                $"Enable Is Trigger on '{name}' so it can receive the player car.",
                this);
        }
    }
}

// QuestReceiveTrigger detects only the assigned player car, opens the quest once per entry, and requires an exit after rejection.

using UnityEngine;

public class BombHitchPoint : MonoBehaviour
{
    private Bomb _attachedBomb;

    public bool TryReserve(Bomb bomb)
    {
        if (bomb == null || (_attachedBomb != null && _attachedBomb != bomb))
        {
            return false;
        }

        _attachedBomb = bomb;
        return true;
    }

    public void Release(Bomb bomb)
    {
        if (_attachedBomb == bomb)
        {
            _attachedBomb = null;
        }
    }
}

// BombHitchPoint marks the car's exact bomb pivot and prevents more than one bomb from using the same hitch.

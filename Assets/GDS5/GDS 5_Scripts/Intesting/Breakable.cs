using UnityEngine;

[SelectionBase]
public class Breakable : MonoBehaviour
{
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] GameObject intactversion;
    [SerializeField] GameObject brokenversion;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;
    [SerializeField] private Vector3 breakDirection = Vector3.up;
    [SerializeField, Min(0f)] private float breakForce = 10f;

    BoxCollider bc;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        intactversion.SetActive(true);
        brokenversion.SetActive(false);

        bc = GetComponent<BoxCollider>();  
    }

    private void OnMouseDown()
    {
      Break();
       Vector3 direction = breakDirection.sqrMagnitude > 0f
            ? breakDirection.normalized
            : Vector3.up;

        targetRigidbody.AddForce(direction * breakForce, forceMode);
        Debug.Log("Break force applied.", this);
    }

    // Update is called once per frame
private void Break()
    {
        intactversion.SetActive(false);
        brokenversion.SetActive(true);

        bc.enabled = false;
    }
}

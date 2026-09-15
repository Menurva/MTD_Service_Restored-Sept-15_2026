using UnityEngine;
public class TheBombCube : MonoBehaviour 
{
     [SerializeField] private float _triggerforce = 0.5f; //the force that will be applied to the player when the bomb cube is triggered//
     [SerializeField] private float _explosionRadius = 5f; //the radius of the explosion//
     [SerializeField] private float _explosionForce = 500f; //the force of the explosion//
     [SerializeField] private GameObject _particles; //the particle system that will be played when the bomb cube is triggered//
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude >= _triggerforce) 
        {
            var surroundingObjects = Physics.OverlapSphere(transform.position, _explosionRadius);

            foreach (var obj in surroundingObjects) 
            {
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null) continue;

                    rb.AddExplosionForce(_explosionForce, transform.position, _explosionRadius);
                }

                Instantiate(_particles, transform.position, Quaternion.identity);

                Destroy(gameObject);
            }
        }
    }


    // Update is called once per frame

    /*void Update() //we need this to set trigger button for bomb cube//
    {
        
    }*/
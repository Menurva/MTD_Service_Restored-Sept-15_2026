using UnityEngine;

public class Playermovement : MonoBehaviour
{
    public CharacterController controller;
    public float turnSmoothtime = 0.1f;
    float turnSmoothVelocity;
    [SerializeField] private float moveSpeed = 5f;


   

    private void Update()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput).normalized;
        transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);
        Vector3 direction = new Vector3(horizontalInput, 0f, verticalInput).normalized;

     if (movement.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothtime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            controller.Move(direction * moveSpeed * Time.deltaTime);
        }

        LogMovementKeyPresses();

    }

    private void LogMovementKeyPresses()
    {
        if (Input.GetKeyDown(KeyCode.W)) Debug.Log("W key has been pressed.");
        if (Input.GetKeyDown(KeyCode.A)) Debug.Log("A key has been pressed.");
        if (Input.GetKeyDown(KeyCode.S)) Debug.Log("S key has been pressed.");
        if (Input.GetKeyDown(KeyCode.D)) Debug.Log("D key has been pressed.");
    }
}

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float acceleration = 1.0f; // Rate at which speed increases
    public float maxSpeed = 10.0f; // Maximum speed
    public float rotationSpeed = 2.0f; // Rate at which the player rotates

    private Vector3 velocity = Vector3.zero;

    void Update()
    {
        // Input handling
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        // Calculate movement direction
        Vector3 moveDirection = transform.forward * moveVertical + transform.right * moveHorizontal;
        moveDirection.Normalize();

        // Apply acceleration
        velocity += moveDirection * acceleration * Time.deltaTime;

        // Clamp speed
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        // Update position
        transform.position += velocity * Time.deltaTime;

        // Rotate based on mouse movement (optional)
        float rotateHorizontal = Input.GetAxis("Mouse X") * rotationSpeed;
        transform.Rotate(0, rotateHorizontal, 0);
    }
}

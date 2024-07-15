using UnityEngine;

public class RotateDirectionalLight : MonoBehaviour
{
    public float rotationSpeed = 30f; // Adjust this value to control the base rotation speed
    public float acceleratedSpeed = 2f; // Multiplier for accelerated rotation speed
    public KeyCode accelerateKey = KeyCode.Space; // Key to accelerate rotation

    void Update()
    {
        // Check if the space key is held down to accelerate rotation
        float currentRotationSpeed = Input.GetKey(accelerateKey) ? rotationSpeed * acceleratedSpeed : rotationSpeed;

        // Rotate the directional light around the z-axis over time
        transform.Rotate(Vector3.right, currentRotationSpeed * Time.deltaTime);
    }
}

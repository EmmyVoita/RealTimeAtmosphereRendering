using UnityEngine;

public class CameraLookAtOrigin : MonoBehaviour
{

    [ExecuteInEditMode, ImageEffectAllowedInSceneView]
    void Update()
    {
        // Calculate the direction from the camera to the origin
        Vector3 direction = Vector3.Normalize(Vector3.zero - transform.position);

        // Calculate the rotation to look at the origin
        Quaternion rotation = Quaternion.LookRotation(direction);

        // Apply the rotation to the camera
        transform.rotation = rotation;
    }
}


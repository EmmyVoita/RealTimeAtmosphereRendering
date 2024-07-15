

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class VisualizeShader1 : MonoBehaviour
{
    // Set up this camera to match the Scene view camera parameters
    




    //the image effect shader will tied to a material
    
    private Matrix4x4 viewMat;
    private Matrix4x4 projMat;
    private Matrix4x4 viewProjMat;


    [SerializeField]
    private Camera currentCamera;


    [Header("World Position Render Texture And Mat")]
    public Material material;
    public RenderTexture targetWorldPosTexture;

    [Header("Camera Settings in Game View")]
    public float nearClipPlane = 0.1f;
    public float farClipPlane = 10000f;
    public float fieldOfView = 60f;

    public Color clearColor = Color.black;
    public LayerMask cullingMask;



    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!enabled)
        {
            return;
        }

        if (currentCamera == null)
        {
            currentCamera = GetComponent<Camera>();
        }

        if (SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera == Camera.current)
        {
            Camera sceneCamera = UnityEditor.SceneView.lastActiveSceneView.camera;

            currentCamera.CopyFrom(sceneCamera);
            currentCamera.depth = sceneCamera.depth - 1;

            currentCamera.depthTextureMode = DepthTextureMode.Depth;
            currentCamera.targetTexture = targetWorldPosTexture;
            currentCamera.backgroundColor = Color.black;
            currentCamera.clearFlags = CameraClearFlags.SolidColor;
            currentCamera.backgroundColor = clearColor;
            
        }
        else
        {
            // transform the transform.position to 0,0,0
            currentCamera.transform.localPosition = new Vector3(0, 0, 0);
            currentCamera.transform.localRotation = Quaternion.identity;
            currentCamera.nearClipPlane = nearClipPlane;
            currentCamera.farClipPlane = farClipPlane;
            currentCamera.fieldOfView = fieldOfView;

            currentCamera.depthTextureMode = DepthTextureMode.Depth;
            currentCamera.targetTexture = targetWorldPosTexture;
            currentCamera.backgroundColor = clearColor;
            currentCamera.clearFlags = CameraClearFlags.SolidColor;
            currentCamera.backgroundColor = clearColor;
        }
    }
    #endif

  
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {   

        Graphics.Blit(source, destination, material);
    }
}
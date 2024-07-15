using System.Collections;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class VisualizeShader : MonoBehaviour
{
    [SerializeField]
    private Camera currentCamera;

    [Header("World Position Render Textures And Mats")]
    public Material[] materials;
    public RenderTexture[] targetTextures;

    [Header("Camera Settings in Game View")]
    public float nearClipPlane = 0.1f;
    public float farClipPlane = 10000f;
    public float fieldOfView = 60f;
    public Color[] clearColor;
    public LayerMask cullingMask;

    private Matrix4x4 viewMat;
    private Matrix4x4 projMat;
    private Matrix4x4 viewProjMat;
    public  RenderTexture temp;

    void VPI()
    {
        viewMat = currentCamera.worldToCameraMatrix;
        projMat = GL.GetGPUProjectionMatrix(currentCamera.projectionMatrix, false);
        viewProjMat = (projMat * viewMat);
        Shader.SetGlobalMatrix("_ViewProjInv", viewProjMat.inverse);
    }

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
            materials[1].SetTexture("_OccluderTex", temp);

            // Iterate through materials and set target textures
            for (int i = 0; i < materials.Length; i++)
            {
                currentCamera.targetTexture = targetTextures[i];
                currentCamera.backgroundColor = clearColor[i];
                currentCamera.clearFlags = CameraClearFlags.SolidColor;

                // Clear the background of the render texture
                Graphics.SetRenderTarget(targetTextures[i]);
                GL.Clear(false, true, clearColor[i]);

                // Apply material and render
                Graphics.Blit(null, targetTextures[i], materials[i]);
            }
        }
        else
        {
            currentCamera.transform.localPosition = Vector3.zero;
            currentCamera.transform.localRotation = Quaternion.identity;
            currentCamera.nearClipPlane = nearClipPlane;
            currentCamera.farClipPlane = farClipPlane;
            currentCamera.fieldOfView = fieldOfView;

            currentCamera.depthTextureMode = DepthTextureMode.Depth;

            materials[1].SetTexture("_OccluderTex", temp);

            // Iterate through materials and set target textures
            for (int i = 0; i < materials.Length; i++)
            {
                currentCamera.targetTexture = targetTextures[i];
                currentCamera.backgroundColor = clearColor[i];
                //currentCamera.clearFlags = CameraClearFlags.SolidColor;

                 // Clear the background of the render texture
                Graphics.SetRenderTarget(targetTextures[i]);
                GL.Clear(false, true, clearColor[i]);

                // Apply material and render
                Graphics.Blit(null, targetTextures[i], materials[i]);
            }
        }
    }
#endif

    public void RenderImage(RenderTexture source, RenderTexture destination)
    {
        VPI();
        InitRenderTextures(source);

        currentCamera.backgroundColor = clearColor[1];
        // Check if both textures are valid
        if (source != null && temp != null)
        {
            // Copy the contents of the source texture to the destination texture
            Graphics.CopyTexture(source, temp);
        }

        materials[1].SetTexture("_OccluderTex", temp);

        // Iterate through materials and apply them
        for (int i = 0; i < materials.Length; i++)
        {
            //currentCamera.targetTexture = targetTextures[i];
            //currentCamera.backgroundColor = clearColor[i];

            // Clear the background of the render texture
            Graphics.SetRenderTarget(targetTextures[i]);
            GL.Clear(false, true, clearColor[i]);

            Graphics.Blit(source, targetTextures[i], materials[i]);
        }
    }

    private void InitRenderTextures(RenderTexture source)
    {
       // Check if render texture and motion vector texture need to be created or resized
        if (temp == null || temp.width != source.width || temp.height != source.height)
        {
            // Release and recreate render texture
            if (temp != null)
                temp.Release();

            temp = new RenderTexture(source.width, source.height, 0, source.format, RenderTextureReadWrite.Linear);
            temp.enableRandomWrite = true;
            temp.Create();
        }
    }
}

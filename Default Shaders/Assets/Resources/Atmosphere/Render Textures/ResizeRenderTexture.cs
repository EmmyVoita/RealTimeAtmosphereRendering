using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class ResizeRenderTexture : MonoBehaviour
{
    // Reference to the RenderTexture
    public RenderTexture[] renderTexture;

    
    #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            for (int i = 0; i < renderTexture.Length; i++)
            {
                if (SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera == Camera.current)
                {
                    // Check if the screen dimensions have changed
                    if (SceneView.currentDrawingSceneView.position.width != renderTexture[i].width || SceneView.currentDrawingSceneView.position.height != renderTexture[i].height)
                    {
                        // Update the RenderTexture size
                        renderTexture[i].Release();
                        renderTexture[i].width = (int) SceneView.currentDrawingSceneView.position.width;
                        renderTexture[i].height = (int) SceneView.currentDrawingSceneView.position.height;
                        renderTexture[i].Create();
                    }
                }
                else
                {
                    
                    // Check if the screen dimensions have changed
                    if (Screen.width != renderTexture[i].width || Screen.height != renderTexture[i].height)
                    {
                        // Update the RenderTexture size
                        renderTexture[i].Release();
                        renderTexture[i].width = Screen.width;
                        renderTexture[i].height = Screen.height;
                        renderTexture[i].Create();
                    }

                }
            }
            
        }
    #endif
}

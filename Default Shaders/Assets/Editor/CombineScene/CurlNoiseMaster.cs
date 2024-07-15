using UnityEngine;
using UnityEditor;
using System.IO;

[ExecuteInEditMode]
public class CurlNoiseMaster : MonoBehaviour
{
    public RenderTexture noiseTex;
    public int width = 8;
    public int height = 8;
    public Vector2 r_noise_scale = new Vector2(1,1);
    public Vector2 g_noise_scale = new Vector2(1,1);
    public Vector2 b_noise_scale = new Vector2(1,1);
    public ComputeShader noiseCompute;
    public string savePath = "Assets/CurlNoise/GeneratedTexture.png"; // Set your desired save path here


    bool needsUpdate = true;

    void Update()
    {
        if (noiseTex == null || !noiseTex.IsCreated() || noiseTex.width != width || noiseTex.height != height)
        {
            if (noiseTex != null)
                noiseTex.Release();

            noiseTex = new RenderTexture(width, height, 0);
            noiseTex.enableRandomWrite = true;
            noiseTex.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            noiseTex.Create();
        }

        if (needsUpdate)
        {
            needsUpdate = false;

            noiseCompute.SetInt("width", width);
            noiseCompute.SetInt("height",  height);
            noiseCompute.SetVector("r_noise_scale", r_noise_scale);
            noiseCompute.SetVector("g_noise_scale", g_noise_scale);
            noiseCompute.SetVector("b_noise_scale", b_noise_scale);
            noiseCompute.SetTexture(0, "Result", noiseTex);

            int groupSize = 8;
            int numGroupsX = Mathf.CeilToInt(width / (float)groupSize);
            int numGroupsY = Mathf.CeilToInt(height / (float)groupSize);
            noiseCompute.Dispatch(0, numGroupsX, numGroupsY, 1);

            SaveTexture();
        }

        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", noiseTex);
    }

    void OnValidate()
    {
        needsUpdate = true;
    }

    void SaveTexture()
    {
        Texture2D texture = new Texture2D(noiseTex.width, noiseTex.height, TextureFormat.RGBA32, false);
        RenderTexture.active = noiseTex;
        texture.ReadPixels(new Rect(0, 0, noiseTex.width, noiseTex.height), 0, 0);
        texture.Apply();
        byte[] bytes = texture.EncodeToPNG();
        DestroyImmediate(texture);

        File.WriteAllBytes(savePath, bytes);
        AssetDatabase.Refresh();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;
using static UnityEngine.GraphicsBuffer;

[ExecuteInEditMode]
public class DeepShadowMapGen : MonoBehaviour
{
    public Camera mainCamera;
    public Material ShadowMapMaterial;
    public Light DirectionalLight;

    public bool viewerEnabled;
    public bool updateInEditor;

    private CommandBuffer BeforeForwardOpaque;
    private CommandBuffer AfterForwardOpaque;
  

    private ComputeBuffer NumberBuffer;
    private ComputeBuffer DepthBuffer;

    [Header("Debug Settings:")]
    public bool logInitalization;
    public bool logCleanup;

    [Header("Reset DeepShadowMap:")]
    public ComputeShader ResetCompute;
    public bool logComputeTime_ResetCompute;

    [Header("Kernel:")]
    [SerializeField]
    private int KernelResetNumberBuffer = -1;
    [SerializeField]
    private int KernelResetDepthBuffer = -1;
    [SerializeField]
    private int KernelScreenSpaceDeepShadowmap = -1;
    [SerializeField]
    private int KernelGaussianBlurShadow = -1;

    private RenderTexture _DepthTex;
    private RenderTexture _ShadowTex;
    private RenderTexture _BlurTex;


    public ComputeShader ResolveCompute;
    public Material DepthMaterial;


    const int dimension = 1024;
    const int elements = 8;

    // Start is called before the first frame update

    public bool _AsDefaultShadowmap = false;

    [SerializeField]
    private bool isInitialized = false;

    public void SetIsInitialized(bool value)
    {
        isInitialized = value;
    }

    public void Awake()
    {
        isInitialized = false;
        KernelResetNumberBuffer = -1;
        KernelResetDepthBuffer = -1;
        KernelScreenSpaceDeepShadowmap = -1;
        KernelGaussianBlurShadow = -1;


        if (!isInitialized)
        {
            // Set global shader properties:
            Shader.SetGlobalBuffer("NumberBuffer", NumberBuffer);
            Shader.SetGlobalBuffer("DepthBuffer", DepthBuffer);
            Shader.SetGlobalInt("Dimension", dimension);

            isInitialized = true;
            InitializeDeepShadowMap();
        }
    }



    private void InitializeDeepShadowMap()
    {

        int numElement = dimension * dimension * elements;

        string initializeMessage = "DeepShadowMapGen.Initialize() Time: " + DateTime.Now + " \n <color=cyan>--></color> Created Following CommandBuffers: ";

        // add command buffer for handling before and after opaque
        if (BeforeForwardOpaque == null)
        {
            BeforeForwardOpaque = new CommandBuffer();
            mainCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, BeforeForwardOpaque);
            initializeMessage += "<b><color=magenta>BeforeForwardOpaque</color></b>, ";
        }
        if (AfterForwardOpaque == null)
        {
            AfterForwardOpaque = new CommandBuffer();
            mainCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, AfterForwardOpaque);
            initializeMessage += "<b><color=magenta>AfterForwardOpaque</color></b> ";
        }

        initializeMessage += "\n <color=cyan>--></color> Created Following ComputeBuffers:  ";


        // Init ComputeBuffers:
        if (NumberBuffer == null || !NumberBuffer.IsValid())
        {
            NumberBuffer = new ComputeBuffer(dimension * dimension, sizeof(uint));
            initializeMessage += "<b><color=magenta>NumberBuffer</color></b>, ";
        }
        else
        {
            NumberBuffer.Release(); // Release existing buffer
            NumberBuffer = new ComputeBuffer(dimension * dimension, sizeof(uint)); // Create a new buffer
        }

        if (DepthBuffer == null || !DepthBuffer.IsValid())
        {
            DepthBuffer = new ComputeBuffer(numElement, sizeof(float) * 2);
            initializeMessage += "<b><color=magenta>DepthBuffer</color></b> ";
        }
        else
        {
            DepthBuffer.Release(); // Release existing buffer
            DepthBuffer = new ComputeBuffer(numElement, sizeof(float) * 2); // Create a new buffer
        }


        // Begin Frame?
        InitRenderTextures();

        if (KernelResetNumberBuffer == -1)
        {
            KernelResetNumberBuffer = ResetCompute.FindKernel("KernelResetNumberBuffer");
            initializeMessage += "\n <color=cyan>--></color>ResetCompute.FindKernel(\"KernelResetNumberBuffer\"): " + KernelResetNumberBuffer;
        }
        if (KernelResetDepthBuffer == -1)
        {
            KernelResetDepthBuffer = ResetCompute.FindKernel("KernelResetDepthBuffer");
            initializeMessage += "\n <color=cyan>--></color>ResetCompute.FindKernel(\"KernelResetDepthBuffer\"): " + KernelResetDepthBuffer;
        }

        DispatchResetCompute(ref initializeMessage);


        if (KernelScreenSpaceDeepShadowmap == -1)
        {
            KernelScreenSpaceDeepShadowmap = ResolveCompute.FindKernel("KernelScreenSpaceDeepShadowmap");
            initializeMessage += "\n <color=cyan>--></color>ResolveCompute.FindKernel(\"KernelScreenSpaceDeepShadowmap\"): " + KernelScreenSpaceDeepShadowmap;
        }
        if (KernelGaussianBlurShadow == -1)
        {
            KernelGaussianBlurShadow = ResolveCompute.FindKernel("KernelGaussianBlurShadow");
            initializeMessage += "\n <color=cyan>--></color>ResolveCompute.FindKernel(\"KernelGaussianBlurShadow\"): " + KernelGaussianBlurShadow;
        }


        ResolveCompute.SetInt("Dimension", dimension);
        ResolveCompute.SetBuffer(KernelScreenSpaceDeepShadowmap, "NumberBuffer", NumberBuffer);
        ResolveCompute.SetBuffer(KernelScreenSpaceDeepShadowmap, "DepthBuffer", DepthBuffer);
        ResolveCompute.SetTexture(KernelScreenSpaceDeepShadowmap, "_DepthTex", _DepthTex);
        ResolveCompute.SetTexture(KernelScreenSpaceDeepShadowmap, "_ShadowTex", _ShadowTex);

        
        initializeMessage += "\n <color=cyan>--></color> isInitialized: " + isInitialized;

        if (logInitalization)Debug.Log(initializeMessage);
    }

    private void DispatchResetCompute(ref string initializeMessage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();

        if (NumberBuffer == null) Debug.Log("NUMBER BUFFER NULL");
        if (DepthBuffer == null) Debug.Log("DepthBuffer NULL");

        ResetCompute.SetInt("Dimension", dimension);
        ResetCompute.SetBuffer(KernelResetNumberBuffer, "NumberBuffer", NumberBuffer);
        ResetCompute.SetBuffer(KernelResetDepthBuffer, "DepthBuffer", DepthBuffer);

        // Dispatch reset compute kernel:
        ResetCompute.Dispatch(KernelResetNumberBuffer, dimension / 8, dimension / 8, 1);

        if (logComputeTime_ResetCompute)
        {
            // Get minmax data just to force main thread to wait until compute shaders are finished.
            // This allows us to measure the execution time.
            initializeMessage += "\n <color=cyan>--></color>" + $" DispatchResetCompute() -> Time taken was: {timer.ElapsedMilliseconds}ms";
        }
    }

    private void InitRenderTextures()
    {

        if (_DepthTex == null || _DepthTex.width != Screen.width || _DepthTex.height != Screen.height)
        {
            // Release and recreate render texture
            if (_DepthTex != null)
                _DepthTex.Release();

            _DepthTex = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
            _DepthTex.Create();
        }

        if (_ShadowTex == null || _ShadowTex.width != Screen.width || _ShadowTex.height != Screen.height)
        {
            // Release and recreate render texture
            if (_ShadowTex != null)
                _ShadowTex.Release();

            _ShadowTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _ShadowTex.enableRandomWrite = true;
            _ShadowTex.Create();
        }

        if (_BlurTex == null || _BlurTex.width != Screen.width || _BlurTex.height != Screen.height)
        {
            // Release and recreate render texture
            if (_BlurTex != null)
                _BlurTex.Release();

            _BlurTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _BlurTex.enableRandomWrite = true;
            _BlurTex.Create();
        }

    }



    public void UpdateMap()
    {
        if (!isInitialized)
        {
            Debug.LogError("DeepShadowMapGen.UpdateMap() -> DeepShadowMapGen is not initialized!");

            InitializeDeepShadowMap();

            isInitialized = true;
        }
           

        BeforeForwardOpaque.Clear();
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        BeforeForwardOpaque.BeginSample("DepthOnly");
        BeforeForwardOpaque.SetViewMatrix(mainCamera.worldToCameraMatrix);
        BeforeForwardOpaque.SetProjectionMatrix(mainCamera.projectionMatrix);
        BeforeForwardOpaque.SetRenderTarget(_DepthTex);
        BeforeForwardOpaque.ClearRenderTarget(true, true, Color.black);


        for (int i = 0, imax = renderers.Length; i < imax; i++)
        {
            Renderer rend = renderers[i];
            if (rend.shadowCastingMode != ShadowCastingMode.Off)
            {
                //casterAABBs.Add(rend.bounds);
                if (BoundsUtils.IntersectFrustum(rend.bounds, rend.localToWorldMatrix, Camera.main.cullingMatrix))
                {
                    for (int m = 0, mmax = rend.sharedMaterials.Length; m < mmax; m++)
                    {
                        var mat = rend.sharedMaterial; // "sharedMaterials" cases bugs;
                        int pass = mat.FindPass("DepthOnly");
                        if (pass < 0)
                        {
                            BeforeForwardOpaque.DrawRenderer(rend, DepthMaterial, m, 0);
                        }
                        else
                        {
                            BeforeForwardOpaque.DrawRenderer(rend, mat, m, pass);
                        }
                        mat.SetShaderPassEnabled("DepthOnly", false);
                    }
                }
            }
        }

        BeforeForwardOpaque.EndSample("DepthOnly");
        BeforeForwardOpaque.SetRenderTarget(BuiltinRenderTextureType.None);
        BeforeForwardOpaque.ClearRenderTarget(true, true, Color.white);
        Matrix4x4 lightMatrix = DirectionalLight.transform.worldToLocalMatrix;

        //if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore 
        //    || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
        {
            Vector4 forward = lightMatrix.GetRow(2);
            lightMatrix.SetRow(2, -forward);
        }
        BeforeForwardOpaque.SetViewMatrix(lightMatrix);
        Matrix4x4 projMatrix = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, 0.1f, 10);
        BeforeForwardOpaque.SetProjectionMatrix(projMatrix);
        BeforeForwardOpaque.SetViewport(new Rect(0, 0, dimension, dimension));

        /*if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
        {
            Matrix4x4 mAdj = Matrix4x4.identity;
            mAdj.m22 = -0.5f;
            mAdj.m23 = 0.5f;
            projMatrix = mAdj * projMatrix;
        }*/
        BeforeForwardOpaque.SetGlobalMatrix("_LightVP", projMatrix * lightMatrix);
        //BeforeForwardOpaque.SetGlobalFloat("_HairAlpha", HairAlpha); //---------------------------------------------------------------------------------------------------

        BeforeForwardOpaque.BeginSample("ShadowMapMaterial");
        for (int i = 0, imax = renderers.Length; i < imax; i++)
        {
            Renderer rend = renderers[i];
            if (rend.shadowCastingMode != ShadowCastingMode.Off)
            {
                //casterAABBs.Add(rend.bounds);
                if (BoundsUtils.IntersectFrustum(rend.bounds, rend.localToWorldMatrix, Camera.main.cullingMatrix))
                {
                    for (int m = 0, mmax = rend.sharedMaterials.Length; m < mmax; m++)
                    {
                        var mat = rend.sharedMaterial; // "sharedMaterials" cases bugs;
                        int pass = mat.FindPass("DeepShadowCaster");
                        if (pass < 0)
                        {
                            BeforeForwardOpaque.DrawRenderer(rend, ShadowMapMaterial, m, 0);
                        }
                        else
                        {
                            BeforeForwardOpaque.DrawRenderer(rend, mat, m, pass);
                        }
                        mat.SetShaderPassEnabled("DeepShadowCaster", false);
                    }
                }
            }
        }
        BeforeForwardOpaque.ClearRenderTarget(true, true, Color.black);
        BeforeForwardOpaque.EndSample("ShadowMapMaterial");

        BeforeForwardOpaque.SetRenderTarget(_ShadowTex);
        BeforeForwardOpaque.ClearRenderTarget(true, true, Color.white);
        BeforeForwardOpaque.SetComputeIntParam(ResolveCompute, "_ScreenWidth", Screen.width);
        BeforeForwardOpaque.SetComputeIntParam(ResolveCompute, "_ScreenHeight", Screen.height);
        BeforeForwardOpaque.SetComputeMatrixParam(ResolveCompute, "_CameraInvVP", mainCamera.cullingMatrix.inverse);
        BeforeForwardOpaque.SetComputeMatrixParam(ResolveCompute, "_LightVP", projMatrix * lightMatrix);
        BeforeForwardOpaque.SetComputeIntParam(ResolveCompute, "_AsDefaultShadowmap", _AsDefaultShadowmap ? 1 : 0);
        BeforeForwardOpaque.DispatchCompute(ResolveCompute, KernelScreenSpaceDeepShadowmap, (7 + Screen.width) / 8, (7 + Screen.height) / 8, 1);


        BeforeForwardOpaque.SetRenderTarget(_BlurTex);
        BeforeForwardOpaque.ClearRenderTarget(true, true, Color.white);
        BeforeForwardOpaque.SetComputeIntParam(ResolveCompute, "_BlurStep", 1);
        BeforeForwardOpaque.SetComputeTextureParam(ResolveCompute, KernelGaussianBlurShadow, "_SourceShadowTexture", _ShadowTex);
        BeforeForwardOpaque.SetComputeTextureParam(ResolveCompute, KernelGaussianBlurShadow, "_BlurShadowTexture", _BlurTex);
        BeforeForwardOpaque.DispatchCompute(ResolveCompute, KernelGaussianBlurShadow, (7 + Screen.width) / 8, (7 + Screen.height) / 8, 1);

        //BeforeForwardOpaque.SetRenderTarget(_ShadowTex);
        //BeforeForwardOpaque.ClearRenderTarget(true, true, Color.white);
        //BeforeForwardOpaque.SetComputeIntParam(ResolveCompute, "_BlurStep", 2);
        //BeforeForwardOpaque.SetComputeTextureParam(ResolveCompute, KernelGaussianBlurShadow, "_SourceShadowTexture", _BlurTex);
        //BeforeForwardOpaque.SetComputeTextureParam(ResolveCompute, KernelGaussianBlurShadow, "_BlurShadowTexture", _ShadowTex);
        //BeforeForwardOpaque.DispatchCompute(ResolveCompute, KernelGaussianBlurShadow, (7 + Screen.width) / 8, (7 + Screen.height) / 8, 1);

        //BeforeForwardOpaque.SetRenderTarget(_BlurTex);
        //BeforeForwardOpaque.ClearRenderTarget(true, true, Color.white);
        //BeforeForwardOpaque.SetComputeIntParam(ResolveCompute, "_BlurStep", 4);
        //BeforeForwardOpaque.SetComputeTextureParam(ResolveCompute, KernelGaussianBlurShadow, "_SourceShadowTexture", _ShadowTex);
        //BeforeForwardOpaque.SetComputeTextureParam(ResolveCompute, KernelGaussianBlurShadow, "_BlurShadowTexture", _BlurTex);
        //BeforeForwardOpaque.DispatchCompute(ResolveCompute, KernelGaussianBlurShadow, (7 + Screen.width) / 8, (7 + Screen.height) / 8, 1);

        BeforeForwardOpaque.SetGlobalTexture("_BlurShadowTexture", _BlurTex);

        BeforeForwardOpaque.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

        BeforeForwardOpaque.SetViewMatrix(mainCamera.worldToCameraMatrix);
        BeforeForwardOpaque.SetProjectionMatrix(mainCamera.projectionMatrix);
        BeforeForwardOpaque.SetGlobalVector("CameraPos", mainCamera.transform.position);
        BeforeForwardOpaque.SetGlobalVector("LightDir", DirectionalLight.transform.forward);

        //BeforeForwardOpaque.SetGlobalColor("_HairColor", HairColor); //---------------------------------------------------------------------------------------------------

        AfterForwardOpaque.Clear();
        AfterForwardOpaque.DispatchCompute(ResetCompute, KernelResetDepthBuffer, dimension / 8, dimension / 8, 1);
        AfterForwardOpaque.DispatchCompute(ResetCompute, KernelResetNumberBuffer, dimension / 8, dimension / 8, 1);










    }

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        if(NumberBuffer != null && NumberBuffer.IsValid()) compute.SetBuffer(kernelID, "NumberBuffer", NumberBuffer);
        if(DepthBuffer != null && DepthBuffer.IsValid()) compute.SetBuffer(kernelID, "DepthBuffer", DepthBuffer);
    }





   void OnDestroy()
    {
        
        string releaseMessage = "DeepShadowMapGen.Cleanup() Time: " + DateTime.Now + " \n <color=cyan>--></color> Released Following Buffers: ";

        if (NumberBuffer != null && NumberBuffer.IsValid())
        {
            NumberBuffer.Release();
            releaseMessage += "<b><color=magenta>NumberBuffer</color></b>, ";
            NumberBuffer = null; // Set to null after releasing
        }

        if (DepthBuffer != null && DepthBuffer.IsValid())
        {
            DepthBuffer.Release();
            releaseMessage += "<b><color=magenta>DepthBuffer</color></b> ";
            DepthBuffer = null; // Set to null after releasing
        }

        if (_DepthTex != null)
        {
            _DepthTex.Release(); // Release any GPU resources
        }

        if(_ShadowTex != null)
        {
            _ShadowTex.Release();
        }

        if(_BlurTex != null)
        {
            _BlurTex.Release();
        }

        KernelResetNumberBuffer = -1;
        KernelResetDepthBuffer = -1;
        KernelScreenSpaceDeepShadowmap = -1;
        KernelGaussianBlurShadow = -1;

        isInitialized = false;
        releaseMessage += "\n <color=cyan>--></color> isInitialized:" + isInitialized;
        
        if (logCleanup) Debug.Log(releaseMessage);

    }

}

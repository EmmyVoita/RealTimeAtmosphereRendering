using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class AtmosphereComputeMaster : MonoBehaviour 
{

    private RenderTexture _target;

    public RenderTexture _World_Position_Texture;

    public RenderTexture _Light_Depth_Texture;

    public RenderTexture MainShadowmapCopy;
    public GameObject MainLight;

    public Camera _main_light_camera;
    // ------------------------------------------------------------------------------------------ //
    private Camera _camera;

    private Vector2 screenParams;

    private RenderTexture mainTextureBuffer;

    public Texture2D blueNoise;

    public Light directional_light;


    const string headerDecoration = " --- ";
    [Header (headerDecoration + "Main" + headerDecoration)]

    public ComputeShader compute_atmosphere;
    private int atmosphere_kernel_id;

    public Vector3 planet_center = new(0,0,0);
    public float atmosphere_radius = 10.0f;
    public float planet_radius = 2.0f;

    [Header (headerDecoration + "Ray March Settings" + headerDecoration)]

    [Range(1,256)]
    public int PRIMARY_STEP_COUNT = 32;
    [Range(1,128)]
    public int LIGHT_STEP_COUNT = 8;
    [Range(0.1f,100.0f)]
    public float ray_march_step_size = 5;

    public float ray_offset_strength = 10;
    [Range(0.0f,100.0f)]
    public float density_multiplier = 1;

    public Vector3 cuttoff_threshold = new Vector3(10,10,10);



    // Internal
    [HideInInspector]
    public Material material;

    private int frameCounter = 1;







    private Vector4 paramProjectionExtents;
    private Matrix4x4 paramCurrV;
    private Matrix4x4 paramCurrVP;
    private Matrix4x4 paramPrevVP;
    private Matrix4x4 paramPrevVP_NoFlip;

    private Matrix4x4 inverse_projection;

    private Matrix4x4 inverse_view;

    private Matrix4x4 light_current_VP;

    float previous_time = 0.0f;
    float current_time = 0.0f;


    


    private int data_length;
    public int total_data_size;
    ComputeBuffer test_buffer;

    void Reset()
    {
        _camera = GetComponent<Camera>();
    }

    void Clear()
    {

        previous_time = 0.0f;
    }

    private void Awake()
    {
        Reset();
        Clear();
    }

   
    private void Start()
    {
        Application.targetFrameRate = 400;
    }
  
    private void SetAtmopshereShaderParameters(RenderTexture source)
    {
        compute_atmosphere.SetInt ("frame_counter", frameCounter);
        compute_atmosphere.SetFloat ("ray_march_step_size", ray_march_step_size);
        compute_atmosphere.SetFloat ("density_multiplier", density_multiplier);
        compute_atmosphere.SetInt ("PRIMARY_STEP_COUNT", PRIMARY_STEP_COUNT );
        compute_atmosphere.SetInt ("LIGHT_STEP_COUNT", LIGHT_STEP_COUNT );



        compute_atmosphere.SetFloat("atmosphere_radius", atmosphere_radius);
        compute_atmosphere.SetFloat("ray_offset_strength", ray_offset_strength);
        compute_atmosphere.SetFloat("planet_radius", planet_radius);
        compute_atmosphere.SetVector("planet_center", planet_center);
        compute_atmosphere.SetFloat("light_intensity", directional_light.intensity);
        compute_atmosphere.SetVector("cuttoff_threshold", cuttoff_threshold);
        
        compute_atmosphere.SetMatrix("Light_VP", light_current_VP);
        

        compute_atmosphere.SetTexture(atmosphere_kernel_id, "Result", _target);
        compute_atmosphere.SetTexture(atmosphere_kernel_id, "BlueNoise", blueNoise);
        compute_atmosphere.SetTexture(atmosphere_kernel_id, "WorldPosition", _World_Position_Texture);
        //compute_atmosphere.SetTexture(atmosphere_kernel_id, "LightDepth",  _Light_Depth_Texture);

        //MainShadowmapCopy = MainLight.GetComponent<CopyLightPass>().m_ShadowmapCopy;
        //compute_atmosphere.SetTexture(atmosphere_kernel_id, "MainShadowmapCopy",  MainShadowmapCopy);

        PassMainTexture(source);
        PassCameraVariables();
        PassLightVariables();
        //UseComputeBuffer();

        // Set atmosphere compute shader parameters
        //compute_atmosphere.

    }

    private void PassMainTexture(RenderTexture source)
    {
        if (mainTextureBuffer == null || mainTextureBuffer.width != source.width || mainTextureBuffer.height != source.height)
        {
            // Release previous buffer if dimensions have changed
            if(mainTextureBuffer != null)
                mainTextureBuffer.Release();

            // Create a new buffer with the same dimensions as the source texture
            mainTextureBuffer = new RenderTexture(source.width, source.height, 0);
            mainTextureBuffer.enableRandomWrite = true;
            //mainTextureBuffer.Create();
        }

        Graphics.Blit(source, mainTextureBuffer);
        compute_atmosphere.SetTexture(atmosphere_kernel_id, "_MainTex", mainTextureBuffer);
    }

    private void PassCameraVariables()
    {
        
        compute_atmosphere.SetVector("_WorldSpaceCameraPos", _camera.transform.position);
        compute_atmosphere.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        compute_atmosphere.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Calculate screen parameters
        float screen_width = Screen.width;
        float screen_height = Screen.height;
        screenParams = new Vector2(screen_width, screen_height);
        // Pass screen parameters to compute shader
        compute_atmosphere.SetVector("_ScreenParams", screenParams);
    }

    private void PassLightVariables()
    {
        compute_atmosphere.SetVector("_WorldSpaceLightPos0", RenderSettings.sun.transform.position);
        compute_atmosphere.SetVector("_LightColor0;", RenderSettings.sun.color);
    }

    private void UseComputeBuffer()
    {
        int totalSize = sizeof(float) * PRIMARY_STEP_COUNT;
        //int totalSize = outputSize + vector3Size;

        //account for 4x4 blocks only 1/16 of frame
        data_length = (int)(screenParams.x * screenParams.y/16);
        total_data_size = data_length * totalSize;



        //test_buffer = new ComputeBuffer(data_length, totalSize);

        //compute_atmosphere.SetBuffer(atmosphere_kernel_id, "TestBuffer", test_buffer);
    }

    

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        atmosphere_kernel_id = compute_atmosphere.FindKernel("AtmosphereCompute");
      
        BeginFrame();

        SetAtmopshereShaderParameters(source);
        Render(destination);

        float[] resultData = new float[total_data_size];
        //test_buffer.GetData(resultData);

        EndFrame();
    }

    private void BeginFrame()
    {
        // Make sure we have a current render target
        InitRenderTextures();

        

        // Set the view and projection matricies for the current and previous frame
        Matrix4x4 currentV = _camera.worldToCameraMatrix;
        //Matrix4x4 currentP = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, true);
        Matrix4x4 currentP = _camera.projectionMatrix;


        Matrix4x4 light_current_V = _main_light_camera.worldToCameraMatrix;
        Matrix4x4 light_current_P = _main_light_camera.projectionMatrix;

        light_current_VP = light_current_P * light_current_V;


        paramCurrV = currentV;
        paramCurrVP = currentP * paramCurrV;


        current_time = Time.time;

        //-----------------------------------------------------------------------------------------------------------------
        // Command Buffer for getting the light depth
        //----------------------------------------------------------------------------------------------------------------- 
       

    }

    public void EndFrame()
    {

        // Increment the counter each frame
        frameCounter++;

        // Reset the counter back to 1 if it exceeds 16
        if (frameCounter > 16)
        {
            // After the frame set initMotionVector to true
            frameCounter = 1; 
        }

        previous_time = current_time;

        if (test_buffer != null)
        {
            test_buffer.Release();
        }
    }



    private void Render(RenderTexture destination)
    {

        // ------------------------------------------------------------------------------------------ //
        //  Atmosphere Pass (contains motion vector calculations)
        // ------------------------------------------------------------------------------------------ //

        // Set the thread group dimensions
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        // Set the target and dispatch the compute shader
        compute_atmosphere.Dispatch(atmosphere_kernel_id, threadGroupsX, threadGroupsY, 1);

        // ------------------------------------------------------------------------------------------ //
        //  Display
        // ------------------------------------------------------------------------------------------ //

        // Blit the result texture to the screen 
        Graphics.Blit(_target, destination);
    }


    private void OnDisable()
    {
        if (mainTextureBuffer != null)
        {
            mainTextureBuffer.Release();
            mainTextureBuffer = null;
        }
    }

    private void OnDestroy()
    {
        // Release and destroy the ComputeBuffer
        if (test_buffer != null)
        {
            test_buffer.Release();
            //test_buffer.Dispose();
        }
    }

    private void InitRenderTextures()
    {
       // Check if render texture and motion vector texture need to be created or resized
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release and recreate render texture
            if (_target != null)
                _target.Release();

            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }

    }
}
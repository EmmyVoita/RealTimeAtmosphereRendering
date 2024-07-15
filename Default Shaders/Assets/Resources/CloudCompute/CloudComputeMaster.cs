using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudComputeMaster : MonoBehaviour 
{

    private RenderTexture _target;
    private RenderTexture motionVectorTexture;

    private RenderTexture worldPositionTexture;

    private RenderTexture history_buffer;

 


    // ------------------------------------------------------------------------------------------ //
    private Camera _camera;

    private Vector2 screenParams;

    private RenderTexture mainTextureBuffer;




    const string headerDecoration = " --- ";
    [Header (headerDecoration + "Main" + headerDecoration)]

    public ComputeShader compute_cloud;
    public ComputeShader compute_reprojection;
    public ComputeShader compute_history_buffer;

    private int compute_cloud_kernel_id;
    private int compute_reprojection_kernel_id;
    private int compute_history_kernel_id;

    // ------------------------------------------------------------------------------------------ //

    public Transform container;
    public Vector3 cloudTestParams;

    

    [Header ("March settings" + headerDecoration)]

    [Range(1,20)]

    public int maxLightRaySamples = 8;

    public int maxViewRaySamples = 128;

    [Range(0.5f,50.0f)]
    public float ray_march_step_size = 11.0f;
    public float rayOffsetStrength;
    public Texture2D blueNoise;

    [Space(10)]
    [Header (headerDecoration + "Cloud Coverage Texture Settings" + headerDecoration)]
    [Space(10)]

    public Texture2D cloud_coverage_texture;
    public float cloud_coverage_texture_offset;
    [Range (0, 1)]
    public float cloud_coverage_texture_step = 0.1f;
    public Vector2 cloud_coverage_texture_tiling;

    [Space(10)]
    [Header (headerDecoration + "Cloud Coverage Height and Density Settings" + headerDecoration)]
    [Space(10)]

    public float altitude_gradient_power_1 = 1;
    public float altitude_gradient_power_2 = 1;

    [Range (0, 10)]
    public float low_altitude_multiplier_influence = 0.1f;

    public Texture height_gradient;

    public Texture density_gradient;
    public float density_gradient_scalar = 1.0f;
    
    [Space(10)]
    [Header (headerDecoration + "Base Shape" + headerDecoration)]
    [Space(10)]
    public float cloudScale = 1;
    public float densityMultiplier = 1;
    public float densityOffset;
    public Vector3 shapeOffset;
    public Vector2 heightOffset;
    public Vector4 shapeNoiseWeights;

    [Space(10)]
    [Header (headerDecoration + "Detail" + headerDecoration)]
    [Space(10)]

    public Texture2D curl_noise_texture;
    public float detailNoiseScale = 10;
    public float detailNoiseWeight = .1f;
    public Vector3 detailNoiseWeights;
    public Vector3 curl_noise_weights;
    public Vector3 detailOffset;
    
    [Space(10)]
    [Header (headerDecoration + "Lighting" + headerDecoration)]
    [Space(10)]

    [Range (0.0f,15.0f)]
    public float powder_factor = 0.5f;
    public float lightAbsorptionThroughCloud = 1;
    public float lightAbsorptionTowardSun = 1;
    [Range (0, 1)]
    public float darknessThreshold = .2f;
    [Range (0, 1)]
    public float forwardScattering = .83f;
    [Range (0, 1)]
    public float backScattering = .3f;
    [Range (0, 1)]
    public float baseBrightness = .8f;
    [Range (0, 1)]
    public float phaseFactor = .15f;

    [Space(10)]
    [Header (headerDecoration + "Animation" + headerDecoration)]
    [Space(10)]
    public float timeScale = 1;
    public float baseSpeed = 1;
    public float detailSpeed = 2;

    [Space(10)]
    [Header (headerDecoration + "Sky Ambient Color" + headerDecoration)]
    [Space(10)]
    public Color colA;
    public Color colB;

    [Range(0,1)]
    public float extinction_factor = 1.0f;

    [Space(10)]
    [Header (headerDecoration + "Temporal Reprojection Settings" + headerDecoration)]
    [Space(10)]

    [Range(0,1)]
    [Tooltip("Slider for dependence on temporal vs spatial coherence. 0 is full spatial coherence. 1 is full temporal coherence.")]
    public float temporal_vs_spatial = 0.8f;

    public float sample_standard_deviation = 3.0f;
    public float sample_distribution_max_offset = 8.0f;
    public float mv_magnitude_scalar = 5.0f;

    [Range(1,10)]
    public int neighborhood_tile_size = 3;

    // Internal
    [HideInInspector]
    public Material material;

    private int frameCounter = 1;




    private bool paramInitialized;

    private bool initMotionVector;
    private Vector4 paramProjectionExtents;
    private Matrix4x4 paramCurrV;
    private Matrix4x4 paramCurrVP;
    private Matrix4x4 paramPrevVP;
    private Matrix4x4 paramPrevVP_NoFlip;

    private Matrix4x4 inverse_projection;

    private Matrix4x4 inverse_view;



    float previous_time = 0.0f;
    float current_time = 0.0f;
    
   


    private bool print_data = false;

    // ------------------------------------------------------------------------------------------ //

    private List<RenderTexture> history_frame_list;  
    private ComputeBuffer history_frame_buffer;

    // ------------------------------------------------------------------------------------------ //
    void Reset()
    {
        _camera = GetComponent<Camera>();
        history_frame_list = new List<RenderTexture>();
    }

    void Clear()
    {
        paramInitialized = false;
        initMotionVector = false;
        previous_time = 0.0f;
        print_data = false;
    }

    private void Awake()
    {
        Reset();
        Clear();
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) 
        {
            print_data = true;
        }
    }

   
    private void Start()
    {
        Application.targetFrameRate = 60;
    }
  
    private void SetShaderParameters()
    {

 
        compute_cloud.SetInt("neighborhood_tile_size", neighborhood_tile_size);
        compute_cloud.SetFloat("previous_time", previous_time);
        compute_cloud.SetFloat("current_time", current_time);


        compute_cloud.SetBool("initMotionVector", initMotionVector);
        // Pass near and far plane;
        compute_cloud.SetFloat("nearPlane", _camera.nearClipPlane);
        compute_cloud.SetFloat("farPlane", _camera.farClipPlane);

        //Pass matricies for reprojection
        compute_cloud.SetMatrix("_CurrV", paramCurrV);
        compute_cloud.SetMatrix("_CurrVP", paramCurrVP);
        compute_cloud.SetMatrix("_PrevVP", paramPrevVP);
        compute_cloud.SetMatrix("_PrevVP_NoFlip", paramPrevVP_NoFlip);


        // Pass the frame counter for rendering 4x4 pixel blocks over 16 frames
        compute_cloud.SetInt ("frameCounter", frameCounter);

        // Pass the depth texture data from the main camera 
        compute_cloud.SetTextureFromGlobal(compute_cloud_kernel_id, "_DepthTexture", "_CameraDepthTexture");

        // Pass the cloud container dimensions
        Vector3 size = container.localScale;
        int width = Mathf.CeilToInt (size.x);
        int height = Mathf.CeilToInt (size.y);
        int depth = Mathf.CeilToInt (size.z);
        compute_cloud.SetVector ("mapSize", new Vector4 (width, height, depth, 0));
        compute_cloud.SetVector ("boundsMin", container.position - container.localScale / 2);
        compute_cloud.SetVector ("boundsMax", container.position + container.localScale / 2);
        
        // Pass noise textures 
        var noise = FindObjectOfType<NoiseGenerator> ();
        noise.UpdateNoise();
        compute_cloud.SetTexture (compute_cloud_kernel_id, "NoiseTex", noise.shapeTexture);
        compute_cloud.SetTexture (compute_cloud_kernel_id, "DetailNoiseTex", noise.detailTexture);
        compute_cloud.SetTexture (compute_cloud_kernel_id, "BlueNoise", blueNoise);
        compute_cloud.SetTexture (compute_cloud_kernel_id, "CloudCoverage", cloud_coverage_texture);
        compute_cloud.SetTexture (compute_cloud_kernel_id, "HeightGradient", height_gradient);
        compute_cloud.SetTexture (compute_cloud_kernel_id, "DensityGradient", density_gradient);
        compute_cloud.SetTexture (compute_cloud_kernel_id, "CurlNoiseTex", curl_noise_texture);


        // Pass the cloud coverage settings
        compute_cloud.SetFloat  ("cloud_coverage_texture_offset", cloud_coverage_texture_offset);
        compute_cloud.SetFloat  ("cloud_coverage_texture_step", cloud_coverage_texture_step); 
        compute_cloud.SetVector ("coverage_tiling", cloud_coverage_texture_tiling);
        compute_cloud.SetFloat  ("altitude_gradient_power_1", altitude_gradient_power_1);
        compute_cloud.SetFloat  ("altitude_gradient_power_2", altitude_gradient_power_2);
        compute_cloud.SetFloat  ("low_altitude_multiplier_influence", low_altitude_multiplier_influence);
        compute_cloud.SetFloat  ("scale", cloudScale);
        compute_cloud.SetFloat  ("densityMultiplier", densityMultiplier);
        compute_cloud.SetFloat  ("densityOffset", densityOffset);
        compute_cloud.SetFloat  ("detailNoiseScale", detailNoiseScale);
        compute_cloud.SetFloat  ("detailNoiseWeight", detailNoiseWeight);
        compute_cloud.SetVector ("shapeOffset", shapeOffset);
        compute_cloud.SetVector ("detailOffset", detailOffset);
        compute_cloud.SetVector ("detailWeights", detailNoiseWeights);
        compute_cloud.SetVector ("curl_noise_weights", curl_noise_weights);
        compute_cloud.SetVector ("shapeNoiseWeights", shapeNoiseWeights);
        compute_cloud.SetFloat  ("density_gradient_scalar", density_gradient_scalar); 
        
        
        // Pass the march settings
        maxLightRaySamples = Mathf.Max (1, maxLightRaySamples);
        compute_cloud.SetInt    ("maxLightRaySamples", maxLightRaySamples);
        compute_cloud.SetFloat  ("ray_march_step_size", ray_march_step_size);
        compute_cloud.SetInt    ("maxViewRaySamples", maxViewRaySamples);
        compute_cloud.SetFloat  ("rayOffsetStrength", rayOffsetStrength);
     

        // Cloud lighting settings
        compute_cloud.SetFloat  ("darknessThreshold", darknessThreshold);
        compute_cloud.SetVector ("params", cloudTestParams);
        compute_cloud.SetFloat  ("powder_factor", powder_factor);
        compute_cloud.SetFloat  ("lightAbsorptionThroughCloud", lightAbsorptionThroughCloud);
        compute_cloud.SetFloat  ("lightAbsorptionTowardSun", lightAbsorptionTowardSun);
        compute_cloud.SetVector ("IsotropicLightTop", colA);
        compute_cloud.SetVector ("IsotropicLightBottom", colB);
        compute_cloud.SetFloat  ("extinction_factor", extinction_factor);
        compute_cloud.SetVector ("phaseParams", new Vector4 (forwardScattering, backScattering, baseBrightness, phaseFactor));


        // Pass Animation settings
        compute_cloud.SetFloat ("timeScale", (Application.isPlaying) ? timeScale : 0);
        compute_cloud.SetFloat ("baseSpeed", baseSpeed);
        compute_cloud.SetFloat ("detailSpeed", detailSpeed);


        // Pass the weathermap data
        var weatherMapGen = FindObjectOfType<WeatherMap> ();
        if (!Application.isPlaying) {
            weatherMapGen.UpdateMap ();
        }
        //material.SetTexture ("WeatherMap", weatherMapGen.weatherMap);
    }

    private void PassMainTextureToComputeShader(RenderTexture source)
    {
        if (mainTextureBuffer == null || mainTextureBuffer.width != source.width || mainTextureBuffer.height != source.height)
        {
            // Release previous buffer if dimensions have changed
            if (mainTextureBuffer != null)
                mainTextureBuffer.Release();

            // Create a new buffer with the same dimensions as the source texture
            mainTextureBuffer = new RenderTexture(source.width, source.height, 0);
            mainTextureBuffer.enableRandomWrite = true;
            mainTextureBuffer.Create();
        }

        Graphics.Blit(source, mainTextureBuffer);
        compute_cloud.SetTexture(compute_cloud_kernel_id, "_MainTex", mainTextureBuffer);
    }

    private void PassCameraVariablesToComputeShader()
    {
        
        compute_cloud.SetVector("_WorldSpaceCameraPos", _camera.transform.position);
        compute_cloud.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        compute_cloud.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Calculate screen parameters
        float screen_width = Screen.width;
        float screen_height = Screen.height;
        screenParams = new Vector2(screen_width, screen_height);
        // Pass screen parameters to compute shader
        compute_cloud.SetVector("_ScreenParams", screenParams);
    }

    private void PassLightVariablesToComputeShader()
    {
        compute_cloud.SetVector("_WorldSpaceLightPos0", RenderSettings.sun.transform.position);
        compute_cloud.SetVector("_LightColor0;", RenderSettings.sun.color);
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        compute_cloud_kernel_id = compute_cloud.FindKernel("cloud_ray_march");
        compute_history_kernel_id = compute_history_buffer.FindKernel("compute_history");
        compute_reprojection_kernel_id = compute_reprojection.FindKernel("temporal_reprojection");

        BeginFrame();

        SetShaderParameters();
        PassMainTextureToComputeShader(source);
        PassCameraVariablesToComputeShader();
        PassLightVariablesToComputeShader();
        Render(destination);

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
        Matrix4x4 currentP_NoFlip = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
        Matrix4x4 previousV = paramInitialized ? paramCurrV : currentV;
        inverse_projection = _camera.projectionMatrix.inverse;
        inverse_view = _camera.worldToCameraMatrix.inverse;
        
        paramInitialized = true;

        paramCurrV = currentV;
        paramCurrVP = currentP * paramCurrV;
        paramPrevVP = currentP * previousV;
        paramPrevVP_NoFlip = currentP_NoFlip * previousV;


        current_time = Time.time;
    }

    public void EndFrame()
    {
        // Increment the counter each frame
        frameCounter++;

        // Reset the counter back to 1 if it exceeds 16
        if (frameCounter > 16)
        {
            // After the frame set initMotionVector to true
            initMotionVector = true;
            frameCounter = 1; 
            //motionVectorTexture.Release();
            
        }

        previous_time = current_time;
        //motionVectorTexture.Release();
        
    }


    private void PassHistoryBuffer()
    {
        // ------------------------------------------------------------------------------------------ //
        // Handle passing history buffer to compute shader
        // ------------------------------------------------------------------------------------------ //


        int buffer_size = history_frame_list.Count;
        int data_size = 0;

        compute_cloud.SetInt("buffer_size", buffer_size);
        
        if(buffer_size > 0)
        {
            // Calculate the total data size based on the number of pixels in each RenderTexture
            foreach (RenderTexture render_texture in history_frame_list)
            {
                int pixels_count = render_texture.width * render_texture.height;
                data_size += pixels_count;
            }
            // Create the ComputeBuffer with the size of the RenderTexture list
            history_frame_buffer = new ComputeBuffer(data_size, sizeof(float) * 4);

            // Initialize the buffer with RenderTexture data
            int offset = 0;

            // Initialize the buffer with RenderTexture data
            for (int i = 0; i < buffer_size; i++)
            {
                RenderTexture render_texture = history_frame_list[i];

                // Get the Texture2D representation of the RenderTexture
                Texture2D texture2D = ConvertToTexture2D(render_texture);

                // Get the pixel data of the Texture2D
                Color[] pixels = texture2D.GetPixels();

                // Convert Color array to float4 array
                Vector4[] float4Data = new Vector4[pixels.Length];

                for (int j = 0; j < pixels.Length; j++)
                {
                    float4Data[j] = new Vector4(pixels[j].r, pixels[j].g, pixels[j].b, pixels[j].a);
                }

                // Store the float4 data in the ComputeBuffer
                history_frame_buffer.SetData(float4Data, 0, offset, pixels.Length);
                offset += pixels.Length;
            }

            // Set the buffer data on the compute shader
            compute_cloud.SetBuffer(compute_cloud_kernel_id, "history_frame_buffer", history_frame_buffer);
        }

        // ------------------------------------------------------------------------------------------ //
    }

    private void UpdateHistoryBuffer()
    {
        // ------------------------------------------------------------------------------------------ //
        // Handle updating history buffer
        // ------------------------------------------------------------------------------------------ //

        // Create a new rendertexture to hold the output of the current frame and insert that into the top of the history buffer
        RenderTexture downscaled_rt = new RenderTexture(Screen.width/16, Screen.height/16, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        downscaled_rt.enableRandomWrite = true;
        downscaled_rt.Create();

        // Set the filter modes
        _target.filterMode = FilterMode.Point;
        downscaled_rt.filterMode = FilterMode.Point;

        Graphics.Blit(_target, downscaled_rt);
        history_frame_list.Insert(0, downscaled_rt);

        // Release the oldest render texture in the history buffer
        if (history_frame_list.Count > 15)
        {
            RenderTexture oldRt = history_frame_list[15];
            history_frame_list.RemoveAt(15);
            oldRt.Release(); // Release the resources of the removed RenderTexture
        }

        // ------------------------------------------------------------------------------------------ //

        if(history_frame_buffer != null)
            history_frame_buffer.Release();
    }



    private void Render(RenderTexture destination)
    {

        // ------------------------------------------------------------------------------------------ //
        //  Cloud Pass (contains motion vector calculations)
        // ------------------------------------------------------------------------------------------ //

    
        // Set textures
        compute_cloud.SetTexture(compute_cloud_kernel_id, "motionVectorTexture", motionVectorTexture);
        compute_cloud.SetTexture(compute_cloud_kernel_id, "worldPositionTexture", worldPositionTexture);
        compute_cloud.SetTexture(compute_cloud_kernel_id, "Result", _target);
        
        // Set the thread group dimensions
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        // Set the target and dispatch the compute shader
        compute_cloud.Dispatch(compute_cloud_kernel_id, threadGroupsX, threadGroupsY, 1);

        // ------------------------------------------------------------------------------------------ //
        // History Buffer 
        // Summary: Store the previous 15 frames in a single texture and clear old data
        // ------------------------------------------------------------------------------------------ //

        // Pass data to the history compute shader
        compute_history_buffer.SetTexture(compute_history_kernel_id, "motionVectorTexture", motionVectorTexture);
        compute_history_buffer.SetTexture(compute_history_kernel_id, "input", _target);
        compute_history_buffer.SetTexture(compute_history_kernel_id, "history_buffer",  history_buffer);
        compute_history_buffer.SetInt ("frame_counter", frameCounter);
       
        // Set the target and dispatch the compute shader
        compute_history_buffer.Dispatch(compute_history_kernel_id, threadGroupsX, threadGroupsY, 1);

        // ------------------------------------------------------------------------------------------ //
        // Temporal Reprojection Pass
        // Summary: Reproject the history buffer onto the current frame
        // ------------------------------------------------------------------------------------------ //

        // Compute Buffer for debugging
        ComputeBuffer resultBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(float) * 2);
        compute_reprojection.SetBuffer(compute_reprojection_kernel_id, "result_buffer", resultBuffer);

        compute_reprojection.SetMatrix("inverse_view_matrix", inverse_view);
        compute_reprojection.SetMatrix("inverse_projection_matrix", inverse_projection);
        compute_reprojection.SetMatrix("_CurrVP", paramCurrVP);

        compute_reprojection.SetFloat  ("temporal_weight", temporal_vs_spatial);
        compute_reprojection.SetFloat  ("spatial_weight", 1-temporal_vs_spatial);
        compute_reprojection.SetFloat("sample_standard_deviation", sample_standard_deviation);
        compute_reprojection.SetFloat("sample_distribution_max_offset", sample_distribution_max_offset);
        compute_reprojection.SetFloat("mv_magnitude_scalar", mv_magnitude_scalar);
        
        compute_reprojection.SetInt  ("neighborhood_tile_size", neighborhood_tile_size);
        
        compute_reprojection.SetTexture(compute_reprojection_kernel_id, "motion_vector_ss", motionVectorTexture);
        compute_reprojection.SetTexture(compute_reprojection_kernel_id, "world_position", worldPositionTexture);
        compute_reprojection.SetTexture(compute_reprojection_kernel_id, "history_buffer",  history_buffer);
        compute_reprojection.SetTexture(compute_reprojection_kernel_id, "Result", _target);
        compute_reprojection.SetInt ("frame_counter", frameCounter);

        // Set the target and dispatch the compute shader
        compute_reprojection.Dispatch(compute_reprojection_kernel_id, threadGroupsX, threadGroupsY, 1);

        // ------------------------------------------------------------------------------------------ //
        //  Display
        // ------------------------------------------------------------------------------------------ //

        Graphics.Blit(_target, destination);
        // Blit the result texture to the screen 
        //Graphics.Blit(_target, destination);

        Vector2[] resultData = new Vector2[Screen.width * Screen.height];
        resultBuffer.GetData(resultData);

        if(print_data)
        {
            print_data = false;
            Debug.Log("Printing Data:");
            for (int y = 0; y < Screen.height; y++) 
            {
                for (int x = 0; x < Screen.width; x++) 
                {
                    int index = x + y * Screen.width;
                    if(resultData[index].magnitude != 0.0)
                    {
                        Debug.Log("Pixel value at (" + x + ", " + y + "): " + resultData[index]);
                    }
                }
            }
        }

        resultBuffer.Release();


    }

    private Texture2D ConvertToTexture2D(RenderTexture renderTexture)
    {
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);

        // Read the RenderTexture data into the Texture2D
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();

        return texture2D;
    }

    private void OnDisable()
    {
        if (mainTextureBuffer != null)
        {
            mainTextureBuffer.Release();
           // DestroyImmediate(mainTextureBuffer);
            mainTextureBuffer = null;
        }
    }

    private void OnDestroy()
    {
        // Release and destroy the ComputeBuffer
        /*if(history_frame_buffer != null)
            history_frame_buffer.Release();
        history_frame_buffer.Dispose();*/
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

        if (motionVectorTexture == null || motionVectorTexture.width != Screen.width || motionVectorTexture.height != Screen.height)
        {
            // Release and recreate motion vector texture
            if (motionVectorTexture != null)
                motionVectorTexture.Release();

            motionVectorTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            motionVectorTexture.enableRandomWrite = true;
            motionVectorTexture.Create();
        }

        if (worldPositionTexture == null || worldPositionTexture.width != Screen.width || worldPositionTexture.height != Screen.height)
        {
            // Release and recreate motion vector texture
            if (worldPositionTexture != null)
                worldPositionTexture.Release();

            worldPositionTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            worldPositionTexture.enableRandomWrite = true;
            worldPositionTexture.Create();
        }

        
        if ( history_buffer == null ||  history_buffer.width != Screen.width ||  history_buffer.height != Screen.height)
        {
            // Release and recreate motion vector texture
            if ( history_buffer != null)
                 history_buffer.Release();

             history_buffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
             history_buffer.enableRandomWrite = true;
             history_buffer.Create();
        }
        
    }
}
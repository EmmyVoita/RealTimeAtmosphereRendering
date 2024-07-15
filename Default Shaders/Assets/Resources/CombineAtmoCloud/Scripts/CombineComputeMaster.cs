using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtmosphereRendering
{
    [ExecuteInEditMode, ImageEffectAllowedInSceneView]
    public class CombineComputeMaster : MonoBehaviour 
    {
        const string headerDecorationStart = " [ ";
        const string headerDecorationEnd = " ] ";
        private const uint MAX_FRAME_COUNT = uint.MaxValue;
        
        // Render Textures:
        public RenderTexture worldPositionTexture;
        public RenderTexture occluderTexture;
        public Material combineOccluder_material;
        private RenderTexture mainTextureBuffer;
        [SerializeField] private RenderTexture _historyBuffer;
        [SerializeField] private RenderTexture _subFrameBuffer;
        [SerializeField] private RenderTexture _currentBuffer;
        [SerializeField] private RenderTexture  _backBuffer;
        [SerializeField] private RenderTexture  _backBuffer2;

        public VisualizeShader visualizeShader;



        // Directional Light Ref:
        public Light directionalLightObject;
        public Camera directionalLightCamera;
        private Matrix4x4 light_current_VP;

        // Generator References
        private NoiseGenerator noise;
        public DeepShadowMapGen deepShadowMapGen;


        // Comnpute Shader
        public ComputeShader combineCompute;
        public ComputeShader postProcessing;
        private int combineKernelID;
        private int postProcessingKernelID;

        // Cloud Container
        public Transform container;

        [Header (headerDecorationStart + "PerformanceSettings" + headerDecorationEnd)]
        public PerformanceSettings performanceSettings;

        [Header (headerDecorationStart + "RayMarchSettings" + headerDecorationEnd)]
        public RayMarchSettings rayMarchSettings;

        [Header(headerDecorationStart + "CloudCoverageSettings" + headerDecorationEnd)]
        public CloudCoverageSettings cloudCoverageSettings;

        [Header(headerDecorationStart + "LightingSettings" + headerDecorationEnd)]
        public LightingSettings lightingSettings;

        [Header(headerDecorationStart + "BaseShapeSettings" + headerDecorationEnd)]
        public ShapeSettings shapeSettings;

        [Header(headerDecorationStart + "Animation" + headerDecorationEnd)]
        public AnimationSettings animationSettings;

        [Header(headerDecorationStart + "AtmosphereSettings" + headerDecorationEnd)]
        public AtmosphereSettings atmosphereSettings;
        
        [Header("Atmosphere Container:")]
        public Transform atmo_container;


        public SharedProperties _cloudsSharedProperties;


        // Internal
        [HideInInspector]
        public Material material;

        private Camera _camera;
        private Vector2 screenParams;


        private bool paramInitialized;

        private Vector4 paramProjectionExtents;
        private Matrix4x4 paramCurrV;
        private Matrix4x4 paramCurrVP;
        private Matrix4x4 paramPrevVP;
        private Matrix4x4 paramPrevVP_NoFlip;

        private Matrix4x4 inverse_projection;

        private Matrix4x4 inverse_view;

        
        private Matrix4x4 previousFrameVP;
        private Matrix4x4 currentFrameVP;



        float previous_time = 0.0f;
        float current_time = 0.0f;

        private bool historyBufferInitalized = false;


        void OnValidate()
		{
			if( _cloudsSharedProperties == null)
			{
				_cloudsSharedProperties = new SharedProperties();
			}
        }

        // ------------------------------------------------------------------------------------------ //
        void Reset()
        {
            historyBufferInitalized = false;
        
            // Get reference to the main camera
            _camera = this.gameObject.GetComponent<Camera>();
            _camera.depthTextureMode = DepthTextureMode.Depth;

            // Get reference to the noiseGenerator
            noise = FindObjectOfType<NoiseGenerator>();

            _cloudsSharedProperties.OnStart();

        }

        void Clear()
        {
            paramInitialized = false;
            previous_time = 0.0f;

        }

        private void Awake()
        {

            Reset();

            // Get reference to the deepShadowMapGen and initialize it.
            //deepShadowMapGen = FindObjectOfType<DeepShadowMapGen>();
            //if (deepShadowMapGen == null) Debug.LogError("Error: DeepShadowMapGen not found. Line: Awake() \n Time:" + Time.time + " FrameCount: " + performanceSettings.GetFrameCounter());

        

            Clear();
        }





        private void Start()
        {
            Application.targetFrameRate = 200;
        }
    
        private void SetShaderParameters()
        {
            // Do some synchronous work that doesn't require noise or shadow map data
            // --------------------------------------------------------------------- //

            //Set PerformanceSettings:
            //performanceSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

            _cloudsSharedProperties.ApplyFameSettingsToCompute(combineCompute, ref combineKernelID);

            // Set RayMarchSettings:
            rayMarchSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

            // Set LightingSettings:
            lightingSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

            // Set CloudCoverageSettings:
            cloudCoverageSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

            // Set AnimationSettings:
            animationSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);
            
            // Set AtmosphereSettings:
            atmosphereSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

            // Wait for tasks to complete 
            // --------------------------------------------------------------------- //

            //await Task.WhenAll(noiseGenerationTask);


            // Do some synchronous work that after getting noise and shadow map data
            // --------------------------------------------------------------------- //

            //deepShadowMapGen.SetShaderProperties(ref combineCompute, ref combineKernelID);

            // Set BaseShapeSettings:
            shapeSettings.SetShaderProperties(ref combineCompute, ref combineKernelID, ref noise);


           


            combineCompute.SetFloat("previous_time", previous_time);
            combineCompute.SetFloat("current_time", current_time);


            // Pass near and far plane;
            combineCompute.SetFloat("nearPlane", _camera.nearClipPlane);
            combineCompute.SetFloat("farPlane", _camera.farClipPlane);

            //Pass matricies for reprojection
            combineCompute.SetMatrix("_CurrV", paramCurrV);
            combineCompute.SetMatrix("_CurrVP", paramCurrVP);
            combineCompute.SetMatrix("_PrevVP", paramPrevVP);
            combineCompute.SetMatrix("_PrevVP_NoFlip", paramPrevVP_NoFlip);


        

            // Pass the depth texture data from the main camera 
            combineCompute.SetTextureFromGlobal(combineKernelID, "_DepthTexture", "_CameraDepthTexture");

            // Pass the cloud container dimensions
            Vector3 size = container.localScale;
            int width = Mathf.CeilToInt (size.x);
            int height = Mathf.CeilToInt (size.y);
            int depth = Mathf.CeilToInt (size.z);
            combineCompute.SetVector ("mapSize", new Vector4 (width, height, depth, 0));
            combineCompute.SetVector ("boundsMin", container.position - container.localScale / 2);
            combineCompute.SetVector ("boundsMax", container.position + container.localScale / 2);

            // Pass the atmo container dimensions
            Vector3 size_a = atmo_container.localScale;
            int width_a = Mathf.CeilToInt (size_a.x);
            int height_a = Mathf.CeilToInt (size_a.y);
            int depth_a = Mathf.CeilToInt (size_a.z);
            combineCompute.SetVector ("mapSize_Atmo", new Vector4 (width_a, height_a, depth_a, 0));
            combineCompute.SetVector ("boundsMin_Atmo", atmo_container.position - atmo_container.localScale / 2);
            combineCompute.SetVector ("boundsMax_Atmo", atmo_container.position + atmo_container.localScale / 2);
            
            if(atmosphereSettings.matchEarthRatio)
            {
                atmosphereSettings.planet_radius = (height_a + atmosphereSettings.atmosphereRadiusOffset)/100000.0f * 6371000.0f;
            }

            combineCompute.SetVector("_DirLightDirection", directionalLightObject.transform.forward);

            combineCompute.SetMatrix("Light_VP", light_current_VP);
            combineCompute.SetVector("_WorldSpaceLightPos0", RenderSettings.sun.transform.position);
            combineCompute.SetVector("lightColorMain", directionalLightObject.color);
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
            combineCompute.SetTexture(combineKernelID, "_MainTex", mainTextureBuffer);
        }

        private void PassCameraVariablesToComputeShader()
        {
            
            
            // Calculate screen parameters
            float screen_width = Screen.width;
            float screen_height = Screen.height;
            screenParams = new Vector2(screen_width, screen_height);

            // Pass screen parameters to compute shader
            combineCompute.SetVector("_ScreenParams", screenParams);
        }



        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            

            combineKernelID = combineCompute.FindKernel("AtmosphereRayMarch");
            postProcessingKernelID = postProcessing.FindKernel("PostProcessAtmo");

            
            visualizeShader.RenderImage(source,destination);
            BeginFrame();



            SetShaderParameters();
            PassMainTextureToComputeShader(source);

            PassCameraVariablesToComputeShader();

            SetDebugParams();

            Render(destination);

            //EndFrame();
        }

        private void BeginFrame()
        {
            // Update Noise Gnerator using Asyncornous Task: 
            //noiseGenerationTask = noise.UpdateNoiseAsync();
            noise.UpdateNoise();


            _cloudsSharedProperties.subPixelSize = (int) Mathf.Sqrt(performanceSettings.GetFrameInterval());
            _cloudsSharedProperties.BeginFrame(_camera);

            if ( _subFrameBuffer == null || _historyBuffer == null || _currentBuffer == null || _backBuffer == null ||
			    _cloudsSharedProperties.dimensionsChangedSinceLastFrame)
			{
				InitRenderTextures();
			}

            // Update DeepShadowMap Generator using Syncronous Update. Can't do async, have to do calculations on main thread:
            //deepShadowMapGen.UpdateMap();





            // Make sure we have a current render target
            //InitRenderTextures();

            current_time = Time.time;
        
        }

        public void EndFrame()
        {
            _cloudsSharedProperties.EndFrame();
            
        
            previous_time = current_time;
        }





        private void Render(RenderTexture destination)
        {
            //RenderTexture previousActiveRenderTexture = RenderTexture.active;
            // ------------------------------------------------------------------------------------------ //
            //  Cloud Pass 
            // ------------------------------------------------------------------------------------------ //

            combineCompute.SetVector("_Resolution", new Vector2(1920,1080));
            combineCompute.SetVector("_FrameJitter", _cloudsSharedProperties.frameJitter);

            _cloudsSharedProperties.ApplyToCompute(combineCompute);

            // Set textures
            combineCompute.SetTexture(combineKernelID, "worldPositionTexture", worldPositionTexture);
            combineCompute.SetTexture(combineKernelID, "Result", _subFrameBuffer);
            
            // Set the thread group dimensions
            int threadGroupsX = Mathf.CeilToInt(_cloudsSharedProperties.subFrameWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_cloudsSharedProperties.subFrameHeight / 8.0f);

            // Set the target and dispatch the compute shader
            combineCompute.Dispatch(combineKernelID, threadGroupsX, threadGroupsY, 1);



            // ------------------------------------------------------------------------------------------ //
            //  Upsample Pass
            // ------------------------------------------------------------------------------------------ //

            // Create a Material that uses the shader
            Material upsampleMaterial = new Material(Shader.Find("Custom/UnlitUpSample"));

            // Set the texture you're upsampling
            upsampleMaterial.SetTexture("_MainTex", _subFrameBuffer);

            // Render a full-screen quad with the Material
            Graphics.Blit(null, _backBuffer, upsampleMaterial);

            if(_historyBuffer && !historyBufferInitalized)
            {
                Graphics.Blit( _backBuffer, _historyBuffer);
                historyBufferInitalized = true;
            } 

            


            // ------------------------------------------------------------------------------------------ //

            // Create a Material that uses the shader
            Material reprojectionMaterial = new Material(Shader.Find("Custom/TemporalReprojection"));

            // Set the texture you're upsampling
            reprojectionMaterial.SetTexture("_SubFrame",  _backBuffer);
            reprojectionMaterial.SetTexture("_PrevFrame", _historyBuffer);
            reprojectionMaterial.SetVector("_Resolution", new Vector2(1920,1080));
            reprojectionMaterial.SetVector("_FrameJitter", _cloudsSharedProperties.frameJitter);

            reprojectionMaterial.SetMatrix("_1CameraToWorld", _camera.cameraToWorldMatrix);
            reprojectionMaterial.SetMatrix("_CameraInverseProjection", _cloudsSharedProperties.jitterProjectionMatrix.inverse);

            reprojectionMaterial.SetFloat("nearPlane", _camera.nearClipPlane);
            reprojectionMaterial.SetFloat("farPlane", _camera.farClipPlane);

            _cloudsSharedProperties.ApplyToMaterial(reprojectionMaterial);

            // Render a full-screen quad with the Material
            Graphics.Blit(null, _backBuffer2, reprojectionMaterial);
            //Graphics.Blit(_backBuffer2, _currentBuffer);

            


            //-------------------------------------------------------------------------------------------//

            
            // Create a Material that uses the shader
            Material postProcessMaterial = new Material(Shader.Find("Custom/UnlitPostProcessCloud"));

            // Set the texture you're upsampling
            postProcessMaterial.SetTexture("_MainTex",  _backBuffer2);
            postProcessMaterial.SetTexture("_PrevFrameColor", _historyBuffer);
            postProcessMaterial.SetVector("_Resolution", new Vector2(1920,1080));
            postProcessMaterial.SetVector("_FrameJitter", _cloudsSharedProperties.frameJitter);

            postProcessMaterial.SetMatrix("_1CameraToWorld", _camera.cameraToWorldMatrix);
            postProcessMaterial.SetMatrix("_CameraInverseProjection", _cloudsSharedProperties.jitterProjectionMatrix.inverse);

            postProcessMaterial.SetFloat("nearPlane", _camera.nearClipPlane);
            postProcessMaterial.SetFloat("farPlane", _camera.farClipPlane);

            _cloudsSharedProperties.ApplyToMaterial(postProcessMaterial);

            // Render a full-screen quad with the Material
            Graphics.Blit(null, _currentBuffer, postProcessMaterial);

            // Swap the current frame and history buffer
            if(_historyBuffer != null && _currentBuffer != null)
            {
                Graphics.Blit(_currentBuffer, _historyBuffer);
            } 

            //-------------------------------------------------------------------------------------------//
            
            
            
            

            //-------------------------------------------------------------------------------------------//

            combineOccluder_material.SetTexture("_AtmoTex", _currentBuffer);
            combineOccluder_material.SetTexture("_OccluderTex", occluderTexture);
            Graphics.Blit(_currentBuffer, destination, combineOccluder_material);

            EndFrame();
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
                mainTextureBuffer = null;
            }
        }

        void OnDestroy()
        {
            if(_historyBuffer != null) _historyBuffer.Release();
            if( _subFrameBuffer != null) _subFrameBuffer.Release();
            if(_currentBuffer != null) _currentBuffer.Release();
            if(_backBuffer != null) _backBuffer.Release();
            if(_backBuffer2 != null) _backBuffer2.Release();
        }

    

        private void InitRenderTextures()
        {

            
            // Check if render texture and motion vector texture need to be created or resized
            if (_subFrameBuffer == null || _subFrameBuffer.width != _cloudsSharedProperties.subFrameWidth || _subFrameBuffer.height != _cloudsSharedProperties.subFrameHeight)
            {
                // Release and recreate render texture
                if (_subFrameBuffer != null)
                    _subFrameBuffer.Release();

                _subFrameBuffer = new RenderTexture(_cloudsSharedProperties.subFrameWidth, _cloudsSharedProperties.subFrameHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _subFrameBuffer.enableRandomWrite = true;
                _subFrameBuffer.filterMode = FilterMode.Bilinear;
                _subFrameBuffer.Create();
            }

            // Check if render texture and motion vector texture need to be created or resized
            if (_historyBuffer == null || _historyBuffer.width != Screen.width || _historyBuffer.height != Screen.height)
            {
                // Release and recreate render texture
                if (_historyBuffer != null)
                    _historyBuffer.Release();

                _historyBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _historyBuffer.enableRandomWrite = true;
                _historyBuffer.filterMode = FilterMode.Bilinear;
                _historyBuffer.Create();
            }

            // Check if render texture and motion vector texture need to be created or resized
            if (_currentBuffer == null || _currentBuffer.width != Screen.width || _currentBuffer.height != Screen.height)
            {
                // Release and recreate render texture
                if (_currentBuffer != null)
                    _currentBuffer.Release();

                _currentBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _currentBuffer.enableRandomWrite = true;
                _currentBuffer.filterMode = FilterMode.Bilinear;
                _currentBuffer.Create();
            }

            // Check if render texture and motion vector texture need to be created or resized
            if (_backBuffer == null || _backBuffer.width != Screen.width || _backBuffer.height != Screen.height)
            {
                // Release and recreate render texture
                if (_backBuffer != null)
                    _backBuffer.Release();

                _backBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _backBuffer.enableRandomWrite = true;
                _backBuffer.filterMode = FilterMode.Bilinear;
                _backBuffer.Create();
            }

            // Check if render texture and motion vector texture need to be created or resized
            if (_backBuffer2 == null || _backBuffer2.width != Screen.width || _backBuffer2.height != Screen.height)
            {
                // Release and recreate render texture
                if (_backBuffer2 != null)
                    _backBuffer2.Release();

                _backBuffer2 = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _backBuffer2.enableRandomWrite = true;
                _backBuffer2.filterMode = FilterMode.Bilinear;
                _backBuffer2.Create();
            }
            

        }


        void SetDebugParams()
        {
            if (TextureViewerController.S != null)
            {
                TextureViewerController.S.SetShaderProperties(ref combineCompute);
            }
            else
            {
                Debug.LogWarning("TextureViewerController.S is null. Make sure it's properly initialized.");
            }
        
        }
    }
}


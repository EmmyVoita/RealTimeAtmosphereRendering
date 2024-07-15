using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CloudMaster : MonoBehaviour {
    const string headerDecoration = " --- ";
    [Header (headerDecoration + "Main" + headerDecoration)]
    public Shader shader;
    public Transform container;
    public Vector3 cloudTestParams;

    [Header ("March settings" + headerDecoration)]

    [Range(1,20)]
    public int numStepsLight = 8;

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

    // Internal
    [HideInInspector]
    public Material material;

    void Awake () 
    {
        Debug.Log("Hellow from awake");
        var weatherMapGen = FindObjectOfType<WeatherMap> ();
        if (Application.isPlaying) {
            weatherMapGen.UpdateMap ();
        }
    }

    private void Start()
    {
        Debug.Log("Hellow from start -------------------------------------------");
    }

    [ImageEffectOpaque]
    private void OnRenderImage (RenderTexture src, RenderTexture dest) {

        // Validate inputs
        if (material == null || material.shader != shader) {
            material = new Material (shader);
        }
        numStepsLight = Mathf.Max (1, numStepsLight);

        // Noise
        var noise = FindObjectOfType<NoiseGenerator> ();
        noise.UpdateNoise ();

        material.SetTexture ("NoiseTex", noise.shapeTexture);
        material.SetTexture ("DetailNoiseTex", noise.detailTexture);
        material.SetTexture ("BlueNoise", blueNoise);
        material.SetTexture ("CloudCoverage", cloud_coverage_texture);
        material.SetTexture ("HeightGradient", height_gradient);
        material.SetTexture ("DensityGradient", density_gradient);
        material.SetTexture ("CurlNoiseTex", curl_noise_texture);
        

        // Weathermap
        var weatherMapGen = FindObjectOfType<WeatherMap> ();
        if (!Application.isPlaying) {
            weatherMapGen.UpdateMap ();
        }
        material.SetTexture ("WeatherMap", weatherMapGen.weatherMap);

        Vector3 size = container.localScale;
        int width = Mathf.CeilToInt (size.x);
        int height = Mathf.CeilToInt (size.y);
        int depth = Mathf.CeilToInt (size.z);

        material.SetFloat ("scale", cloudScale);
        material.SetFloat ("densityMultiplier", densityMultiplier);
        material.SetFloat ("densityOffset", densityOffset);
        material.SetFloat ("powder_factor", powder_factor);
        material.SetFloat ("lightAbsorptionThroughCloud", lightAbsorptionThroughCloud);
        material.SetFloat ("lightAbsorptionTowardSun", lightAbsorptionTowardSun);
        material.SetFloat ("darknessThreshold", darknessThreshold);
        material.SetVector ("params", cloudTestParams);
        material.SetFloat ("rayOffsetStrength", rayOffsetStrength);

        material.SetFloat ("detailNoiseScale", detailNoiseScale);
        material.SetFloat ("detailNoiseWeight", detailNoiseWeight);
        material.SetVector ("shapeOffset", shapeOffset);
        material.SetVector ("detailOffset", detailOffset);
        material.SetVector ("detailWeights", detailNoiseWeights);
        material.SetVector ("curl_noise_weights", curl_noise_weights);
        material.SetVector ("shapeNoiseWeights", shapeNoiseWeights);
        material.SetVector ("phaseParams", new Vector4 (forwardScattering, backScattering, baseBrightness, phaseFactor));

        material.SetVector ("boundsMin", container.position - container.localScale / 2);
        material.SetVector ("boundsMax", container.position + container.localScale / 2);

        //Cloud Coverage
        material.SetFloat ("cloud_coverage_texture_offset", cloud_coverage_texture_offset);
        material.SetFloat ("cloud_coverage_texture_step", cloud_coverage_texture_step);
        
        material.SetVector("coverage_tiling", cloud_coverage_texture_tiling);
        material.SetFloat ("altitude_gradient_power_1", altitude_gradient_power_1);
        material.SetFloat ("altitude_gradient_power_2", altitude_gradient_power_2);
        material.SetFloat ("low_altitude_multiplier_influence", low_altitude_multiplier_influence);
        

        material.SetInt ("numStepsLight", numStepsLight);
        material.SetFloat("ray_march_step_size", ray_march_step_size);

        material.SetVector ("mapSize", new Vector4 (width, height, depth, 0));

        material.SetFloat ("timeScale", (Application.isPlaying) ? timeScale : 0);
        material.SetFloat ("baseSpeed", baseSpeed);
        material.SetFloat ("detailSpeed", detailSpeed);
        material.SetFloat("density_gradient_scalar", density_gradient_scalar);

        // Set debug params
        SetDebugParams ();

        material.SetColor ("IsotropicLightTop", colA);
        material.SetColor ("IsotropicLightBottom", colB);
        material.SetFloat("extinction_factor", extinction_factor);
        

        // Bit does the following:
        // - sets _MainTex property on material to the source texture
        // - sets the render target to the destination texture
        // - draws a full-screen quad
        // This copies the src texture to the dest texture, with whatever modifications the shader makes
        Graphics.Blit (src, dest, material);
    }

    void SetDebugParams () {

        var noise = FindObjectOfType<NoiseGenerator> ();
        var weatherMapGen = FindObjectOfType<WeatherMap> ();

        int debugModeIndex = 0;
        if (noise.viewerEnabled) {
            debugModeIndex = (noise.activeTextureType == NoiseGenerator.CloudNoiseType.Shape) ? 1 : 2;
        }
        if (weatherMapGen.viewerEnabled) {
            debugModeIndex = 3;
        }

        //material.SetInt ("debugViewMode", debugModeIndex);
        //material.SetFloat ("debugNoiseSliceDepth", noise.viewerSliceDepth);
        //material.SetFloat ("debugTileAmount", noise.viewerTileAmount);
        //material.SetFloat ("viewerSize", noise.viewerSize);
       // material.SetVector ("debugChannelWeight", noise.ChannelMask);
        //material.SetInt ("debugGreyscale", (noise.viewerGreyscale) ? 1 : 0);
       // material.SetInt ("debugShowAllChannels", (noise.viewerShowAllChannels) ? 1 : 0);
    }

}
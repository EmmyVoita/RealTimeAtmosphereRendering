using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/ShapeSettings")]
public class ShapeSettings : ScriptableObject
{
    [Header("Shape Settings: ")]
    [Tooltip("Scale factor applied to the shape noise used in cloud density calculations. Higher values increase the level of detail.")]
    public float shapeNoiseScale = 1;

    [Range(0, 1)]
    [Tooltip("A weight controlling the influence of shape noise on cloud density. Higher values give more prominence to detail noise.")]
    public float shapeNoiseInfluence = .6f;

    [Tooltip("Weights for individual components of the shape noise. Adjusting these values can fine-tune the appearance of the cloud shape.")]
    public Vector3 shapeNoiseComponentWeights;

    [Tooltip("Offset values applied to the shape noise sampling position. Adjusting these can shift the pattern of the shape noise.")]
    public Vector3 shapeNoiseOffset;



    [Header("Detail Noise: ")]
     [Range(0, 1)]
    public float CLOUDS_DETAIL_STRENGTH = 0.35f;
    public float CLOUDS_DENSITY = 0.03f;




    [Tooltip("A multiplier affecting the scale of the detail noise used in cloud density calculations. Higher values increase the level of detail.")]
    public float detailNoiseScale = 1.0f;

    //[Range(0, 1)]
    [Tooltip("A weight controlling the influence of detail noise on cloud density. Higher values give more prominence to detail noise.")]
    public float detailNoiseInfluence = .35f;

    

    [Tooltip("Adjusts the strength of the eroding effect on the edges of clouds caused by the detail noise. Increasing this value intensifies the erosion, while decreasing it reduces the effect. Fine-tune this scalar to achieve the desired visual appearance of cloud edges.")]
    public float detailErodeWeightScalar = 1.0f;

    //[Tooltip("The texture used for generating detail noise, which adds naturalistic variation to cloud formations.")]
    //public Texture2D detailNoiseTexture;

    [Tooltip("Weights for individual components of the detail noise. Adjusting these values can fine-tune the appearance of the detail noise.")]
    public Vector3 detailNoiseComponentWeights; 

    [Tooltip("Offset applied to the detail noise sampling position. Adjusting this value can shift the pattern of the detail noise.")]
    public Vector3 detailNoiseOffset;

    [Header("Detail Noise Test: ")]
    [Range(0, 1)]
    public float DETAIL_TEST_MAX = 0.3f;

    [Header("Curl Noise: ")]

    public float CURL_NOISE_SCALE = 0.5f;

    [Tooltip("The texture used for generating curl noise, which adds naturalistic motion to cloud formations.")]
    public Texture2D curlNoiseTexture;

    [Tooltip("Weights controlling the influence of curl noise components on the vector field. Adjusting these values can fine-tune the direction and intensity of motion in the x, y, and z axes.")]
    public Vector3 curlNoiseComponentWeights = new Vector3(1.0f, 1.0f, 1.0f);



    [Header("Shared Settings: ")]
    [Tooltip("Multiplier affecting the overall density of the clouds. Higher values result in denser cloud formations.")]
    [Range(0, 10)]
    public float densityMultiplier = 1;

    [Tooltip("Offset value applied to the cloud density. Adjusting this can fine-tune the density pattern.")]
    [Range(-1, 1)]
    public float shapeDensityOffset;

    [Tooltip("Offset value applied to the cloud density. Adjusting this can fine-tune the density pattern.")]
    [Range(-1, 1)]
    public float detailDensityOffset;

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID, ref NoiseGenerator noise)
    {
        // Shape
        //----------------------------------------------------------------------------------

        // Set Float:
        compute.SetFloat("baseNoiseScale", shapeNoiseScale);
        compute.SetFloat("shapeNoiseInfluence", shapeNoiseInfluence);
        compute.SetFloat("densityMultiplier", densityMultiplier);
        compute.SetFloat("shapeDensityOffset", shapeDensityOffset);
        

        // Set Vector:
        compute.SetVector("baseNoiseOffset", shapeNoiseOffset);
        compute.SetVector("shapeNoiseComponentWeights", shapeNoiseComponentWeights);

        //Set Texture:
        compute.SetTexture(kernelID, "BaseNoiseTex", noise.shapeTexture);
        compute.SetTexture(kernelID, "DetailNoiseTex", noise.detailTexture);

        // Detail
        //----------------------------------------------------------------------------------

        // Set Float:
        compute.SetFloat("detailNoiseScale", detailNoiseScale);
        compute.SetFloat("detailNoiseInfluence", detailNoiseInfluence);
        compute.SetFloat("detailErodeWeightScalar", detailErodeWeightScalar);
        compute.SetFloat("CLOUDS_DETAIL_STRENGTH", CLOUDS_DETAIL_STRENGTH);
        compute.SetFloat("CLOUDS_DENSITY", CLOUDS_DENSITY);
        compute.SetFloat("DETAIL_TEST_MAX", DETAIL_TEST_MAX);

        // Set Vector:
        compute.SetVector("detailNoiseOffset", detailNoiseOffset);
        compute.SetVector("detailNoiseComponentWeights", detailNoiseComponentWeights);
        compute.SetVector("curlNoiseComponentWeights", curlNoiseComponentWeights);

        // Set Texture:
        compute.SetTexture(kernelID, "CurlNoiseTex", curlNoiseTexture);
        
        // Curl
        //----------------------------------------------------------------------------------
        compute.SetFloat("CURL_NOISE_SCALE", CURL_NOISE_SCALE);
    }

    public void Reset()
    {
        shapeNoiseComponentWeights = new Vector4(0, 0.625f, 0.125f, 0.250f);
        detailNoiseComponentWeights = new Vector3(0.625f, 0.125f, 0.250f);
        shapeNoiseInfluence = .6f;
        detailNoiseInfluence = .35f;
    }
}

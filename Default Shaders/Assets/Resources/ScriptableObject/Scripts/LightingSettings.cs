using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/LightingSettings")]
public class LightingSettings : ScriptableObject
{
    [Header("Lighting Settings: ")]

    public float power = 200f;

    public float atmoPower = 1f;
    [Header("Beer-Powder: ")]
    [Range(0, 1)]
    public float BEER_POWDER_BLEND_FACTOR = 0.5f;
    public float BEER_POWDER_POWER = 1.0f;
    [Range(0, 1)]
    public float BEER_POWDER_SCALAR = 0.3f;

    [Tooltip("Multiplier affecting the scattering density. Higher values result in denser scattering effects.")]
    public float scatteringDensityMultiplier = 0.5f;

    [Tooltip("Multiplier affecting the absorption of light passing through the clouds.")]
    public float lightAbsorptionThroughClouds = 1;

    [Tooltip("Multiplier affecting the absorption of light towards the sun.")]
    public float lightAbsorptionTowardsSun = 1;

    [Header("Light Transmittance Blending: ")]
    [Tooltip("In this case, darknessThreshold is acting as the minimum threshold. If the original value is below this threshold, it will be increased to at least this value. Then, (1 - darknessThreshold) is acting as a blending factor that determines how much of the original value is retained.")]
    [Range(0, 1)]
    public float darknessThreshold = .2f;

    [Header("Phase Function Settings: ")]
    [Tooltip("Controls the forward scattering factor. Higher values result in stronger forward scattering.")]
    [Range(0, 1)]
    public float forwardScattering = 0.1f;

    [Tooltip("Controls the back scattering factor. Higher values result in stronger back scattering.")]
    [Range(0, 1)]
    public float backScattering = .3f;

    [Tooltip("Base brightness of the scattering. Higher values increase the overall brightness of the scattering.")]
    [Range(0, 1)]
    public float baseBrightness = 0.0f;

    [Tooltip("Multiplier affecting the phase function. Adjusting this can fine-tune the appearance of the scattering.")]
    [Range(0, 1)]
    public float phaseFunctionMultiplier = 1.0f;


    [Header("Scattering Properties: ")]
    public bool useColoredScattering;
    
    [Header("Cloud Scattering [Custom Cloud Scattering]: ")]
    public Vector3 cloudSigmaS = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 cloudSigmaE = new Vector3(0.5f, 0.5f, 0.5f);
    [Range(0, 1)]
    public float blendFactor = 0.5f;
    
    [Header("Atmsopheric Fog: ")]
    public float fogFactor = 1.0f;
    [Range(0,128)]
    public int EXIT_CLOUD_COUNT = 10;

    [Header("Cloud Luminance Settings: ")]
    [Range(0, 1)]
    public float CLOUD_AMBIENT_INFLUENCE = .5f;
    [Range(0, 1)]
    public float CLOUD_ATMOSPHERE_INFLUENCE = .125f;
     [Range(0, 1)]
    public float LIGHT_MARCH_INFLUENCE = 1.0f;

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        // Set Float:
        compute.SetFloat("power", power);
        compute.SetFloat("atmoPower", atmoPower);
        compute.SetFloat("scatteringDensityMultiplier", scatteringDensityMultiplier);
        compute.SetFloat("lightAbsorptionThroughCloud", lightAbsorptionThroughClouds);
        compute.SetFloat("lightAbsorptionTowardSun", lightAbsorptionTowardsSun);
        compute.SetFloat("darknessThreshold", darknessThreshold);
        compute.SetFloat("customCloudBlendFactor", blendFactor);
        compute.SetFloat("fogFactor", fogFactor);

        // Set Vector:
        compute.SetVector("phaseParams", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFunctionMultiplier));
        compute.SetVector("cloudSigmaS", cloudSigmaS);
        compute.SetVector("cloudSigmaE", cloudSigmaE);

        if (useColoredScattering) 
            compute.EnableKeyword("COLOR_SCATTERING");
        else 
            compute.DisableKeyword("COLOR_SCATTERING");

          compute.SetInt("EXIT_CLOUD_COUNT", EXIT_CLOUD_COUNT);
        
        compute.SetFloat("BEER_POWDER_BLEND_FACTOR", BEER_POWDER_BLEND_FACTOR);

        compute.SetFloat("CLOUD_AMBIENT_FACTOR", CLOUD_AMBIENT_INFLUENCE);
        compute.SetFloat("CLOUD_ATMOSPHERE_FACTOR", CLOUD_ATMOSPHERE_INFLUENCE);
        compute.SetFloat("LIGHT_MARCH_INFLUENCE", LIGHT_MARCH_INFLUENCE);
        compute.SetFloat("BEER_POWDER_POWER", BEER_POWDER_POWER);
        compute.SetFloat("BEER_POWDER_SCALAR", BEER_POWDER_SCALAR);

    }   

    public void Reset()
    {
        power = 200f;
        forwardScattering = 0.1f;
        backScattering = 0.3f;
        baseBrightness = 0.0f;
        phaseFunctionMultiplier = 1.0f;
    }
}

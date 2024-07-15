using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/RayMarchSettings")]
public class RayMarchSettings : ScriptableObject
{
    [Header("Ray March Step Size:")]
    [Tooltip("")]
    public int STEPS_LIGHT = 8;
    public int STEPS_PRIMARY = 32;

 

    [Header("Dithering Settings:")]
    public bool useDithering= true;
    public Texture2D blueNoise;
    public float rayOffsetStrength = 50f;
    
    [Header("Atmosphere Sampling:")]
    public int NUM_ATMO_SAMPLES = 8;

    [Header("Cloud Volume Sampling:")]
    public int NUM_CLOUD_SAMPLES = 32;

    [Header("Maximum Number of Samples:")]
    public int MAX_SAMPLE_COUNT = 256;

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        STEPS_LIGHT = Mathf.Max(1, STEPS_LIGHT);
        STEPS_PRIMARY = Mathf.Max(1, STEPS_PRIMARY);

        // Set Int:
        compute.SetInt("STEPS_LIGHT", STEPS_LIGHT);
        compute.SetInt("STEPS_PRIMARY", STEPS_PRIMARY);
        compute.SetInt("STEPS_PRIMARY_CLOUD", NUM_CLOUD_SAMPLES);
        compute.SetInt("NUM_ATMO_SAMPLES", NUM_ATMO_SAMPLES);
        compute.SetInt("MAX_SAMPLE_COUNT", MAX_SAMPLE_COUNT);

        // Set Float:
        compute.SetFloat("rayOffsetStrength", rayOffsetStrength);
        
        // Set Texture:
        compute.SetTexture(kernelID, "BlueNoise", blueNoise);

        if(useDithering) compute.EnableKeyword("DITHERING");
        else compute.DisableKeyword("DITHERING");
    }
}

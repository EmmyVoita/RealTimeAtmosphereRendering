using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/AtmosphereSettings")]
public class AtmosphereSettings : ScriptableObject
{
    [Header("World Position Texture:")]
    [Tooltip("")]
    public RenderTexture _World_Position_Texture;

    [Header("Planet Data:")]
    public Vector3 planet_center = new(0,0,0);
    public float planet_radius = 2.0f;

    public float atmoHeightOffset = 1000.0f;

    public float atmosphereRadiusOffset = -100.0f;

    public bool matchEarthRatio = true;

    [Header("Atmosphere Scattering:")]
    public float rayleighWeight = 1.0f;
    public float mieWeight = 1.0f;


    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        compute.SetTexture(kernelID, "WorldPosition", _World_Position_Texture);
        compute.SetFloat("planet_radius", planet_radius);
        compute.SetFloat("atmoHeightOffset", atmoHeightOffset);
        compute.SetFloat("atmosphereRadiusOffset", atmosphereRadiusOffset);
        compute.SetVector("planet_center", planet_center);

       compute.SetFloat("rayleighWeight", rayleighWeight);
       compute.SetFloat("mieWeight", mieWeight);
    }
}

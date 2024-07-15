using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/CloudCoverageSettings")]
public class CloudCoverageSettings : ScriptableObject
{


    [Header("Cloud Coverage Texture Settings: ")]
    public Texture2D cloudCoverageTexture;
    public Vector2 coverageOffset;
    public Vector2 coverageTiling;


    [Header("Cloud Coverage Texture Step: ")]
    public bool useTextureStep = false;
    [Range(0, 1)]
    public float coverageTextureStep = 0.1f;
    [Range(-1, 1)]
    public float coverageTextureOffset = 0.0f;



    [Header("Cloud Coverage: ")]
    [Range(0, 1)]
    public float CLOUDS_COVERAGE = 0.52f;
    [Range(0, 1)]
    public float topHeightOffset = 0.1f;
    [Range(0, 1)]
    public float bottomHeight = .25f;
    [Range(0, 1)]
    public float topHeight = .75f;



    public Texture height_gradient;

    public Texture density_gradient;
    public float density_gradient_scalar = 1.0f;

    [Header("Cloud Coverage Viewer: ")]
    public bool viewerEnabled;
    public PerspectiveMode perspectiveMode;
    [Tooltip("R.) Height gradient used to round top and bottom of clouds \n" +
             "G.) Cloud coverage texture sampled \n" +
             "B.) Shaper texture (perlinWorley remapped using worleyFBM) \n" +
             "A.) G channel remapped using B channel (Base density function output) ")]
    public TextureChannel activeChannel;

    [Header("Viewer Settings")]
    public bool viewerGreyscale = true;
    public bool viewerShowAllChannels;
    [Range(0, 1)]
    public float viewerSliceDepth;
    [Range(1, TextureViewerController.maxTileAmount)]
    public float viewerTileAmount = 1;
    [Range(0, 1)]
    public float viewerSize = 1;

    


    private void OnValidate()
    {
        if (TextureViewerController.S != null)
        {
            if (viewerEnabled)
            {
                TextureViewerController.S.EnableViewer();
                TextureViewerController.S.UpdateViewerSettings(CurrentViewer.CloudCoverage, perspectiveMode, activeChannel, viewerGreyscale,
                                                                 viewerShowAllChannels, viewerSliceDepth, viewerTileAmount, viewerSize);
            }
            else if(!viewerEnabled)
            {
                TextureViewerController.S.DisableViewer();
            }
        }
        else
        {
            Debug.LogWarning("TextureViewerController.S is null. Make sure it's properly initialized.");
        }
        
    }



    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        compute.SetBool("useCoverageTextureStep", useTextureStep);


        // Set Float:
        compute.SetFloat("CLOUDS_COVERAGE", CLOUDS_COVERAGE);
        compute.SetFloat("bottomHeight", bottomHeight);
        compute.SetFloat("topHeight", topHeight);
        compute.SetFloat("topHeightOffset", topHeightOffset);
        compute.SetFloat("coverageTextureDensityOffset", coverageTextureOffset);

        compute.SetFloat("coverageTextureStep", coverageTextureStep);
        compute.SetFloat("density_gradient_scalar", density_gradient_scalar);

        // Set Vector:
        compute.SetVector("coverageTiling", coverageTiling);
        compute.SetVector("coverageOffset", coverageOffset);
        // Set Texture:
        compute.SetTexture(kernelID, "CloudCoverage", cloudCoverageTexture);
        compute.SetTexture(kernelID, "HeightGradient", height_gradient);
        compute.SetTexture(kernelID, "DensityGradient", density_gradient);
    }
}


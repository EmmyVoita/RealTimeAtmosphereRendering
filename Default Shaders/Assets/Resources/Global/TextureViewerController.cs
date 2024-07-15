using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using AtmosphereRendering;

public enum TextureChannel { R, G, B, A }
public enum PerspectiveMode { Front, Side, TopDown }
public enum CurrentViewer { CloudCoverage, NoiseGeneration, DeepShadowMap }



public class TextureViewerController : MonoBehaviour
{
    //static public TextureViewerController S { get; private set; }
    public const int maxTileAmount = 10;

    static private TextureViewerController instance;
    
    static public TextureViewerController S
    {
        get
        {
            
            if (instance == null)
            {
                instance = FindObjectOfType<TextureViewerController>();
                if (instance == null)
                {
                    GameObject singletonObject = new GameObject();
                    instance = singletonObject.AddComponent<TextureViewerController>();
                    singletonObject.name = "TextureViewerController (Singleton)";
                }
            }
            return instance;
        }
    }

    //public bool useSingleton = true;

    [Header("Viewer Settings")]
    public bool viewerEnabled;
    public bool viewerGreyscale = true;
    public bool viewerShowAllChannels;
    [Range(0, 1)]
    public float viewerSliceDepth;
    [Range(1, maxTileAmount)]
    public float viewerTileAmount = 1;
    [Range(0, 1)]
    public float viewerSize = 1;
    public int debugModeIndex = 0;


    [Header("Editor Settings")]
    public TextureChannel activeChannel;
    public PerspectiveMode perspectiveMode;
    public CurrentViewer currentViewer;

    [Header("References")]
    public NoiseGenerator noiseGenerator;
    public DeepShadowMapGen deepShadowMapGen;
    public CloudCoverageSettings cloudCoverageSettings;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);

        Debug.Log("Singleton Set");

        if(noiseGenerator == null) noiseGenerator = FindObjectOfType<NoiseGenerator>();
        if(deepShadowMapGen == null) deepShadowMapGen = FindObjectOfType<DeepShadowMapGen>();
        if(cloudCoverageSettings == null) cloudCoverageSettings = FindObjectOfType<CombineComputeMaster>().cloudCoverageSettings;
    }

    public Vector4 ChannelMask
    {
        get
        {
            Vector4 channelWeight = new Vector4(
                (activeChannel == TextureChannel.R) ? 1 : 0,
                (activeChannel == TextureChannel.G) ? 1 : 0,
                (activeChannel == TextureChannel.B) ? 1 : 0,
                (activeChannel == TextureChannel.A) ? 1 : 0
            );
            return channelWeight;
        }
    }

    public int _PerspectiveMode
    {
        get
        {
            int _perspectiveMode;

            if (perspectiveMode == PerspectiveMode.Front) _perspectiveMode = 1;
            else if (perspectiveMode == PerspectiveMode.Side) _perspectiveMode = 2;
            else if (perspectiveMode == PerspectiveMode.TopDown) _perspectiveMode = 3;
            else
            {
                Debug.LogError("PerspectiveMode is not set correctly");
                _perspectiveMode = 1;
            }

            return _perspectiveMode;
        }
    }


    public int _DebugViewMode
    {
        get
        {
            int _debugViewMode = 0;

            if (currentViewer == CurrentViewer.NoiseGeneration) 
            {
                _debugViewMode = (noiseGenerator.activeTextureType == NoiseGenerator.CloudNoiseType.Shape) ? 1 : 2;
            }
            else if (currentViewer == CurrentViewer.DeepShadowMap)
            {
                _debugViewMode = 3;
            }
            else if(currentViewer == CurrentViewer.CloudCoverage)
            {
                _debugViewMode = 4;
            }
            else
            {
                _debugViewMode = 0;
                Debug.LogError("_DebugViewMode is not set correctly");
            }

            return _debugViewMode;
        }
    }


    public void UpdateViewerSettings(CurrentViewer _currentViewer, PerspectiveMode _perspectiveMode, 
                                     TextureChannel _textureChannel, bool _viewerGreyscale,  bool _viewerShowAllChannels, 
                                     float _viewerSliceDepth, float _viewerTileAmount, float _viewerSize)
    {


        activeChannel = _textureChannel;
        perspectiveMode = _perspectiveMode;
        viewerGreyscale = _viewerGreyscale;
        viewerShowAllChannels = _viewerShowAllChannels;
        viewerSliceDepth = _viewerSliceDepth;
        viewerTileAmount = _viewerTileAmount;
        viewerSize = _viewerSize;

        currentViewer = _currentViewer;
        DisableOtherViewers();
    }

    private void DisableOtherViewers()
    {
        switch (currentViewer)
        {
            case CurrentViewer.CloudCoverage:
                if (noiseGenerator != null)
                    noiseGenerator.viewerEnabled = false;
                if (deepShadowMapGen != null)
                    deepShadowMapGen.viewerEnabled = false;
                break;

            case CurrentViewer.NoiseGeneration:
                if (cloudCoverageSettings != null)
                    cloudCoverageSettings.viewerEnabled = false;
                if (deepShadowMapGen != null)
                    deepShadowMapGen.viewerEnabled = false;
                break;

            case CurrentViewer.DeepShadowMap:
                if (noiseGenerator != null)
                    noiseGenerator.viewerEnabled = false;
                if (cloudCoverageSettings != null)
                    cloudCoverageSettings.viewerEnabled = false;
                break;

            default:
                Debug.LogError("CurrentViewer is not set correctly");
                break;
        }
    }


    public void EnableViewer()
    {
        viewerEnabled = true;
    }

    public void DisableViewer()
    {
        viewerEnabled = false;
    }

    public void SetShaderProperties(ref ComputeShader compute)
    {
        if(viewerEnabled)
        {
            compute.EnableKeyword("DEBUG_MODE");
            compute.SetInt("debugViewMode", _DebugViewMode);

            compute.SetInt("debugPerspectiveMode", TextureViewerController.S._PerspectiveMode);
            compute.SetVector("debugChannelWeight", TextureViewerController.S.ChannelMask);


            compute.SetFloat("debugNoiseSliceDepth", TextureViewerController.S.viewerSliceDepth);
            compute.SetFloat("debugTileAmount", TextureViewerController.S.viewerTileAmount);
            compute.SetFloat("viewerSize", TextureViewerController.S.viewerSize);
            compute.SetInt("debugGreyscale", (TextureViewerController.S.viewerGreyscale) ? 1 : 0);
            compute.SetInt("debugShowAllChannels", (TextureViewerController.S.viewerShowAllChannels) ? 1 : 0);
        }
        else
        {
            compute.DisableKeyword("DEBUG_MODE");
        }

       
    }

 


}

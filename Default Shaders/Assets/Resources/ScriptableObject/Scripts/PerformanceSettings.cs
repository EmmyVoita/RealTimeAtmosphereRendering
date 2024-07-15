using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/PerformanceSettings")]
public class PerformanceSettings : ScriptableObject
{
    public enum FrameRenderingMode
    {
        Block_1x1,
        Block_2x2,
        Block_4x4,
    }

    [Header("FrameRendering:")]
    [Tooltip("Render frames in 'kxk' blocks over k^2 frames.")]
    public FrameRenderingMode frameRenderingMode = FrameRenderingMode.Block_1x1;
    [SerializeField]
    private uint frameCounter = 0;
    private ComputeBuffer frameBuffer;


    public void SetFrameCounter(uint value) { frameCounter = value; }
    public uint GetFrameCounter() { return frameCounter; }


    public int GetFrameInterval()
    {
        switch (frameRenderingMode)
        {
            case FrameRenderingMode.Block_1x1:
                return 1;
            case FrameRenderingMode.Block_2x2:
                return 4;
            case FrameRenderingMode.Block_4x4:
                return 16;
            default:
                return 1;
        }
    }

    private int SubPixelSizeToInt()
    {
        int value = 2;

        switch(frameRenderingMode)
        {
            case FrameRenderingMode.Block_1x1: value = 1; break;
            case FrameRenderingMode.Block_2x2: value = 2; break;
            case FrameRenderingMode.Block_4x4: value = 4; break;
        }

        return value;
    }

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {

        // Set uInt:
        frameBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        frameBuffer.SetData(new uint[] { frameCounter });
        compute.SetBuffer(kernelID, "FrameCounterBuffer", frameBuffer);
   

        // Set Int:
        compute.SetInt("frameInterval", GetFrameInterval());
    }
    public void ReleaseBuffer()
    {
        frameBuffer.Release();
    }
    


}
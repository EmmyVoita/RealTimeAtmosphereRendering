using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/DeepShadowMapSettings")]
public class DeepShadowMapSettings : ScriptableObject
{
    public ComputeShader ResetCompute;
    private int KernelResetNumberBuffer;
    private int KernelResetDepthBuffer;

    private ComputeBuffer NumberBuffer;
    private ComputeBuffer DepthBuffer;
    [SerializeField]
    const int dimension = 1024;
    [SerializeField]
    const int elements = 32;

  

    // Start is called before the first frame update
    public void Initialize()
    {
        int numElement = dimension * dimension * elements;
        if (NumberBuffer == null) NumberBuffer = new ComputeBuffer(dimension * dimension, sizeof(uint));
        if (DepthBuffer == null) DepthBuffer = new ComputeBuffer(numElement, sizeof(float) * 2);

        KernelResetNumberBuffer = ResetCompute.FindKernel("KernelResetNumberBuffer");
        KernelResetDepthBuffer = ResetCompute.FindKernel("KernelResetDepthBuffer");
    }

    // Update is called once per frame
    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        compute.SetInt("Dimension", dimension);

        compute.SetBuffer(kernelID, "NumberBuffer", NumberBuffer);
        compute.SetBuffer(kernelID, "DepthBuffer", DepthBuffer);

        ResetCompute.Dispatch(KernelResetNumberBuffer, dimension / 8, dimension / 8, 1);


        compute.SetBuffer(kernelID, "NumberBuffer", NumberBuffer);
        compute.SetBuffer(kernelID, "DepthBuffer", DepthBuffer);

        Shader.SetGlobalBuffer("NumberBuffer", NumberBuffer);
        //Shader.SetGlobalBuffer("DepthBuffer", DepthBuffer);
        Shader.SetGlobalInt("Dimension", dimension);
    }

    public void Cleanup()
    {
        NumberBuffer.Dispose();
        DepthBuffer.Dispose();
    }

}

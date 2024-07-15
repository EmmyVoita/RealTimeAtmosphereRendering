using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/AnimationSettings")]
public class AnimationSettings : ScriptableObject
{

    public float timeScale = 1;
    public float baseSpeed = 1;
    public float detailSpeed = 2;
    public float CURL_SPEED = 0.5f;
    public Vector3 WIND_DIR = new Vector3(0.4f, 0.1f, 1.0f);
    public float WIND_SPEED = 0.75f;

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        // Set Float:
        compute.SetFloat("timeScale", (Application.isPlaying) ? timeScale : 0);
        compute.SetFloat("baseSpeed", baseSpeed);
        compute.SetFloat("detailSpeed", detailSpeed);
        compute.SetFloat("CURL_SPEED",  CURL_SPEED);

        compute.SetVector("WIND_DIR",  WIND_DIR);
        compute.SetFloat("WIND_SPEED",  WIND_SPEED);

    }
}
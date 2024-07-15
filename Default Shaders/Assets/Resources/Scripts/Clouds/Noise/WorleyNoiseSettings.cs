using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class WorleyNoiseSettings : ScriptableObject {

    [Header ("Worley Noise Settings: ")]
    public int seed;
    [Range (1, 50)]
    public int numDivisionsA = 5;
    [Range (1, 50)]
    public int numDivisionsB = 10;
    [Range (1, 50)]
    public int numDivisionsC = 15;

    public float persistence = .5f;
    public int tile = 1;
    public bool invertWorley = true;
    public float worleyContrastExponent = 1.0f;

    [Header ("Perlin Noise Settings: ")]

    public float perlin_frequency = 4.0f;
    public int perlin_octaves = 7;

    public bool invertPerlin = false;
    public bool showJustPerlin = false;
    public float perlinContrastExponent = 3.0f;

    [Range (0, 1)]
    public float perlinRemapMin = 0.22f;
    [Range (0, 1)]
    public float perlinRemapMax = 1.0f;

    [Header ("Combine Settings: ")]
     [Range (0, 1)]
    public float perlinInfluence = 0.5f;
    public bool usePerlinWorley = false;
    public bool invertCombinedNoise = false;

    //[Header ("Shared Settings: ")]



}
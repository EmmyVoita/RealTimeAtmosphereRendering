using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AtmosphereRendering;

public class VisualizeAtmosphere : MonoBehaviour
{
    public AtmosphereSettings atmosphereSettings;
    public CombineComputeMaster combineComputeMaster;

    public bool showTotalRadius = true;
    public bool showPlanetRadius = true;

    [Header("Test Sample Point")]
    public bool showTestSamplePoint = true; 
    public Vector3 samplePoint = new Vector3(0,0,0);    
    public float samplePointSphereRadius = 10.0f;
    public float current_height = 0.0f;



    private Vector3 planetCenter = new Vector3(0,0,0);

    void OnDrawGizmos()
    {
       


        // Pass the atmo container dimensions
        Vector3 size_a = combineComputeMaster.atmo_container.localScale;
        int width_a = Mathf.CeilToInt (size_a.x);
        int height_a = Mathf.CeilToInt (size_a.y);
        int depth_a = Mathf.CeilToInt (size_a.z);

        Vector4 mapSize_Atmo = new Vector4 (width_a, height_a, depth_a, 0); 
        Vector3 boundsMin_Atmo = combineComputeMaster.atmo_container.position - combineComputeMaster.atmo_container.localScale / 2;
        Vector3 boundsMax_Atmo =  combineComputeMaster.atmo_container.position + combineComputeMaster.atmo_container.localScale / 2;


        planetCenter.x = boundsMin_Atmo.x + (boundsMax_Atmo.x - boundsMin_Atmo.x)/2;
        planetCenter.z = boundsMin_Atmo.z + (boundsMax_Atmo.z - boundsMin_Atmo.z)/2;

     

        float atmosphereRadius = ((boundsMax_Atmo.y + atmosphereSettings.atmosphereRadiusOffset) - boundsMin_Atmo.y);
        float totalRadius = atmosphereSettings.planet_radius + atmosphereRadius;  
        float planetCenterY = boundsMin_Atmo.y - (atmosphereSettings.planet_radius);
        planetCenter.y = planetCenterY;

        if(showPlanetRadius)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(planetCenter, atmosphereSettings.planet_radius);
        }

        if(showTotalRadius)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(planetCenter, totalRadius);
        }

        if(showTestSamplePoint)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(samplePoint, samplePointSphereRadius);

            current_height = CalculateHeight(samplePoint);
            Debug.DrawRay(new Vector3(planetCenter.x, boundsMin_Atmo.y, planetCenter.z), Vector3.up * current_height, Color.white);
        }   
    }

    public float CalculateHeight(Vector3 samplePosition)
    {
        Vector3 offsetPrimarySamplePos = samplePosition - planetCenter;
        float current_height = Vector3.Magnitude(offsetPrimarySamplePos) - atmosphereSettings.planet_radius;
        
        return current_height;
    }
}

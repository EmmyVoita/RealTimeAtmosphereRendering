using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using AtmosphereRendering;

public struct PlanetData
{
    public Vector3 planet_center;
    public float planet_radius;
    public float atmosphere_radius;
    public float total_radius;
    public float scale_ratio;
    public float planet_scale;
    public float atmosphere_scale;
};

public struct Ray
{
    public Vector3 origin;
    public Vector3 position;
    public Vector3 direction;
};

public class VisualizeRayMarch : MonoBehaviour
{
    public Camera playerCamera;
    public AtmosphereSettings atmosphereSettings;

    public RayMarchSettings rayMarchSettings;
    public CombineComputeMaster combineComputeMaster;

    public float maxDist = 100000.0f;
    public float samplePointSize = 10.0f;

    public int rayGridSize = 5;

    public bool viewRayMarch = true;    
    public bool viewRayBoxIntersect = true;

    public bool viewStepDistribution = true;
    public bool viewRayBoxIntersectAtmo = true;



    const float reference_planet_radius = 6371000.0f;
    const float reference_atmosphere_radius = 100000.0f;
    // reference planet data
    [Header("Reference Data: ")]
    public float reference_total_radius = 6371000.0f + 100000.0f;
    public float reference_ratio = (6371000.0f + 100000.0f) / 100000.0f;
    private Vector3 planetCenter = new Vector3(0,0,0);



    [Header("Atmosphere Intersection Data: ")]
    public Vector3 atmosphereEntryPoint;
     public Vector3 atmosphereExitPoint;


    [Header("PlanetData: ")]
    
    [SerializeField]
    private float planet_ScaleRatio;
    [SerializeField]
    private float planet_PlanetScale;
    [SerializeField]
    private float planet_AtmosphereScale;
    [SerializeField]
    private float planet_Radius;
    [SerializeField]
    private float planet_AtmosphereRadius;
    [SerializeField]
    private float planet_TotalRadius;
    [SerializeField]
    private Vector3 planet_Center;






    public PlanetData CreatePlanetData(Vector3 planet_center, float planetRadius, float atmosphereRadius, float totalRadius)
    {
        PlanetData planet_data;
        planet_data.planet_center = planet_center;
        planet_data.planet_radius = planetRadius;
        planet_data.atmosphere_radius = atmosphereRadius;
        planet_data.total_radius = totalRadius;

        planet_data.scale_ratio = planetRadius / atmosphereRadius;
        planet_data.planet_scale = reference_planet_radius / planetRadius;
        planet_data.atmosphere_scale = planet_data.scale_ratio / reference_ratio;
        return planet_data;
    }

    Ray CreateRay(Vector3 position, Vector3 direction)
    {
        Ray ray;
        ray.position = position;
        ray.origin = position;
        ray.direction = direction;
        return ray;
    }


   Ray CreateCameraRay(Vector2 uv, Matrix4x4 unity_CameraToWorld, Matrix4x4 _CameraInverseProjection)
    {
        // Transform the camera origin to world space
        Vector3 position = unity_CameraToWorld.MultiplyPoint(Vector3.zero);

        // Invert the perspective projection of the view-space position
        Vector3 direction = _CameraInverseProjection.MultiplyPoint(new Vector3(uv.x * 2 - 1, uv.y * 2 - 1, 1));

        // Transform the direction from camera to world space and normalize
        direction = unity_CameraToWorld.MultiplyVector(direction).normalized;

        return CreateRay(position, direction);
    }


    // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
    Vector2 RayBoxDst(Vector3 boundsMin, Vector3 boundsMax, Vector3 rayOrigin, Vector3 invRaydir, out bool intersects)
    {
        // Adapted from: http://jcgt.org/published/0007/03/04/
        Vector3 t0 = Vector3.Scale(boundsMin - rayOrigin, invRaydir);
        Vector3 t1 = Vector3.Scale(boundsMax - rayOrigin, invRaydir);
        Vector3 tmin = Vector3.Min(t0, t1);
        Vector3 tmax = Vector3.Max(t0, t1);

        float dstA = Mathf.Max(Mathf.Max(tmin.x, tmin.y), tmin.z);
        float dstB = Mathf.Min(tmax.x, Mathf.Min(tmax.y, tmax.z));

        // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
        // dstA is dst to nearest intersection, dstB dst to far intersection

        // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
        // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

        // CASE 3: ray misses box (dstA > dstB)

        float dstToBox = Mathf.Max(0, dstA);
        float dstInsideBox = Mathf.Max(0, dstB - dstToBox);

        intersects = dstInsideBox != 0;

        return new Vector2(dstToBox, dstInsideBox);
    }


    
    
    Vector2 RaySphereIntersect(Vector3 start, Vector3 dir, float radius)
    {
        // Translate the ray to be relative to the sphere's center
        // Vector3 ray_origin_relative_to_center = start - sphere_center;

        // Ray-sphere intersection using quadratic equation
        float a = Vector3.Dot(dir, dir);
        float b = 2.0f * Vector3.Dot(dir, start);
        float c = Vector3.Dot(start, start) - (radius * radius);
        float d = (b * b) - 4.0f * a * c;

        if (d < 0.0f)
            return new Vector2(100000, -100000);

        // first and second intersection point along the ray's path
        float first_intersection = (-b - Mathf.Sqrt(d)) / (2.0f * a);
        float second_intersection = (-b + Mathf.Sqrt(d)) / (2.0f * a);

        return new Vector2(first_intersection, second_intersection);
    }



  



    void OnDrawGizmos()
    {
        Vector3 boundsMin = combineComputeMaster.container.position - combineComputeMaster.container.localScale / 2;
        Vector3 boundsMax =  combineComputeMaster.container.position + combineComputeMaster.container.localScale / 2;


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


        PlanetData planet_data = CreatePlanetData(planetCenter, atmosphereSettings.planet_radius, atmosphereRadius, totalRadius);
        planet_ScaleRatio = planet_data.scale_ratio;
        planet_PlanetScale = planet_data.planet_scale;
        planet_AtmosphereScale = planet_data.atmosphere_scale;
        planet_Radius = planet_data.planet_radius;
        planet_AtmosphereRadius = planet_data.atmosphere_radius;
        planet_TotalRadius = planet_data.total_radius;
        planet_Center = planet_data.planet_center;
        
        if(viewRayMarch)
        {
           
            // Loop through the rays in the grid
            for (int i = 0; i < rayGridSize; i++)
            {
                for (int j = 0; j < rayGridSize; j++)
                {
                    // Calculate UV coordinates based on grid position
                    float uvX = (float)i / (rayGridSize - 1);
                    float uvY = (float)j / (rayGridSize - 1);

                    // Create camera ray using UV coordinates
                    Ray cameraRay = CreateCameraRay(new Vector2(uvX, uvY), playerCamera.cameraToWorldMatrix, playerCamera.projectionMatrix.inverse);

                    bool dodrawRay = true;

                    Vector3 ray_origin = cameraRay.origin;
                    Vector3 ray_direction = cameraRay.direction;

                    ray_origin -= planet_data.planet_center;

                    //-----------------------------------------------------------------------------------------------------------------
                    // Cloud Ray Box Intersection
                    //-----------------------------------------------------------------------------------------------------------------
                    
                    // point of intersection with the cloud container
                    //Vector3 entryPoint = rayOrigin + ray_direction * dstToBox;
                    //float dstTraveled = blueNoiseOffset;
                    //Vector3 ray_position_cloud = rayOrigin;

                    // Calculate the primary step size.
                    //float stepPrimary = dstInsideBox / float(PRIMARY_STEP_COUNT);



                    bool renderAtmosphere = false;
                    // Intersection of the ray with the atmosphere box
                    Vector2 rayToContainerInfo = RayBoxDst(boundsMin_Atmo, boundsMax_Atmo, cameraRay.origin, new Vector3(1.0f / cameraRay.direction.x, 1.0f / cameraRay.direction.y, 1.0f / cameraRay.direction.z), out renderAtmosphere);

                    // Early out if ray doesn't intersect atmosphere.
                    if (!renderAtmosphere)
                    {
                        dodrawRay = false;
                    }


                    //-----------------------------------------------------------------------------------------------------------------
                    // Ray march step size and starting position
                    //-----------------------------------------------------------------------------------------------------------------


                    float a = Vector3.Dot(ray_direction, ray_direction);
                    float b = 2.0f * Vector3.Dot(ray_direction, ray_origin);
                    float c = Vector3.Dot(ray_origin, ray_origin) - (planet_data.total_radius * planet_data.total_radius);
                    float d = (b * b) - 4.0f * a * c;

                    // Early out if ray doesn't intersect atmosphere.
                    if (d < 0.0)
                        dodrawRay = false;
                
                    
                    Vector2 ray_length = new Vector2( Mathf.Max((-b - Mathf.Sqrt(d)) / (2.0f * a), 0.0f), Mathf.Min((-b + Mathf.Sqrt(d)) / (2.0f * a), maxDist));


                    if(ray_length.x > ray_length.y)
                        dodrawRay = false;

                    

                    // For calculations using the viewer's position, the actual position used is dependent on whether the viewer is inside the atmosphere.
                    // If the viewer is inside, use viewer's position. If the viewer is outside, use the intersection point with the atmosphere.
                    // This is accomplished by taking the max between the first intersection point and 0.
                    ray_length.x = Mathf.Max(ray_length.x, 0.0f);

                    // Set the intersect to the ground intersect (max_dist) when applicable.
                    ray_length.y = Mathf.Min(ray_length.y, maxDist);


                    //-----------------------------------------------------------------------------------------------------------------
                    // Ray march step size and starting position
                    //-----------------------------------------------------------------------------------------------------------------

                    // Calculate the first intersection point along the ray. Intersection with the atmosphere.
                    Vector3 intersection_point = ray_origin + ray_direction * ray_length.x;
                    Vector3 end_point = ray_origin + ray_direction * ray_length.y;

                    // get the step size of the ray
                    float actual_step_size_p = (ray_length.y - ray_length.x) / (float) rayMarchSettings.STEPS_PRIMARY;
                    float virtual_step_size_p = actual_step_size_p * planet_data.planet_scale;

                    float cloudActualStepSize_p  = actual_step_size_p/5;
                    float cloudVirtualStepSize_p = cloudActualStepSize_p * planet_data.planet_scale;

                    if(dodrawRay)
                    {
                    
                        Gizmos.color = Color.red;
                    
                        
                        Gizmos.DrawRay(cameraRay.origin, cameraRay.direction * (ray_length.y - ray_length.x));

                        float ray_pos_p = ray_length.x + actual_step_size_p * 0.5f;
                        for(int k = 0; k < rayMarchSettings.STEPS_PRIMARY; k++)
                        {
                            // calculate where we are along this ray
                            Vector3 primary_sample_pos = cameraRay.origin + cameraRay.direction * ray_pos_p;

                            bool renderClouds = false;

                            // Determine if the ray intersects the bounding cloud volume:    
                            Vector2 rayToContainerInfoCloud = RayBoxDst(boundsMin, boundsMax, cameraRay.origin, new Vector3(1.0f / cameraRay.direction.x, 1.0f / cameraRay.direction.y, 1.0f / cameraRay.direction.z), out renderClouds);
                            float dstToBox = rayToContainerInfoCloud.x;
                            float dstInsideBox = rayToContainerInfoCloud.y;

                            if(renderClouds && ray_pos_p > dstToBox && ray_pos_p < dstInsideBox)
                            {
                                Gizmos.color = Color.yellow;
                                Gizmos.DrawSphere(primary_sample_pos, samplePointSize);
                                ray_pos_p += cloudActualStepSize_p;
                            }
                            else
                            {
                                Gizmos.color = Color.black;
                                Gizmos.DrawSphere(primary_sample_pos, samplePointSize);
                                ray_pos_p += actual_step_size_p;
                            }
                            
                        }
                    }
                }
            }
        }
       
        if(viewRayBoxIntersect)
        {
            Ray cameraRay = CreateCameraRay(new Vector2(0.5f, 0.5f), playerCamera.cameraToWorldMatrix, playerCamera.projectionMatrix.inverse);

            bool renderClouds = false;
            // Determine if the ray intersects the bounding cloud volume:    
            Vector2 rayToContainerInfoCloud = RayBoxDst(boundsMin, boundsMax, cameraRay.origin, new Vector3(1.0f / cameraRay.direction.x, 1.0f / cameraRay.direction.y, 1.0f / cameraRay.direction.z), out renderClouds);
            float dstToBox = rayToContainerInfoCloud.x;
            float dstInsideBox = rayToContainerInfoCloud.y;

            if(renderClouds)
            {
                Vector3 enteryPoint = cameraRay.origin + cameraRay.direction * dstToBox;



                Gizmos.color = Color.grey;
                Gizmos.DrawRay(cameraRay.origin, cameraRay.direction * (dstInsideBox + dstToBox));

                

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(enteryPoint, samplePointSize);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(cameraRay.origin + cameraRay.direction * (dstInsideBox + dstToBox), samplePointSize);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(cameraRay.origin + cameraRay.direction * dstInsideBox, samplePointSize);

                /*
                if(dstToBox >= dstInsideBox)
                {
                 
                  
                }
                else
                {
                    Gizmos.color = Color.red;
                      Gizmos.DrawSphere(cameraRay.origin + cameraRay.direction * dstInsideBox, samplePointSize);
                }
                */
                
            }
        }

        if(viewRayBoxIntersectAtmo)
        {
            Ray cameraRay = CreateCameraRay(new Vector2(0.5f, 0.5f), playerCamera.cameraToWorldMatrix, playerCamera.projectionMatrix.inverse);

            bool dodrawRay = true;

            // get where the ray intersects the planet
            Vector2 planet_intersect = RaySphereIntersect(cameraRay.origin - planet_data.planet_center, cameraRay.direction, planet_data.planet_radius);
            Vector2 atmosphere_intersect = RaySphereIntersect(cameraRay.origin - planet_data.planet_center, cameraRay.direction, planet_data.total_radius);


              
            if (0.0 < planet_intersect.y) 
            {
                maxDist = Mathf.Max(planet_intersect.x, 0.0f);
            }
            else if(0.0 < atmosphere_intersect.y)
            {
                maxDist = Mathf.Max(atmosphere_intersect.y, 0.0f);
            }
            else
            {
                maxDist = 100000000.0f;
            }
    



            bool renderAtmosphere = false;
            // Determine if the ray intersects the bounding cloud volume:    
            Vector2 rayToContainerInfoAtmo = RayBoxDst(boundsMin_Atmo, boundsMax_Atmo, cameraRay.origin, new Vector3(1.0f / cameraRay.direction.x, 1.0f / cameraRay.direction.y, 1.0f / cameraRay.direction.z), out renderAtmosphere);
            float dstToBox = rayToContainerInfoAtmo.x;
            float dstInsideBox = rayToContainerInfoAtmo.y;

            // Early out if ray doesn't intersect atmosphere.
            if (!renderAtmosphere)
            {
                dodrawRay = false;
            }
            

          

            Vector3 ray_origin = cameraRay.origin;
            Vector3 ray_direction = cameraRay.direction;

            ray_origin -= planet_data.planet_center;

            //-----------------------------------------------------------------------------------------------------------------
            // Cloud Ray Box Intersection
            //-----------------------------------------------------------------------------------------------------------------
            
            // point of intersection with the cloud container
            //Vector3 entryPoint = rayOrigin + ray_direction * dstToBox;
            //float dstTraveled = blueNoiseOffset;
            //Vector3 ray_position_cloud = rayOrigin;

            // Calculate the primary step size.
            //float stepPrimary = dstInsideBox / float(PRIMARY_STEP_COUNT);



          
      
          

            //-----------------------------------------------------------------------------------------------------------------
            // Ray march step size and starting position
            //-----------------------------------------------------------------------------------------------------------------


            float a = Vector3.Dot(ray_direction, ray_direction);
            float b = 2.0f * Vector3.Dot(ray_direction, ray_origin);
            float c = Vector3.Dot(ray_origin, ray_origin) - (planet_data.total_radius * planet_data.total_radius);
            float d = (b * b) - 4.0f * a * c;

            // Early out if ray doesn't intersect atmosphere.
            if (d < 0.0)
                dodrawRay = false;
        
            
            Vector2 ray_length = new Vector2( Mathf.Max((-b - Mathf.Sqrt(d)) / (2.0f * a), 0.0f), Mathf.Min((-b + Mathf.Sqrt(d)) / (2.0f * a), maxDist));


            if(ray_length.x > ray_length.y)
                dodrawRay = false;

            

            // For calculations using the viewer's position, the actual position used is dependent on whether the viewer is inside the atmosphere.
            // If the viewer is inside, use viewer's position. If the viewer is outside, use the intersection point with the atmosphere.
            // This is accomplished by taking the max between the first intersection point and 0.
            ray_length.x = Mathf.Max(ray_length.x, 0.0f);

            // Set the intersect to the ground intersect (max_dist) when applicable.
            ray_length.y = Mathf.Min(ray_length.y, maxDist);


            //-----------------------------------------------------------------------------------------------------------------
            // Ray march step size and starting position
            //-----------------------------------------------------------------------------------------------------------------

            // Calculate the first intersection point along the ray. Intersection with the atmosphere.
            Vector3 intersection_point = ray_origin + ray_direction * ray_length.x;
            Vector3 end_point = ray_origin + ray_direction * ray_length.y;

            // get the step size of the ray
            float actual_step_size_p = (ray_length.y - ray_length.x) / (float) rayMarchSettings.STEPS_PRIMARY;
            float virtual_step_size_p = actual_step_size_p * planet_data.planet_scale;

            float cloudActualStepSize_p  = actual_step_size_p/5;
            float cloudVirtualStepSize_p = cloudActualStepSize_p * planet_data.planet_scale;



         
            if(dodrawRay)
            {
               atmosphereEntryPoint = (ray_origin + planet_data.planet_center) + ray_direction * rayToContainerInfoAtmo.x; 
               atmosphereExitPoint = (ray_origin + planet_data.planet_center) + ray_direction * maxDist; 
               // Vector3 enteryPoint = cameraRay.origin + cameraRay.direction * dstToBox;



                Gizmos.color = Color.grey;
                Gizmos.DrawRay(cameraRay.origin, cameraRay.direction * ( ray_length.y));

                

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(atmosphereEntryPoint, samplePointSize);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(atmosphereExitPoint, samplePointSize);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(cameraRay.origin + cameraRay.direction * dstInsideBox, samplePointSize);

                /*
                if(dstToBox >= dstInsideBox)
                {
                 
                  
                }
                else
                {
                    Gizmos.color = Color.red;
                      Gizmos.DrawSphere(cameraRay.origin + cameraRay.direction * dstInsideBox, samplePointSize);
                }
                */
                
            }
        }

        if(viewStepDistribution)
        {
          
            Ray cameraRay = CreateCameraRay(new Vector2(0.5f, 0.5f), playerCamera.cameraToWorldMatrix, playerCamera.projectionMatrix.inverse);

            // get where the ray intersects the planet
            Vector2 planet_intersect = RaySphereIntersect(cameraRay.origin - planet_data.planet_center, cameraRay.direction, planet_data.planet_radius);
            Vector2 atmosphere_intersect = RaySphereIntersect(cameraRay.origin - planet_data.planet_center, cameraRay.direction, planet_data.total_radius);

            if (0.0 < planet_intersect.y) 
            {
                maxDist = Mathf.Max(planet_intersect.x, 0.0f);
            }
            else if(0.0 < atmosphere_intersect.y)
            {
                maxDist = Mathf.Max(atmosphere_intersect.y, 0.0f);
            }
            else
            {
                maxDist = 100000000.0f;
            }
    


            Vector3 ray_origin =cameraRay.origin;
            //ray_origin -= planet_data.planet_center;
            Vector3 ray_direction = cameraRay.direction;

            bool dodrawRay = true; 

            float a = Vector3.Dot(ray_direction, ray_direction);
            float b = 2.0f * Vector3.Dot(ray_direction, ray_origin);
            float c = Vector3.Dot(ray_origin, ray_origin) - (planet_data.total_radius * planet_data.total_radius);
            float d = (b * b) - 4.0f * a * c;

            // Early out if ray doesn't intersect atmosphere.
            if (d < 0.0)
                dodrawRay = false;
        
            
            Vector2 ray_length = new Vector2( Mathf.Max((-b - Mathf.Sqrt(d)) / (2.0f * a), 0.0f), Mathf.Min((-b + Mathf.Sqrt(d)) / (2.0f * a), maxDist));


            if(ray_length.x > ray_length.y)
                dodrawRay = false;

            

            // For calculations using the viewer's position, the actual position used is dependent on whether the viewer is inside the atmosphere.
            // If the viewer is inside, use viewer's position. If the viewer is outside, use the intersection point with the atmosphere.
            // This is accomplished by taking the max between the first intersection point and 0.
            ray_length.x = Mathf.Max(ray_length.x, 0.0f);

            // Set the intersect to the ground intersect (max_dist) when applicable.
            ray_length.y = Mathf.Min(ray_length.y, maxDist);


            //-----------------------------------------------------------------------------------------------------------------
            // Ray march step size and starting position
            //-----------------------------------------------------------------------------------------------------------------

            // Calculate the first intersection point along the ray. Intersection with the atmosphere.
            Vector3 intersection_point = ray_origin + ray_direction * ray_length.x;
            Vector3 end_point = ray_origin + ray_direction * ray_length.y;
                 
            // get the step size of the ray
            float actual_step_size_p = (ray_length.y - ray_length.x) / (float) rayMarchSettings.STEPS_PRIMARY;
            float virtual_step_size_p = actual_step_size_p * planet_data.planet_scale;
            

            bool renderClouds = false;
            // Determine if the ray intersects the bounding cloud volume:    
            Vector2 rayToContainerInfoCloud = RayBoxDst(boundsMin, boundsMax, cameraRay.origin, new Vector3(1.0f / cameraRay.direction.x, 1.0f / cameraRay.direction.y, 1.0f / cameraRay.direction.z), out renderClouds);
            float dstToBox = rayToContainerInfoCloud.x;
            float dstInsideBox = rayToContainerInfoCloud.y;




            if(dodrawRay)
            {

                Gizmos.color = Color.grey;
                Gizmos.DrawRay(ray_origin, ray_direction * ray_length.y);

                

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(intersection_point, samplePointSize);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(end_point, samplePointSize);

                float ray_pos_p = ray_length.x + actual_step_size_p * 0.5f;
                for(int k = 0; k < rayMarchSettings.STEPS_PRIMARY; k++)
                {
                    // calculate where we are along this ray
                    Vector3 primary_sample_pos = cameraRay.origin + cameraRay.direction * ray_pos_p;
                    Gizmos.color = Color.white;
                    Gizmos.DrawSphere(primary_sample_pos, samplePointSize);
                    ray_pos_p += actual_step_size_p;
                }        
            }
        }

    }
}

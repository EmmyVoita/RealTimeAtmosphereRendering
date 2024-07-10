
#include "../Includes/AtmoRender.cginc"

//static const float PI = 3.14159265359;
//static const float PI = 3.14159265359;
//static const float TAU = PI * 2;
//static const float maxFloat = 3.402823466e+38;

//
//static const int PRIMARY_STEP_COUNT = 128;
//static const int LIGHT_STEP_COUNT = 32;
//static const int NUM_DIR_LIGHTS = 1;

// reference planet data
//static const float reference_planet_radius = 6371000.0;
//static const float reference_atmosphere_radius = 100000.0;
//static const float reference_total_radius = reference_planet_radius + reference_atmosphere_radius;
//static const float reference_ratio = reference_planet_radius / reference_atmosphere_radius;

// scattering coeffs
//static const float3 BETA_RAYLEIGH = float3(0.0000055, 0.000013, 0.0000224);                   /* rayleigh, affects the color of the sky */
//static const float3 BETA_RAYLEIGH = float3(0.000005802, 0.000013558, 0.0000331);
//static const float  BETA_MIE =  0.000021;                                                      /* mie, affects the color of the blob around the sun */
//static const float  BETA_MIE =  0.0000044;
//static const float3 BETA_ABSORPTION  = float3(0.0000204, 0.0000497, 0.00000195);               /* what color gets absorbed by the atmosphere (Due to things like ozone) */
//static const float3 BETA_ABSORPTION  = float3(0.00000065, 0.000001881, 0.000000085); 
//static const float3 BETA_AMBIENT = float3(0.000000,0.0000000,0.0000000);                      // the amount of scattering that always occurs, cna help make the back side of the atmosphere a bit brighter
//static const float g = 0.70;                                                                  /* mie scattering direction, or how big the blob around the sun is */


// and the heights (how far to go up before the scattering has no effect)
//static const float HEIGHT_RAY = 8000.0;                                                       /* rayleigh height */
///static const float HEIGHT_MIE = 1200.0;                                                       /* mie height */
//static const float HEIGHT_ABSORPTION = 30000;                                                 /* at what height the absorption is at it's maximum */
//static const float ABSORPTION_FALLOFF = 4000;                                                 /* how much the absorption decreases the further away it gets from the maximum height */
/*
struct PlanetData
{
    float3 planet_center;
    float planet_radius;
    float atmosphere_radius;
    float total_radius;
    float scale_ratio;
    float planet_scale;
    float atmosphere_scale;
};

PlanetData CreatePlanetData(float3 planet_center, float planet_radius, float atmosphere_radius)
{
    PlanetData planet_data;
    planet_data.planet_center = planet_center;
    planet_data.planet_radius = planet_radius;
    planet_data.atmosphere_radius = atmosphere_radius - planet_radius;
    planet_data.total_radius =  planet_radius + planet_data.atmosphere_radius;



    planet_data.scale_ratio = planet_radius / planet_data.atmosphere_radius;
    planet_data.planet_scale = reference_planet_radius / planet_radius;
    planet_data.atmosphere_scale = planet_data.scale_ratio / reference_ratio;
    return planet_data;
}
*/


//https://github.com/simondevyoutube/ProceduralTerrain_Part10/blob/main/src/scattering-shader.js
bool _RayIntersectsSphere(float3 rayStart, float3 rayDir, float3 sphereCenter, float sphereRadius, float max_dist, out float2 ray_length) 
{
    float a = dot(rayDir, rayDir);
    float b = 2.0 * dot(rayDir, rayStart);
    float c = dot(rayStart, rayStart) - sphereRadius * sphereRadius;
    float d =  b * b - 4.0 * a * c;

    // Also skip single point of contact
    if (d < 0.0) 
        return false;

    float r0 = (-b - sqrt(d)) / (2.0 * a);
    float r1 = (-b + sqrt(d)) / (2.0 * a);

    ray_length = float2(max(r0,0.0), min(r1, max_dist));

    return (max(r0,r1) >= 0.0);
}






float3 SampleLightRay(float3 ray_origin, float max_dist, float3 light_dir, PlanetData planet_data, float4 scaled_height, int LIGHT_STEP_COUNT)
{   

    //float2 ray_length = 0;

    //float actual_step_size_l = (ray_length.y - ray_length.x) / float(LIGHT_STEP_COUNT);
    float actual_step_size_l = RayIntersectSphereLight(ray_origin, light_dir, planet_data.total_radius, LIGHT_STEP_COUNT);
    float virtual_step_size_l = actual_step_size_l * planet_data.planet_scale;
    float ray_pos_l = actual_step_size_l * 0.5;

    float3 optical_depth_light = 0;

    for (int j = 0; j < LIGHT_STEP_COUNT; j++)
    {   
        // current position along the light ray
        float3 current_light_sample_pos = ray_origin + light_dir * ray_pos_l;
        
        // the height of the position
        float current_height = length(current_light_sample_pos) - planet_data.planet_radius;

        
        float3 density = float3(exp(-current_height / scaled_height.xy), 0.0);
        //float3 density = float3(exp(-current_height / scaled_height.xy) * virtual_step_size_l, 0.0);

        
        float denom = (scaled_height.z - current_height) / scaled_height.w;
        //density.z = (1.0 / (denom * denom + 1.0)) * density.x * virtual_step_size_l;
        density.z = (1.0 / (denom * denom + 1.0)) * density.x;


        // multiply the density by the step size
        density *= virtual_step_size_l;

        // add the density to the total optical depth
        optical_depth_light += density;

        // increment the position along the ray
        ray_pos_l  +=  actual_step_size_l;
    }

    return optical_depth_light;
}


float3 CalculateScattering(float3 ray_origin, float3 ray_direction, float max_dist, float3 scene_color, float3 light_dir, float3 light_intensity, PlanetData planet_data, out float accumulated_weight, int PRIMARY_STEP_COUNT, int LIGHT_STEP_COUNT, float3 cuttoff_threshold)
{   
    ray_origin -= planet_data.planet_center;
    

    // calculate the start and end position of the ray, as a distance along the ray
    // we do this with a ray sphere intersect
    float a = dot(ray_direction, ray_direction);
    float b = 2.0 * dot(ray_direction, ray_origin);
    float c = dot(ray_origin, ray_origin) - (planet_data.total_radius * planet_data.total_radius);
    float d = (b * b) - 4.0 * a * c;

    // Early out if ray doesn't intersect atmosphere.
    if (d < 0.0) return scene_color;

    // calculate the ray length
    float2 ray_length = float2( max((-b - sqrt(d)) / (2.0 * a), 0.0), min((-b + sqrt(d)) / (2.0 * a), max_dist));

    float2 planet_intersect = RaySphereIntersect(ray_origin, ray_direction, planet_data.planet_radius, planet_data.planet_center); 
     max_dist = max(planet_intersect.x, 0.0);

    // if the ray did not hit the atmosphere, return a black color
    if(ray_length.x > ray_length.y) return scene_color;

    // prevent the mie glow from appearing if there's an object in front of the camera
    bool allow_mie = max_dist > ray_length.y;

    // make sure the ray is no longer than allowed
    ray_length.y = min(ray_length.y, max_dist);
    ray_length.x = max(ray_length.x, 0.0);

    // Calculate the intersection point along the ray.
    float3 intersection_point = ray_origin + ray_direction * ray_length.x;

    // Check if the absolute value of the x-coordinate of the intersection point is greater than some threshold.
    if (abs(intersection_point.x) > cuttoff_threshold.x || abs(intersection_point.z) > cuttoff_threshold.z || abs(intersection_point.y) >cuttoff_threshold.y) return scene_color; // Return black color.

    // get the step size of the ray
    float actual_step_size_p = (ray_length.y - ray_length.x) / float(PRIMARY_STEP_COUNT);
    float virtual_step_size_p = actual_step_size_p * planet_data.planet_scale;
    
    // next, set how far we are along the ray, so we can calculate the position of the sample
    // if the camera is outside the atmosphere, the ray should start at the edge of the atmosphere
    // if it's inside, it should start at the position of the camera
    // the min statement makes sure of that
    float ray_pos_p = ray_length.x + actual_step_size_p * 0.5;

    // Calculate the Rayleigh and Mie phases.
    // This is the color that will be scattered for this ray
    // mu, mumu and gg are used quite a lot in the calculation, so to speed it up, precalculate them
    float mu = dot(ray_direction, light_dir);
    float mumu = mu * mu;
    float gg = g * g;
    float phase_rayleigh = 3.0 / (50.2654824574) * (1.0 + mumu);
    float phase_mie = allow_mie ? 3.0 / (25.1327412287) * ((1.0) - gg) * (mumu + 1.0) / (pow(max(0.0, (1.0 + gg - 2.0 * mu * g)), 1.5) * (2.0 + gg)) : 0.0;

    // Scale the heights (how far to go up before the scattering has no effect)
    float SCALED_HEIGHT_RAY = HEIGHT_RAY / (planet_data.planet_scale * planet_data.atmosphere_scale);
    float SCALED_HEIGHT_MIE = HEIGHT_MIE / (planet_data.planet_scale * planet_data.atmosphere_scale);
    float SCALED_HEIGHT_ABSORPTION = HEIGHT_ABSORPTION * (planet_data.planet_scale * planet_data.atmosphere_scale);
    float SCALED_ABSORPTION_FALLOFF = ABSORPTION_FALLOFF / (planet_data.planet_scale * planet_data.atmosphere_scale);
    float4 SCALED_HEIGHTS = float4(SCALED_HEIGHT_RAY, SCALED_HEIGHT_MIE, SCALED_HEIGHT_ABSORPTION, SCALED_ABSORPTION_FALLOFF);

    // these are the values we use to gather all the scattered light
    float3 accumulated_rayleigh = 0;
    float3 accumulated_mie = 0;

    // initialize the optical depth.
    float3 optical_depth = 0;

    // variables for estimating a more accurate depth value
    float3 weighted_sum = float3(0, 0, 0);
    accumulated_weight = 0.0;

    
    // Take N steps along the primary ray
    for (int i = 0; i < PRIMARY_STEP_COUNT; i++)
    {   
        // calculate where we are along this ray
        float3 primary_sample_pos = ray_origin + ray_direction * ray_pos_p;
        //float3 primary_sample_pos = ray_origin + ray_direction * (primaryStepPosition + actual_step_size_p * 0.5);

        float current_height = length(primary_sample_pos) - planet_data.planet_radius;

        
        //float3 density = float3(exp(-current_height / SCALED_HEIGHTS.xy) * virtual_step_size_p, 0.0);
        float3 density = float3(exp(-current_height / SCALED_HEIGHTS.xy), 0.0);
        
        float denom = (SCALED_HEIGHTS.z - current_height) / SCALED_HEIGHTS.w;
        density.z = (1.0 / (denom * denom + 1.0)) * density.x; //* virtual_step_size_p;
        //density.z = (1.0 / cosh((SCALED_HEIGHTS.z - current_height) / SCALED_HEIGHTS.w));
        //density.z *= density.x * virtual_step_size_p;

        // multiply it by the step size here
        // we are going to use the density later on as well
        density *= virtual_step_size_p;

         // Add these densities to the optical depth, so that we know how many particles are on this ray.
        optical_depth += density;


        // Sample Light Ray
        float3 optical_depth_light = SampleLightRay(primary_sample_pos, max_dist, light_dir, planet_data, SCALED_HEIGHTS, LIGHT_STEP_COUNT);

        /*float3 r = (-BETA_RAYLEIGH * (optical_depth.x + optical_depth_light.x) -
                    BETA_MIE  * (optical_depth.y + optical_depth_light.y) -
                    BETA_ABSORPTION * (optical_depth.z + optical_depth_light.z));*/

        float3 r = (-BETA_RAYLEIGH * (optical_depth.x) -
        BETA_MIE  * (optical_depth.y) -
        BETA_ABSORPTION * (optical_depth.z));

        float3 attenuation = exp(r);

        // accumulate the scattered light
        accumulated_rayleigh += density.x * attenuation;
        accumulated_mie += density.y * attenuation;

        // accumulate the depth
        float weight = actual_step_size_p * (density.x + density.y + density.z);
        weighted_sum += primary_sample_pos * weight;
        accumulated_weight += weight;

        // increment the primary ray
        ray_pos_p += actual_step_size_p;
        //primaryStepPosition += actual_step_size_p;
    }


    //return exp(-(BETA_MIE * optical_depth.y + BETA_RAYLEIGH * optical_depth.x + BETA_ABSORPTION * optical_depth.z));

    float3 opacity = exp( -(BETA_MIE * optical_depth.y + BETA_RAYLEIGH * optical_depth.x + BETA_ABSORPTION * optical_depth.z));

    //if(accumulated_mie.x == 0.0 && accumulated_mie.y == 0.0 && accumulated_mie.z == 0.0)
    //return opacity;

    return accumulated_rayleigh * BETA_RAYLEIGH + scene_color * opacity;


    return (phase_rayleigh * BETA_RAYLEIGH * accumulated_rayleigh + // rayleigh color
            phase_mie * BETA_MIE * accumulated_mie +                // mie
            optical_depth.x * BETA_AMBIENT)                         // ambient
            * light_intensity; //+ scene_color * opacity;              // background
}

/*float2 RaySphereIntersect(float3 start, float3 dir, float radius, float3 sphere_center) 
{
    // float3 start: starting position of the ray
    // float3 dir: ray direction
    // float radius: radius of the planet. 

    // ray-sphere intersection that assumes
    // the sphere is centered at the origin.
    // No intersection when result.x > result.y

    // Translate the ray and sphere to be relative to the sphere's center
    float3 ray_origin_relative_to_center = start - sphere_center;

    // Ray-sphere intersection using quadratic equatrion
    float a = dot(dir, dir);
    float b = 2.0 * dot(dir, ray_origin_relative_to_center);
    float c = dot(ray_origin_relative_to_center, ray_origin_relative_to_center) - (radius * radius);
    float d = (b*b) - 4.0*a*c;

    if (d < 0.0) 
        return float2(100000,-100000);

    // first and second intersection point along the ray's path
    float first_intersection = (-b - sqrt(d))/(2.0*a);
    float second_intersection = (-b + sqrt(d))/(2.0*a);

    return float2( first_intersection, second_intersection);
}*/


/*
To make the planet we're rendering look nicer, we implemented a skylight function here

Essentially it just takes a sample of the atmosphere in the direction of the surface normal
*/

float magnitude(float3 vector_3) 
{
    return sqrt(dot(vector_3, vector_3));
}

/*float3 mix(float3 a, float3 b, float t) 
{
    return a * (1.0 - t) + b * t;
}*/

float4 skylight(float3 sample_pos, float3 surface_normal, float3 light_dir, float3 background_col, PlanetData planet_data, float3 light_intensity, int PRIMARY_STEP_COUNT, int LIGHT_STEP_COUNT, float3 cuttoff_threshold) 
{

    // slightly bend the surface normal towards the light direction
    surface_normal = normalize(mix(surface_normal, light_dir, 0.6));
    
    float accumulated_weight;
    // and sample the atmosphere
    float3 calculate_scattering = CalculateScattering(
    	sample_pos,						// the position of the camera
        surface_normal, 				// the camera vector (ray direction of this pixel)
        10.0 * planet_data.total_radius, 			// max dist, since nothing will stop the ray here, just use some arbitrary value
        background_col,					// scene color, just the background color here
        light_dir,						// light direction
        light_intensity,
        planet_data, accumulated_weight, PRIMARY_STEP_COUNT, LIGHT_STEP_COUNT, cuttoff_threshold);				// steps in the light direction;

    return float4(calculate_scattering, accumulated_weight);
}


/*
The following function returns the scene color and depth 
(the color of the pixel without the atmosphere, and the distance to the surface that is visible on that pixel)

in this case, the function renders a green sphere on the place where the planet should be
color is in .xyz, distance in .w

I won't explain too much about how this works, since that's not the aim of this shader
*/


float4 RenderScene(float3 pos, float3 dir, float3 light_dir, float3 light_intensity, PlanetData planet_data, float3 background, int PRIMARY_STEP_COUNT, int LIGHT_STEP_COUNT, float3 cuttoff_threshold) 
{
    
    // the color to use, w is the scene depth
    float4 color = float4(background, 100000);
    //float4 color = float4(0.0, 0.0, 0.0, 1000000000000.0);
    
    // add a sun, if the angle between the ray direction and the light direction is small enough, color the pixels white
    //color.xyz = float3(dot(dir, light_dir) > 0.9998 ? 3.0 : 0.0);
    
    // get where the ray intersects the planet
    float2 planet_intersect = RaySphereIntersect(pos, dir, planet_data.planet_radius, planet_data.planet_center); 

    // the ray intersects the planet at the world position stored in the buffer
    //float3 planet_intersect = world_position; //RayWorldIntersect(pos,dir, world_position);
    
    // if the ray hit the planet, set the max distance to that ray
    if (0.0 < planet_intersect.y) 
    {

    	color.w = max(planet_intersect.x, 0.0);
        
        // sample position, where the pixel is
        //float3 sample_pos = pos + (dir * planet_intersect.x) - planet_data.planet_center;
        
        // and the surface normal
        //float3 surface_normal = normalize(sample_pos);
        
        // get the color of the sphere
        //color.xyz = float3(0.0, 0.25, 0.05); 
        //color.xyz = background;
        
        // get wether this point is shadowed, + how much light scatters towards the camera according to the lommel-seelinger law
        //float3 N = surface_normal;
        //float3 V = -dir;
        //float3 L = light_dir;
        //float dotNV = max(0.000001, dot(N, V));
        //float dotNL = max(0.000001, dot(N, L));
        //float shadow = dotNL / (dotNL + dotNV);
        
        // apply the shadow
        // JUST COMMENTING THIS OUT TO REMOVE POSSIBLE FACTORS, UNCOMMENT WHEN YOU FIX DEPTH
        //color.xyz *= shadow;
        
        //float4 sky_light = skylight(sample_pos, surface_normal, light_dir, 0, planet_data, light_intensity, PRIMARY_STEP_COUNT, LIGHT_STEP_COUNT, cuttoff_threshold);
        //color.w = max(sky_light.a, 0);
        //color.w = sky_light.a;

        // apply skylight
        //color.xyz += clamp(sky_light.xyz * float3(0.0, 0.25, 0.05), 0.0, 1.0);
        //color.xyz += sky_light.xyz * float3(0.0, 0.25, 0.05);
    }

    /*
    if (magnitude(ground_intersection) > 0.0) 
    {

    	color.w = max(ground_intersection.x, 0.0);
        
        // sample position, where the pixel is
        //float3 sample_pos = pos + (dir * planet_intersect.x) - planet_data.planet_center;
        
        // and the surface normal
        float3 surface_normal = normalize(ground_intersection);
        
        // get the color of the sphere
        color.xyz = float3(0.0, 0.25, 0.05); 
        color.xyz = background;
        
        // get wether this point is shadowed, + how much light scatters towards the camera according to the lommel-seelinger law
        float3 N = surface_normal;
        float3 V = -dir;
        float3 L = light_dir;
        float dotNV = max(0.000001, dot(N, V));
        float dotNL = max(0.000001, dot(N, L));
        float shadow = dotNL / (dotNL + dotNV);
        
        // apply the shadow
        // JUST COMMENTING THIS OUT TO REMOVE POSSIBLE FACTORS, UNCOMMENT WHEN YOU FIX DEPTH
        //color.xyz *= shadow;
        
        float4 sky_light = skylight(ground_intersection, surface_normal, light_dir, 0, planet_data, light_intensity, PRIMARY_STEP_COUNT, LIGHT_STEP_COUNT, cuttoff_threshold);
        //color.w = max(sky_light.a, 0);
        //color.w = sky_light.a;

        // apply skylight
        color.xyz += clamp(sky_light.xyz * float3(0.0, 0.25, 0.05), 0.0, 1.0);
        //color.xyz += sky_light.xyz * float3(0.0, 0.25, 0.05);
    }*/
    
	return float4(color.xyz,color.w);
}


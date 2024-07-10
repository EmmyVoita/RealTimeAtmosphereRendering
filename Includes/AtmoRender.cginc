
#ifndef AtmoRender
#define AtmoRender






// reference planet data
static const float reference_planet_radius = 6371000.0;
static const float reference_atmosphere_radius = 100000.0;
static const float reference_total_radius = reference_planet_radius + reference_atmosphere_radius;
static const float reference_ratio = reference_planet_radius / reference_atmosphere_radius;

// scattering coefficents
static const float3 BETA_RAYLEIGH = float3(0.0000055, 0.000013, 0.0000224);                   /* rayleigh, affects the color of the sky */
static const float  BETA_MIE =  0.000021;                                                      /* mie, affects the color of the blob around the sun */
static const float3 BETA_ABSORPTION  = float3(0.0000204, 0.0000497, 0.00000195);               /* what color gets absorbed by the atmosphere (Due to things like ozone) */
static const float3 BETA_AMBIENT = float3(0.000000,0.0000000,0.0000000);                      // the amount of scattering that always occurs, cna help make the back side of the atmosphere a bit brighter
static const float g = 0.70;                                                                  /* mie scattering direction, or how big the blob around the sun is */

// scattering coefficents
// rayliehg is wavelength dependent affects r g b differently, mie is wavelength indpenedent ( effect all colors equally)
static const float3 SIGMA_RAYLEIGH_S = float3(0.000005802, 0.000013558, 0.0000331);
//static const float3 SIGMA_RAYLEIGH_S = float3(0.0000055, 0.000013, 0.0000224);  
static const float  SIGMA_MIE_S = 0.000003996;
static const float  SIGMA_OZONE_S = 0;

// Absorption coefficents
static const float  SIGMA_RAYLEIGH_A = 0;
static const float  SIGMA_MIE_A = 0.0000044;
static const float3 SIGMA_OZONE_A = float3(0.00000065, 0.000001881, 0.000000085); 
//static const float3 SIGMA_OZONE_A = float3(0.0000204, 0.0000497, 0.00000195);
// and the heights (how far to go up before the scattering has no effect)
static const float HEIGHT_RAY = 8000.0;                                                       /* rayleigh height */
static const float HEIGHT_MIE = 1200.0;                                                       /* mie height */
static const float HEIGHT_ABSORPTION = 30000;                                                 /* at what height the absorption is at it's maximum */
static const float ABSORPTION_FALLOFF = 4000;                                                 /* how much the absorption decreases the further away it gets from the maximum height */





// Struct Decloration
//-----------------------------------------------------------------------------------------------------------------
struct DirectionalLight
{
    float3 direction;
    float intensity;
    float4x4 light_VP;
};

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

//DirectionalLight CreateDirectionalLight(float3 dir, float inten, float4x4 Light_VP)
DirectionalLight CreateDirectionalLight(float3 dir, float inten, float4x4 Light_VP)
{
    DirectionalLight dl;
    float3 normalized_light_dir = normalize(dir);
    dl.direction = normalized_light_dir;
    dl.intensity = inten;
    dl.light_VP = Light_VP;
    return dl;
}

PlanetData CreatePlanetData(float3 planet_center, float planet_radius, float atmosphere_radius)
{
    // Atmosphere_radius: total radius of the planet + atmosphere
    // If we assume that the ground is at 0, then the planet radius us the 


    PlanetData planet_data;
    planet_data.planet_center = planet_center;
    planet_data.planet_radius = -planet_center.y;
    planet_data.atmosphere_radius = atmosphere_radius + planet_center.y;
    planet_data.total_radius = atmosphere_radius;

    planet_data.scale_ratio = -planet_center.y / planet_data.atmosphere_radius;
    planet_data.planet_scale = reference_planet_radius / -planet_center.y;
    planet_data.atmosphere_scale = planet_data.scale_ratio / reference_ratio;
    return planet_data;
}

// Helper functions
//-----------------------------------------------------------------------------------------------------------------

float Magnitude(float3 vector_3) 
{
    return sqrt(dot(vector_3, vector_3));
}

float3 mix(float3 a, float3 b, float t) 
{
    return a * (1.0 - t) + b * t;
}


bool RayIntersectsPoint(float3 ray_start, float3 ray_dir, float3 world_pos)
{
    // Calculate the vector from the ray start to the world position.
    float3 rayToPosition = world_pos - ray_start;

    // Calculate the distance along the ray direction to the intersection point.
    float t = dot(rayToPosition, ray_dir);

    // Check if the intersection point is in front of the ray (t >= 0).
    if (t >= 0.0)
    {
        // Calculate the point on the ray closest to the world position.
        float3 closestPointOnRay = ray_start + t * ray_dir;

        // Check if the closest point on the ray is very close to the world position.
        // You may want to adjust this epsilon value based on your needs.
        float epsilon = 0.001; // A small value to account for floating-point errors.
        return length(closestPointOnRay -  world_pos) < epsilon;
    }

    // If t < 0, the intersection point is behind the ray.
    return false;
}

float RayIntersectSphereLight(float3 rayStart, float3 light_dir, float atmo_radius, int LIGHT_STEP_COUNT) 
{
    // Calculate the step size of the light ray.
    // again with a ray sphere intersect
    // a, b, c and d are already defined
    float a = dot(light_dir, light_dir);
    float b = 2.0 * dot(light_dir, rayStart);
    float c = dot(rayStart, rayStart) - (atmo_radius * atmo_radius);
    float d = (b * b) - 4.0 * a * c;

    // no early stopping, this one should always be inside the atmosphere
    // calculate the ray length
    float step_size_l = (-b + sqrt(d)) / (2.0 * a * float(LIGHT_STEP_COUNT));
    return step_size_l;
}

float2 RaySphereIntersect(float3 start, float3 dir, float radius, float3 sphere_center) 
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
}

float RayPlaneIntersect(float3 start, float3 dir, float3 planeNormal, float3 planet_center)
{
    float3 planePoint = float3(0.0, -planet_center.y, 0.0); // Centered at 0, choose any point on the plane

    float t = -dot(planeNormal, (start - planePoint)) / dot(planeNormal, dir);

    return t;
}



#endif




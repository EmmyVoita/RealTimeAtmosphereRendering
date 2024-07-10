
struct Ray
{
    float3 origin; 
    float3 position;
    float3 direction;
};

struct RayHit
{
    float3 position;
    float distance;
    float3 normal;
};

RayHit CreateRayHit()
{
    RayHit hit;
    hit.position = float3(0.0f, 0.0f, 0.0f);
    hit.distance = 1.#INF;
    hit.normal = float3(0.0f, 0.0f, 0.0f);
    return hit;
}

Ray CreateRay(float3 position, float3 direction)
{
    Ray ray;
    ray.position = position;
    ray.origin = position;
    ray.direction = direction;
    return ray;
}

Ray CreateCameraRay(float2 uv, float4x4 unity_CameraToWorld, float4x4 _CameraInverseProjection)
{
    // Transform the camera origin to world space
    float3 position = mul(unity_CameraToWorld, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;
    
    // Invert the perspective projection of the view-space position
    float3 direction = mul(_CameraInverseProjection, float4(uv, 0.0f, 1.0f)).xyz;

    // Transform the direction from camera to world space and normalize
    direction = mul(unity_CameraToWorld, float4(direction, 0.0f)).xyz;
    direction = normalize(direction);
    return CreateRay(position, direction);
}


void IntersectSphere(Ray ray, inout RayHit bestHit, float4 sphere)
{
    // Calculate distance along the ray where the sphere is intersected
    float3 d = ray.position - sphere.xyz;
    float p1 = -dot(ray.direction, d);
    float p2sqr = p1 * p1 - dot(d, d) + sphere.w * sphere.w;
    if (p2sqr < 0)
        return;
    float p2 = sqrt(p2sqr);
    float t = p1 - p2 > 0 ? p1 - p2 : p1 + p2;
    if (t > 0 && t < bestHit.distance)
    {
        bestHit.distance = t;
        bestHit.position = ray.position + t * ray.direction;
        bestHit.normal = normalize(bestHit.position - sphere.xyz);
    }
}


RayHit Trace(Ray ray, float atmosphere_radius, float3 planet_center)
{
    RayHit bestHit = CreateRayHit();
    IntersectSphere(ray, bestHit, float4(planet_center, atmosphere_radius));
    return bestHit;
}
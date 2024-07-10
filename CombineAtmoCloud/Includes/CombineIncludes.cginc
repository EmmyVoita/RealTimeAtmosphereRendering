float remap(float v, float minOld, float maxOld, float minNew, float maxNew)
{
    return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float remap(float v, float s, float e) {
	return (v - s) / (e - s);
}

float remap01(float x, float a, float b)
{
	return ((x - a) / (b - a));   
}

float linearstep0( const float e, float v ) {
    return min( v*(1./e), 1. );
}

float beer(float d)
{
    float beer = exp(-d);
    return beer;
}

float hg(float g, float costh)
{
    return (1.0 / (4.0 * 3.1415)) * ((1.0 - g * g) / pow(max(0, 1.0 + g * g - 2.0 * g * costh), 1.5));
}

// https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
float3 ACESFilm(float3 x)
{
    return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

// Exponential Integral
// (http://en.wikipedia.org/wiki/Exponential_integral)
float Ei(float z)
{
    return 0.5772156649015328606065 + log(1e-4 + abs(z)) + z * (1.0 + z * (0.25 + z * ((1.0 / 18.0) + z * ((1.0 / 96.0) + z *
    (1.0 / 600.0))))); // For x!=0
}



float2 squareUV(float2 uv, float2 ScreenParams)
{
    float width = ScreenParams.x;
    float height = ScreenParams.y;
    //float minDim = min(width, height);
    float scale = 1000;
    float x = uv.x * width;
    float y = uv.y * height;
    return float2(x / scale, y / scale);
}

// Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir, out bool intersects)
{
    // Adapted from: http://jcgt.org/published/0007/03/04/
    float3 t0 = (boundsMin - rayOrigin) * invRaydir;
    float3 t1 = (boundsMax - rayOrigin) * invRaydir;
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    
    float dstA = max(max(tmin.x, tmin.y), tmin.z);
    float dstB = min(tmax.x, min(tmax.y, tmax.z));

    // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
    // dstA is dst to nearest intersection, dstB dst to far intersection

    // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
    // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

    // CASE 3: ray misses box (dstA > dstB)

    float dstToBox = max(0, dstA);
    float dstInsideBox = max(0, dstB - dstToBox);

    if(dstInsideBox != 0)
    {
        intersects = true;
    }
    else
    {
        intersects = false;
    }
    
    return float2(dstToBox, dstInsideBox);
}




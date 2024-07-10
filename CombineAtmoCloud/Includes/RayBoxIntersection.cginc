#include "Assets/CombineAtmoCloud/Shaders/CombineCompute/CombineComputeSettings/ContainerSettings.compute"

#ifndef RAYBOX_INTERSECT_COMPUTE
#define RAYBOX_INTERSECT_COMPUTE
    
  

    // https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
    // Compute the near and far intersections using the slab method.
    // No intersection if tNear > tFar.
    float2 IntersectAABB(float1x3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
    {
        float3 tMin = (boxMin - rayOrigin) / rayDir;
        float3 tMax = (boxMax - rayOrigin) / rayDir;
        float3 t1 = min(tMin, tMax);
        float3 t2 = max(tMin, tMax);
        float tNear = max(max(t1.x, t1.y), t1.z);
        float tFar = min(min(t2.x, t2.y), t2.z);
        return float2(tNear, tFar);
    }

    bool InsideAABB(float3 p)
    {
        float eps = 1e-4;
        return (p.x > boundsMin.x - eps) && (p.y > boundsMin.y - eps) && (p.z > boundsMin.z - eps) &&
			    (p.x < boundsMax.x + eps) && (p.y < boundsMax.y + eps) && (p.z < boundsMax.z + eps);
    }

    bool GetCloudIntersection(float3 org, float3 dir, out float distToStart, out float totalDistance)
    {
        float2 intersections = IntersectAABB(org, dir, boundsMin, boundsMax);
	
        if (InsideAABB(org))
        {
            intersections.x = 1e-4;
        }
    
        distToStart = intersections.x;
        totalDistance = intersections.y - intersections.x;
        return intersections.x > 0.0 && (intersections.x < intersections.y);
    }


    // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
    /*float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir, out bool intersects)
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
        intersects = dstInsideBox != 0;
        return float2(dstToBox, dstInsideBox);
    }*/



#endif
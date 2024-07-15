

#ifndef ATMO_MATH_INCLUDES
#define ATMO_MATH_INCLUDES

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


 
#endif


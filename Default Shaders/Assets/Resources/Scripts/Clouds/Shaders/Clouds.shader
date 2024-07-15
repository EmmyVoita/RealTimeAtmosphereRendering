
Shader "Hidden/Clouds"
{
    
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define DEBUG_MODE 1
            #define TEST 0
            #define DRAWDEPTH 0

            // vertex input: position, UV
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };
            
            v2f vert (appdata v) {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                // Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
                // (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }

            // Textures
            Texture3D<float4> NoiseTex;
            Texture3D<float4> DetailNoiseTex;
            Texture2D<float4> WeatherMap;
            Texture2D<float4> BlueNoise;
            Texture2D<float4> CloudCoverage;
            Texture2D<float4> HeightGradient;
            Texture2D<float4> DensityGradient;
            Texture2D<float4> CurlNoiseTex;
            
            SamplerState samplerNoiseTex;
            SamplerState samplerDetailNoiseTex;
            SamplerState samplerWeatherMap;
            SamplerState samplerBlueNoise;
            SamplerState samplerCloudCoverage;
            SamplerState samplerHeightGradient;
            SamplerState samplerDensityGradient;
            SamplerState samplerCurlNoiseTex;

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            // Shape settings
            float4 params;
            int3 mapSize;
            float densityMultiplier;
            float densityOffset;
            float scale;
            float detailNoiseScale;
            float detailNoiseWeight;
            float3 curl_noise_weights;
            float3 detailWeights;
            float4 shapeNoiseWeights;
            float4 phaseParams;

            float density_gradient_scalar;

            //Cloud Coverage:
            float cloud_coverage_texture_offset = 0;
            float cloud_coverage_texture_step;
            float2 coverage_tiling;
            float altitude_gradient_power_1;
            float altitude_gradient_power_2;
            float low_altitude_multiplier_influence;

            // March settings
            int numStepsLight;
            float ray_march_step_size;
            float rayOffsetStrength;

            float3 boundsMin;
            float3 boundsMax;

            float3 shapeOffset;
            float3 detailOffset;

            // Light settings
            float powder_factor;
            float lightAbsorptionTowardSun;
            float lightAbsorptionThroughCloud;
            float darknessThreshold;
            float4 _LightColor0;
            float4 IsotropicLightTop;
            float4 IsotropicLightBottom;
            float  extinction_factor;

            // Animation settings
            float timeScale;
            float baseSpeed;
            float detailSpeed;

            // Debug settings:
            int debugViewMode; // 0 = off; 1 = shape tex; 2 = detail tex; 3 = weathermap
            int debugGreyscale;
            int debugShowAllChannels;
            float debugNoiseSliceDepth;
            float4 debugChannelWeight;
            float debugTileAmount;
            float viewerSize;
            
            float remap(float v, float minOld, float maxOld, float minNew, float maxNew) {
                return minNew + (v-minOld) * (maxNew - minNew) / (maxOld-minOld);
            }

            float2 squareUV(float2 uv) {
                float width = _ScreenParams.x;
                float height =_ScreenParams.y;
                //float minDim = min(width, height);
                float scale = 1000;
                float x = uv.x * width;
                float y = uv.y * height;
                return float2 (x/scale, y/scale);
            }

            // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) 
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
                return float2(dstToBox, dstInsideBox);
            }

            // Henyey-Greenstein
            float hg(float a, float g) {
                float g2 = g*g;
                return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
            }

            float phase(float a) 
            {
                //dual-lob Henyey-Greenstein
                float blend = .5;
                float hgBlend = hg(a,phaseParams.x) * (1-blend) + hg(a,-phaseParams.y) * blend;
                return phaseParams.z + hgBlend*phaseParams.w;
            }

            float beer(float d) {
                float beer = exp(-d);
                return beer;
            }

            float remap01(float v, float low, float high) 
            {
                return (v-low)/(high-low);
            }


            float sampleDensity(float3 rayPos) 
            {
                // Constants:
                const int mipLevel = 0;
                const float baseScale = 1/1000.0;
                const float offsetSpeed = 1/100.0;

                // Calculate texture sample positions
                float time = _Time.x * timeScale;
                float3 size = boundsMax - boundsMin;
                //size = remap(size,boundsMin, boundsMax, 0, 1);
                float3 boundsCentre = (boundsMin+boundsMax) * .5;
                float3 uvw = (size * .5 + rayPos) * baseScale * scale;
                float3 shapeSamplePos = uvw + shapeOffset * offsetSpeed + float3(time,time*0.1,time*0.2) * baseSpeed;

                // Gradient UV's
                float heightscale = size.y / 10000;//size.x;
                float3 gradient_uvw = (rayPos - boundsMin) * baseScale * 0.1;
                gradient_uvw.y /= heightscale;



                // Get the Height Gradient From the Height Gradient Texture:
                float height_gradient = HeightGradient.SampleLevel(samplerHeightGradient, gradient_uvw.yx, 0);

                //Get the Density Gradient From the Density Gradient Texture:
                float density_gradient = DensityGradient.SampleLevel(samplerDensityGradient, gradient_uvw.yx, 0);

                
                // Get the Cloud Coverage From the Coverage Texture:
                float2 cloud_coverage_uv = float2(gradient_uvw.x * coverage_tiling.x, gradient_uvw.z * coverage_tiling.y);

                //Add extra cloud coverage for low altitude sampling:
                float step_altitude = pow((1-gradient_uvw.y), altitude_gradient_power_2 * 0.01);
                float low_altitude_multiplier = saturate( pow((1-density_gradient), altitude_gradient_power_1) * height_gradient * step_altitude);
                float coverage_offset = cloud_coverage_texture_offset - low_altitude_multiplier * low_altitude_multiplier_influence; 

                float cloud_coverage = CloudCoverage.SampleLevel(samplerCloudCoverage, cloud_coverage_uv, 0);
                cloud_coverage += coverage_offset; //1-step(cloud_coverage_texture_step, cloud_coverage) + coverage_offset; 
                

                // Modify the Height Gradient using falloff at along x/z edges of the cloud container
                const float containerEdgeFadeDst = 50;
                float dstFromEdgeX = min(containerEdgeFadeDst, min(rayPos.x - boundsMin.x, boundsMax.x - rayPos.x));
                float dstFromEdgeZ = min(containerEdgeFadeDst, min(rayPos.z - boundsMin.z, boundsMax.z - rayPos.z));
                float edgeWeight = min(dstFromEdgeZ,dstFromEdgeX)/containerEdgeFadeDst;
                
                float gMin = .2;
                float gMax = .7;
                float heightPercent = (rayPos.y - boundsMin.y) / size.y;
                float heightGradient = saturate(remap(heightPercent, 0.0, gMin, 0, 1)) * saturate(remap(heightPercent, 1, gMax, 0, 1));
                height_gradient *= edgeWeight;


                //First, we build a basic cloud shape by sampling our first 3dTexture:
                float4 base_shape_noise = NoiseTex.SampleLevel(samplerNoiseTex, shapeSamplePos, mipLevel);
                float shape_FBM = base_shape_noise.g * .625 + base_shape_noise.b * .125 + base_shape_noise.a * .25; 
                float base_cloud_density = remap(base_shape_noise.r, shape_FBM - 1., 1., 0., 1.);
                base_cloud_density = saturate(base_cloud_density + densityOffset * .1);
                base_cloud_density = remap(base_cloud_density, .85, 1., 0., 1.);
                
                base_cloud_density *= 1-cloud_coverage;

                //The next step is to multiply the result by the coverage and reduce density at the bottoms of the clouds:
                //This ensures that the bottoms will be whispy and it increases the presence of clouds in a more natural way. 
                //Remember that density increases over altitude. Now that we have our base cloud shape, we add details.

                
                // cloud shape modeled after the GPU Pro 7 chapter
                float cloud_with_coverage  = remap(base_cloud_density * height_gradient, cloud_coverage, 1.0, 0.0, 1.0);



                //cloud_with_coverage *= cloud_coverage;
                // Calculate Cloud Density with Coverage:
                //float cloud_with_coverage = remap(base_shape_density * height_gradient * density_gradient,  cloud_coverage, 1.0, 0.0, 1.0);
                //float cloud_with_coverage = remap(height_gradient * density_gradient,  cloud_coverage, 1.0, 0.0, 1.0);
                //cloud_with_coverage *= base_shape_density;
                //float oneMinusShape = 1 - shape_FBM;
                //float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
                //float cloudDensity = cloud_with_coverage - (1-detailFBM) * detailErodeWeight * detailNoiseWeight;
                //cloud_with_coverage *= base_shape_density;

                //return cloud_with_coverage;
    
                
                // Save sampling from detail tex if shape density <= 0
                if (cloud_with_coverage > 0) 
                {
                    // Sample detail noise
                    float3 detailSamplePos = uvw*detailNoiseScale + detailOffset * offsetSpeed + float3(time*.4,-time,time*0.1)*detailSpeed;
                    float4 detailNoise = DetailNoiseTex.SampleLevel(samplerDetailNoiseTex, detailSamplePos, mipLevel);
                    // Sample the curl noise:
                    float4 curlNoise = CurlNoiseTex.SampleLevel(samplerCurlNoiseTex, detailSamplePos, mipLevel);
                    //curlNoise *= float4(curl_noise_weights, 1.0);
                    //Combine the detail and curl noise:
                    detailNoise *= curlNoise;
                    //detailNoise *= 1;
                    
                    float3 normalizedDetailWeights = detailWeights / dot(detailWeights, 1);
                    float detailFBM = dot(detailNoise, normalizedDetailWeights);


                    // Subtract detail noise from base shape (weighted by inverse density so that edges get eroded more than centre)
                    float oneMinusShape = 1 - shape_FBM;
                    float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
                    float cloudDensity = cloud_with_coverage - (1-detailFBM) * detailErodeWeight * detailNoiseWeight;
    
                    return cloudDensity * (density_gradient * densityMultiplier * 0.1);
                }


                return 0;
            }

            // Calculate proportion of light that reaches the given point from the lightsource
            float lightmarch(float3 position) 
            {
                float3 directionToLight = _WorldSpaceLightPos0.xyz;
                float distanceInsideBox = rayBoxDst(boundsMin, boundsMax, position, 1/directionToLight).y;
                
                float stepSize = distanceInsideBox/numStepsLight;
                float totalDensity = 0;

                for (int step = 0; step < numStepsLight; step++) 
                {
                    position += directionToLight * stepSize;
                    totalDensity += max(0, sampleDensity(position) * stepSize);
                }

                return totalDensity;
            }

            // Exponential Integral
            // (http://en.wikipedia.org/wiki/Exponential_integral)
            float Ei( float z )
            {
                return 0.5772156649015328606065 + log( 1e-4 + abs(z) ) + z * (1.0 + z * (0.25 + z * ( (1.0/18.0) + z * ( (1.0/96.0) + z *
                (1.0/600.0) ) ) ) ); // For x!=0
            }

            float3 ComputeAmbientColor ( float3 _Position, float _ExtinctionCoeff )
            {
                float Hp = boundsMax.y - _Position.y; // Height to the top of the volume
                float a = -_ExtinctionCoeff * Hp;
                float3 IsotropicScatteringTop = IsotropicLightTop * max( 0.0, exp( a ) - a * Ei( a ));
                float Hb = _Position.y - boundsMin.y; // Height to the bottom of the volume
                a = -_ExtinctionCoeff * Hb;
                float3 IsotropicScatteringBottom = IsotropicLightBottom * max( 0.0, exp( a ) - a * Ei( a ));
                return IsotropicScatteringTop + IsotropicScatteringBottom;
            }


            float4 debugDrawNoise(float2 uv) {

                float4 channels = 0;
                float3 samplePos = float3(uv.x,uv.y, debugNoiseSliceDepth);

                if (debugViewMode == 1) {
                    channels = NoiseTex.SampleLevel(samplerNoiseTex, samplePos, 0);
                }
                else if (debugViewMode == 2) {
                    channels = DetailNoiseTex.SampleLevel(samplerDetailNoiseTex, samplePos, 0);
                }
                else if (debugViewMode == 3) {
                    channels = WeatherMap.SampleLevel(samplerWeatherMap, samplePos.xy, 0);
                }

                if (debugShowAllChannels) {
                    return channels;
                }
                else {
                    float4 maskedChannels = (channels*debugChannelWeight);
                    if (debugGreyscale || debugChannelWeight.w == 1) {
                        return dot(maskedChannels,1);
                    }
                    else {
                        return maskedChannels;
                    }
                }
            }

            
            //http://magnuswrenninge.com/wp-content/uploads/2010/03/Wrenninge-OzTheGreatAndVolumetric.pdf

            /* The main idea is to artificially lower the extinction
            coefficient σt along the shadow ray to let more light reach the
            shaded point. But rather than use a fixed scaling factor, we use
            a summation over several scales. We also adjust the local phase
            function eccentricity g and local scattering coefficient σs such
            that the total contribution of light at a given point is:*/

            float MultipleOctaveScattering(float density)
            {
                float EXTINCTION_MULT = 1.0;
                float attenuation = 0.2;
                float contribution = 0.4;
                float phaseAttenuation = 0.1;

                const float scatteringOctaves = 4.0;

                float a = 2.0;
                float b = 2.0;
                float c = 2.0;
                float g = 0.85;

                float luminance = 0.0;

                for(float i = 0.0; i < scatteringOctaves; i++)
                {
                    float phaseFunction = phase(0.3 * c);
                    float beers = exp(-density * EXTINCTION_MULT * a);

                    luminance += b * phaseFunction * beers;

                    a *= attenuation;
                    b *= contribution;
                    c *= (1.0 - phaseAttenuation);
                }
                return luminance;
            }

            // Z buffer to linear depth
            inline float LinearEyeDepth2( float z )
            {
                // _ZBufferParams.z = (1-far/near) / far = -9.9999
                // _ZBufferParams.w = (far / near) / far = 10
                //return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);

                // x is (1-far/near), y is (far/near), z is (x/far) and w is (y/far).

                // https://forum.unity.com/threads/solved-what-is-lineareyedepth-doing-exactly.539791/
                //float cameraDepth = tex2D(_CameraDepthTexture, screenPos).r;
                //float eyeDepth = far * near / ((near - far) * cameraDepth + far);
                //return 10000 * 0.1 / ((0.1 - 10000) * z + 10000); 

                return 1.0 / (9.9999 * z - 10.0);

                return 1.0 / z;
            }

          
            float4 frag (v2f i) : SV_Target
            {
                #if DEBUG_MODE == 1
                if (debugViewMode != 0) 
                {
                    float width = _ScreenParams.x;
                    float height =_ScreenParams.y;
                    float minDim = min(width, height);
                    float x = i.uv.x * width;
                    float y = (1-i.uv.y) * height;

                    if (x < minDim*viewerSize && y < minDim*viewerSize) 
                    {
                        return debugDrawNoise(float2(x/(minDim*viewerSize)*debugTileAmount, y/(minDim*viewerSize)*debugTileAmount));
                    }
                }
                #endif

                
                // Create ray
                float3 rayPos = _WorldSpaceCameraPos;
                float viewLength = length(i.viewVector);
                float3 rayDir = i.viewVector / viewLength;

                
                // Depth and cloud container intersection info:
                float nonlin_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonlin_depth) * viewLength;
                float2 rayToContainerInfo = rayBoxDst(boundsMin, boundsMax, rayPos, 1/rayDir);


                float dstToBox = rayToContainerInfo.x;
                float dstInsideBox = rayToContainerInfo.y;

                // point of intersection with the cloud container
                float3 entryPoint = rayPos + rayDir * dstToBox;

                // random starting offset (makes low-res results noisy rather than jagged/glitchy, which is nicer)
                float randomOffset = BlueNoise.SampleLevel(samplerBlueNoise, squareUV(i.uv*3), 0);
                randomOffset *= rayOffsetStrength * 10;
                
                // Phase function makes clouds brighter around sun
                float cosAngle = dot(rayDir, _WorldSpaceLightPos0.xyz);
                float phaseVal = phase(cosAngle);

                float dstTravelled = randomOffset;
                float dstLimit = min(depth-dstToBox, dstInsideBox);
                
                
                float stepSize = ray_march_step_size;

                // March through volume:
                float view_ray_transmittance = 1;
                float3 combined_transmittance = 0;
                float3 previousRayPos = entryPoint + rayDir * dstTravelled;
                

                while (dstTravelled < dstLimit) 
                {
                    rayPos = entryPoint + rayDir * dstTravelled;
                    float density = sampleDensity(rayPos);

                    // Sample at 2x step size until a cloud is hit (density > 0). If the density is greater than 0, 
                    // then go back to the previous step and start stepping at 1x step size. If the next sample density is 0 (exit cloud),
                    // then go back to sampling at 2x step size. 
                    
                    if (density > 0 && stepSize == ray_march_step_size) 
                    {
                        // Go back to the previous sample position
                        rayPos = previousRayPos;
                        stepSize = ray_march_step_size;
                    }
                    else if(density > 0 && stepSize == 2*ray_march_step_size)
                    {
                        // Light energy at a given sample point in the cloud as a function of
                        // Energy = Attenuation * Phase * InScattering
                        // Energy = exp(-density along light ray) * HG(cos(theta, eccentricity) * 1- exp(-density_sample))
                        
                        
                        // The powder effect should be dependent of the view direction and sun direction:
                        float view_dot_light = remap(dot(normalize(_WorldSpaceCameraPos), normalize(_WorldSpaceLightPos0.xyz)),-1,1,0,1);

                        // Calculate how much light reaches the current sample point from the light source (Beer):
                        float light_density = lightmarch(rayPos);
                        //float light_transmittance_beer = exp(-light_density * lightAbsorptionTowardSun);
                        float light_transmittance_beer = MultipleOctaveScattering(light_density * lightAbsorptionTowardSun);
                        light_transmittance_beer = darknessThreshold + light_transmittance_beer * (1-darknessThreshold);

                        // Apply the equation again for the path along the ray to the viewer (Beer-Powder):

                        //float view_ray_transmittance_beer = exp( -density * stepSize * lightAbsorptionThroughCloud);
                        float view_ray_transmittance_beer = MultipleOctaveScattering(density * stepSize * lightAbsorptionThroughCloud);
                        //float view_ray_transmittance_powder = 1.0 - exp( -density * stepSize * 2) * view_dot_light * powder_factor * .01;
                        float view_ray_transmittance_powder = 1.0 - MultipleOctaveScattering(density * stepSize * 2) * view_dot_light * powder_factor * .01;

                        float view_ray_transmittance_beer_powder = view_ray_transmittance_beer * view_ray_transmittance_powder;
                        view_ray_transmittance_beer_powder = darknessThreshold + view_ray_transmittance_beer_powder * (1-darknessThreshold);


                        view_ray_transmittance *= view_ray_transmittance_beer_powder;

                        float extinction_coefficent = extinction_factor * density;
                        float3 ambient_color = ComputeAmbientColor(rayPos, extinction_coefficent);

                        
                        // Combined transmittance:
                        combined_transmittance += (density * stepSize) * view_ray_transmittance * light_transmittance_beer * phaseVal * ambient_color;

                       
                        // Exit early if T is close to zero as further samples won't affect the result much:
                        if (view_ray_transmittance < 0.01) 
                        {
                            break;
                        }

                    }
                    else
                    {
                        stepSize = ray_march_step_size * 2;
                    }

                    // Update the previous sample position
                    dstTravelled += stepSize;
                    previousRayPos = rayPos;
                }



                // Add clouds to background
                float3 backgroundCol = tex2D(_MainTex,i.uv);
                float3 cloudCol = combined_transmittance * _LightColor0;
                float3 col = backgroundCol * view_ray_transmittance + cloudCol;
                return float4(col,0);
                //return float4(1,1,1,1) * dstLimit;

            }

            ENDCG
        }
    }
}
// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable

// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable

// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable
// Upgrade NOTE: replaced '_CameraToWorld' with 'unity_CameraToWorld'

Shader "Custom/TemporalReprojection"
{
    Properties
    {
        _SubFrame ("Texture", 2D) = "white" {}
        _PrevFrame ("Texture", 2D) = "white" {}
        //_Resolution ("Resolution", Vector) = (1, 1)
       // _FrameJitter ("Frame Jitter", Vector) = (0, 0)
    }
    SubShader
    {
      

        Pass
        {
            Tags { "RenderType"="Opaque" }
            LOD 200
            Cull Off
            ZWrite Off
            Ztest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Assets/Resources/CombineAtmoCloud/Includes/RayMarch.cginc"

            static const float kernel[9] =
            {
                .0625, .125, .0625,
                .125,  .25,  .125,
                .0625, .125, .0625  
            };

            // Define the offsets
            static const int2 offsets[8] = {
                int2(-1, -1), int2(-1, 1),
                int2(1, -1), int2(1, 1),
                int2(1, 0), int2(0, -1),
                int2(0, 1), int2(-1, 0)
            };


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            //Texture2D _PrevFrameColor;
            sampler2D _SubFrame;
            sampler2D _PrevFrame;

            // Declare the properties here
            float2 _Resolution;
            float2 _FrameJitter;

            
            

            float4x4 _1CameraToWorld;
            float4x4 _CameraInverseProjection;
            float4x4 _PrevVP_NoFlip;
            float nearPlane;
            float farPlane;

            float4x4 _InverseProjection;
            float4x4 _InverseRotation;
            float4x4 _PreviousRotation;
            float4x4 _Projection;

            float _SubPixelSize;
            float _SubFrameNumber;
            float2 _SubFrameSize;
            float2 _FrameSize;


            v2f vert (appdata_img v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = TRANSFORM_TEX(v.uv, _SubFrame);
                o.uv = v.texcoord;
                return o;
            }

            float JitterCorrection(float2 uv)
            {
                float2 localIndex = floor(fmod(uv * _Resolution, 4.0f));
                localIndex = abs(localIndex - _FrameJitter);

                return saturate(localIndex.x + localIndex.y);
            }

            float4 GaussianBlur(int2 uv)
            {
                float4 col = float4(0., 0., 0., 0.);

                int2 offsets[9] = { 
                    int2(-1, -1), int2(-1, 0), int2(-1, 1),
                    int2(0, -1), int2(0, 0), int2(0, 1),
                    int2(1, -1), int2(1, 0), int2(1, 1)
                };

                

                for (int i = 0; i < 9; i++)
                {
                    float2 offset = offsets[i] / _Resolution;
                    col += float4(tex2D(_SubFrame, uv + offset).rgb,1.) * kernel[i];
                }

                return col;
            }

            float2 GetReprojectionUV(float3 worldPosition, float width, float height)
            {
                // Determine the ss_uv for the world position in the previous frame using the previous VP matrix, this assumes the object doesnt move:
                float4 reproject_cs_pos = mul(_PrevVP_NoFlip, float4(worldPosition, 1.0));
                float2 reproject_ss_ndc = reproject_cs_pos.xy / reproject_cs_pos.w;
                float2 reproject_ss_uv = 0.5 * reproject_ss_ndc + 0.5;

                // Convert uv to ss_id to use for thread_id:
                float2 reproject_thread_id = reproject_ss_uv * float2(width, height);
                return reproject_thread_id;
            }

            float DenormalizeDepth(float normalizedDepth)
            {
                return normalizedDepth * (farPlane - nearPlane) + nearPlane;
            }

            float3 DenormalizeDepthToWorldPosition(float2 uv, float normalizedDepth)
            {
                Ray ray = CreateCameraRay(uv, _1CameraToWorld, _CameraInverseProjection);
                float3 worldPos = ray.origin + ray.direction * DenormalizeDepth(normalizedDepth);
                return worldPos;
            }

            bool IsDepthSimilar(float currentDepth, float historyDepth, float threshold) {
                return abs(currentDepth - historyDepth) < threshold;
            }

            float SobelEdgeDetectionDepth(int2 uv, float2 resolution) {
                float Gx[3][3] = {
                    {-1, 0, 1},
                    {-2, 0, 2},
                    {-1, 0, 1}
                };
            
                float Gy[3][3] = {
                    {-1, -2, -1},
                    { 0,  0,  0},
                    { 1,  2,  1}
                };
            
                float edgeX = 0.0;
                float edgeY = 0.0;
            
                for (int i = -1; i <= 1; ++i) {
                    for (int j = -1; j <= 1; ++j) {
                        float depth = tex2D(_SubFrame,uv + int2(i, j)/resolution).a;
                        edgeX += Gx[i + 1][j + 1] * depth;
                        edgeY += Gy[i + 1][j + 1] * depth;
                    }
                }
            
                return sqrt(edgeX * edgeX + edgeY * edgeY);
            }
            

            float4 clip_aabb(float3 aabb_min, float3 aabb_max, float4 p, float4 q)
            {
                float3 p_clip = 0.5 * (aabb_max + aabb_min);
                float3 e_clip = 0.5 * (aabb_max - aabb_min);
                float4 v_clip = q - float4(p_clip, p.w);
                float3 v_unit = v_clip.xyz / e_clip;
                float3 a_unit = abs(v_unit);
                float ma_unit = max(max(a_unit.x, a_unit.y), a_unit.z);
                
                if (ma_unit > 1.0)
                    return float4(p_clip, p.w) + v_clip / ma_unit;
                else
                    return q; // point inside AABB
            }

            float4 Calctxaa(float2 hist_uv, float2 id, float4 currentBuffer, float2 resolution) {
                // Get the color from the history buffer
                //float4 historyBuffer = float4(tex2D(_PrevFrame, hist_uv).rgb, 1.);
                float4 historyBuffer = float4(tex2D(_PrevFrame, hist_uv).rgba);

                float currentDepth = currentBuffer.a;
                float historyDepth = tex2D(_PrevFrame, hist_uv).a;
            
                currentBuffer.a = 1.;
            
                // Depth rejection
                if (!IsDepthSimilar(currentDepth, historyDepth, 0.01)) {
                    historyBuffer = currentBuffer;
                }
            
            
                // Initialize average and variance with the current color
                float4 colorAvg = currentBuffer;
                float4 colorVar = currentBuffer * currentBuffer;

        
                // Compute neighborhood average and variance
                for (int i = 0; i < 8; i++) {

                    float2 uv = id.xy + offsets[i]/resolution;
                    uv = clamp(uv, 0.0, 1.0); // Clamp UV coordinates to [0, 1]
                    float4 neighborTexel = tex2D(_SubFrame, uv);
                    colorAvg += neighborTexel;
                    colorVar += neighborTexel * neighborTexel;
                }
                colorAvg /= 9.0;
                colorVar /= 9.0;
            
                float gColorBoxSigma = 0.75;
                float4 sigma = sqrt(max(float4(0, 0, 0, 0), colorVar - colorAvg * colorAvg));
                float4 colorMin = colorAvg - gColorBoxSigma * sigma;
                float4 colorMax = colorAvg + gColorBoxSigma * sigma;
            
                // Clamp the history buffer value to the calculated range
                historyBuffer = clamp(historyBuffer, colorMin, colorMax);
            
                // Blend current and clamped history colors
                float4 txaa = lerp(currentBuffer, historyBuffer, 0.95);
                return txaa;
            }

            float4 Calctxaa2(float2 hist_uv, float2 id, float4 currentBuffer, float2 resolution) 
            {
                // Get the color from the history buffer
                float4 historyBuffer = float4(tex2D(_PrevFrame, hist_uv).rgb, 1.);
                
                float currentDepth = currentBuffer.a;
                float historyDepth = tex2D(_PrevFrame, hist_uv).a;
            
                currentBuffer.a = 1.;
            
                // Depth rejection
                /*if (!IsDepthSimilar(currentDepth, historyDepth, 0.01)) {
                    historyBuffer = currentBuffer;
                }*/
            
            
                // Calculate neighborhood statistics
                float4 colorMin = float4(1.0, 1.0, 1.0, 1.0);
                float4 colorMax = float4(0.0, 0.0, 0.0, 0.0);
                for (int i = -1; i <= 1; i++) {
                    for (int j = -1; j <= 1; j++) {
                        float4 neighborTexel = tex2D(_SubFrame, id.xy + float2(i, j)/resolution);
                        colorMin = min(colorMin, neighborTexel);
                        colorMax = max(colorMax, neighborTexel);
                    }
                }
            
                // Blend with min-max of 5 taps in '+' pattern
                float4 largerMin = min(colorMin, min(tex2D(_SubFrame,id.xy - 2/resolution).rgba, tex2D(_SubFrame,id.xy + 2/resolution).rgba));
                float4 largerMax = max(colorMax, max(tex2D(_SubFrame,id.xy - 2/resolution).rgba, tex2D(_SubFrame,id.xy + 2/resolution).rgba));
                
                // Clip historical sample towards center of AABB formed by larger neighborhood
                historyBuffer = clip_aabb(largerMin.rgb, largerMax.rgb, currentBuffer, historyBuffer);
            
                // Edge detection
                float edgeStrength = SobelEdgeDetectionDepth(id.xy, resolution);
                float edgeThreshold = 0.1; // Adjust threshold as needed
            
                // Adjust clamping range based on edge information
                float gColorBoxSigma = lerp(0.75, 1.5, saturate(edgeStrength / edgeThreshold)); // Adjust sigma dynamically
                //float gColorBoxSigma = 0.75;
                float4 sigma = sqrt(max(float4(0, 0, 0, 0), historyBuffer - historyBuffer * historyBuffer));
                colorMin = historyBuffer - gColorBoxSigma * sigma;
                colorMax = historyBuffer + gColorBoxSigma * sigma;
            
                // Clip the history buffer value to the calculated range
                historyBuffer = clip_aabb(colorMin.rgb, colorMax.rgb, currentBuffer, historyBuffer);
            
                // Adaptive blending
                float blendFactor = 0.95;
                float4 txaa = lerp(currentBuffer, historyBuffer, blendFactor);
                return txaa;
            }

            fixed4 frag (v2f i) : SV_Target
            {   

                float4 currColor = tex2D(_SubFrame, i.uv);
   
                float2 centeredNormalizedUV = float2(i.uv * 2.0f - 1.0f);
                float3 worldPosition = DenormalizeDepthToWorldPosition(centeredNormalizedUV, currColor.a);

                float2 hist_uv = GetReprojectionUV(worldPosition, _Resolution.x, _Resolution.y);
                float4 txaa_r2 = Calctxaa2(hist_uv, i.uv, currColor, _Resolution);
                float4 txaa_r = Calctxaa(hist_uv, i.uv, currColor, _Resolution);

               

              
                float4 finalColor = float4(0,0,0,1);

                //finalColor = float4(hist_uv / float2(_Resolution.x, _Resolution.y),0,1);
                //finalColor = txaa_r2;
                
                if(i.uv.x < 0.5)
                {   
                    
                    finalColor = txaa_r;
                    //finalColor =  tex2D(_PrevFrame, hist_uv).a;
                    
       
                }
                else
                {
                    float currentDepth = currColor.a;
                    float historyDepth = tex2D(_PrevFrame, hist_uv).a;
                
                    //currentBuffer.a = 1.;
                
                    // Depth rejection
                    if (!IsDepthSimilar(currentDepth, historyDepth, 0.01)) {
                        finalColor = float4(1,0,0,1);
                    }
                    else
                    {   
                        if(abs(length(currColor - tex2D(_PrevFrame, hist_uv))) < 0.1)
                        {
                            finalColor = float4(0,1,0,1);
                        }
                        else
                        {
                            finalColor = float4(0,1,1,1);
                        }
                        
                    }
                    
                    finalColor = txaa_r;
                    //finalColor = float4(1,1,1,1) * abs(currentDepth - historyDepth);
                    
                }
                    
                    

                return finalColor;


                /*float2 uv = floor(i.uv * _FrameSize);
				float2 uv2 = (floor(i.uv * _SubFrameSize) + 0.5) / _SubFrameSize;
				
				float x = fmod( uv.x, _SubPixelSize);
				float y = fmod( uv.y, _SubPixelSize);
				float frame = y * _SubPixelSize + x;
				float4 cloud;

				cloud = tex2D( _SubFrame, i.uv); 
				
				if( frame == _SubFrameNumber)
				{ 
					cloud = tex2D( _SubFrame, uv2); 
				} 
				else
				{
					float4 prevPos = float4( i.uv * 2.0 - 1.0, 1.0, 1.0);
					prevPos = mul( _InverseProjection, prevPos);
					prevPos = prevPos / prevPos.w;
					prevPos.xyz = mul( (float3x3)_InverseRotation, prevPos.xyz);
					prevPos.xyz = mul( (float3x3)_PreviousRotation, prevPos.xyz);
					float4 reproj = mul( _Projection, prevPos);
					reproj /= reproj.w;
					reproj.xy = reproj.xy * 0.5 + 0.5;
					
					if( reproj.y < 0.0 || reproj.y > 1.0 || reproj.x < 0.0 || reproj.x > 1.0)
					{
						//cloud = float4( 1.0, 0.0, 0.0, 1.0);
						cloud = tex2D( _SubFrame, i.uv);
					}
					else
					{
                        //cloud = float4(1,0,0,1);
						cloud = tex2D( _PrevFrame, reproj.xy);
					}
				}*/
                
                /*
                float4 cloud;
                cloud = tex2D( _SubFrame, i.uv); 

                float4 prevPos = float4( i.uv * 2.0 - 1.0, 1.0, 1.0);
                prevPos = mul( _InverseProjection, prevPos);
                prevPos = prevPos / prevPos.w;
                prevPos.xyz = mul( (float3x3)_InverseRotation, prevPos.xyz);
                prevPos.xyz = mul( (float3x3)_PreviousRotation, prevPos.xyz);
                float4 reproj = mul( _Projection, prevPos);
                reproj /= reproj.w;
                reproj.xy = reproj.xy * 0.5 + 0.5;
                
                if( reproj.y < 0.0 || reproj.y > 1.0 || reproj.x < 0.0 || reproj.x > 1.0)
                {
                    //cloud = float4( 1.0, 0.0, 0.0, 1.0);
                    cloud = tex2D( _SubFrame, i.uv);
                }
                else
                {
                    cloud = tex2D( _PrevFrame, reproj.xy);
                }

				//cloud = tex2D( _SubFrame, uv2); 
                //cloud = tex2D( _SubFrame, i.uv);*/
				
				//return cloud;

            }
            ENDCG
        }

        /*Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            static const float kernel[9] =
            {
                .0625, .125, .0625,
                .125,  .25,  .125,
                .0625, .125, .0625  
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float2 _Resolution;

            float4 GaussianBlur(float2 uv)
            {
                float4 col = float4(0., 0., 0., 0.);

                int2 offsets[9] = { 
                    int2(-1, -1), int2(-1, 0), int2(-1, 1),
                    int2(0, -1), int2(0, 0), int2(0, 1),
                    int2(1, -1), int2(1, 0), int2(1, 1)
                };

                
                for (int i = 0; i < 9; i++)
                {
                    float2 offset = offsets[i] / _Resolution;
                    float2 uvSamp = clamp(uv + offset, 0.0, 1.0); // Clamp UV coordinates to [0, 1]
                    col += float4(tex2D(_MainTex, uvSamp).rgb,1.) * kernel[i];
                }

                return col;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Apply Gaussian blur
                fixed4 color = GaussianBlur(i.uv);
                return color;
            }
            ENDCG
        }*/
    }
}
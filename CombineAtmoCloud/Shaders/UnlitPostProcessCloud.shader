// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable

// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable

// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable
// Upgrade NOTE: replaced '_CameraToWorld' with 'unity_CameraToWorld'

Shader "Custom/UnlitPostProcessCloud"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PrevFrameColor ("Texture", 2D) = "white" {}
        //_Resolution ("Resolution", Vector) = (1, 1)
       // _FrameJitter ("Frame Jitter", Vector) = (0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        // Declare the properties here

        


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/Resources/CombineAtmoCloud/Includes/RayMarch.cginc"

      
            // Define the offsets
            static const int2 offsets[8] = {
                int2(-1, -1), int2(-1, 1),
                int2(1, -1), int2(1, 1),
                int2(1, 0), int2(0, -1),
                int2(0, 1), int2(-1, 0)
            };

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
            //Texture2D _PrevFrameColor;
            sampler2D _PrevFrameColor;

            float2 _Resolution;
            float2 _FrameJitter;

            sampler2D _MainTex;
            
            float4 _MainTex_ST;


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float JitterCorrection2(float2 uv)
            {
                float2 localIndex = floor(fmod(uv * _Resolution, 4.0f));
                localIndex = abs(localIndex - _FrameJitter);

                return saturate(localIndex.x + localIndex.y);
            }

            float JitterCorrection4(float2 uv)
            {
                float2 localIndex = floor(fmod(uv * _Resolution, 4.0f));
                float2 diff = localIndex - _FrameJitter;
                float variance = dot(diff, diff) / 2.0f; // Calculate variance
                float standardDeviation = sqrt(variance); // Calculate standard deviation
                return saturate(1.0f - standardDeviation);
            }

            float JitterCorrection5(float2 uv)
            {
                float2 localIndex = floor(fmod(uv * _Resolution, 4.0f));
                float2 normalizedJitter = _FrameJitter / _Resolution; // Normalize the frame jitter
                float2 diff = localIndex - normalizedJitter;
                float variance = dot(diff, diff) / 2.0f; // Calculate variance
                float standardDeviation = sqrt(variance); // Calculate standard deviation
                return saturate(1.0f - standardDeviation);
            }

            
            float JitterCorrection(float2 uv)
            {
                float2 localIndex = floor(fmod(uv * _Resolution, 4.0f));
                float2 diff = localIndex - _FrameJitter;
                float variance = dot(diff, diff) / 2.0f; // Calculate variance
                float standardDeviation = sqrt(variance) / 10.0f; // Reduce standard deviation

                // Use a Gaussian function for the jitter correction
                return exp(-standardDeviation * standardDeviation / 2.0f);
            }

            float JitterCorrection3(float2 uv)
            {
                float2 localIndex = floor(fmod(uv * _Resolution, 4.0f));
                float2 diff = localIndex - _FrameJitter;
                float variance = dot(diff, diff) / 2.0f; // Calculate variance
                float standardDeviation = sqrt(variance); // Calculate standard deviation

                // Use a box filter for the jitter correction
                float boxFilterSize = 1.0f / _Resolution.x;
                float4 colorSum = float4(0.0f, 0.0f, 0.0f, 0.0f);
                for (float y = -boxFilterSize; y <= boxFilterSize; y += boxFilterSize)
                {
                    for (float x = -boxFilterSize; x <= boxFilterSize; x += boxFilterSize)
                    {
                        colorSum += tex2D(_MainTex, uv + float2(x, y));
                    }
                }
                float4 averageColor = colorSum / (4.0f * boxFilterSize * boxFilterSize);

                // Calculate the difference between the current color and the average color
                float4 colorDiff = tex2D(_MainTex, uv) - averageColor;
                float colorDiffLength = length(colorDiff);

                // Use the difference length as the jitter correction
                return saturate(1.0f - colorDiffLength);
            }


        

            fixed4 frag (v2f i) : SV_Target
            {   
                float jitterCorrection = JitterCorrection4(i.uv);
                float jitterCorrection2 = JitterCorrection2(i.uv);
                jitterCorrection = lerp(jitterCorrection, jitterCorrection2, 0.2);
                //jitterCorrection = jitterCorrection2;


                // Sample the texture
                float4 prevColor = tex2D(_PrevFrameColor, i.uv);
                float4 currColor = tex2D(_MainTex, i.uv);
                //float4 currColor = //GaussianBlur(i.uv);
                float4 jitteredColor = lerp(prevColor, currColor, jitterCorrection);

                // Move UV (0, 0) to the center and get the distance from the zenith
                float2 normalizedUV = (i.uv * 2.0f) - 1.0f;                           
                float distanceFromZenith01 = saturate(length(normalizedUV));

                // Arbitrary convergance speeds. Find what works for you.
                float converganceSpeedZenith = 0.75f;                               
                float converganceSpeedHorizon = 0.5f;
                float converganceSpeed = lerp(converganceSpeedZenith, converganceSpeedHorizon, distanceFromZenith01);

                float4 finalColor = lerp(prevColor, jitteredColor, converganceSpeed);
                finalColor = jitteredColor;

                return finalColor;
            }
            ENDCG
        }

        
    }
}
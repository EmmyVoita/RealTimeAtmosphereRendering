Shader "Custom/UnlitUpSample"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
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
            //Texture2D _PrevFrameColor;

            // Declare the properties here
            float2 _Resolution;
            float2 _FrameJitter;

            sampler2D _MainTex;
            sampler2D sampler_PrevFrameColor;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 cubic(float v)
            {
                float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
                float4 s = n * n * n;
                float x = s.x;
                float y = s.y - 4.0 * s.x;
                float z = s.z - 4.0 * s.y + 6.0 * s.x;
                float w = 6.0 - x - y - z;
                return float4(x, y, z, w) * (1.0/6.0);
            }
            
            float4 textureBicubic(float2 texCoords)
            {
                float2 texSize = float2(480,270);
                float2 invTexSize = 1.0 / texSize;
            
                texCoords = texCoords * texSize - 0.5;
            
                float2 fxy = frac(texCoords);
                texCoords -= fxy;
            
                float4 xcubic = cubic(fxy.x);
                float4 ycubic = cubic(fxy.y);
            
                float4 c = texCoords.xxyy + float2 (-0.5, +1.5).xyxy;
            
                float4 s = float4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
                float4 offset = c + float4 (xcubic.yw, ycubic.yw) / s;
            
                offset *= invTexSize.xxyy;
            
                float4 sample0 = tex2D(_MainTex, offset.xz);
                float4 sample1 = tex2D(_MainTex, offset.yz);
                float4 sample2 = tex2D(_MainTex, offset.xw);
                float4 sample3 = tex2D(_MainTex, offset.yw);
            
                float sx = s.x / (s.x + s.y);
                float sy = s.z / (s.z + s.w);
            
                return lerp(
                    lerp(sample3, sample2, sx), lerp(sample1, sample0, sx)
                , sy);
            }
            
            fixed4 frag (v2f i) : SV_Target
            {

                //float4 currColor = textureBicubic(i.uv);
                float4 currColor = tex2D(_MainTex, i.uv);
                return currColor;
            }
            ENDCG
        }
    }
}
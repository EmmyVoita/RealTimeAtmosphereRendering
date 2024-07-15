// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/WorldPosition"
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
           // Lighting On

		    //Tags {"LightMode" = "ForwardBase"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma exclude_renderers gles xbox360 ps3 ps4 xboxone

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                //LIGHTING_COORDS(3,4)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Apply model matrix to vertex position to get world position
                

                // Calculate world position
                o.color = float4( mul(unity_ObjectToWorld, v.vertex).xyz, 1.0);

                // Calculate depth (in view space)
                o.color.a = o.pos.z / o.pos.w;

                //TRANSFER_VERTEX_TO_FRAGMENT(o);
                //o.color.a = LinearEyeDepth(o.pos.z / o.pos.w);


                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Convert screen position to clip space
                //float4 clipPosition = UnityScreenPosToClipPos(i.uv);
                 // Get the depth value (Z component in clip space)
                //float depth = clipPosition.z / clipPosition.w;

                float4 col = i.color;
                //col.a = depth;
                //float4 test = float4(1,1,1,1) * i.color.a;
                //float4 col = float4(1,1,1,1) * i.color.a;
                //return test;
                return col;
            }
            
            ENDCG
        }

       
    }
    FallBack "Diffuse"
}

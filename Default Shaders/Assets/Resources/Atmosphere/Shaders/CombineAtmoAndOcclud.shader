Shader "Hidden/CombineAtmoAndOcclud"
{
    Properties
    {
        _OccluderTex("OccluderTexture", 2D) = "white" {}
        _AtmoTex ("AtmosphereTexture", 2D) = "white" {}
    }
    SubShader
    {
            // No culling or depth
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        //Tags { "RenderType"="Transp" }
        //Tags { "RenderType"="Opaque" }
        ZWrite Off
        LOD 100
      

        //ZWrite Off
        //Blend One One


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            
            uniform float4x4 _ViewProjInv;
            uniform sampler2D _CameraDepthTexture;

            float4 GetWorldPositionFromDepth(float2 uv_depth)
            {
	            float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv_depth);
                #if defined(SHADER_API_OPENGL)
                depth=depth*2.0-1.0;
                #endif

	            float4 H = float4(uv_depth.x * 2.0 - 1.0, (uv_depth.y) * 2.0 - 1.0, depth, 1.0);

	            float4 D = mul(_ViewProjInv, H);
	            return D / D.w;
            }

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _OccluderTex;
            sampler2D _AtmoTex;
    

            float4 frag(v2f i) : SV_Target
            {
	            //float4 col = GetWorldPositionFromDepth(i.uv);
                  // sample the texture
                float4 occ_col = tex2D(_OccluderTex, i.uv);
                //return float4(occ_col.r,0,0,1);
                float atmo_alpha = occ_col.a;
                float4 col = lerp(occ_col.rgba, tex2D(_AtmoTex, i.uv), 1 - atmo_alpha);
                //col += tex2D(_AtmoTex, i.uv);
                //col = tex2D(_AtmoTex, i.uv) + occ_col * (1 - atmo_alpha);
                return col;
            
            }
            ENDCG
        }
    }
}

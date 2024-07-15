Shader "Hidden/GetOccluder"
{
    Properties
    {
        _OccluderTex("OccluderTexture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D  _OccluderTex;

        
            float4 frag(v2f i) : SV_Target
            {
	            //float4 col = GetWorldPositionFromDepth(i.uv);
                  // sample the texture
                float4 col = tex2D(_OccluderTex, i.uv);
                if(length(col) == 0.0) {
                    //col = float4(1.0, 0.0, 0.0, 1.0);
                    return col;
                }
                //return float4(1.0, 0.0, 0.0, 1.0);
	            return col;
            }
            ENDCG
        }
    }
}

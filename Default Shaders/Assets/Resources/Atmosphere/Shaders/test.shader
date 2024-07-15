// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/test" {
Properties
{
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

CGINCLUDE

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"

uniform sampler2D _MainTex;

ENDCG

SubShader
{
	Tags { "RenderType"="Opaque" }
	LOD 200

	Pass
	{
		Lighting On

		Tags {"LightMode" = "ForwardBase"}

		CGPROGRAM

		#pragma vertex vert
		#pragma fragment frag
		#pragma multi_compile_fwdbase

		struct VSOut
		{
			float4 pos		: SV_POSITION;
            float4 w_pos    : TEXCOORD0;
			float2 uv		: TEXCOORD1;
			LIGHTING_COORDS(3,4)
		};

		VSOut vert(appdata_tan v)
		{
			VSOut o;
			o.pos = UnityObjectToClipPos(v.vertex);
			o.uv = v.texcoord.xy;
            o.w_pos = float4( mul(unity_ObjectToWorld, v.vertex).xyz, 1.0);

			TRANSFER_VERTEX_TO_FRAGMENT(o);

			return o;
		}

		float4 frag(VSOut i) : COLOR
		{
			float3 lightColor = _LightColor0.rgb;
			float3 lightDir = _WorldSpaceLightPos0;
			float4 colorTex = tex2D(_MainTex, i.uv.xy * float2(25.0f, 25.0f));
			float  atten = LIGHT_ATTENUATION(i);
			float3 N = float3(0.0f, 1.0f, 0.0f);
			float  NL = saturate(dot(N, lightDir));

			float3 color = i.w_pos; // * NL * atten;
			return float4(color, colorTex.a);
		}

		ENDCG
	}
} 
FallBack "Diffuse"
}
Shader "Custom/template" {
	Properties{
		/*
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		*/
	}
	SubShader{
		Tags{ "RenderType" = "Opaque" }
		LOD 200
		Pass{
		CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag

#include "UnityCG.cginc"

		fixed4 frag(v2f_img i) : SV_Target
	{
		fixed2 resolution = _ScreenParams;
	fixed2 position = (i.uv * resolution / resolution.xy);
	float time = _Time * 30;
	fixed color = 0.0;
	color += sin(position.x * cos(time / 15.0) * 80.0) + cos(position.y * cos(time / 15.0) * 10.0);
	color += sin(position.y * sin(time / 10.0) * 40.0) + cos(position.x * sin(time / 25.0) * 40.0);
	color += sin(position.x * sin(time / 5.0) * 10.0) + sin(position.y * sin(time / 35.0) * 80.0);
	color *= sin(time / 10.0) * 0.5;
	return fixed4(color, color * 0.5, sin(color + time / 3.0) * 0.75, 1.0);

	}
		ENDCG
	}
	}
		FallBack "Diffuse"
}
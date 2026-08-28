Shader "Liquid/Liquid"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_NormalMap("Normal Map", 2D) = "bump" {}
		_WavesTex("Waves", 2D) = "black" {}
		_PerlinNoise("Perlin Noise", 2D) = "black" {}
		_BubbleTex("Bubble", 2D) = "bump" {}
		_LiquidColor("Liquid Color", Color) = (1,1,1,1)
		_TopColor("Top Color", Color) = (1,1,1,1)
		_FoamColor("Foam Color", Color) = (1,1,1,1)
		_Refraction("Refraction Index", Float) = 0.5
		_ProbeLod("Murkiness", Float) = 0.05
		_Syrup("Syrup", Float) = 0
		_EdgeThickness("Edge Thickness", Float) = 0.02
		_FresnelPower("Fresnel Power", Float) = 1.5
		_MeniscusHeight("Meniscus Height", Float) = 0.04
		_MeniscusCurve("Meniscus Curve", Float) = 0.75
		_FoamAmount("Foam Amount", Float) = 1.0
		_BubbleScale("Bubble Scale", Float) = 1.0
		_BubbleCount("Maximum Bubbles", Float) = 30
		_UseGrabpass("Refraction Method", Float) = 0
	}
	SubShader
	{
		Tags 
		{ 
			"RenderType"="Transparent" 
			"Queue"="Transparent-2" 
			"RenderPipeline"="UniversalPipeline"
		}
		LOD 100

		// Back faces
		Pass
		{
			Name "LiquidBack"
			Tags { "LightMode"="UniversalForward" }
			Cull Front
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma shader_feature_local USE_GRABPASS
			
			#include "LiquidUtils.cginc"
			#include "Liquid.cginc"

			v2f vert(appdata v)
			{
				return vertex(v, -1.0);
			}

			float4 frag (v2f i) : SV_Target
			{
				return fragment(i, -1.0);
			}
			
			ENDHLSL
		}

		// Front faces
		Pass
		{
			Name "LiquidFront"
			Tags { "LightMode"="UniversalForward" }
			Cull Back
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma shader_feature_local USE_GRABPASS
			
			#include "LiquidUtils.cginc"
			#include "Liquid.cginc"

			v2f vert(appdata v)
			{
				return vertex(v, 1.0);
			}

			float4 frag (v2f i) : SV_Target
			{
				return fragment(i, 1.0);
			}
			
			ENDHLSL
		}
	}
	CustomEditor "LiquidEditor"
}

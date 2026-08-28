Shader "Liquid/Glass"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_NormalMap("Normal Map", 2D) = "bump" {}
		_RoughnessTex("Roughness", 2D) = "white" {}
		_GlassColor("Glass Tint", Color) = (1,1,1,1)
		_Blur("Blur", Float) = 1
		_FresnelPower("Fresnel Power", Float) = 3
		_NormalMult("Normal Map Multiplier", Float) = 1
		_Thickness("Thickness", Float) = 0.05
		_DrawBackface("Draw Backface", Float) = 1
	}
	SubShader
	{
		Tags 
		{ 
			"RenderType"="Transparent" 
			"Queue"="Transparent-1" 
			"RenderPipeline"="UniversalPipeline"
		}
		LOD 100

		// Pass for backface rendering
		Pass
		{
			Name "GlassBack"
			Tags { "LightMode"="UniversalForward" }

			Cull Front
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			
			#include "LiquidUtils.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float fogFactor : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _GlassColor;
				float _Blur;
				float _Thickness;
				float _DrawBackface;
			CBUFFER_END

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

			v2f vert (appdata v)
			{
				v2f o = (v2f)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 scaledVertex = v.vertex.xyz / (_Thickness + 1.0);
				float3 worldPos = TransformObjectToWorld(scaledVertex);
				o.worldPos = worldPos;
				o.vertex = TransformWorldToHClip(worldPos);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = TransformObjectToWorldNormal(v.normal);
				o.fogFactor = ComputeFogFactor(o.vertex.z);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				if (_DrawBackface < 0.5) discard;

				float3 diffuse = GetLighting(1.0, i.worldPos, i.normal);
				float3 ambient = SampleSH(i.normal);

				float4 mainTex = _MainTex.Sample(sampler_MainTex, i.uv);
				float4 col = mainTex * float4(diffuse + ambient, 1.0) * _GlassColor;
				col.a = _GlassColor.a * mainTex.a * 0.5;

				col.rgb = MixFog(col.rgb, i.fogFactor);
				return col;
			}
			ENDHLSL
		}

		// Main glass front pass
		Pass
		{
			Name "GlassFront"
			Tags { "LightMode"="UniversalForward" }

			Cull Back
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			
			#include "LiquidUtils.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float2 uvnm : TEXCOORD1;
				float3 normal : TEXCOORD2;
				float3 viewDir : TEXCOORD3;
				float3 worldNormal : TEXCOORD4;
				half3 tspace0 : TEXCOORD5;
				half3 tspace1 : TEXCOORD6;
				half3 tspace2 : TEXCOORD7;
				float3 worldPos : TEXCOORD8;
				float4 screenPos : TEXCOORD9;
				float fogFactor : TEXCOORD10;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _NormalMap_ST;
				float4 _GlassColor;
				float _Blur;
				float _FresnelPower;
				float _NormalMult;
				float _Thickness;
				float _DrawBackface;
			CBUFFER_END

			TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
			TEXTURE2D(_RoughnessTex);   SAMPLER(sampler_RoughnessTex);
			TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);

			v2f vert (appdata v)
			{
				v2f o = (v2f)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
				o.worldPos = positionWS;
				o.vertex = TransformWorldToHClip(positionWS);

				half3 worldNormal = TransformObjectToWorldNormal(v.normal);
				half3 worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBitangent = cross(worldNormal, worldTangent) * tangentSign;
				o.tspace0 = half3(worldTangent.x, worldBitangent.x, worldNormal.x);
				o.tspace1 = half3(worldTangent.y, worldBitangent.y, worldNormal.y);
				o.tspace2 = half3(worldTangent.z, worldBitangent.z, worldNormal.z);

				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.uvnm = TRANSFORM_TEX(v.uv, _NormalMap);
				o.normal = worldNormal;
				o.worldNormal = worldNormal;
				o.viewDir = normalize(GetWorldSpaceViewDir(positionWS));
				o.screenPos = ComputeScreenPos(o.vertex);
				o.fogFactor = ComputeFogFactor(o.vertex.z);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				float4 normalMap = _NormalMap.Sample(sampler_NormalMap, i.uvnm);
				half3 tangentNormal = UnpackNormal(normalMap);

				float2 distort = tangentNormal.xy * _NormalMult * 0.05;
				float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);

				half3 worldNormal;
				worldNormal.x = dot(i.tspace0, tangentNormal);
				worldNormal.y = dot(i.tspace1, tangentNormal);
				worldNormal.z = dot(i.tspace2, tangentNormal);
				worldNormal = normalize(worldNormal);

				float roughness = _RoughnessTex.Sample(sampler_RoughnessTex, i.uv).r;
				float fresnel = pow(saturate(1.0 - saturate(dot(i.worldNormal, normalize(i.viewDir)))), max(_FresnelPower, 0.01));

				float3 reflectedDirection = reflect(-normalize(i.viewDir), worldNormal);
				half3 envCubeReflect = GlossyEnvironmentReflection(reflectedDirection, i.worldPos, clamp(_Blur * 0.25 / max(roughness, 0.01), 0.0, 1.0), 1.0);

				half3 sceneColor = SampleSceneColor(screenUV + distort);

				float shininess = 10.0 / max(_Blur, 0.01);
				float3 specular = GetLighting(0.0, i.worldPos, worldNormal, shininess);
				float3 diffuse = GetLighting(1.0, i.worldPos, worldNormal);
				float3 ambient = SampleSH(worldNormal);

				float4 main = _MainTex.Sample(sampler_MainTex, i.uv);

				float4 col = float4(sceneColor, 1.0);
				col.rgb *= roughness;
				col.rgb *= envCubeReflect;
				col.rgb *= _GlassColor.rgb;
				col.rgb += fresnel * 0.5 * _GlassColor.rgb;

				col.rgb = lerp(col.rgb, main.rgb * (diffuse + ambient), main.a);
				col.rgb += specular * roughness;
				col.a = _GlassColor.a;

				col.rgb = MixFog(col.rgb, i.fogFactor);
				return col;
			}
			ENDHLSL
		}

		// Shadow Caster Pass
		Pass 
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull Back

			HLSLPROGRAM
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "LiquidUtils.cginc"

			struct Attributes
			{
				float4 positionOS   : POSITION;
				float3 normalOS     : NORMAL;
				float2 uv           : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS   : SV_POSITION;
				float2 uv           : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _GlassColor;
				float _Blur;
				float _FresnelPower;
				float _NormalMult;
				float _Thickness;
				float _DrawBackface;
			CBUFFER_END

			Varyings ShadowPassVertex(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				return output;
			}

			half4 ShadowPassFragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				half4 col = 1.0 - _GlassColor * 0.5;
				float luminance = col.r * 0.3 + col.g * 0.59 + col.b * 0.11;
				clip(GetDither(input.positionCS.xy, luminance) * luminance);
				return 0;
			}
			ENDHLSL
		}

		// DepthOnly Pass
		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }

			ZWrite On
			ColorMask R
			Cull Back

			HLSLPROGRAM
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS   : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS   : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings DepthOnlyVertex(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				return output;
			}

			half4 DepthOnlyFragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				return 0;
			}
			ENDHLSL
		}
	}
	CustomEditor "GlassEditor"
}

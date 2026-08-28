#ifndef LIQUID_UTILS_INCLUDED
#define LIQUID_UTILS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

// Calculates lighting for diffuse and specular in URP
// type: 0 - specular, 1 - diffuse
float3 GetLighting(float type, float3 worldPos, float3 normal, float shininess)
{
	float3 viewDir = normalize(GetCameraPositionWS() - worldPos);
	normal = normalize(normal);
	shininess = clamp(shininess, 1.0, 1000.0);

	float3 totalDiffuse = float3(0, 0, 0);
	float3 totalSpecular = float3(0, 0, 0);

	// Main Directional Light
	Light mainLight = GetMainLight();
	float3 mainLightDir = normalize(mainLight.direction);
	float NdotL = max(0.0, dot(normal, mainLightDir));
	totalDiffuse += mainLight.color * (NdotL * mainLight.distanceAttenuation);

	float3 reflection = reflect(mainLightDir, normal);
	float specDot = saturate(dot(reflection, -viewDir));
	totalSpecular += mainLight.color * (pow(specDot, shininess) * mainLight.distanceAttenuation);

	// Additional Lights (Point, Spot, etc.)
	#if defined(_ADDITIONAL_LIGHTS)
	uint pixelLightCount = GetAdditionalLightsCount();
	for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
	{
		Light light = GetAdditionalLight(lightIndex, worldPos);
		float3 lightDir = normalize(light.direction);
		float addNdotL = max(0.0, dot(normal, lightDir));
		float atten = light.distanceAttenuation * light.shadowAttenuation;
		totalDiffuse += light.color * (addNdotL * atten);

		float3 addReflect = reflect(lightDir, normal);
		float addSpecDot = saturate(dot(addReflect, -viewDir));
		totalSpecular += light.color * (pow(addSpecDot, shininess) * atten);
	}
	#endif

	if (type == 0.0)
		return totalSpecular;
	else
		return totalDiffuse;
}

float3 GetLighting(float type, float3 worldPos, float3 normal)
{
	return GetLighting(type, worldPos, normal, 0.0);
}

float GetDither(float2 pos, float factor)
{
	float DITHER_THRESHOLDS[16] =
	{
		1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
		13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
		4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
		16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
	};

	return factor - DITHER_THRESHOLDS[(uint(pos.x) % 4) * 4 + uint(pos.y) % 4];
}

#endif
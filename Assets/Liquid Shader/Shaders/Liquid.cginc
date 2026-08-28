#ifndef LIQUID_INCLUDED
#define LIQUID_INCLUDED

#define EPSILON 1.192092896e-07

CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	float4 _LiquidColor;
	float4 _TopColor;
	float4 _FoamColor;
	float _Refraction;
	float _BoundsL;
	float _BoundsH;
	float _BoundsX;
	float _BoundsZ;
	float _ProbeLod;
	float _EdgeThickness;
	float _WavesMult;
	float _FresnelIntensity;
	float _FresnelPower;
	float _MeshScale;
	float _MeniscusHeight;
	float _MeniscusCurve;
	float _Syrup;
	float _Foam;
	float _FoamAmount;
	float _BubbleScale;
	float _BubbleCount;
	float _UseGrabpass;
	float4 _Plane;
	float3 _PlanePos;
CBUFFER_END

TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
TEXTURE2D(_WavesTex);       SAMPLER(sampler_WavesTex);
TEXTURE2D(_BubbleTex);      SAMPLER(sampler_BubbleTex);
TEXTURE2D(_PerlinNoise);    SAMPLER(sampler_PerlinNoise);

struct appdata
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 worldPos : TEXCOORD1;
	float3 viewDir : TEXCOORD2;
	float3 normal : TEXCOORD3;
	half3 tspace0 : TEXCOORD4;
	half3 tspace1 : TEXCOORD5;
	half3 tspace2 : TEXCOORD6;
	float4 screenPos : TEXCOORD7;
	float fogFactor : TEXCOORD8;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

struct Triplanar {
	float2 x, y, z;
};

Triplanar GetTriplanar(float3 worldPos) {
	Triplanar tri;
	tri.x = worldPos.zy;
	tri.y = worldPos.xz;
	tri.z = worldPos.xy;
	return tri;
}

// XZ representation of a texture
float4 BiplanarTex(Texture2D tex, SamplerState smp, float3 worldPos, float2 scale, float3 offset) {
	float4 x = tex.Sample(smp, (worldPos.yz + offset.yz) * scale);
	float4 z = tex.Sample(smp, (worldPos.xy + offset.xy) * scale);
	return x + z;
}

float4 TriplanarTex(Texture2D tex, SamplerState smp, float3 worldPos, float3 normal, float2 scale, float3 offset) {
	normal = abs(normal);
	float3 weights = normal / max(normal.x + normal.y + normal.z, 0.0001);
	float4 x = tex.Sample(smp, (worldPos.yz + offset.yz) * scale);
	float4 y = tex.Sample(smp, (worldPos.xz + offset.xz) * scale);
	float4 z = tex.Sample(smp, (worldPos.xy + offset.xy) * scale);
	return weights.x * x + weights.y * y + weights.z * z;
}

float GetFresnel(float3 normal, float3 viewDir, float facing, float power, float intensity) {
	float dotProduct = 1.0 - pow(saturate(dot(normal, normalize(facing * viewDir))), max(power, 0.01)) * intensity;
	float4 fresnelCol = smoothstep(0.5, 1.0, dotProduct);
	float fresnel = saturate(fresnelCol.x);
	return fresnel;
}

float CalculateWaves(v2f i, float facing) {
	float fresnel = GetFresnel(i.normal, i.viewDir, facing, _MeniscusCurve, 0.5);
	float3 objCenter = TransformObjectToWorld(float3(0, 0, 0));
	float4 wavesTex = BiplanarTex(_WavesTex, sampler_WavesTex, i.worldPos, 0.25 / max(_MeshScale, 0.001), -_Time.x * 10.0 - objCenter);
	float waves = saturate(wavesTex.rgb).x - 0.5;
	waves = waves * 0.005 * pow(max(_WavesMult, 0.0), 5.0) * (1.0 + fresnel) * _MeshScale - (_WavesMult - 1.0) * 0.1;
	return waves;
}

v2f vertex (appdata v, float facing)
{
	v2f o = (v2f)0;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
	o.worldPos = positionWS;
	o.pos = TransformWorldToHClip(positionWS);
	o.uv = TRANSFORM_TEX(v.uv, _MainTex);

	half3 worldNormal = TransformObjectToWorldNormal(v.normal);
	half3 worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
	half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
	half3 worldBitangent = cross(worldNormal, worldTangent) * tangentSign;
	o.tspace0 = half3(worldTangent.x, worldBitangent.x, worldNormal.x);
	o.tspace1 = half3(worldTangent.y, worldBitangent.y, worldNormal.y);
	o.tspace2 = half3(worldTangent.z, worldBitangent.z, worldNormal.z);

	o.normal = worldNormal;
	o.viewDir = normalize(GetWorldSpaceViewDir(positionWS));
	o.screenPos = ComputeScreenPos(o.pos);
	o.fogFactor = ComputeFogFactor(o.pos.z);

	return o;
}
			
float4 fragment (v2f i, float facing)
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	float4 col = _MainTex.Sample(sampler_MainTex, i.uv);
	float4 colorAdd = float4(0, 0, 0, 0);
			
	float fresnel = GetFresnel(i.normal, i.viewDir, facing, _MeniscusCurve, 0.5);
	float height = (_BoundsH - _BoundsL);

	float waves = CalculateWaves(i, facing);
	
	// Get cutoff plane
	float distance = dot(i.worldPos, _Plane.xyz);
	distance += _Plane.w + waves / ((_WavesMult + 1.0) * (_WavesMult + 1.0));

	// Meniscus
	float increment = _EdgeThickness * 0.33;
	float edgeOffset = fresnel * _MeniscusHeight + _EdgeThickness;
	colorAdd = lerp(float4(0, 0, 0, 0), float4(0.35, 0.35, 0.35, 0), saturate((distance - edgeOffset + increment * 3.0) * 75.0));
	colorAdd = lerp(colorAdd, float4(-0.35, -0.35, -0.35, 0), saturate((distance - edgeOffset + increment * 2.5) * 75.0));
	colorAdd = lerp(colorAdd, float4(0, 0, 0, -0.5), saturate((distance - edgeOffset + increment * 1.6) * 75.0));
	colorAdd = lerp(colorAdd, float4(0, 0, 0, -0.5), saturate((distance - edgeOffset + increment * waves * 20.0) * 75.0));
	colorAdd = lerp(colorAdd, float4(0, 0, 0, -1.0), saturate((distance - edgeOffset + increment * waves * 25.0) * 75.0));

	// Calculate normals
	float3 objCenter = TransformObjectToWorld(float3(0, 0, 0));
	float4 normalMap = BiplanarTex(_NormalMap, sampler_NormalMap, i.worldPos, 1.0, -objCenter);
	half3 tangentNormal;
	if (facing > 0)
		tangentNormal = UnpackNormal(_NormalMap.Sample(sampler_NormalMap, i.uv));
	else
		tangentNormal = UnpackNormal(normalMap);

	half3 worldNormal;
	worldNormal.x = dot(i.tspace0, tangentNormal);
	worldNormal.y = dot(i.tspace1, tangentNormal);
	worldNormal.z = dot(i.tspace2, tangentNormal);
	worldNormal = normalize(worldNormal);

	// Bubbles
	float4 bubbles = float4(0, 0, 0, 0);
	float bubbleDistance = saturate(distance * 3.0 + 1.0);
	float numBubbles = _BubbleCount * (_WavesMult * 0.5 - 1.0);
	numBubbles = clamp(numBubbles, 0.0, _BubbleCount);

	float perlin = BiplanarTex(_PerlinNoise, sampler_PerlinNoise, i.worldPos, 2.0, float3(_SinTime.x + 1.0, _CosTime.z + 2.0, _SinTime.y + 3.0)).r;
	int maxB = (int)numBubbles;
	for (int j = 1; j < maxB; j++) {
		float3 bubblePos = float3(sin(_Time.w + j) * _BoundsX / 3.0 + perlin / 40.0, height / 2.0 - fmod((_Time.y + j * (height * 0.1)), max(0.001, lerp(0.0, height, _PlanePos.y + 0.55))), cos(_Time.w - j) * _BoundsZ / 3.0 + perlin / 40.0) - objCenter;
		float2 bubbleScale = 25.0 / max(_BubbleScale, 0.01) + j;

		float4 bubble0 = BiplanarTex(_BubbleTex, sampler_BubbleTex, i.worldPos, bubbleScale, bubblePos);
		float4 bubble1 = BiplanarTex(_BubbleTex, sampler_BubbleTex, i.worldPos, bubbleScale * (1.0 / j + 3.0), bubblePos + 0.01);
		float4 bubble2 = BiplanarTex(_BubbleTex, sampler_BubbleTex, i.worldPos, bubbleScale * (1.0 / j + 2.0), bubblePos - 0.02);

		bubbles.rgb += bubble0.rgb * bubble0.a + bubble1.rgb * bubble1.a + bubble2.rgb * bubble2.a;
		bubbles.a += (bubble0.a + bubble1.a + bubble2.a);
	}

	// Differentiate back face and front face normals
	half3 surfNormal = worldNormal;
	half3 topNormal = normalize(half3(_Plane.x + waves * 10.0, _Plane.y, _Plane.z + waves * 10.0));
	if (facing < 0) {
		surfNormal = topNormal;
		surfNormal = lerp(surfNormal, -worldNormal, saturate((distance - edgeOffset + increment * 3.0) * 25.0));
	} else {
		surfNormal = lerp(surfNormal, topNormal, saturate((distance - edgeOffset + increment * 3.0) * 25.0));
		surfNormal = lerp(surfNormal, worldNormal, saturate((distance - edgeOffset + _EdgeThickness / 3.0) * 100.0));
		surfNormal.x *= (bubbles.r * bubbleDistance * 4.0 + 1.0);
		surfNormal.y *= (bubbles.g * bubbleDistance * 4.0 + 1.0);
		surfNormal.z *= (bubbles.b * bubbleDistance * 4.0 + 1.0);
	}
	surfNormal = normalize(surfNormal);

	// Meniscus refraction
	float refractionIndex = lerp(_Refraction, _Refraction + 0.5, saturate((distance - edgeOffset + increment * 3.0) * 25.0));
	refractionIndex = max(refractionIndex, 0.01);

	float3 refractedDirection = refract(-normalize(i.viewDir), surfNormal, 1.0 / refractionIndex);
	float3 reflectedDirection = reflect(-normalize(i.viewDir), surfNormal);

	half3 refraction = half3(0, 0, 0);
	half3 reflection = half3(0, 0, 0);

	#if defined(USE_GRABPASS)
	float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
	float2 distortOffset = surfNormal.xy * (_EdgeThickness * 2.0 + 0.02);
	refraction = SampleSceneColor(screenUV + distortOffset).rgb;
	reflection = GlossyEnvironmentReflection(reflectedDirection, i.worldPos, _ProbeLod, 1.0);
	#else
	refraction = GlossyEnvironmentReflection(refractedDirection, i.worldPos, _ProbeLod, 1.0);
	reflection = GlossyEnvironmentReflection(reflectedDirection, i.worldPos, _ProbeLod, 1.0);
	#endif

	col.rgb *= refraction * (1.0 - _Syrup);
	col.rgb += _Syrup;

	// Lighting
	float shininess = 30.0 * (1.0 - _ProbeLod);
	float3 specularReflection = GetLighting(0.0, i.worldPos, surfNormal, shininess);
	float3 diffuseReflection = GetLighting(1.0, i.worldPos, surfNormal);
	float3 ambientLighting = SampleSH(surfNormal);

	float foamVal = clamp(_Foam, 0.0, _FoamAmount * 0.03);
	float4 noise = TriplanarTex(_WavesTex, sampler_WavesTex, i.worldPos, surfNormal, float2(1, 1), float3(0, waves, 0));

	if (facing > 0) {
		float refresnel = GetFresnel(i.normal, i.viewDir, facing, _FresnelPower, 1.0);
		col.rgb *= (1.0 - refresnel);
		col.rgb += reflection * refresnel;
		col.rgb *= _LiquidColor.rgb;
		col += colorAdd;
		col.rgb += specularReflection;
		col.rgb -= bubbles.a / 4.0 * bubbleDistance;
		col.rgb += saturate(bubbles.rgb) / 4.0 * bubbleDistance;
		col.rgb = lerp(col.rgb, _FoamColor.rgb * ((diffuseReflection + ambientLighting) * noise.r * 0.25 + 0.5), saturate((distance - edgeOffset + _EdgeThickness / 3.0) * 100.0) * saturate(foamVal * 100.0));
		col.a = _LiquidColor.a;
		col.a = lerp(col.a, 0.0, saturate((distance / 6.0 - foamVal - edgeOffset / 6.0 + _EdgeThickness / 18.0) * 600.0));
	} else {
		float bfFresnel = pow(saturate(1.0 + dot(-normalize(i.viewDir), _Plane.xyz)), _FresnelPower * 0.35);
		col.rgb *= (1.0 - bfFresnel);
		col.rgb += reflection * bfFresnel;
		col.rgb *= _TopColor.rgb;
		col = lerp(col, float4(0, 0, 0, 0), saturate((distance - edgeOffset + increment * 1.6) * 100.0));
		col.rgb += specularReflection * bfFresnel;
		col.a = _TopColor.a;
		col.a = lerp(col.a, 1.0, saturate((distance - edgeOffset + _EdgeThickness) * 100.0));
		col.a = lerp(col.a, 0.0, saturate((distance / 6.0 - foamVal - edgeOffset / 6.0 + _EdgeThickness / 18.0) * 600.0));
		col.rgb = lerp(col.rgb, _FoamColor.rgb * (bfFresnel * 0.5 + 0.5), saturate(foamVal * 100.0));
	}

	if (col.a <= 0.001) discard;

	col.rgb = MixFog(col.rgb, i.fogFactor);
	return col;
}

#endif
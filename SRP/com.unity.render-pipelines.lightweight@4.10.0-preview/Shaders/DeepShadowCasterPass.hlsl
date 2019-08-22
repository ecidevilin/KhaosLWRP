#ifndef LIGHTWEIGHT_DEPTH_NORMAL_PASS_INCLUDED
#define LIGHTWEIGHT_DEPTH_NORMAL_PASS_INCLUDED


#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
float4 _ShadowBias; // x: depth bias, y: normal bias
float3 _LightDirection;

uint _DeepShadowMapSize;
uint _DeepShadowMapDepth;

RWStructuredBuffer<uint> _CountBufferUAV	: register(u1);
RWStructuredBuffer<float2> _DataBufferUAV	: register(u2);

struct Attributes
{
	float4 positionOS     : POSITION;
	float3 normalOS		: NORMAL;
	float2 texcoord     : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 uv           : TEXCOORD0;
	float4 positionCS   : SV_POSITION;
};

// Copy from ShadowCasterPass.hlsl
float4 GetShadowPositionHClip(Attributes input)
{
	float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
	float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

	float invNdotL = 1.0 - saturate(dot(_LightDirection, normalWS));
	float scale = invNdotL * _ShadowBias.y;

	// normal bias is negative since we want to apply an inset normal offset
	positionWS = _LightDirection * _ShadowBias.xxx + positionWS;
	positionWS = normalWS * scale.xxx + positionWS;
	float4 positionCS = TransformWorldToHClip(positionWS);

#if UNITY_REVERSED_Z
	positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
	positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

	return positionCS;
}

Varyings DeepShadowCasterVertex(Attributes input)
{
	Varyings output = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(input);

	output.uv.xy = TRANSFORM_TEX(input.texcoord, _MainTex);
	output.positionCS = GetShadowPositionHClip(input);
	output.uv.zw = output.positionCS.xy * 0.5 + 0.5;
	return output;
}

half4 DeepShadowCasterFragment(Varyings input) : SV_TARGET
{
	half alpha = Alpha(SampleAlbedoAlpha(input.uv.xy, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)).a, _Color, _Cutoff);

	uint2 lightUV = input.uv.zw * _DeepShadowMapSize;
	uint idx = lightUV.y * _DeepShadowMapSize + lightUV.x;
	uint originalVal = 0;
	InterlockedAdd(_CountBufferUAV[idx], 1, originalVal);
	originalVal = min(_DeepShadowMapDepth - 1, originalVal);
	uint offset = idx * _DeepShadowMapDepth;
	_DataBufferUAV[offset + originalVal] = float2(input.positionCS.z, 1 - alpha);
	return 0;
}
#endif

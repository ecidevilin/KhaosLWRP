#ifndef LIGHTWEIGHT_SHADOW_CASTER_PASS_INCLUDED
#define LIGHTWEIGHT_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"

float4 _ShadowBias; // x: depth bias, y: normal bias
float3 _LightDirection;

#ifdef _DEEP_SHADOW_CASTER
#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/DeepShadowMaps.hlsl"
CBUFFER_START(_DeepShadowCasterBuffer)
uint _DeepShadowMapSize;
uint _DeepShadowMapDepth;
CBUFFER_END

RWStructuredBuffer<uint> _CountBufferUAV	: register(u1);
RWStructuredBuffer<uint> _DataBufferUAV	: register(u2);
#endif

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float2 uv           : TEXCOORD0;
    float4 positionCS   : SV_POSITION;
};

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

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);

    output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
    output.positionCS = GetShadowPositionHClip(input);

    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
#ifdef _DEEP_SHADOW_CASTER
	half alpha = SampleAlbedoAlpha(input.uv.xy, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)).a * _Color.a;
	uint2 lightUV = input.positionCS.xy;
	uint idx = lightUV.y * _DeepShadowMapSize + lightUV.x;
	uint originalVal = 0;
	InterlockedAdd(_CountBufferUAV[idx], 1, originalVal);
	originalVal = min(_DeepShadowMapDepth - 1, originalVal);
	uint offset = idx * _DeepShadowMapDepth;
	_DataBufferUAV[offset + originalVal] = PackDepthAndAlpha(input.positionCS.z, alpha);
	return input.positionCS.z;
#else
	Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)).a, _Color, _Cutoff);
	return 0;
#endif
}

#endif

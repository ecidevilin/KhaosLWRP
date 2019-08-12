#ifndef LIGHTWEIGHT_DEPTH_NORMAL_PASS_INCLUDED
#define LIGHTWEIGHT_DEPTH_NORMAL_PASS_INCLUDED


#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"

struct Attributes
{
	float4 position     : POSITION;
	float3 normal		: NORMAL;
	float2 texcoord     : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float2 uv           : TEXCOORD0;
	float4 nz           : TEXCOORD1;
	float4 positionCS   : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
	Varyings output = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
	output.positionCS = TransformObjectToHClip(input.position.xyz);
	output.nz.xyz = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, input.normal));
	output.nz.z = -output.positionCS.z * _ProjectionParams.w;
	return output;
}
// Encoding/decoding [0..1) floats into 8 bit/channel RG. Note that 1.0 will not be encoded properly.
inline float2 EncodeFloatRG(float v)
{
	float2 kEncodeMul = float2(1.0, 255.0);
	float kEncodeBit = 1.0 / 255.0;
	float2 enc = kEncodeMul * v;
	enc = frac(enc);
	enc.x -= enc.y * kEncodeBit;
	return enc;
}
// Encoding/decoding view space normals into 2D 0..1 vector
inline float2 EncodeViewNormalStereo(float3 n)
{
	float kScale = 1.7777;
	float2 enc;
	enc = n.xy / (n.z + 1);
	enc /= kScale;
	enc = enc * 0.5 + 0.5;
	return enc;
}
inline float4 EncodeDepthNormal(float depth, float3 normal)
{
	float4 enc;
	enc.xy = EncodeViewNormalStereo(normal);
	enc.zw = EncodeFloatRG(depth);
	return enc;
}

half4 DepthNormalsFragment(Varyings input) : SV_TARGET
{
	Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)).a, _Color, _Cutoff);
	return EncodeDepthNormal(input.nz.w, input.nz.xyz);;
}
#endif

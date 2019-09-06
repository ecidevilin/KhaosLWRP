Shader "Hidden/Lightweight Render Pipeline/OITComposite"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
    }

    HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
    #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Shadows.hlsl"

    struct Attributes
    {
        float4 positionOS   : POSITION;
        float2 texcoord     : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4  positionCS  : SV_POSITION;
        float2  uv          : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    Varyings Vertex(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);

        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.texcoord;
        return output;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "LightweightPipeline"}
        LOD 100

        Pass
        {
            Name "OITComposite"
            ZTest Always
            ZWrite Off
			Blend OneMinusSrcAlpha SrcAlpha

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma vertex Vertex
            #pragma fragment FragOITComposite

			#pragma multi_compile _ _DEEP_SHADOW_MAPS

			TEXTURE2D(_AccumColor);
			SAMPLER(sampler_AccumColor);

			TEXTURE2D(_AccumGI);
			SAMPLER(sampler_AccumGI);

			TEXTURE2D_HALF(_AccumAlpha);
			SAMPLER(sampler_AccumAlpha);

            half4 FragOITComposite(Varyings input) : SV_Target
            {
				float4 accum = SAMPLE_TEXTURE2D(_AccumColor, sampler_AccumColor, input.uv.xy);
				float4 gi = SAMPLE_TEXTURE2D(_AccumGI, sampler_AccumGI, input.uv.xy);
				float r = accum.a;
				accum.a = SAMPLE_TEXTURE2D(_AccumAlpha, sampler_AccumAlpha, input.uv.xy).r;

				half3 col = saturate(accum.rgb / clamp(accum.a, 1e-4, 5e4));
#ifdef _DEEP_SHADOW_MAPS
				half dsmAtten = SAMPLE_TEXTURE2D(_DeepShadowLut, sampler_DeepShadowLut, input.uv.xy).r;
				col *= dsmAtten;
#endif
				col += saturate(gi.rgb / clamp(accum.a, 1e-4, 5e4));
				return float4(col, r);
            }
            ENDHLSL
        }
    }
}

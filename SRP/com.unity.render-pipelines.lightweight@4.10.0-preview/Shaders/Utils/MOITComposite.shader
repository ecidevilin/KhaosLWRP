Shader "Hidden/Lightweight Render Pipeline/MOITComposite"
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
            Name "MomentOITComposite"
            ZTest Always
            ZWrite Off
			Blend OneMinusSrcAlpha SrcAlpha

            HLSLPROGRAM
			#pragma exclude_renderers d3d11_9x gles
            #pragma vertex Vertex
            #pragma fragment FragMOITComposite

			TEXTURE2D(_MOIT);
			SAMPLER(sampler_MOIT);
			TEXTURE2D_FLOAT(_b0);
			SAMPLER(sampler_b0);

            half4 FragMOITComposite(Varyings input) : SV_Target
            {
				float4 moit = SAMPLE_TEXTURE2D(_MOIT, sampler_MOIT, input.uv.xy);
				moit.rgb /= moit.a;

				float b0 = SAMPLE_TEXTURE2D(_b0, sampler_b0, input.uv.xy).r;

				return half4(moit.rgb, exp(-b0));
            }
            ENDHLSL
        }
    }
}

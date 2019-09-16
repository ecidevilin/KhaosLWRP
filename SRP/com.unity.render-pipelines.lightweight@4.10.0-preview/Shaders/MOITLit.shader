// ------------------------------------------
// Only directional light is supported for lit particles
// No shadow
// No distortion
Shader "Lightweight Render Pipeline/MOITLit"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)

		//NOTE: For DepthOnly & ShadowCaster
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _MetallicGlossMap("Metallic", 2D) = "white" {}
        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

		[Enum(Off, 0, Front, 1, Back, 2)]
		_Cull("Cull", Float) = 2.0

		_Overestimation("Overestimation", Range(0,1)) = 0.25
		_MomentBias("Moment Bias", Range(0, 1)) = 0

        // Hidden properties
		//TODO: Editor
        [HideInInspector] _EmissionEnabled("__emissionenabled", Float) = 0.0
		[HideInInspector] _EmissionColor("Color", Color) = (0,0,0)
		[HideInInspector] _EmissionMap("Emission", 2D) = "white" {}
    }

    SubShader
    {
        Tags{"RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" "RenderPipeline" = "LightweightPipeline"  "Queue" = "Transparent+2000"}

		HLSLINCLUDE
		float2 _LogViewDepthMinDelta;
		float WarpDepth(float vd)
		{
			return (log(vd) - _LogViewDepthMinDelta.x) / _LogViewDepthMinDelta.y *2 - 1;
		}
		ENDHLSL
			Pass
		{
			Name "GenerateMoments"
			Tags {"LightMode" = "GenerateMoments"}
			Blend One One
			ZWrite Off
			Cull[_Cull]
			HLSLPROGRAM
			#pragma exclude_renderers d3d11_9x gles
			#pragma vertex LitPassVertex
			#pragma fragment MomentFragment
			#pragma target 3.0
			#pragma shader_feature _MOMENT4 _MOMENT6 _MOMENT8
			#pragma multi_compile _ _TRIGONOMETRIC
			#include "LitInput.hlsl"
			#include "LitForwardPass.hlsl"

			float4 _WrappingZoneParameters;
			/*! This function implements complex multiplication.*/
			float2 Multiply(float2 LHS, float2 RHS) {
				return float2(LHS.x*RHS.x - LHS.y*RHS.y, LHS.x*RHS.y + LHS.y*RHS.x);
			}

			struct Output
			{
				float b0 : COLOR0;
				float4 b1 : COLOR1;
#ifdef _MOMENT8
				float4 b2 : COLOR2;
#elif defined(_MOMENT6)
				float2 b2 : COLOR2;
#endif
			};
			void GenerateMoments(float vd, float t, out float b0, out float4 b1
#ifdef _MOMENT8
			, out float4 b2
#elif defined(_MOMENT6)
			, out float2 b2
#endif
			)
			{
				float a = -log(t);
				float d = WarpDepth(vd);

				b0 = a;
#ifdef _TRIGONOMETRIC
				float p = mad(d, _WrappingZoneParameters.y, _WrappingZoneParameters.y);
				float2 c;
				sincos(p, c.y, c.x);
				float2 c2 = Multiply(c, c);
				b1 = float4(c, c2) * a;
#ifdef _MOMENT8
				b2 = float4(Multiply(c, c2), Multiply(c2, c2)) * a;
#elif defined(_MOMENT6)
				b2 = Multiply(c, c2) * a;
#endif
#else
				float d2 = d * d;
				float d4 = d2 * d2;
				b1 = float4(d, d2, d2 * d, d4) * a;
#ifdef _MOMENT8
				b2 = b1 * d4;
#elif defined(_MOMENT6)
				b2 = b1.xy * d4;
#endif
#endif
			}

			Output MomentFragment(Varyings input)
			{
				Output o;
				half alpha = SampleAlbedoAlpha(input.uv.xy, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)).a * _Color.a;
				GenerateMoments(input.positionCS.z, 1 - alpha, o.b0, o.b1
#ifdef _MOMENT8
					, o.b2
#elif defined(_MOMENT6)
					, o.b2
#endif
				);
				return o;
			}

			ENDHLSL
		}

		Pass
		{
			Name "ResolveMoments"
			Tags {"LightMode" = "ResolveMoments"}
			Blend One One
			ZWrite Off
			Cull [_Cull]
			HLSLPROGRAM
			#pragma exclude_renderers d3d11_9x gles
			#pragma vertex LitPassVertex
			#pragma fragment MOITLitFragment
			#pragma target 3.0

			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _FADING_ON
			#pragma shader_feature _REQUIRE_UV2
			#pragma shader_feature _MOMENT4 _MOMENT6 _MOMENT8
			#pragma multi_compile _ _TRIGONOMETRIC
			#pragma shader_feature _MOMENT_HALF_PRECISION _MOMENT_SINGLE_PRECISION
			#pragma multi_compile _ _DEEP_SHADOW_MAPS
			#include "LitInput.hlsl"
			#include "LitForwardPass.hlsl"
			#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/MomentMath.hlsl"
			#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/TrigonometricMomentMath.hlsl"
#ifdef _MOMENT_SINGLE_PRECISION
			TEXTURE2D_FLOAT(_B0);
			TEXTURE2D_FLOAT(_B1);

#if defined(_MOMENT8) || defined(_MOMENT6)
			TEXTURE2D_FLOAT(_b2);
#endif
#else
			TEXTURE2D_HALF(_B0);
			TEXTURE2D_HALF(_B1);

#if defined(_MOMENT8) || defined(_MOMENT6)
			TEXTURE2D_HALF(_B2);
#endif
#endif
			SAMPLER(sampler_B0);
			SAMPLER(sampler_B1);
#if defined(_MOMENT8) || defined(_MOMENT6)
			SAMPLER(sampler_B2);
#endif
			float4 _B0_TexelSize;
			float _MomentBias;
			float _Overestimation;
			float4 _WrappingZoneParameters;


			void ResolveMoments(out float td, out float tt, float vd, float2 p)
			{
				float d = WarpDepth(vd);
				td = 1;
				tt = 1;
				float b0 = SAMPLE_TEXTURE2D(_B0, sampler_B0, p).r;
				//clip(b0 - 0.001);
				tt = exp(-b0);


				float4 b1 = SAMPLE_TEXTURE2D(_B1, sampler_B1, p);
				b1 /= b0;
#ifdef _MOMENT8
				float4 b2 = SAMPLE_TEXTURE2D(_B2, sampler_B2, p);
				b2 /= b0;
#ifdef _TRIGONOMETRIC
				float2 tb[4];
				tb[0] = b1.xy;
				tb[1] = b1.zw;
				tb[2] = b2.xy;
				tb[3] = b2.zw;
				td = ComputeTransmittanceTrigonometric(b0, tb, d, _MomentBias, _Overestimation, _WrappingZoneParameters);
#else
				float4 be = float4(b1.yw, b2.yw);
				float4 bo = float4(b1.xz, b2.xz);

				const float bias[8] = { 0, 0.75, 0, 0.67666666666666664, 0, 0.64, 0, 0.60030303030303034 };
				td = ComputeTransmittance(b0, be, bo, d, _MomentBias, _Overestimation, bias);
#endif
#elif defined(_MOMENT6)
				float2 b2 = SAMPLE_TEXTURE2D(_B2, sampler_B2, p).rg;
				b2 /= b0;
#ifdef _TRIGONOMETRIC
				float2 tb[3];
				tb[0] = b1.xy;
				tb[1] = b1.zw;
				tb[2] = b2.xy;
				td = ComputeTransmittanceTrigonometric(b0, tb, d, _MomentBias, _Overestimation, _WrappingZoneParameters);
#else
				float3 be = float3(b1.yw, b2.y);
				float3 bo = float3(b1.xz, b2.x);

				const float bias[6] = { 0, 0.48, 0, 0.451, 0, 0.45 };
				td = ComputeTransmittance(b0, be, bo, d, _MomentBias, _Overestimation, bias);
#endif
#else
#ifdef _TRIGONOMETRIC
				float2 tb[2];
				tb[0] = b1.xy;
				tb[1] = b1.zw;
				td = ComputeTransmittanceTrigonometric(b0, tb, d, _MomentBias, _Overestimation, _WrappingZoneParameters);
#else
				float2 be = b1.yw;
				float2 bo = b1.xz;

				const float4 bias = float4 (0, 0.375, 0, 0.375);
				td = ComputeTransmittance(b0, be, bo, d, _MomentBias, _Overestimation, bias);
#endif
#endif
			}
			struct Output
			{
				float4 moit : COLOR0;
			};

			Output MOITLitFragment(Varyings input)
			{
				Output o;

				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				SurfaceData surfaceData;
				InitializeStandardLitSurfaceData(input.uv, surfaceData);

				InputData inputData;
				InitializeInputData(input, surfaceData.normalTS, inputData);

				float td, tt;
				ResolveMoments(td, tt, input.positionCS.w, input.positionCS.xy * _B0_TexelSize.xy);
				
				half4 color = LightweightFragmentPBR(inputData, surfaceData.albedo, surfaceData.metallic,
					surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha);

				color.rgb = MixFog(color.rgb, inputData.fogCoord);
				color.rgb *= color.a;
				color *= td;
				o.moit = color;
				return o;
			}

			ENDHLSL
		}

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite [_ShadowCasterZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 4.5 //for deep shadow map

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON

			#pragma multi_compile _ _DEEP_SHADOW_CASTER

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "LitInput.hlsl"
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
		Pass
		{
			Name "DepthOnly"
			Tags{"LightMode" = "DepthOnly"}

			ZWrite On
			ColorMask 0
			Cull[_Cull]

			HLSLPROGRAM
				// Required to compile gles 2.0 with standard srp library
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x
				#pragma target 2.0

				#pragma vertex DepthOnlyVertex
				#pragma fragment DepthOnlyFragment

				// -------------------------------------
				// Material Keywords
				#pragma shader_feature _ALPHATEST_ON
				#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

				//--------------------------------------
				// GPU Instancing
				#pragma multi_compile_instancing

				#include "LitInput.hlsl"
				#include "DepthOnlyPass.hlsl"
				ENDHLSL
			}
    }
	
	FallBack "Hidden/InternalErrorShader"
    //CustomEditor "UnityEditor.Experimental.Rendering.LightweightPipeline.MomentOITLitShaderGUI"
}

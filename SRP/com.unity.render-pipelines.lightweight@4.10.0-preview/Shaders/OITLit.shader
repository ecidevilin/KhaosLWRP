// ------------------------------------------
// Only directional light is supported for lit particles
// No shadow
// No distortion
Shader "Lightweight Render Pipeline/OITLit"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _MetallicGlossMap("Metallic", 2D) = "white" {}
        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        // Hidden properties
        [HideInInspector] _Mode("__mode", Float) = 0.0
        [HideInInspector] _FlipbookMode("__flipbookmode", Float) = 0.0
        [HideInInspector] _LightingEnabled("__lightingenabled", Float) = 1.0
        [HideInInspector] _EmissionEnabled("__emissionenabled", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags{"RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" "RenderPipeline" = "LightweightPipeline" }


        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode" = "LightweightForward"}
			Blend One One , Zero OneMinusSrcAlpha
			ZWrite Off
			Cull[_Cull]
            HLSLPROGRAM
            #pragma exclude_renderers d3d11_9x gles
            #pragma vertex LitPassVertex
            #pragma fragment OITLitFragment
            #pragma target 3.0

            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature _FADING_ON
            #pragma shader_feature _REQUIRE_UV2
			//#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

			#include "LitInput.hlsl"
            #include "LitForwardPass.hlsl"

			//inline float weight(float z, float a) {
			//	return clamp(pow(min(1.0, a * 10.0) + 0.01, 3.0) * 1e8 * pow(1.0 - z * 0.9, 3.0), 1e-2, 3e3);
			//}   

			inline float weight0(float z, float a)
			{
				return a * clamp(10 / (1e-5 + pow(z / 5, 2) + pow(z / 200, 6)), 1e-2, 3e3);
			}
			inline float weight1(float z, float a)
			{
				return a * clamp(10 / (1e-5 + pow(z / 10, 3) + pow(z / 200, 6)), 1e-2, 3e3);
			}
			inline float weight2(float z, float a)
			{
				return a * clamp(0.03f / (1e-5 + pow(z / 200, 4)), 1e-2, 3e3);
			}
			inline float weight3(float z, float a)
			{
				float n = _ProjectionParams.y;
				float f = _ProjectionParams.z;
				return a * max(1e-2, 3e3 * pow(1 - (n * f / z - f) / (n - f), 3));
			}

			struct Output
			{
				float4 accumColor : COLOR0;
				float4 accumGI : COLOR1;
				float accumAlpha : COLOR2;
			};

			half4 LightweightFragmentPBRWithGISeparated(InputData inputData, half3 albedo, half metallic, half3 specular,
				half smoothness, half occlusion, half3 emission, half alpha, out half3 gi)
			{
				BRDFData brdfData;
				InitializeBRDFData(albedo, metallic, specular, smoothness, alpha, brdfData);

#if defined(_MAIN_CHARACTER_SHADOWS) || defined(_DEEP_SHADOW_MAPS)
				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.shadowCoord2, inputData.shadowCoord3);
#else
				Light mainLight = GetMainLight(inputData.shadowCoord);
#endif

				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, half4(0, 0, 0, 0));

				gi = GlobalIllumination(brdfData, inputData.bakedGI, occlusion, inputData.normalWS, inputData.viewDirectionWS);
				half3 color = LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

#ifdef _ADDITIONAL_LIGHTS
				int pixelLightCount = GetAdditionalLightsCount();
				for (int i = 0; i < pixelLightCount; ++i)
				{
					Light light = GetAdditionalLight(i, inputData.positionWS);
					gi += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
				}
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
				gi += inputData.vertexLighting * brdfData.diffuse;
#endif

				gi += emission;
				return half4(color, alpha);
			}

			Output OITLitFragment(Varyings input)
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				SurfaceData surfaceData;
				InitializeStandardLitSurfaceData(input.uv, surfaceData);

				InputData inputData;
				InitializeInputData(input, surfaceData.normalTS, inputData);
				
				half3 gi;
				half4 color = LightweightFragmentPBRWithGISeparated(inputData, surfaceData.albedo, surfaceData.metallic,
					surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha, gi);
				
				color.rgb = MixFog(color.rgb, inputData.fogCoord);
				gi = MixFog(gi, inputData.fogCoord);

				float w = weight0(input.positionCS.z, color.a);

				Output o;
				o.accumColor = float4(color.rgb * w, color.a);
				o.accumGI = float4(gi * w, color.a);
				o.accumAlpha = color.a * w;
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
    //CustomEditor "UnityEditor.Experimental.Rendering.LightweightPipeline.ParticlesLitShaderGUI"
}

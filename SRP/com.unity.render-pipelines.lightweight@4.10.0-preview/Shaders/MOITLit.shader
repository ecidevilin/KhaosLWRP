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

		HLSLINCLUDE
		float2 _ViewZMaxMin;
		float WarpDepth(float vd)
		{
			return (log(vd) - log(_ViewZMaxMin.y)) / (log(_ViewZMaxMin.x) - log(_ViewZMaxMin.y)) * 2 - 1;
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
			#pragma shader_feature _MOMENT4 _MOMENT8
			#include "LitInput.hlsl"
			#include "LitForwardPass.hlsl"

			struct Output
			{
				float b0 : COLOR0;
				float4 b1 : COLOR1;
#ifdef _MOMENT8
				float4 b2 : COLOR2;
#endif
			};
			void GenerateMoments(float vd, float t, out float b0, out float4 b1
#ifdef _MOMENT8
			, out float4 b2
#endif
			)
			{
				float d = WarpDepth(vd);
				float a = -log(t);

				b0 = a;
				float d2 = d * d;
				float d4 = d2 * d2;
				b1 = float4(d, d2, d2 * d, d4) * a;
#ifdef _MOMENT8
				b2 = b1 * d4;
#endif
			}

			Output MomentFragment(Varyings input)
			{
				Output o;
				half alpha = SampleAlbedoAlpha(input.uv.xy, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)).a * _Color.a;
				GenerateMoments(input.positionCS.w, 1 - alpha, o.b0, o.b1
#ifdef _MOMENT8
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
			#pragma shader_feature _MOMENT4 _MOMENT8
			#pragma multi_compile _ _DEEP_SHADOW_MAPS
			#include "LitInput.hlsl"
			#include "LitForwardPass.hlsl"

			TEXTURE2D_FLOAT(_b0);
			SAMPLER(sampler_b0);
			TEXTURE2D_FLOAT(_b1);
			SAMPLER(sampler_b1);

			float4 _b0_TexelSize;
#ifdef _MOMENT8
			TEXTURE2D_FLOAT(_b2);
			SAMPLER(sampler_b2);
#endif
			float _MomentBias;
			float _Overestimation;

			float ComputeTransmittance(float b_0, float2 b_even, float2 b_odd, float depth, float bias, float overestimation, float4 bias_vector)
			{
				float4 b = float4(b_odd.x, b_even.x, b_odd.y, b_even.y);
				// Bias input data to avoid artifacts
				b = lerp(b, bias_vector, bias);
				float3 z;
				z[0] = depth;

				// Compute a Cholesky factorization of the Hankel matrix B storing only non-
				// trivial entries or related products
				float L21D11 = mad(-b[0], b[1], b[2]);
				float D11 = mad(-b[0], b[0], b[1]);
				float InvD11 = 1.0f / D11;
				float L21 = L21D11 * InvD11;
				float SquaredDepthVariance = mad(-b[1], b[1], b[3]);
				float D22 = mad(-L21D11, L21, SquaredDepthVariance);

				// Obtain a scaled inverse image of bz=(1,z[0],z[0]*z[0])^T
				float3 c = float3(1.0f, z[0], z[0] * z[0]);
				// Forward substitution to solve L*c1=bz
				c[1] -= b.x;
				c[2] -= b.y + L21 * c[1];
				// Scaling to solve D*c2=c1
				c[1] *= InvD11;
				c[2] /= D22;
				// Backward substitution to solve L^T*c3=c2
				c[1] -= L21 * c[2];
				c[0] -= dot(c.yz, b.xy);
				// Solve the quadratic equation c[0]+c[1]*z+c[2]*z^2 to obtain solutions 
				// z[1] and z[2]
				float InvC2 = 1.0f / c[2];
				float p = c[1] * InvC2;
				float q = c[0] * InvC2;
				float D = (p*p*0.25f) - q;
				float r = sqrt(D);
				z[1] = -p * 0.5f - r;
				z[2] = -p * 0.5f + r;
				// Compute the absorbance by summing the appropriate weights
				float3 polynomial;
				float3 weight_factor = float3(overestimation, (z[1] < z[0]) ? 1.0f : 0.0f, (z[2] < z[0]) ? 1.0f : 0.0f);
				float f0 = weight_factor[0];
				float f1 = weight_factor[1];
				float f2 = weight_factor[2];
				float f01 = (f1 - f0) / (z[1] - z[0]);
				float f12 = (f2 - f1) / (z[2] - z[1]);
				float f012 = (f12 - f01) / (z[2] - z[0]);
				polynomial[0] = f012;
				polynomial[1] = polynomial[0];
				polynomial[0] = f01 - polynomial[0] * z[1];
				polynomial[2] = polynomial[1];
				polynomial[1] = polynomial[0] - polynomial[1] * z[0];
				polynomial[0] = f0 - polynomial[0] * z[0];
				float absorbance = polynomial[0] + dot(b.xy, polynomial.yz);;
				// Turn the normalized absorbance into transmittance
				return saturate(exp(-b_0 * absorbance));
			}
			/*! Given coefficients of a quadratic polynomial A*x^2+B*x+C, this function
			outputs its two real roots.*/
			float2 solveQuadratic(float3 coeffs)
			{
				coeffs[1] *= 0.5;

				float x1, x2, tmp;

				tmp = (coeffs[1] * coeffs[1] - coeffs[0] * coeffs[2]);
				if (coeffs[1] >= 0) {
					tmp = sqrt(tmp);
					x1 = (-coeffs[2]) / (coeffs[1] + tmp);
					x2 = (-coeffs[1] - tmp) / coeffs[0];
				}
				else {
					tmp = sqrt(tmp);
					x1 = (-coeffs[1] + tmp) / coeffs[0];
					x2 = coeffs[2] / (-coeffs[1] + tmp);
				}
				return float2(x1, x2);
			}
			/*! Given coefficients of a cubic polynomial 
			coeffs[0]+coeffs[1]*x+coeffs[2]*x^2+coeffs[3]*x^3 with three real roots, 
			this function returns the root of least magnitude.*/
			float solveCubicBlinnSmallest(float4 coeffs)
			{
				coeffs.xyz /= coeffs.w;
				coeffs.yz /= 3.0;

				float3 delta = float3(mad(-coeffs.z, coeffs.z, coeffs.y), mad(-coeffs.z, coeffs.y, coeffs.x), coeffs.z * coeffs.x - coeffs.y * coeffs.y);
				float discriminant = 4.0 * delta.x * delta.z - delta.y * delta.y;

				float2 depressed = float2(delta.z, -coeffs.x * delta.y + 2.0 * coeffs.y * delta.z);
				float theta = abs(atan2(coeffs.x * sqrt(discriminant), -depressed.y)) / 3.0;
				float2 sin_cos;
				sincos(theta, sin_cos.x, sin_cos.y);
				float tmp = 2.0 * sqrt(-depressed.x);
				float2 x = float2(tmp * sin_cos.y, tmp * (-0.5 * sin_cos.y - 0.5 * sqrt(3.0) * sin_cos.x));
				float2 s = (x.x + x.y < 2.0 * coeffs.y) ? float2(-coeffs.x, x.x + coeffs.y) : float2(-coeffs.x, x.y + coeffs.y);

				return  s.x / s.y;
			}
			/*! Given coefficients of a quartic polynomial
				coeffs[0]+coeffs[1]*x+coeffs[2]*x^2+coeffs[3]*x^3+coeffs[4]*x^4 with four
				real roots, this function returns all roots.*/
			float4 solveQuarticNeumark(float coeffs[5])
			{
				// Normalization
				float B = coeffs[3] / coeffs[4];
				float C = coeffs[2] / coeffs[4];
				float D = coeffs[1] / coeffs[4];
				float E = coeffs[0] / coeffs[4];

				// Compute coefficients of the cubic resolvent
				float P = -2.0*C;
				float Q = C * C + B * D - 4.0*E;
				float R = D * D + B * B*E - B * C*D;

				// Obtain the smallest cubic root
				float y = solveCubicBlinnSmallest(float4(R, Q, P, 1.0));

				float BB = B * B;
				float fy = 4.0 * y;
				float BB_fy = BB - fy;

				float Z = C - y;
				float ZZ = Z * Z;
				float fE = 4.0 * E;
				float ZZ_fE = ZZ - fE;

				float G, g, H, h;
				// Compute the coefficients of the quadratics adaptively using the two 
				// proposed factorizations by Neumark. Choose the appropriate 
				// factorizations using the heuristic proposed by Herbison-Evans.
				if (y < 0 || (ZZ + fE) * BB_fy > ZZ_fE * (BB + fy)) {
					float tmp = sqrt(BB_fy);
					G = (B + tmp) * 0.5;
					g = (B - tmp) * 0.5;

					tmp = (B*Z - 2.0*D) / (2.0*tmp);
					H = mad(Z, 0.5, tmp);
					h = mad(Z, 0.5, -tmp);
				}
				else {
					float tmp = sqrt(ZZ_fE);
					H = (Z + tmp) * 0.5;
					h = (Z - tmp) * 0.5;

					tmp = (B*Z - 2.0*D) / (2.0*tmp);
					G = mad(B, 0.5, tmp);
					g = mad(B, 0.5, -tmp);
				}
				// Solve the quadratics
				return float4(solveQuadratic(float3(1.0, G, H)), solveQuadratic(float3(1.0, g, h)));
			}

			float ComputeTransmittance(float b_0, float4 b_even, float4 b_odd, float depth, float bias, float overestimation, float bias_vector[8])
			{
				float b[8] = { b_odd.x, b_even.x, b_odd.y, b_even.y, b_odd.z, b_even.z, b_odd.w, b_even.w };
				// Bias input data to avoid artifacts
				[unroll] for (int i = 0; i != 8; ++i) {
					b[i] = lerp(b[i], bias_vector[i], bias);
				}

				float z[5];
				z[0] = depth;

				// Compute a Cholesky factorization of the Hankel matrix B storing only non-trivial entries or related products
				float D22 = mad(-b[0], b[0], b[1]);
				float InvD22 = 1.0 / D22;
				float L32D22 = mad(-b[1], b[0], b[2]);
				float L32 = L32D22 * InvD22;
				float L42D22 = mad(-b[2], b[0], b[3]);
				float L42 = L42D22 * InvD22;
				float L52D22 = mad(-b[3], b[0], b[4]);
				float L52 = L52D22 * InvD22;

				float D33 = mad(-L32, L32D22, mad(-b[1], b[1], b[3]));
				float InvD33 = 1.0 / D33;
				float L43D33 = mad(-L42, L32D22, mad(-b[2], b[1], b[4]));
				float L43 = L43D33 * InvD33;
				float L53D33 = mad(-L52, L32D22, mad(-b[3], b[1], b[5]));
				float L53 = L53D33 * InvD33;

				float D44 = mad(-b[2], b[2], b[5]) - dot(float2(L42, L43), float2(L42D22, L43D33));
				float InvD44 = 1.0 / D44;
				float L54D44 = mad(-b[3], b[2], b[6]) - dot(float2(L52, L53), float2(L42D22, L43D33));
				float L54 = L54D44 * InvD44;

				float D55 = mad(-b[3], b[3], b[7]) - dot(float3(L52, L53, L54), float3(L52D22, L53D33, L54D44));
				float InvD55 = 1.0 / D55;

				// Construct the polynomial whose roots have to be points of support of the
				// Canonical distribution:
				// bz = (1,z[0],z[0]^2,z[0]^3,z[0]^4)^T
				float c[5];
				c[0] = 1.0;
				c[1] = z[0];
				c[2] = c[1] * z[0];
				c[3] = c[2] * z[0];
				c[4] = c[3] * z[0];

				// Forward substitution to solve L*c1 = bz
				c[1] -= b[0];
				c[2] -= mad(L32, c[1], b[1]);
				c[3] -= b[2] + dot(float2(L42, L43), float2(c[1], c[2]));
				c[4] -= b[3] + dot(float3(L52, L53, L54), float3(c[1], c[2], c[3]));

				// Scaling to solve D*c2 = c1
				//c = c .*[1, InvD22, InvD33, InvD44, InvD55];
				c[1] *= InvD22;
				c[2] *= InvD33;
				c[3] *= InvD44;
				c[4] *= InvD55;

				// Backward substitution to solve L^T*c3 = c2
				c[3] -= L54 * c[4];
				c[2] -= dot(float2(L53, L43), float2(c[4], c[3]));
				c[1] -= dot(float3(L52, L42, L32), float3(c[4], c[3], c[2]));
				c[0] -= dot(float4(b[3], b[2], b[1], b[0]), float4(c[4], c[3], c[2], c[1]));

				// Solve the quartic equation
				float4 zz = solveQuarticNeumark(c);
				z[1] = zz[0];
				z[2] = zz[1];
				z[3] = zz[2];
				z[4] = zz[3];

				// Compute the absorbance by summing the appropriate weights
				float4 weigth_factor = (float4(z[1], z[2], z[3], z[4]) <= z[0].xxxx);
				// Construct an interpolation polynomial
				float f0 = overestimation;
				float f1 = weigth_factor[0];
				float f2 = weigth_factor[1];
				float f3 = weigth_factor[2];
				float f4 = weigth_factor[3];
				float f01 = (f1 - f0) / (z[1] - z[0]);
				float f12 = (f2 - f1) / (z[2] - z[1]);
				float f23 = (f3 - f2) / (z[3] - z[2]);
				float f34 = (f4 - f3) / (z[4] - z[3]);
				float f012 = (f12 - f01) / (z[2] - z[0]);
				float f123 = (f23 - f12) / (z[3] - z[1]);
				float f234 = (f34 - f23) / (z[4] - z[2]);
				float f0123 = (f123 - f012) / (z[3] - z[0]);
				float f1234 = (f234 - f123) / (z[4] - z[1]);
				float f01234 = (f1234 - f0123) / (z[4] - z[0]);

				float Polynomial_0;
				float4 Polynomial;
				// f0123 + f01234 * (z - z3)
				Polynomial_0 = mad(-f01234, z[3], f0123);
				Polynomial[0] = f01234;
				// * (z - z2) + f012
				Polynomial[1] = Polynomial[0];
				Polynomial[0] = mad(-Polynomial[0], z[2], Polynomial_0);
				Polynomial_0 = mad(-Polynomial_0, z[2], f012);
				// * (z - z1) + f01
				Polynomial[2] = Polynomial[1];
				Polynomial[1] = mad(-Polynomial[1], z[1], Polynomial[0]);
				Polynomial[0] = mad(-Polynomial[0], z[1], Polynomial_0);
				Polynomial_0 = mad(-Polynomial_0, z[1], f01);
				// * (z - z0) + f1
				Polynomial[3] = Polynomial[2];
				Polynomial[2] = mad(-Polynomial[2], z[0], Polynomial[1]);
				Polynomial[1] = mad(-Polynomial[1], z[0], Polynomial[0]);
				Polynomial[0] = mad(-Polynomial[0], z[0], Polynomial_0);
				Polynomial_0 = mad(-Polynomial_0, z[0], f0);
				float absorbance = Polynomial_0 + dot(Polynomial, float4(b[0], b[1], b[2], b[3]));
				// Turn the normalized absorbance into transmittance
				return saturate(exp(-b_0 * absorbance));
			}

			void ResolveMoments(out float td, out float tt, float vd, float2 p)
			{
				float d = WarpDepth(vd);
				td = 1;
				tt = 1;
				float b0 = SAMPLE_TEXTURE2D(_b0, sampler_b0, p).r;
				//clip(b0 - 0.001);
				tt = exp(-b0);


				float4 b1 = SAMPLE_TEXTURE2D(_b1, sampler_b1, p);
				b1 /= b0;
#ifndef _MOMENT8
				float2 be = b1.yw;
				float2 bo = b1.xz;

				const float4 bias = float4 (0, 0.375, 0, 0.375);
				td = ComputeTransmittance(b0, be, bo, d, _MomentBias, _Overestimation, bias);
#else
				float4 b2 = SAMPLE_TEXTURE2D(_b2, sampler_b2, p);
				b2 /= b0;
				float4 be = float4(b1.yw, b2.yw);
				float4 bo = float4(b1.xz, b2.xz);

				const float bias[8] = { 0, 0.75, 0, 0.67666666666666664, 0, 0.64, 0, 0.60030303030303034 };
				td = ComputeTransmittance(b0, be, bo, d, _MomentBias, _Overestimation, bias);
#endif
			}
			struct Output
			{
				float4 moit : COLOR0;
#ifdef _DEEP_SHADOW_MAPS
				float3 gial : COLOR1;
#endif
			};

			half4 LightweightFragmentPBRWithGISeparated(InputData inputData, half3 albedo, half metallic, half3 specular,
				half smoothness, half occlusion, half3 emission, half alpha, out half3 gial)
			{
				BRDFData brdfData;
				InitializeBRDFData(albedo, metallic, specular, smoothness, alpha, brdfData);

#if defined(_MAIN_CHARACTER_SHADOWS) || defined(_DEEP_SHADOW_MAPS)
				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.shadowCoord2, inputData.shadowCoord3);
#else
				Light mainLight = GetMainLight(inputData.shadowCoord);
#endif

				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, half4(0, 0, 0, 0));

				gial = GlobalIllumination(brdfData, inputData.bakedGI, occlusion, inputData.normalWS, inputData.viewDirectionWS);
				half3 color = LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

#ifdef _ADDITIONAL_LIGHTS
				int pixelLightCount = GetAdditionalLightsCount();
				for (int i = 0; i < pixelLightCount; ++i)
				{
					Light light = GetAdditionalLight(i, inputData.positionWS);
					gial += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
				}
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
				gial += inputData.vertexLighting * brdfData.diffuse;
#endif

				gial += emission;
				return half4(color, alpha);
			}

			Output MOITLitFragment(Varyings input)
			{
				Output o;

				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				SurfaceData surfaceData;
				InitializeStandardLitSurfaceData(input.uv, surfaceData);

				InputData inputData;
				InitializeInputData(input, surfaceData.normalTS, inputData);

				float tt, td;
				ResolveMoments(td, tt, input.positionCS.w, input.positionCS.xy * _b0_TexelSize.xy);

#ifdef _DEEP_SHADOW_MAPS
				half3 gial;
				half4 color = LightweightFragmentPBRWithGISeparated(inputData, surfaceData.albedo, surfaceData.metallic,
					surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha
					, gial);
#else
				half4 color = LightweightFragmentPBR(inputData, surfaceData.albedo, surfaceData.metallic,
					surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha);
#endif

				color.rgb = MixFog(color.rgb, inputData.fogCoord);
				color.rgb *= color.a;
				color *= td;
				o.moit = color;

#ifdef _DEEP_SHADOW_MAPS
				gial = MixFog(gial, inputData.fogCoord);
				gial *= color.a * td;
				o.gial = gial;
#endif
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

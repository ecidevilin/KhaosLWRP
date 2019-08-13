#ifndef UNITY_CAMERA_DEPTH
#define UNITY_CAMERA_DEPTH

#if defined(_DEPTH_MSAA_2) || defined(_DEPTH_MSAA_4)
Texture2DMS<float, 1> _CameraDepthTexture;
float4 _CameraDepthTexture_TexelSize;
#else
TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;
#endif

float SampleCameraDepth(float2 uv)
{
#if defined(_DEPTH_MSAA_2) || defined(_DEPTH_MSAA_4)
	return _CameraDepthTexture.Load(uv*_CameraDepthTexture_TexelSize.zw, 0);
#else
	return SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0);
#endif
}

#endif // UNITY_CAMERA_DEPTH



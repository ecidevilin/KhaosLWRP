#ifndef UNITY_CAMERA_DEPTH
#define UNITY_CAMERA_DEPTH

#ifdef CAMERA_DEPTH_MSAA
Texture2DMS<float, 1> _CameraDepthTexture;
float4 _CameraDepthTexture_TexelSize;
#else
TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;
#endif

float SampleCameraDepth(float2 uv)
{
#ifdef CAMERA_DEPTH_MSAA
	return _CameraDepthTexture.Load(uv*_CameraDepthTexture_TexelSize.zw, 0);
#else
	return SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0);
#endif
}

#endif // UNITY_CAMERA_DEPTH



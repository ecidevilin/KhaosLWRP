#ifndef LIGHTWEIGHT_DEEP_SHADOW_MAPS_INCLUDED
#define LIGHTWEIGHT_DEEP_SHADOW_MAPS_INCLUDED


#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"

#define BITS_24_MAX 16777215
#define BITS_24_MAX_RECIPROCAL 1.0f/BITS_24_MAX
#define BITS_8_MAX 255
#define BITS_8_MAX_RECIPROCAL 1.0f/BITS_8_MAX

inline uint PackDepthAndAlpha(float depth, float alpha)
{
	uint z24 = depth * BITS_24_MAX;
	uint t8 = -alpha * BITS_8_MAX + BITS_8_MAX;
	return (t8 << 24) | z24;
}

inline float GetDepthFromPackedData(uint packed)
{
	return (float)(packed & BITS_24_MAX) * BITS_24_MAX_RECIPROCAL;
}

inline float GetTransparencyFromPackedData(uint packed)
{
	return (float)(packed >> 24) * BITS_8_MAX_RECIPROCAL;
}

inline void UnpackDepthAndTransparency(uint packed, out float depth, out float transparency)
{
	depth = GetDepthFromPackedData(packed);
	transparency = GetTransparencyFromPackedData(packed);
}

#endif

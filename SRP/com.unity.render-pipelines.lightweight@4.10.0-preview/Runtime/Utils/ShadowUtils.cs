using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public struct ShadowSliceData
    {
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 shadowTransform;
        public int offsetX;
        public int offsetY;
        public int resolution;

        public void Clear()
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            shadowTransform = Matrix4x4.identity;
            offsetX = offsetY = 0;
            resolution = 1024;
        }
    }

    public static class ShadowUtils
    {
        public static bool ExtractDirectionalLightMatrix(ref CullResults cullResults, ref ShadowData shadowData, int shadowLightIndex, int cascadeIndex, int shadowResolution, float shadowNearPlane, out Vector4 cascadeSplitDistance, out ShadowSliceData shadowSliceData, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix)
        {
            ShadowSplitData splitData;
            bool success = cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex,
                cascadeIndex, shadowData.mainLightShadowCascadesCount, shadowData.mainLightShadowCascadesSplit, shadowResolution, shadowNearPlane, out viewMatrix, out projMatrix,
                out splitData);

            cascadeSplitDistance = splitData.cullingSphere;
            shadowSliceData.offsetX = (cascadeIndex % 2) * shadowResolution;
            shadowSliceData.offsetY = (cascadeIndex / 2) * shadowResolution;
            shadowSliceData.resolution = shadowResolution;
            shadowSliceData.viewMatrix = viewMatrix;
            shadowSliceData.projectionMatrix = projMatrix;
            shadowSliceData.shadowTransform = GetShadowTransform(projMatrix, viewMatrix);

            // If we have shadow cascades baked into the atlas we bake cascade transform
            // in each shadow matrix to save shader ALU and L/S
            if (shadowData.mainLightShadowCascadesCount > 1)
                ApplySliceTransform(ref shadowSliceData, shadowData.mainLightShadowmapWidth, shadowData.mainLightShadowmapHeight);

            return success;
        }

        public static bool ExtractSpotLightMatrix(ref CullResults cullResults, ref ShadowData shadowData, int shadowLightIndex, out Matrix4x4 shadowMatrix, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix)
        {
            ShadowSplitData splitData;
            bool success = cullResults.ComputeSpotShadowMatricesAndCullingPrimitives(shadowLightIndex, out viewMatrix, out projMatrix, out splitData);
            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }

        public static void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData, ref DrawShadowsSettings settings,
            Matrix4x4 proj, Matrix4x4 view)
        {
            cmd.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
            cmd.EnableScissorRect(new Rect(shadowSliceData.offsetX + 4, shadowSliceData.offsetY + 4, shadowSliceData.resolution - 8, shadowSliceData.resolution - 8));

            cmd.SetViewProjectionMatrices(view, proj);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            context.DrawShadows(ref settings);
            cmd.DisableScissorRect();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static int GetMaxTileResolutionInAtlas(int atlasWidth, int atlasHeight, int tileCount)
        {
            int resolution = Mathf.Min(atlasWidth, atlasHeight);
            int currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            while (currentTileCount < tileCount)
            {
                resolution = resolution >> 1;
                currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            }
            return resolution;
        }

        public static void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }

        public static Vector4 GetShadowBias(ref VisibleLight shadowLight, int shadowLightIndex, ref ShadowData shadowData, Matrix4x4 lightProjectionMatrix, float shadowResolution)
        {
            if (shadowLightIndex < 0 || shadowLightIndex >= shadowData.bias.Count)
            {
                Debug.LogWarning(string.Format("{0} is not a valid light index.", shadowLightIndex));
                return Vector4.zero;
            }

            float frustumSize;
            if (shadowLight.lightType == LightType.Directional)
            {
                // Frustum size is guaranteed to be a cube as we wrap shadow frustum around a sphere
                frustumSize = 2.0f / lightProjectionMatrix.m00;
            }
            else if (shadowLight.lightType == LightType.Spot)
            {
                // For perspective projections, shadow texel size varies with depth
                // It will only work well if done in receiver side in the pixel shader. Currently LWRP
                // do bias on caster side in vertex shader. When we add shader quality tiers we can properly
                // handle this. For now, as a poor approximation we do a constant bias and compute the size of
                // the frustum as if it was orthogonal considering the size at mid point between near and far planes.
                // Depending on how big the light range is, it will be good enough with some tweaks in bias
                frustumSize = Mathf.Tan(shadowLight.spotAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range;
            }
            else
            {
                Debug.LogWarning("Only spot and directional shadow casters are supported in lightweight pipeline");
                frustumSize = 0.0f;
            }

            // depth and normal bias scale is in shadowmap texel size in world space
            float texelSize = frustumSize / shadowResolution;
            float depthBias = -shadowData.bias[shadowLightIndex].x * texelSize;
            float normalBias = -shadowData.bias[shadowLightIndex].y * texelSize;
            
            if (shadowData.supportsSoftShadows)
            {
                // TODO: depth and normal bias assume sample is no more than 1 texel away from shadowmap
                // This is not true with PCF. Ideally we need to do either
                // cone base bias (based on distance to center sample)
                // or receiver place bias based on derivatives.
                // For now we scale it by the PCF kernel size (5x5)
                const float kernelRadius = 2.5f;
                depthBias *= kernelRadius;
                normalBias *= kernelRadius;
            }

            return new Vector4(depthBias, normalBias, 0.0f, 0.0f);
        }

        public static void SetupShadowCasterConstantBuffer(CommandBuffer cmd, ref VisibleLight shadowLight, Vector4 shadowBias)
        {
            Vector3 lightDirection = -shadowLight.localToWorld.GetColumn(2);
            cmd.SetGlobalVector("_ShadowBias", shadowBias);
            cmd.SetGlobalVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
        }

        [Obsolete("SetupShadowCasterConstants is deprecated, use SetupShadowCasterConstantBuffer instead")]
        public static void SetupShadowCasterConstants(CommandBuffer cmd, ref VisibleLight visibleLight, Matrix4x4 proj, float cascadeResolution)
        {
            Light light = visibleLight.light;
            float bias = 0.0f;
            float normalBias = 0.0f;

            if (visibleLight.lightType == LightType.Directional)
            {
                // Currently only square POT cascades resolutions are used.
                // We scale normalBias
                double frustumWidth = 2.0 / (double)proj.m00;
                double frustumHeight = 2.0 / (double)proj.m11;
                float texelSizeX = (float)(frustumWidth / (double)cascadeResolution);
                float texelSizeY = (float)(frustumHeight / (double)cascadeResolution);
                float texelSize = Mathf.Max(texelSizeX, texelSizeY);

                // Depth Bias - bias shadow is specified in terms of shadowmap pixels.
                // bias = 1 means the shadowmap is offseted 1 texel world space size in the direciton of the light
                bias = -light.shadowBias * texelSize;

                // Since we are applying normal bias on caster side we want an inset normal offset
                // thus we use a negative normal bias.
                normalBias = -light.shadowNormalBias * texelSize * 3.65f;
            }
            else if (visibleLight.lightType == LightType.Spot)
            {
                float sign = (SystemInfo.usesReversedZBuffer) ? -1.0f : 1.0f;
                bias = light.shadowBias * sign;
                normalBias = 0.0f;
            }
            else
            {
                Debug.LogWarning("Only spot and directional shadow casters are supported in lightweight pipeline");
            }

            Vector3 lightDirection = -visibleLight.localToWorld.GetColumn(2);
            cmd.SetGlobalVector("_ShadowBias", new Vector4(bias, normalBias, 0.0f, 0.0f));
            cmd.SetGlobalVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
        }

        public static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
        {
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            if (SystemInfo.usesReversedZBuffer)
            {
                proj.m20 = -proj.m20;
                proj.m21 = -proj.m21;
                proj.m22 = -proj.m22;
                proj.m23 = -proj.m23;
            }

            Matrix4x4 worldToShadow = proj * view;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }

        private static List<string> _tags = new List<string>();

        public static void GetVPMatrixWithWorldBounds(Light light, Bounds bounds, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out Vector4 cullingSphere)
        {

            Vector3 center = bounds.center;
            float radius = bounds.extents.magnitude;
            cullingSphere = center;
            cullingSphere.w = radius;
            Vector3 intialLightPos = center;// - light.transform.forward.normalized * radius;

            projMatrix = Matrix4x4.Ortho(-radius, radius, -radius, radius, radius * 0.1f, radius * 2.3f);
            projMatrix = GL.GetGPUProjectionMatrix(projMatrix, false);
            viewMatrix = light.transform.worldToLocalMatrix;
            Vector4 viewTsl = -viewMatrix.MultiplyVector(intialLightPos);
            viewTsl.z -= radius * 1.2f;
            viewTsl.w = 1;
            viewMatrix.SetColumn(3, viewTsl);
        }

        public static bool GetVPMatrixWithTag(Light light, string tag, List<Renderer> renderers, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out Vector4 cullingSphere)
        {
            _tags.Clear();
            _tags.Add(tag);
            return GetVPMatrixWithTags(light, _tags, renderers, out viewMatrix, out projMatrix, out cullingSphere);
        }

        public static bool GetVPMatrixWithTags(Light light, List<string> tags, List<Renderer> renderers, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out Vector4 cullingSphere)
        {
            viewMatrix = Matrix4x4.identity;
            projMatrix = Matrix4x4.identity;
            cullingSphere = Vector4.zero;
            renderers.Clear();
            Bounds bounds = new Bounds();
            foreach (var tag in tags)
            {
                GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
                if (null == objs || 0 == objs.Length)
                {
                    continue;
                }
                foreach (var p in objs)
                {
                    Renderer r = p.GetComponent<Renderer>();
                    if (null != r && r.enabled && r.shadowCastingMode != ShadowCastingMode.Off)
                    {
                        if (r is SkinnedMeshRenderer)
                        {
                            (r as SkinnedMeshRenderer).sharedMesh.RecalculateBounds();
                        }
                        Bounds rb = r.bounds;
                        if (0 != renderers.Count)
                        {
                            bounds.Encapsulate(rb);
                        }
                        else
                        {
                            bounds = rb;
                        }
                        renderers.Add(r);
                    }
                }
            }
            if (0 == renderers.Count)
            {
                return false;
            }

            GetVPMatrixWithWorldBounds(light, bounds, out viewMatrix, out projMatrix, out cullingSphere);

            return true;
        }

        //public static bool GetVPMatrixWithRenderQueue(Light light, RenderQueueRange range, 
        //    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out Vector4 cullingSphere,
        //    bool checkShadowCastingMode)
        //{
        //    viewMatrix = Matrix4x4.identity;
        //    projMatrix = Matrix4x4.identity;
        //    cullingSphere = Vector4.zero;
        //    bool b = false;
        //    Bounds bounds = new Bounds();

        //    Renderer[] coms = Renderer.FindObjectsOfType<Renderer>();
            
        //    if (null == coms || 0 == coms.Length)
        //    {
        //        return false;
        //    }
        //    foreach (var p in coms)
        //    {
        //        Renderer r = p.GetComponent<Renderer>();
        //        if (null != r && r.enabled && 
        //            (!checkShadowCastingMode || (checkShadowCastingMode && r.shadowCastingMode != ShadowCastingMode.Off))
        //            && r.sharedMaterial.renderQueue >= range.min && r.sharedMaterial.renderQueue <= range.max)
        //        {
        //            if (r is SkinnedMeshRenderer)
        //            {
        //                (r as SkinnedMeshRenderer).sharedMesh.RecalculateBounds();
        //            }
        //            Bounds rb = r.bounds;
        //            if (b)
        //            {
        //                bounds.Encapsulate(rb);
        //            }
        //            else
        //            {
        //                bounds = rb;
        //                b = true;
        //            }
        //        }
        //    }
        //    if (!b)
        //    {
        //        return false;
        //    }

        //    GetVPMatrixWithWorldBounds(light, bounds, out viewMatrix, out projMatrix, out cullingSphere);

        //    return true;
        //}

    }
}

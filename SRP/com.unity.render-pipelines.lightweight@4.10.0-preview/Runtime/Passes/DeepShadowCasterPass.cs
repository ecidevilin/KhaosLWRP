using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class DeepShadowCasterPass : ScriptableRenderPass
    {
        const string k_RenderDeepShadowCaster = "Render Deep Shadow Caster";

        private ComputeBuffer _CountBuffer;
        private ComputeBuffer _DataBuffer;

        private ComputeShader _ResetCompute;
        private int KernelResetBuffer;


        private List<Renderer> _Renderers = new List<Renderer>();
        Matrix4x4 _ViewMatrix;
        Matrix4x4 _ProjMatrix;
        Vector4 _CullingSphere;
        Matrix4x4 _DeepShadowMatrix;

        RenderTexture _Temp;


        static private List<string> _Tags = new List<string>()
        {
            "Player",
        };

        public DeepShadowCasterPass()
        {
            //NOTE: Not working when rendering shadows
            //RegisterShaderPassName("DeepShadowCaster");
        }

        public bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
        {

            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return false;


            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            if (light.shadows == LightShadows.None)
                return false;


            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }

            Bounds bounds;
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
                return false;

            //TODO : More branches
            _CountBuffer = renderer.GetBuffer(ComputeBufferHandle.DeepShadowMapsCount);
            _DataBuffer = renderer.GetBuffer(ComputeBufferHandle.DeepShadowMapsData);

            return true;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (_Temp)
            {
                RenderTexture.ReleaseTemporary(_Temp);
                _Temp = null;
            }

            base.FrameCleanup(cmd);
        }

        bool GetVPMatrix(Light light)
        {
            //if (!ShadowUtils.GetVPMatrixWithTags(light, _Tags, _Renderers, out _ViewMatrix, out _ProjMatrix, out _CullingSphere))
            //{
            //    return false;
            //}
            // FIXME: for test
            Bounds bounds = new Bounds(new Vector3(1, 1.65f, 1), new Vector3(0.25f, 0.25f, 0.4f));
            ShadowUtils.GetVPMatrixWithWorldBounds(light, bounds, out _ViewMatrix, out _ProjMatrix, out _CullingSphere);
            _DeepShadowMatrix = ShadowUtils.GetShadowTransform(_ProjMatrix, _ViewMatrix);

            return true;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            if (!renderingData.shadowData.supportsDeepShadowMaps)
            {
                return;
            }
            LightData lightData = renderingData.lightData;
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;


            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
            ShadowData shadowData = renderingData.shadowData;

            if (!GetVPMatrix(shadowLight.light))
            {
                return;
            }

            if (null == _ResetCompute)
            {
                _ResetCompute = renderer.GetCompute(ComputeHandle.ResetDeepShadowDataCompute);
                KernelResetBuffer = _ResetCompute.FindKernel("KernelResetBuffer");
            }

            CommandBuffer cmd = CommandBufferPool.Get(k_RenderDeepShadowCaster);
            using (new ProfilingSample(cmd, k_RenderDeepShadowCaster))
            {
                int deepShadowMapsSize = shadowData.deepShadowMapsSize;
                int deepShadowMapsDepth = shadowData.deepShadowMapsDepth;
                // Reset
                cmd.SetComputeBufferParam(_ResetCompute, KernelResetBuffer, "_CountBuffer", _CountBuffer);
                cmd.SetComputeBufferParam(_ResetCompute, KernelResetBuffer, "_DataBuffer", _DataBuffer);
                cmd.SetComputeIntParam(_ResetCompute, "_DeepShadowMapSize", deepShadowMapsSize);
                cmd.SetComputeIntParam(_ResetCompute, "_DeepShadowMapDepth", deepShadowMapsDepth);
                cmd.DispatchCompute(_ResetCompute, KernelResetBuffer, deepShadowMapsSize / 8, deepShadowMapsSize / 8, 1);

                _Temp = RenderTexture.GetTemporary(deepShadowMapsSize, deepShadowMapsSize, 0, RenderTextureFormat.R8); //Without rt, the second row of the vp matrix is negated
                
                // Cast
                SetRenderTarget(cmd, _Temp, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare, 
                    ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.SetViewport(new Rect(Vector2.zero, Vector2.one * deepShadowMapsSize));

                Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref shadowData, _ProjMatrix, shadowData.deepShadowMapsSize);
                ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);

                cmd.SetViewProjectionMatrices(_ViewMatrix, _ProjMatrix);
                cmd.SetRandomWriteTarget(1, _CountBuffer);
                cmd.SetRandomWriteTarget(2, _DataBuffer);
                cmd.SetGlobalInt("_DeepShadowMapSize", deepShadowMapsSize);
                cmd.SetGlobalInt("_DeepShadowMapDepth", deepShadowMapsDepth);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                //foreach (var r in _Renderers)
                //{
                //    for (int i = 0, imax = r.sharedMaterials.Length; i < imax; i++)
                //    {
                //        cmd.DrawRenderer(r, r.sharedMaterials[i], i, r.sharedMaterials[i].FindPass("DeepShadowCaster"));
                //    }
                //}

                ShadowSliceData slice = new ShadowSliceData()
                {
                    offsetX = 0,
                    offsetY = 0,
                    resolution = deepShadowMapsSize,
                };

                cmd.SetGlobalMatrix("_DeepShadowMapsWorldToShadow", _DeepShadowMatrix);
                DrawShadowsSettings settings = new DrawShadowsSettings(renderingData.cullResults, shadowLightIndex);
                settings.splitData.cullingSphere = _CullingSphere;

                cmd.EnableShaderKeyword("_DEEP_SHADOW_CASTER");
                cmd.SetGlobalInt("_ShadowCasterZWrite", 0);
                ShadowUtils.RenderShadowSlice(cmd, ref context, ref slice, ref settings, _ProjMatrix, _ViewMatrix);
                cmd.DisableShaderKeyword("_DEEP_SHADOW_CASTER");
                cmd.SetGlobalInt("_ShadowCasterZWrite", 1);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // For Resolve
                SetupDeepShadowMapResolverConstants(cmd, ref shadowData, shadowLight);
                cmd.ClearRandomWriteTargets();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        void SetupDeepShadowMapResolverConstants(CommandBuffer cmd, ref ShadowData shadowData, VisibleLight shadowLight)
        {
            Light light = shadowLight.light;

            cmd.SetGlobalMatrix("_DeepShadowMapsWorldToShadow", _DeepShadowMatrix);
            cmd.SetGlobalFloat("_DeepShadowStrength", light.shadowStrength);
            Vector4 cullingSphereWithSquaredRadius = _CullingSphere;
            cullingSphereWithSquaredRadius.w *= cullingSphereWithSquaredRadius.w;
            cmd.SetGlobalVector("_DeepShadowMapsCullingSphere", cullingSphereWithSquaredRadius);

        }
    }
}

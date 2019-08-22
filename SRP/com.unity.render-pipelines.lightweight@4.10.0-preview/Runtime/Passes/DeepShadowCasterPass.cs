using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class DeepShadowCasterPass : ScriptableRenderPass
    {
        const string k_RenderDeepShadowCaster = "Render Deep Shadow Caster";

        const int _Dimension = 1024;
        const int _Elements = 32;

        private ComputeBuffer _CountBuffer;
        private ComputeBuffer _DataBuffer;

        private ComputeShader _ResetCompute;
        private int KernelResetBuffer;


        private List<Renderer> _Renderers = new List<Renderer>();
        Matrix4x4 _ViewMatrix;
        Matrix4x4 _ProjMatrix;
        Matrix4x4 _DeepShadowMatrix;

        public DeepShadowCasterPass()
        {
            RegisterShaderPassName("DeepShadowCaster");
        }

        // TODO: settings
        public static void NewDeepShadowMapsBuffer(ref ComputeBuffer CountBuffer, ref ComputeBuffer DataBuffer)
        {
            CountBuffer = new ComputeBuffer(_Dimension * _Dimension, sizeof(uint));
            DataBuffer = new ComputeBuffer(_Dimension * _Dimension * _Elements, sizeof(float) * 2);
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
            base.FrameCleanup(cmd);
        }

        bool GetVPMatrix(Light light)
        {
            if (!ShadowUtils.GetVPMatrixWithTag(light, "Player", _Renderers, out _ViewMatrix, out _ProjMatrix))
            {
                return false;
            }

            _DeepShadowMatrix = ShadowUtils.GetShadowTransform(_ProjMatrix, _ViewMatrix);

            return true;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");

            //TODO : Settings
            //if (!renderingData.shadowData.supportsMainLightShadows) 
            //{
            //    return;
            //}
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
                // Reset
                cmd.SetComputeBufferParam(_ResetCompute, KernelResetBuffer, "_CountBuffer", _CountBuffer);
                cmd.SetComputeBufferParam(_ResetCompute, KernelResetBuffer, "_DataBuffer", _DataBuffer);
                cmd.DispatchCompute(_ResetCompute, KernelResetBuffer, _Dimension / 8, _Dimension / 8, 1);

                // Cast
                SetRenderTarget(cmd, BuiltinRenderTextureType.None, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare, 
                    ClearFlag.Color, Color.black, TextureDimension.Tex2D);
                cmd.SetViewport(new Rect(Vector2.zero, Vector2.one * _Dimension));

                Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref shadowData, _ProjMatrix, shadowData.mainCharacterShadowmapWidth);
                ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);

                cmd.SetViewProjectionMatrices(_ViewMatrix, _ProjMatrix);
                cmd.SetRandomWriteTarget(1, _CountBuffer);
                cmd.SetRandomWriteTarget(2, _DataBuffer);
                cmd.SetGlobalInt("_Dimension", _Dimension);
                cmd.SetGlobalInt("_Elements", _Elements);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                foreach (var r in _Renderers)
                {
                    for (int i = 0, imax = r.sharedMaterials.Length; i < imax; i++)
                    {
                        cmd.DrawRenderer(r, r.sharedMaterials[i], i, r.sharedMaterials[i].FindPass("DeepShadowCaster"));
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // For Resolve
                cmd.SetGlobalMatrix("_DeepShadowMapsWorldToShadow", _DeepShadowMatrix);
                cmd.ClearRandomWriteTargets();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

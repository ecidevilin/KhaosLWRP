using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class ScreenSpaceDeepShadowMapsPass : ScriptableRenderPass
    {
        private static class DeepShadowMapsConstantBuffer
        {
        }
        const string k_RenderScreenSpaceDeepShadowMaps = "Render Screen Space Deep Shadow Maps";

        const int k_Dimension = 1024;
        const int k_Elements = 32;

        private ComputeBuffer _CountBuffer;
        private ComputeBuffer _DataBuffer;

        private int KernelResetBuffer;
        
        private RenderTextureFormat _ShadowLutFormat;

        private RenderTargetHandle _DeepShadowLutHandle;
        private RenderTargetHandle _DeepShadowTmpHandle;
        private RenderTextureDescriptor _Descriptor;
       

        public ScreenSpaceDeepShadowMapsPass()
        {
            RegisterShaderPassName("DeepShadowCaster");
            _ShadowLutFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
        }

        public bool Setup(ScriptableRenderer renderer, RenderTextureDescriptor baseDescriptor, RenderTargetHandle deepShadowLutHandle, ref RenderingData renderingData)
        {

            //int shadowLightIndex = renderingData.lightData.mainLightIndex;
            //if (shadowLightIndex == -1)
            //    return false;


            //VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            //Light light = shadowLight.light;
            //if (light.shadows == LightShadows.None)
            //    return false;


            //if (shadowLight.lightType != LightType.Directional)
            //{
            //    Debug.LogWarning("Only directional lights are supported as main light.");
            //}

            //Bounds bounds;
            //if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
            //    return false;

            //TODO : More branches
            _CountBuffer = renderer.GetBuffer(ComputeBufferHandle.DeepShadowMapsCount);
            _DataBuffer = renderer.GetBuffer(ComputeBufferHandle.DeepShadowMapsData);

            _DeepShadowLutHandle = deepShadowLutHandle;
            _DeepShadowTmpHandle.Init("_DeepShadowTmp");
            _Descriptor = baseDescriptor;
            _Descriptor.colorFormat = _ShadowLutFormat;
            _Descriptor.depthBufferBits = 0;

            return true;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_DeepShadowLutHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_DeepShadowLutHandle.id);
                _DeepShadowLutHandle = RenderTargetHandle.CameraTarget;
            }
            if (_DeepShadowTmpHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_DeepShadowTmpHandle.id);
                _DeepShadowTmpHandle = RenderTargetHandle.CameraTarget;
            }
            base.FrameCleanup(cmd);
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

            CommandBuffer cmd = CommandBufferPool.Get(k_RenderScreenSpaceDeepShadowMaps);
            using (new ProfilingSample(cmd, k_RenderScreenSpaceDeepShadowMaps))
            {
                // Resolve
                Material ssdsm = renderer.GetMaterial(MaterialHandle.ScreenSpaceDeepShadowMaps);
                ssdsm.SetBuffer(Shader.PropertyToID("_CountBuffer"), _CountBuffer);
                ssdsm.SetBuffer(Shader.PropertyToID("_DataBuffer"), _DataBuffer);
                //TODO: Settings for blurring
                cmd.GetTemporaryRT(_DeepShadowLutHandle.id, _Descriptor, FilterMode.Bilinear);
                RenderTargetIdentifier DeepShadowLut = _DeepShadowLutHandle.Identifier();
                SetRenderTarget(cmd, DeepShadowLut, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.Color | ClearFlag.Depth, Color.black, _Descriptor.dimension);
                cmd.Blit(null, DeepShadowLut, ssdsm);

                // Blur
                Material pom = renderer.GetMaterial(MaterialHandle.GaussianBlur);
                pom.SetFloat("_SampleOffset", 1);
                cmd.GetTemporaryRT(_DeepShadowTmpHandle.id, _Descriptor, FilterMode.Bilinear);
                RenderTargetIdentifier DeepShadowTmp = _DeepShadowTmpHandle.Identifier();
                SetRenderTarget(cmd, DeepShadowTmp, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.Color | ClearFlag.Depth, Color.black, _Descriptor.dimension);
                cmd.Blit(DeepShadowLut, DeepShadowTmp, pom);
                //TODO : for stereo
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

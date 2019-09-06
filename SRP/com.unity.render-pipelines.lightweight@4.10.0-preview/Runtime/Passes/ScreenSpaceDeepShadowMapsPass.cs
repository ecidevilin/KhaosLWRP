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

        private ComputeBuffer _CountBuffer;
        private ComputeBuffer _DataBuffer;

        private int KernelResetBuffer;
        
        private RenderTextureFormat _ShadowLutFormat;

        private RenderTargetHandle _Destination;

        private RenderTargetHandle _DeepShadowLut;
        private RenderTargetHandle _DeepShadowTmp;
        private RenderTextureDescriptor _Descriptor;


        static RenderTargetHandle _DeepShadowTest;

        public ScreenSpaceDeepShadowMapsPass()
        {
            _ShadowLutFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
        }

        public bool Setup(ScriptableRenderer renderer, RenderTextureDescriptor baseDescriptor, RenderTargetHandle destination, ref RenderingData renderingData)
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

            _Destination = destination;
            
            _Descriptor = baseDescriptor;
            _Descriptor.colorFormat = _ShadowLutFormat;
            _Descriptor.depthBufferBits = 0;
            _DeepShadowLut.Init("_DSMLut");
            _DeepShadowTmp.Init("_DSMTmp");
            _DeepShadowTest.Init("_DeepShadowTest");

            return true;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_DeepShadowLut != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_DeepShadowLut.id);
                _DeepShadowLut = RenderTargetHandle.CameraTarget;
            }
            if (_DeepShadowTmp != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_DeepShadowTmp.id);
                _DeepShadowTmp = RenderTargetHandle.CameraTarget;
            }
            base.FrameCleanup(cmd);
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

            RenderTargetIdentifier result;

            CommandBuffer cmd = CommandBufferPool.Get(k_RenderScreenSpaceDeepShadowMaps);
            using (new ProfilingSample(cmd, k_RenderScreenSpaceDeepShadowMaps))
            {
#if UNITY_EDITOR
                var testDescriptor = _Descriptor;
                testDescriptor.enableRandomWrite = true;
                testDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                cmd.GetTemporaryRT(_DeepShadowTest.id, testDescriptor);
                var _ResetCompute = renderer.GetCompute(ComputeHandle.ResetDeepShadowDataCompute);
                int KernelTestDeepShadowMap = _ResetCompute.FindKernel("KernelTestDeepShadowMap");
                cmd.SetRenderTarget(_DeepShadowTest.Identifier());
                cmd.SetComputeBufferParam(_ResetCompute, KernelTestDeepShadowMap, "_CountBuffer", _CountBuffer);
                cmd.SetComputeBufferParam(_ResetCompute, KernelTestDeepShadowMap, "_DataBuffer", _DataBuffer);
                cmd.SetComputeTextureParam(_ResetCompute, KernelTestDeepShadowMap, "_TestRt", _DeepShadowTest.Identifier());
                cmd.DispatchCompute(_ResetCompute, KernelTestDeepShadowMap, shadowData.deepShadowMapsSize / 8, shadowData.deepShadowMapsSize / 8, 1);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
#endif

                // Resolve
                Material ssdsm = renderer.GetMaterial(MaterialHandle.ScreenSpaceDeepShadowMaps);
                ssdsm.SetBuffer(Shader.PropertyToID("_CountBuffer"), _CountBuffer);
                ssdsm.SetBuffer(Shader.PropertyToID("_DataBuffer"), _DataBuffer);
                cmd.GetTemporaryRT(_DeepShadowLut.id, _Descriptor);

                RenderTargetIdentifier lutId = _DeepShadowLut.Identifier();
                SetRenderTarget(cmd, lutId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.Color | ClearFlag.Depth, Color.black, _Descriptor.dimension);
                cmd.Blit(lutId, lutId, ssdsm);

                result = lutId;

                // Blur
                int blurOffset = shadowData.deepShadowMapsBlurOffset;
                
                if (blurOffset > 0)
                {
                    Material pom = renderer.GetMaterial(MaterialHandle.GaussianBlur);
                    cmd.GetTemporaryRT(_DeepShadowTmp.id, _Descriptor);
                    RenderTargetIdentifier src = lutId;
                    RenderTargetIdentifier dst = _DeepShadowTmp.Identifier();
                    while (blurOffset > 0)
                    {
                        pom.SetFloat("_SampleOffset", blurOffset);
                        SetRenderTarget(cmd, dst, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                            ClearFlag.Color | ClearFlag.Depth, Color.black, _Descriptor.dimension);
                        cmd.Blit(src, dst, pom);
                        result = dst;
                        dst = src;
                        src = result;
                        blurOffset >>= 1;
                    }
                }
                //TODO : for stereo

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetGlobalTexture(_Destination.id, result);
                cmd.ReleaseTemporaryRT(_DeepShadowTest.id);
            }
            //SetKeyword
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DeepShadowMaps, true);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

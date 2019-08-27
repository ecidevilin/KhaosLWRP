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

        private RenderTexture _DeepShadowLut;
        private RenderTexture _DeepShadowTmp;
        private RenderTextureDescriptor _Descriptor;
       

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

            temp = new RenderTexture(1024, 1024, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
            };
            temp.Create();

            return true;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_DeepShadowLut)
            {
                RenderTexture.ReleaseTemporary(_DeepShadowLut);
                _DeepShadowLut = null;
            }
            if (_DeepShadowTmp)
            {
                RenderTexture.ReleaseTemporary(_DeepShadowTmp);
                _DeepShadowTmp = null;
            }
            base.FrameCleanup(cmd);
        }

        RenderTexture temp;

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

            RenderTexture result;

            CommandBuffer cmd = CommandBufferPool.Get(k_RenderScreenSpaceDeepShadowMaps);
            using (new ProfilingSample(cmd, k_RenderScreenSpaceDeepShadowMaps))
            {
                var _ResetCompute = renderer.GetCompute(ComputeHandle.ResetDeepShadowDataCompute);
                int KernelTestDeepShadowMap = _ResetCompute.FindKernel("KernelTestDeepShadowMap");
                cmd.SetRenderTarget(temp);
                cmd.SetComputeBufferParam(_ResetCompute, KernelTestDeepShadowMap, "_CountBuffer", _CountBuffer);
                cmd.SetComputeBufferParam(_ResetCompute, KernelTestDeepShadowMap, "_DataBuffer", _DataBuffer);
                cmd.SetComputeTextureParam(_ResetCompute, KernelTestDeepShadowMap, "_TestRt", temp);
                cmd.DispatchCompute(_ResetCompute, KernelTestDeepShadowMap, 1024 / 8, 1024 / 8, 1);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Resolve
                Material ssdsm = renderer.GetMaterial(MaterialHandle.ScreenSpaceDeepShadowMaps);
                ssdsm.SetBuffer(Shader.PropertyToID("_CountBuffer"), _CountBuffer);
                ssdsm.SetBuffer(Shader.PropertyToID("_DataBuffer"), _DataBuffer);
                //TODO: Settings for blurring
                _DeepShadowLut = RenderTexture.GetTemporary(_Descriptor);
                _DeepShadowLut.filterMode = FilterMode.Bilinear;
                _DeepShadowLut.wrapMode = TextureWrapMode.Clamp;
                _DeepShadowLut.name = "_DeepShadowLut";


                SetRenderTarget(cmd, _DeepShadowLut, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.Color | ClearFlag.Depth, Color.black, _Descriptor.dimension);
                cmd.Blit(null, _DeepShadowLut, ssdsm);

                // Blur
                Material pom = renderer.GetMaterial(MaterialHandle.GaussianBlur);
                pom.SetFloat("_SampleOffset", 1);
                _DeepShadowTmp = RenderTexture.GetTemporary(_Descriptor);
                _DeepShadowTmp.filterMode = FilterMode.Bilinear;
                _DeepShadowTmp.wrapMode = TextureWrapMode.Clamp;
                _DeepShadowTmp.name = "_DeepShadowTmp";
                SetRenderTarget(cmd, _DeepShadowTmp, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.Color | ClearFlag.Depth, Color.black, _Descriptor.dimension);
                cmd.Blit(_DeepShadowLut, _DeepShadowTmp, pom);
                //TODO : for stereo

                result = _DeepShadowTmp;

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                SetupScreenSpaceDeepShadowMapsConstants(cmd, ref shadowData, shadowLight, result);
            }
            //SetKeyword
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        void SetupScreenSpaceDeepShadowMapsConstants(CommandBuffer cmd, ref ShadowData shadowData, VisibleLight shadowLight, RenderTexture rt)
        {
            Light light = shadowLight.light;

            float invShadowAtlasWidth = 1.0f / shadowData.mainCharacterShadowmapWidth;
            float invShadowAtlasHeight = 1.0f / shadowData.mainCharacterShadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
            cmd.SetGlobalTexture(_Destination.id, rt);

        }
    }
}

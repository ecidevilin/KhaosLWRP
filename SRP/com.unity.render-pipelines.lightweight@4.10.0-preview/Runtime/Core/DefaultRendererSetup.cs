using System;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    internal class DefaultRendererSetup : IRendererSetup
    {
        private DepthOnlyPass m_DepthOnlyPass;
        private CopyDepthPass _CopyDepthForOITPass;
        private OITDepthOnlyPass _OITDepthOnlyPass;
        private DepthNormalsPass m_DepthNormalsPass;
        private MainLightShadowCasterPass m_MainLightShadowCasterPass;
        private MainCharacterShadowCasterPass m_MainCharacterShadowCasterPass;
        private DeepShadowCasterPass _DeepShadowCasterPass;
        private AdditionalLightsShadowCasterPass m_AdditionalLightsShadowCasterPass;
        private SetupForwardRenderingPass m_SetupForwardRenderingPass;
        private ScreenSpaceShadowResolvePass m_ScreenSpaceShadowResolvePass;
        private ScreenSpaceDeepShadowMapsPass _ScreenSpaceDeepShadowMapsPass;
        private CreateLightweightRenderTexturesPass m_CreateLightweightRenderTexturesPass;
        private BeginXRRenderingPass m_BeginXrRenderingPass;
        private SetupLightweightConstanstPass m_SetupLightweightConstants;
        private RenderOpaqueForwardPass m_RenderOpaqueForwardPass;
        private OpaquePostProcessPass m_OpaquePostProcessPass;
        private DrawSkyboxPass m_DrawSkyboxPass;
        private CopyDepthPass m_CopyDepthPass;
        private CopyColorPass m_CopyColorPass;
        private RenderTransparentForwardPass m_RenderTransparentForwardPass;
        //private RenderWeightedOITForwardPass _RenderWeightedOITForwardPass;
        private RenderMomentOITForwardPass _RenderMomentOITForwardPass;
        private TransparentPostProcessPass m_TransparentPostProcessPass;
        private FinalBlitPass m_FinalBlitPass;
        private EndXRRenderingPass m_EndXrRenderingPass;

#if UNITY_EDITOR
        private SceneViewDepthCopyPass m_SceneViewDepthCopyPass;
#endif


        private RenderTargetHandle ColorAttachment;
        private RenderTargetHandle DepthAttachment;
        private RenderTargetHandle DepthTexture;
        private RenderTargetHandle OITDepthTexture;
        private RenderTargetHandle OpaqueColor;
        private RenderTargetHandle DepthNormalsTexture;
        private RenderTargetHandle MainLightShadowmap;
        private RenderTargetHandle MainCharacterShadowmap;
        private RenderTargetHandle AdditionalLightsShadowmap;
        private RenderTargetHandle ScreenSpaceShadowmap;
        private RenderTargetHandle ScreenSpaceDeepShadowLut;

        private List<IBeforeRender> m_BeforeRenderPasses = new List<IBeforeRender>(10);

        [NonSerialized]
        private bool m_Initialized = false;

        private void Init()
        {
            if (m_Initialized)
                return;

            m_DepthOnlyPass = new DepthOnlyPass();
            _CopyDepthForOITPass = new CopyDepthPass();
            _OITDepthOnlyPass = new OITDepthOnlyPass();
            m_DepthNormalsPass = new DepthNormalsPass();
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass();
            m_MainCharacterShadowCasterPass = new MainCharacterShadowCasterPass();
            m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass();
            _DeepShadowCasterPass = new DeepShadowCasterPass();
            m_SetupForwardRenderingPass = new SetupForwardRenderingPass();
            m_ScreenSpaceShadowResolvePass = new ScreenSpaceShadowResolvePass();
            _ScreenSpaceDeepShadowMapsPass = new ScreenSpaceDeepShadowMapsPass();
            m_CreateLightweightRenderTexturesPass = new CreateLightweightRenderTexturesPass();
            m_BeginXrRenderingPass = new BeginXRRenderingPass();
            m_SetupLightweightConstants = new SetupLightweightConstanstPass();
            m_RenderOpaqueForwardPass = new RenderOpaqueForwardPass();
            m_OpaquePostProcessPass = new OpaquePostProcessPass();
            m_DrawSkyboxPass = new DrawSkyboxPass();
            m_CopyDepthPass = new CopyDepthPass();
            m_CopyColorPass = new CopyColorPass();
            m_RenderTransparentForwardPass = new RenderTransparentForwardPass();
            //_RenderWeightedOITForwardPass = new RenderWeightedOITForwardPass();
            _RenderMomentOITForwardPass = new RenderMomentOITForwardPass();
            m_TransparentPostProcessPass = new TransparentPostProcessPass();
            m_FinalBlitPass = new FinalBlitPass();
            m_EndXrRenderingPass = new EndXRRenderingPass();

#if UNITY_EDITOR
            m_SceneViewDepthCopyPass = new SceneViewDepthCopyPass();
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            ColorAttachment.Init("_CameraColorTexture");
            DepthAttachment.Init("_CameraDepthAttachment");
            DepthTexture.Init("_CameraDepthTexture");
            OITDepthTexture.Init("_OITDepthTexture");
            OpaqueColor.Init("_CameraOpaqueTexture");
            DepthNormalsTexture.Init("_CameraDepthNormalsTexture");
            MainLightShadowmap.Init("_MainLightShadowmapTexture");
            MainCharacterShadowmap.Init("_MainCharacterShadowmapTexture");
            AdditionalLightsShadowmap.Init("_AdditionalLightsShadowmapTexture");
            ScreenSpaceShadowmap.Init("_ScreenSpaceShadowmapTexture");
            ScreenSpaceDeepShadowLut.Init("_DeepShadowLut");

            m_Initialized = true;
        }

        public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Init();

            Camera camera = renderingData.cameraData.camera;
            renderer.postProcessingContext.camera = camera;
            camera.GetComponents(m_BeforeRenderPasses);

            renderer.SetupPerObjectLightIndices(ref renderingData.cullResults, ref renderingData.lightData);
            RenderTextureDescriptor baseDescriptor = ScriptableRenderer.CreateRenderTextureDescriptor(ref renderingData.cameraData);
            RenderTextureDescriptor shadowDescriptor = baseDescriptor;
            ClearFlag clearFlag = ScriptableRenderer.GetCameraClearFlag(renderingData.cameraData.camera);
            shadowDescriptor.dimension = TextureDimension.Tex2D;

            bool requiresRenderToTexture = ScriptableRenderer.RequiresIntermediateColorTexture(ref renderingData.cameraData, baseDescriptor)
                                           || m_BeforeRenderPasses.Count != 0;

            RenderTargetHandle colorHandle = RenderTargetHandle.CameraTarget;
            RenderTargetHandle depthHandle = RenderTargetHandle.CameraTarget;
            var sampleCount = (SampleCount)renderingData.cameraData.msaaSamples;

            if (requiresRenderToTexture)
            {
                colorHandle = ColorAttachment;
                depthHandle = DepthAttachment;

                m_CreateLightweightRenderTexturesPass.Setup(baseDescriptor, colorHandle, depthHandle, sampleCount);
                renderer.EnqueuePass(m_CreateLightweightRenderTexturesPass);
            }

            foreach (var pass in m_BeforeRenderPasses)
            {
                renderer.EnqueuePass(pass.GetPassToEnqueue(baseDescriptor, colorHandle, depthHandle, clearFlag));
            }

            bool mainLightShadows = false;
            if (renderingData.shadowData.supportsMainLightShadows)
            {
                mainLightShadows = m_MainLightShadowCasterPass.Setup(MainLightShadowmap, ref renderingData);
                if (mainLightShadows)
                    renderer.EnqueuePass(m_MainLightShadowCasterPass);
            }

            if (renderingData.shadowData.supportsMainCharacterShadows)
            {
                if (m_MainCharacterShadowCasterPass.Setup(MainCharacterShadowmap, ref renderingData))
                {
                    renderer.EnqueuePass(m_MainCharacterShadowCasterPass);
                }
            }

            bool renderDeepShadowMap = renderingData.shadowData.supportsDeepShadowMaps;
            if (renderDeepShadowMap)
            {
                renderDeepShadowMap &= _DeepShadowCasterPass.Setup(renderer, ref renderingData);
                if (renderDeepShadowMap)
                {
                    renderer.EnqueuePass(_DeepShadowCasterPass);
                }
            }

            if (renderingData.shadowData.supportsAdditionalLightShadows)
            {
                bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(AdditionalLightsShadowmap, ref renderingData, renderer.maxVisibleAdditionalLights);
                if (additionalLightShadows)
                    renderer.EnqueuePass(m_AdditionalLightsShadowCasterPass);
            }

            bool resolveShadowsInScreenSpace = mainLightShadows && renderingData.shadowData.requiresScreenSpaceShadowResolve;
            bool requiresDepthPrepass = resolveShadowsInScreenSpace || renderingData.cameraData.isSceneViewCamera ||
                                        (renderingData.cameraData.requiresDepthTexture && (!CanCopyDepth(ref renderingData.cameraData) || renderingData.cameraData.isOffscreenRender));

            // For now VR requires a depth prepass until we figure out how to properly resolve texture2DMS in stereo
            requiresDepthPrepass |= renderingData.cameraData.isStereoEnabled;
            renderer.EnqueuePass(m_SetupForwardRenderingPass);

            bool supportsOIT = renderingData.cameraData.supportsOIT;
            requiresDepthPrepass |= supportsOIT;

            if (requiresDepthPrepass)
            {
                m_DepthOnlyPass.Setup(baseDescriptor, DepthTexture, sampleCount);
                renderer.EnqueuePass(m_DepthOnlyPass);
                depthHandle = DepthTexture;

                foreach (var pass in camera.GetComponents<IAfterDepthPrePass>())
                    renderer.EnqueuePass(pass.GetPassToEnqueue(m_DepthOnlyPass.descriptor, DepthTexture));

                if (supportsOIT)
                {
                    _CopyDepthForOITPass.Setup(depthHandle, OITDepthTexture);
                    renderer.EnqueuePass(_CopyDepthForOITPass);
                    _OITDepthOnlyPass.Setup(baseDescriptor, OITDepthTexture, sampleCount);
                    renderer.EnqueuePass(_OITDepthOnlyPass);
                }
            }

            bool requiresDepthNormalsPass = renderingData.cameraData.requiresDepthNormalsTexture;

            if (requiresDepthNormalsPass)
            {
                m_DepthNormalsPass.Setup(baseDescriptor, DepthNormalsTexture, depthHandle, requiresDepthPrepass, sampleCount);
                renderer.EnqueuePass(m_DepthNormalsPass);
            }

            if (renderDeepShadowMap)
            {
                if (_ScreenSpaceDeepShadowMapsPass.Setup(renderer, baseDescriptor, ScreenSpaceDeepShadowLut, ref renderingData))
                {
                    renderer.EnqueuePass(_ScreenSpaceDeepShadowMapsPass);
                }
            }

            if (resolveShadowsInScreenSpace)
            {
                m_ScreenSpaceShadowResolvePass.Setup(baseDescriptor, ScreenSpaceShadowmap);
                renderer.EnqueuePass(m_ScreenSpaceShadowResolvePass);
            }


            if (renderingData.cameraData.isStereoEnabled)
                renderer.EnqueuePass(m_BeginXrRenderingPass);

            RendererConfiguration rendererConfiguration = ScriptableRenderer.GetRendererConfiguration(renderingData.lightData.additionalLightsCount);

            m_SetupLightweightConstants.Setup(renderer.maxVisibleAdditionalLights, renderer.perObjectLightIndices);
            renderer.EnqueuePass(m_SetupLightweightConstants);

            // If a before all render pass executed we expect it to clear the color render target
            if (m_BeforeRenderPasses.Count != 0)
                clearFlag = ClearFlag.None;

            m_RenderOpaqueForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, clearFlag, camera.backgroundColor, rendererConfiguration, requiresDepthPrepass);
            renderer.EnqueuePass(m_RenderOpaqueForwardPass);
            foreach (var pass in camera.GetComponents<IAfterOpaquePass>())
                renderer.EnqueuePass(pass.GetPassToEnqueue(baseDescriptor, colorHandle, depthHandle));

            if (renderingData.cameraData.postProcessEnabled)
            {
                renderingData.cameraData.postProcessLayer.SetupContext(renderer.postProcessingContext);
                if (renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(renderer.postProcessingContext))
                {
                    m_OpaquePostProcessPass.Setup(baseDescriptor, colorHandle);
                    renderer.EnqueuePass(m_OpaquePostProcessPass);

                    foreach (var pass in camera.GetComponents<IAfterOpaquePostProcess>())
                        renderer.EnqueuePass(pass.GetPassToEnqueue(baseDescriptor, colorHandle, depthHandle));
                }
            }

            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            {
                m_DrawSkyboxPass.Setup(colorHandle, depthHandle);
                renderer.EnqueuePass(m_DrawSkyboxPass);
            }

            foreach (var pass in camera.GetComponents<IAfterSkyboxPass>())
                renderer.EnqueuePass(pass.GetPassToEnqueue(baseDescriptor, colorHandle, depthHandle));

            if (renderingData.cameraData.requiresDepthTexture && !requiresDepthPrepass)
            {
                m_CopyDepthPass.Setup(depthHandle, DepthTexture);
                renderer.EnqueuePass(m_CopyDepthPass);
            }

            if (renderingData.cameraData.requiresOpaqueTexture)
            {
                m_CopyColorPass.Setup(colorHandle, OpaqueColor);
                renderer.EnqueuePass(m_CopyColorPass);
            }

            //_RenderWeightedOITForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, rendererConfiguration, sampleCount);
            //renderer.EnqueuePass(_RenderWeightedOITForwardPass);
            if (supportsOIT)
            {
                if (_RenderMomentOITForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, rendererConfiguration, sampleCount, renderingData))
                {
                    renderer.EnqueuePass(_RenderMomentOITForwardPass);
                }
            }

            m_RenderTransparentForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, rendererConfiguration);
            renderer.EnqueuePass(m_RenderTransparentForwardPass);

            foreach (var pass in camera.GetComponents<IAfterTransparentPass>())
                renderer.EnqueuePass(pass.GetPassToEnqueue(baseDescriptor, colorHandle, depthHandle));

            if (renderingData.cameraData.postProcessEnabled)
            {
                m_TransparentPostProcessPass.Setup(baseDescriptor, colorHandle, BuiltinRenderTextureType.CameraTarget);
                renderer.EnqueuePass(m_TransparentPostProcessPass);
            }
            else if (!renderingData.cameraData.isOffscreenRender && colorHandle != RenderTargetHandle.CameraTarget)
            {
                m_FinalBlitPass.Setup(baseDescriptor, colorHandle);
                renderer.EnqueuePass(m_FinalBlitPass);
            }

            foreach (var pass in camera.GetComponents<IAfterRender>())
                renderer.EnqueuePass(pass.GetPassToEnqueue());

            if (renderingData.cameraData.isStereoEnabled)
            {
                renderer.EnqueuePass(m_EndXrRenderingPass);
            }

#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                m_SceneViewDepthCopyPass.Setup(DepthTexture);
                renderer.EnqueuePass(m_SceneViewDepthCopyPass);
            }
#endif
        }

        bool CanCopyDepth(ref CameraData cameraData)
        {
            bool msaaEnabledForCamera = (int)cameraData.msaaSamples > 1;
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            bool supportsDepthTarget = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            // TODO:  We don't have support to highp Texture2DMS currently and this breaks depth precision.
            // currently disabling it until shader changes kick in.
            //bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;
            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }
    }
}

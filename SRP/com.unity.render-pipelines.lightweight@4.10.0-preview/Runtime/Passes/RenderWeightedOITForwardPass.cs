using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class RenderWeightedOITForwardPass : ScriptableRenderPass
    {
        const string _kRenderOITTag = "Render OIT";
        FilterRenderersSettings _OITFilterSettings;
        RenderTargetHandle _ColorAttachmentHandle { get; set; }
        RenderTargetHandle _DepthAttachmentHandle { get; set; }

        RenderTargetHandle _AccumColorHandle;
        RenderTargetHandle _AccumGIHandle;
        RenderTargetHandle _AccumAlphaHandle;

        RenderTargetBinding _AccumBinding;

        RenderTextureDescriptor _Descriptor { get; set; }
        RendererConfiguration _RendererConfiguration;


        RenderTextureDescriptor _DescriptorAC { get; set; }
        RenderTextureDescriptor _DescriptorAA { get; set; }

        public RenderWeightedOITForwardPass()
        {
            RegisterShaderPassName("LightweightForward");
            RegisterShaderPassName("SRPDefaultUnlit");
            _OITFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueUtils.oit,
            };
        }

        /// <summary>
        /// Configure the pass before execution
        /// </summary>
        /// <param name="baseDescriptor">Current target descriptor</param>
        /// <param name="colorAttachmentHandle">Color attachment to render into</param>
        /// <param name="depthAttachmentHandle">Depth attachment to render into</param>
        /// <param name="configuration">Specific render configuration</param>
        public void Setup(
            RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle colorAttachmentHandle,
            RenderTargetHandle depthAttachmentHandle,
            RendererConfiguration configuration,
            SampleCount samples)
        {
            this._ColorAttachmentHandle = colorAttachmentHandle;
            this._DepthAttachmentHandle = depthAttachmentHandle;
            _Descriptor = baseDescriptor;
            _RendererConfiguration = configuration;

            if ((int)samples > 1)
            {
                baseDescriptor.bindMS = false;
                baseDescriptor.msaaSamples = (int)samples;
            }

            baseDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            baseDescriptor.depthBufferBits = 0;
            _DescriptorAC = baseDescriptor;

            baseDescriptor.colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf)
                ? RenderTextureFormat.RHalf
                : RenderTextureFormat.ARGBHalf;
            _DescriptorAA = baseDescriptor;

            _AccumColorHandle.Init("_AccumColor");
            _AccumGIHandle.Init("_AccumGI");
            _AccumAlphaHandle.Init("_AccumAlpha");

            _AccumBinding = new RenderTargetBinding( new RenderTargetIdentifier[]
            {
                _AccumColorHandle.Identifier(),
                _AccumGIHandle.Identifier(),
                _AccumAlphaHandle.Identifier(),
            },
            new RenderBufferLoadAction[]
            {
                RenderBufferLoadAction.DontCare,
                RenderBufferLoadAction.DontCare,
                RenderBufferLoadAction.DontCare,
            },
            new RenderBufferStoreAction[]
            {
                RenderBufferStoreAction.Store,
                RenderBufferStoreAction.Store,
                RenderBufferStoreAction.Store,
            },
            _DepthAttachmentHandle.Identifier(),
            RenderBufferLoadAction.Load,
            RenderBufferStoreAction.DontCare);
        }


        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_AccumColorHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_AccumColorHandle.id);
                _AccumColorHandle = RenderTargetHandle.CameraTarget;
            }
            if (_AccumGIHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_AccumGIHandle.id);
                _AccumGIHandle = RenderTargetHandle.CameraTarget;
            }
            if (_AccumAlphaHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_AccumAlphaHandle.id);
                _AccumAlphaHandle = RenderTargetHandle.CameraTarget;
            }
            base.FrameCleanup(cmd);
        }

        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");

            CommandBuffer cmd = CommandBufferPool.Get(_kRenderOITTag);
            using (new ProfilingSample(cmd, _kRenderOITTag))
            {

                cmd.GetTemporaryRT(_AccumColorHandle.id, _DescriptorAC);
                cmd.GetTemporaryRT(_AccumGIHandle.id, _DescriptorAC);
                cmd.GetTemporaryRT(_AccumAlphaHandle.id, _DescriptorAA);

                cmd.SetRenderTarget(_AccumBinding);
                cmd.ClearRenderTarget(false, true, Color.black);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                var drawSettings = CreateDrawRendererSettings(camera, SortFlags.None, _RendererConfiguration, renderingData.supportsDynamicBatching);
                context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, _OITFilterSettings);

                // Render objects that did not match any shader pass with error shader
                renderer.RenderObjectsWithError(context, ref renderingData.cullResults, camera, _OITFilterSettings, SortFlags.None);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                //cmd.SetGlobalTexture("_AccumColor", _AccumColorHandle.Identifier());
                //cmd.SetGlobalTexture("_AccumAlpha", _AccumAlphaHandle.Identifier());

                RenderBufferLoadAction loadOp = RenderBufferLoadAction.Load;
                RenderBufferStoreAction storeOp = RenderBufferStoreAction.Store;
                SetRenderTarget(cmd, _ColorAttachmentHandle.Identifier(), loadOp, storeOp,
                    _DepthAttachmentHandle.Identifier(), loadOp, storeOp, ClearFlag.None, Color.black, _Descriptor.dimension);
                cmd.Blit(_ColorAttachmentHandle.Identifier(), _ColorAttachmentHandle.Identifier(), renderer.GetMaterial(MaterialHandle.OITComposite));
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

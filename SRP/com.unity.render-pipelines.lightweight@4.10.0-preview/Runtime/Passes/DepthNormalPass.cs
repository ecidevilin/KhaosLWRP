using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    /// <summary>
    /// Render all objects that have a 'DepthOnly' pass into the given depth buffer.
    ///
    /// You can use this pass to prime a depth buffer for subsequent rendering.
    /// Use it as a z-prepass, or use it to generate a depth buffer.
    /// </summary>
    public class DepthNormalPass : ScriptableRenderPass
    {
        const string k_DepthNormalTag = "Depth Normal Pass";

        int kDepthBufferBits = 32;
        
        private FilterRenderersSettings opaqueFilterSettings { get; set; }

        private RenderTargetHandle destination { get; set; }
        private RenderTextureDescriptor descriptor { get; set; }

        /// <summary>
        /// Create the DepthOnlyPass
        /// </summary>
        public DepthNormalPass()
        {
            RegisterShaderPassName("DepthNormal");
            opaqueFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
            };
        }
        
        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(
            RenderTextureDescriptor baseDescriptor, RenderTargetHandle destination)
        {
            this.destination = destination;
            baseDescriptor.depthBufferBits = kDepthBufferBits;
            //baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor = baseDescriptor;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            CommandBuffer cmd = CommandBufferPool.Get(k_DepthNormalTag);
            using (new ProfilingSample(cmd, k_DepthNormalTag))
            {
                cmd.GetTemporaryRT(destination.id, descriptor, FilterMode.Bilinear);
                SetRenderTarget(
                    cmd,
                    destination.Identifier(),
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    ClearFlag.Color | ClearFlag.Depth,
                    Color.black,
                    TextureDimension.Tex2D);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawRendererSettings(renderingData.cameraData.camera, sortFlags, RendererConfiguration.None, renderingData.supportsDynamicBatching);
                if (renderingData.cameraData.isStereoEnabled)
                {
                    Camera camera = renderingData.cameraData.camera;
                    context.StartMultiEye(camera);
                    context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, opaqueFilterSettings);
                    context.StopMultiEye(camera);
                }
                else
                    context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, opaqueFilterSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (destination != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(destination.id);
                destination = RenderTargetHandle.CameraTarget;
            }
        }
    }
}

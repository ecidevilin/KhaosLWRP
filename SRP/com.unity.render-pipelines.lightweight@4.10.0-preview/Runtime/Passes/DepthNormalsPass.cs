using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class DepthNormalsPass : ScriptableRenderPass
    {
        const string k_DepthNormalsTag = "Depth Normals Pass";

        int kDepthBufferBits = 16;
        
        private FilterRenderersSettings opaqueFilterSettings { get; set; }

        private RenderTargetHandle depthNormalsHandle { get; set; }
        private RenderTargetHandle depthAttachmentHandle { get; set; }
        private RenderTextureDescriptor descriptor { get; set; }
        private bool isDepthPrepassEnabled;

        /// <summary>
        /// Create the DepthOnlyPass
        /// </summary>
        public DepthNormalsPass()
        {
            RegisterShaderPassName("DepthNormals");
            opaqueFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
            };
        }
        
        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(
            RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthNormalsHandle, RenderTargetHandle depthAttachmentHandle, bool depthPrepass)
        {
            this.depthNormalsHandle = depthNormalsHandle;
            this.depthAttachmentHandle = depthAttachmentHandle;
            baseDescriptor.depthBufferBits = depthPrepass ? 0 : kDepthBufferBits;
            baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor = baseDescriptor;
            this.isDepthPrepassEnabled = depthPrepass;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            CommandBuffer cmd = CommandBufferPool.Get(k_DepthNormalsTag);
            using (new ProfilingSample(cmd, k_DepthNormalsTag))
            {
                cmd.GetTemporaryRT(depthNormalsHandle.id, descriptor, FilterMode.Bilinear);
                
                if (isDepthPrepassEnabled)
                {
                    SetRenderTarget(
                        cmd,
                        depthNormalsHandle.Identifier(),
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store,
                        depthAttachmentHandle.Identifier(),
                        RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.DontCare,
                        ClearFlag.Color,
                        Color.black,
                        TextureDimension.Tex2D);
                    cmd.DisableShaderKeyword("_ALPHATEST_ON");
                }
                else
                {
                    SetRenderTarget(
                        cmd,
                        depthNormalsHandle.Identifier(),
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store,
                        ClearFlag.Color | ClearFlag.Depth,
                        Color.black,
                        TextureDimension.Tex2D);
                    cmd.EnableShaderKeyword("_ALPHATEST_ON");
                }
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
            if (depthNormalsHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(depthNormalsHandle.id);
                depthNormalsHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}

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
    public class OITDepthOnlyPass : ScriptableRenderPass
    {
        const string k_OITDepthPrepassTag = "OIT Depth Prepass";

        int kDepthBufferBits = 32;

        private RenderTargetHandle depthAttachmentHandle { get; set; }
        internal RenderTextureDescriptor descriptor { get; private set; }
        private FilterRenderersSettings oitFilterSettings { get; set; }

        /// <summary>
        /// Create the DepthOnlyPass
        /// </summary>
        public OITDepthOnlyPass()
        {
            RegisterShaderPassName("DepthOnly");
            oitFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueUtils.oit,
            };
        }
        
        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(
            RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle depthAttachmentHandle,
            SampleCount samples)
        {
            this.depthAttachmentHandle = depthAttachmentHandle;
            baseDescriptor.colorFormat = RenderTextureFormat.Depth;
            baseDescriptor.depthBufferBits = kDepthBufferBits;

            if ((int)samples > 1)
            {
                baseDescriptor.bindMS = false;
                baseDescriptor.msaaSamples = (int)samples;
            }

            descriptor = baseDescriptor;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            CommandBuffer cmd = CommandBufferPool.Get(k_OITDepthPrepassTag);
            using (new ProfilingSample(cmd, k_OITDepthPrepassTag))
            {
                //cmd.GetTemporaryRT(depthAttachmentHandle.id, descriptor, FilterMode.Point);
                SetRenderTarget(
                    cmd,
                    depthAttachmentHandle.Identifier(),
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.black,
                    descriptor.dimension);

                if (descriptor.msaaSamples > 1)
                {
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthNoMsaa);
                    if (descriptor.msaaSamples == 4)
                    {
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    }
                    else
                    {
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    }
                }
                else
                {
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthNoMsaa);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                }
                CoreUtils.SetKeyword(cmd, "_ALPHATEST_ON", true);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var drawSettings = CreateDrawRendererSettings(renderingData.cameraData.camera, SortFlags.None, RendererConfiguration.None, renderingData.supportsDynamicBatching);
                if (renderingData.cameraData.isStereoEnabled)
                {
                    Camera camera = renderingData.cameraData.camera;
                    context.StartMultiEye(camera);
                    context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, oitFilterSettings);
                    context.StopMultiEye(camera);
                }
                else
                    context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, oitFilterSettings);
                CoreUtils.SetKeyword(cmd, "_ALPHATEST_ON", false);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            
            //if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
            //{
            //    cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
            //    depthAttachmentHandle = RenderTargetHandle.CameraTarget;
            //}
        }
    }
}

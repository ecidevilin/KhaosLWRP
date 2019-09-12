using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class RenderMomentOITForwardPass : ScriptableRenderPass
    {
        const string _kRenderOITTag = "Render Moment OIT";
        FilterRenderersSettings _OITFilterSettings;
        RenderTargetHandle _DepthAttachmentHandle { get; set; }
        RenderTargetHandle _ColorAttachmentHandle { get; set; }

        RenderTargetHandle _B0Handle;
        RenderTargetHandle _B1Handle;
        RenderTargetHandle _B2Handle;
        RenderTargetBinding _GMBinding;

        RenderTargetHandle _MOITHandle;
        RenderTargetHandle _GIALHandle;
        RenderTargetBinding _RMBinding;

        RenderTextureDescriptor _Descriptor { get; set; }
        RenderTextureDescriptor _DescriptorFloat { get; set; }
        RenderTextureDescriptor _DescriptorFloat2 { get; set; }
        RenderTextureDescriptor _DescriptorFloat4 { get; set; }

        RendererConfiguration _RendererConfiguration;

        Vector2 _ViewDepthMinMax;

        bool _Trigonometric = false;

        public RenderMomentOITForwardPass()
        {
            RegisterShaderPassName("GenerateMoments");
            _OITFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueUtils.oit,
            };
        }

        MomentsCount _MomentsCount;
        public FloatPrecision _MomentsPrecision;

        public static bool GetViewDepthMinMaxWithRenderQueue(Camera camera, RenderQueueRange range,
            out Vector2 minMax)
        {
            minMax = Vector2.zero;
            bool b = false;
            Bounds bounds = new Bounds();

            Renderer[] coms = Renderer.FindObjectsOfType<Renderer>();

            if (null == coms || 0 == coms.Length)
            {
                return false;
            }
            foreach (var p in coms)
            {
                Renderer r = p.GetComponent<Renderer>();
                if (null != r && r.enabled
                    && r.sharedMaterial.renderQueue >= range.min && r.sharedMaterial.renderQueue <= range.max)
                {
                    if (r is SkinnedMeshRenderer)
                    {
                        (r as SkinnedMeshRenderer).sharedMesh.RecalculateBounds();
                    }
                    Bounds rb = r.bounds;
                    if (b)
                    {
                        bounds.Encapsulate(rb);
                    }
                    else
                    {
                        bounds = rb;
                        b = true;
                    }
                }
            }
            if (!b)
            {
                return false;
            }

            Vector3 fwd = camera.transform.forward;
            Vector3 c2b = bounds.center - camera.transform.position;
            float c2bDis = Vector3.Dot(fwd, c2b);
            float bs = bounds.extents.magnitude;
            
            minMax.x = Mathf.Max(0, c2bDis - bs);
            minMax.y = c2bDis + bs;

            return true;
        }

        /// <summary>
        /// Configure the pass before execution
        /// </summary>
        /// <param name="baseDescriptor">Current target descriptor</param>
        /// <param name="colorAttachmentHandle">Color attachment to render into</param>
        /// <param name="depthAttachmentHandle">Depth attachment to render into</param>
        /// <param name="configuration">Specific render configuration</param>
        public bool Setup(
            RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle colorAttachmentHandle,
            RenderTargetHandle depthAttachmentHandle,
            RendererConfiguration configuration,
            SampleCount samples,
            RenderingData renderingData)
        {
            if (!GetViewDepthMinMaxWithRenderQueue(renderingData.cameraData.camera, RenderQueueUtils.oit, out _ViewDepthMinMax))
            {
                return false;
            }
            this._ColorAttachmentHandle = colorAttachmentHandle;
            this._DepthAttachmentHandle = depthAttachmentHandle;
            _RendererConfiguration = configuration;

            if ((int)samples > 1)
            {
                baseDescriptor.bindMS = false;
                baseDescriptor.msaaSamples = (int)samples;
            }

            baseDescriptor.depthBufferBits = 0;

            _Descriptor = baseDescriptor;

            _MomentsPrecision = renderingData.cameraData.momentsPrecision;

            if (_MomentsPrecision == FloatPrecision._Single)
            {
                baseDescriptor.colorFormat = RenderTextureFormat.ARGBFloat;
                _DescriptorFloat4 = baseDescriptor;

                baseDescriptor.colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat)
                    ? RenderTextureFormat.RGFloat
                    : RenderTextureFormat.ARGBFloat;
                _DescriptorFloat2 = baseDescriptor;

                baseDescriptor.colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat)
                    ? RenderTextureFormat.RFloat
                    : RenderTextureFormat.ARGBFloat;
                _DescriptorFloat = baseDescriptor;
            }
            else
            {
                baseDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
                _DescriptorFloat4 = baseDescriptor;

                baseDescriptor.colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)
                    ? RenderTextureFormat.RGHalf
                    : RenderTextureFormat.ARGBHalf;
                _DescriptorFloat2 = baseDescriptor;

                baseDescriptor.colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf)
                    ? RenderTextureFormat.RHalf
                    : RenderTextureFormat.ARGBHalf;
                _DescriptorFloat = baseDescriptor;
            }

            _B0Handle.Init("_B0");
            _B1Handle.Init("_B1");
            _B2Handle.Init("_B2");

            _MomentsCount = (MomentsCount)renderingData.cameraData.momentsCount;

            if (MomentsCount._4 != _MomentsCount)
            {
                _GMBinding = new RenderTargetBinding(
                new RenderTargetIdentifier[]
                {
                _B0Handle.Identifier(),
                _B1Handle.Identifier(),
                _B2Handle.Identifier(),
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
            else
            {
                _GMBinding = new RenderTargetBinding(
                new RenderTargetIdentifier[]
                {
                _B0Handle.Identifier(),
                _B1Handle.Identifier(),
                },
                new RenderBufferLoadAction[]
                {
                RenderBufferLoadAction.DontCare,
                RenderBufferLoadAction.DontCare,
                },
                new RenderBufferStoreAction[]
                {
                RenderBufferStoreAction.Store,
                RenderBufferStoreAction.Store,
                },
                _DepthAttachmentHandle.Identifier(),
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.DontCare);
            }

            _MOITHandle.Init("_MOIT");
            _GIALHandle.Init("_GIAL");
            _RMBinding = new RenderTargetBinding(
                new RenderTargetIdentifier[]
                {
                _MOITHandle.Identifier(),
                _GIALHandle.Identifier(),
                },
                new RenderBufferLoadAction[]
                {
                RenderBufferLoadAction.DontCare,
                RenderBufferLoadAction.DontCare,
                },
                new RenderBufferStoreAction[]
                {
                RenderBufferStoreAction.Store,
                RenderBufferStoreAction.Store,
                },
                _DepthAttachmentHandle.Identifier(),
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.DontCare
                );
            return true;
        }


        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (_B0Handle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_B0Handle.id);
                _B0Handle = RenderTargetHandle.CameraTarget;
            }
            if (_B1Handle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_B1Handle.id);
                _B1Handle = RenderTargetHandle.CameraTarget;
            }
            if (_B2Handle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_B2Handle.id);
                _B2Handle = RenderTargetHandle.CameraTarget;
            }
            if (_MOITHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_MOITHandle.id);
                _MOITHandle = RenderTargetHandle.CameraTarget;
            }
            if (_GIALHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_GIALHandle.id);
                _GIALHandle = RenderTargetHandle.CameraTarget;
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

                cmd.GetTemporaryRT(_B0Handle.id, _DescriptorFloat);
                cmd.GetTemporaryRT(_B1Handle.id, _DescriptorFloat4);
                if (MomentsCount._8 == _MomentsCount)
                {
                    cmd.GetTemporaryRT(_B2Handle.id, _DescriptorFloat4);
                }
                else if (MomentsCount._6 == _MomentsCount)
                {
                    cmd.GetTemporaryRT(_B2Handle.id, _DescriptorFloat2);
                }
                CoreUtils.SetKeyword(cmd, "_MOMENT6", MomentsCount._6 == _MomentsCount);
                CoreUtils.SetKeyword(cmd, "_MOMENT8", MomentsCount._8 == _MomentsCount);
                CoreUtils.SetKeyword(cmd, "_MOMENT_HALF_PRECISION", FloatPrecision._Half == _MomentsPrecision);
                CoreUtils.SetKeyword(cmd, "_TRIGONOMETRIC", _Trigonometric);

                cmd.SetRenderTarget(_GMBinding);
                cmd.ClearRenderTarget(false, true, Color.black);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Vector2 logViewDepthMinDelta = new Vector2(Mathf.Log(_ViewDepthMinMax.x), Mathf.Log(_ViewDepthMinMax.y));
                logViewDepthMinDelta.y = logViewDepthMinDelta.y - logViewDepthMinDelta.x;
                cmd.SetGlobalVector("_LogViewDepthMinDelta", logViewDepthMinDelta);
                //cmd.SetGlobalFloat("_Overestimation", 0.25f);
                //cmd.SetGlobalFloat("_MomentBias", 0);

                if (_Trigonometric)
                {
                    Vector4 _WrappingZoneParameters = new Vector4();
                    _WrappingZoneParameters.x = 3.14f;
                    _WrappingZoneParameters.y = 3.14f - 0.5f * _WrappingZoneParameters.x;
                    float a = _WrappingZoneParameters.y * 2;
                    float x = Mathf.Cos(a);
                    float y = Mathf.Sin(a);
                    float r = Mathf.Abs(y) - Mathf.Abs(x);
                    r = (x < 0) ? (2.0f - r) : r;
                    r = (y < 0) ? (6.0f - r) : r;
                    _WrappingZoneParameters.z = 1 / (7 - r);
                    _WrappingZoneParameters.w = 1 - 7 * _WrappingZoneParameters.z;
                    cmd.SetGlobalVector("_WrappingZoneParameters", _WrappingZoneParameters);
                }

                Camera camera = renderingData.cameraData.camera;
                var drawSettings = CreateDrawRendererSettings(camera, SortFlags.None, _RendererConfiguration, renderingData.supportsDynamicBatching);
                
                context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, _OITFilterSettings);

                // Render objects that did not match any shader pass with error shader
                renderer.RenderObjectsWithError(context, ref renderingData.cullResults, camera, _OITFilterSettings, SortFlags.None);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.GetTemporaryRT(_MOITHandle.id, _Descriptor);
                if (renderingData.shadowData.supportsDeepShadowMaps)
                {
                    cmd.GetTemporaryRT(_GIALHandle.id, _Descriptor);
                    cmd.SetRenderTarget(_RMBinding);
                    cmd.ClearRenderTarget(false, true, Color.black);
                }
                else
                {
                    CoreUtils.SetRenderTarget(cmd,
                        _MOITHandle.Identifier(), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                        _DepthAttachmentHandle.Identifier(), RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare,
                        ClearFlag.Color, Color.black);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetGlobalTexture("_b0", _B0Handle.id);
                cmd.SetGlobalTexture("_b1", _B1Handle.id);
                if (MomentsCount._4 != _MomentsCount)
                {
                    cmd.SetGlobalTexture("_b2", _B2Handle.id);
                }

                drawSettings.SetShaderPassName(0, new ShaderPassName("ResolveMoments"));
                context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, _OITFilterSettings);
                // Render objects that did not match any shader pass with error shader
                renderer.RenderObjectsWithError(context, ref renderingData.cullResults, camera, _OITFilterSettings, SortFlags.None);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();


                CoreUtils.SetRenderTarget(cmd, 
                    _ColorAttachmentHandle.Identifier(), RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 
                    ClearFlag.None);
                cmd.Blit(_ColorAttachmentHandle.Identifier(), _ColorAttachmentHandle.Identifier(), renderer.GetMaterial(MaterialHandle.MomentOITComposite));
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

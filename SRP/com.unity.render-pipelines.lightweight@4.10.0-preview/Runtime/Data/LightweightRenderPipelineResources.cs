using UnityEngine.Serialization;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class LightweightRenderPipelineResources : ScriptableObject
    {
        [FormerlySerializedAs("BlitShader"), SerializeField] Shader m_BlitShader = null;
        [FormerlySerializedAs("CopyDepthShader"), SerializeField] Shader m_CopyDepthShader = null;
        [FormerlySerializedAs("ScreenSpaceShadowShader"), SerializeField] Shader m_ScreenSpaceShadowShader = null;
        [FormerlySerializedAs("SamplingShader"), SerializeField] Shader m_SamplingShader = null;
        [FormerlySerializedAs("ScreenSpaceDeepShadowMapsShader"), SerializeField] Shader _ScreenSpaceDeepShadowMapsShader = null;
        [FormerlySerializedAs("GaussianBlurShader"), SerializeField] Shader _GaussianBlurShader = null;
        [FormerlySerializedAs("OITCompositeShader"), SerializeField] Shader _OITCompositeShader = null;
        [FormerlySerializedAs("MomentOITCompositeShader"), SerializeField] Shader _MomentOITCompositeShader = null;
        [FormerlySerializedAs("ResetDeepShadowDataCompute"), SerializeField] ComputeShader _ResetDeepShadowDataCompute = null;

        public Shader blitShader
        {
            get { return m_BlitShader; }
        }

        public Shader copyDepthShader
        {
            get { return m_CopyDepthShader; }
        }

        public Shader screenSpaceShadowShader
        {
            get { return m_ScreenSpaceShadowShader; }
        }

        public Shader samplingShader
        {
            get { return m_SamplingShader; }
        }

        public Shader screenSpaceDeepShadowMapsShader
        {
            get { return _ScreenSpaceDeepShadowMapsShader; }
        }

        public Shader gaussianBlurShader
        {
            get { return _GaussianBlurShader; }
        }

        public Shader oitCompositeShader
        {
            get { return _OITCompositeShader; }
        }

        public Shader momentOITCompositeShader
        {
            get { return _MomentOITCompositeShader; }
        }

        public ComputeShader resetDeepShadowDataCompute
        {
            get { return _ResetDeepShadowDataCompute; }
        }
    }
}

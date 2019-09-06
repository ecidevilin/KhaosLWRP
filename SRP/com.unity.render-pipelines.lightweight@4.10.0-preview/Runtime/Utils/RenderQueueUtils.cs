using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public static class RenderQueueUtils
    {
        public static RenderQueueRange all = new RenderQueueRange()
        {
            min = 0,
            max = 5000,
        };
        public static RenderQueueRange opaque = new RenderQueueRange()
        {
            min = 0,
            max = 2500,
        };
        public static RenderQueueRange transparent = new RenderQueueRange()
        {
            min = 2501,
            max = 4500,
        };
        public static RenderQueueRange oit = new RenderQueueRange()
        {
            min = 4501,
            max = 5000,
        };
    }
}

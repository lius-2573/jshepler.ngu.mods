using System;
using UnityEngine;

namespace jshepler.ngu.mods
{
    internal static class AutomationThrottle
    {
        private const int DefaultFrameInterval = 60;
        private const float DefaultWishIntervalSeconds = 1f;

        internal static bool ShouldRunEveryFrames(ref int lastFrame)
        {
            var interval = Options.Performance.FrameCheckInterval?.Value ?? DefaultFrameInterval;
            if (interval < 1)
                interval = 1;

            var frame = Time.frameCount;
            if (lastFrame >= 0 && frame - lastFrame < interval)
                return false;

            lastFrame = frame;
            return true;
        }

        internal static bool ShouldRunEverySeconds(ref float lastTime)
        {
            var interval = Options.Performance.WishCheckIntervalSeconds?.Value ?? DefaultWishIntervalSeconds;
            if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0f)
                interval = DefaultWishIntervalSeconds;

            var now = Time.unscaledTime;
            if (!float.IsNegativeInfinity(lastTime) && now - lastTime < interval)
                return false;

            lastTime = now;
            return true;
        }

        internal static void Reset(ref int lastFrame)
        {
            lastFrame = -1;
        }

        internal static void Reset(ref float lastTime)
        {
            lastTime = float.NegativeInfinity;
        }
    }
}

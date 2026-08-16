using System;

namespace ErenshorPvP
{
    // Pure policy for the common Forgotten Roads module-window header contract.
    internal static class PvpWindowChromePolicy
    {
        internal const float ExpandedHeight = 520f;
        internal const float HeaderHeight = 34f;
        internal const float CollapsedHeight = HeaderHeight;

        internal static float Height(bool collapsed)
        {
            return collapsed ? CollapsedHeight : ExpandedHeight;
        }

        // PvpPanel uses a bottom-left pivot. Preserve the visual top edge when switching between
        // expanded and header-only states so collapse never makes the window jump on screen.
        internal static float PreserveTopBottomY(float oldBottomY, float oldHeight, float newHeight)
        {
            if (float.IsNaN(oldBottomY) || float.IsInfinity(oldBottomY)) oldBottomY = 0f;
            if (float.IsNaN(oldHeight) || float.IsInfinity(oldHeight) || oldHeight < 0f) oldHeight = 0f;
            if (float.IsNaN(newHeight) || float.IsInfinity(newHeight) || newHeight < 0f) newHeight = 0f;
            return oldBottomY + oldHeight - newHeight;
        }

        internal static bool ChevronPointsUp(bool collapsed)
        {
            // Expanded -> up arrow means collapse. Collapsed -> down arrow means expand.
            return !collapsed;
        }

        internal static string RunSelfTests()
        {
            if (Math.Abs(Height(false) - 520f) > .001f) return "FAIL expanded height";
            if (Math.Abs(Height(true) - 34f) > .001f) return "FAIL collapsed height";
            if (Math.Abs(PreserveTopBottomY(100f, 520f, 34f) - 586f) > .001f) return "FAIL top preservation collapse";
            if (Math.Abs(PreserveTopBottomY(586f, 34f, 520f) - 100f) > .001f) return "FAIL top preservation expand";
            if (!ChevronPointsUp(false) || ChevronPointsUp(true)) return "FAIL chevron direction";
            return "PASS pvp module header chrome";
        }
    }
}

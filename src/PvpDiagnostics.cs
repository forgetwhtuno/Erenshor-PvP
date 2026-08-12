using UnityEngine;

namespace ErenshorPvP
{
    // Development-only evidence stream. Core failures and final encounter results remain logged
    // independently; this switch controls the high-detail validation lines used during acceptance.
    internal static class PvpDiagnostics
    {
        internal static void Log(string value)
        {
            if (!PvpController.ValidationLogging) return;
            try { Debug.Log("[Erenshor PvP] " + value); } catch { }
        }

        internal static void Warning(string value)
        {
            if (!PvpController.ValidationLogging) return;
            try { Debug.LogWarning("[Erenshor PvP] " + value); } catch { }
        }
    }
}

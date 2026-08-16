using System;

namespace ErenshorPvP
{
    // Unity-free player-facing label policy so retained controls cannot regress back to
    // ambiguous "Toggle ..." wording. Runtime state remains authoritative in PvpController.
    internal static class PvpUiPresentation
    {
        internal static string ToggleLabel(string label, bool enabled)
        {
            return (label ?? string.Empty) + " [" + (enabled ? "ON" : "OFF") + "]";
        }

        internal static string RunSelfTests()
        {
            if (ToggleLabel("PvP Enabled", true) != "PvP Enabled [ON]") return "FAIL pvp enabled label";
            if (ToggleLabel("Arranged Challenges", false) != "Arranged Challenges [OFF]") return "FAIL arranged label";
            if (ToggleLabel("Wild Ambushes", true) != "Wild Ambushes [ON]") return "FAIL ambush label";
            return "PASS pvp explicit toggle labels";
        }
    }
}

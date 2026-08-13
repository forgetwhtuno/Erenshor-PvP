using System;
using ErenshorPvP;

internal static class PvpUiPolicyTests
{
    private static int Main()
    {
        try
        {
            Assert(PvpUiGeometry.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "normalized retained position policy");
            Assert(SuiteLauncherPolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "mandatory launcher fallback policy");
            Assert(PvpHubPresentation.Build(true, false) == "Enabled | Idle", "idle hub status exact");
            Assert(PvpHubPresentation.Build(true, true) == "Enabled | Match active", "active hub status exact");
            Assert(PvpHubPresentation.Build(false, false).Length < 240, "hub status remains bounded");
            Console.WriteLine("PvpUiPolicyTests: PASS"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("PvpUiPolicyTests: FAIL " + ex.Message); return 1; }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

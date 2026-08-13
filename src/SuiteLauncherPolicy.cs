namespace ErenshorPvP
{
    // Pure launcher visibility rule shared by retained UI and deterministic tests.
    internal static class SuiteLauncherPolicy
    {
        internal static bool ShouldShow(bool gameplayReady, bool hubAvailable, bool bridgeRegistered, bool explicitlyVisibleWithHub)
        {
            return gameplayReady && (explicitlyVisibleWithHub || !hubAvailable || !bridgeRegistered);
        }

        internal static string RunSelfTests()
        {
            if (ShouldShow(false, false, false, false)) return "FAIL launcher before gameplay ready";
            if (!ShouldShow(true, false, false, false)) return "FAIL launcher fallback without hub";
            if (!ShouldShow(true, true, false, false)) return "FAIL launcher fallback without bridge";
            if (ShouldShow(true, true, true, false)) return "FAIL launcher hidden-with-hub policy";
            if (!ShouldShow(true, true, true, true)) return "FAIL launcher explicit-with-hub policy";
            return "PASS pvp launcher visibility";
        }
    }
}

namespace ErenshorPvP
{
    internal static class PvpHubPresentation
    {
        internal static string Build(bool enabled, bool encounterActive)
        {
            return (enabled ? "Enabled" : "Disabled") + " | " + (encounterActive ? "Match active" : "Idle");
        }
    }
}

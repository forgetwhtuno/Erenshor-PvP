using System;
using System.Globalization;

namespace ErenshorPvP
{
    // Pure wire policy for Suite quick-close. Runtime UI ownership stays in PvpPanel.
    internal static class PvpUiStatePolicy
    {
        internal static string Build(string moduleId, bool open, int sortOrder, double activated)
        {
            if (string.IsNullOrEmpty(moduleId)) return string.Empty;
            if (sortOrder < -10000) sortOrder = -10000;
            if (sortOrder > 10000) sortOrder = 10000;
            if (double.IsNaN(activated) || double.IsInfinity(activated) || activated < 0d) activated = 0d;
            return "protocol=1&module=" + moduleId
                + "&open=" + (open ? "true" : "false")
                + "&closeable=true&sortOrder=" + sortOrder.ToString(CultureInfo.InvariantCulture)
                + "&activated=" + activated.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

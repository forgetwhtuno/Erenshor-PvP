using System;
using System.Collections.Generic;

namespace ErenshorPvP
{
    internal static class PvpPluginIdentityPolicy
    {
        internal const string ExpectedAssemblyIdentity = "ErenshorPvP";

        internal static bool ExactlyOneExpectedIdentity(IEnumerable<string> assemblyIdentities)
        {
            int count = 0;
            if (assemblyIdentities != null)
            {
                foreach (string identity in assemblyIdentities)
                    if (string.Equals(identity, ExpectedAssemblyIdentity, StringComparison.OrdinalIgnoreCase)) count++;
            }
            return count == 1;
        }
    }
}

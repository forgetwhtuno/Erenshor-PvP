using System;

namespace ErenshorPvP
{
    internal enum PvpEncounterMode { Arranged, Ambush }

    internal sealed class PvpEncounterFlavor
    {
        internal readonly string Motive;
        internal readonly string SystemLine;
        internal readonly string LeaderLine;
        internal PvpEncounterFlavor(string motive, string systemLine, string leaderLine)
        { Motive = motive; SystemLine = systemLine; LeaderLine = leaderLine; }
    }

    internal static class PvpEncounterFlavorFactory
    {
        internal static PvpEncounterFlavor Create(PvpEncounterMode mode, PvpTeamPlan team, int seed)
        { return Create(mode, team, seed, false); }

        internal static PvpEncounterFlavor Create(PvpEncounterMode mode, PvpTeamPlan team, int seed, bool verifiedHuntCamp)
        {
            string leader = team == null || string.IsNullOrWhiteSpace(team.LeaderName) ? "A rival" : team.LeaderName;
            string guild = team == null || team.Members.Count == 0 || team.Members[0].Profile == null ? string.Empty : team.Members[0].Profile.GuildId;
            Random random = new Random(seed);
            if (mode == PvpEncounterMode.Arranged)
            {
                if (!string.IsNullOrWhiteSpace(guild) && random.Next(100) < 45)
                    return new PvpEncounterFlavor("guild_challenge", "Guild challenge from " + guild + ".", leader + ": our guild wants a proper match. You in?");
                return new PvpEncounterFlavor("party_match", "An opposing party proposes an arranged match.", leader + ": your side versus ours, fair and clean. You in?");
            }
            if (verifiedHuntCamp && random.Next(100) < 45)
                return new PvpEncounterFlavor("camp_claim", "A hostile party moves in to seize your verified Hunt Camp spot.", leader + ": we're taking this camp. Nothing personal.");
            int choice = random.Next(string.IsNullOrWhiteSpace(guild) ? 2 : 3);
            if (choice == 0) return new PvpEncounterFlavor("killing_spree", "A roaming party on a killing spree has found you.", leader + ": there you are. Let's see what you've got.");
            if (choice == 1) return new PvpEncounterFlavor("territory", "A rival party attacks to drive you out of its territory.", leader + ": wrong place to settle in. Fight or run.");
            return new PvpEncounterFlavor("guild_raid", "A hostile " + guild + " party launches a wild guild raid.", leader + ": " + guild + " owns this ground today.");
        }

        internal static string RunSelfTests()
        {
            PvpEncounterFlavor arranged = Create(PvpEncounterMode.Arranged, null, 1);
            PvpEncounterFlavor ambush = Create(PvpEncounterMode.Ambush, null, 1);
            if (arranged == null || string.IsNullOrWhiteSpace(arranged.Motive) || arranged.LeaderLine.IndexOf("You in?", StringComparison.Ordinal) < 0) return "FAIL arranged flavor";
            if (ambush == null || string.IsNullOrWhiteSpace(ambush.Motive) || ambush.LeaderLine.IndexOf("You in?", StringComparison.Ordinal) >= 0) return "FAIL ambush flavor";
            for (int seed = 0; seed < 100; seed++) if (Create(PvpEncounterMode.Ambush, null, seed, false).Motive == "camp_claim") return "FAIL unverified camp claim";
            bool sawCamp = false; for (int seed = 0; seed < 100; seed++) if (Create(PvpEncounterMode.Ambush, null, seed, true).Motive == "camp_claim") { sawCamp = true; break; }
            if (!sawCamp) return "FAIL verified camp motive";
            return "PASS pvp encounter modes";
        }
    }
}

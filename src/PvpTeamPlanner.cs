using System;
using System.Collections.Generic;
using System.Linq;

namespace ErenshorPvP
{
    internal enum PvpCombatRole { Vanguard, Striker, Caster, Support }

    // Read-only snapshot of one live PvP proxy, used by the panel roster.
    internal sealed class PvpRosterEntry
    {
        internal readonly string Name;
        internal readonly int Level;
        internal readonly string ClassName;
        internal readonly string GuildId;
        internal readonly PvpCombatRole Role;
        internal readonly int CurrentHp;
        internal readonly int MaxHp;
        internal readonly int KnownSpells;
        internal readonly bool Alive;

        internal PvpRosterEntry(string name, int level, string className, string guildId, PvpCombatRole role,
            int currentHp, int maxHp, int knownSpells, bool alive)
        {
            Name = string.IsNullOrEmpty(name) ? "Proxy" : name;
            Level = level;
            ClassName = string.IsNullOrEmpty(className) ? "Unknown" : className;
            GuildId = guildId ?? string.Empty;
            Role = role;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            KnownSpells = knownSpells;
            Alive = alive;
        }

        internal string HealthText
        {
            get { return MaxHp <= 0 ? "?" : CurrentHp + "/" + MaxHp; }
        }

        internal float HealthFraction
        {
            get { return MaxHp <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (float)CurrentHp / MaxHp)); }
        }
    }

    internal sealed class PvpTeamMember
    {
        internal readonly PvpOpponentProfile Profile;
        internal readonly PvpCombatRole Role;
        internal PvpTeamMember(PvpOpponentProfile profile) { Profile = profile; Role = RoleFor(profile == null ? null : profile.ClassName); }

        internal static PvpCombatRole RoleFor(string className)
        {
            string value = (className ?? string.Empty).ToLowerInvariant();
            if (value.Contains("paladin") || value.Contains("reaver")) return PvpCombatRole.Vanguard;
            if (value.Contains("druid")) return PvpCombatRole.Support;
            if (value.Contains("arcanist") || value.Contains("storm")) return PvpCombatRole.Caster;
            return PvpCombatRole.Striker;
        }
    }

    internal sealed class PvpTeamPlan
    {
        internal readonly List<PvpTeamMember> Members;
        internal PvpTeamPlan(List<PvpTeamMember> members) { Members = members ?? new List<PvpTeamMember>(); }
        internal string LeaderName { get { return Members.Count == 0 ? string.Empty : Members[0].Profile.Name; } }
        internal int AverageLevel { get { return Members.Count == 0 ? 0 : (int)Math.Round(Members.Average(x => x.Profile.Level)); } }
        internal string DescribeCompact()
        {
            return string.Join(", ", Members.Where(x => x != null && x.Profile != null)
                .Select(x => x.Profile.Name + " L" + x.Profile.Level + " " +
                    (string.IsNullOrWhiteSpace(x.Profile.ClassName) ? "Unknown" : x.Profile.ClassName) + "/" + x.Role).ToArray());
        }
    }

    internal static class PvpTeamPlanner
    {
        internal static PvpTeamPlan Build(int defenderCount, int defenderLevel, IList<PvpOpponentProfile> candidates, int seed)
        { return Build(defenderCount, defenderLevel, candidates, seed, -1); }

        internal static PvpTeamPlan Build(int defenderCount, int defenderLevel, IList<PvpOpponentProfile> candidates, int seed, int requestedSize)
        { return Build(defenderCount, defenderLevel, candidates, seed, requestedSize, null); }

        internal static PvpTeamPlan Build(int defenderCount, int defenderLevel, IList<PvpOpponentProfile> candidates, int seed, int requestedSize, string preferredLeader)
        {
            List<PvpOpponentProfile> pool = candidates == null ? new List<PvpOpponentProfile>() : candidates.Where(x => x != null).ToList();
            if (pool.Count == 0) return new PvpTeamPlan(null);
            Random random = new Random(seed);
            int desired = requestedSize >= 1 && requestedSize <= 5 ? requestedSize : DesiredSize(defenderCount, random);
            // A requested leader is only rejected when they are genuinely not in the eligible pool.
            // Party-composition rules then adapt around them instead of failing the request: they
            // stay leader, and a leader too weak to attack alone simply brings a partner.
            bool hasPreferred = !string.IsNullOrWhiteSpace(preferredLeader);
            PvpOpponentProfile preferred = null;
            if (hasPreferred)
            {
                preferred = pool.FirstOrDefault(x => string.Equals(x.Name, preferredLeader, StringComparison.OrdinalIgnoreCase));
                if (preferred == null) return new PvpTeamPlan(null);
            }
            List<PvpOpponentProfile> leaderPool = pool;
            if (defenderCount == 2 && desired == 1)
            {
                List<PvpOpponentProfile> clearlyStronger = pool.Where(x => x.Level >= defenderLevel + 2).ToList();
                if (hasPreferred) { if (preferred.Level < defenderLevel + 2) desired = 2; }
                else if (clearlyStronger.Count == 0) desired = 2;
                else leaderPool = clearlyStronger;
            }
            if (defenderCount >= 4)
            {
                List<PvpOpponentProfile> atOrAbove = pool.Where(x => x.Level >= defenderLevel).ToList();
                // Full defender parties should not receive one qualifying leader padded with
                // lower-level guildmates when a complete at-or-above-level team exists.
                if (atOrAbove.Count >= desired) { pool = atOrAbove; if (!hasPreferred) leaderPool = pool; }
                else if (atOrAbove.Count > 0 && !hasPreferred) leaderPool = atOrAbove;
            }
            PvpOpponentProfile leader = hasPreferred ? preferred : leaderPool[random.Next(leaderPool.Count)];
            List<PvpTeamMember> members = new List<PvpTeamMember> { new PvpTeamMember(leader) };
            List<PvpOpponentProfile> remaining = pool.Where(x => x != leader).ToList();
            while (members.Count < desired && remaining.Count > 0)
            {
                HashSet<PvpCombatRole> roles = new HashSet<PvpCombatRole>(members.Select(x => x.Role));
                PvpOpponentProfile profile = remaining
                    .OrderByDescending(x => SameGuild(leader, x))
                    .ThenByDescending(x => !roles.Contains(PvpTeamMember.RoleFor(x.ClassName)))
                    .ThenBy(x => Math.Abs(x.Level - defenderLevel))
                    .ThenBy(x => random.Next())
                    .First();
                members.Add(new PvpTeamMember(profile)); remaining.Remove(profile);
            }
            return new PvpTeamPlan(members);
        }

        private static int DesiredSize(int defenders, Random random)
        {
            defenders = Math.Max(1, Math.Min(5, defenders));
            int roll = random.Next(100);
            // Extreme outnumbering remains possible for MMO-style danger, but it should
            // be memorable rather than the normal solo experience.
            if (defenders == 1)
            {
                if (roll < 35) return 1;
                if (roll < 65) return 2;
                if (roll < 85) return 3;
                if (roll < 95) return 4;
                return 5;
            }
            if (defenders == 2)
            {
                if (roll < 15) return 1;
                if (roll < 70) return 2;
                return 3;
            }
            if (defenders == 3)
            {
                if (roll < 55) return 3;
                if (roll < 85) return 4;
                return 5;
            }
            return 5;
        }

        private static bool SameGuild(PvpOpponentProfile left, PvpOpponentProfile right)
        {
            return left != null && right != null && !string.IsNullOrWhiteSpace(left.GuildId) &&
                   string.Equals(left.GuildId, right.GuildId, StringComparison.OrdinalIgnoreCase);
        }

        internal static string RunSelfTests()
        {
            if (PvpTeamMember.RoleFor("Paladin") != PvpCombatRole.Vanguard) return "FAIL role paladin";
            if (PvpTeamMember.RoleFor("Druid") != PvpCombatRole.Support) return "FAIL role druid";
            if (PvpTeamMember.RoleFor("Arcanist") != PvpCombatRole.Caster) return "FAIL role arcanist";
            List<PvpOpponentProfile> pool = new List<PvpOpponentProfile>();
            string[] classes = { "Paladin", "Druid", "Arcanist", "Windblade" };
            for (int i = 0; i < 8; i++) pool.Add(PvpOpponentProfile.ForTest("Sim" + i, 10 + (i % 2), classes[i % classes.Length], i < 4 ? "A" : "B"));
            bool[] soloSizes = new bool[6];
            int[] soloCounts = new int[6];
            for (int seed = 0; seed < 1000; seed++)
            {
                int size = Build(1, 10, pool, seed).Members.Count;
                soloSizes[size] = true; soloCounts[size]++;
            }
            for (int size = 1; size <= 5; size++) if (!soloSizes[size]) return "FAIL solo party distribution " + size;
            if (soloCounts[5] >= soloCounts[1] || soloCounts[4] >= soloCounts[2]) return "FAIL solo extreme sizes are not rare";
            for (int seed = 0; seed < 100; seed++) if (Build(2, 10, pool, seed).Members.Count < 2) return "FAIL weak lone attacker vs duo";
            PvpTeamPlan full = Build(5, 10, pool, 4);
            if (full.Members.Count != 5 || full.AverageLevel < 10) return "FAIL full party scaling";
            if (full.Members.Select(x => x.Role).Distinct().Count() < 3) return "FAIL role diversity";
            List<PvpOpponentProfile> mixed = new List<PvpOpponentProfile>();
            for (int i = 0; i < 5; i++) mixed.Add(PvpOpponentProfile.ForTest("High" + i, 12, classes[i % classes.Length], "HighGuild"));
            for (int i = 0; i < 5; i++) mixed.Add(PvpOpponentProfile.ForTest("Low" + i, 8, classes[i % classes.Length], "LowGuild"));
            PvpTeamPlan strongFull = Build(5, 10, mixed, 17);
            if (strongFull.Members.Count != 5 || strongFull.Members.Any(x => x.Profile.Level < 10)) return "FAIL full party admitted lower-level member despite complete strong pool";
            PvpTeamPlan guild = Build(3, 10, pool, 8);
            if (guild.Members.Count > 1 && pool.Count(x => x.GuildId == guild.Members[0].Profile.GuildId) > 1 &&
                guild.Members[1].Profile.GuildId != guild.Members[0].Profile.GuildId) return "FAIL guild preference";
            PvpTeamPlan preferred = Build(3, 10, pool, 8, 3, "Sim5");
            if (preferred.Members.Count != 3 || preferred.LeaderName != "Sim5") return "FAIL preferred leader";
            if (Build(3, 10, pool, 8, 3, "Missing").Members.Count != 0) return "FAIL missing preferred leader";
            // A requested leader who cannot legally attack a duo alone must bring a partner
            // rather than causing the whole request to be rejected, and must stay leader.
            for (int seed = 0; seed < 100; seed++)
            {
                PvpTeamPlan duo = Build(2, 10, pool, seed, 1, "Sim0");
                if (duo.Members.Count != 2 || duo.LeaderName != "Sim0") return "FAIL weak preferred leader vs duo";
            }
            PvpTeamPlan weakLeaderFull = Build(5, 10, mixed, 21, 5, "Low0");
            if (weakLeaderFull.Members.Count != 5 || weakLeaderFull.LeaderName != "Low0") return "FAIL weak preferred leader vs full party";
            if (weakLeaderFull.Members.Skip(1).Any(x => x.Profile.Level < 10)) return "FAIL full party strength around preferred leader";
            if (Build(5, 10, mixed, 21, 5, "High0").Members.Any(x => x.Profile.Level < 10)) return "FAIL strong preferred leader full party strength";
            return "PASS pvp team planner";
        }
    }
}

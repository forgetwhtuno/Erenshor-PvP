using System;
using System.Collections.Generic;

namespace ErenshorPvP
{
    public sealed class PvpSemanticEvent
    {
        public string Type { get; private set; }
        public string MatchId { get; private set; }
        public string OpponentName { get; private set; }
        public string Zone { get; private set; }
        public string Decision { get; private set; }
        public string ReasonToken { get; private set; }
        // PvP's own verdict for this event, so a consumer never re-derives meaning from the raw
        // reason token. Empty for events that carry no terminal verdict.
        public string Classification { get; private set; }

        internal PvpSemanticEvent(string type, string matchId, string opponentName, string zone,
            string decision, string reasonToken)
            : this(type, matchId, opponentName, zone, decision, reasonToken, string.Empty) { }

        internal PvpSemanticEvent(string type, string matchId, string opponentName, string zone,
            string decision, string reasonToken, string classification)
        {
            Type = Clean(type); MatchId = Clean(matchId); OpponentName = Clean(opponentName);
            Zone = Clean(zone); Decision = Clean(decision); ReasonToken = Clean(reasonToken);
            Classification = Clean(classification);
        }

        public string ToObservedGameEventDescription()
        {
            List<string> fields = new List<string>();
            Add(fields, "type", Type); Add(fields, "match_id", MatchId);
            Add(fields, "opponent", OpponentName); Add(fields, "zone", Zone);
            Add(fields, "decision", Decision); Add(fields, "reason_token", ReasonToken);
            Add(fields, "classification", Classification);
            return string.Join("; ", fields.ToArray());
        }

        private static void Add(List<string> fields, string key, string value)
        { if (!string.IsNullOrWhiteSpace(value)) fields.Add(key + "=" + value); }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Replace(';', ',').Replace('=', ':').Trim();
            return clean.Length <= 160 ? clean : clean.Substring(0, 160);
        }
    }

    public static class ErenshorPvpEvents
    {
        public const int ContractVersion = 2;
        public static event Action<PvpSemanticEvent> SemanticEvent;

        internal static void Publish(PvpSemanticEvent value)
        {
            if (value == null) return;
            Action<PvpSemanticEvent> handlers = SemanticEvent;
            if (handlers != null)
                foreach (Delegate raw in handlers.GetInvocationList())
                    try { ((Action<PvpSemanticEvent>)raw)(value); } catch { }
            try
            {
                Type bridge = FindDeepSimsBridge();
                if (bridge == null) return;
                // Prefer the classification-carrying overload so the consumer uses PvP's verdict
                // instead of re-deriving it. Older Deep Sims builds only expose the six-field form.
                System.Reflection.MethodInfo method = bridge.GetMethod("NotifyPvpEvent",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null,
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) }, null);
                if (method != null)
                {
                    method.Invoke(null, new object[] { value.Type, value.MatchId, value.OpponentName, value.Zone, value.Decision, value.ReasonToken, value.Classification });
                    return;
                }
                method = bridge.GetMethod("NotifyPvpEvent",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null,
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) }, null);
                if (method != null) method.Invoke(null, new object[] { value.Type, value.MatchId, value.OpponentName, value.Zone, value.Decision, value.ReasonToken });
            }
            catch { }
        }

        private static Type FindDeepSimsBridge()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type candidate = assembly.GetType("ErenshorDeepSims.PvpEventBridge", false);
                    if (candidate != null) return candidate;
                }
                catch { }
            }
            return null;
        }
    }

    // Stable, dependency-free surface for optional mods. Callers request; PvP decides.
    public static class ErenshorPvpApi
    {
        // v2 adds RecentResults/ClassifyOutcome. v1 consumers keep working through the Last* values.
        public const int ContractVersion = 2;
        public const int MaxRecentResults = 16;

        // Classification tokens. PvP is the authority for what a raw outcome means, so a consumer
        // never has to guess whether an internal failure was a legitimate escape.
        public const string ClassPlayerWin = "player_win";
        public const string ClassNemesisWin = "nemesis_win";
        public const string ClassPlayerFled = "player_fled";
        public const string ClassEnemyRetreated = "enemy_retreated";
        public const string ClassCancelled = "cancelled";
        public const string ClassInvalid = "invalid";

        private static readonly object ResultLock = new object();
        private static readonly List<string> RecentEncoded = new List<string>();
        private static long _sequence;

        public static string LastMatchId { get; private set; }
        public static string LastOpponent { get; private set; }
        public static string LastOutcome { get; private set; }
        public static string LastMode { get; private set; }
        public static string LastClassification { get; private set; }

        public static string RequestNemesisAmbush(string simName)
        {
            try { return PvpController.RequestNamedAmbush(simName, "nemesis"); }
            catch { return "blocked:request_failed"; }
        }

        // Bounded, non-destructive snapshot, oldest first. Each entry is
        // "sequence|match_id|opponent|outcome|mode|classification|utc_ticks".
        // Reading never consumes, so two independent consumers cannot starve each other and a
        // consumer that was not loaded yet still sees the recent history when it starts polling.
        // Consumers deduplicate on their own side using the sequence or match id.
        public static string[] RecentResults()
        {
            lock (ResultLock) return RecentEncoded.ToArray();
        }

        // Maps a raw PvP outcome token onto a classification. Internal failures, third-party
        // interference, spawn failures and cancellations are never reported as escapes.
        public static string ClassifyOutcome(string outcome)
        {
            string value = (outcome ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "proxy_death") return ClassPlayerWin;
            if (value == "player_death") return ClassNemesisWin;
            if (value == "player_fled") return ClassPlayerFled;
            if (value == "retreat") return ClassEnemyRetreated;
            if (value == "scene_transition" || value == "manual" || value == "shutdown" ||
                value == "timer" || value == "cleanup") return ClassCancelled;
            return ClassInvalid;
        }

        internal static void RecordResult(string matchId, string opponent, string outcome, string mode)
        { TryRecordResult(matchId, opponent, outcome, mode, ClassifyOutcome(outcome)); }

        internal static void RecordResult(string matchId, string opponent, string outcome, string mode, string classification)
        { TryRecordResult(matchId, opponent, outcome, mode, classification); }

        // Returns true only for the first terminal result recorded for a match. Terminal publishers
        // use this as the single deduplication authority so every consumer, including SemanticEvent
        // subscribers, observes exactly one verdict.
        internal static bool TryRecordResult(string matchId, string opponent, string outcome, string mode, string classification)
        {
            string id = Field(matchId); string who = Field(opponent);
            string reason = Field(outcome); string encounterMode = Field(mode);
            string verdict = Field(string.IsNullOrWhiteSpace(classification) ? ClassifyOutcome(outcome) : classification);
            if (id.Length == 0) return false;
            lock (ResultLock)
            {
                for (int i = 0; i < RecentEncoded.Count; i++)
                {
                    string[] existing = RecentEncoded[i].Split(new[] { '|' });
                    // One match produces one terminal record. A later cancellation/cleanup pass
                    // must never overwrite or duplicate an already reported verdict.
                    if (existing.Length > 1 && string.Equals(existing[1], id, StringComparison.Ordinal)) return false;
                }
                _sequence++;
                RecentEncoded.Add(_sequence + "|" + id + "|" + who + "|" + reason + "|" + encounterMode + "|" + verdict + "|" + DateTime.UtcNow.Ticks);
                while (RecentEncoded.Count > MaxRecentResults) RecentEncoded.RemoveAt(0);
                // Legacy v1 readers see the same first authoritative result as v2 queue readers.
                LastMatchId = id; LastOpponent = who; LastOutcome = reason;
                LastMode = encounterMode; LastClassification = verdict;
                return true;
            }
        }

        private static string Field(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= 80 ? clean : clean.Substring(0, 80);
        }

        internal static string RunSelfTests()
        {
#if SHARED_CONTRACTS
            // PvP is the authority, so it is pinned to the shared table exactly like the consumer
            // mirrors are. If this fails, the contract itself moved and every consumer must follow.
            if (ErenshorSharedContracts.PvpContractConformance.ContractVersion != ContractVersion)
                return "FAIL shared contract version " + ErenshorSharedContracts.PvpContractConformance.ContractVersion + " != api " + ContractVersion;
            string shared = ErenshorSharedContracts.PvpContractConformance.RunClassifierConformance("pvp authority", ClassifyOutcome);
            if (!shared.StartsWith("PASS", StringComparison.Ordinal)) return shared;
#endif
            if (ClassifyOutcome("proxy_death") != ClassPlayerWin) return "FAIL classify win";
            if (ClassifyOutcome("player_death") != ClassNemesisWin) return "FAIL classify loss";
            if (ClassifyOutcome("player_fled") != ClassPlayerFled) return "FAIL classify flee";
            if (ClassifyOutcome("retreat") != ClassEnemyRetreated) return "FAIL classify retreat";
            if (ClassifyOutcome("scene_transition") != ClassCancelled) return "FAIL classify cancelled";
            if (ClassifyOutcome("third_party_aggro") != ClassInvalid) return "FAIL classify interference";
            if (ClassifyOutcome("fight_state_failed") != ClassInvalid) return "FAIL classify internal failure";
            if (ClassifyOutcome("team_spawn_failed") != ClassInvalid) return "FAIL classify spawn failure";
            int before = RecentResults().Length;
            string probe = "selftest" + Guid.NewGuid().ToString("N");
            RecordResult(probe, "Probe", "proxy_death", "ambush");
            RecordResult(probe, "Probe", "scene_transition", "ambush");
            string[] after = RecentResults();
            if (after.Length != Math.Min(MaxRecentResults, before + 1)) return "FAIL result queue duplicate suppression";
            string[] fields = after[after.Length - 1].Split(new[] { '|' });
            if (fields.Length != 7 || fields[1] != probe || fields[5] != ClassPlayerWin) return "FAIL result queue encoding";
            if (LastMatchId != probe || LastOutcome != "proxy_death" || LastClassification != ClassPlayerWin) return "FAIL legacy last result overwritten by duplicate";
            RecordResult(string.Empty, "Probe", "proxy_death", "ambush");
            if (RecentResults().Length != after.Length) return "FAIL empty match id queued";
#if SHARED_CONTRACTS
            // The live queue must satisfy the row shape consumers actually parse.
            string rows = ErenshorSharedContracts.PvpContractConformance.RunRowConformance("pvp queue", RecentResults());
            if (!rows.StartsWith("PASS", StringComparison.Ordinal)) return rows;
#endif
            return "PASS pvp result contract";
        }
    }
}

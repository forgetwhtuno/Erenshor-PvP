namespace ErenshorPvP
{
    // Pure lifecycle ledger: Unity objects remain owned by PvpTemporaryCloneFactory, while this
    // records the only legal match transitions and makes terminal/cancel cleanup idempotent.
    internal enum PvpMatchLifecycleState { Disabled, Ready, PendingChallenge, Spawning, Active, CleaningUp }

    internal sealed class PvpMatchLifecyclePolicy
    {
        internal PvpMatchLifecycleState State { get; private set; }
        internal string MatchId { get; private set; }

        internal PvpMatchLifecyclePolicy(bool enabled) { State = enabled ? PvpMatchLifecycleState.Ready : PvpMatchLifecycleState.Disabled; MatchId = string.Empty; }
        internal void SetEnabled(bool enabled) { State = enabled ? PvpMatchLifecycleState.Ready : PvpMatchLifecycleState.Disabled; MatchId = string.Empty; }
        internal bool Queue(string matchId) { if (State != PvpMatchLifecycleState.Ready || string.IsNullOrEmpty(matchId)) return false; MatchId = matchId; State = PvpMatchLifecycleState.PendingChallenge; return true; }
        internal bool BeginSpawn(string matchId)
        {
            if ((State != PvpMatchLifecycleState.Ready && State != PvpMatchLifecycleState.PendingChallenge) || string.IsNullOrEmpty(matchId)) return false;
            MatchId = matchId; State = PvpMatchLifecycleState.Spawning; return true;
        }
        internal bool SpawnSucceeded() { if (State != PvpMatchLifecycleState.Spawning) return false; State = PvpMatchLifecycleState.Active; return true; }
        internal void ClearPending() { if (State == PvpMatchLifecycleState.PendingChallenge) { State = PvpMatchLifecycleState.Ready; MatchId = string.Empty; } }
        internal void BeginCleanup() { if (State == PvpMatchLifecycleState.Disabled || State == PvpMatchLifecycleState.Ready) return; State = PvpMatchLifecycleState.CleaningUp; }
        internal void CompleteCleanup(bool enabled) { State = enabled ? PvpMatchLifecycleState.Ready : PvpMatchLifecycleState.Disabled; MatchId = string.Empty; }
    }
}

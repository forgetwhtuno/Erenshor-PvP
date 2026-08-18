namespace ErenshorPvP
{
    // Pure lifecycle ledger: Unity objects remain owned by PvpTemporaryCloneFactory, while this
    // records the only legal match transitions and makes GO/terminal cleanup idempotent.
    internal enum PvpMatchLifecycleState
    {
        Disabled,
        Ready,
        PendingChallenge,
        Preparing,
        Countdown,
        Active,
        CleaningUp
    }

    internal sealed class PvpMatchLifecyclePolicy
    {
        internal PvpMatchLifecycleState State { get; private set; }
        internal string MatchId { get; private set; }
        internal int GoTransitions { get; private set; }
        internal bool CombatReleased { get { return State == PvpMatchLifecycleState.Active; } }
        internal bool HoldAttackers { get { return State == PvpMatchLifecycleState.Preparing || State == PvpMatchLifecycleState.Countdown; } }
        internal bool DefenderMayAttackProxy { get { return CombatReleased; } }

        internal PvpMatchLifecyclePolicy(bool enabled)
        {
            State = enabled ? PvpMatchLifecycleState.Ready : PvpMatchLifecycleState.Disabled;
            MatchId = string.Empty;
        }

        internal void SetEnabled(bool enabled)
        {
            State = enabled ? PvpMatchLifecycleState.Ready : PvpMatchLifecycleState.Disabled;
            MatchId = string.Empty;
            GoTransitions = 0;
        }

        internal bool Queue(string matchId)
        {
            if (State != PvpMatchLifecycleState.Ready || string.IsNullOrEmpty(matchId)) return false;
            MatchId = matchId;
            GoTransitions = 0;
            State = PvpMatchLifecycleState.PendingChallenge;
            return true;
        }

        // Kept as BeginSpawn for compatibility with the existing controller call site. Semantically
        // this is the Preparing phase: the proxy roots exist only after this transition succeeds.
        internal bool BeginSpawn(string matchId)
        {
            if ((State != PvpMatchLifecycleState.Ready && State != PvpMatchLifecycleState.PendingChallenge) || string.IsNullOrEmpty(matchId)) return false;
            MatchId = matchId;
            GoTransitions = 0;
            State = PvpMatchLifecycleState.Preparing;
            return true;
        }

        internal bool SpawnSucceeded()
        {
            if (State != PvpMatchLifecycleState.Preparing) return false;
            State = PvpMatchLifecycleState.Countdown;
            return true;
        }

        internal bool Go()
        {
            if (State != PvpMatchLifecycleState.Countdown || GoTransitions != 0) return false;
            GoTransitions = 1;
            State = PvpMatchLifecycleState.Active;
            return true;
        }

        internal void ClearPending()
        {
            if (State == PvpMatchLifecycleState.PendingChallenge)
            {
                State = PvpMatchLifecycleState.Ready;
                MatchId = string.Empty;
                GoTransitions = 0;
            }
        }

        internal void BeginCleanup()
        {
            if (State == PvpMatchLifecycleState.Disabled || State == PvpMatchLifecycleState.Ready) return;
            State = PvpMatchLifecycleState.CleaningUp;
        }

        internal void CompleteCleanup(bool enabled)
        {
            State = enabled ? PvpMatchLifecycleState.Ready : PvpMatchLifecycleState.Disabled;
            MatchId = string.Empty;
            GoTransitions = 0;
        }
    }
}

namespace ErenshorPvP
{
    // Pure health semantics for the bounded native-navigation probe. A Coroutine/IEnumerator
    // reference proves launch only; it is not runtime-health evidence.
    internal static class PvpNativeNavHealthPolicy
    {
        internal static bool IsHealthy(bool nativeStartCompleted, bool navCoroutineObserved,
            bool updateNavReached, bool firstUpdateNavCompleted, bool faulted)
        {
            return nativeStartCompleted && navCoroutineObserved && updateNavReached &&
                   firstUpdateNavCompleted && !faulted;
        }

        internal static bool LaunchAloneIsHealthy(bool navCoroutineObserved)
        {
            return false;
        }

        internal static bool CompleteNavFailure(int proxyCount, int faultedCount)
        {
            return proxyCount > 0 && faultedCount >= proxyCount;
        }

        internal static bool NeedsPursuit(bool hasTarget, float distance, float attackRange)
        {
            return hasTarget && distance > attackRange + 0.25f;
        }

        internal static bool PursuitSatisfied(bool needsPursuit, bool destinationAttempted, bool movementObserved)
        {
            return !needsPursuit || destinationAttempted || movementObserved;
        }
    }
}

namespace ErenshorPvP
{
    // Pure gesture ownership state used by the retained-uGUI drag guard. Keeping this separate
    // makes acquire/release behavior deterministic and testable without Unity.
    internal sealed class PvpPointerOwnershipState
    {
        internal bool OwnsPointer { get; private set; }
        internal bool IsDragging { get; private set; }

        internal bool PointerDown()
        {
            if (OwnsPointer) return false;
            OwnsPointer = true;
            return true;
        }

        internal bool BeginDrag()
        {
            bool acquired = PointerDown();
            IsDragging = true;
            return acquired;
        }

        internal bool Release()
        {
            if (!OwnsPointer && !IsDragging) return false;
            bool owned = OwnsPointer;
            OwnsPointer = false;
            IsDragging = false;
            return owned;
        }
    }
}

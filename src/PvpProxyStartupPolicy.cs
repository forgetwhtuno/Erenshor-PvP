namespace ErenshorPvP
{
    // Pure policy for the synthetic combat root. NPC.Start belongs to the borrowed native creature
    // identity; PvP owns the synthetic proxy's runtime state explicitly and therefore bypasses that
    // lifecycle only after the object is registered as a temporary PvP actor and its required
    // component graph is proven coherent.
    internal static class PvpProxyStartupPolicy
    {
        internal static bool InvariantPasses(bool registered, bool hasNpc, bool hasCharacter, bool hasStats,
            bool characterLinksNpc, bool statsLinksCharacter, bool hasCaster, bool hasNav, bool persistentSim)
        {
            return registered && hasNpc && hasCharacter && hasStats && characterLinksNpc && statsLinksCharacter &&
                   hasCaster && hasNav && !persistentSim;
        }

        internal static bool ShouldRunNativeNpcStart(bool registeredTemporaryProxy, bool clonedFromLiveStartedNpc)
        {
            // A clone of a live scene NPC inherits the source component's already-initialized
            // runtime fields, so replaying the borrowed creature Start lifecycle is both redundant
            // and unsafe after the object has been converted into a synthetic PvP identity.
            // Resource-prefab clones have not proven that property and retain their native Start.
            return !registeredTemporaryProxy || !clonedFromLiveStartedNpc;
        }

        // HandleMaintenaceAndCounters dereferences NPC.MyStats every frame and dereferences
        // NPC.Myself/NameFlash when an aggro target exists. These are NPC-owned runtime fields;
        // Character.MyStats alone is not sufficient proof that a cloned NPC can enter Update.
        internal static bool MaintenanceStatePasses(bool registeredTemporaryProxy, bool npcLinksCharacter,
            bool npcLinksStats, bool npcLinksNav, bool npcLinksCaster, bool hasNameFlash, bool raidSlotClear)
        {
            return registeredTemporaryProxy && npcLinksCharacter && npcLinksStats && npcLinksNav &&
                   npcLinksCaster && hasNameFlash && raidSlotClear;
        }

        internal static bool ShouldInterceptMaintenance(bool registeredTemporaryProxy, bool maintenanceStateValid)
        {
            // Fail open for every vanilla NPC. An invalid registered PvP proxy is skipped only
            // long enough for the PvP factory to terminate and destroy the encounter.
            return registeredTemporaryProxy && !maintenanceStateValid;
        }

        internal static bool RewardBoundaryPasses(bool xpReadableAndZero, bool bossXpZero, bool bonusXpZero,
            bool questNull, bool factionEmpty, bool lootGoldZero, bool lootDisabled)
        {
            return xpReadableAndZero && bossXpZero && bonusXpZero && questNull && factionEmpty && lootGoldZero && lootDisabled;
        }

        internal static string ZeroHealingAssessment(int healCapableAttackers, int healChecks, int spellStarts, long healingDone)
        {
            if (healingDone > 0) return "healing_observed";
            if (healCapableAttackers <= 0) return "expected_no_heal_loadout";
            if (healChecks <= 0) return "heal_ai_not_evaluated";
            if (spellStarts <= 0) return "heal_capable_but_no_cast_started";
            return "heal_capable_casting_observed_no_effective_heal";
        }
    }
}

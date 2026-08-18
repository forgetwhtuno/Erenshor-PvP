namespace ErenshorPvP
{
    // Pure policy for the synthetic combat root. The cloned NPC receives its own native Start
    // lifecycle; PvP then reasserts only synthetic identity/reward constraints after Start completes.
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
            // 0.5.9 forensic recovery: a cloned MonoBehaviour receives its own Unity lifecycle.
            // The recent live-good PvP path relied on that native Start after the proxy was enabled
            // for combat. Bypassing Start forced PvP to reconstruct an incomplete Start-owned nav
            // graph and caused NavUpdate/UpdateNav to fault. Run native Start for the proxy and
            // reassert PvP identity/reward state in the Start postfix instead.
            return true;
        }

        // HandleMaintenaceAndCounters dereferences NPC.MyStats every frame and dereferences
        // NPC.Myself/NameFlash when an aggro target exists. These are NPC-owned runtime fields;
        // Character.MyStats alone is not sufficient proof that a cloned NPC can enter Update.
        //
        // hasNamePlateText / hasNamePlateObject were added after the 5v5 that produced ~1,130
        // NPC.HandleNameTag NREs while this invariant still reported PASS. hasNameFlash is NOT
        // sufficient: NameFlash (FlashUIColors) is a DIFFERENT field from the one HandleNameTag
        // dereferences. Verified against the installed Assembly-CSharp.dll, HandleNameTag reads
        // NPC.NamePlateTxt (TMPro.TextMeshPro) through callvirt Behaviour.get_enabled() in every
        // branch, and also reads NamePlateObject. The earlier Start-bypass line therefore had to
        // reconstruct both fields. 0.5.9 restores native Start but keeps this pre/post-Start invariant
        // so a proxy can never reach NPC.Update with a missing or source-shared nameplate. A proxy
        // missing either must fail closed instead of reaching combat.
        internal static bool MaintenanceStatePasses(bool registeredTemporaryProxy, bool npcLinksCharacter,
            bool npcLinksStats, bool npcLinksNav, bool npcLinksCaster, bool hasNameFlash, bool raidSlotClear,
            bool hasNamePlateText, bool hasNamePlateObject)
        {
            return registeredTemporaryProxy && npcLinksCharacter && npcLinksStats && npcLinksNav &&
                   npcLinksCaster && hasNameFlash && raidSlotClear && hasNamePlateText && hasNamePlateObject;
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

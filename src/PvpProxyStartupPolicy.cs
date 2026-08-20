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

        // 0.5.11 observability: a single proxy-level answer to "did this proxy actually evaluate
        // and use its abilities", built from telemetry that already exists per proxy (admitted
        // spell counts, CheckHeals/DoAttackSkill/DoAttackSpell decisions, StartSpell-family starts,
        // and effective damage/healing outcomes). A proxy with zero admitted class spells is a real,
        // expected outcome (e.g. a pure-melee loadout) and must report as such rather than as a
        // failure. Each return value is a strictly later stage than the one before it, so "loaded
        // but never evaluated" is always distinguishable from "evaluated but never cast", which is
        // itself distinguishable from "cast but no measurable outcome landed".
        internal static string ProxyAbilityUseAssessment(int offensiveSpellsLoaded, int healSpellsLoaded,
            int healChecks, int attackSkillDecisions, int attackSpellDecisions, int spellStarts,
            long damageDealt, long healingDone)
        {
            if (offensiveSpellsLoaded <= 0 && healSpellsLoaded <= 0) return "no_class_abilities_loaded";
            if (attackSkillDecisions <= 0 && attackSpellDecisions <= 0 && healChecks <= 0) return "ability_ai_not_evaluated";
            if (spellStarts <= 0) return "ability_evaluated_no_cast_started";
            if (damageDealt <= 0 && healingDone <= 0) return "cast_started_no_effective_outcome";
            return "ability_use_confirmed";
        }
    }
}

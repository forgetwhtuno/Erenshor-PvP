namespace ErenshorPvP
{
    // Active PvP is world combat, not an arena. The policy only overrides native interaction when
    // team identity is required for the arranged match or when current native state positively proves
    // that a world actor is neutral/noncombat and should be protected from accidental participation.
    internal enum PvpInteractionDecision
    {
        AllowUnrelated,
        AllowMatch,
        AllowWorld,
        Block
    }

    internal static class PvpWorldCombatPolicy
    {
        internal static bool IsProtectedNonCombat(bool simPlayer, bool ownedOrSummoned, bool vendor,
            bool invulnerable, bool neverAggro, bool resourceObject, bool knownFriendlyFaction)
        {
            if (simPlayer || ownedOrSummoned) return false;
            return vendor || invulnerable || neverAggro || resourceObject || knownFriendlyFaction;
        }

        internal static PvpInteractionDecision DecideAggro(bool sourceAttacker, bool sourceDefender,
            bool targetAttacker, bool targetDefender, bool sourceProtected, bool targetProtected)
        {
            if ((sourceAttacker && targetDefender) || (sourceDefender && targetAttacker))
                return PvpInteractionDecision.AllowMatch;
            if ((sourceAttacker && targetAttacker) || (sourceDefender && targetDefender))
                return PvpInteractionDecision.Block;

            bool sourceParticipant = sourceAttacker || sourceDefender;
            bool targetParticipant = targetAttacker || targetDefender;
            if ((sourceParticipant && targetProtected) || (sourceProtected && targetParticipant))
                return PvpInteractionDecision.Block;
            if (sourceParticipant || targetParticipant) return PvpInteractionDecision.AllowWorld;
            return PvpInteractionDecision.AllowUnrelated;
        }

        internal static PvpInteractionDecision DecideDamage(bool targetDefender, bool targetAttacker,
            bool sourceAttacker, bool sourceDefender, bool unknownPlayerProjectile,
            bool sourceProtected, bool targetProtected)
        {
            bool playerSide = sourceDefender || unknownPlayerProjectile;
            if ((targetDefender && sourceAttacker) || (targetAttacker && playerSide))
                return PvpInteractionDecision.AllowMatch;
            if ((targetDefender && sourceDefender) || (targetAttacker && sourceAttacker))
                return PvpInteractionDecision.Block;

            bool sourceParticipant = sourceAttacker || playerSide;
            bool targetParticipant = targetDefender || targetAttacker;
            if ((sourceParticipant && targetProtected) || (sourceProtected && targetParticipant))
                return PvpInteractionDecision.Block;
            if (sourceParticipant || targetParticipant) return PvpInteractionDecision.AllowWorld;
            return PvpInteractionDecision.AllowUnrelated;
        }

        internal static PvpInteractionDecision DecideHeal(bool targetDefender, bool targetAttacker,
            bool sourceDefender, bool sourceAttacker, bool sourceProtected, bool targetProtected)
        {
            if ((targetDefender && sourceDefender) || (targetAttacker && sourceAttacker))
                return PvpInteractionDecision.AllowMatch;
            if ((targetDefender && sourceAttacker) || (targetAttacker && sourceDefender))
                return PvpInteractionDecision.Block;

            bool sourceParticipant = sourceDefender || sourceAttacker;
            bool targetParticipant = targetDefender || targetAttacker;
            if ((sourceParticipant && targetProtected) || (sourceProtected && targetParticipant))
                return PvpInteractionDecision.Block;
            if (sourceParticipant || targetParticipant) return PvpInteractionDecision.AllowWorld;
            return PvpInteractionDecision.AllowUnrelated;
        }

        internal static PvpInteractionDecision DecideSpellStart(bool sourceDefender, bool sourceAttacker,
            bool targetDefender, bool targetAttacker, bool noTarget, bool beneficial,
            bool sourceProtected, bool targetProtected)
        {
            bool sourceParticipant = sourceDefender || sourceAttacker;
            if (sourceParticipant && noTarget)
            {
                // Do not proximity-block AE/PBAE starts. Native targeting runs normally and the
                // actual per-target damage/aggro hooks protect only proven neutral/noncombat actors.
                return PvpInteractionDecision.AllowMatch;
            }

            if (sourceAttacker && targetDefender)
                return beneficial ? PvpInteractionDecision.Block : PvpInteractionDecision.AllowMatch;
            if (sourceDefender && targetAttacker)
                return beneficial ? PvpInteractionDecision.Block : PvpInteractionDecision.AllowMatch;
            if ((sourceAttacker && targetAttacker) || (sourceDefender && targetDefender))
                return beneficial ? PvpInteractionDecision.AllowMatch : PvpInteractionDecision.Block;

            bool targetParticipant = targetDefender || targetAttacker;
            if ((sourceParticipant && targetProtected) || (sourceProtected && targetParticipant))
                return PvpInteractionDecision.Block;
            if (sourceParticipant || targetParticipant) return PvpInteractionDecision.AllowWorld;
            return PvpInteractionDecision.AllowUnrelated;
        }

        internal static string RunSelfTests()
        {
            if (IsProtectedNonCombat(true, false, true, true, true, true, true)) return "FAIL sim misclassified protected";
            if (IsProtectedNonCombat(false, true, true, true, true, true, true)) return "FAIL pet misclassified protected";
            if (!IsProtectedNonCombat(false, false, true, false, false, false, false)) return "FAIL vendor protection";
            if (!IsProtectedNonCombat(false, false, false, false, true, false, false)) return "FAIL never-aggro protection";
            if (!IsProtectedNonCombat(false, false, false, false, false, false, true)) return "FAIL friendly faction protection";
            if (IsProtectedNonCombat(false, false, false, false, false, false, false)) return "FAIL unknown world actor overprotected";

            if (DecideAggro(true, false, false, true, false, false) != PvpInteractionDecision.AllowMatch) return "FAIL pvp hostile aggro";
            if (DecideAggro(true, false, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL proxy world aggro";
            if (DecideAggro(false, false, true, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL world actor proxy aggro";
            if (DecideAggro(true, false, false, false, false, true) != PvpInteractionDecision.Block) return "FAIL protected target aggro";

            if (DecideDamage(true, false, false, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL outside damage to defender";
            if (DecideDamage(false, true, false, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL outside damage to attacker";
            if (DecideDamage(false, false, true, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL proxy damage to world combatant";
            if (DecideDamage(false, false, true, false, false, false, true) != PvpInteractionDecision.Block) return "FAIL protected target damage";

            if (DecideHeal(true, false, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL outside heal to defender";
            if (DecideHeal(false, true, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL outside heal to attacker";
            if (DecideHeal(false, true, true, false, false, false) != PvpInteractionDecision.Block) return "FAIL cross-team heal";

            if (DecideSpellStart(false, true, false, false, true, false, false, false) != PvpInteractionDecision.AllowMatch) return "FAIL attacker AE start";
            if (DecideSpellStart(true, false, false, false, true, false, false, false) != PvpInteractionDecision.AllowMatch) return "FAIL defender AE start";
            if (DecideSpellStart(false, true, false, false, false, false, false, false) != PvpInteractionDecision.AllowWorld) return "FAIL attacker world spell";
            if (DecideSpellStart(false, true, false, false, false, false, false, true) != PvpInteractionDecision.Block) return "FAIL protected targeted spell";
            return "PASS pvp world combat policy";
        }
    }
}

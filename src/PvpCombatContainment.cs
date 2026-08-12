using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ErenshorPvP
{
    internal enum PvpInteractionDecision { AllowUnrelated, AllowMatch, AllowWorldAndCancel, Block, BlockAndCancel }

    // Owns the first native targeting test. Combat components remain disabled; these guards make
    // damage and outside aggro fail closed while target-state behavior is verified.
    internal static class PvpCombatContainment
    {
        private static NPC _cloneNpc;
        private static Character _cloneActor;
        private static Character _player;
        private static NavMeshAgent _cloneNav;
        private static float _targetTestEnds;
        private static bool _lethalFight;
        private static bool _retreatRolled;
        private static float _fightStartedAt;
        private static long _damageToAttackers;
        private static long _petDamageToAttackers;
        private static long _damageToDefenders;
        private static long _healingToAttackers;
        private static long _healingToDefenders;
        private static bool _damageContextPet;
        private static int _healTelemetryDepth;
        private static bool _thirdPartyInterference;
        private static readonly List<NPC> EnemyNpcs = new List<NPC>();
        private static readonly List<Character> EnemyActors = new List<Character>();
        private static readonly List<Character> Defenders = new List<Character>();
        private static readonly HashSet<Character> DefenderPets = new HashSet<Character>();

        internal struct DamageTelemetryState
        {
            internal bool ContextSet;
            internal bool PreviousPetContext;
        }

        internal struct HpTelemetryState
        {
            internal bool Track;
            internal bool TargetIsEnemy;
            internal int StartingHp;
            internal bool OwnsHealScope;
        }

        internal static bool TargetTestActive { get { return _cloneNpc != null && Time.unscaledTime < _targetTestEnds; } }
        internal static bool LethalFightActive { get { return _lethalFight && EnemyActors.Count > 0 && _player != null; } }

        internal static bool WorldCombatBusy()
        {
            try
            {
                if (GameData.AttackingPlayer == null) return false;
                return GameData.AttackingPlayer.Any(x => x != null && !EnemyNpcs.Contains(x));
            }
            catch { return true; }
        }

        internal static string VerifyRuntime()
        {
            if (!LethalFightActive) return "COMBAT VERIFY FAIL lethal=false.";
            List<string> failures = new List<string>();
            if (EnemyNpcs.Count == 0 || EnemyNpcs.Count != EnemyActors.Count) failures.Add("enemy_count");
            if (Defenders.Count == 0 || _player == null || !Defenders.Contains(_player)) failures.Add("defenders");
            for (int i = 0; i < EnemyNpcs.Count; i++)
            {
                NPC npc = EnemyNpcs[i]; Character actor = i < EnemyActors.Count ? EnemyActors[i] : null;
                if (npc == null || actor == null || actor.MyStats == null) { failures.Add("enemy" + (i + 1) + "_actor"); continue; }
                if (npc.CurrentAggroTarget == null || !Defenders.Contains(npc.CurrentAggroTarget)) failures.Add("enemy" + (i + 1) + "_target");
            }
            return failures.Count == 0 ? "COMBAT VERIFY PASS attackers=" + EnemyActors.Count + "; defenders=" + Defenders.Count + "; defender_pets=" + DefenderPets.Count + "; damage_to_attackers=" + _damageToAttackers + "; pet_damage=" + _petDamageToAttackers + "; healing_to_attackers=" + _healingToAttackers + "; damage_to_defenders=" + _damageToDefenders + "; healing_to_defenders=" + _healingToDefenders + "; targets=contained." :
                "COMBAT VERIFY FAIL " + string.Join(",", failures.ToArray()) + ".";
        }

        internal static string BeginTargetingTest(NPC cloneNpc, Character cloneActor)
        {
            Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
            if (cloneNpc == null || cloneActor == null || player == null) return "[Erenshor PvP] Target test blocked: clone or player is unavailable.";
            _cloneNpc = cloneNpc; _cloneActor = cloneActor; _player = player;
            _cloneNav = cloneActor.GetComponent<NavMeshAgent>();
            if (_cloneNav != null)
            {
                _cloneNav.enabled = true;
                if (_cloneNav.isOnNavMesh) _cloneNav.isStopped = false;
                else _cloneNav.enabled = false;
            }
            _targetTestEnds = Time.unscaledTime + 10f;
            cloneNpc.ForceAggroOn(player);
            if (cloneNpc.CurrentAggroTarget != player) { End("target_rejected"); return "[Erenshor PvP] Target test blocked: native target was not accepted."; }
            return "[Erenshor PvP] Clone target/chase test active for 10 seconds. It will face and path toward you; AI and all damage remain disabled.";
        }

        internal static void Tick()
        {
            if (_cloneNpc == null) return;
            if (LethalFightActive)
            {
                try
                {
                    if (_thirdPartyInterference || HasThirdPartyAggro()) { End("third_party_aggro"); PvpTemporaryCloneFactory.DespawnAfterFight("third_party_aggro", null); return; }
                    if (!_player.Alive) { End("player_death"); PvpTemporaryCloneFactory.DespawnAfterFight("player_death", null); return; }
                    List<Character> living = EnemyActors.Where(x => x != null && x.Alive && x.MyStats != null).ToList();
                    if (!_retreatRolled && living.Count == 1 && living[0].MyStats.CurrentMaxHP > 0 &&
                        living[0].MyStats.CurrentHP <= living[0].MyStats.CurrentMaxHP * .12f)
                    {
                        _retreatRolled = true;
                        if (UnityEngine.Random.Range(0, 100) < 20)
                        {
                            End("retreat"); PvpTemporaryCloneFactory.DespawnAfterFight("retreat", null); return;
                        }
                    }
                    if (EnemyActors.All(x => x == null || !x.Alive))
                    {
                        Character winner = _player;
                        End("proxy_death");
                        PvpTemporaryCloneFactory.DespawnAfterFight("proxy_death", winner);
                        return;
                    }
                }
                catch { End("fight_state_failed"); PvpTemporaryCloneFactory.DespawnAfterFight("fight_state_failed", null); }
                return;
            }
            if (Time.unscaledTime >= _targetTestEnds) { End("timer"); return; }
            try
            {
                if (_cloneActor != null && _player != null)
                {
                    Vector3 destination = _player.transform.position;
                    Vector3 flat = destination - _cloneActor.transform.position; flat.y = 0f;
                    if (flat.sqrMagnitude > 0.04f) _cloneActor.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    if (_cloneNav != null && _cloneNav.enabled && _cloneNav.isOnNavMesh) _cloneNav.SetDestination(destination);
                }
            }
            catch { End("target_update_failed"); }
        }

        internal static void End(string reason)
        {
            bool wasLethal = _lethalFight;
            if (wasLethal) LogBalanceSummary(reason);
            NPC npc = _cloneNpc;
            NavMeshAgent nav = _cloneNav;
            _cloneNpc = null; _cloneActor = null; _player = null; _cloneNav = null; _targetTestEnds = 0f; _lethalFight = false; _retreatRolled = false;
            foreach (NPC enemy in EnemyNpcs) { try { if (enemy != null) enemy.ForceAggroOn(null); } catch { } }
            foreach (Character pet in DefenderPets) { try { if (pet != null && pet.MyNPC != null) pet.MyNPC.ForceAggroOn(null); } catch { } }
            EnemyNpcs.Clear(); EnemyActors.Clear(); Defenders.Clear(); DefenderPets.Clear();
            _fightStartedAt = 0f; _damageToAttackers = 0; _petDamageToAttackers = 0; _damageToDefenders = 0;
            _healingToAttackers = 0; _healingToDefenders = 0; _damageContextPet = false; _healTelemetryDepth = 0; _thirdPartyInterference = false;
            try { if (npc != null) npc.ForceAggroOn(null); } catch { }
            try { if (nav != null) { nav.ResetPath(); nav.enabled = false; } } catch { }
        }

        internal static bool AllowAggro(NPC npc, Character target)
        {
            if (target == null) return true; // cleanup must always be able to clear target state.
            if (TargetTestActive && npc == _cloneNpc) return target == _player;
            if (!LethalFightActive) return !PvpTemporaryCloneFactory.IsTemporaryNpc(npc);

            Character actor = null;
            try
            {
                actor = npc == null ? null : npc.GetComponent<Character>();
                if (actor == null && npc != null) actor = npc.GetComponentInParent<Character>();
            }
            catch { }
            bool attackerEnemy = EnemyNpcs.Contains(npc) || EnemyActors.Contains(actor);
            bool attackerDefender = Defenders.Contains(actor) || DefenderPets.Contains(actor) || RegisterDefenderPet(actor);
            bool targetEnemy = EnemyActors.Contains(target);
            bool targetDefender = Defenders.Contains(target) || DefenderPets.Contains(target) || RegisterDefenderPet(target);
            PvpInteractionDecision decision = DecideAggro(attackerEnemy, attackerDefender, targetEnemy, targetDefender);
            if (decision == PvpInteractionDecision.AllowMatch || decision == PvpInteractionDecision.AllowUnrelated) return true;
            if (decision == PvpInteractionDecision.AllowWorldAndCancel)
            {
                // Ordinary world combat remains authoritative. A defender may attack an NPC,
                // but doing so ends the separate PvP encounter on the next containment tick.
                _thirdPartyInterference = true;
                PvpDiagnostics.Log("world_aggro_allowed attacker=" + Describe(actor, npc) + "; target=" + Describe(target, null));
                return true;
            }
            if (decision == PvpInteractionDecision.BlockAndCancel) _thirdPartyInterference = true;
            if (decision == PvpInteractionDecision.Block || decision == PvpInteractionDecision.BlockAndCancel)
            {
                // Proxies never attack world NPCs, and world NPCs never enter the PvP match.
                PvpDiagnostics.Log("blocked_aggro attacker=" + Describe(actor, npc) + "; target=" + Describe(target, null));
                return false;
            }
            return true;
        }

        internal static bool PrepareDamage(Character target, Character attacker, bool fromPlayer, ref int result, ref DamageTelemetryState state)
        {
            state = new DamageTelemetryState();
            if (LethalFightActive)
            {
                bool playerTarget = Defenders.Contains(target) || DefenderPets.Contains(target) || RegisterDefenderPet(target);
                bool proxyTarget = EnemyActors.Contains(target);
                bool enemyAttacker = EnemyActors.Contains(attacker);
                bool defenderAttacker = Defenders.Contains(attacker) || DefenderPets.Contains(attacker) || RegisterDefenderPet(attacker);
                // Native player spell/projectile damage can arrive without an attacker reference;
                // admit that narrow case only against the current temporary proxy.
                PvpInteractionDecision decision = DecideDamage(playerTarget, proxyTarget, enemyAttacker, defenderAttacker,
                    attacker == null && fromPlayer, attacker != null && !enemyAttacker && !defenderAttacker);
                if (decision == PvpInteractionDecision.AllowMatch)
                {
                    state.ContextSet = true;
                    state.PreviousPetContext = _damageContextPet;
                    _damageContextPet = proxyTarget && DefenderPets.Contains(attacker);
                    return true;
                }
                if (decision == PvpInteractionDecision.AllowWorldAndCancel)
                {
                    _thirdPartyInterference = true;
                    PvpDiagnostics.Log("world_damage_allowed target=outside; attacker=" +
                        (attacker == null ? "player_projectile" : attacker.name));
                    return true;
                }
                if (decision == PvpInteractionDecision.BlockAndCancel) _thirdPartyInterference = true;
                if (decision == PvpInteractionDecision.Block || decision == PvpInteractionDecision.BlockAndCancel)
                {
                    PvpDiagnostics.Log("blocked_damage target=" + (playerTarget ? "defender" : proxyTarget ? "attacker" : "outside") +
                        "; attacker=" + (attacker == null ? (fromPlayer ? "player_projectile" : "none") : attacker.name));
                    result = 0; return false;
                }
            }
            if (!PvpTemporaryCloneFactory.IsTemporaryActor(target) && !PvpTemporaryCloneFactory.IsTemporaryActor(attacker)) return true;
            result = 0;
            return false;
        }

        internal static void FinishDamage(DamageTelemetryState state)
        {
            if (state.ContextSet) _damageContextPet = state.PreviousPetContext;
        }

        internal static void BeginHpTelemetry(Stats stats, ref HpTelemetryState state)
        {
            state = new HpTelemetryState();
            CaptureHpTelemetry(stats, ref state);
        }

        internal static void BeginHealTelemetry(Stats stats, ref HpTelemetryState state)
        {
            state = new HpTelemetryState { OwnsHealScope = true };
            _healTelemetryDepth++;
            if (_healTelemetryDepth == 1) CaptureHpTelemetry(stats, ref state);
        }

        private static void CaptureHpTelemetry(Stats stats, ref HpTelemetryState state)
        {
            if (!LethalFightActive || stats == null) return;
            Character target = null;
            try { target = stats.Myself; } catch { }
            bool enemy = EnemyActors.Contains(target);
            bool defender = Defenders.Contains(target) || DefenderPets.Contains(target) || RegisterDefenderPet(target);
            if (!enemy && !defender) return;
            state.Track = true; state.TargetIsEnemy = enemy; state.StartingHp = stats.CurrentHP;
        }

        internal static void FinishHpReduction(Stats stats, HpTelemetryState state)
        {
            if (!state.Track || stats == null) return;
            int applied = Mathf.Max(0, state.StartingHp - Mathf.Max(0, stats.CurrentHP));
            if (applied <= 0) return;
            if (state.TargetIsEnemy)
            {
                _damageToAttackers += applied;
                if (_damageContextPet) _petDamageToAttackers += applied;
            }
            else _damageToDefenders += applied;
        }

        internal static void FinishHpHealing(Stats stats, HpTelemetryState state)
        {
            try
            {
                if (!state.Track || stats == null) return;
                int applied = Mathf.Max(0, stats.CurrentHP - state.StartingHp);
                if (applied <= 0) return;
                if (state.TargetIsEnemy) _healingToAttackers += applied;
                else _healingToDefenders += applied;
            }
            finally
            {
                if (state.OwnsHealScope) _healTelemetryDepth = Math.Max(0, _healTelemetryDepth - 1);
            }
        }

        private static bool AllowContainedDamage(bool targetIsDefender, bool targetIsEnemy, bool attackerIsEnemy,
            bool attackerIsDefender, bool unknownPlayerProjectile)
        {
            return (targetIsDefender && attackerIsEnemy) ||
                   (targetIsEnemy && (attackerIsDefender || unknownPlayerProjectile));
        }

        private static PvpInteractionDecision DecideDamage(bool targetDefender, bool targetEnemy,
            bool attackerEnemy, bool attackerDefender, bool unknownPlayerProjectile, bool knownOutsideAttacker)
        {
            if (AllowContainedDamage(targetDefender, targetEnemy, attackerEnemy, attackerDefender, unknownPlayerProjectile))
                return PvpInteractionDecision.AllowMatch;
            bool targetParticipant = targetDefender || targetEnemy;
            bool playerSide = attackerDefender || unknownPlayerProjectile;
            if (!targetParticipant && playerSide) return PvpInteractionDecision.AllowWorldAndCancel;
            bool attackerParticipant = attackerEnemy || playerSide;
            if (!targetParticipant && !attackerParticipant) return PvpInteractionDecision.AllowUnrelated;
            bool proxyToOutside = attackerEnemy && !targetParticipant;
            return knownOutsideAttacker || proxyToOutside ? PvpInteractionDecision.BlockAndCancel : PvpInteractionDecision.Block;
        }

        private static PvpInteractionDecision DecideAggro(bool attackerEnemy, bool attackerDefender,
            bool targetEnemy, bool targetDefender)
        {
            if ((attackerEnemy && targetDefender) || (attackerDefender && targetEnemy)) return PvpInteractionDecision.AllowMatch;
            bool targetParticipant = targetEnemy || targetDefender;
            if (attackerDefender && !targetParticipant) return PvpInteractionDecision.AllowWorldAndCancel;
            bool attackerParticipant = attackerEnemy || attackerDefender;
            if (!targetParticipant && !attackerParticipant) return PvpInteractionDecision.AllowUnrelated;
            return PvpInteractionDecision.BlockAndCancel;
        }

        internal static string RunSelfTests()
        {
            if (!AllowContainedDamage(false, true, false, true, false)) return "FAIL defender pet offense";
            if (!AllowContainedDamage(true, false, true, false, false)) return "FAIL defender pet target";
            if (!AllowContainedDamage(false, true, false, false, true)) return "FAIL player projectile";
            if (AllowContainedDamage(false, true, false, false, false)) return "FAIL outside attacker admitted";
            if (AllowContainedDamage(true, false, false, true, false)) return "FAIL friendly damage admitted";
            if (AllowContainedDamage(false, false, false, true, false)) return "FAIL defender damage escaped match";
            if (AllowContainedDamage(false, false, false, false, true)) return "FAIL player projectile escaped match";
            if (DecideDamage(false, false, false, true, false, false) != PvpInteractionDecision.AllowWorldAndCancel) return "FAIL player world damage blocked";
            if (DecideDamage(false, false, false, false, true, false) != PvpInteractionDecision.AllowWorldAndCancel) return "FAIL player AE world damage blocked";
            if (DecideDamage(false, false, true, false, false, false) != PvpInteractionDecision.BlockAndCancel) return "FAIL proxy escaped match boundary";
            if (DecideDamage(true, false, false, false, false, true) != PvpInteractionDecision.BlockAndCancel) return "FAIL outside damage entered match";
            if (DecideDamage(true, false, false, false, false, false) != PvpInteractionDecision.Block) return "FAIL unattributed tick cancelled match";
            if (DecideAggro(false, true, false, false) != PvpInteractionDecision.AllowWorldAndCancel) return "FAIL party world aggro blocked";
            if (DecideAggro(false, false, false, true) != PvpInteractionDecision.BlockAndCancel) return "FAIL outside aggro entered match";
            return "PASS pvp combat containment";
        }

        internal static string BeginLethalFight(NPC cloneNpc, Character cloneActor, CastSpell cloneSpells)
        {
            return BeginLethalFight(new List<NPC> { cloneNpc }, new List<Character> { cloneActor }, new List<CastSpell> { cloneSpells });
        }

        internal static string BeginLethalFight(IList<NPC> cloneNpcs, IList<Character> cloneActors, IList<CastSpell> cloneSpells)
        {
            Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
            if (cloneNpcs == null || cloneActors == null || cloneNpcs.Count == 0 || cloneNpcs.Count != cloneActors.Count || player == null || player.MyStats == null)
                return "[Erenshor PvP] Lethal fight blocked: combat actors are not ready.";
            for (int i = 0; i < cloneActors.Count; i++)
                if (cloneNpcs[i] == null || cloneActors[i] == null || cloneActors[i].MyStats == null || cloneActors[i].MyStats.CurrentHP <= 0 || cloneActors[i].MyStats.CurrentMaxHP <= 0)
                    return "[Erenshor PvP] Lethal fight blocked: proxy " + (i + 1) + " has no valid HP.";
            if (player.MyStats.CurrentHP <= 0 || player.MyStats.CurrentMaxHP <= 0)
                return "[Erenshor PvP] Lethal fight blocked: player has no valid HP.";
            if (HasThirdPartyAggro()) return "[Erenshor PvP] Lethal fight blocked: clear nearby hostile combat first.";

            _cloneNpc = cloneNpcs[0]; _cloneActor = cloneActors[0]; _player = player; _targetTestEnds = 0f;
            EnemyNpcs.Clear(); EnemyActors.Clear(); Defenders.Clear();
            EnemyNpcs.AddRange(cloneNpcs.Where(x => x != null)); EnemyActors.AddRange(cloneActors.Where(x => x != null));
            Defenders.Add(player); AddPartyDefenders(); SnapshotDefenderPets();
            _fightStartedAt = Time.unscaledTime; _damageToAttackers = 0; _petDamageToAttackers = 0; _damageToDefenders = 0;
            _healingToAttackers = 0; _healingToDefenders = 0; _damageContextPet = false; _healTelemetryDepth = 0; _thirdPartyInterference = false;
            _lethalFight = true; _retreatRolled = false;
            try
            {
                for (int i = 0; i < EnemyNpcs.Count; i++)
                {
                    if (cloneSpells != null && i < cloneSpells.Count && cloneSpells[i] != null)
                        cloneSpells[i].enabled = cloneSpells[i].KnownSpells != null && cloneSpells[i].KnownSpells.Count > 0;
                    EnemyActors[i].enabled = true; EnemyNpcs[i].enabled = true;
                    Character target = Defenders[i % Defenders.Count];
                    EnemyNpcs[i].ForceAggroOn(target);
                    if (EnemyNpcs[i].CurrentAggroTarget != target) { End("target_rejected"); return "[Erenshor PvP] Lethal fight blocked: proxy " + (i + 1) + " rejected its target."; }
                }
                PvpDiagnostics.Log("lethal_started attackers=" + EnemyActors.Count + "; defenders=" + Defenders.Count + "; defender_pets=" + DefenderPets.Count + "; player_hp=" + player.MyStats.CurrentHP + "/" + player.MyStats.CurrentMaxHP);
                return "[Erenshor PvP] Lethal team PvP started: " + EnemyActors.Count + " attacker(s) vs " + Defenders.Count + " defender(s).";
            }
            catch
            {
                End("start_failed");
                return "[Erenshor PvP] Lethal fight failed safely before combat began.";
            }
        }

        private static void AddPartyDefenders()
        {
            try
            {
                if (GameData.GroupMembers == null) return;
                foreach (SimPlayerTracking tracking in GameData.GroupMembers)
                {
                    Character actor = tracking == null || tracking.MyAvatar == null || tracking.MyAvatar.MyStats == null ? null : tracking.MyAvatar.MyStats.Myself;
                    if (actor != null && actor.Alive && !Defenders.Contains(actor)) Defenders.Add(actor);
                }
            }
            catch { }
        }

        private static void SnapshotDefenderPets()
        {
            DefenderPets.Clear();
            try
            {
                foreach (NPC npc in UnityEngine.Object.FindObjectsOfType<NPC>())
                {
                    Character actor = npc == null ? null : npc.GetComponent<Character>();
                    if (actor == null && npc != null) actor = npc.GetComponentInParent<Character>();
                    RegisterDefenderPet(actor);
                }
            }
            catch { }
        }

        // Character.Master is the verified native ownership link used for summoned pets. Walk a
        // short bounded chain so a pet or nested summon is admitted only when it resolves to the
        // local player or a living party defender already captured for this encounter.
        private static bool RegisterDefenderPet(Character actor)
        {
            if (actor == null || Defenders.Contains(actor) || EnemyActors.Contains(actor)) return false;
            Character owner = null;
            try { owner = actor.Master; } catch { return false; }
            for (int depth = 0; owner != null && depth < 4; depth++)
            {
                if (Defenders.Contains(owner))
                {
                    DefenderPets.Add(actor);
                    return true;
                }
                try { owner = owner.Master; } catch { return false; }
            }
            return false;
        }

        private static void LogBalanceSummary(string reason)
        {
            try
            {
                int defeated = EnemyActors.Count(x => x == null || !x.Alive || x.MyStats == null || x.MyStats.CurrentHP <= 0);
                List<int> levels = EnemyActors.Where(x => x != null && x.MyStats != null && x.MyStats.Level > 0).Select(x => x.MyStats.Level).ToList();
                int averageLevel = levels.Count == 0 ? 0 : Mathf.RoundToInt((float)levels.Average());
                float seconds = _fightStartedAt <= 0f ? 0f : Mathf.Max(0f, Time.unscaledTime - _fightStartedAt);
                PvpDiagnostics.Log("balance_summary reason=" + (reason ?? "unknown") +
                    "; seconds=" + seconds.ToString("0.0") + "; attackers=" + EnemyActors.Count +
                    "; attackers_defeated=" + defeated + "; enemy_avg_level=" + averageLevel +
                    "; defenders=" + Defenders.Count + "; defender_pets=" + DefenderPets.Count +
                    "; damage_to_attackers=" + _damageToAttackers + "; pet_damage=" + _petDamageToAttackers +
                    "; healing_to_attackers=" + _healingToAttackers +
                    "; damage_to_defenders=" + _damageToDefenders + "; healing_to_defenders=" + _healingToDefenders);
            }
            catch { }
        }

        private static string Describe(Character actor, NPC npc)
        {
            try
            {
                if (actor != null && actor.MyStats != null && !string.IsNullOrWhiteSpace(actor.MyStats.MyName)) return actor.MyStats.MyName;
                if (npc != null && !string.IsNullOrWhiteSpace(npc.NPCName)) return npc.NPCName;
                if (actor != null) return actor.name;
                if (npc != null) return npc.name;
            }
            catch { }
            return "unknown";
        }

        private static bool HasThirdPartyAggro()
        {
            try
            {
                if (GameData.AttackingPlayer == null) return false;
                foreach (NPC npc in GameData.AttackingPlayer)
                {
                    if (npc != null && !EnemyNpcs.Contains(npc) && Defenders.Contains(npc.CurrentAggroTarget)) return true;
                }
            }
            catch { return true; }
            return false;
        }
    }

    [HarmonyPatch(typeof(NPC), "AggroOn")]
    internal static class PvpCloneAggroPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NPC __instance, Character __0) { return PvpCombatContainment.AllowAggro(__instance, __0); }
    }

    [HarmonyPatch(typeof(NPC), "ForceAggroOn")]
    internal static class PvpCloneForceAggroPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NPC __instance, Character __0) { return PvpCombatContainment.AllowAggro(__instance, __0); }
    }

    [HarmonyPatch(typeof(NPC), "ManageAggro")]
    internal static class PvpCloneManageAggroPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NPC __instance, Character __1) { return PvpCombatContainment.AllowAggro(__instance, __1); }
    }

    [HarmonyPatch(typeof(Character), "DamageMe")]
    internal static class PvpCloneDamagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Character __instance, bool __1, Character __3, ref int __result, ref PvpCombatContainment.DamageTelemetryState __state)
        { return PvpCombatContainment.PrepareDamage(__instance, __3, __1, ref __result, ref __state); }
        [HarmonyPostfix]
        private static void Postfix(Character __instance, PvpCombatContainment.DamageTelemetryState __state)
        { PvpCombatContainment.FinishDamage(__state); }
    }

    [HarmonyPatch(typeof(Character), "MagicDamageMe")]
    internal static class PvpCloneMagicDamagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Character __instance, bool __1, Character __3, ref int __result, ref PvpCombatContainment.DamageTelemetryState __state)
        { return PvpCombatContainment.PrepareDamage(__instance, __3, __1, ref __result, ref __state); }
        [HarmonyPostfix]
        private static void Postfix(Character __instance, PvpCombatContainment.DamageTelemetryState __state)
        { PvpCombatContainment.FinishDamage(__state); }
    }

    [HarmonyPatch(typeof(Character), "BleedDamageMe")]
    internal static class PvpCloneBleedDamagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Character __instance, bool __1, Character __2, ref int __result, ref PvpCombatContainment.DamageTelemetryState __state)
        { return PvpCombatContainment.PrepareDamage(__instance, __2, __1, ref __result, ref __state); }
        [HarmonyPostfix]
        private static void Postfix(Character __instance, PvpCombatContainment.DamageTelemetryState __state)
        { PvpCombatContainment.FinishDamage(__state); }
    }

    [HarmonyPatch(typeof(Stats), "ReduceHP")]
    internal static class PvpReduceHpTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Stats __instance, ref PvpCombatContainment.HpTelemetryState __state)
        { PvpCombatContainment.BeginHpTelemetry(__instance, ref __state); }
        [HarmonyPostfix]
        private static void Postfix(Stats __instance, PvpCombatContainment.HpTelemetryState __state)
        { PvpCombatContainment.FinishHpReduction(__instance, __state); }
    }

    [HarmonyPatch(typeof(Stats), "HealMe", new Type[] { typeof(Spell), typeof(int), typeof(bool), typeof(bool), typeof(Character) })]
    internal static class PvpSpellHealTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Stats __instance, ref PvpCombatContainment.HpTelemetryState __state)
        { PvpCombatContainment.BeginHealTelemetry(__instance, ref __state); }
        [HarmonyPostfix]
        private static void Postfix(Stats __instance, PvpCombatContainment.HpTelemetryState __state)
        { PvpCombatContainment.FinishHpHealing(__instance, __state); }
    }

    [HarmonyPatch(typeof(Stats), "HealMe", new Type[] { typeof(int) })]
    internal static class PvpFlatHealTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Stats __instance, ref PvpCombatContainment.HpTelemetryState __state)
        { PvpCombatContainment.BeginHealTelemetry(__instance, ref __state); }
        [HarmonyPostfix]
        private static void Postfix(Stats __instance, PvpCombatContainment.HpTelemetryState __state)
        { PvpCombatContainment.FinishHpHealing(__instance, __state); }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace ErenshorPvP
{
    // Temporary encounter actors never enter SimPlayerMngr's persistent collections. They are
    // inert until PvpCombatContainment explicitly begins an approved lethal encounter.
    internal static class PvpTemporaryCloneFactory
    {
        private static readonly HashSet<int> TemporaryActorIds = new HashSet<int>();
        private static bool _suppressPersistentLoad;
        private static GameObject _clone;
        private static PvpOpponentProfile _activeProfile;
        private static readonly List<GameObject> TeamClones = new List<GameObject>();
        private static readonly List<PvpOpponentProfile> TeamProfiles = new List<PvpOpponentProfile>();
        private static readonly List<Spell> TemporarySpells = new List<Spell>();
        private static readonly Dictionary<int, Animator> VisualAnimators = new Dictionary<int, Animator>();
        private static readonly HashSet<int> EquipmentVisualsApplied = new HashSet<int>();
        private static readonly HashSet<int> ClassLoadoutsApplied = new HashSet<int>();
        private static readonly Dictionary<int, int> EligibleCombatSpellCounts = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> EligibleHealSpellCounts = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> AttackDecisionCounts = new Dictionary<int, int>();
        private static readonly HashSet<int> AttackSkillDecisionLogged = new HashSet<int>();
        private static readonly HashSet<int> AttackSpellDecisionLogged = new HashSet<int>();
        private static readonly Dictionary<int, int> HealCheckCounts = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> SpellStartCounts = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> LastSpellStartFrame = new Dictionary<int, int>();
        private static readonly HashSet<int> NativeStartCompleted = new HashSet<int>();
        private sealed class NativeNavProbe
        {
            internal bool CoroutineObserved;
            internal bool UpdateNavReached;
            internal bool FirstUpdateNavCompleted;
            internal bool Faulted;
            internal string FaultType = string.Empty;
            internal bool DestinationAttempted;
            internal bool MovementObserved;
            internal Vector3 StartPosition;
            internal Vector3 LastPosition;
        }
        private static readonly Dictionary<int, NativeNavProbe> NativeNavProbes = new Dictionary<int, NativeNavProbe>();
        private static readonly HashSet<int> NativeUpdateReached = new HashSet<int>();
        private static readonly HashSet<int> CombatSectionReached = new HashSet<int>();
        private static readonly HashSet<int> LegalTargetAcquired = new HashSet<int>();
        private static readonly HashSet<int> NavPursuitRequested = new HashSet<int>();
        private static readonly Dictionary<int, int> MeleeAttemptCounts = new Dictionary<int, int>();
        private static readonly HashSet<int> NativeUpdateExceptionLogged = new HashSet<int>();
        private static bool _runtimeInvalidCleanupQueued;
        private static string _runtimeInvalidReason = string.Empty;
        private static float _despawnAt;
        private static string _activeMatchId;
        private static PvpEncounterMode _activeMode = PvpEncounterMode.Arranged;
        private static string _activeMotive = string.Empty;
        private static string _templateSource = "none";
        private const float PlayerProtectedClearance = 10f;
        private const float SpawnProtectedClearance = 8f;
        private const float SpawnActorCollisionClearance = 1.5f;
        private const float FormationDistance = 11f;

        internal static bool SuppressPersistentLoad { get { return _suppressPersistentLoad; } }
        internal static bool HasActiveTeam { get { return TeamClones.Any(x => x != null); } }

        internal static void Tick()
        {
            if (_runtimeInvalidCleanupQueued)
            {
                _runtimeInvalidCleanupQueued = false;
                Despawn("runtime_invalid");
                return;
            }
            TickNativeNavHealth();
            PvpCombatContainment.Tick();
            if (TeamClones.Count > 0 && Time.unscaledTime >= _despawnAt) Despawn("timer");
        }

        internal static string SpawnVisualClone() { return SpawnVisualClone("PvP Proxy"); }

        internal static string SpawnVisualClone(PvpOpponentProfile profile)
        {
            return SpawnVisualClone(profile == null ? "PvP Proxy" : profile.Name, profile);
        }

        internal static string SpawnVisualClone(string opponentName) { return SpawnVisualClone(opponentName, null); }

        private static string SpawnVisualClone(string opponentName, PvpOpponentProfile profile)
        {
            if (TeamClones.Count > 0) return "[Erenshor PvP] A temporary PvP team is already active. Use /epvp despawn.";
            List<Vector3> positions; string reason;
            if (!TryFindClearFormation(1, out positions, out reason)) return "[Erenshor PvP] Clone spawn blocked: " + reason;
            return SpawnMember(opponentName, profile, 0, positions[0]);
        }

        internal static string SpawnTeam(PvpTeamPlan plan, string matchId)
        { return SpawnTeam(plan, matchId, PvpEncounterMode.Arranged, "party_match"); }

        internal static string SpawnTeam(PvpTeamPlan plan, string matchId, PvpEncounterMode mode, string motive)
        {
            if (plan == null || plan.Members.Count == 0) return "[Erenshor PvP] Team spawn blocked: empty plan.";
            if (TeamClones.Count > 0) return "[Erenshor PvP] A temporary PvP team is already active. Use /epvp despawn.";
            List<Vector3> positions; string clearanceReason;
            if (!TryFindClearFormation(plan.Members.Count, out positions, out clearanceReason))
                return "[Erenshor PvP] Team spawn blocked: " + clearanceReason;
            _activeMatchId = matchId ?? string.Empty;
            _activeMode = mode; _activeMotive = motive ?? string.Empty;
            for (int i = 0; i < plan.Members.Count; i++)
            {
                PvpOpponentProfile profile = plan.Members[i].Profile;
                string result = SpawnMember(profile.Name, profile, i, positions[i]);
                if (result.IndexOf("spawned", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Despawn("team_spawn_failed");
                    return result;
                }
            }
            _despawnAt = Time.unscaledTime + 30f;
            return "[Erenshor PvP] PvP team spawned: " + plan.Members.Count + " off-map profiles, average level " + plan.AverageLevel + ".";
        }

        private static string SpawnMember(string opponentName, PvpOpponentProfile profile, int memberIndex, Vector3 position)
        {
            try
            {
                if (GameData.Zoning || GameData.PlayerControl == null || GameData.PlayerControl.Myself == null)
                    return "[Erenshor PvP] Clone test blocked: game state is not ready.";
                GameObject template = FindNativeMobTemplate();
                if (template == null) return "[Erenshor PvP] PvP needs a nearby native mob template in this zone; no proxy was spawned.";

                Character player = GameData.PlayerControl.Myself;
                GameObject clone;
                Vector3 towardPlayer = player.transform.position - position; towardPlayer.y = 0f;
                Quaternion rotation = towardPlayer.sqrMagnitude > .01f ? Quaternion.LookRotation(towardPlayer.normalized, Vector3.up) : player.transform.rotation;
                clone = UnityEngine.Object.Instantiate(template, position, rotation);
                if (clone == null) return "[Erenshor PvP] Clone test failed safely: instantiate returned null.";

                clone.name = "PvP_TemporaryClone";
                string proxyName = string.IsNullOrWhiteSpace(opponentName) ? "PvP Proxy" : opponentName + " (PvP)";
                clone.name = "PvP_TemporaryClone_" + proxyName;
                SimPlayer sim = clone.GetComponent<SimPlayer>();
                if (sim != null) { sim.myIndex = -1; sim.MySimTracking = null; sim.enabled = false; }
                NPC npc = clone.GetComponent<NPC>();
                Character actor = clone.GetComponent<Character>();
                CastSpell spells = clone.GetComponent<CastSpell>();
                NavMeshAgent nav = clone.GetComponent<NavMeshAgent>();
                SimPlayerLanguage language = clone.GetComponent<SimPlayerLanguage>();
                LootTable loot = clone.GetComponent<LootTable>();
                if (npc != null)
                {
                    npc.NPCName = proxyName;
                    npc.ThisSim = null;
                    npc.SimPlayer = false;
                    npc.InGroup = false;
                    npc.NeverAggro = true;
                    ApplyProfileLoadout(npc, profile);
                    npc.enabled = false;
                }
                if (actor != null)
                {
                    TrySetField(actor, "xp", 0);
                    TrySetField(actor, "BossXp", 0f);
                    TrySetField(actor, "QuestCompleteOnDeath", null);
                    TrySetField(actor, "factionMods", new ModifyFaction[0]);
                    actor.enabled = false;
                    ConfigureCombatStats(player, actor, profile);
                }
                if (loot != null) { loot.MinGold = 0; loot.MaxGold = 0; loot.MyGold = 0; loot.enabled = false; }
                if (language != null) language.enabled = false;
                if (spells != null) spells.enabled = false;
                if (nav != null) nav.enabled = false;

                ConfigureNativeMaintenanceState(npc, actor, spells, nav);
                if (npc != null)
                {
                    // IEnumerator fields copied from a source component are never proof of a
                    // scheduled coroutine on the clone. Native NPC.Start will establish fresh
                    // nav/behavior coroutine ownership when this proxy is enabled at GO.
                    TrySetField(npc, "navDo", null);
                    TrySetField(npc, "behDo", null);
                }

                if (profile != null) AttachSimVisualShell(clone, profile);
                try
                {
                    if (npc != null)
                    {
                        npc.UpdateNamePlate();
                        if (npc.NamePlate != null) foreach (Renderer renderer in npc.NamePlate.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                    }
                }
                catch { }

                TemporaryActorIds.Add(clone.GetInstanceID());
                TeamClones.Add(clone);
                TeamProfiles.Add(profile);
                if (_clone == null) { _clone = clone; _activeProfile = profile; }
                string runtimeState;
                bool runtimeReady = ValidateNativeMaintenanceState(npc, actor, actor == null ? null : actor.MyStats, spells, nav, out runtimeState);
                PvpDiagnostics.Log("proxy_runtime_state proxy=" + proxyName + "; requiredRuntimeState=" +
                    (runtimeReady ? "PASS" : "FAIL") + "; missing=" + runtimeState + "; template=" + _templateSource);
                if (!runtimeReady)
                {
                    Despawn("runtime_state_missing");
                    return "[Erenshor PvP] Clone test blocked: temporary opponent native runtime is incomplete.";
                }
                _despawnAt = Time.unscaledTime + 20f;
                ErenshorPvpEvents.Publish(new PvpSemanticEvent("pvp_proxy_spawned", _activeMatchId, proxyName,
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "visual_test", "inert_temporary_actor"));
                string hp = actor == null || actor.MyStats == null ? "unknown" : actor.MyStats.CurrentHP + "/" + actor.MyStats.CurrentMaxHP;
                PvpDiagnostics.Log("proxy_spawn hp=" + hp + "; level=" + (actor == null || actor.MyStats == null ? "unknown" : actor.MyStats.Level.ToString()) + "; profile=" + (profile == null ? "test" : profile.Describe()) + "; template=" + _templateSource + "; persistent_sim=false");
                return "[Erenshor PvP] Temporary native-mob PvP proxy spawned for 20 seconds. HP=" + hp + "; named after an off-map Sim; no Sim roster, loot, XP, quest, or faction identity.";
            }
            catch (Exception ex)
            {
                _suppressPersistentLoad = false;
                Despawn("failure");
                return "[Erenshor PvP] Clone test failed safely (" + ex.GetType().Name + ").";
            }
        }

        internal static bool CanSpawnClearTeam(int memberCount, out string reason)
        {
            List<Vector3> ignored;
            return TryFindClearFormation(memberCount, out ignored, out reason);
        }

        private static bool TryFindClearFormation(int memberCount, out List<Vector3> positions, out string reason)
        {
            positions = new List<Vector3>(); reason = "no clear navigable formation was found";
            Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
            if (player == null) { reason = "player state is unavailable"; return false; }
            memberCount = Math.Max(1, Math.Min(5, memberCount));
            List<Character> worldActors = OutsideNpcActors(player);
            List<Character> protectedActors = worldActors.Where(PvpCombatContainment.IsProtectedWorldActor).ToList();
            Character nearest = protectedActors.OrderBy(x => (x.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (nearest != null)
            {
                float nearestDistance = Vector3.Distance(nearest.transform.position, player.transform.position);
                if (!MeetsClearance(nearestDistance, PlayerProtectedClearance))
                {
                    reason = "move away from protected world actor " + ActorName(nearest) + " (" + nearestDistance.ToString("0.0") + "m; need " + PlayerProtectedClearance.ToString("0") + "m clearance)";
                    PvpDiagnostics.Log("spawn_clearance blocked=protected_near_player; npc=" + ActorName(nearest) + "; distance=" + nearestDistance.ToString("0.0"));
                    return false;
                }
            }

            Vector3 forward = player.transform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward; else forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3[] directions =
            {
                forward, -forward, right, -right,
                (forward + right).normalized, (forward - right).normalized,
                (-forward + right).normalized, (-forward - right).normalized
            };
            NavMeshHit playerHit;
            if (!NavMesh.SamplePosition(player.transform.position, out playerHit, 3f, NavMesh.AllAreas))
            { reason = "the player is not near a navigable combat surface"; return false; }

            for (int d = 0; d < directions.Length; d++)
            {
                List<Vector3> candidate = new List<Vector3>(); bool valid = true;
                for (int i = 0; i < memberCount; i++)
                {
                    float lateral = (i - ((memberCount - 1) * .5f)) * 1.8f;
                    Vector3 intended = player.transform.position + directions[d] * FormationDistance + right * lateral;
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(intended, out hit, 3f, NavMesh.AllAreas) ||
                        protectedActors.Any(x => !MeetsClearance(Vector3.Distance(x.transform.position, hit.position), SpawnProtectedClearance)) ||
                        worldActors.Any(x => !MeetsClearance(Vector3.Distance(x.transform.position, hit.position), SpawnActorCollisionClearance)))
                    { valid = false; break; }
                    NavMeshPath path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(hit.position, playerHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    { valid = false; break; }
                    candidate.Add(hit.position);
                }
                if (!valid || candidate.Count != memberCount) continue;
                positions = candidate; reason = string.Empty;
                PvpDiagnostics.Log("spawn_clearance pass members=" + memberCount + "; player_protected_clearance=" + PlayerProtectedClearance +
                    "; spawn_protected_clearance=" + SpawnProtectedClearance + "; spawn_actor_collision_clearance=" + SpawnActorCollisionClearance +
                    "; world_actors=" + worldActors.Count + "; protected_actors=" + protectedActors.Count + "; formation_distance=" + FormationDistance);
                return true;
            }
            PvpDiagnostics.Log("spawn_clearance blocked=no_formation; world_actors=" + worldActors.Count + "; protected_actors=" + protectedActors.Count + "; members=" + memberCount);
            return false;
        }

        private static List<Character> OutsideNpcActors(Character player)
        {
            HashSet<Character> principals = new HashSet<Character> { player };
            try
            {
                if (GameData.GroupMembers != null)
                    foreach (SimPlayerTracking tracking in GameData.GroupMembers)
                    {
                        Character actor = tracking == null || tracking.MyAvatar == null || tracking.MyAvatar.MyStats == null
                            ? null : tracking.MyAvatar.MyStats.Myself;
                        if (actor != null) principals.Add(actor);
                    }
            }
            catch { }
            List<Character> result = new List<Character>();
            try
            {
                foreach (NPC npc in UnityEngine.Object.FindObjectsOfType<NPC>())
                {
                    if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                    Character actor = npc.GetComponent<Character>() ?? npc.GetComponentInParent<Character>();
                    if (actor == null || actor.MyStats == null || !actor.Alive || IsOwnedBy(actor, principals)) continue;
                    if (!result.Contains(actor)) result.Add(actor);
                }
            }
            catch { }
            return result;
        }

        private static bool IsOwnedBy(Character actor, HashSet<Character> principals)
        {
            if (actor == null) return false;
            if (principals.Contains(actor)) return true;
            Character owner = null;
            try { owner = actor.Master; } catch { }
            for (int depth = 0; owner != null && depth < 4; depth++)
            {
                if (principals.Contains(owner)) return true;
                try { owner = owner.Master; } catch { return false; }
            }
            return false;
        }

        private static string ActorName(Character actor)
        {
            try
            {
                if (actor != null && actor.MyStats != null && !string.IsNullOrWhiteSpace(actor.MyStats.MyName)) return actor.MyStats.MyName;
                if (actor != null && actor.MyNPC != null && !string.IsNullOrWhiteSpace(actor.MyNPC.NPCName)) return actor.MyNPC.NPCName;
                return actor == null ? "an NPC" : actor.name;
            }
            catch { return "an NPC"; }
        }

        private static bool MeetsClearance(float distance, float required)
        {
            return distance >= required;
        }

        internal static string RunSpawnPolicySelfTests()
        {
            if (MeetsClearance(9.99f, PlayerProtectedClearance)) return "FAIL protected player clearance";
            if (!MeetsClearance(PlayerProtectedClearance, PlayerProtectedClearance)) return "FAIL protected player boundary";
            if (MeetsClearance(7.99f, SpawnProtectedClearance)) return "FAIL protected spawn clearance";
            if (!MeetsClearance(SpawnProtectedClearance, SpawnProtectedClearance)) return "FAIL protected spawn boundary";
            if (MeetsClearance(1.49f, SpawnActorCollisionClearance)) return "FAIL world actor collision clearance";
            if (!MeetsClearance(SpawnActorCollisionClearance, SpawnActorCollisionClearance)) return "FAIL world actor collision boundary";
            return "PASS pvp spawn clearance";
        }

        internal static string Despawn(string reason)
        {
            PvpCombatContainment.End(reason);
            GameObject clone = _clone;
            // A match that ends without a fight verdict still needs a terminal record, otherwise a
            // consumer such as Nemesis waits forever for a result that will never arrive. It is
            // reported as cancelled/invalid, never as an escape.
            string cancelledMatchId = _activeMatchId;
            string cancelledOpponent = _activeProfile != null ? _activeProfile.Name
                : (TeamProfiles.Count > 0 && TeamProfiles[0] != null ? TeamProfiles[0].Name : "PvP Proxy");
            PvpEncounterMode cancelledMode = _activeMode;
            _clone = null; _activeProfile = null; _despawnAt = 0f; _activeMatchId = string.Empty; _activeMode = PvpEncounterMode.Arranged; _activeMotive = string.Empty;
            string cancelledClassification = ErenshorPvpApi.ClassifyOutcome(reason);
            string cancelledModeToken = cancelledMode.ToString().ToLowerInvariant();
            if (!string.IsNullOrEmpty(cancelledMatchId))
            {
                if (ErenshorPvpApi.TryRecordResult(cancelledMatchId, cancelledOpponent, reason ?? "manual", cancelledModeToken, cancelledClassification))
                {
                    // Social consumers hear the first terminal result exactly once. The housekeeping
                    // despawn event below carries no match and is not part of their allow lists.
                    ErenshorPvpEvents.Publish(new PvpSemanticEvent("pvp_cancelled", cancelledMatchId, cancelledOpponent,
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, cancelledModeToken, reason ?? "manual", cancelledClassification));
                }
            }
            if (TeamClones.Count == 0 && clone == null) return "[Erenshor PvP] No temporary clone is active.";
            foreach (GameObject member in TeamClones)
            {
                if (member == null) continue;
                TemporaryActorIds.Remove(member.GetInstanceID());
                VisualAnimators.Remove(member.GetInstanceID());
                EquipmentVisualsApplied.Remove(member.GetInstanceID());
                ClassLoadoutsApplied.Remove(member.GetInstanceID());
                EligibleCombatSpellCounts.Remove(member.GetInstanceID());
                EligibleHealSpellCounts.Remove(member.GetInstanceID());
                AttackDecisionCounts.Remove(member.GetInstanceID());
                AttackSkillDecisionLogged.Remove(member.GetInstanceID());
                AttackSpellDecisionLogged.Remove(member.GetInstanceID());
                HealCheckCounts.Remove(member.GetInstanceID());
                SpellStartCounts.Remove(member.GetInstanceID());
                NativeStartCompleted.Remove(member.GetInstanceID());
                NativeNavProbes.Remove(member.GetInstanceID());
                NativeUpdateReached.Remove(member.GetInstanceID());
                CombatSectionReached.Remove(member.GetInstanceID());
                LegalTargetAcquired.Remove(member.GetInstanceID());
                NavPursuitRequested.Remove(member.GetInstanceID());
                MeleeAttemptCounts.Remove(member.GetInstanceID());
                NativeUpdateExceptionLogged.Remove(member.GetInstanceID());
                try { UnityEngine.Object.Destroy(member); } catch { }
            }
            TeamClones.Clear(); TeamProfiles.Clear(); VisualAnimators.Clear(); EquipmentVisualsApplied.Clear(); ClassLoadoutsApplied.Clear(); EligibleCombatSpellCounts.Clear(); EligibleHealSpellCounts.Clear(); AttackDecisionCounts.Clear(); AttackSkillDecisionLogged.Clear(); AttackSpellDecisionLogged.Clear(); HealCheckCounts.Clear(); SpellStartCounts.Clear(); LastSpellStartFrame.Clear(); NativeStartCompleted.Clear(); NativeNavProbes.Clear(); NativeUpdateReached.Clear(); CombatSectionReached.Clear(); LegalTargetAcquired.Clear(); NavPursuitRequested.Clear(); MeleeAttemptCounts.Clear(); NativeUpdateExceptionLogged.Clear(); _runtimeInvalidCleanupQueued = false; _runtimeInvalidReason = string.Empty; DestroyTemporarySpells();
            ErenshorPvpEvents.Publish(new PvpSemanticEvent("pvp_proxy_despawned", string.Empty, "PvP Proxy",
                string.Empty, "cleanup", reason ?? "manual"));
            PvpController.EncounterCleaned();
            return "[Erenshor PvP] Temporary clone removed (" + (reason ?? "manual") + ").";
        }

        internal static void Shutdown() { Despawn("shutdown"); }

        internal static bool IsTemporaryActor(Character actor)
        {
            try { return actor != null && TeamClones.Contains(actor.gameObject); } catch { return false; }
        }

        internal static bool IsTemporaryNpc(NPC npc)
        {
            try { return npc != null && TeamClones.Contains(npc.gameObject); } catch { return false; }
        }

        // Character/NPC Start can rebuild native XP values after the proxy is cloned. Reapply the
        // no-borrowed-rewards boundary immediately before DoDeath so only PvpRewardService pays
        // out for the completed team encounter.
        internal static void SuppressBorrowedDeathRewards(Character actor)
        {
            if (!IsTemporaryActor(actor)) return;
            try
            {
                TrySetField(actor, "xp", 0);
                actor.BossXp = 0f;
                actor.BonusRangeXP = Vector2.zero;
                actor.QuestCompleteOnDeath = null;
                actor.factionMods = new ModifyFaction[0];
                LootTable loot = actor.GetComponent<LootTable>();
                if (loot != null)
                {
                    loot.MinGold = 0;
                    loot.MaxGold = 0;
                    loot.MyGold = 0;
                    loot.enabled = false;
                }
                PvpDiagnostics.Log("borrowed_death_rewards_suppressed actor=" + actor.name);
            }
            catch (Exception ex) { Debug.LogWarning("[Erenshor PvP] reward_suppression_failed=" + ex.GetType().Name); }
        }

        internal static bool ValidateProxyStartupInvariant(GameObject go, out string reason)
        {
            reason = string.Empty;
            if (go == null) { reason = "missing_root"; return false; }
            NPC npc = go.GetComponent<NPC>();
            Character actor = go.GetComponent<Character>();
            Stats stats = actor == null ? null : actor.MyStats;
            CastSpell caster = go.GetComponent<CastSpell>();
            NavMeshAgent nav = go.GetComponent<NavMeshAgent>();
            SimPlayer sim = go.GetComponent<SimPlayer>();
            bool actorLinksNpc = false, statsLinksActor = false;
            try { actorLinksNpc = actor != null && actor.MyNPC == npc; } catch { }
            try { statsLinksActor = stats != null && stats.Myself == actor; } catch { }
            bool persistent = false;
            try { persistent = (sim != null && (sim.enabled || sim.MySimTracking != null || sim.myIndex >= 0)) || (npc != null && (npc.SimPlayer || npc.ThisSim != null)); } catch { persistent = true; }
            string maintenanceReason;
            bool maintenance = ValidateNativeMaintenanceState(npc, actor, stats, caster, nav, out maintenanceReason);
            bool pass = PvpProxyStartupPolicy.InvariantPasses(TeamClones.Contains(go), npc != null, actor != null, stats != null,
                actorLinksNpc, statsLinksActor, caster != null, nav != null, persistent);
            pass = pass && maintenance;
            if (!pass)
            {
                reason = "registered=" + TeamClones.Contains(go) + ",npc=" + (npc != null) + ",character=" + (actor != null) +
                    ",stats=" + (stats != null) + ",actorNpc=" + actorLinksNpc + ",statsActor=" + statsLinksActor +
                    ",caster=" + (caster != null) + ",nav=" + (nav != null) + ",persistent=" + persistent +
                    ",maintenance=" + maintenanceReason;
            }
            return pass;
        }

        private static void ConfigureNativeMaintenanceState(NPC npc, Character actor, CastSpell caster, NavMeshAgent nav)
        {
            if (npc == null) return;
            try
            {
                // These are the Start-owned references that must still point at the converted proxy after
                // native Start completes. The same routine also establishes a safe pre-Start graph for
                // countdown validation.
                TrySetField(npc, "Myself", actor);
                TrySetField(npc, "MyStats", actor == null ? null : actor.MyStats);
                TrySetField(npc, "MySpells", caster);
                TrySetField(npc, "MyNav", nav);
                TrySetField(npc, "MyCharControl", npc.GetComponent<CharacterController>());
                TrySetField(npc, "MyRaidSlot", null);
                EnsureNamePlatePresentation(npc, actor);
                object currentFlash;
                if (!TryReadField(npc, "NameFlash", out currentFlash) || currentFlash == null)
                {
                    FlashUIColors flash = null;
                    object namePlateObject;
                    if (TryReadField(npc, "NamePlateObject", out namePlateObject) && namePlateObject is Component)
                        flash = ((Component)namePlateObject).GetComponent<FlashUIColors>();
                    object namePlate;
                    if (flash == null && TryReadField(npc, "NamePlate", out namePlate) && namePlate is Component)
                        flash = ((Component)namePlate).GetComponent<FlashUIColors>();
                    if (flash == null) flash = npc.GetComponentInChildren<FlashUIColors>(true);
                    TrySetField(npc, "NameFlash", flash);
                }
            }
            catch { }
        }

        // NPC.Update calls HandleNameTag() as its THIRD statement - before the NeverAggro early-out and
        // before every combat call - and HandleNameTag dereferences NPC.NamePlateTxt (TMPro.TextMeshPro)
        // via callvirt Behaviour.get_enabled() in every branch. NPC.Update has no exception handler, so a
        // null NamePlateTxt throws every frame and the whole combat/aggro/nav half of Update never runs.
        //
        // Verified against the installed Assembly-CSharp.dll: NamePlateTxt and NamePlateObject are written
        // by NPC.Start and by nothing else, and read by HandleNameTag (NamePlateObject also by Start).
        // The 0.5.5 repair originally reconstructed these because 0.5.4/0.5.5 bypassed native Start.
        // 0.5.9 preserves the proxy-owned nameplate invariant even though native Start is restored, so a
        // template/source nameplate is never shared across actors.
        //
        // Native Start's recipe (IL_0475-0682) is reproduced here in the same order:
        //   NamePlate       = Instantiate(GameData.GM.GetComponent<Misc>().NamePlate, pos, rot).transform
        //   NamePlateTxt    = NamePlate.GetComponent<TextMeshPro>()
        //   NamePlateObject = NamePlate.GetComponent<NamePlate>()
        //   NamePlateObject.MyStats / .Myself = this NPC's Stats / Character
        //   NamePlate text  = NPCName
        //   NamePlate.SetParent(transform)
        // Preference order is reuse-then-rebuild so a clone that already carries a valid nameplate keeps it.
        private static void EnsureNamePlatePresentation(NPC npc, Character actor)
        {
            if (npc == null) return;
            try
            {
                Transform plate = npc.NamePlate;

                // (a)/(b) Reuse the clone's OWN nameplate when it survived cloning. A component that is not
                // under this proxy's transform belongs to the source/template NPC: binding it would make two
                // NPCs share one nameplate and mutate the live original, so it is rejected here.
                if (plate != null && !IsUnderProxyRoot(npc, plate)) plate = null;
                if (plate == null)
                {
                    TextMeshPro owned = FindOwnNamePlateText(npc);
                    if (owned != null) plate = owned.transform;
                }

                // (c) The clone genuinely has no nameplate presentation. Build the native equivalent from the
                // same prefab NPC.Start uses, so HandleNameTag sees exactly the shape it expects.
                if (plate == null) plate = InstantiateNativeNamePlate(npc);
                if (plate == null) return; // validation below fails the proxy; preparation must not continue.

                TrySetField(npc, "NamePlate", plate);
                TextMeshPro text = plate.GetComponent<TextMeshPro>();
                if (text == null) text = plate.GetComponentInChildren<TextMeshPro>(true);
                TrySetField(npc, "NamePlateTxt", text);

                NamePlate plateComponent = plate.GetComponent<NamePlate>();
                if (plateComponent == null) plateComponent = plate.GetComponentInChildren<NamePlate>(true);
                if (plateComponent != null)
                {
                    TrySetField(npc, "NamePlateObject", plateComponent);
                    // Start binds the nameplate back to its owner's live stats/character. Without this the
                    // plate would report the template creature instead of the proxy.
                    TrySetField(plateComponent, "MyStats", actor == null ? null : actor.MyStats);
                    TrySetField(plateComponent, "Myself", actor);
                }

                if (text != null && !string.IsNullOrEmpty(npc.NPCName)) text = ApplyNamePlateText(text, npc.NPCName);
                if (plate.parent != npc.transform) plate.SetParent(npc.transform);
            }
            catch { }
        }

        // 0.5.9 forensic recovery: native NPC.Start owns the complete navigation/behavior
        // lifecycle. Do not manufacture NavUpdate/BehaviorUpdate IEnumerator instances here. A
        // non-null coroutine field proves only launch; the bounded probe below requires native Start,
        // UpdateNav entry, a completed first UpdateNav call, and no fault.
        internal static void PrepareNativeStartProbe(NPC npc)
        {
            if (npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null) return;
            int id = npc.gameObject.GetInstanceID();
            NativeStartCompleted.Remove(id);
            NativeNavProbe probe = new NativeNavProbe();
            try
            {
                probe.StartPosition = npc.transform.position;
                probe.LastPosition = probe.StartPosition;
            }
            catch { }
            NativeNavProbes[id] = probe;
            // A cloned component may carry IEnumerator fields copied from the already-started source.
            // They are not running on this MonoBehaviour and must never be mistaken for native health.
            TrySetField(npc, "navDo", null);
            TrySetField(npc, "behDo", null);
        }

        internal static void ObserveNativeNavEntered(NPC npc)
        {
            if (npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null) return;
            NativeNavProbe probe;
            if (!NativeNavProbes.TryGetValue(npc.gameObject.GetInstanceID(), out probe)) return;
            probe.UpdateNavReached = true;
            object navDo;
            if (TryReadField(npc, "navDo", out navDo) && navDo != null) probe.CoroutineObserved = true;
        }

        internal static void ObserveNativeNavCompleted(NPC npc)
        {
            if (npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null) return;
            NativeNavProbe probe;
            if (!NativeNavProbes.TryGetValue(npc.gameObject.GetInstanceID(), out probe)) return;
            probe.UpdateNavReached = true;
            probe.FirstUpdateNavCompleted = true;
            object navDo;
            if (TryReadField(npc, "navDo", out navDo) && navDo != null) probe.CoroutineObserved = true;
        }

        internal static Exception ObserveNativeNavException(NPC npc, Exception exception)
        {
            if (exception == null || npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null) return exception;
            int id = npc.gameObject.GetInstanceID();
            NativeNavProbe probe;
            if (!NativeNavProbes.TryGetValue(id, out probe))
            {
                probe = new NativeNavProbe();
                NativeNavProbes[id] = probe;
            }
            bool first = !probe.Faulted;
            probe.Faulted = true;
            probe.FaultType = exception.GetType().Name;
            if (first)
                PvpDiagnostics.Log("proxy_nav_faulted proxy=" + SafeProxyName(npc) +
                    "; fault=" + probe.FaultType + "; updateNavReached=" + probe.UpdateNavReached +
                    "; firstUpdateNavCompleted=" + probe.FirstUpdateNavCompleted);
            return exception; // diagnostics only; never hide a native navigation failure.
        }

        internal static bool NativeNavHealthy(NPC npc)
        {
            if (npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null) return false;
            int id = npc.gameObject.GetInstanceID();
            NativeNavProbe probe;
            if (!NativeNavProbes.TryGetValue(id, out probe)) return false;
            return PvpNativeNavHealthPolicy.IsHealthy(NativeStartCompleted.Contains(id),
                probe.CoroutineObserved, probe.UpdateNavReached, probe.FirstUpdateNavCompleted, probe.Faulted);
        }

        internal static bool CompleteNativeNavFailure(IList<NPC> npcs)
        {
            if (npcs == null || npcs.Count == 0) return false;
            int faulted = 0, relevant = 0;
            for (int i = 0; i < npcs.Count; i++)
            {
                NPC npc = npcs[i];
                if (npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null) continue;
                relevant++;
                NativeNavProbe probe;
                if (NativeNavProbes.TryGetValue(npc.gameObject.GetInstanceID(), out probe) && probe.Faulted) faulted++;
            }
            return PvpNativeNavHealthPolicy.CompleteNavFailure(relevant, faulted);
        }

        internal static string NativeNavHealthSummary()
        {
            int starts = 0, coroutine = 0, entered = 0, completed = 0, destination = 0, movement = 0, faulted = 0;
            int agentPresent = 0, agentEnabled = 0, onMesh = 0;
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i]; if (go == null) continue; int id = go.GetInstanceID();
                if (NativeStartCompleted.Contains(id)) starts++;
                NativeNavProbe probe;
                if (NativeNavProbes.TryGetValue(id, out probe))
                {
                    if (probe.CoroutineObserved) coroutine++;
                    if (probe.UpdateNavReached) entered++;
                    if (probe.FirstUpdateNavCompleted) completed++;
                    if (probe.DestinationAttempted) destination++;
                    if (probe.MovementObserved) movement++;
                    if (probe.Faulted) faulted++;
                }
                try
                {
                    NavMeshAgent nav = go.GetComponent<NavMeshAgent>();
                    if (nav != null)
                    {
                        agentPresent++;
                        if (nav.enabled) agentEnabled++;
                        if (nav.enabled && nav.isOnNavMesh) onMesh++;
                    }
                }
                catch { }
            }
            return "nav_start_completed=" + starts + "/" + TeamClones.Count +
                "; nav_coroutine_observed=" + coroutine + "; updateNav_reached=" + entered +
                "; nav_first_step_completed=" + completed + "; nav_destination=" + destination +
                "; nav_movement=" + movement + "; nav_faulted=" + faulted +
                "; nav_agents=" + agentPresent + "; nav_enabled=" + agentEnabled + "; nav_on_mesh=" + onMesh;
        }

        private static void TickNativeNavHealth()
        {
            if (!PvpCombatContainment.LethalFightActive) return;
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i]; if (go == null) continue;
                NativeNavProbe probe; int id = go.GetInstanceID();
                if (!NativeNavProbes.TryGetValue(id, out probe)) continue;
                try
                {
                    NPC npc = go.GetComponent<NPC>();
                    NavMeshAgent nav = go.GetComponent<NavMeshAgent>();
                    object navDo;
                    if (npc != null && TryReadField(npc, "navDo", out navDo) && navDo != null) probe.CoroutineObserved = true;
                    if (nav != null && nav.enabled && nav.isOnNavMesh && nav.hasPath) probe.DestinationAttempted = true;
                    Vector3 position = go.transform.position;
                    if (!probe.MovementObserved && (position - probe.StartPosition).sqrMagnitude > 0.04f) probe.MovementObserved = true;
                    probe.LastPosition = position;
                }
                catch { }
            }
        }

        private static TextMeshPro ApplyNamePlateText(TextMeshPro text, string value)
        {
            try { text.text = value; } catch { }
            return text;
        }

        private static bool IsUnderProxyRoot(NPC npc, Transform candidate)
        {
            if (npc == null || candidate == null) return false;
            Transform root = npc.transform;
            for (Transform t = candidate; t != null; t = t.parent) if (t == root) return true;
            return false;
        }

        // Only ever returns a TextMeshPro that lives inside this proxy's own hierarchy.
        private static TextMeshPro FindOwnNamePlateText(NPC npc)
        {
            if (npc == null) return null;
            TextMeshPro[] candidates = npc.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                TextMeshPro candidate = candidates[i];
                if (candidate != null && IsUnderProxyRoot(npc, candidate.transform)) return candidate;
            }
            return null;
        }

        private static Transform InstantiateNativeNamePlate(NPC npc)
        {
            try
            {
                if (GameData.GM == null) return null;
                Misc misc = GameData.GM.GetComponent<Misc>();
                if (misc == null || misc.NamePlate == null) return null;
                GameObject spawned = UnityEngine.Object.Instantiate(misc.NamePlate,
                    npc.transform.position, npc.transform.rotation);
                if (spawned == null) return null;
                spawned.name = "PvP_NamePlate_" + (string.IsNullOrEmpty(npc.NPCName) ? "proxy" : npc.NPCName);
                return spawned.transform;
            }
            catch { return null; }
        }

        private static bool ValidateNativeMaintenanceState(NPC npc, Character actor, Stats stats,
            CastSpell caster, NavMeshAgent nav, out string reason)
        {
            bool self = false, boundStats = false, boundNav = false, boundCaster = false, flash = false, raidClear = false;
            bool plateText = false, plateObject = false;
            try
            {
                object value;
                self = npc != null && TryReadField(npc, "Myself", out value) && object.ReferenceEquals(value, actor);
                boundStats = npc != null && TryReadField(npc, "MyStats", out value) && object.ReferenceEquals(value, stats);
                boundNav = npc != null && TryReadField(npc, "MyNav", out value) && object.ReferenceEquals(value, nav);
                boundCaster = npc != null && TryReadField(npc, "MySpells", out value) && object.ReferenceEquals(value, caster);
                flash = npc != null && TryReadField(npc, "NameFlash", out value) && value != null;
                raidClear = npc != null && TryReadField(npc, "MyRaidSlot", out value) && value == null;
                // The exact two references NPC.HandleNameTag dereferences on its first Update. Compared
                // against Unity's null (not just a C# reference check) so a destroyed nameplate also fails.
                plateText = npc != null && TryReadField(npc, "NamePlateTxt", out value) &&
                            value is TextMeshPro && (TextMeshPro)value != null;
                plateObject = npc != null && TryReadField(npc, "NamePlateObject", out value) &&
                              value is NamePlate && (NamePlate)value != null;
            }
            catch { }
            bool pass = PvpProxyStartupPolicy.MaintenanceStatePasses(npc != null && TeamClones.Contains(npc.gameObject),
                self, boundStats, boundNav, boundCaster, flash, raidClear, plateText, plateObject);
            reason = pass ? "pass" : "self=" + self + ",stats=" + boundStats + ",nav=" + boundNav +
                ",caster=" + boundCaster + ",nameFlash=" + flash + ",raidSlotClear=" + raidClear +
                ",namePlateTxt=" + plateText + ",namePlateObject=" + plateObject;
            return pass;
        }

        internal static bool AllowNativeMaintenance(NPC npc)
        {
            if (!IsTemporaryNpc(npc)) return true;
            Character actor = npc == null ? null : npc.GetComponent<Character>();
            Stats stats = actor == null ? null : actor.MyStats;
            CastSpell caster = npc == null ? null : npc.GetComponent<CastSpell>();
            NavMeshAgent nav = npc == null ? null : npc.GetComponent<NavMeshAgent>();
            string reason;
            bool valid = ValidateNativeMaintenanceState(npc, actor, stats, caster, nav, out reason);
            if (!PvpProxyStartupPolicy.ShouldInterceptMaintenance(true, valid)) return true;
            if (!_runtimeInvalidCleanupQueued)
            {
                _runtimeInvalidCleanupQueued = true;
                _runtimeInvalidReason = reason;
                PvpDiagnostics.Log("proxy_runtime_invalid proxy=" + SafeProxyName(npc) + "; missing=" + reason +
                    "; action=cancel_and_cleanup; template=" + _templateSource);
                try { PvpCombatContainment.End("runtime_invalid"); } catch { }
            }
            return false;
        }

        internal static bool AllowNativeNpcStart(NPC npc, out bool temporaryNativeStartExpected)
        {
            temporaryNativeStartExpected = false;
            if (!IsTemporaryNpc(npc)) return true;

            string reason;
            bool ready = ValidateProxyStartupInvariant(npc == null ? null : npc.gameObject, out reason);
            if (!ready)
            {
                if (!_runtimeInvalidCleanupQueued)
                {
                    _runtimeInvalidCleanupQueued = true;
                    _runtimeInvalidReason = "pre_start/" + reason;
                }
                PvpDiagnostics.Log("proxy_native_start action=blocked_invalid; proxy=" + SafeProxyName(npc) +
                    "; pre_start_invariant=fail:" + reason + "; template=" + _templateSource);
                return false;
            }

            bool runNative = PvpProxyStartupPolicy.ShouldRunNativeNpcStart(true, true);
            temporaryNativeStartExpected = runNative;
            PvpDiagnostics.Log("proxy_native_start action=native_pending; proxy=" + SafeProxyName(npc) +
                "; pre_start_invariant=pass; template=" + _templateSource);
            return runNative;
        }

        internal static void ObserveNativeNpcStartCompleted(NPC npc, bool temporaryNativeStartExpected)
        {
            if (!temporaryNativeStartExpected || !IsTemporaryNpc(npc) || npc.gameObject == null) return;
            int id = npc.gameObject.GetInstanceID();
            NativeStartCompleted.Add(id);
            NativeNavProbe probe;
            if (!NativeNavProbes.TryGetValue(id, out probe))
            {
                probe = new NativeNavProbe();
                try { probe.StartPosition = probe.LastPosition = npc.transform.position; } catch { }
                NativeNavProbes[id] = probe;
            }
            object navDo;
            if (TryReadField(npc, "navDo", out navDo) && navDo != null) probe.CoroutineObserved = true;

            Character actor = npc.GetComponent<Character>();
            CastSpell caster = npc.GetComponent<CastSpell>();
            NavMeshAgent nav = npc.GetComponent<NavMeshAgent>();
            int teamIndex = TeamClones.IndexOf(npc.gameObject);
            PvpOpponentProfile profile = teamIndex >= 0 && teamIndex < TeamProfiles.Count ? TeamProfiles[teamIndex] : null;
            try
            {
                // Native Start is authoritative for the lifecycle graph. Immediately reassert only
                // PvP-owned identity/loadout/reward constraints that Start may derive from the borrowed body.
                npc.ThisSim = null;
                npc.SimPlayer = false;
                npc.InGroup = false;
                npc.NeverAggro = false;
                ApplyProfileLoadout(npc, profile);
                ConfigureNativeMaintenanceState(npc, actor, caster, nav);
                SuppressBorrowedDeathRewards(actor);
            }
            catch { }

            string invariant, reward;
            bool runtimeValid = ValidateProxyStartupInvariant(npc.gameObject, out invariant);
            bool rewardValid = ValidateProxyRewardBoundary(npc.gameObject, out reward);
            PvpDiagnostics.Log("proxy_native_start action=native; completion=completed; proxy=" + SafeProxyName(npc) +
                "; runtime=" + (runtimeValid ? "pass" : "fail:" + invariant) +
                "; reward=" + (rewardValid ? "pass" : "fail:" + reward) +
                "; nav_coroutine_observed=" + probe.CoroutineObserved + "; template=" + _templateSource);
            if (!runtimeValid || !rewardValid)
            {
                _runtimeInvalidCleanupQueued = true;
                _runtimeInvalidReason = !runtimeValid ? invariant : reward;
                return;
            }
            PvpCombatContainment.ObserveProxyNativeStartCompleted(npc);
        }

        internal static void ObserveNativeNpcStartFailed(NPC npc, Exception exception)
        {
            if (npc == null || !IsTemporaryNpc(npc) || npc.gameObject == null || exception == null) return;
            int id = npc.gameObject.GetInstanceID();
            NativeNavProbe probe;
            if (!NativeNavProbes.TryGetValue(id, out probe))
            {
                probe = new NativeNavProbe();
                NativeNavProbes[id] = probe;
            }
            probe.Faulted = true;
            probe.FaultType = "NPC.Start/" + exception.GetType().Name;
            _runtimeInvalidCleanupQueued = true;
            _runtimeInvalidReason = probe.FaultType;
        }

        internal static bool ValidateProxyRewardBoundary(GameObject go, out string reason)
        {
            reason = string.Empty;
            Character actor = go == null ? null : go.GetComponent<Character>();
            if (actor == null) { reason = "missing_character"; return false; }
            int xp;
            bool xpReadableAndZero = TryReadIntField(actor, "xp", out xp) && xp == 0;
            bool bossXpZero = false, bonusXpZero = false, questNull = false, factionEmpty = false;
            try
            {
                bossXpZero = actor.BossXp == 0f;
                bonusXpZero = actor.BonusRangeXP == Vector2.zero;
                questNull = actor.QuestCompleteOnDeath == null;
                factionEmpty = actor.factionMods == null || actor.factionMods.Length == 0;
            }
            catch { }
            LootTable loot = go.GetComponent<LootTable>();
            bool lootGoldZero = loot == null || (loot.MinGold == 0 && loot.MaxGold == 0 && loot.MyGold == 0);
            bool lootDisabled = loot == null || !loot.enabled;
            bool pass = PvpProxyStartupPolicy.RewardBoundaryPasses(xpReadableAndZero, bossXpZero, bonusXpZero,
                questNull, factionEmpty, lootGoldZero, lootDisabled);
            if (!pass)
                reason = "xp=" + (xpReadableAndZero ? "0" : "unreadable_or_nonzero") + ",bossXp=" + bossXpZero +
                    ",bonusXp=" + bonusXpZero + ",questNull=" + questNull + ",factionEmpty=" + factionEmpty +
                    ",lootGoldZero=" + lootGoldZero + ",lootDisabled=" + lootDisabled;
            return pass;
        }

        internal static void ObserveNativeUpdate(NPC npc)
        {
            if (!IsTemporaryNpc(npc) || !PvpCombatContainment.LethalFightActive || npc.NeverAggro) return;
            int id = npc.gameObject.GetInstanceID();
            if (NativeUpdateReached.Add(id))
                PvpDiagnostics.Log("native_update_reached proxy=" + SafeProxyName(npc) + "; neverAggro=false");
        }

        internal static void ObserveNativeUpdateCompleted(NPC npc)
        {
            if (!IsTemporaryNpc(npc) || !PvpCombatContainment.LethalFightActive || npc.NeverAggro) return;
            int id = npc.gameObject.GetInstanceID();
            Character target = null;
            try { target = npc.CurrentAggroTarget; } catch { }
            if (target != null && PvpCombatContainment.IsProtectedWorldActor(target))
            {
                PvpDiagnostics.Log("protected_target_cleared proxy=" + SafeProxyName(npc) + "; target=" + PvpCombatContainment.DescribeCombatTarget(target));
                try { npc.ForceAggroOn(null); } catch { }
                return;
            }
            if (target != null && PvpCombatContainment.IsPermittedProxyTarget(target) && LegalTargetAcquired.Add(id))
                PvpDiagnostics.Log("legal_target_acquired proxy=" + SafeProxyName(npc) + "; target=" + PvpCombatContainment.DescribeCombatTarget(target));

            try
            {
                NavMeshAgent nav = npc.GetComponent<NavMeshAgent>();
                if (nav != null && nav.enabled && nav.isOnNavMesh && nav.hasPath && NavPursuitRequested.Add(id))
                    PvpDiagnostics.Log("nav_pursuit_requested proxy=" + SafeProxyName(npc) + "; target=" + PvpCombatContainment.DescribeCombatTarget(target));
            }
            catch { }
        }

        internal static Exception ObserveNativeUpdateException(NPC npc, Exception exception)
        {
            if (exception == null || !IsTemporaryNpc(npc)) return exception;
            int id = npc.gameObject.GetInstanceID();
            if (NativeUpdateExceptionLogged.Add(id))
                PvpDiagnostics.Log("native_npc_exception method=NPC.Update; proxy=" + SafeProxyName(npc) +
                    "; type=" + exception.GetType().Name + "; message=" + (exception.Message ?? string.Empty));
            return exception; // diagnostics never suppress native failures.
        }

        internal static void ObserveCombatSection(NPC npc)
        {
            if (!IsTemporaryNpc(npc) || !PvpCombatContainment.LethalFightActive || npc.NeverAggro) return;
            int id = npc.gameObject.GetInstanceID();
            if (CombatSectionReached.Add(id))
                PvpDiagnostics.Log("combat_section_reached proxy=" + SafeProxyName(npc));
        }

        internal static void ObserveMeleeAttempt(NPC npc)
        {
            if (!IsTemporaryNpc(npc) || !PvpCombatContainment.LethalFightActive || npc.NeverAggro) return;
            int id = npc.gameObject.GetInstanceID();
            int before; MeleeAttemptCounts.TryGetValue(id, out before);
            Increment(MeleeAttemptCounts, id);
            if (before == 0)
            {
                PvpDiagnostics.Log("melee_decision proxy=" + SafeProxyName(npc));
                PvpDiagnostics.Log("melee_attempt proxy=" + SafeProxyName(npc));
            }
        }

        internal static void ObserveAttackDecision(NPC npc, bool spellDecision)
        {
            if (!IsTemporaryNpc(npc) || !PvpCombatContainment.LethalFightActive || npc.NeverAggro) return;
            int id = npc.gameObject.GetInstanceID();
            Increment(AttackDecisionCounts, id);
            HashSet<int> logged = spellDecision ? AttackSpellDecisionLogged : AttackSkillDecisionLogged;
            if (logged.Add(id))
                PvpDiagnostics.Log((spellDecision ? "attack_spell_decision" : "attack_skill_decision") +
                    " proxy=" + SafeProxyName(npc));
        }

        internal static void ObserveHealCheck(NPC npc)
        {
            if (!IsTemporaryNpc(npc) || !PvpCombatContainment.LethalFightActive) return;
            int id = npc.gameObject.GetInstanceID();
            int before; HealCheckCounts.TryGetValue(id, out before);
            Increment(HealCheckCounts, id);
            if (before == 0) PvpDiagnostics.Log("heal_check proxy=" + SafeProxyName(npc));
        }

        internal static bool ObserveAndAllowSpellStart(CastSpell caster, Spell spell, Stats target)
        {
            if (!PvpCombatContainment.AllowSpellStart(caster, spell, target)) return false;
            ObserveSpellStart(caster, spell);
            return true;
        }

        internal static void ObserveSpellStart(CastSpell caster, Spell spell)
        {
            Character actor = null;
            try { actor = caster == null ? null : caster.MyChar; } catch { }
            if (!IsTemporaryActor(actor)) return;
            int actorId = actor.gameObject.GetInstanceID();
            int spellId = spell == null ? 0 : spell.GetHashCode();
            int key = unchecked((actorId * 397) ^ spellId);
            int frame = Time.frameCount, previous;
            if (LastSpellStartFrame.TryGetValue(key, out previous) && previous == frame) return;
            LastSpellStartFrame[key] = frame;
            int before; SpellStartCounts.TryGetValue(actorId, out before);
            Increment(SpellStartCounts, actorId);
            if (before == 0) PvpDiagnostics.Log("spell_start proxy=" + (actor.MyStats == null ? actor.name : actor.MyStats.MyName) +
                "; spell=" + (spell == null ? "unknown" : spell.SpellName));
        }

        internal static bool HasAnyNativeCombatEvidence()
        {
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i]; if (go == null) continue;
                int id = go.GetInstanceID();
                int attacks = 0, melee = 0, heals = 0, casts = 0;
                AttackDecisionCounts.TryGetValue(id, out attacks);
                MeleeAttemptCounts.TryGetValue(id, out melee);
                HealCheckCounts.TryGetValue(id, out heals);
                SpellStartCounts.TryGetValue(id, out casts);
                bool nativeUpdate = NativeUpdateReached.Contains(id);
                bool legalTarget = LegalTargetAcquired.Contains(id);
                bool combatDecision = attacks > 0 || melee > 0;
                if (PvpCombatStartupPolicy.HasCombatEvidence(nativeUpdate, legalTarget,
                    NavPursuitRequested.Contains(id), combatDecision, heals > 0, casts > 0, false, false))
                    return true;
            }
            return false;
        }

        internal static string NativeCombatEvidenceSummary()
        {
            return "native_updates=" + NativeUpdateReached.Count + "; combat_sections=" + CombatSectionReached.Count +
                "; legal_targets=" + LegalTargetAcquired.Count + "; nav_pursuit=" + NavPursuitRequested.Count +
                "; melee_attempts=" + MeleeAttemptCounts.Values.Sum() + "; " + BalanceRuntimeSummary();
        }

        private static void Increment(Dictionary<int, int> map, int id)
        {
            int value; map.TryGetValue(id, out value); map[id] = value + 1;
        }

        private static string SafeProxyName(NPC npc)
        {
            try { return npc == null ? "null" : (string.IsNullOrWhiteSpace(npc.NPCName) ? npc.gameObject.name : npc.NPCName); }
            catch { return "unknown"; }
        }

        internal static string BalanceRuntimeSummary()
        {
            int healCapable = 0, healChecks = 0, attackDecisions = 0, spellStarts = 0;
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i]; if (go == null) continue; int id = go.GetInstanceID();
                int value;
                if (EligibleHealSpellCounts.TryGetValue(id, out value) && value > 0) healCapable++;
                if (HealCheckCounts.TryGetValue(id, out value)) healChecks += value;
                if (AttackDecisionCounts.TryGetValue(id, out value)) attackDecisions += value;
                if (SpellStartCounts.TryGetValue(id, out value)) spellStarts += value;
            }
            return "heal_capable_attackers=" + healCapable + "; heal_checks=" + healChecks +
                "; attack_spell_decisions=" + attackDecisions + "; spell_starts=" + spellStarts;
        }

        internal static string BalanceHealingAssessment(long healingDone)
        {
            int healCapable = 0, healChecks = 0, spellStarts = 0;
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i]; if (go == null) continue; int id = go.GetInstanceID(); int value;
                if (EligibleHealSpellCounts.TryGetValue(id, out value) && value > 0) healCapable++;
                if (HealCheckCounts.TryGetValue(id, out value)) healChecks += value;
                if (SpellStartCounts.TryGetValue(id, out value)) spellStarts += value;
            }
            return PvpProxyStartupPolicy.ZeroHealingAssessment(healCapable, healChecks, spellStarts, healingDone);
        }

        internal static string BeginTargetingTest()
        {
            if (_clone == null) return "[Erenshor PvP] Spawn a temporary clone first: /epvp spawnclone";
            return PvpCombatContainment.BeginTargetingTest(_clone.GetComponent<NPC>(), _clone.GetComponent<Character>());
        }

        internal static bool PrepareForCountdown(out string reason)
        {
            reason = string.Empty;
            if (_clone == null || TeamClones.Count == 0) { reason = "no_active_team"; return false; }
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject member = TeamClones[i];
                string invariant;
                if (!ValidateProxyStartupInvariant(member, out invariant))
                {
                    reason = "proxy" + (i + 1) + "_startup:" + invariant;
                    return false;
                }
                Character actor = member == null ? null : member.GetComponent<Character>();
                NPC npc = member == null ? null : member.GetComponent<NPC>();
                CastSpell caster = member == null ? null : member.GetComponent<CastSpell>();
                NavMeshAgent nav = member == null ? null : member.GetComponent<NavMeshAgent>();
                SuppressBorrowedDeathRewards(actor);
                string rewardInvariant;
                if (!ValidateProxyRewardBoundary(member, out rewardInvariant))
                {
                    reason = "proxy" + (i + 1) + "_reward:" + rewardInvariant;
                    return false;
                }

                // Keep the NPC component disabled through countdown so Unity has not yet invoked
                // its Start lifecycle. At GO we clear NeverAggro, enable nav/spells, then enable NPC;
                // native Start therefore launches BOTH NavUpdate and BehaviorUpdate under the same
                // conditions as the recent live-good implementation.
                if (npc != null) { npc.NeverAggro = true; npc.enabled = false; }
                if (actor != null) actor.enabled = true;
                if (caster != null) caster.enabled = false;
                if (nav != null) nav.enabled = false;
                PvpDiagnostics.Log("proxy_prepared proxy=" + (i + 1) + "; neverAggro=" +
                    (npc == null ? "missing" : npc.NeverAggro.ToString().ToLowerInvariant()) +
                    "; npc_enabled=" + (npc != null && npc.enabled) +
                    "; spells_enabled=" + (caster != null && caster.enabled) + "; nav_enabled=" + (nav != null && nav.enabled));
            }
            return true;
        }

        internal static bool MaintainCountdownHold(out string reason)
        {
            reason = string.Empty;
            if (TeamClones.Count == 0) { reason = "no_active_team"; return false; }
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject member = TeamClones[i];
                NPC npc = member == null ? null : member.GetComponent<NPC>();
                CastSpell caster = member == null ? null : member.GetComponent<CastSpell>();
                NavMeshAgent nav = member == null ? null : member.GetComponent<NavMeshAgent>();
                if (npc == null) { reason = "proxy" + (i + 1) + "_npc_missing"; return false; }
                npc.NeverAggro = true;
                npc.enabled = false;
                if (caster != null) caster.enabled = false;
                try { if (nav != null) nav.enabled = false; } catch { }
                if (!npc.NeverAggro || npc.enabled || (caster != null && caster.enabled) || (nav != null && nav.enabled))
                {
                    reason = "proxy" + (i + 1) + "_hold_failed";
                    return false;
                }
            }
            return true;
        }

        internal static string BeginLethalFight()
        {
            if (_clone == null) return "[Erenshor PvP] Spawn a temporary proxy first: /epvp spawnclone";
            // Validate the converted proxy graph before GO. Native NPC.Start is intentionally restored
            // as the owner of its full navigation/behavior lifecycle; the Start postfix reasserts
            // PvP identity/reward state immediately after native initialization.
            for (int i = 0; i < TeamClones.Count; i++)
            {
                string invariant;
                if (!ValidateProxyStartupInvariant(TeamClones[i], out invariant))
                {
                    PvpDiagnostics.Log("combat_start_blocked proxy=" + (i + 1) + "; startup_invariant=" + invariant);
                    return "[Erenshor PvP] Lethal fight blocked: proxy " + (i + 1) + " native component graph is incomplete.";
                }
                Character rewardActor = TeamClones[i] == null ? null : TeamClones[i].GetComponent<Character>();
                SuppressBorrowedDeathRewards(rewardActor);
                string rewardInvariant;
                if (!ValidateProxyRewardBoundary(TeamClones[i], out rewardInvariant))
                {
                    PvpDiagnostics.Log("combat_start_blocked proxy=" + (i + 1) + "; reward_invariant=" + rewardInvariant);
                    return "[Erenshor PvP] Lethal fight blocked: proxy " + (i + 1) + " borrowed reward state could not be suppressed.";
                }
            }
            RefreshCombatRuntime();
            List<NPC> npcs = new List<NPC>(); List<Character> actors = new List<Character>(); List<CastSpell> spells = new List<CastSpell>();
            foreach (GameObject member in TeamClones)
            {
                if (member == null) continue;
                npcs.Add(member.GetComponent<NPC>()); actors.Add(member.GetComponent<Character>()); spells.Add(member.GetComponent<CastSpell>());
            }
            string result = PvpCombatContainment.BeginLethalFight(npcs, actors, spells);
            if (PvpCombatContainment.LethalFightActive) _despawnAt = float.PositiveInfinity;
            return result;
        }

        private static void RefreshCombatRuntime()
        {
            Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject member = TeamClones[i];
                PvpOpponentProfile profile = i < TeamProfiles.Count ? TeamProfiles[i] : null;
                if (member == null || profile == null) continue;
                NPC npc = member.GetComponent<NPC>();
                Character actor = member.GetComponent<Character>();
                ApplyProfileLoadout(npc, profile);
                ConfigureCombatStats(player, actor, profile);
                CastSpell caster = member.GetComponent<CastSpell>();
                PvpDiagnostics.Log("combat_runtime_ready profile=" + profile.Name +
                    "; known_spells=" + (caster == null || caster.KnownSpells == null ? 0 : caster.KnownSpells.Count) +
                    "; melee=" + (npc == null ? "unavailable" : npc.DamageRange.ToString()));
            }
        }

        // PvP remains lethal if the player stays in the encounter, but opting out by fleeing is
        // a valid MMO-style outcome. It never grants a victory reward and is recorded separately
        // from a defeat so a player does not need to use the debug despawn command to leave.
        internal static string Flee()
        {
            if (!PvpCombatContainment.LethalFightActive)
                return "[Erenshor PvP] You can only flee an active lethal PvP encounter.";
            PvpCombatContainment.End("player_fled");
            DespawnAfterFight("player_fled", null);
            return "[Erenshor PvP] You disengage and escape. No PvP reward was granted.";
        }

        internal static string CloneStatus()
        {
            if (_clone == null) return "[Erenshor PvP] No temporary proxy is active.";
            List<string> health = new List<string>();
            foreach (GameObject member in TeamClones)
            {
                Character actor = member == null ? null : member.GetComponent<Character>();
                health.Add(actor == null || actor.MyStats == null ? "unknown" : actor.MyStats.CurrentHP + "/" + actor.MyStats.CurrentMaxHP);
            }
            Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
            string playerHp = player == null || player.MyStats == null ? "unknown" : player.MyStats.CurrentHP + "/" + player.MyStats.CurrentMaxHP;
            return "[Erenshor PvP] attackers=" + TeamClones.Count + "; hp=" + string.Join(",", health.ToArray()) + "; player=" + playerHp + "; lethal=" + PvpCombatContainment.LethalFightActive + ".";
        }

        internal static string DiagnosticStatus()
        {
            if (!HasActiveTeam) FindNativeMobTemplate();
            return "template=" + _templateSource + "; active_match=" + (string.IsNullOrEmpty(_activeMatchId) ? "none" : _activeMatchId.Substring(0, Math.Min(8, _activeMatchId.Length))) +
                "; mode=" + _activeMode.ToString().ToLowerInvariant() + "; motive=" + (string.IsNullOrEmpty(_activeMotive) ? "none" : _activeMotive) + "; " + CloneStatus().Replace("[Erenshor PvP] ", string.Empty);
        }

        internal static PvpEncounterMode ActiveMode { get { return _activeMode; } }
        internal static string ActiveMotive { get { return _activeMotive ?? string.Empty; } }

        // Structured view of the live proxies for the panel roster. Kept separate from the
        // chat-facing status strings so UI formatting never dictates log wording.
        internal static List<PvpRosterEntry> Roster()
        {
            List<PvpRosterEntry> entries = new List<PvpRosterEntry>();
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i];
                PvpOpponentProfile profile = i < TeamProfiles.Count ? TeamProfiles[i] : null;
                if (go == null && profile == null) continue;
                Character actor = go == null ? null : go.GetComponent<Character>();
                CastSpell caster = go == null ? null : go.GetComponent<CastSpell>();
                int hp = -1, maxHp = -1;
                try { if (actor != null && actor.MyStats != null) { hp = actor.MyStats.CurrentHP; maxHp = actor.MyStats.CurrentMaxHP; } }
                catch { }
                bool alive = false;
                try { alive = actor != null && actor.Alive; } catch { }
                entries.Add(new PvpRosterEntry(
                    profile == null ? "Proxy" : profile.Name,
                    profile == null ? 0 : profile.Level,
                    profile == null ? string.Empty : profile.ClassName,
                    profile == null ? string.Empty : profile.GuildId,
                    PvpTeamMember.RoleFor(profile == null ? null : profile.ClassName),
                    hp, maxHp,
                    caster == null || caster.KnownSpells == null ? 0 : caster.KnownSpells.Count,
                    alive));
            }
            return entries;
        }

        internal static string TeamStatus()
        {
            if (!HasActiveTeam) return "[Erenshor PvP] No active PvP team.";
            List<string> members = new List<string>();
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i]; PvpOpponentProfile profile = i < TeamProfiles.Count ? TeamProfiles[i] : null;
                Character actor = go == null ? null : go.GetComponent<Character>(); CastSpell caster = go == null ? null : go.GetComponent<CastSpell>();
                string hp = actor == null || actor.MyStats == null ? "?" : actor.MyStats.CurrentHP + "/" + actor.MyStats.CurrentMaxHP;
                members.Add((profile == null ? "Proxy" : profile.Name + " L" + profile.Level + " " + profile.ClassName) +
                    " hp=" + hp + " spells=" + (caster == null || caster.KnownSpells == null ? 0 : caster.KnownSpells.Count));
            }
            return "[Erenshor PvP] Active " + _activeMode.ToString().ToLowerInvariant() + " (" + _activeMotive + "): " + string.Join("; ", members.ToArray());
        }

        internal static string VerifyRuntime()
        {
            if (!HasActiveTeam) return "[Erenshor PvP] VERIFY FAIL no active team.";
            List<string> failures = new List<string>();
            if (TeamProfiles.Count != TeamClones.Count) failures.Add("profile_count");
            for (int i = 0; i < TeamClones.Count; i++)
            {
                GameObject go = TeamClones[i];
                if (go == null) { failures.Add("proxy" + (i + 1) + "_missing"); continue; }
                Character actor = go.GetComponent<Character>(); NPC npc = go.GetComponent<NPC>();
                if (actor == null || actor.MyStats == null || actor.MyStats.CurrentMaxHP <= 0) failures.Add("proxy" + (i + 1) + "_hp");
                if (npc == null || npc.SimPlayer || npc.ThisSim != null) failures.Add("proxy" + (i + 1) + "_identity");
                else if (npc.NoSelfHeal) failures.Add("proxy" + (i + 1) + "_self_heal_blocked");
                Transform visual = null;
                foreach (Transform child in go.transform) if (child != null && child.name.StartsWith("PvP_SimVisual_", StringComparison.Ordinal)) { visual = child; break; }
                if (visual == null) failures.Add("proxy" + (i + 1) + "_visual");
                else
                {
                    if (!visual.GetComponentsInChildren<Renderer>(true).Any(x => x != null && x.enabled)) failures.Add("proxy" + (i + 1) + "_visual_hidden");
                    if (!visual.gameObject.activeInHierarchy) failures.Add("proxy" + (i + 1) + "_visual_inactive");
                    if (visual.GetComponentsInChildren<Stats>(true).Any(x => x != null && x.enabled)) failures.Add("proxy" + (i + 1) + "_visual_stats_active");
                    Animator shellAnimator = visual.GetComponentInChildren<Animator>(true);
                    if (shellAnimator == null || !shellAnimator.enabled) failures.Add("proxy" + (i + 1) + "_animator");
                    else if (shellAnimator.runtimeAnimatorController == null) failures.Add("proxy" + (i + 1) + "_animator_controller");
                    Animator boundAnimator;
                    if (!VisualAnimators.TryGetValue(go.GetInstanceID(), out boundAnimator) || boundAnimator != shellAnimator)
                        failures.Add("proxy" + (i + 1) + "_animator_unbound");
                }
                PvpOpponentProfile profile = i < TeamProfiles.Count ? TeamProfiles[i] : null;
                if (profile != null && !EquipmentVisualsApplied.Contains(go.GetInstanceID()))
                    failures.Add("proxy" + (i + 1) + "_equipment");
                if (profile != null && !ClassLoadoutsApplied.Contains(go.GetInstanceID())) failures.Add("proxy" + (i + 1) + "_loadout");
                CastSpell caster = go.GetComponent<CastSpell>();
                int expectedSpells;
                if (EligibleCombatSpellCounts.TryGetValue(go.GetInstanceID(), out expectedSpells) && expectedSpells > 0 &&
                    (caster == null || caster.KnownSpells == null || caster.KnownSpells.Count == 0))
                    failures.Add("proxy" + (i + 1) + "_spells");
            }
            return failures.Count == 0 ? "[Erenshor PvP] VERIFY PASS proxies=" + TeamClones.Count + "; visuals=visible; profiles=matched." :
                "[Erenshor PvP] VERIFY FAIL " + string.Join(",", failures.ToArray()) + ".";
        }

        internal static void DespawnAfterFight(string reason, Character winner)
        {
            // Containment can observe several terminal native callbacks in the same frame. Once
            // the owned proxy collections are empty and the primary clone is gone, cleanup has
            // already completed and must not publish/log a second terminal cleanup.
            if (TeamClones.Count == 0 && _clone == null) return;
            // Keep this terminal path independently safe for callers outside containment.Tick.
            // End is idempotent and clears native aggro, defender-pet targets, and navigation.
            PvpCombatContainment.End(reason);
            GameObject clone = _clone;
            string opponent = _activeProfile == null ? "PvP Proxy" : _activeProfile.Name;
            List<PvpOpponentProfile> completedProfiles = new List<PvpOpponentProfile>(TeamProfiles.Where(x => x != null));
            string completedMatchId = _activeMatchId;
            PvpEncounterMode completedMode = _activeMode; string completedMotive = _activeMotive;
            int proxyCount = TeamClones.Count;
            int animated = VisualAnimators.Count(x => x.Value != null);
            int equipped = EquipmentVisualsApplied.Count;
            int loadouts = ClassLoadoutsApplied.Count;
            int spellTotal = EligibleCombatSpellCounts.Values.Sum();
            PvpDiagnostics.Log("validation_summary match=" + ShortMatch(completedMatchId) + "; outcome=" + (reason ?? "unknown") +
                "; profiles=" + completedProfiles.Count + "; proxies=" + proxyCount + "; animated=" + animated +
                "; equipped=" + equipped + "; loadouts=" + loadouts + "; spells=" + spellTotal);
            _clone = null; _activeProfile = null; _despawnAt = 0f; _activeMatchId = string.Empty; _activeMode = PvpEncounterMode.Arranged; _activeMotive = string.Empty;
            if (TeamClones.Count == 0 && clone == null) return;
            foreach (GameObject member in TeamClones)
            {
                if (member == null) continue;
                TemporaryActorIds.Remove(member.GetInstanceID());
                VisualAnimators.Remove(member.GetInstanceID());
                EquipmentVisualsApplied.Remove(member.GetInstanceID());
                ClassLoadoutsApplied.Remove(member.GetInstanceID());
                EligibleCombatSpellCounts.Remove(member.GetInstanceID());
                EligibleHealSpellCounts.Remove(member.GetInstanceID());
                AttackDecisionCounts.Remove(member.GetInstanceID());
                AttackSkillDecisionLogged.Remove(member.GetInstanceID());
                AttackSpellDecisionLogged.Remove(member.GetInstanceID());
                HealCheckCounts.Remove(member.GetInstanceID());
                SpellStartCounts.Remove(member.GetInstanceID());
                NativeStartCompleted.Remove(member.GetInstanceID());
                NativeNavProbes.Remove(member.GetInstanceID());
                NativeUpdateReached.Remove(member.GetInstanceID());
                CombatSectionReached.Remove(member.GetInstanceID());
                LegalTargetAcquired.Remove(member.GetInstanceID());
                NavPursuitRequested.Remove(member.GetInstanceID());
                MeleeAttemptCounts.Remove(member.GetInstanceID());
                NativeUpdateExceptionLogged.Remove(member.GetInstanceID());
                try { UnityEngine.Object.Destroy(member); } catch { }
            }
            TeamClones.Clear(); TeamProfiles.Clear(); VisualAnimators.Clear(); EquipmentVisualsApplied.Clear(); ClassLoadoutsApplied.Clear(); EligibleCombatSpellCounts.Clear(); EligibleHealSpellCounts.Clear(); AttackDecisionCounts.Clear(); AttackSkillDecisionLogged.Clear(); AttackSpellDecisionLogged.Clear(); HealCheckCounts.Clear(); SpellStartCounts.Clear(); LastSpellStartFrame.Clear(); NativeStartCompleted.Clear(); NativeNavProbes.Clear(); NativeUpdateReached.Clear(); CombatSectionReached.Clear(); LegalTargetAcquired.Clear(); NavPursuitRequested.Clear(); MeleeAttemptCounts.Clear(); NativeUpdateExceptionLogged.Clear(); DestroyTemporarySpells();
            PvpDiagnostics.Log("validation_cleanup match=" + ShortMatch(completedMatchId) +
                "; proxy_collections=" + TeamClones.Count + "; profile_collections=" + TeamProfiles.Count +
                "; spell_collections=" + TemporarySpells.Count + "; destroy_scheduled=" + proxyCount);
            string completedClassification = ErenshorPvpApi.ClassifyOutcome(reason);
            bool competitiveResult = PvpCombatStartupPolicy.ShouldRecordCompetitiveResult(reason);
            if (competitiveResult)
            {
                if (ErenshorPvpApi.TryRecordResult(completedMatchId, opponent, reason ?? "unknown", completedMode.ToString().ToLowerInvariant(), completedClassification))
                    ErenshorPvpEvents.Publish(new PvpSemanticEvent("pvp_match_completed", completedMatchId, opponent,
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, completedMode.ToString().ToLowerInvariant(), reason ?? "unknown", completedClassification));
                PvpRecordService.Complete(opponent, reason ?? "unknown", completedMode);
            }
            else
            {
                // Technical startup failure is operational evidence, not a PvP result. Publish a
                // transient cancellation event for integrations but deliberately do not append it to
                // RecentResults/PvpRecordService or grant any reward/stat/history credit.
                ErenshorPvpEvents.Publish(new PvpSemanticEvent("pvp_cancelled", completedMatchId, opponent,
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, completedMode.ToString().ToLowerInvariant(),
                    reason ?? "unknown", "invalid"));
                PvpDiagnostics.Log("technical_failure_result match=" + ShortMatch(completedMatchId) +
                    "; winner=none; xp=0; gold=0; win_credit=false; history_credit=false");
                winner = null;
            }
            Debug.Log("[Erenshor PvP] lethal_result=" + (reason ?? "unknown") + "; mode=" + completedMode.ToString().ToLowerInvariant() + "; motive=" + completedMotive + "; winner=" + (winner == null ? "none" : "player"));
            try
            {
                if (string.Equals(reason, "proxy_death", StringComparison.Ordinal)) UpdateSocialLog.LogAdd("[PvP] " + opponent + (completedMode == PvpEncounterMode.Ambush ? ": you held your ground. We're done here." : ": gf, well played."), "lightblue");
                else if (string.Equals(reason, "player_death", StringComparison.Ordinal)) UpdateSocialLog.LogAdd("[PvP] " + opponent + (completedMode == PvpEncounterMode.Ambush && completedMotive == "camp_claim" ? ": camp's ours now." : ": gf. See you out there."), "lightblue");
                else if (string.Equals(reason, "retreat", StringComparison.Ordinal)) UpdateSocialLog.LogAdd("[PvP] " + opponent + " disengages and escapes.", "lightblue");
                else if (string.Equals(reason, "player_fled", StringComparison.Ordinal)) UpdateSocialLog.LogAdd("[PvP] You disengage from " + opponent + " and escape.", "lightblue");
            }
            catch { }
            if (PvpCombatStartupPolicy.CanGrantVictoryReward(reason, winner != null))
            {
                string result = PvpRewardService.GrantVictory(completedMatchId, winner, completedProfiles);
                PvpDiagnostics.Log("reward_result match=" + ShortMatch(completedMatchId) + "; " + result.Replace("[Erenshor PvP] ", string.Empty));
                try { UpdateSocialLog.LogAdd(result, "lightblue"); } catch { }
            }
            PvpController.EncounterCleaned();
        }

        private static string ShortMatch(string matchId)
        {
            return string.IsNullOrEmpty(matchId) ? "none" : matchId.Substring(0, Math.Min(8, matchId.Length));
        }

        private static bool TryReadIntField(object instance, string name, out int value)
        {
            value = 0;
            try
            {
                FieldInfo field = instance == null ? null : instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return false;
                object raw = field.GetValue(instance);
                if (raw is int) { value = (int)raw; return true; }
                return false;
            }
            catch { return false; }
        }

        private static bool TryReadField(object instance, string name, out object value)
        {
            value = null;
            try
            {
                FieldInfo field = instance == null ? null : instance.GetType().GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return false;
                value = field.GetValue(instance);
                return true;
            }
            catch { return false; }
        }

        private static void TrySetField(object instance, string name, object value)
        {
            try
            {
                FieldInfo field = instance == null ? null : instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(instance, value);
            }
            catch { }
        }

        private static GameObject FindNativeMobTemplate()
        {
            try
            {
                foreach (NPC npc in UnityEngine.Object.FindObjectsOfType<NPC>())
                {
                    if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy) continue;
                    if (!IsEligibleCombatTemplate(npc)) continue;
                    Character actor = npc.GetComponent<Character>();
                    if (actor != null && actor.MyStats != null && actor.Alive)
                    {
                        _templateSource = "live:" + npc.gameObject.name;
                        PvpDiagnostics.Log("template_runtime_state template=" + _templateSource + "; " + DescribeNativeMaintenanceState(npc));
                        return npc.gameObject;
                    }
                }
            }
            catch { }
            // A zone does not need to contain a currently spawned creature. Unity keeps loaded
            // prefab assets available through Resources; cloning one avoids mutating any scene NPC.
            try
            {
                foreach (NPC npc in Resources.FindObjectsOfTypeAll<NPC>())
                {
                    if (npc == null || npc.gameObject == null || !IsEligibleCombatTemplate(npc)) continue;
                    if (npc.gameObject.scene.IsValid()) continue;
                    Character actor = npc.GetComponent<Character>();
                    if (actor == null || actor.MyStats == null || npc.GetComponent<NavMeshAgent>() == null) continue;
                    string name = npc.gameObject.name ?? string.Empty;
                    if (name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("raid", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    _templateSource = "resource:" + name;
                    return npc.gameObject;
                }
            }
            catch { }
            _templateSource = "unavailable";
            return null;
        }

        private static string DescribeNativeMaintenanceState(NPC npc)
        {
            object value;
            bool self = TryReadField(npc, "Myself", out value) && value != null;
            bool stats = TryReadField(npc, "MyStats", out value) && value != null;
            bool nav = TryReadField(npc, "MyNav", out value) && value != null;
            bool caster = TryReadField(npc, "MySpells", out value) && value != null;
            bool flash = TryReadField(npc, "NameFlash", out value) && value != null;
            bool raid = TryReadField(npc, "MyRaidSlot", out value) && value != null;
            // namePlateTxt is the field NPC.HandleNameTag actually dereferences. It is reported
            // separately from nameFlash because a template can satisfy one and not the other, which is
            // exactly how the broken 5v5 logged nameFlash=True while every proxy still threw.
            bool plateText = TryReadField(npc, "NamePlateTxt", out value) && value != null;
            bool plateObject = TryReadField(npc, "NamePlateObject", out value) && value != null;
            return "npcMyself=" + self + "; npcMyStats=" + stats + "; npcMyNav=" + nav +
                "; npcMySpells=" + caster + "; nameFlash=" + flash + "; raidSlot=" + raid +
                "; namePlateTxt=" + plateText + "; namePlateObject=" + plateObject;
        }

        // Companion bodies have very different scale, animation, and stat lifecycles from normal
        // hostile NPCs. In particular, borrowing a player's pet produced oversized inactive PvP
        // shells. Template selection must only use ordinary native combat NPCs.
        private static bool IsEligibleCombatTemplate(NPC npc)
        {
            if (npc == null || npc.SimPlayer || npc.ThisSim != null || IsTemporaryNpc(npc)) return false;
            if (npc.SummonedByPlayer) return false;
            string name = (npc.NPCName ?? string.Empty) + " " + (npc.gameObject == null ? string.Empty : npc.gameObject.name ?? string.Empty);
            return name.IndexOf("pet", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("companion", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("familiar", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("minion", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("summon", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void ApplyProfileLoadout(NPC npc, PvpOpponentProfile profile)
        {
            if (npc == null) return;
            // A borrowed body must never retain its source creature's spell/skill identity.
            npc.MyBuffSpells = npc.MyBuffSpells ?? new List<Spell>(); npc.MyBuffSpells.Clear();
            npc.MyAttackSpells = npc.MyAttackSpells ?? new List<Spell>(); npc.MyAttackSpells.Clear();
            npc.MyHealSpells = npc.MyHealSpells ?? new List<Spell>(); npc.MyHealSpells.Clear();
            npc.MyCCSpells = npc.MyCCSpells ?? new List<Spell>(); npc.MyCCSpells.Clear();
            npc.MyTauntSpell = npc.MyTauntSpell ?? new List<Spell>(); npc.MyTauntSpell.Clear();
            npc.GroupHeals = npc.GroupHeals ?? new List<Spell>(); npc.GroupHeals.Clear();
            npc.MyAttackSkills = npc.MyAttackSkills ?? new List<Skill>(); npc.MyAttackSkills.Clear();
            npc.MyPetSpell = null; npc.MyHOTSpell = null; npc.GroupHOTSpell = null;
            npc.MyEmitVitaeSpell = null; npc.AETaunt = null; npc.NPCProcOnHit = null;
            npc.NoSelfHeal = false;
            npc.GuildName = profile == null ? string.Empty : profile.GuildId;
            int level = profile == null ? 1 : Math.Max(1, profile.Level);
            // The previous level..2x-level range became trivial chip damage after player armor.
            // An equal-level opponent should remain dangerous even when its spell AI pauses.
            npc.BaseAtkDmg = Math.Max(3, level * 3);
            npc.MinAtkDmg = Math.Max(2, level * 2);
            npc.DamageRange = new Vector2(Math.Max(2, level * 2), Math.Max(4, level * 4));
            ApplyClassSpells(npc, profile);
        }

        private static void AttachSimVisualShell(GameObject combatRoot, PvpOpponentProfile profile)
        {
            if (combatRoot == null || profile == null || GameData.SimMngr == null) return;
            try
            {
                // Hide the borrowed creature body before adding the Sim visual child.
                foreach (Renderer renderer in combatRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

                _suppressPersistentLoad = true;
                GameObject visual;
                GameObject visualTemplate = FindSimVisualTemplate(profile);
                if (visualTemplate == null) { _suppressPersistentLoad = false; return; }
                bool namedTemplate = visualTemplate != GameData.SimMngr.BlankSPTemplate;
                try { visual = UnityEngine.Object.Instantiate(visualTemplate, combatRoot.transform.position, combatRoot.transform.rotation); }
                finally { _suppressPersistentLoad = false; }
                if (visual == null) return;
                visual.name = "PvP_SimVisual_" + profile.Name;
                visual.transform.SetParent(combatRoot.transform, true);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (NPC component in visual.GetComponentsInChildren<NPC>(true)) component.enabled = false;
                foreach (Character component in visual.GetComponentsInChildren<Character>(true)) component.enabled = false;
                // The visual clone is not a real actor. Leaving its Stats MonoBehaviour running
                // calls CheckAuras against missing Sim state every frame and destabilizes combat.
                foreach (Stats component in visual.GetComponentsInChildren<Stats>(true)) component.enabled = false;
                foreach (SimPlayer component in visual.GetComponentsInChildren<SimPlayer>(true)) component.enabled = false;
                foreach (NavMeshAgent component in visual.GetComponentsInChildren<NavMeshAgent>(true)) component.enabled = false;
                foreach (CastSpell component in visual.GetComponentsInChildren<CastSpell>(true)) component.enabled = false;
                foreach (LootTable component in visual.GetComponentsInChildren<LootTable>(true)) component.enabled = false;
                foreach (SimPlayerLanguage component in visual.GetComponentsInChildren<SimPlayerLanguage>(true)) component.enabled = false;
                // Off-map and blank Sim templates may be pooled inactive. All gameplay-bearing
                // components are disabled above, so activating the render shell is now safe and
                // is required for its Animator to evaluate.
                visual.SetActive(true);
                // A named template may have had its Animator disabled while it was pooled or
                // inactive. The shell is render-only, so its Animator is safe to restore.
                foreach (Animator component in visual.GetComponentsInChildren<Animator>(true)) component.enabled = true;

                // Erenshor's NPC movement, attack, casting, hit, and death paths all write to
                // Character.MyAnim. Redirect those native animation commands from the hidden
                // borrowed creature to the visible Sim shell.
                Animator visualAnimator = visual.GetComponentInChildren<Animator>(true);
                Character combatActor = combatRoot.GetComponent<Character>();
                if (visualAnimator != null && combatActor != null)
                {
                    visualAnimator.enabled = true;
                    combatActor.AssignAnim(visualAnimator);
                    VisualAnimators[combatRoot.GetInstanceID()] = visualAnimator;
                    PvpDiagnostics.Log("visual_animator_bound profile=" + profile.Name + "; animator=" + visualAnimator.name);
                }
                else Debug.LogWarning("[Erenshor PvP] visual_animator_missing profile=" + profile.Name);

                ModularParts parts = FindPrimaryModularParts(visual);
                if (parts != null)
                {
                    InitializeVisualSim(parts, profile);
                    parts.Gender = profile.Gender;
                    parts.HairName = profile.HairName;
                    parts.HairCol = profile.HairColor;
                    parts.SkinCol = profile.SkinColor;
                    try { parts.DoSkinColor(); } catch { }
                    try { parts.UpdateHair(profile.HairName, profile.HairColor); } catch { }
                    if (ApplyEquipmentVisuals(parts, profile)) EquipmentVisualsApplied.Add(combatRoot.GetInstanceID());
                }
                PvpDiagnostics.Log("visual_shell profile=" + profile.Describe() + "; source=" + (namedTemplate ? "named_sim_template" : "blank_sim_template") + "; equipment_ids=" + profile.EquippedItemIds.Count);
            }
            catch (Exception ex)
            {
                _suppressPersistentLoad = false;
                Debug.LogWarning("[Erenshor PvP] visual_shell_failed=" + ex.GetType().Name);
            }
        }

        private static ModularParts FindPrimaryModularParts(GameObject visual)
        {
            if (visual == null) return null;
            // Prefer the native Sim's serialized renderer reference over a hierarchy guess.
            SimPlayer visualSim = visual.GetComponentInChildren<SimPlayer>(true);
            if (visualSim != null && visualSim.Mods != null) return visualSim.Mods;
            ModularParts fallback = null;
            foreach (ModularParts candidate in visual.GetComponentsInChildren<ModularParts>(true))
            {
                if (candidate == null) continue;
                if (fallback == null) fallback = candidate;
                Transform parent = candidate.transform.parent;
                if (parent != null && parent.GetComponent<SimPlayer>() != null) return candidate;
            }
            return fallback;
        }

        // UpdateSimPlayerVisuals reads appearance/cosmetic fields from the ModularParts object's
        // immediate SimPlayer parent. Persistent Sim loading is intentionally suppressed for PvP,
        // so initialize only this temporary visual copy with safe non-null values.
        private static void InitializeVisualSim(ModularParts parts, PvpOpponentProfile profile)
        {
            if (parts == null || profile == null || parts.transform.parent == null) return;
            SimPlayer sim = parts.transform.parent.GetComponent<SimPlayer>();
            if (sim == null) return;
            int count = GameData.SimMngr == null || GameData.SimMngr.Sims == null ? 0 : GameData.SimMngr.Sims.Count;
            sim.myIndex = count <= 0 ? 0 : Math.Max(0, Math.Min(count - 1, profile.SimIndex));
            sim.HairName = profile.HairName;
            sim.HairColor = profile.HairColorIndex;
            sim.SkinColor = profile.SkinColorIndex;
            if (sim.SimCosHead == null) sim.SimCosHead = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosChest == null) sim.SimCosChest = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosBack == null) sim.SimCosBack = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosArm == null) sim.SimCosArm = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosFoot == null) sim.SimCosFoot = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosWrist == null) sim.SimCosWrist = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosLeg == null) sim.SimCosLeg = new ItemSaveData(string.Empty, 1);
            if (sim.SimCosHand == null) sim.SimCosHand = new ItemSaveData(string.Empty, 1);
        }

        private static bool ApplyEquipmentVisuals(ModularParts parts, PvpOpponentProfile profile)
        {
            if (parts == null || profile == null || GameData.ItemDB == null) return false;
            try
            {
                List<SimInvSlot> armor = new List<SimInvSlot>();
                // SpawnWeapons dereferences both hands. Native callers supply explicit Empty
                // slots for unused hands rather than null.
                SimInvSlot main = new SimInvSlot(Item.SlotType.Primary) { MyItem = GameData.PlayerInv.Empty, Quant = 1 };
                SimInvSlot off = new SimInvSlot(Item.SlotType.Secondary) { MyItem = GameData.PlayerInv.Empty, Quant = 1 };
                int valid = 0;
                bool fallback = false;
                foreach (string id in profile.EquippedItemIds)
                {
                    Item item = string.IsNullOrWhiteSpace(id) ? null : GameData.ItemDB.GetItemByID(id);
                    if (item == null) continue;
                    valid++;
                    SimInvSlot slot = new SimInvSlot(item.RequiredSlot) { MyItem = item, Quant = 1 };
                    if (item.RequiredSlot == Item.SlotType.Primary) main = slot;
                    else if (item.RequiredSlot == Item.SlotType.Secondary) off = slot;
                    else armor.Add(slot);
                }
                if (valid == 0)
                {
                    fallback = true;
                    List<Item> generated = PvpFallbackEquipment.Build(profile, ClassFor(profile.ClassName));
                    for (int i = 0; i < generated.Count; i++)
                    {
                        Item item = generated[i];
                        if (item == null) continue;
                        valid++;
                        SimInvSlot slot = new SimInvSlot(item.RequiredSlot) { MyItem = item, Quant = 1 };
                        if (item.RequiredSlot == Item.SlotType.Primary) main = slot;
                        else if (item.RequiredSlot == Item.SlotType.Secondary) off = slot;
                        else if (item.RequiredSlot == Item.SlotType.PrimaryOrSecondary)
                        {
                            if (main.MyItem == GameData.PlayerInv.Empty) main = slot;
                            else off = slot;
                        }
                        else armor.Add(slot);
                    }
                }
                parts.UpdateSimPlayerVisuals(armor, main, off);
                PvpDiagnostics.Log("equipment_visual_applied profile=" + profile.Name + "; valid_items=" + valid +
                    "; source=" + (fallback ? "level_class_fallback" : "saved_profile"));
                return valid > 0;
            }
            catch (Exception ex) { Debug.LogWarning("[Erenshor PvP] equipment_visual_failed=" + ex.GetType().Name + "; profile=" + profile.Name + "; message=" + ex.Message); return false; }
        }

        private static void ApplyClassSpells(NPC npc, PvpOpponentProfile profile)
        {
            if (npc == null || profile == null || GameData.SpellDatabase == null || GameData.SpellDatabase.SpellDatabase == null) return;
            try
            {
                Class profileClass = ClassFor(profile.ClassName);
                if (profileClass == null) return;
                HashSet<string> acquired = new HashSet<string>(profile.AcquiredSpellIds.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
                HashSet<string> admitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int categorized = 0;
                foreach (Spell source in GameData.SpellDatabase.SpellDatabase)
                {
                    if (source == null || !source.SimUsable || source.RequiredLevel > profile.Level || source.PetToSummon != null || source.Type == Spell.SpellType.Pet) continue;
                    if (source.UsedBy == null || !source.UsedBy.Contains(profileClass)) continue;
                    if (source.SimsNeedHelpToLearn && !acquired.Contains(source.Id) && !acquired.Contains(source.SpellName)) continue;
                    string key = string.IsNullOrEmpty(source.Id) ? source.SpellName : source.Id;
                    if (string.IsNullOrEmpty(key) || !admitted.Add(key)) continue;
                    Spell spell = UnityEngine.Object.Instantiate(source); spell.CanHitPlayers = true; TemporarySpells.Add(spell);
                    if (spell.Type == Spell.SpellType.Heal || spell.TargetHealing > 0 || spell.CasterHealing > 0)
                    {
                        // Preserve native healer behavior, including self-heals and allied heals.
                        // PvP containment rejects cross-team/outside spell healing instead of
                        // globally disabling the NPC's self-heal decision path.
                        npc.MyHealSpells.Add(spell);
                        categorized++;
                    }
                    else if (spell.CrowdControlSpell || spell.RootTarget || spell.StunTarget || spell.FearTarget) { npc.MyCCSpells.Add(spell); categorized++; }
                    else if (spell.Type == Spell.SpellType.Beneficial || spell.SelfOnly || spell.ApplyToCaster || spell.PercentManaRestoration > 0) { npc.MyBuffSpells.Add(spell); categorized++; }
                    else if (spell.Type == Spell.SpellType.Damage || spell.Type == Spell.SpellType.StatusEffect ||
                        spell.Type == Spell.SpellType.AE || spell.Type == Spell.SpellType.PBAE || spell.TargetDamage > 0)
                    { npc.MyAttackSpells.Add(spell); categorized++; }
                }
                EligibleCombatSpellCounts[npc.gameObject.GetInstanceID()] = categorized;
                EligibleHealSpellCounts[npc.gameObject.GetInstanceID()] = npc.MyHealSpells.Count;
                CastSpell caster = npc.GetComponent<CastSpell>();
                if (caster != null)
                {
                    if (caster.KnownSpells == null) caster.KnownSpells = new List<Spell>();
                    caster.KnownSpells.Clear();
                    caster.KnownSpells.AddRange(npc.MyAttackSpells);
                    caster.KnownSpells.AddRange(npc.MyHealSpells);
                    caster.KnownSpells.AddRange(npc.MyCCSpells);
                    caster.KnownSpells.AddRange(npc.MyBuffSpells);
                }
                if (caster == null) throw new InvalidOperationException("Proxy template has no CastSpell component.");
                ClassLoadoutsApplied.Add(npc.gameObject.GetInstanceID());
                PvpDiagnostics.Log("class_loadout profile=" + profile.Name + "; attack=" + npc.MyAttackSpells.Count +
                    "; heal=" + npc.MyHealSpells.Count + "; cc=" + npc.MyCCSpells.Count + "; buff=" + npc.MyBuffSpells.Count);
            }
            catch (Exception ex) { Debug.LogWarning("[Erenshor PvP] class_loadout_failed=" + ex.GetType().Name); }
        }

        private static Spell FindSpell(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName) || GameData.SpellDatabase == null) return null;
            try
            {
                Spell exact = GameData.SpellDatabase.GetSpellByID(idOrName);
                if (exact != null) return exact;
            }
            catch { }
            try
            {
                if (GameData.SpellDatabase.SpellDatabase == null) return null;
                return GameData.SpellDatabase.SpellDatabase.FirstOrDefault(x => x != null &&
                    (string.Equals(x.Id, idOrName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.SpellName, idOrName, StringComparison.OrdinalIgnoreCase)));
            }
            catch { return null; }
        }

        private static void DestroyTemporarySpells()
        {
            foreach (Spell spell in TemporarySpells) try { if (spell != null) UnityEngine.Object.Destroy(spell); } catch { }
            TemporarySpells.Clear();
        }

        private static GameObject FindSimVisualTemplate(PvpOpponentProfile profile)
        {
            try
            {
                if (GameData.SimMngr.ActualSims != null)
                {
                    foreach (GameObject candidate in GameData.SimMngr.ActualSims)
                    {
                        if (candidate != null && string.Equals(candidate.name, profile.Name, StringComparison.OrdinalIgnoreCase)) return candidate;
                    }
                }
            }
            catch { }
            return GameData.SimMngr.BlankSPTemplate;
        }

        // BlankSPTemplate is a visual/template object, not an encounter-ready character and
        // arrives with 1/1 HP. Copy only live numeric combat values from the local player; no
        // inventory, spells, persistent Sim state, or save-backed identity crosses this boundary.
        private static void ConfigureCombatStats(Character player, Character proxy, PvpOpponentProfile profile)
        {
            try
            {
                if (player == null || proxy == null || player.MyStats == null || proxy.MyStats == null) return;
                Stats source = player.MyStats;
                Stats target = proxy.MyStats;
                target.Level = profile == null ? Math.Max(1, source.Level) : profile.Level;
                target.CharacterClass = ClassFor(profile == null ? null : profile.ClassName) ?? source.CharacterClass;
                float levelScale = profile == null || source.Level <= 0 ? 1f : Mathf.Clamp((float)profile.Level / source.Level, .65f, 1.5f);
                PvpCombatRole role = PvpTeamMember.RoleFor(profile == null ? null : profile.ClassName);
                float healthRole = role == PvpCombatRole.Vanguard ? 1.20f : role == PvpCombatRole.Support ? 1.05f : .92f;
                float armorRole = role == PvpCombatRole.Vanguard ? 1.18f : role == PvpCombatRole.Striker ? .92f : .85f;
                target.BaseHP = Math.Max(1, Mathf.RoundToInt(source.BaseHP * levelScale * healthRole));
                target.BaseAC = Math.Max(0, Mathf.RoundToInt(source.BaseAC * levelScale * armorRole));
                target.BaseMana = Math.Max(0, source.BaseMana);
                target.BaseStr = Math.Max(1, source.BaseStr); target.BaseEnd = Math.Max(1, source.BaseEnd);
                target.BaseDex = Math.Max(1, source.BaseDex); target.BaseAgi = Math.Max(1, source.BaseAgi);
                target.BaseInt = Math.Max(1, source.BaseInt); target.BaseWis = Math.Max(1, source.BaseWis); target.BaseCha = Math.Max(1, source.BaseCha);
                target.CurrentMaxHP = Math.Max(1, Mathf.RoundToInt(source.CurrentMaxHP * levelScale * healthRole)); target.CurrentHP = target.CurrentMaxHP;
                target.CurrentAC = Math.Max(0, Mathf.RoundToInt(source.CurrentAC * levelScale * armorRole));
                target.CurrentMana = Math.Max(0, Mathf.RoundToInt(source.CurrentMana * levelScale));
                target.StopAllRegen = true;
                target.BaseMHAtkDelay = Math.Max(.1f, source.BaseMHAtkDelay);
                target.CurrentMHAtkDelay = Math.Max(.1f, source.CurrentMHAtkDelay);
                target.BaseOHAtkDelay = Math.Max(.1f, source.BaseOHAtkDelay);
                target.CurrentOHAtkDelay = Math.Max(.1f, source.CurrentOHAtkDelay);
            }
            catch { }
        }

        private static Class ClassFor(string className)
        {
            try
            {
                if (GameData.ClassDB == null) return null;
                string value = (className ?? string.Empty).ToLowerInvariant();
                if (value.Contains("arcan")) return GameData.ClassDB.Arcanist;
                if (value.Contains("paladin")) return GameData.ClassDB.Paladin;
                if (value.Contains("duel") || value.Contains("windblade")) return GameData.ClassDB.Duelist;
                if (value.Contains("druid")) return GameData.ClassDB.Druid;
                if (value.Contains("storm")) return GameData.ClassDB.Stormcaller;
                if (value.Contains("reaver")) return GameData.ClassDB.Reaver;
            }
            catch { }
            return null;
        }
    }

    // SimPlayer.Awake calls LoadAllSimData immediately. During the one-frame clone construction
    // window, suppress that method so it cannot read/write a persistent roster entry by myIndex.
    [HarmonyPatch(typeof(SimPlayer), "LoadAllSimData")]
    internal static class PvpTemporaryCloneLoadPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() { return !PvpTemporaryCloneFactory.SuppressPersistentLoad; }
    }

    // 0.5.9 forensic recovery: the cloned NPC must receive its own native Start lifecycle. The
    // Prefix scopes diagnostics to registered PvP roots; the Postfix immediately reasserts PvP-owned
    // identity/loadout/reward state. Vanilla NPCs are untouched.
    [HarmonyPatch(typeof(NPC), "Start")]
    internal static class PvpTemporaryNpcStartPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NPC __instance, out bool __state)
        {
            return PvpTemporaryCloneFactory.AllowNativeNpcStart(__instance, out __state);
        }

        [HarmonyPostfix]
        private static void Postfix(NPC __instance, bool __state)
        {
            PvpTemporaryCloneFactory.ObserveNativeNpcStartCompleted(__instance, __state);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(NPC __instance, bool __state, Exception __exception)
        {
            if (__state && __exception != null && PvpTemporaryCloneFactory.IsTemporaryNpc(__instance))
            {
                PvpDiagnostics.Log("proxy_native_start action=native; completion=failed; proxy=" +
                    (__instance == null ? "null" : __instance.gameObject.name) + "; error=" + __exception.GetType().Name);
                PvpTemporaryCloneFactory.ObserveNativeNpcStartFailed(__instance, __exception);
            }
            // Diagnostics only: return the original exception unchanged. Never suppress a native
            // NPC.Start failure for a proxy or ordinary game NPC.
            return __exception;
        }
    }

    // The native maintenance method is never changed for vanilla NPCs. It only prevents an
    // already-identified invalid temporary proxy from throwing every frame while the factory
    // closes and destroys that match on its next Tick.
    [HarmonyPatch(typeof(NPC), "HandleMaintenaceAndCounters")]
    internal static class PvpTemporaryNpcMaintenancePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NPC __instance)
        {
            return PvpTemporaryCloneFactory.AllowNativeMaintenance(__instance);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(NPC __instance, Exception __exception)
        {
            if (__exception != null && PvpTemporaryCloneFactory.IsTemporaryNpc(__instance))
                PvpDiagnostics.Warning("native_npc_exception method=NPC.HandleMaintenaceAndCounters; proxy=" +
                    (__instance == null || __instance.gameObject == null ? "null" : __instance.gameObject.name) +
                    "; error=" + __exception.GetType().Name);
            // Diagnostic only. Never suppress an exception that escaped the proven temporary-proxy
            // maintenance precondition; live acceptance must prove the current native path stays clean.
            return __exception;
        }
    }

    [HarmonyPatch(typeof(NPC), "UpdateNav")]
    internal static class PvpNativeUpdateNavHealthPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance)
        { PvpTemporaryCloneFactory.ObserveNativeNavEntered(__instance); }

        [HarmonyPostfix]
        private static void Postfix(NPC __instance)
        { PvpTemporaryCloneFactory.ObserveNativeNavCompleted(__instance); }

        [HarmonyFinalizer]
        private static Exception Finalizer(NPC __instance, Exception __exception)
        { return PvpTemporaryCloneFactory.ObserveNativeNavException(__instance, __exception); }
    }

    [HarmonyPatch(typeof(NPC), "Update")]
    internal static class PvpNativeUpdateTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance)
        { PvpTemporaryCloneFactory.ObserveNativeUpdate(__instance); }

        [HarmonyPostfix]
        private static void Postfix(NPC __instance)
        { PvpTemporaryCloneFactory.ObserveNativeUpdateCompleted(__instance); }

        [HarmonyFinalizer]
        private static Exception Finalizer(NPC __instance, Exception __exception)
        { return PvpTemporaryCloneFactory.ObserveNativeUpdateException(__instance, __exception); }
    }

    [HarmonyPatch(typeof(NPC), "Combat")]
    internal static class PvpCombatSectionTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance) { PvpTemporaryCloneFactory.ObserveCombatSection(__instance); }
    }

    [HarmonyPatch(typeof(NPC), "PerformMeleeHit")]
    internal static class PvpMeleeAttemptTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance) { PvpTemporaryCloneFactory.ObserveMeleeAttempt(__instance); }
    }

    [HarmonyPatch(typeof(NPC), "DoAttackSkill")]
    internal static class PvpAttackSkillDecisionTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance) { PvpTemporaryCloneFactory.ObserveAttackDecision(__instance, false); }
    }

    [HarmonyPatch(typeof(NPC), "DoAttackSpell")]
    internal static class PvpAttackDecisionTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance) { PvpTemporaryCloneFactory.ObserveAttackDecision(__instance, true); }
    }

    [HarmonyPatch(typeof(NPC), "CheckHeals")]
    internal static class PvpHealCheckTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance) { PvpTemporaryCloneFactory.ObserveHealCheck(__instance); }
    }

    [HarmonyPatch(typeof(NPC), "CheckHealsRaid")]
    internal static class PvpHealRaidCheckTelemetryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NPC __instance) { PvpTemporaryCloneFactory.ObserveHealCheck(__instance); }
    }

    [HarmonyPatch(typeof(CastSpell), "StartSpell", new Type[] { typeof(Spell), typeof(Stats) })]
    internal static class PvpSpellStartTelemetry2Patch
    { [HarmonyPrefix] private static bool Prefix(CastSpell __instance, Spell __0, Stats __1) { return PvpTemporaryCloneFactory.ObserveAndAllowSpellStart(__instance, __0, __1); } }
    [HarmonyPatch(typeof(CastSpell), "StartSpell", new Type[] { typeof(Spell), typeof(Stats), typeof(float) })]
    internal static class PvpSpellStartTelemetry3Patch
    { [HarmonyPrefix] private static bool Prefix(CastSpell __instance, Spell __0, Stats __1) { return PvpTemporaryCloneFactory.ObserveAndAllowSpellStart(__instance, __0, __1); } }
    [HarmonyPatch(typeof(CastSpell), "StartSpell", new Type[] { typeof(Spell), typeof(Stats), typeof(float), typeof(bool) })]
    internal static class PvpSpellStartTelemetry4Patch
    { [HarmonyPrefix] private static bool Prefix(CastSpell __instance, Spell __0, Stats __1) { return PvpTemporaryCloneFactory.ObserveAndAllowSpellStart(__instance, __0, __1); } }
    [HarmonyPatch(typeof(CastSpell), "StartSpell", new Type[] { typeof(Spell), typeof(Stats), typeof(float), typeof(bool), typeof(float) })]
    internal static class PvpSpellStartTelemetry5Patch
    { [HarmonyPrefix] private static bool Prefix(CastSpell __instance, Spell __0, Stats __1) { return PvpTemporaryCloneFactory.ObserveAndAllowSpellStart(__instance, __0, __1); } }

    [HarmonyPatch(typeof(CastSpell), "StartSpellFromProc", new Type[] { typeof(Spell), typeof(Stats), typeof(float), typeof(bool), typeof(float) })]
    internal static class PvpSpellProcTelemetryPatch
    { [HarmonyPrefix] private static bool Prefix(CastSpell __instance, Spell __0, Stats __1) { return PvpTemporaryCloneFactory.ObserveAndAllowSpellStart(__instance, __0, __1); } }

    [HarmonyPatch(typeof(CastSpell), "StartSpellNoAnim", new Type[] { typeof(Spell), typeof(Stats), typeof(float) })]
    internal static class PvpSpellNoAnimTelemetryPatch
    { [HarmonyPrefix] private static bool Prefix(CastSpell __instance, Spell __0, Stats __1) { return PvpTemporaryCloneFactory.ObserveAndAllowSpellStart(__instance, __0, __1); } }

    [HarmonyPatch(typeof(Character), "DoDeath")]
    internal static class PvpTemporaryCloneDeathRewardPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Character __instance)
        {
            PvpTemporaryCloneFactory.SuppressBorrowedDeathRewards(__instance);
        }
    }
}

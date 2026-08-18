# Changelog


## 0.5.8 - MMO world-combat participation policy

- Removed the isolation-era `third_party_aggro` termination path. Active PvP no longer ends or despawns
  simply because an ordinary Sim, pet, hostile mob, or other native combat-capable world actor acquires
  aggro, attacks, heals, takes damage, or otherwise joins the live combat graph.
- Removed the `player_in_combat` offer gate so an arranged challenge or eligible ambush can coexist with
  hostiles already fighting the player/party. Preparation/countdown still protects the temporary proxies
  from pre-GO interaction while unrelated native world combat continues normally.
- Added a narrow native-state protection classifier for genuinely neutral/noncombat world actors. Sims and
  owned/summoned pets are explicitly not protected merely because they are NPC-backed. Verified vendor,
  invulnerable, `NeverAggro`, resource/chest, and Player/PC/Villager/DEBUG-faction actors are protected;
  ambiguous actors defer to native behavior rather than being blocked just because they have an `NPC`.
- Proxy aggro/damage/spell targeting against a protected actor is rejected per interaction and a protected
  target discovered after native `NPC.Update` is cleared without ending the match. Ordinary world targets
  remain eligible for native AI target changes after the initial defender seed.
- AoE/PBAE starts are no longer rejected based on surrounding actors. Untargeted AoE starts pass through;
  only an actually affected actor that current native state proves protected is filtered by the per-target
  hooks. No proximity-based recreation of Erenshor faction targeting was added.
- Outside world damage/healing/aggro involving PvP participants is allowed when native Erenshor permits it.
  PvP team identity still prevents attacker-team/defender-team friendly fire and cross-team beneficial
  healing.
- Spawn formation no longer demands arena-style 8-10m clearance from every NPC-backed actor. Hostile mobs,
  Sims, and pets only receive a 1.5m physical-overlap buffer; proven protected world actors retain the
  larger 10m player / 8m spawn safety clearance.
- Native combat evidence now accepts a permitted world target as a legal acquisition, so world-combat
  expansion cannot falsely trip the inert-team technical-failure watchdog.


## 0.5.7 - arranged GO gate, native engagement proof, and technical-failure safety

- Replaced the implicit spawn -> Active transition with an explicit `Preparing -> Countdown -> GO -> Active`
  lifecycle. Temporary attackers now enter countdown with `NPC.NeverAggro=true`, spell casting/navigation
  held, and native maintenance/nameplate updates available; the containment layer also blocks defender
  aggro, damage, and spell starts against the temporary team before GO.
- GO is a single main-thread release transition. Every attacker is enabled, native navigation is made
  available, `NeverAggro` is cleared for the team together, only current-match defender targets are
  seeded through native `ForceAggroOn`, and the native `NavUpdate`/`BehaviorUpdate` coroutines are
  launched. Arranged matches show 3-2-1-GO; wild ambushes retain their immediate-start presentation.
  No custom movement or combat AI was added.
- Hardened the 0.5.6 coroutine repair: `behDo` and `navDo` are now verified/started independently. A
  non-null behavior handle can no longer short-circuit the helper while navigation is absent (the native
  `NPC.Start` path can legitimately create exactly that state when `NeverAggro=true`). Live-clone borrowed
  coroutine handles are cleared before GO, and GO fails closed unless both native behavior and nav handles
  are resident for every attacker.
- Added bounded, first-hit native-combat diagnostics for `NPC.Update`, combat-section entry, legal target
  acquisition, native pursuit, melee decisions/attempts, attack-skill/spell decisions, heal checks, spell
  starts, damage to a defender, healing to an attacker, and `NPC.Update` exceptions.
- Added a six-second post-GO engagement watchdog. A combat-section entry alone is diagnostic and does not
  satisfy the watchdog; valid evidence requires native target acquisition after Update, pursuit, melee/attack
  evaluation, heal/support evaluation, a spell start, effective defender damage, or effective attacker
  healing. If no attacker reaches any such signal, or the entire attacker team is defeated before
  engagement, the match ends as
  `technical_failure_ai_inactive` with winner none, zero PvP XP/gold, no win/stat/history credit, and no
  victory reward.
- Removed the historical blanket `NoSelfHeal=true` policy. Native self-heals, allied attacker healing,
  HoTs/lifesteal, and support behavior remain available; cross-team spell healing and outside assistance
  are blocked by participant ownership.
- Friendly same-side defender/pet aggro or damage is blocked without being misclassified as third-party
  interference. Unrelated world interference retains the fail-safe cancellation policy.
- Expanded deterministic policy/source-guard coverage for plugin identity, countdown/GO ownership,
  pre-GO attack holds, native engagement evidence, technical-failure reward safety, healing semantics,
  participant/pet ownership, repeated matches, and duplicate-safe cleanup.


## 0.5.6 - native behaviour coroutine repair (second bypassed-Start defect)

- Fixed the residual "attackers stand still" behaviour that survived the 0.5.5 nameplate repair. The
  0.5.5 live 5v5 proved the nameplate fix worked - `HandleNameTag` NREs went 1130 -> 0 and
  `namePlateTxt=True; namePlateObject=True` - yet attackers still produced
  `damage_to_defenders=0`, `attack_spell_decisions=0`, `heal_checks=0`, `spell_starts=0` over a full
  78-second match. Root cause: **none of the combat AI runs in `NPC.Update` at all.** Verified against
  the installed `Assembly-CSharp.dll`, the decision chain is
  `NPC.BehaviorUpdate(0.1f)` (a coroutine) -> `DoNonRaidBehavior`/`DoRaidBehavior` -> `Combat` ->
  `DoAttackSpell`/`DoAttackSkill`/`PerformMeleeHit`, and `CheckHeals`/`CheckBuffs`.
- `NPC.Start` (IL_07AA-07F4) is the only place that launches it:
  `if (!NeverAggro) { navDo = NavUpdate(0.3f); StartCoroutine(navDo); }` followed by
  `behDo = BehaviorUpdate(0.1f); StartCoroutine(behDo);`. A live-cloned proxy deliberately skips that
  `Start`, so **neither the behaviour nor the navigation coroutine was ever started** - the proxy could
  hold a valid, successfully forced aggro target and still never evaluate a single attack, spell or heal.
  This was a second, independent consequence of the same bypassed-`Start` decision that caused the
  nameplate defect.
- Registered temporary proxies now start both native coroutines at combat activation (not at spawn, so
  proxies stay inert while preparing), mirroring `Start`'s ordering and its `NeverAggro` guard on the nav
  coroutine. The call is idempotent - a proxy can never run two behaviour coroutines - and new
  `proxy_behavior_coroutines started=N/M` telemetry reports how many are actually resident.
- Fixed the startup banner reporting a stale hardcoded version. `ErenshorPvPPlugin` logged a literal
  `0.5.4` while the running assembly was genuinely `0.5.5`, which made the load line useless for
  confirming which DLL was live - the exact check live acceptance depends on. The banner and the runtime
  revision marker now derive from the `LunarisPlugin` attribute, so they can never drift from the build
  again.


## 0.5.5 - proxy nameplate runtime repair (inert-attacker root cause)

- Fixed the defect that made an entire arranged attacker team stand still, never fight back, and still
  hand the player a victory. Verified against the installed `Assembly-CSharp.dll`: `NPC.Update` calls
  `HandleNameTag()` as its **third** statement - before the `NeverAggro` early-out and before every
  combat call - and `HandleNameTag` dereferences `NPC.NamePlateTxt` (`TMPro.TextMeshPro`) through
  `callvirt Behaviour.get_enabled()` in every branch. `NPC.Update` has no exception handler, so a null
  `NamePlateTxt` threw every frame and the whole combat/aggro/nav half of `Update` never executed once.
  That is the single cause of the observed `damage_to_defenders=0`, `heal_checks=0`,
  `attack_spell_decisions=0`, `spell_starts=0`, and the ~1,130 `NPC.HandleNameTag` NREs in one 7-second
  match.
- Whole-assembly field-access scan proved `NamePlateTxt`/`NamePlateObject` are written **only** by
  `NPC.Start` and read **only** by `HandleNameTag`. A live-cloned PvP proxy deliberately skips that
  `Start` (`PvpProxyStartupPolicy.ShouldRunNativeNpcStart`), and `ConfigureNativeMaintenanceState`
  restored the other Start-owned references (`Myself`, `MyStats`, `MySpells`, `MyNav`, `MyCharControl`,
  `MyRaidSlot`, `NameFlash`) but not these two. `UpdateNamePlate()` could not compensate because it
  early-returns when `ThisSim == null`, which is always true for a non-persistent proxy.
- Registered temporary proxies now rebuild the nameplate presentation natively, reproducing
  `NPC.Start`'s own recipe in order: reuse the clone's own nameplate when it survived cloning,
  otherwise instantiate the same `GameData.GM.GetComponent<Misc>().NamePlate` prefab, bind
  `NamePlateTxt`/`NamePlateObject` from it, point `NamePlate.MyStats`/`.Myself` back at the proxy, set
  the plate text to `NPCName`, and re-parent it under the proxy. A candidate whose transform is not a
  descendant of the proxy root is rejected, so a proxy can never bind - and mutate - the original live
  template NPC's nameplate.
- Extended `requiredRuntimeState` to validate `hasNamePlateTxt` and `hasNamePlateObject`. The previous
  invariant only proved `hasNameFlash`, which is a **different field** (`FlashUIColors`) from the one
  `HandleNameTag` dereferences - which is exactly how the broken match logged `nameFlash=True` and
  `requiredRuntimeState=PASS` while every proxy was still guaranteed to throw on its first frame. A
  proxy missing either reference now fails preparation instead of reaching combat. Proxy and template
  telemetry both report the two new fields separately.
- Quarantined two stale duplicate `ErenshorPvP` DLLs (both plugin version 0.5.2) that had been left in
  the Lunaris scan root with live `.dll` extensions and were producing the two
  `Plugin already loaded: 'ErenshorPvP'` startup errors. They were moved out of the scan root and out of
  Git rather than deleted; exactly one `ErenshorPvP` assembly identity now exists under the game tree.
- `BUILD_AND_INSTALL.ps1` now compiles to `build-output/` and installs as a separate verified step,
  refuses to replace the installed DLL while Erenshor is running, and reports/compares candidate vs
  installed SHA-256. It previously compiled straight into `<Erenshor>\plugins`, so a failed compile could
  leave a broken assembly where Lunaris scans.


## 0.5.4 - Forgotten Roads release/discoverability correctness

- Preserved the narrow temporary-proxy `NPC.HandleMaintenaceAndCounters` containment repair; no native combat-math redesign.
- Added the Forgotten Roads retained-uGUI module header contract to the PvP panel: one robust chevron left of `PVP`, header-only collapse, and X close.
- Collapse/expand preserves the panel top edge, keeps drag/camera ownership cleanup, and hides the body while collapsed.
- Release/live acceptance remains required for two consecutive 5v5 matches and reward/cleanup proof.

## 0.5.2 - Suite close contract / drag release hardening

- Added the missing Suite `ui.state` Aura provider for the retained PvP panel, including actual Canvas sort order and activation time; existing `closePanel` is now a complete quick-close contract.
- Panel/header interaction refreshes activation ordering without changing gameplay state.
- Hardened drag cleanup by force-releasing owned pointer state before UI hide and panel/launcher position resets, in addition to the existing pointer-up, end-drag, focus/pause, close, zone, disable/destroy and unload paths.
- Expanded pure tests for PvP `ui.state` formatting/bounds and retained existing pointer ownership acquire/release cycles.

## Unreleased - proxy startup / drag ownership diagnostics

- Scoped the observed `NPC.Start()` failure to registered PvP temporary combat roots cloned from an
  already-started live scene NPC. Those live-source clones now bypass the inappropriate borrowed-NPC
  startup replay; ordinary game NPCs remain untouched and resource-prefab proxies retain native
  startup with pending/completed/failed diagnostics.
- Added a fail-closed pre-lethal proxy component invariant and a reasserted reward invariant covering
  private kill-XP readability/zeroing, boss/bonus XP, quest completion, faction changes, and loot
  gold/enablement. Existing death-time suppression and configured PvP reward authority remain in place.
- Added attack-spell decision, heal-check, and CastSpell-start telemetry so a zero-healing match can be
  classified as no heal-capable loadout, heal AI not evaluated, no cast started, or no effective heal.
  No balance values were changed from the single supplied 5v5 observation.
- Retained-uGUI drag ownership now begins on left pointer-down (before Unity's drag threshold), is
  reasserted only while PvP owns the gesture, restores a pre-existing native drag flag, and fails safe
  on lost pointer, focus/pause, disable, destroy, panel close, zoning, and unload.
- Repeated terminal callbacks after proxy collections are already empty no longer publish/log a second
  fight cleanup. Added pure tests for proxy startup/reward policy and pointer acquire/release cycles.

## Unreleased - retained uGUI / Suite control migration

- Aligned the retained launcher/panel with the proven Sim Actions dark/translucent/cyan visual language; the three consent switches now render their live state explicitly as `PvP Enabled [ON/OFF]`, `Arranged Challenges [ON/OFF]`, and `Wild Ambushes [ON/OFF]`.
- Removed legacy F10 panel polling. Normal access remains the retained launcher or optional Suite Hub, with `/epvp` retained as command recovery/debug access. Combat, matchmaking, rewards, ambush cadence, acceptance/refusal, spawn clearance, and cleanup were not changed.
- Replaced the production PvP IMGUI launcher/window with one persistent retained-uGUI canvas (`Canvas`/`CanvasScaler`/`GraphicRaycaster`, TMP, Buttons, ScrollRect/layout). Removed the PvP UI `PlayerControl.LeftClick` and `csMouseOrbit` Harmony workarounds; production UI no longer uses `OnGUI`, `GUI.Window`, `GUI.DragWindow`, native `DragUI`, or `EditUIMode`.
- Added mod-owned EventSystem drag guards for the launcher grip and panel header. Owned drag sets `GameData.DraggingUIElement`, releases on drag end/pointer-up/disable/destroy/zoning/unload, persists normalized position once per completed gesture, and reclamps on resolution changes.
- Added the dedicated STATUS/FIGHT/RULES/SCORE/optional DEBUG retained panel. Ordinary controls route through `PvpControlApi`; Flee requires a second confirmation click. Development spawn/despawn probes remain command-only. Matchmaking, proxy spawning, lethal combat, containment, reward, result, persistence, and opt-in semantics were not redesigned.
- Added `showLauncher` Suite/Aura setting with fail-open fallback: if Hub is absent/unusable or this module bridge is not registered, the standalone launcher is forced visible. Hub status remains deliberately concise (`Enabled | Idle` / `Enabled | Match active`) to stay below the 240-character descriptor limit.
- Added pure retained-UI geometry, launcher-visibility, and concise-status tests. Current-assembly compile/live Lunaris validation remains required before release.

## 0.5.0 - Native Lunaris migration

- Migrated off BepInEx 5 onto native Lunaris: `BaseUnityPlugin`/`[BepInPlugin]`/`[BepInProcess]`/
  `Logger` replaced by `LunarisPlugin`/`[LunarisPlugin]`/
  `[LunarisPermission(Reflection | Harmony)]`/native `Logging`. `BepInEx.Configuration.ConfigFile`/
  `ConfigEntry<T>` replaced by a new typed `PvpSettings` class (`[Config]` fields) plus a small
  `PvpConfigEntry<T>` compatibility shim, so `PvpController`, `PvpRewardService`, and
  `PvpRecordService` (previously three separate classes each taking a shared `ConfigFile`) needed
  no changes beyond their field types and `Initialize` signatures. All 34 existing settings across
  those three classes are preserved verbatim (section/key/default/description).
- Native Lunaris config does not auto-persist a `.Value` write to disk the way BepInEx's
  `ConfigEntry` did, so an explicit `PvpController.SaveSettings` hook (wired by the plugin to
  `Config.Save()`, matching the pattern used in this author's Erenshor Nemesis migration) is now
  called after every settings mutation: PvP/Ambush/UI/Debug toggles, ambush cadence adjustments,
  panel position and full-view persistence, `/epvp ambushhere`, win/loss/escape record updates,
  and the reward anti-farm cooldown timestamp.
- This is a loader/config/logging/lifecycle migration only: no eligibility, matchmaking, spawn,
  combat-containment, reward-calculation, or event-contract logic changed. Every Harmony patch
  target was re-verified against the currently installed `Assembly-CSharp.dll`.
- `BUILD_AND_INSTALL.ps1` rewritten for Lunaris: install target is now
  `<Erenshor>\plugins\ErenshorPvP.dll`; reference resolution now looks for a Lunaris developer
  folder (`Lunaris.dll`/`0Harmony.dll`) instead of a BepInEx profile root; all r2modman
  BepInEx-profile auto-detection removed. The optional `../shared` cross-mod contract-conformance
  compile step (shared with Erenshor Nemesis and Deep Sims) is unchanged.
- Verified: real compile against the installed Erenshor + Lunaris assemblies, zero `BepInEx`
  references in the compiled output, and a static hot-unload audit (the
  `SceneManager.sceneLoaded`/`sceneUnloaded` subscriptions installed in `Awake()` are unsubscribed
  in `OnDestroy()`; `Harmony.UnpatchSelf()` is called; `PvpController.Shutdown()` runs and
  `PvpController.SaveSettings` is cleared before the plugin instance reference is released; the
  `AppDomain.CurrentDomain.GetAssemblies()` usages in `PvpCompatibility` and `PvpEventContract`
  are fresh per-call scans with no `AssemblyLoad` subscription to leak).
- Not yet done: live in-game verification under Lunaris, including `/epvp selftest` (the existing
  8-group deterministic policy suite is heavily entangled with Unity/game types across
  `PvpCombatContainment`, `PvpTemporaryCloneFactory`, and `PvpPanel`, so unlike the smaller mods in
  this migration series it was not practical to extract into a standalone outside-the-game test
  runner for this pass — it must be run live via `/epvp selftest` as part of verification, same as
  before this migration).

- Added `shared/PvpContractConformance.cs`, compiled into PvP, Nemesis, and Deep Sims, pinning all three to one outcome-classification table and one result-row shape. `/epvp selftest` now runs it against `ClassifyOutcome` and against the live result queue. The shared file is optional at build time so a standalone copy of the mod still compiles.
- `PvpSemanticEvent` now carries PvP's own `Classification` for terminal events, and `Publish` prefers a seven-field `NotifyPvpEvent` on the Deep Sims bridge, falling back to the six-field form for older builds. Consumers no longer re-derive a verdict from the raw reason token.
- A match cancelled without a verdict now publishes `pvp_cancelled` with its match id instead of the housekeeping `pvp_proxy_despawned` event, which no consumer accepted, so zoning out of a fight is visible to social consumers.
- Raised the optional-mod contract to v2. `ErenshorPvpApi.RecentResults()` exposes a bounded, non-destructive queue of the last 16 terminal match records (`sequence|match_id|opponent|outcome|mode|classification|utc_ticks`), so a consumer that polls late, or two results landing between polls, can no longer lose a result. The v1 `Last*` properties remain for older consumers.
- Added `ErenshorPvpApi.ClassifyOutcome`. PvP is now the authority for what an outcome means: verified player win, Nemesis win, player fled, enemy retreated, cancelled, and invalid are distinct. Third-party interference, internal fight-state failures, and spawn failures classify as invalid rather than as legitimate escapes.
- `Despawn` now emits a terminal cancelled/invalid record for a match that ends without a fight verdict (zoning, manual despawn, shutdown, offer timeout, failed team spawn), so an external consumer is never left waiting for a result that will never arrive. One match still produces exactly one record.
- Fixed preferred-leader party planning. A requested leader is rejected only when genuinely absent from the eligible pool; a leader too weak to legally attack a duo alone now brings a partner instead of failing the request, and stays leader. Full-party strength rules, level ranges, same-guild preference, and role diversity are unchanged.

- Reworked the Liam Kilfa containment fix so ordinary Erenshor combat remains authoritative: player/party attacks against world NPCs are allowed and end the separate PvP encounter, while proxies cannot attack outsiders and outsiders cannot enter the PvP fight.
- Added clear-area encounter placement. Offers and spawns require 10m player clearance from unrelated NPCs, 8m formation-point clearance, complete NavMesh paths, and an 11m formation selected across eight directions. Party members and their owned pets are excluded from the outside-NPC scan; forced starts explain which nearby NPC or navigation condition blocked the match.
- Live validation confirmed the merged fallback equipment selector: Reynold resolved 105 eligible class/level items, selected 10 visible pieces, and the two-proxy encounter reported `equipped=2`.
- Moved encounter totals to the authoritative native `Stats.ReduceHP` path so periodic/direct HP loss is captured alongside ordinary physical, magic, and bleed calls without double counting. Added nested-safe telemetry for both native `HealMe` overloads and reports healing separately for attackers and defenders.
- Added a consolidated validation evidence stream: `match_plan`, `balance_summary`, `validation_summary`, `reward_result`, and `validation_cleanup` cover scaling, HP flow, visuals/equipment/loadouts, rewards, and bounded cleanup.
- Added persisted `/epvp validation on|off` and a TEST-tab toggle. Detailed acceptance logs default on during development and can be silenced after validation without hiding core failures or final results.
- Fixed fallback equipment discovery after live verification showed an empty non-null `ItemDBList` masking the populated native item array. The selector now merges and deduplicates `ItemDBList`, `ItemDB`, and `GenericItems`, retains class/level/visible-slot restrictions, tolerates equivalent class assets by verified class/display name, and logs compact database/visible/class/level/eligible/selected counts.
- Added per-encounter balance telemetry using actual before/after HP loss rather than requested hit values. Each lethal termination logs duration, composition, enemy average level, defeated attackers, defender pets, damage to both sides, and pet damage; `/epvp verify` exposes the live counters.
- Added contained defender-pet combat. Existing and newly summoned native pets are admitted only when their bounded `Character.Master` chain resolves to the local player or a captured living party defender; pet attacks can damage proxies, proxies can damage those pets, unrelated actors remain blocked, and cleanup releases registered pet aggro.
- Added defender-pet counts to lethal-start and runtime-verification diagnostics plus deterministic containment-policy self-tests.
- Corrected group matchmaking to use the living active defender party's average level for both off-map candidate filtering and final team validation instead of silently using only the player level. Diagnostics and the RULES tab now expose the calculated average.
- Weighted automatic attacker counts so solo four- and five-enemy encounters remain possible but uncommon; explicit debug sizes remain exact and still pass the same safety policy.

## 0.4.0

- Added encounter-local fallback equipment for valid off-map profiles whose tracking/save record contains no usable gear. The selector uses native item data, requires the profile's real class, stays at or below its level, fills visible armor and hand slots deterministically, and never mutates Sim inventory or save data. Runtime verification now requires either saved or fallback equipment to render for every proxy.
- Fixed the live held-weapon failure: `ModularParts.SpawnWeapons` requires explicit primary and secondary slots and dereferences both; PvP now supplies native Empty slots for unused hands instead of null. The native Sim's serialized `Mods` renderer is also preferred over hierarchy guessing.
- Reapplied class spells, tuned melee damage, and profile combat stats immediately before lethal combat, after delayed native `NPC.Start` has had a chance to restore borrowed-template state. Added `combat_runtime_ready` diagnostics with actual `CastSpell.KnownSpells` and melee range.
- Disabled cosmetic rewards after live evidence proved `Inventory.TransmogSlots` are typed equipment positions, not generic unlock storage. The old random-empty-slot implementation could place a hammer into the chest cosmetic position and hide the player's armor; no further transmog state is written until a verified slot-safe native API exists.
- Corrected full-party composition: when four or five defenders have five eligible at-or-above-level opponents available, the entire attacking party now comes from that stronger pool instead of allowing a qualifying leader to pad the team with lower-level guildmates. Added deterministic mixed-level coverage.
- Strengthened off-map identity: matchmaking now excludes a profile when native `SimPlayerTracking.CurScene` matches the player's current scene, even if the avatar is momentarily pooled/inactive and therefore absent from `FindObjectsOfType`. `/epvp diagnose` separately reports same-zone and genuinely off-map profile counts.
- Hardened the next live appearance/loadout test: pooled off-map Sim templates are explicitly activated only after every gameplay-bearing shell component is disabled; native NPC and `CastSpell` lists are initialized when a sparse resource template leaves them null; and `/epvp verify` now rejects inactive visuals, missing Animator controllers, or class loadouts that did not finish wiring.
- Made `XpFractionOfLevel` a hard victory-reward ceiling. Encounter risk may reduce XP for an easier matchup but can no longer multiply the default 50% award above half of the current level threshold; added deterministic reward-boundary self-tests.
- Fixed live proxy-template selection to reject player-summoned pets and companion-style bodies, which had produced oversized, non-Sim-looking opponents.
- Made visual shells render-only by disabling their uninitialized `Stats` updates (eliminating repeated `Stats.CheckAuras` errors) and explicitly restoring their Animator. Added the missing Unity animation assembly reference to the standalone build script.
- Bound each proxy's native `Character` animation output to the visible Sim shell with Erenshor's public `AssignAnim(Animator)` path. Native NPC movement, melee, casting, hit, and `Character.DoDeath` transitions now address the Sim Animator instead of the hidden creature Animator; `/epvp verify` reports missing or unbound shells.
- Fixed class loadouts to mirror native `SimPlayer.LoadSimSpells`: every class/level-eligible `SimUsable` spell is admitted, while only `SimsNeedHelpToLearn` spells require the saved acquired-spell list. Pet spells remain excluded.
- Initialized the temporary visual Sim's index, hair/skin indices, and non-null cosmetic placeholders before calling `UpdateSimPlayerVisuals`, fixing the equipment-renderer's null path without loading or mutating persistent Sim data.
- Expanded `/epvp verify` to fail explicitly on saved equipment that did not reach the native visual renderer and on eligible class combat spells that did not reach `CastSpell.KnownSpells`; successful equipment application now logs the valid item count.
- Fixed double XP seen in the first successful team victory: native `Character.Start` could restore the borrowed creature's kill-XP value after clone setup. A scoped `Character.DoDeath` prefix now zeros XP, boss XP, bonus XP, loot gold, quest completion, and faction changes only for registered PvP proxies immediately before death; the configured team-victory reward remains authoritative.
- Added a player-controlled Flee outcome (`/epvp flee`/`/epvp escape` and FIGHT-tab button): it records an escape, destroys the active proxy party, and grants no victory reward.

- Fixed the panel turning the camera. `csMouseOrbit.LateUpdate` reads `Input.GetAxis("Mouse X"/"Mouse Y")` every frame with no mouse-button gate, so any pointer movement over the panel - including a drag - rotated the view. Suppressing the click alone could never fix this. The orbit speeds are now zeroed for the duration of that call while the pointer is over PvP UI, so the camera keeps following the player but the axis deltas contribute nothing. The mute is self-healing and is also released on shutdown.
- Fixed the panel snapping to the top-left corner and becoming unmovable after being dragged past a screen edge. The window re-anchored from persisted offsets every frame while `GUI.DragWindow` moved it independently, and the offsets were reverse-engineered from the resulting rect. Dragging is now handled explicitly with `GUIUtility.hotControl` and writes the persisted position directly, so the stored offsets are the single source of truth. Added a deterministic drag round-trip test covering clamp-to-corner and drag-back-out.
- Added `/epvp arranged on|off` and `/epvp ambush on|off`, giving the two consent paths chat parity with their PVP-tab switches. The bare form reports current state, and every reply restates which path prompts: arranged always asks Accept or Refuse, ambushes never do. Both warn when the master switch is off so a toggle cannot look like it took effect.
- Made the consent difference visible where it is set: each PVP-tab sub-switch now carries a one-line explanation, and the config descriptions say plainly that arranged prompts and ambushes do not.
- Added the missing `UnityEngine.AnimationModule` reference to `ErenshorPvP.csproj`, which the standalone build script already had.
- Audited every `/epvp` command against the panel and closed three gaps: **Reset position** and **Status** now sit in a collapsed PANEL section on the PVP tab instead of only in the hidden TEST tab, **Verify** and **Diagnose** are available on FIGHT with no encounter running (where Diagnose is most useful, since it reports why no offer fired), and **Clone status** is reachable during an active fight. README now documents where each command lives.
- Panel is now compact by default: master switch, current zone safety, and anything awaiting a decision (pending challenge card, live fight roster with a Flee button). A **Full** checkbox in the header reveals the tab bar and every detail view, and the choice is persisted.
- Rebuilt the panel on an auto-sizing `GUILayout` window with collapsible sections and a scroll view, so a tab can grow or a roster can fill up without running off the screen or needing a hand-maintained height table.
- Fixed panel clicks reaching the world: Erenshor reads the mouse in `PlayerControl.LeftClick` rather than through IMGUI events, so consuming the IMGUI event was not enough. A Harmony prefix now suppresses the world click while the cursor is over the panel or the map-side toggle, matching how the Travel overlay does it.

- Rebuilt the F10 panel as a Party Tools-style window: same palette, header drag, upper-right anchoring below the minimap, and offset-based position persistence, so the two mods read as one interface and neither overlaps the character/party panels.
- Split the panel into PVP, FIGHT, RULES, and SCORE tabs plus a hidden TEST tab toggled with `/epvp debug`.
- Added a separate `Arranged challenges` switch alongside `Wild ambushes` under the master `World PvP` switch; arranged offers now respect it.
- Added an in-panel `Allow/Stop ambushes here` button, ambush chance and interval steppers, and a live level-range/party-size rule summary.
- Added a live attacker roster with name, level, class, role, guild, health bar, and spell count, plus mode and motive for the active encounter.
- Split the score view into arranged and ambush totals with reward, anti-farm cooldown, and cosmetic-slot status.
- Panel clicks that miss a control are now consumed so they can no longer reach the world and swing the camera.
- Gave every `/epvp` subcommand a panel control so the chat syntax is never required: encounter and inspection commands are grouped in the TEST tab, Verify/Diagnose/Team/Despawn also sit on FIGHT, and the ambush-zone list plus a match simulator sit on RULES.
- Command output is now mirrored inside the panel for 30 seconds as well as written to the social log, so results stay readable while the panel covers the chat area. A command that throws reports as text instead of escaping into `OnGUI`.
- Added `/epvp panelreset` and deterministic panel-positioning tests to `/epvp selftest`.

## 0.3.0

- Split world PvP into consensual arranged party/guild matches and rare non-consensual wild ambushes.
- Added an exact ambush-zone allowlist, 15-35 minute randomized opportunity cadence, configurable opportunity chance, and automatic start without an Accept prompt.
- Added grounded camp-claim, killing-spree, territory, and guild-raid motives. Camp claims require a verified active Campmaster Hunt Camp.
- Added `/epvp force arranged|ambush [1-5]`, mode/motive diagnostics, mode-specific records, outcome chat, semantic events, and Deep Sims reaction context.
- Added `/epvp ambushzones` and `/epvp ambushhere on|off` so exact wild-zone permissions can be managed in game; protected scenes cannot be added.

## 0.2.0

- Accepted offers now spawn 1–5 off-map Sim-profile proxies and immediately begin lethal team combat.
- Added party-size scaling, stronger-solo rules, full-party rules, same-guild preference, class roles, and level-aware role stat scaling.
- Added saved equipment visuals and acquired Sim-usable class spell loadouts with melee fallback.
- Added multi-party damage/aggro containment, all-attacker victory detection, low-health retreat, friendly result chat, and expanded team health diagnostics.
- Enabled a configurable native transmog unlock roll after verified victories.
- Added a fail-closed co-op session gate and corrected the panel/status text to describe the live backend.
- Hardened persisted panel dragging and full-screen clamping; retained the compact map-side toggle.
- Added loaded-resource NPC template fallback, continuous match IDs, `/epvp diagnose`, and deterministic party-distribution/guild/scaling tests.
- Blocked offers during existing combat, applied cooldowns to every attacking profile, guaranteed scene-transition cleanup, and resolved saved spells by either ID or display name.
- Added `/epvp force 1-5` real-composition testing, explicit forced-scan failures, proxy nameplate restoration, and a taller pending-offer panel.
- Added `/epvp verify` runtime assertions for profile count, valid HP/NPC identity, visible Sim shells, defender targets, and combat containment.
- Completed the optional Deep Sims bridge for sanitized challenge/result reactions; all lifecycle publishers now use the same bridge path and match ID.
- Tightened attacker-less proxy damage so only Erenshor damage explicitly marked `_fromPlayer` is admitted; environmental/unknown damage remains contained.

## 0.1.0

- Added standalone Erenshor PvP plugin.
- Added disabled-by-default consent UI, `/epvp` commands, protected-zone and level policy.
- Added fail-closed candidate filtering for local, nearby, alive, non-party SimPlayers.
- Added sanitized public PvP semantic event contract.
- Added deterministic `/epvp selftest` checks.
- Added read-only `/epvp spawnprobe` validation for the native temporary-Sim research phase.
- Added an isolated, inert, timed `/epvp spawnclone` lifecycle test with manual despawn and persistent-data-load suppression.
- Added an experimental `/epvp fightclone` lethal proxy-combat test: normal player death/respawn is retained, external damage is contained, and the temporary actor is removed on either result.
- Added native XP and gold victory rewards for a verified proxy defeat: default 50% of the current level XP threshold, level-based gold, and a persisted 30-minute anti-farm cooldown. Rewards use `GameData.AddExperience` and the live `Inventory` instead of direct save-file edits.
- Deferred cosmetics/transmog rewards until the game's separate cosmetic-inventory grant/save path is verified.
- Replaced local-Sim matchmaking with off-map profile selection and separated local Practice Duels from lethal PvP.
- Added profile snapshots for level, class, guild, gender, appearance, gear score, and equipment identifiers.
- Added a disabled Sim visual shell over the native combat body, with named Sim templates preferred when available.
- Removed borrowed mob spells/skills/healing/pets/procs and added persistent win/loss/escape records.
- Added protected startup/city/tutorial gates, a compact map-side toggle, and Party Tools-style persisted panel dragging.


## Unreleased - Suite UI/API coherence handoff

- Added optional, versioned `PvpControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Reworked only panel/launcher interaction: runtime Rect now owns drag position, header-only drag persists after the gesture, and camera/target containment covers the full drag. PvP gameplay paths are unchanged.
## 0.5.3 - Forgotten Roads launcher/header chrome

- Standardized the standalone retained-uGUI launcher at 154x32 with restrained `[ON]`/`[OFF]` status and programmatic grip marks.
- Standardized compact title and close-button dimensions without changing PvP gameplay or panel contents.

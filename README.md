# Erenshor PvP

Part of the **Forgotten Roads for Erenshor** mod collection.

Standalone MMO-style PvP encounters for Erenshor. Practice Duels remains friendly, consensual, and non-lethal for Sims already in the zone. Erenshor PvP selects off-map Sim profiles and runs lethal encounters with normal player death and respawn. World PvP contains both consensual arranged matches and rare non-consensual wild ambushes.

Current development line: 0.5.9. Remaining work is tracked in [docs/PVP_TODO.md](docs/PVP_TODO.md); the most important remaining gate is live in-game validation of native combat, visuals, rewards, and the retained-uGUI panel.

### Temporary proxy startup boundary

Version 0.5.9 restores the cloned proxy's own native `NPC.Start` lifecycle instead of bypassing
Start and manually manufacturing `NavUpdate` / `BehaviorUpdate` coroutines. Temporary NPCs remain
disabled through preparation/countdown. At GO, `NeverAggro` is released and each NPC is enabled;
Unity/native `NPC.Start` binds and launches the complete native lifecycle. A scoped Start postfix
then reasserts only PvP-owned synthetic identity, class loadout, nameplate ownership, and no-reward
constraints before the initial defender target is seeded through native aggro.

The previous `nav=5/5` check proved only that coroutine handles had been assigned. The 0.5.9 bounded
probe instead records native Start completion, native nav coroutine observation, `NPC.UpdateNav`
entry, first successful `UpdateNav` completion, NavMeshAgent enabled/on-mesh state, destination/path
evidence, movement evidence, and fault type. A proxy team whose native nav path completely faults
fails closed as `technical_failure_ai_inactive` with no winner/reward/history credit. The existing
proxy-owned `NamePlateTxt` / `NamePlateObject` invariant remains enforced so restoring native Start
does not reintroduce the earlier `HandleNameTag` failure.


## Status: native Lunaris migration candidate

This development line is native Lunaris. The earlier loader/config/logging/lifecycle migration
was compile-verified against the then-current installed assemblies without changing eligibility,
matchmaking, spawning, combat containment, rewards, or event contracts. The current workstream
additionally replaces the player-facing IMGUI surface with retained uGUI while leaving those
existing gameplay services authoritative. **This retained-uGUI delta still requires a fresh build
against the current installed `Assembly-CSharp.dll`/Lunaris/Unity assemblies and live in-game
verification, including `/epvp selftest`, before release.** A legacy BepInEx release remains
available in this repository's Git history for anyone still on BepInEx.

## Features

- Disabled by default. Turning world PvP on opts the character into both arranged offers and the possibility of rare ambushes in configured wild zones. Normal mouse access is through the retained-uGUI **PvP** launcher or Suite Hub's **Open PvP** action; `/epvp` remains a compatibility/debug command surface.
- The dedicated panel is retained Unity uGUI (`Canvas`/`GraphicRaycaster`/TMP/Buttons/ScrollRect). Its launcher and header have separate EventSystem drag grips, buttons are never drag surfaces, and owned drags set `GameData.DraggingUIElement` until every end/cancel/unload path releases it. No `OnGUI`, `GUI.Window`, world-click Harmony suppression, camera Harmony muting, or `EditUIMode` forcing is used by production UI.
- Panel tabs: **STATUS** (master PvP/arranged/ambush switches, zone safety, ambush-zone control, pending Accept/Refuse), **FIGHT** (live encounter roster plus two-step Flee confirmation), **RULES** (party-size/level context and safe ambush cadence steppers), **SCORE** (record/reward state), and optional **DEBUG** (bounded runtime verification only; spawn/despawn probes remain command-only).
- Local Sims are never turned hostile. Only profiles outside the current zone can lead or join an attacking party; native `CurScene` tracking keeps a briefly pooled/inactive same-zone Sim from being misclassified as off-map.
- Arranged party/guild offers identify the leader, composition, and motive and require Accept or Refuse.
- Wild ambushes do not ask for match consent after world PvP has been enabled. They start automatically, only in the exact `Ambush/Zones` allowlist and never in protected areas. Existing native world combat is allowed to remain active and can become part of the same combat graph. Natural opportunities use a randomized 15-35 minute interval and a 50% roll by default, so ambushes remain uncommon.
- Ambush text reflects a verified Hunt Camp claim, killing spree, territorial attack, or guild raid. Camp claims require Campmaster to verify an active Hunt Camp; otherwise that motive is unavailable.
- Parties contain 1–5 off-map profiles. Matchmaking uses the average level of the living active defender party for both candidate eligibility and final team validation. Solo defenders may face 1–5, with four- and five-attacker extremes intentionally uncommon; duos face 1–3 and a lone attacker must be at least two levels stronger; trios face 3–5; groups of four or five face a full party of five at or above the defender average when profiles permit.
- Team construction prefers the leader's guild, then missing combat roles and profiles closest to the defender level. Classes are assigned Vanguard, Striker, Caster, or Support roles, producing diverse parties when the eligible profile pool permits.
- Proxies use a native NPC combat body for real targeting, damage, death, pathing, and normal Erenshor combat. The borrowed creature render is hidden and a non-persistent Sim visual shell is attached.
- Pooled/inactive Sim templates are activated only after their actor, AI, collision, navigation, spell, loot, and persistent-Sim components are disabled. `/epvp verify` rejects inactive shells, missing animation controllers, and class loadouts that did not finish wiring.
- Every lethal encounter writes one `balance_summary` diagnostic at termination with duration, attacker/defender counts, defeated attackers, average enemy level, registered defender pets, actual HP damage dealt to each side, and the pet share of attacker damage. These bounded counters reset after each match and make balance changes evidence-driven.
- Saved off-map appearance, hair, skin, equipped-item visuals, class, acquired Sim-usable spells, level, guild, and gear score are copied into a bounded encounter snapshot. If an otherwise eligible profile has no resolvable saved equipment, the temporary visual receives deterministic native class-compatible items at or below its level for visible armor and weapon slots. Fallback gear is encounter-only and is never written to the Sim or an Erenshor save. No temporary actor is registered in the Sim roster.
- Borrowed creature identity, loot, XP, quests, faction changes, pets, procs, and creature skills are removed. Only the profile's verified Sim-usable spell loadout is admitted; the loadout and tuned melee range are reapplied at combat start after native `Start` has finished.
- PvP offers and spawns require a navigable formation, but not an isolated arena. Proven protected neutral/noncombat world actors retain a 10m player / 8m spawn safety clearance; ordinary hostile mobs, Sims, pets, and other native world combatants only receive a small 1.5m physical-overlap buffer. All attackers still require a complete NavMesh path from the 11m formation.
- Native pets owned through `Character.Master` by the local player or a living party defender are treated as defender-side participants, while ordinary MMO-style world combat remains open. Other local Sims, Sim-owned pets, hostile mobs, existing enemies, and other combat-capable world actors may acquire aggro, attack, heal, take AoE damage, and otherwise join the native combat graph without cancelling or despawning the PvP encounter. The mod only intervenes when current native state positively proves a neutral/noncombat actor should be protected (for example a vendor, invulnerable/`NeverAggro` actor, resource object, or known Player/PC/Villager/DEBUG-faction non-Sim); that individual target/effect is rejected or cleared without terminating the match. Untargeted AoE/PBAE starts are not blocked by proximity. A low-health final attacker still has a small chance to disengage, granting no reward.
- Player death is real and uses Erenshor's normal death/respawn consequences. PvP victory requires every attacker to die.
- A player may deliberately flee an active encounter with the FIGHT-tab **Flee** button or `/epvp flee`. This safely ends the match, records an escape, and grants no victory reward; staying in the encounter remains lethal.
- Verified victory grants configurable native XP and gold. XP is reduced for lower-risk matches but hard-capped at the configured fraction of the current level threshold (50% by default); gold scales modestly with attacker count and average opponent level. A persistent 30-minute anti-farm cooldown prevents repeated payouts.
- Cosmetic rewards are temporarily disabled. `TransmogSlots` proved to be typed cosmetic equipment positions rather than a generic unlock inventory; a slot-safe native unlock path must be verified before this reward returns.
- Persistent config-backed records track total, arranged, and ambush wins/losses plus escapes, last opponent, mode, and result. Arranged chat stays sporting; ambush dialogue reflects its hostile motive and outcome without becoming abusive.
- Protected areas include Port Azure, Stowaway's Step, every island scene, tutorials, character selection, and city/hub scene patterns. Custom protected and high-risk scene lists and level ranges are configurable.
- Network/co-op sessions fail closed; a PvP encounter is not started until a verified host-authoritative network design exists.
- A public sanitized `PvpSemanticEvent` contract and optional fact-only `PvpEventBridge` let Deep Sims react to challenges and verified results without controlling combat. The PvP mod remains fully standalone when Deep Sims is absent.

## Current UI

PvP owns one persistent retained-uGUI canvas containing a small launcher and a dedicated tabbed panel. The visible launcher is the normal standalone entry point. With Suite Hub usable and this module's Aura bridge registered, the launcher obeys the `Show PvP launcher` preference; if Hub is absent or the bridge is unavailable, the launcher is forced visible so the player cannot lock themselves out.

The panel has a visible **X**, a dedicated header drag surface that never overlaps the X or tab buttons, normalized bottom-left position persistence, resolution-change reclamping, and a visible reset-position action. Long content stays inside the scroll viewport. The UI object tree is built once and dynamic values are updated in place; tab switches only activate/deactivate the already-built page roots.

Tabs:

- **STATUS** — concise current state, PvP enable/disable, arranged challenge toggle, wild ambush toggle, current-zone safety/ambush permission, and pending challenge Accept/Refuse.
- **FIGHT** — current proxy roster and HP, plus **Flee this fight** with an explicit second click required within five seconds.
- **RULES** — party-size/level context, configured ambush chance and interval, and bounded cadence controls routed through `PvpControlApi`.
- **SCORE** — arranged/ambush record, escapes, last result, reward state, and anti-farm cooldown.
- **DEBUG** — only when `/epvp debug` enables it. It contains bounded verification/status controls. Spawn/target/fight clone probes remain command-only development tools and are not production panel buttons.

All panel mutations route through `PvpControlApi` and then the existing controller/services. The UI does not directly own matchmaking, proxy spawning, combat, damage, rewards, persistence, or encounter outcomes. Normal access is through the retained launcher or Suite Hub; `/epvp` remains the command recovery/debug surface. No global panel hotkey is polled.

For Suite quick-close, PvP now exposes `ui.state` alongside its existing visual-only `closePanel` action. The state reports the retained panel's actual Canvas sort order plus a monotonic activation timestamp, so a verified centralized Hub can order it against other Suite windows. Closing the PvP panel never ends the current PvP encounter.

Drag/camera ownership starts on **left pointer-down before Unity's drag threshold**, not only at BeginDrag. It is reasserted only while PvP owns that gesture and is force-released on pointer-up/end-drag, focus/pause loss, UI hide, reset-position actions, scene transitions, panel close, disable/destroy, and plugin unload. No EditUIMode or camera Harmony patch is used.

## Commands

```text
/epvp                 status / open panel
/epvp on|off          enable or disable incoming PvP
/epvp arranged on|off consensual challenges; these always prompt. Bare form reports state
/epvp ambush on|off   wild ambushes; these never prompt. Bare form reports state
/epvp force [1-5]     create an arranged offer now; optional exact attacker count
/epvp force arranged [1-5]
/epvp force ambush [1-5]  start an eligible wild ambush immediately for testing
/epvp accept|refuse   answer the current offer
/epvp clonestatus     active attacker count and health
/epvp team            pending/active names, levels, classes, roles, HP, and spell counts
/epvp verify          active proxy visual/identity/HP/target/containment verification
/epvp diagnose        one-line zone/profile/template/combat diagnostics
/epvp ambushzones     list exact scenes where natural ambushes are permitted
/epvp ambushhere on|off  add/remove the current scene (protected scenes cannot be added)
/epvp despawn         safely remove a test/active proxy team
/epvp flee            leave an active lethal PvP encounter; records an escape and grants no reward
/epvp debug           show or hide the panel's DEBUG tab
/epvp validation on|off  detailed acceptance logs; disable after validation
/epvp panelreset      move the panel back to its default position
/epvp selftest        deterministic policy tests
/epvp plan <defenders> <attackers> [defenderLevel] [attackerLevel]
/epvp spawnprobe      read-only native capability diagnostics
/epvp spawnclone      isolated one-proxy lifecycle test
/epvp targetclone     isolated target/path test
/epvp fightclone      start lethal combat for a manually spawned test proxy
```

`/epvp` is intentional because Erenshor reserves `/p` for party chat.

### Panel coverage

Ordinary player-facing actions are available without chat: enable/disable, arranged/wild switches, current zone state, pending Accept/Refuse, active-fight Flee, cadence controls, records/rewards, and position reset. Detailed spawn/probe/test commands remain deliberately command-only so migration of the presentation layer cannot accidentally broaden combat controls.

`/epvp` remains intentional because Erenshor reserves `/p` for party chat. `/epvp panelreset` remains a recovery command in addition to the visible reset control. `/epvp debug` only controls visibility of the bounded DEBUG tab.

Only arranged challenges ever ask permission. Wild ambushes remain governed by the existing world-PvP opt-in, allowlists, cooldowns, and combat eligibility rules; this UI pass does not change those semantics.

## Test arranged and ambush encounters

1. Enter a non-city, non-tutorial combat zone. The mod prefers a living native NPC as a combat-body template, then falls back to a loaded non-boss NPC prefab resource.
2. Run `/epvp on`, then `/epvp force arranged 1` through `5` to validate exact arranged compositions.
3. Confirm each arranged offer waits for Accept or Refuse, then press Accept or run `/epvp accept`.
4. Keep `/epvp validation on` during acceptance. Immediately run `/epvp team` and `/epvp verify`. A healthy encounter reports both `VERIFY PASS` and `COMBAT VERIFY PASS`; detailed logs include `match_plan`, `proxy_spawn`, fallback selection counts when needed, `visual_shell`, `class_loadout`, and `lethal_started`.
5. Fight normally. Confirm all proxies can take damage, party members assist, enemy spell kits are profile-derived, and victory happens only after every attacker dies.
6. In a scene listed under `Ambush/Zones`, run `/epvp force ambush 1` through `5`. Confirm there is no Accept prompt, motive-aware warning/chat appears, and combat begins immediately.
7. Verify normal death/respawn on a loss, or the XP/gold message and mode-specific record update on a win. `balance_summary`, `validation_summary`, `reward_result`, and `validation_cleanup` provide one bounded evidence set per encounter. Cosmetic unlocks remain disabled until a slot-safe native API is proven.

After acceptance passes, run `/epvp validation off`. Core failures and final lethal results remain logged, while high-detail spawn, equipment, class, containment, balance, reward, and cleanup evidence is silenced.

If anything misbehaves, use `/epvp despawn`; scene transitions and mod shutdown also clean up all temporary actors and cloned spells.

## Installation

This is a **native Lunaris plugin** — BepInEx is no longer required for this version. Requires
Lunaris installed in your Erenshor install. The compiled DLL is placed directly in
`<Erenshor>\plugins\ErenshorPvP.dll`; Lunaris manages enable/disable.

## Safety boundary

Erenshor remains authoritative for gameplay AI. This mod chooses eligibility and builds bounded encounter actors, but no LLM chooses movement, attacks, spells, healing, targeting, loot, or equipment. The mod never directly edits Erenshor save files. Rewards use native live APIs and normal game saving; records and cooldowns use native Lunaris configuration. Unknown actor, scene, or network state fails closed.

Native spawn research is documented in [docs/NATIVE_SPAWN_FINDINGS.md](docs/NATIVE_SPAWN_FINDINGS.md). The game's exposed `SimPlayer` spawners mutate persistent roster state, which is why combat uses a temporary NPC proxy plus disabled visual shell.

## Credits and Inspiration

### Inspiration

- **[Reckss PvP Mod](https://github.com/Reckimus/ErenshorPvP) by Recks (Reckimus)** helped inspire the direction of adding PvP to Erenshor. No public license was ever published for that project, so no code was copied, decompiled, or used as a dependency; this mod is an independent implementation built against the currently installed game assembly.

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

## Development note

The goal is to build features for Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.


## Optional Suite Hub integration

Forgotten Roads Hub is **optional**. This mod exposes a versioned, primitive-only `PvpControlApi`/Aura surface and never references the Hub assembly. The Hub can display the deliberately concise status (`Enabled | Idle` / `Enabled | Match active`), change the established safe basic toggles (`PvP Enabled`, `Show PvP launcher`, arranged challenges, wild ambushes), and invoke the conventional `openPanel`/close/reset actions.

The dedicated retained-uGUI panel remains fully usable without Hub. Launcher fallback is fail-open for access: when Hub is absent/unusable or this module's own Aura registration failed, the launcher is forced visible regardless of the saved preference. When Hub and the bridge are both usable, the preference is obeyed.

Combat, matchmaking, proxy spawning, damage, death/respawn, rewards, ambush scheduling, eligibility, and encounter outcomes remain authoritative in existing PvP services and were not redesigned by this UI workstream.

The retained-uGUI migration still requires a current-assembly compile and live Lunaris test before release.

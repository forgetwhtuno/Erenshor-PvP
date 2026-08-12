# Erenshor PvP

Standalone MMO-style PvP encounters for Erenshor. Practice Duels remains friendly, consensual, and non-lethal for Sims already in the zone. Erenshor PvP selects off-map Sim profiles and runs lethal encounters with normal player death and respawn. World PvP contains both consensual arranged matches and rare non-consensual wild ambushes.

Current development line: 0.4.0. Remaining work is tracked in [docs/PVP_TODO.md](docs/PVP_TODO.md); the most important remaining gate is live in-game validation of native combat, visuals, rewards, and the Party Tools-style panel.

## Features

- Disabled by default. Turning world PvP on opts the character into both arranged offers and the possibility of rare ambushes in configured wild zones. Use the compact `PvP ON/OFF` switch near the map, `/epvp on`, or `/epvp off`. F10 opens the full panel.
- The panel follows the Party Tools convention: the same palette, header drag, default position in the upper right below the minimap, and offset-based saved position, so it sits alongside Party Tools without overlapping the character or party panels. While the pointer is over the panel or the map-side toggle it behaves like a menu, not the world: a `PlayerControl.LeftClick` prefix suppresses world clicks so the current target is never dropped, and the `csMouseOrbit` orbit speeds are held at zero for that frame so moving or dragging the panel cannot turn the camera. The camera still follows the player normally throughout.
- Panel tabs: **PVP** (master `World PvP` switch, separate `Arranged challenges` and `Wild ambushes` switches, zone status, an `Allow/Stop ambushes here` button, and the pending challenge card with Accept/Refuse), **FIGHT** (live attacker roster with level, class, role, guild, health, and spell count plus the encounter mode and motive), **RULES** (level range, party-size rules, ambush chance and interval steppers, next ambush/offer timers), and **SCORE** (separate arranged and ambush records, reward values, anti-farm cooldown, and cosmetic-slot status). `/epvp debug` reveals a hidden **TEST** tab with force/verify/diagnose controls.
- Local Sims are never turned hostile. Only profiles outside the current zone can lead or join an attacking party; native `CurScene` tracking keeps a briefly pooled/inactive same-zone Sim from being misclassified as off-map.
- Arranged party/guild offers identify the leader, composition, and motive and require Accept or Refuse.
- Wild ambushes do not ask for match consent after world PvP has been enabled. They start automatically, only in the exact `Ambush/Zones` allowlist, never in protected areas or during existing combat. Natural opportunities use a randomized 15-35 minute interval and a 50% roll by default, so ambushes remain uncommon.
- Ambush text reflects a verified Hunt Camp claim, killing spree, territorial attack, or guild raid. Camp claims require Campmaster to verify an active Hunt Camp; otherwise that motive is unavailable.
- Parties contain 1–5 off-map profiles. Matchmaking uses the average level of the living active defender party for both candidate eligibility and final team validation. Solo defenders may face 1–5, with four- and five-attacker extremes intentionally uncommon; duos face 1–3 and a lone attacker must be at least two levels stronger; trios face 3–5; groups of four or five face a full party of five at or above the defender average when profiles permit.
- Team construction prefers the leader's guild, then missing combat roles and profiles closest to the defender level. Classes are assigned Vanguard, Striker, Caster, or Support roles, producing diverse parties when the eligible profile pool permits.
- Proxies use a native NPC combat body for real targeting, damage, death, pathing, and normal Erenshor combat. The borrowed creature render is hidden and a non-persistent Sim visual shell is attached.
- Pooled/inactive Sim templates are activated only after their actor, AI, collision, navigation, spell, loot, and persistent-Sim components are disabled. `/epvp verify` rejects inactive shells, missing animation controllers, and class loadouts that did not finish wiring.
- Every lethal encounter writes one `balance_summary` diagnostic at termination with duration, attacker/defender counts, defeated attackers, average enemy level, registered defender pets, actual HP damage dealt to each side, and the pet share of attacker damage. These bounded counters reset after each match and make balance changes evidence-driven.
- Saved off-map appearance, hair, skin, equipped-item visuals, class, acquired Sim-usable spells, level, guild, and gear score are copied into a bounded encounter snapshot. If an otherwise eligible profile has no resolvable saved equipment, the temporary visual receives deterministic native class-compatible items at or below its level for visible armor and weapon slots. Fallback gear is encounter-only and is never written to the Sim or an Erenshor save. No temporary actor is registered in the Sim roster.
- Borrowed creature identity, loot, XP, quests, faction changes, pets, procs, and creature skills are removed. Only the profile's verified Sim-usable spell loadout is admitted; the loadout and tuned melee range are reapplied at combat start after native `Start` has finished.
- PvP offers and spawns require a clear navigable combat area: the player must be at least 10m from unrelated NPCs, each formation point at least 8m away, and all attackers receive a complete NavMesh path from an 11m formation. The selector checks eight directions and fails safely with the nearest-NPC reason when no formation is available.
- Native pets owned through `Character.Master` by the local player or a living party defender are treated as bounded members of the defender side. Normal Erenshor world combat remains legal: if the player or party attacks an ordinary NPC, that hit is allowed and the separate PvP encounter ends. PvP proxies cannot attack world NPCs, outside NPCs cannot enter the PvP fight, and verified third-party hostility cancels safely. A low-health final attacker has a small chance to disengage, granting no reward.
- Player death is real and uses Erenshor's normal death/respawn consequences. PvP victory requires every attacker to die.
- A player may deliberately flee an active encounter with the FIGHT-tab **Flee** button or `/epvp flee`. This safely ends the match, records an escape, and grants no victory reward; staying in the encounter remains lethal.
- Verified victory grants configurable native XP and gold. XP is reduced for lower-risk matches but hard-capped at the configured fraction of the current level threshold (50% by default); gold scales modestly with attacker count and average opponent level. A persistent 30-minute anti-farm cooldown prevents repeated payouts.
- Cosmetic rewards are temporarily disabled. `TransmogSlots` proved to be typed cosmetic equipment positions rather than a generic unlock inventory; a slot-safe native unlock path must be verified before this reward returns.
- Persistent config-backed records track total, arranged, and ambush wins/losses plus escapes, last opponent, mode, and result. Arranged chat stays sporting; ambush dialogue reflects its hostile motive and outcome without becoming abusive.
- Protected areas include Port Azure, Stowaway's Step, every island scene, tutorials, character selection, and city/hub scene patterns. Custom protected and high-risk scene lists and level ranges are configurable.
- Network/co-op sessions fail closed; a PvP encounter is not started until a verified host-authoritative network design exists.
- A public sanitized `PvpSemanticEvent` contract and optional fact-only `PvpEventBridge` let Deep Sims react to challenges and verified results without controlling combat. The PvP mod remains fully standalone when Deep Sims is absent.

## Current UI

The Party Tools-style UI is now implemented in the PvP plugin as a coordinated standalone window rather than a hard dependency on Party Tools.

The panel opens **compact**: the master World PvP switch, the current zone's safety status, and anything actually waiting on you — a pending challenge card with Accept/Refuse, or a live fight roster with a Flee button. Nothing else is shown. Ticking **Full** in the header reveals the tab bar and the detail views below; the choice is remembered. Long content scrolls and section headings collapse, so no tab can push the window off the screen.

Full view contains:

- **PVP**: master World PvP switch, Arranged Challenges switch, Wild Ambushes switch, zone safety status, ambush-zone control, and Accept/Refuse challenge card.
- **FIGHT**: active mode, motive, attacker roster, level, class, role, guild, health, and spell counts, plus Verify, Diagnose, Team, and Despawn controls.
- **RULES**: level range, party-size rules, ambush cadence/chance, next-event timers, ambush-zone controls, and a defenders-vs-attackers match simulator.
- **SCORE**: arranged and ambush records, rewards, anti-farm cooldown, and cosmetic status.
- **TEST**: hidden with `/epvp debug`. Groups every remaining command under Encounter, Inspect, Isolated Clone Tests, and Panel headings.

Every `/epvp` subcommand has a panel control, so the chat syntax is never required. Command output is written to the social log as usual and mirrored inside the panel for 30 seconds, so results stay readable while the panel covers the chat area.

The panel position is persisted using the same offset-based approach as Party Tools. `/epvp panelreset` restores the default position.

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
/epvp debug           show or hide the panel's TEST tab
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

### Where each command lives in the panel

Every command has a control. Controls needed for ordinary play are reachable without the hidden TEST tab; only development and isolated-proxy tooling is behind it.

| Command | Panel control |
|---|---|
| `/epvp` | F10 opens the panel. The compact body *is* the status view; PVP > PANEL > **Status** prints the status line |
| `/epvp on` / `off` | **World PvP** switch (compact body and PVP tab), or the map-side `PvP ON/OFF` toggle |
| `/epvp arranged on` / `off` | **Arranged challenges** sub-switch on the PVP tab |
| `/epvp ambush on` / `off` | **Wild ambushes** sub-switch on the PVP tab |
| `/epvp accept` / `refuse` / `decline` | **Accept** / **Refuse** on the challenge card (compact body, PVP tab, TEST) |
| `/epvp flee` / `escape` | **Flee this fight** on the active encounter block (compact body, PVP tab, FIGHT tab) |
| `/epvp force [arranged\|challenge\|ambush] [1-5]` | TEST > ENCOUNTER: attacker stepper plus **Force arranged** / **Force ambush** |
| `/epvp team` | FIGHT > **Team**, with and without an active encounter |
| `/epvp clonestatus` | FIGHT > **Clone status** |
| `/epvp verify` | FIGHT > **Verify** |
| `/epvp diagnose` | FIGHT > **Diagnose** |
| `/epvp ambushzones` | RULES > AMBUSH CADENCE > **List zones** |
| `/epvp ambushhere on` / `off` | **Allow/Stop ambushes here** on the PVP tab, or RULES > AMBUSH CADENCE > **Allow/Stop here** |
| `/epvp plan <defenders> <attackers>` | RULES > MATCH SIMULATOR: party-size steppers plus **Simulate match** |
| `/epvp panelreset` | PVP > PANEL > **Reset position** |
| `/epvp despawn` | FIGHT > **Despawn team (cleanup)** |
| `/epvp selftest` | TEST > INSPECT > **Self test** |
| `/epvp validation on` / `off` | TEST > INSPECT > **Detailed validation logging** |
| `/epvp spawnprobe` | TEST > INSPECT > **Spawn probe** |
| `/epvp spawnclone` / `targetclone` / `fightclone` | TEST > ISOLATED CLONE TESTS |
| `/epvp debug` | Chat only, by design: it reveals the hidden tab. TEST > PANEL > **Hide test tab** turns it back off |

Only arranged challenges ever ask permission. The PVP tab states this at each switch, and `/epvp arranged` and `/epvp ambush` repeat it in their replies, so enabling ambushes cannot be mistaken for opting into a prompt.

The optional `[defenderLevel] [attackerLevel]` arguments of `/epvp plan` are chat-only. The simulator uses the policy defaults, which is what the everyday party-size check needs.

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

## Safety boundary

Erenshor remains authoritative for gameplay AI. This mod chooses eligibility and builds bounded encounter actors, but no LLM chooses movement, attacks, spells, healing, targeting, loot, or equipment. The mod never directly edits Erenshor save files. Rewards use native live APIs and normal game saving; records and cooldowns use BepInEx configuration. Unknown actor, scene, or network state fails closed.

Native spawn research is documented in [docs/NATIVE_SPAWN_FINDINGS.md](docs/NATIVE_SPAWN_FINDINGS.md). The game's exposed `SimPlayer` spawners mutate persistent roster state, which is why combat uses a temporary NPC proxy plus disabled visual shell.

## Credits and Inspiration

### Inspiration

- **[Reckss PvP Mod](https://github.com/Reckimus/ErenshorPvP) by Recks (Reckimus)** helped inspire the direction of adding PvP to Erenshor. No public license was ever published for that project, so no code was copied, decompiled, or used as a dependency; this mod is an independent implementation built against the currently installed game assembly.

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal has been to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.

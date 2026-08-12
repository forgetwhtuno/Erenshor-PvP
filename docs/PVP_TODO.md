# Erenshor PvP TODO

This is the working checklist for the standalone PvP mod. It deliberately distinguishes code that exists from behavior that still needs to be observed in Erenshor.

Future Declared Skirmish, rivalry ladder, King of the Hill, and Capture the Flag work is separately scoped in [the PvP Events draft](../../docs/feature-drafts/pvp-events/README.md) and [its TODO](../../docs/feature-drafts/pvp-events/TODO.md). Those modes must not displace the live acceptance work below.

## 1. Live acceptance testing — highest priority

- [x] Add a persisted detailed-validation switch and consolidated per-match evidence for matchmaking, equipment, combat HP flow, rewards, and cleanup; keep core failures/results outside the switch.
- [ ] Run `/epvp selftest` in-game and capture all policy, matchmaking, planner, flavor, and panel-positioning PASS results.
- [ ] Run `/epvp diagnose` in a normal combat zone and confirm the scene, off-map profile count, template source, and protected-zone decision.
- [ ] Test arranged encounters with `/epvp force arranged 1`, `2`, `3`, `4`, and `5`.
- [ ] Confirm every arranged encounter waits for Accept/Refuse and never starts before acceptance.
- [ ] Test ambushes with `/epvp force ambush 1`, `2`, `3`, `4`, and `5` in an allowlisted wild zone.
- [ ] Confirm ambushes start without an Accept prompt and show a motive-aware warning.
- [ ] Confirm ambushes cannot start in cities, tutorials, islands, character selection, or other protected scenes.
- [ ] Confirm `/epvp ambushhere on|off` updates the allowlist and protected scenes cannot be added.
- [ ] Confirm natural ambush timing is uncommon and respects the configured interval/chance.
- [ ] Capture `/epvp team` and `/epvp verify` during an active encounter. Both verifier sections must report PASS.

## 2. Party Tools-style UI

Implemented in the current source, but still needs live verification:

- [ ] Confirm the window uses the same visual language as Party Tools at the game’s actual resolution and UI scale.
- [ ] Confirm F10 opens/closes the panel without moving the camera or changing character movement.
- [ ] Confirm dragging the header works across the full header area and persists after reload.
- [ ] Confirm `/epvp panelreset` restores a visible position below the minimap.
- [ ] Confirm the panel never covers the party/character UI at the supported resolutions.
- [ ] Confirm clicks on labels, empty panel space, tabs, and buttons are consumed by the panel.
- [ ] Confirm the compact map-side toggle can be hidden/shown with the configured option.
- [ ] Confirm the PVP tab’s three switches behave independently: World PvP, Arranged Challenges, and Wild Ambushes.
- [ ] Confirm the FIGHT tab updates roster health and spell counts during combat.
- [ ] Confirm RULES reflects the actual current zone, level range, party-size rules, ambush chance, and timers.
- [ ] Confirm SCORE separates arranged and ambush records and displays reward/cooldown/cosmetic state.
- [ ] Confirm TEST remains hidden unless `/epvp debug` is used.
- [ ] Decide whether a future Party Tools API should host a literal shared tab/window. Current implementation is intentionally standalone while matching Party Tools styling and behavior.

## 3. Native Sim appearance and combat

- [x] Record one bounded `balance_summary` per lethal encounter using actual HP deltas: duration, composition, levels, deaths, side damage, and pet contribution.
- [ ] Confirm each proxy displays the correct Sim nameplate rather than the borrowed creature name.
- [ ] Confirm hair, skin, gender, equipped armor, and held weapons are correct for profiles with saved gear; confirm gearless profiles receive visible deterministic class/level fallback armor and weapons. Native Empty hand slots replace null and fallback items remain encounter-only.
- [ ] Confirm the visual Sim shell animates and follows the native combat body during idle, movement, melee, casting, hit, and death states.
- [ ] Confirm class-derived spell lists load from saved spell IDs and display the expected spell count.
- [ ] Confirm casters cast damage/control spells without borrowing creature-only skills.
- [ ] Confirm healers/support Sims heal only valid members of their attacking party and do not create infinite self-healing.
- [ ] Confirm vanguard/striker/caster/support stat scaling is sensible across the level range.
- [ ] Confirm native party Sims assist against all attackers and never damage one another.
- [ ] Confirm player and party-Sim pets report under `defender_pets`, damage only PvP attackers, can receive attacker damage, and release aggro on cleanup. Ownership admission and containment policy are implemented and self-tested.
- [ ] Confirm attackers can retreat/disengage at low health and that retreat grants no victory reward.
- [ ] Confirm an attacker defeat is registered only after every attacker in the team is dead.
- [ ] Confirm unrelated NPCs cannot join, damage, or receive accidental PvP targeting.
- [x] Preserve ordinary player/party combat against world NPCs while keeping proxies out of that combat: a world hit is allowed and ends PvP, proxy/outside cross-targeting is blocked, and verified outside aggression cancels safely.
- [ ] Live-verify clear-area placement: 10m player clearance, 8m per-spawn clearance, complete NavMesh paths, 11m formation, party/pet exclusions, and a useful forced-start refusal near an NPC.
- [x] Confirm encounter-local fallback equipment resolves real native items: live level-12 Reaver evidence found 105 eligible items, selected 10, and passed the consolidated two-proxy equipment count.
- [ ] Confirm scene transitions, death/respawn, manual despawn, and shutdown leave no proxy, spell clone, target, or stale UI state.

## 4. Encounter behavior and rules

- [x] Use the living active defender party's average level consistently for candidate filtering, team construction, and final match validation; expose it in diagnostics and RULES.
- [x] Weight automatic solo encounter sizes so four- and five-attacker extremes are possible but uncommon while preserving exact forced-size testing.
- [ ] Validate solo 1–5 attacker composition in real encounters.
- [ ] Validate duo 1–3 composition and the stronger-level requirement for a lone attacker.
- [ ] Validate trio 3–5 composition.
- [ ] Validate four- and five-person defenders require a five-person attacker party at the intended level.
- [ ] Confirm same-guild preference and role diversity using real profile data.
- [ ] Confirm arranged guild/party dialogue reflects the actual leader guild and party composition.
- [ ] Confirm ambush motives vary between killing spree, territory, guild raid, and verified camp claim.
- [ ] Confirm camp-claim dialogue is impossible without an active Campmaster Hunt Camp.
- [ ] Confirm ambushes are opt-in through the World PvP switch but are not individually consented once enabled.
- [ ] Confirm disabling Arranged Challenges does not disable allowed wild ambushes.
- [ ] Confirm disabling Wild Ambushes does not disable arranged challenges.
- [ ] Confirm PvP never interferes with friendly Practice Duels against local Sims.

## 5. Rewards, records, and progression

- [ ] Verify victory XP is granted once after all attackers die.
- [ ] Verify gold is granted once and scales with opponent level/party risk.
- [ ] Verify player death uses normal Erenshor death, debuffs, and respawn.
- [ ] Verify player death, player flight, attacker retreat, despawn, cancellation, and failed starts grant no victory reward.
- [ ] Verify `/epvp flee` and the FIGHT-tab Flee button end only an active lethal encounter, remove every proxy, and increment Escapes without counting a win or loss.
- [ ] Verify the anti-farm cooldown survives reload and blocks repeated reward claims.
- [ ] Verify arranged and ambush wins/losses appear in the correct SCORE counters.
- [ ] Verify last opponent, last mode, and last result persist in BepInEx configuration.
- [ ] Find and verify a slot-safe native cosmetic unlock API. Direct `TransmogSlots` writes are disabled because a weapon could enter the chest cosmetic position and hide armor.
- [ ] Balance XP, gold, cosmetic chance, and cooldown after several real matches.

## 6. Compatibility and social integration

- [ ] Confirm Deep Sims receives challenge, arranged-result, ambush, retreat, and cancellation events exactly once.
- [ ] Confirm Deep Sims reactions remain short, grounded, and social; no LLM output controls movement, targeting, attacks, spells, or rewards.
- [ ] Confirm the mod remains functional when Deep Sims is absent.
- [ ] Confirm Practice Duels and PvP cannot claim the same local Sim as an opponent.
- [ ] Confirm COOP remains fail-closed and never creates duplicate host/client PvP directors.
- [ ] If COOP PvP is eventually desired, design and verify a host-authoritative network protocol before enabling it.

## 7. Release and maintenance

- [ ] Run this repository's `BUILD_AND_INSTALL.ps1` after every PvP source change.
- [ ] Run the standalone PvP self-tests and the Deep Sims deterministic regression suite.
- [ ] Update the version, changelog, README, and this TODO when behavior changes.
- [ ] Record a clean deployed DLL hash for each release candidate.
- [ ] Retest native reflection hooks after every Erenshor game update.
- [ ] Package the plugin with a first-run configuration explanation and the in-game test commands.
- [ ] Do not mark the mod fully complete until the live acceptance items above have captured evidence in the BepInEx log.

# Erenshor PvP 0.4.0 completion audit

This audit separates build/static evidence from behavior that must be observed inside Erenshor. Compilation alone is not accepted as proof of native Unity behavior.

## Proven by source and deployed build

| Requirement | Evidence |
|---|---|
| Standalone plugin | `forgetwhtuno.erenshor.pvp` 0.4.0 builds without a Deep Sims reference; the optional bridge is reflection-only. |
| Consent model | Arranged matches create a 30-second Accept/Refuse offer. Ambushes bypass the offer only after world PvP is enabled and only inside the exact ambush allowlist. |
| Ambush cadence/motives | Natural ambush opportunities use configurable randomized 15-35 minute intervals and a separate chance roll. Pure tests prove arranged lines request consent, ambush lines do not, and camp claims require verified Hunt Camp context. |
| Off-map identity | `PvpController.TrySelectOffMap` excludes every active `SimPlayer` tracking object and every profile whose native `SimPlayerTracking.CurScene` matches the current scene before snapshot selection. |
| No persistent temporary Sim | Combat roots are native NPC proxies; visual Sim components are disabled and `LoadAllSimData` is suppressed during visual construction. |
| Party rules | Pure policy/planner tests pass for solo 1-5 distribution, duo lone-attacker protection, full-party requirements, guild preference, and role diversity. |
| Zone rules | Character selection, tutorials, cities/hubs, Port Azure, Stowaway's Step, and every island scene are hard protected; exact configurable lists remain available. |
| Containment | Harmony damage/aggro guards keep proxies and outside NPCs out of one another's combat. Ordinary player/party world attacks remain legal and end the separate PvP encounter; verified third-party aggression cancels safely. Installed assembly signatures confirm attacker positions 3/3/2 and `_fromPlayer` position 1 for physical/magic/bleed damage. |
| Rewards/records | Rewards occur only after all proxy actors are dead, use native XP/inventory APIs, reduce XP for low-risk matches while enforcing the configured XP cap, scale gold by party/level risk, and use a persisted cooldown. Wins/losses/escapes persist in BepInEx config. |
| Cleanup | Manual despawn, failure, result, shutdown, and every scene transition destroy all proxy objects and cloned spells. |
| UI/commands | Party Tools-style PVP/FIGHT/RULES/SCORE tabs, hidden TEST tab, map-side toggle, movable/clamped/persisted panel, exact arranged/ambush force commands, `/epvp team`, `/epvp diagnose`, `/epvp verify`, and `/epvp panelreset` compile in the deployed DLL. |
| Deployed build candidate | 2026-08-11 one-click root build/install to the selected r2modman profile completed successfully. SHA-256: `9D504644C734EEBEF5CF0F141829843EBABD18880636CE1748A9E27628D9DF86`. |
| Deployed pure self-tests | The prior installed candidate passed `PvpPolicy`, `PvpMatchmakingPolicy`, `PvpTeamPlanner`, `PvpEncounterFlavorFactory`, `PvpRewardService`, and `PvpPanelPositioning` self-tests on 2026-08-11. The current candidate adds defender-average and weighted-size cases and still requires one `/epvp selftest` capture in game. These cover rules, reward bounds, and panel math only, not live Unity combat. |
| Companion build | Root `BUILD_AND_INSTALL.ps1` successfully built and installed Deep Sims, Follow, Practice Duels, PvP, Party Tools, and Campmaster in one run. |
| Deep Sims bridge | Deployed assembly lookup resolves `ErenshorDeepSims.PvpEventBridge`; its public six-string signature matches the PvP publisher. |
| Deep Sims regression safety | The standalone Deep Sims deterministic regression suite passed `222/222` tests after the deployed one-click build on 2026-08-11. |

## Required live acceptance run

Perform this in a non-protected combat zone:

```text
/epvp on
/epvp selftest
/epvp diagnose
/epvp force arranged 1
/epvp team
/epvp accept
/epvp verify
```

Repeat `/epvp force arranged 2` through `5`, then in an allowed ambush scene repeat `/epvp force ambush 1` through `5`, completing or cleaning up each encounter before the next.

Acceptance evidence:

- `selftest` reports all PASS.
- `diagnose` reports ready, unprotected, not zoning, not co-op, at least the requested number of off-map profiles, and a live/resource template.
- The pending composition contains the requested number and sensible level/class/role values.
- Arranged mode waits for explicit acceptance; ambush mode displays motive-aware warning/chat and starts without an Accept prompt.
- `/epvp diagnose` reports `ambush_allowed=true` only for exact configured wild zones and never for protected scenes.
- Every attacker looks like a Sim, has equipment where its profile supplies equipment, and displays the correct nameplate rather than the borrowed creature.
- `/epvp verify` reports both `VERIFY PASS` and `COMBAT VERIFY PASS`.
- Melee attackers can damage and be damaged; profiles with admitted spells show nonzero spell counts and use native spells without healing indefinitely.
- Player party Sims assist normally and do not damage one another.
- Unrelated NPCs neither join nor damage participants; entering ordinary hostile combat cancels safely.
- Victory occurs only after every attacker dies; low-health retreat grants no reward.
- Player defeat uses normal Erenshor death, debuff, and respawn behavior.
- Victory grants one XP/gold award; XP never exceeds the configured fraction of a level, and a second victory inside the cooldown grants none.
- Cosmetic rewards remain disabled and do not write `TransmogSlots`; a slot-safe native unlock API is required before restoring them.
- F10 panel drag persists and does not reset; map toggle remains usable without covering party/character UI.
- PVP/FIGHT/RULES/SCORE tabs remain readable and update while the encounter changes state; TEST stays hidden until `/epvp debug`.
- Scene transition and `/epvp despawn` leave no proxy, target, cloned spell, or stale pending offer.
- When Deep Sims is installed, challenge/result facts may cause at most a normal bounded social reaction and never a gameplay action.

## Current evidence gap

The source and deployed 0.4.0 build now include the Party Tools-style panel and the
arranged/ambush test controls, but those controls still require a live in-game run.
The live 2026-08-11 runs confirmed visible Sim animation, lethal damage, deaths, team
completion, borrowed-mob reward suppression, class spell population, and meaningful
combat pressure. A level-12 solo 1v1 ended in player victory after 20.0 seconds; a
level-12 solo 2v1 ended in player death after 19.7 seconds. Saved equipment rendered,
while the first fallback-equipment candidate returned zero items and failed verification;
the next candidate merges all verified native item collections and logs its filter counts.
Earlier runs also exposed missing held weapons and an unsafe cosmetic-slot write; the
current source fixes the null hand-slot path and keeps cosmetics disabled.
The merged fallback selector is now live-proven, including a ten-piece level/class fallback.
A later run identified an outgoing area-effect containment gap: Moonburst reached local NPC
Liam Kilfa, whose retaliatory damage was blocked before the encounter cancelled. The current
candidate preserves ordinary player/party attacks against world NPCs but ends PvP when that
happens; proxies and outside NPCs remain mutually excluded. New offer/spawn placement requires
a clear navigable arena away from unrelated NPCs. The remaining evidence gap is the revised
outside-actor boundary and clear placement, pet contribution, party-average selection, party assistance,
normal death consequences, reward persistence,
third-party aggro containment, and the panel's drag,
clamping, persistence, and click-capture behavior. Capture the acceptance run above
before calling the release behavior complete.

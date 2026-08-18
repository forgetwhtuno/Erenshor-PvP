# PvP temporary NPC runtime live acceptance

Installed build: `ErenshorPvP.dll` SHA-256
`0BA0642D44D70EF49718CFC13AE3AA8F96B3D39E493CA33EBD1877D423207ACA`.

Run in game and inspect `lunaris.log`:

1. Enable PvP and start a 5v5 match. Confirm five `proxy_runtime_state` lines
   report `requiredRuntimeState=PASS` and each reports a `template_runtime_state`.
2. Let combat run for at least 60 seconds if it lasts that long. Confirm zero
   `NPC.HandleMaintenaceAndCounters` NREs and normal attack/cast/move behavior.
3. Complete the match. Confirm terminal cleanup, no remaining
   `PvP_TemporaryClone_*`, one reward marker, XP and gold awarded once, and
   `borrowed_death_rewards_suppressed` remains present.
4. Start a second match and repeat the runtime-state check.
5. Cancel a match, then zone around/after a match if safe, and disable PvP.
   Confirm all proxies are removed in each case.
6. If a runtime invariant is deliberately/accidentally broken, expect one
   `proxy_runtime_invalid ... action=cancel_and_cleanup` line, then match
   cleanup; never a repeating stack trace. Verify unrelated native NPCs keep
   updating normally.

This document is a checklist, not a claim that an interactive 60-second match
was run from this noninteractive workspace.

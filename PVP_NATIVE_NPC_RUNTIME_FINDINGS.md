# PvP temporary NPC native-runtime findings

Date: 2026-08-15. Native evidence was read from the installed
`<Erenshor>\Erenshor_Data\Managed\Assembly-CSharp.dll`
(timestamp 2026-08-07 17:04:02).

## Exact failure

The newest `lunaris.log` proves the five `live:A Brittle Skeleton` proxies
passed spawn and combat setup, then each logged `proxy_native_start action=bypass`
and immediately flooded `NPC.HandleMaintenaceAndCounters()` NREs through
`NPC.Update()`.

Installed IL shows `NPC.Update()` calls `HandleMaintenaceAndCounters()` before
normal aggro/AI handling. The maintenance method has these update-time dereferences:

- `NPC.MyStats.Invisible` is unconditional (IL `0x01A0`): `NPC.MyStats` is an
  exact required reference.
- When `CurrentAggroTarget` exists, it reads `NPC.Myself.Alive` (IL `0x034C`)
  and calls `NPC.NameFlash.Flash(...)` (IL `0x035A`): both are required for a
  fighting proxy.
- The raid branch dereferences `MyRaidSlot` and `Myself.MyCharmedNPC` only when
  `MyRaidSlot` is non-null. PvP proxies explicitly clear the raid slot.

`NPC.Start()` normally binds `Myself` via `GetComponent<Character>()`, `MyNav`,
`MyStats`, and `MySpells`; it then creates/binds a nameplate and obtains
`NameFlash` from that nameplate. The prior PvP invariant checked the separate
`Character.MyStats`, but did not check these private NPC runtime fields. A
proxy could therefore report `invariant=pass` while native maintenance was not
safe. This is category B/F: the live-source clone had its own `NPC.Start`
bypassed after conversion, without an equivalent complete maintenance-state
binding.

## Repair

`PvpTemporaryCloneFactory` now explicitly establishes the exact safe subset of
the native `Start` runtime state without replaying borrowed NPC identity setup:

- binds NPC `Myself`, `MyStats`, `MyNav`, `MySpells`, and `MyCharControl` to the
  cloned root/components;
- clears `MyRaidSlot`;
- finds/binds `NameFlash` from the existing cloned nameplate/component graph;
- validates those NPC-owned references before spawn succeeds and again before
  native maintenance runs.

The normal live NPC template logs a small comparator record:
`template_runtime_state` reports only presence of `Myself`, `MyStats`, `MyNav`,
`MySpells`, `NameFlash`, and raid slot. Each PvP clone logs
`proxy_runtime_state requiredRuntimeState=PASS/FAIL; missing=...`.

The new Harmony prefix targets only `NPC.HandleMaintenaceAndCounters` for a
positive `TeamClones` membership match. Vanilla NPCs return `true` immediately
and are never intercepted. If a registered PvP proxy becomes invalid later,
the prefix logs one `proxy_runtime_invalid`, ends the PvP match, skips that
single invalid maintenance invocation, and queues idempotent factory despawn on
the next tick. It does not swallow exceptions globally or alter `NPC.Update`.

## Rewards

`PvpRewardService` remains unchanged: exact-once marker, `GameData.AddExperience
(xp, false)`, gold increment, and inventory update are preserved. Borrowed
native death rewards continue to be re-suppressed immediately before death.

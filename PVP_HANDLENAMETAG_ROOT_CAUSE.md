# PvP — HandleNameTag NRE / inert-proxy root cause (proven)

Evidence base: current installed `Assembly-CSharp.dll` (IL, read via Mono.Cecil), current local
`mods/Erenshor-PvP` source, and live `lunaris.log` from the 2026-08-17 arranged 5v5 vs
Eron / Arty / Vaelora / Blake / Blademann.

## 1. Installed plugin identity (duplicate ID resolved)

Three DLLs share the assembly identity `ErenshorPvP`, **all inside the Lunaris scan root**
(`<Erenshor>/plugins`). Two are backups that were left with a live `.dll` extension, which is why
startup logs `Plugin already loaded: 'ErenshorPvP'` exactly twice (lunaris.log lines 25-26).

| Relative path | Plugin version | SHA-256 | Verdict |
|---|---|---|---|
| `plugins\ErenshorPvP.dll` | 0.5.3 | `97b7cf8c4bc77f3fb60733dca036b8693480a05c4918b873cdf463ae9f6383f2` | won startup (log line 47), but STALE vs source |
| `plugins\ErenshorPvP.pre-native-runtime-20260815-224527.dll` | 0.5.2 | `c29f852b2a9e4e26026175d50103a70bdce260117bae3eccc66d4b4f6c398bb2` | stale duplicate, quarantine |
| `plugins\ErenshorPvP.pre-native-runtime-final-20260815-224618.dll` | 0.5.2 | `a90126937ceb20407352df7f9e3629e009e375402a90f35cfd8a4aabcdd12479` | stale duplicate, quarantine |

All three report `AssemblyVersion 0.0.0.0` (the PvP project sets no assembly version attribute), so
Lunaris' duplicate detection keys on the assembly/plugin name `ErenshorPvP`, not on a version.

Current local source declares `0.5.4` (`src/ErenshorPvPPlugin.cs:9`). **The live evidence was
therefore produced by 0.5.3 — one version behind current source.** No PvP DLLs exist in
`.plugins-backups/` or `backup-pre-lunaris/` (verified by recursive scan of the whole game tree).

## 2. `NPC.Update()` ordering (installed IL)

```
IL_0000  if (SimPlayer) HandleAppliedForce()
IL_000F  HandleMaintenaceAndCounters()
IL_0015  HandleNameTag()            <-- throws here
IL_001B  if (NeverAggro) return
IL_0024  EnsureNav()
IL_002A  SetAttackRanges()
IL_0030  HandleMinimapStuff()
IL_0036  HandleOffNavAndLeashing()
IL_003C  HandleEnrage()
IL_0042  ClearInvalidCombatTarget()
IL_0048  ...aggro target validation / ShareAggroTargetWithMyGroup()...
IL_009F  ForceNewAggroTarget()
IL_00A5  SetRelaxState()
IL_00AB  TrackMobsHuntingPlayerGroup()
IL_00B1  if (SimPlayer) return
...remaining combat AI...
```

`NPC.Update()` contains **zero exception handlers** (verified: `Update` handlers=0,
`HandleNameTag` handlers=0, `HandleMaintenaceAndCounters` handlers=0). An NRE at IL_0015 escapes
`Update()`, Unity logs it, and the remainder of that component's `Update` is abandoned — every frame.

## 3. The exact null dependency

`HandleNameTag()` dereferences `NPC::NamePlateTxt` (type `TMPro.TextMeshPro`) via
`callvirt UnityEngine.Behaviour::get_enabled()` in **every** branch of the method (IL_0026, IL_0055,
IL_0066, IL_009B, IL_00CB, IL_00D8, IL_0103, IL_0113, IL_012F, IL_013F, IL_015A, IL_0189, IL_019A,
IL_01CF, ...). A null there is an unavoidable NRE — Unity's overloaded `==` null check does not
protect a `callvirt` on an actually-null reference.

Whole-assembly field-access scan:

| Field | Written by | Read by |
|---|---|---|
| `NamePlateTxt` | **`NPC::Start` only** | **`NPC::HandleNameTag` only** |
| `NamePlateObject` | `NPC::Start` only | `NPC::HandleNameTag`, `NPC::Start` |
| `NamePlate` | `NPC::Start`, `PlayerControl::Start` | many |

## 4. Why PvP proxies specifically fail

1. PvP clones a live scene NPC, registers it as a temporary proxy, and marks it
   `NativeStartBypassEligible` (`PvpTemporaryCloneFactory.cs:168-169`).
2. `PvpProxyStartupPolicy.ShouldRunNativeNpcStart()` returns **false** for a registered temporary
   proxy cloned from a live started NPC — native `NPC.Start()` is deliberately bypassed so the
   borrowed creature identity is not rebuilt (`PvpProxyStartupPolicy.cs:16-23`).
3. `NamePlateTxt` is assigned **only** in `NPC.Start()`. Bypassing Start therefore leaves it null.
4. `ConfigureNativeMaintenanceState()` manually re-binds the Start-owned fields PvP knew about —
   `Myself`, `MyStats`, `MySpells`, `MyNav`, `MyCharControl`, `MyRaidSlot`, `NameFlash`
   (`PvpTemporaryCloneFactory.cs:442-470`) — but **omits `NamePlateTxt` and `NamePlateObject`**.
   `NamePlateTxt` appears **nowhere** in the PvP source (zero hits across `src/*.cs`).
5. The existing invariant validates `hasNameFlash` — the `NameFlash` field of type `FlashUIColors` —
   which is a **different field** from the one `HandleNameTag` actually dereferences. This is why
   telemetry reported `nameFlash=True` and `requiredRuntimeState=PASS` while the proxy was still
   guaranteed to throw.
6. `UpdateNamePlate()` cannot compensate: it early-returns at IL_0019 when `ThisSim == null`, which
   is always the case for a non-persistent PvP proxy (`persistent_sim=false`). PvP's call to it
   (`PvpTemporaryCloneFactory.cs:159`) is additionally wrapped in a silent `catch {}`.

## 5. Consequence — goals 1 and 2 are ONE defect

Because `HandleNameTag()` runs at Update statement #3, before the `NeverAggro` early-out and before
every combat call, the NRE means `EnsureNav`, `SetAttackRanges`, `ClearInvalidCombatTarget`,
`ForceNewAggroTarget`, `SetRelaxState` and all downstream targeting/melee/spell logic **never execute
even once**. This fully accounts for the observed live telemetry:

- proxies stood still (nav/pursuit never evaluated)
- never fought back (no aggro target ever acquired)
- `damage_to_defenders=0`, `heal_checks=0`, `attack_spell_decisions=0`, `spell_starts=0`
- `healing_assessment=heal_ai_not_evaluated`
- ~1,130 `NPC.HandleNameTag` NREs (5 proxies x ~226 frames across the 7.0s match + cleanup)

"Proxy combat AI never evaluates" is **not** an independent bug — it is a direct downstream
consequence of the null `NamePlateTxt`. No target seeding and no custom damage loop is required;
restoring the field should restore the whole native AI path.

`NamePlateTxt` is purely presentational: `HandleNameTag` only toggles `enabled` on it based on
camera distance and current-target state. It has no gameplay effect, so binding it is safe.

## 6. Native countdown lever discovered

`NPC::NeverAggro` is a public bool checked at IL_001B — immediately **after** `HandleNameTag()` and
immediately **before** every combat call. Setting it true keeps a proxy fully inert using native
semantics (maintenance + nameplate still run; all combat/aggro/nav does not), and clearing it at GO
releases native AI in one transition. This is the correct native gate for Preparing/Countdown and
avoids disabling `Update` or simulating AI.

## 7. Existing lifecycle gap

`PvpMatchLifecyclePolicy` states are `Disabled, Ready, PendingChallenge, Spawning, Active, CleaningUp`.
`SpawnSucceeded()` transitions `Spawning -> Active` **directly**; there is no Countdown state and no
GO transition, which is why the live run had no countdown and the defending party could attack
immediately.

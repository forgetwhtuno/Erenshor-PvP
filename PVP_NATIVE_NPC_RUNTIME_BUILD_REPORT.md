# PvP temporary NPC runtime build report

## Verification

`tests/RUN_UI_TESTS.ps1` passed, including new deterministic checks for:

- complete/missing NPC-side maintenance state;
- PvP-only invalid-proxy interception and vanilla fail-open behavior;
- existing reward-boundary checks.

The source guard also confirms the reward service still uses the required XP,
gold, and inventory-update calls.

## Build and installation

Compiled every PvP source file with .NET Framework `csc` against the currently
installed Erenshor managed assemblies and installed `Lunaris.dll`. The existing
local 0Harmony developer reference was used to compile the already-Harmony-based
PvP plugin.

Final installed DLL:

- `<Erenshor>\plugins\ErenshorPvP.dll`
- SHA-256: `0BA0642D44D70EF49718CFC13AE3AA8F96B3D39E493CA33EBD1877D423207ACA`

Backups retained:

- Pre-repair DLL:
  `ErenshorPvP.pre-native-runtime-20260815-224527.dll`
  (`C29F852B2A9E4E26026175D50103A70BDCE260117BAE3ECCC66D4B4F6C398BB2`)
- Intermediate staged repair DLL:
  `ErenshorPvP.pre-native-runtime-final-20260815-224618.dll`
  (`A90126937CEB20407352DF7F9E3629E009E375402A90F35CFD8A4AABCDD12479`)

No Git operation was performed. Interactive in-game acceptance remains the
checklist in `PVP_NATIVE_NPC_RUNTIME_LIVE_TEST.md`.

# Native temporary-Sim spawn findings

Verified against Erenshor's installed `Assembly-CSharp.dll` on 2026-08-11.

`SimPlayerMngr.SpawnMeInPlayerZone(SimPlayerTracking, string, Vector3, bool)` is a persistent-population method, not a temporary encounter factory. It iterates `SimPlayerMngr.Sims`, changes `SimPlayerTracking.CurScene`, writes `SimsInZones[simIndex]`, and adds the result to `ActiveSimInstances`.

`SimPlayerTracking.SpawnMeInGame(Vector3, SimPlayerTracking)` instantiates `GameData.SimMngr.BlankSPTemplate`, but it sets the spawned avatar's group state by indexing `GameData.SimMngr.Sims[avatar.myIndex]`.

`SimPlayer.Start()` independently resolves `MySimTracking` from the same persistent `Sims[myIndex]` list. Therefore a raw clone of the blank template cannot be temporary merely by assigning a new `SimPlayerTracking` after instantiation.

## Consequence

Do not call either native spawn method for PvP clones. Do not append a PvP clone to `SimPlayerMngr.Sims`, `SimsInZones`, or saved Sim data.

The temporary-actor implementation must instead establish an isolated initialization path that prevents the clone's native `SimPlayer` lifecycle from resolving or saving a persistent tracking entry. It must then explicitly own:

- actor metadata and display name;
- AI enablement and target containment;
- virtual health and match state;
- destruction and reference cleanup.

The existing `/epvp spawnprobe` command checks the required factory/template objects live but remains read-only.

The current `/epvp spawnclone` milestone uses the visual template only. It disables the `SimPlayer` component, clears `NPC.ThisSim`, marks `NPC.SimPlayer` false, disables the loot component, and clears clone XP/quest/faction modifiers. It is therefore a temporary PvP proxy mob, not a cloned persistent Sim.

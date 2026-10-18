# Valerius — warning cleanup and CFG

## Changes
- CS0162: replaced `const bool VerboseDiagnostics = false` with a runtime property. Detailed logs remain off by default.
- CS0618: updated `Rigidbody.velocity` to `Rigidbody.linearVelocity` for current Unity.
- CS0649: removed unused `Buffer.Serial`.
- MSB4011: removed redundant DoPrebuild.props import from the supplied csproj copy. The Jotunn package already imports it per your warning.
- MSB3245: **not automatically fixed**; it is produced by the external Jotunn build references. Check the local game assemblies and Jotunn reference paths if it persists.
- Configuration: preserved all user-provided prefab lists, outlier entries, category/collision toggles, 16 m shared-material batching remains hardcoded.

## Installation
1. Back up existing .cs, .csproj and CFG.
2. Replace `ValeriusDungeonBaker.cs` and `ValeriusDynamicSpawner.cs`; add `ValeriusDiagnostics.cs`. These are based on the most recent source snapshots supplied in this conversation. If you've edited the spawner locally since the last upload, merge the simple diagnostic/velocity changes instead of replacing the file.
3. Replace the project .csproj only if it still has the duplicate explicit DoPrebuild import. Preserve any other local project edits.
4. The other `.cs` files are included as a coherent source set. Continue to exclude the old `BlueprintObjExporter.cs`.
5. Replace the BepInEx CFG with the supplied file (or copy its values), then restart Valheim.
6. Run `dotnet build -c Debug`.

**Validation:** syntax/string and structural checks only. No local dotnet or Valheim Unity assemblies were available for full compilation.

**Not addressed:** true shader-gradient snow, prefab position regressions and cloth-door culling.

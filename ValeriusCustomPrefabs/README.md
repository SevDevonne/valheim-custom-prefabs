# Valerius refactor + universal snapping — source release candidate

**Basis:** user's latest `src (2).zip`, not earlier ZIP patches.

## What changed
- `TerrainPrefabs/TerrainCollision.cs` NEW: owns the deformable MeshCollider,
  its piece-layer assignment, frame sync, and mesh updates. The legacy
  `SculptColliderLink` type is preserved here for hoe targeting.
- `HoeSculpt/Sculpt.cs`: retains all sculpting operations and saved-height key,
  but no longer creates/manages colliders. It only calls `collision.RefreshMesh`.
- `TerrainPrefabs/Terrain.cs`: adds TerrainCollision to each terrain prefab.
- `TerrainPrefabs/Snapping.cs`: single shape-independent fallback using
  tagged snap transforms, nearby colliders and camera intent. Only runs for
  Valerius placement ghosts, after Valheim's native ghost update. Limits
  correction to 0.85 m to avoid long-range magnetism. Attempts to defer
  to manual snapping and leaves all geometry unchanged.
- Debug snap markers now sit under the prefab root rather than inactive
  snap-point transforms, so they can actually be visible.

## Install
1. BACK UP your current `src` directory and world.
2. REPLACE your existing `src` directory with the ZIP's `src` directory.
   Do not MERGE older Snapping.cs/Sculpt.cs copies from previous ZIPs.
3. Verify `src/HoeSculpt/Sculpt.cs` exists and
   `src/TerrainPrefabs/TerrainCollision.cs` exists.
4. `dotnet clean` and `dotnet build` on your Windows project.
5. Test in a disposable world before using an existing world.

## Test matrix
1. Floor to floor: auto; Q/E manual.
2. Wall to wall: horizontal adjacency; vertical stacking.
3. Floor to wall and wall to floor.
4. Slope to floor/wall; curved pieces.
5. Valerius piece to vanilla wood piece.
6. Verify existing sculpted heights still load; hoe sculpts and collider follows.
7. Verify no long-range snapping and no change to lava render offsets.

## Verification limits
Static source checks were run (see `STATIC_CHECKS.txt`). This environment
has no Valheim/Jotunn C# build toolchain; this ZIP was NOT compiled and the
universal snapping fallback has NOT been validated in-game. It is a
candidate for your build and tests, not a confirmed snapping fix.

## Known constraints
- The fallback is position-only, using snap transforms; it doesn't rotate
  pieces or infer which face should be adjacent. Native Valheim rotation
  remains in charge. A point-pair can still yield an undesired orientation.
- The Q/E safeguard is conservative: after Q or E it disables the fallback
  for that ghost instance. Changing the selected prefab resets it.
- The `m_manualSnapPoint` field is checked reflectively, but its semantics
  in your exact Valheim build are not confirmed.
- The snap-point coordinates still come from the existing geometry helper;
  lava `renderOffsetY=-0.9` is not automatically reflected in snap points.
- The requested lava wall +0.12 m local horizontal visual correction is
  NOT included: axis/direction has not been established and it is separate
  from this refactor.

## Additional source audit — existing problems not silently changed
1. `TerrainPrefabs/Hammer.cs` still calls `Config.Bind` for shape, rise,
   last-equipped selection. `HoeSculpt/Hoe.cs` persists brush radius and
   strength. This contradicts the earlier in-memory-only preference.
2. The lava mesh is rendered with -0.9 m Y offset, but `Snapping.cs`
   uses the unshifted preview anchor. Snapping/visual mismatch needs an
   explicit geometry policy, not another guessed adjustment.
3. `TerrainPrefabs/Terrain.cs` detaches the terrain render mesh from the
   prefab root at runtime. Its lifecycle and networking should be tested
   across unload/reload and world cleanup.
4. Several whole-world `FindObjectsOfType` scans remain in sculpting and
   terrain material selection. Performance cost may grow with piece count.
5. The debug marker switch remains true in Terrain.cs; disable for release.
6. The generic snap fallback only handles translation, not matching edge
   orientation; the camera-intent heuristics require real in-game testing.

No world data key, prefab ID, geometry builder, hazard setting, dungeon
marker, or hammer UI logic was changed in this candidate.

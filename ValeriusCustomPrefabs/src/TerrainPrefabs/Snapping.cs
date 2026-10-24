using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    public sealed partial class TerrainPiece : MonoBehaviour
    {

        private void GetStructuralCorners(Vector3[] lower, Vector3[] upper)
        {
            float h = footprint * 0.5f;
            float hx = footprint * 0.5f;
            float y = previewAnchorLocal.y + 0.025f;
            float cx = previewAnchorLocal.x, cz = previewAnchorLocal.z;
            for (int i = 0; i < 4; i++)
            {
                float x = (i == 1 || i == 2) ? hx : -hx;
                float z = i >= 2 ? h : -h;
                lower[i] = new Vector3(cx + x, y, cz + z);
                upper[i] = new Vector3(cx + x, y + ProfileHeight(x, z), cz + z);
            }
        }

        public void GetGuidelineSnapPositions(List<Vector3> result)
        {
            if (result == null) return;
            result.Clear();
            var lower = new Vector3[4];
            var upper = new Vector3[4];
            GetStructuralCorners(lower, upper);
            if (IsVerticalWall)
            {
                Vector3[] wallLower, wallUpper;
                GetWallShape(out wallLower, out wallUpper);
                AddSnapUnique(result, wallLower[0]);
                AddSnapUnique(result, wallUpper[0]);
                int last = wallLower.Length - 1;
                AddSnapUnique(result, wallLower[last]);
                AddSnapUnique(result, wallUpper[last]);
                return;
            }
            bool slope = !string.IsNullOrEmpty(geometry) &&
                geometry.StartsWith("slope", StringComparison.Ordinal);
            for (int i = 0; i < 4; i++)
            {
                // Slopes have ONE surface height at each footprint corner.
                // The lower[i] point at a raised corner is beneath the mesh
                // and must not compete with the real crest snap point.
                AddSnapUnique(result, slope ? upper[i] : lower[i]);
            }

        }

        private static void AddSnapUnique(List<Vector3> output, Vector3 point)
        {
            for (int i = 0; i < output.Count; i++)
                if ((output[i] - point).sqrMagnitude < 0.000025f) return;
            output.Add(point);
        }

        public float SnapProfileHeight(float localX, float localZ)
        {
            return ProfileHeight(localX, localZ);
        }
    }
    // Prefab snap points are shared by native snapping and the universal fallback.
    internal static class TerrainSnaps
    {
        internal static void InstallSnapPoints(GameObject prefab, TerrainPiece visual)
        {
            // Collect before destroying: mutating the hierarchy during enumeration is unsafe.
            var inherited = new List<GameObject>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (t != prefab.transform && t.CompareTag("snappoint"))
                    inherited.Add(t.gameObject);

            foreach (GameObject item in inherited)
                UnityEngine.Object.DestroyImmediate(item);

            var positions = new List<Vector3>();
            visual.GetGuidelineSnapPositions(positions);
            for (int i = 0; i < positions.Count; i++)
            {
                var snap = new GameObject("Corner " + (i + 1));
                snap.transform.SetParent(prefab.transform, false);
                snap.transform.localPosition = positions[i];
                snap.tag = "snappoint";
                snap.SetActive(false);
            }
        }

        internal static void ShowDebugSnapMarkers(GameObject prefab)
        {
            foreach (Transform snap in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (snap == prefab.transform || !snap.CompareTag("snappoint")) continue;

                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "DEBUG_SnapMarker";
                // Markers must not be children of inactive snap transforms.
                marker.transform.SetParent(prefab.transform, false);
                marker.transform.localPosition = snap.localPosition;
                marker.transform.localScale = Vector3.one * 0.12f;
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().material.color = Color.magenta;
            }
        }
    }

    // One geometric rule for every terrain shape: align two actual snap points.
    // No floor/wall/slope-specific placement cases.
    // Native Valheim placement runs first; this is a bounded fallback for our
    // custom terrain pieces, not a replacement for Valheim's manual selection.
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class TerrainUniversalSnapPatch
    {
        private static readonly System.Reflection.FieldInfo GhostField =
            AccessTools.Field(typeof(Player), "m_placementGhost");
        private static readonly System.Reflection.FieldInfo ManualField =
            AccessTools.Field(typeof(Player), "m_manualSnapPoint");
        private static readonly Collider[] Nearby = new Collider[512];
        // Match the ordering of Valheim's Piece.GetSnapPoints() exactly.
        private static readonly List<Transform> NativeSourceSnapPoints = new List<Transform>(8);
        // Keep lateral capture tight, but allow reaching a 4m slope crest.
        private const float MaxHorizontalCorrection = 2.85f;
        private const float MaxHorizontalCorrectionAimed = 2.85f;
        private const float MaxVerticalCorrection = 4.50f;
        // Projected-screen selection works at any viewing angle / slope height.
        private const float AimedSnapScreenRadius = 0.09f;
        private const float AimRadiusOffPiece = 0.09f;

        // Confirmed against the user's assembly_valheim.dll:
        // Player.m_manualSnapPoint is an int: -1 = Auto, 0..N-1 = selected
        // source snap point. Piece.GetSnapPoints(List<Transform>) enumerates
        // direct children tagged "snappoint" in hierarchy order.
        private static int GetManualSnapIndex(Player player)
        {
            if (ManualField == null || ManualField.FieldType != typeof(int)) return -1;
            return (int)ManualField.GetValue(player);
        }

        private static void CollectPoints(Transform root, List<Transform> points)
        {
            points.Clear();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.CompareTag("snappoint")) points.Add(child);
        }

        private static float ScreenDistanceFromAim(Camera camera, Vector3 worldPoint)
        {
            Vector3 projected = camera.WorldToViewportPoint(worldPoint);
            if (projected.z <= 0f) return float.PositiveInfinity;
            // Scale X by aspect so the tolerance is circular in screen pixels.
            float dx = (projected.x - 0.5f) * camera.aspect;
            float dy = projected.y - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Piece FindTargetPiece(Collider collider)
        {
            if (collider == null) return null;
            Piece piece = collider.GetComponentInParent<Piece>();
            if (piece != null) return piece;
            SculptColliderLink link = collider.GetComponent<SculptColliderLink>();
            return link != null && link.owner != null
                ? link.owner.GetComponent<Piece>() : null;
        }

        // A slope's side is an EDGE, not just an isolated corner. Only
        // accept an edge connection if both endpoints coincide after moving
        // the ghost; this avoids the one-corner false matches seen on slopes.
        private static readonly int[,] FloorEdges = new int[,]
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }
        };

        // GetGuidelineSnapPositions orders wall points as
        // bottom-start, top-start, bottom-end, top-end.
        private static readonly int[,] WallEdges = new int[,]
        {
            { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 }
        };

        private static float ScreenDistanceToEdge(Camera camera, Vector3 a, Vector3 b)
        {
            Vector3 pa = camera.WorldToViewportPoint(a);
            Vector3 pb = camera.WorldToViewportPoint(b);
            if (pa.z <= 0f || pb.z <= 0f) return float.PositiveInfinity;
            Vector2 va = new Vector2((pa.x - 0.5f) * camera.aspect, pa.y - 0.5f);
            Vector2 vb = new Vector2((pb.x - 0.5f) * camera.aspect, pb.y - 0.5f);
            Vector2 ab = vb - va;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0.000001f ? Mathf.Clamp01(-Vector2.Dot(va, ab) / len2) : 0f;
            return (va + ab * t).magnitude;
        }

        private static bool IsSlope(TerrainPiece piece)
        {
            return piece != null && !string.IsNullOrEmpty(piece.geometry) &&
                piece.geometry.StartsWith("slope", StringComparison.Ordinal);
        }

        private static void TryEdgeSnaps(
            TerrainPiece sourceTerrain, TerrainPiece targetTerrain,
            List<Transform> sourcePoints, List<Transform> targetPoints,
            Camera camera, bool aimed, ref float best, ref Vector3 correction)
        {
            // Preserve established floor-to-wall behaviour. This edge rule
            // addresses slope joins, including slope-to-floor and slope-to-wall.
            if (!IsSlope(sourceTerrain) && !IsSlope(targetTerrain)) return;
            if (sourcePoints.Count != 4 || targetPoints.Count != 4) return;

            int[,] sourceEdges = sourceTerrain.IsVerticalWall ? WallEdges : FloorEdges;
            int[,] targetEdges = targetTerrain.IsVerticalWall ? WallEdges : FloorEdges;
            for (int t = 0; t < 4; t++)
            {
                Vector3 ta = targetPoints[targetEdges[t, 0]].position;
                Vector3 tb = targetPoints[targetEdges[t, 1]].position;
                float aimDistance = ScreenDistanceToEdge(camera, ta, tb);
                if (aimDistance > (aimed ? 0.10f : 0.075f)) continue;

                for (int a = 0; a < 4; a++)
                {
                    Vector3 sa = sourcePoints[sourceEdges[a, 0]].position;
                    Vector3 sb = sourcePoints[sourceEdges[a, 1]].position;
                    if (!sourceTerrain.IsVerticalWall && !targetTerrain.IsVerticalWall)
                    {
                        // Adjacent footprints need OPPOSING outward edge
                        // directions. Otherwise an edge match can place a
                        // duplicate directly over the existing piece.
                        Vector2 se = new Vector2(sb.x - sa.x, sb.z - sa.z);
                        Vector2 te = new Vector2(tb.x - ta.x, tb.z - ta.z);
                        if (se.sqrMagnitude < 0.0001f || te.sqrMagnitude < 0.0001f ||
                            Vector2.Dot(se.normalized, te.normalized) > -0.5f)
                            continue;
                    }
                    for (int reverse = 0; reverse < 2; reverse++)
                    {
                        Vector3 deltaA = (reverse == 0 ? ta : tb) - sa;
                        Vector3 deltaB = (reverse == 0 ? tb : ta) - sb;
                        // Require matching length AND slope: both ends must
                        // align. A lone corner is not a valid side join.
                        if ((deltaA - deltaB).sqrMagnitude > 0.015625f) continue;
                        Vector3 delta = (deltaA + deltaB) * 0.5f;
                        float horizontal = new Vector2(delta.x, delta.z).magnitude;
                        float vertical = Mathf.Abs(delta.y);
                        if (horizontal > MaxHorizontalCorrection ||
                            vertical > MaxVerticalCorrection) continue;
                        // Prefer a valid full-edge connection over a single
                        // corner; target aim and translation still matter.
                        float score = aimDistance * 5f + horizontal * 0.28f +
                            vertical * 0.055f + (aimed ? 0f : 0.20f) - 0.65f;
                        if (score >= best) continue;
                        best = score;
                        correction = delta;
                    }
                }
            }
        }

        private static void Postfix(Player __instance)
        {
            if (__instance == null || __instance != Player.m_localPlayer ||
                GhostField == null || Camera.main == null) return;
            GameObject ghost = GhostField.GetValue(__instance) as GameObject;
            if (ghost == null || ghost.GetComponent<TerrainPiece>() == null) return;

            // Search by physical proximity AND camera intent, never by a
            // long-range camera ray alone (which caused the distant magnetism).
            var sourcePoints = new List<Transform>(8);
            CollectPoints(ghost.transform, sourcePoints);
            if (sourcePoints.Count == 0) return;

            int manualIndex = GetManualSnapIndex(__instance);
            bool manual = manualIndex >= 0;
            if (manual)
            {
                // Native Q/E selection uses the *source* point index, not the
                // closest corner. Use the same Piece.GetSnapPoints ordering.
                Piece ghostPiece = ghost.GetComponent<Piece>();
                if (ghostPiece == null) return;
                NativeSourceSnapPoints.Clear();
                ghostPiece.GetSnapPoints(NativeSourceSnapPoints);
                if (manualIndex >= NativeSourceSnapPoints.Count) return;
                Transform selected = NativeSourceSnapPoints[manualIndex];
                sourcePoints.Clear();
                sourcePoints.Add(selected);
            }

            Ray aim = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Use the first actual piece hit by the cursor, not every piece
            // behind it. Otherwise a distant piece can steal the snap.
            Piece directlyAimed = null;
            RaycastHit[] hits = Physics.RaycastAll(aim, 12f, ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Piece hitPiece = FindTargetPiece(hit.collider);
                if (hitPiece == null || hitPiece.gameObject == ghost) continue;
                directlyAimed = hitPiece;
                break;
            }

            int pieceLayer = LayerMask.NameToLayer("piece");
            int nonSolid = LayerMask.NameToLayer("piece_nonsolid");
            int mask = pieceLayer >= 0 ? 1 << pieceLayer : 0;
            if (nonSolid >= 0) mask |= 1 << nonSolid;
            // Deformable lava/terrain colliders may live on Default rather
            // than the vanilla piece layers. FindTargetPiece still filters
            // candidates back to real Piece owners.
            int defaultLayer = LayerMask.NameToLayer("Default");
            if (defaultLayer >= 0) mask |= 1 << defaultLayer;
            if (mask == 0) return;
            // A 4m rise plus the piece footprint can exceed 4.5m from the
            // ghost origin even when the snap points are close in screen space.
            int count = Physics.OverlapSphereNonAlloc(ghost.transform.position,
                7.0f, Nearby, mask, QueryTriggerInteraction.Ignore);
            // Select the corner the player is AIMING AT before selecting a
            // source corner. Edge snapping is only a fallback when the crosshair
            // is not on an eligible corner. This prevents edge scores from
            // stealing deliberate corner-to-corner placements.
            float bestCornerAim = float.PositiveInfinity;
            float bestCornerMove = float.PositiveInfinity;
            Vector3 cornerCorrection = Vector3.zero;
            bool foundCorner = false;

            float bestEdge = float.PositiveInfinity;
            Vector3 edgeCorrection = Vector3.zero;
            var visited = new HashSet<Piece>();
            var targetPoints = new List<Transform>(8);
            TerrainPiece sourceTerrain = ghost.GetComponent<TerrainPiece>();
            for (int i = 0; i < count; i++)
            {
                Piece target = FindTargetPiece(Nearby[i]);
                if (target == null || target.gameObject == ghost || !visited.Add(target)) continue;
                CollectPoints(target.transform, targetPoints);
                bool aimed = target == directlyAimed;
                TerrainPiece targetTerrain = target.GetComponent<TerrainPiece>();
                // Manual Q/E chooses a single source corner. Edge matching
                // would override that choice, so retain edges for Auto only.
                if (!manual && targetTerrain != null)
                    TryEdgeSnaps(sourceTerrain, targetTerrain, sourcePoints,
                        targetPoints, Camera.main, aimed, ref bestEdge, ref edgeCorrection);

                foreach (Transform dest in targetPoints)
                {
                    // Target selection is based ONLY on where the player aims.
                    // Do not mix source movement into the target-corner score.
                    float aimDistance = ScreenDistanceFromAim(Camera.main, dest.position);
                    if (aimDistance > (aimed ? AimedSnapScreenRadius : AimRadiusOffPiece))
                        continue;

                    // For the selected target, choose the source corner that
                    // requires the smallest displacement. Compare 3D distances,
                    // not just horizontal movement; crest corners must count.
                    float sourceMove = float.PositiveInfinity;
                    Vector3 sourceCorrection = Vector3.zero;
                    foreach (Transform src in sourcePoints)
                    {
                        Vector3 delta = dest.position - src.position;
                        float horizontal = new Vector2(delta.x, delta.z).magnitude;
                        float vertical = Mathf.Abs(delta.y);
                        if (horizontal > (aimed ? MaxHorizontalCorrectionAimed : MaxHorizontalCorrection) ||
                            vertical > MaxVerticalCorrection) continue;
                        float move = delta.sqrMagnitude;
                        if (move >= sourceMove) continue;
                        sourceMove = move;
                        sourceCorrection = delta;
                    }
                    if (float.IsPositiveInfinity(sourceMove)) continue;

                    // Prefer the directly aimed piece, then the target corner
                    // closest to the crosshair, then the shortest source move.
                    float targetAim = aimDistance + (aimed ? 0f : 0.20f);
                    if (foundCorner &&
                        (targetAim > bestCornerAim + 0.0001f ||
                         (Mathf.Abs(targetAim - bestCornerAim) <= 0.0001f &&
                          sourceMove >= bestCornerMove))) continue;
                    foundCorner = true;
                    bestCornerAim = targetAim;
                    bestCornerMove = sourceMove;
                    cornerCorrection = sourceCorrection;
                }
            }

            Vector3 correction = foundCorner ? cornerCorrection : edgeCorrection;
            bool hasSnap = foundCorner || !float.IsPositiveInfinity(bestEdge);
            if (hasSnap && correction.sqrMagnitude > 0.000001f)
                ghost.transform.position += correction;
        }
    }

}

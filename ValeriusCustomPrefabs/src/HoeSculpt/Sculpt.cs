using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    internal enum SculptMode { Raise, Lower, Smooth }

    public sealed class SculptTile : MonoBehaviour
    {
        private int VertexCount { get { return visual != null && visual.SculptMesh != null ? visual.SculptMesh.vertexCount : 0; } }
        private HashSet<int> boundary;
        private static long EdgeKey(int a, int b)
        {
            int low = Math.Min(a, b), high = Math.Max(a, b);
            return ((long)low << 32) | (uint)high;
        }
        private void BuildBoundary()
        {
            boundary = new HashSet<int>();
            int[] tris = visual.SculptMesh.triangles;
            var occurrences = new Dictionary<long, int>();
            for (int i = 0; i < tris.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    long key = EdgeKey(tris[i + k], tris[i + (k + 1) % 3]);
                    int count;
                    occurrences.TryGetValue(key, out count);
                    occurrences[key] = count + 1;
                }
            foreach (var pair in occurrences)
                if (pair.Value == 1)
                {
                    boundary.Add((int)(pair.Key >> 32));
                    boundary.Add((int)(pair.Key & 0xffffffffL));
                }
        }
        // Saved height key must remain stable.
        private const string DataKey = "VH_EditableHeights_v1";
        private TerrainPiece visual;
        private ZNetView nview;
        private TerrainCollision collision;
        private float[] heights;
        private float[] baseHeights;
        private string lastData;
        private float nextPoll;
        private bool initialized;

        private void Awake()
        {
            visual = GetComponent<TerrainPiece>();
            nview = GetComponent<ZNetView>();
            collision = GetComponent<TerrainCollision>();
        }

        private void Update()
        {
            if (nview == null || !nview.IsValid() || nview.GetZDO() == null ||
                visual == null || !visual.SculptReady) return;
            if (!initialized)
            {
                heights = new float[VertexCount];
                baseHeights = new float[VertexCount];
                Vector3[] initial = visual.SculptMesh.vertices;
                for (int i = 0; i < heights.Length; ++i)
                    heights[i] = baseHeights[i] = initial[i].y;
                BuildBoundary();
                initialized = true;

            }
            if (Time.time < nextPoll) return;
            nextPoll = Time.time + 0.25f;
            string data = nview.GetZDO().GetString(DataKey, "");
            if (data != lastData)
            {
                lastData = data;
                Decode(data);
                ApplyMesh();
                RefreshSharedSeamNormals();
            }
        }

        public void Sculpt(Vector3 worldPoint, float delta)
        {
            SculptConnected(this, worldPoint, delta);
        }

        private struct GridPoint
        {
            public SculptTile tile;
            public int index;
            public Vector3 world;
        }

        private static long VertexKey(Vector3 world)
        {
            long x = (long)Math.Round(world.x * 1000.0);
            long z = (long)Math.Round(world.z * 1000.0);
            return (x << 32) ^ (z & 0xffffffffL);
        }

        private Vector3 GetWorldVertex(int index)
        {
            Vector3 local = visual.SculptMesh.vertices[index];
            local.y = heights[index];
            return visual.SculptTransform.TransformPoint(local);
        }

        private bool CompatibleNeighbour(SculptTile other)
        {
            if (other == null || other == this || !other.initialized ||
                !other.visual.SculptReady || boundary == null || other.boundary == null) return false;
            int matching = 0;
            foreach (int a in boundary)
            {
                Vector3 va = GetWorldVertex(a);
                foreach (int b in other.boundary)
                {
                    Vector3 vb = other.GetWorldVertex(b);
                    if ((new Vector2(va.x - vb.x, va.z - vb.z)).sqrMagnitude > 0.000004f) continue;
                    if (Mathf.Abs(va.y - vb.y) > 0.20f) continue;
                    if (++matching >= 2) return true;
                }
            }
            return false;
        }

        public static void SculptConnected(SculptTile target, Vector3 centre, float delta)
        {
            SculptConnected(target, centre, Mathf.Abs(delta), 0.9f,
                delta >= 0f ? SculptMode.Raise : SculptMode.Lower);
        }

        internal static bool SculptConnected(SculptTile target, Vector3 centre,
            float strength, float radius, SculptMode mode)
        {
            if (target == null || !target.initialized || !target.visual.SculptReady) return false;
            radius = Mathf.Clamp(radius, 0.5f, 8f);
            strength = Mathf.Clamp(strength, 0.01f, 1f);
            var nearby = new List<SculptTile>();
            foreach (var tile in UnityEngine.Object.FindObjectsOfType<SculptTile>())
            {
                if (!tile.initialized || !tile.visual.SculptReady || tile.nview == null ||
                    !tile.nview.IsValid() || tile.nview.GetZDO() == null) continue;
                float range = radius + tile.visual.footprint + 1f;
                if ((new Vector2(tile.transform.position.x - centre.x,
                                 tile.transform.position.z - centre.z)).sqrMagnitude > range * range) continue;
                nearby.Add(tile);
            }
            var connected = new List<SculptTile> { target };
            for (int i = 0; i < connected.Count; i++)
                foreach (var candidate in nearby)
                    if (!connected.Contains(candidate) && connected[i].CompatibleNeighbour(candidate))
                        connected.Add(candidate);

            var grouped = new Dictionary<long, List<GridPoint>>();
            foreach (var tile in connected)
                for (int index = 0; index < tile.VertexCount; index++)
                {
                    Vector3 world = tile.GetWorldVertex(index);
                    if ((new Vector2(world.x - centre.x, world.z - centre.z)).sqrMagnitude > radius * radius)
                        continue;
                    long key = VertexKey(world);
                    List<GridPoint> points;
                    if (!grouped.TryGetValue(key, out points))
                        grouped[key] = points = new List<GridPoint>();
                    points.Add(new GridPoint { tile = tile, index = index, world = world });
                }

            var modified = new HashSet<SculptTile>();
            foreach (var pair in grouped)
            {
                var points = pair.Value;
                if (points.Count == 0) continue;
                Vector3 pos = points[0].world;
                float distance = Vector2.Distance(new Vector2(pos.x, pos.z),
                                                  new Vector2(centre.x, centre.z));
                float weight = Mathf.Clamp01(1f - distance / radius);
                weight = weight * weight * (3f - 2f * weight);
                float baseline = 0f;
                foreach (var point in points) baseline += point.world.y;
                baseline /= points.Count;
                float desiredY;
                if (mode == SculptMode.Smooth)
                {
                    float sum = 0f;
                    int count = 0;
                    foreach (var nearbyGroup in grouped.Values)
                    {
                        Vector3 sample = nearbyGroup[0].world;
                        if ((new Vector2(sample.x - pos.x, sample.z - pos.z)).sqrMagnitude > 0.40f)
                            continue;
                        float groupY = 0f;
                        foreach (var pt in nearbyGroup) groupY += pt.world.y;
                        sum += groupY / nearbyGroup.Count;
                        count++;
                    }
                    float average = count > 0 ? sum / count : baseline;
                    desiredY = Mathf.Lerp(baseline, average,
                        weight * Mathf.Clamp01(strength / 0.25f));
                }
                else
                    desiredY = baseline + (mode == SculptMode.Lower ? -1f : 1f) * strength * weight;
                foreach (var point in points)
                {
                    var tile = point.tile;
                    Vector3 local = tile.visual.SculptTransform.InverseTransformPoint(
                        new Vector3(point.world.x, desiredY, point.world.z));
                    tile.heights[point.index] = Mathf.Clamp(local.y, tile.baseHeights[point.index] - 1.5f, tile.baseHeights[point.index] + 2.5f);
                    modified.Add(tile);
                }
            }
            CommitChanges(modified);
            return modified.Count > 0;
        }

        public static bool ApplyHoeOperation(SculptTile target, Vector3 centre, TerrainOp op)
        {
            if (target == null || !target.initialized || op == null) return false;
            var settings = op.m_settings;
            if (!settings.m_level && !settings.m_raise && !settings.m_smooth) return false;
            float radius = Mathf.Max(settings.m_level ? settings.m_levelRadius : 0f,
                settings.m_raise ? settings.m_raiseRadius : 0f,
                settings.m_smooth ? settings.m_smoothRadius : 0f);
            radius = Mathf.Clamp(radius, 0.1f, 12f);
            var nearby = new List<SculptTile>();
            foreach (var tile in UnityEngine.Object.FindObjectsOfType<SculptTile>())
                if (tile.initialized && tile.visual != null && tile.visual.SculptReady &&
                    tile.nview != null && tile.nview.IsValid() && tile.nview.GetZDO() != null &&
                    Vector2.Distance(new Vector2(tile.transform.position.x, tile.transform.position.z),
                        new Vector2(centre.x, centre.z)) < radius + tile.visual.footprint + 1f)
                    nearby.Add(tile);
            var connected = new List<SculptTile> { target };
            for (int i = 0; i < connected.Count; i++)
                foreach (var candidate in nearby)
                    if (!connected.Contains(candidate) && connected[i].CompatibleNeighbour(candidate))
                        connected.Add(candidate);
            var grouped = new Dictionary<long, List<GridPoint>>();
            foreach (var tile in connected)
                for (int idx = 0; idx < tile.VertexCount; idx++)
                {
                    Vector3 pos = tile.GetWorldVertex(idx);
                    float dx = pos.x - centre.x, dz = pos.z - centre.z;
                    if (settings.m_square ? Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) > radius : dx * dx + dz * dz > radius * radius)
                        continue;
                    long key = VertexKey(pos);
                    List<GridPoint> group;
                    if (!grouped.TryGetValue(key, out group)) grouped[key] = group = new List<GridPoint>();
                    group.Add(new GridPoint { tile = tile, index = idx, world = pos });
                }
            var changed = new HashSet<SculptTile>();
            foreach (var pair in grouped)
            {
                var group = pair.Value;
                Vector3 pos = group[0].world;
                float oldY = 0f;
                foreach (var point in group) oldY += point.world.y;
                oldY /= group.Count;
                float dist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(centre.x, centre.z));
                float wanted = oldY;
                if (settings.m_level && dist <= settings.m_levelRadius)
                    wanted = centre.y + settings.m_levelOffset;
                if (settings.m_raise && dist <= settings.m_raiseRadius)
                {
                    float t = settings.m_square ? 1f : Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, settings.m_raiseRadius));
                    wanted += settings.m_raiseDelta * Mathf.Pow(t, Mathf.Max(0.01f, settings.m_raisePower));
                }
                if (settings.m_smooth && dist <= settings.m_smoothRadius)
                {
                    float t = settings.m_square ? 1f : Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, settings.m_smoothRadius));
                    wanted = Mathf.Lerp(wanted, centre.y + settings.m_levelOffset,
                        Mathf.Pow(t, Mathf.Max(0.01f, settings.m_smoothPower)));
                }
                foreach (var point in group)
                {
                    var tile = point.tile;
                    Vector3 local = tile.visual.SculptTransform.InverseTransformPoint(
                        new Vector3(pos.x, wanted, pos.z));
                    tile.heights[point.index] = Mathf.Clamp(local.y, tile.baseHeights[point.index] - 1.5f, tile.baseHeights[point.index] + 2.5f);
                    changed.Add(tile);
                }
            }
            CommitChanges(changed);
            return changed.Count != 0;
        }

        private static void CommitChanges(HashSet<SculptTile> changed)
        {
            foreach (var tile in changed)
            {
                tile.nview.ClaimOwnership();
                string encoded = tile.Encode();
                tile.nview.GetZDO().Set(DataKey, encoded);
                tile.lastData = encoded;
                tile.ApplyMesh();
            }
            if (changed.Count > 0) RefreshSharedSeamNormals();
        }

        private static void RefreshSharedSeamNormals()
        {
            var tiles = new List<SculptTile>();
            foreach (var tile in UnityEngine.Object.FindObjectsOfType<SculptTile>())
                if (tile.initialized && tile.visual != null && tile.visual.SculptReady &&
                    tile.boundary != null && tile.boundary.Count > 0)
                    tiles.Add(tile);
            if (tiles.Count < 2) return;

            var groups = new Dictionary<long, List<GridPoint>>();
            foreach (var tile in tiles)
                foreach (int index in tile.boundary)
                {
                    Vector3 p = tile.GetWorldVertex(index);
                    long key = VertexKey(p);
                    List<GridPoint> group;
                    if (!groups.TryGetValue(key, out group))
                        groups[key] = group = new List<GridPoint>();
                    group.Add(new GridPoint { tile = tile, index = index, world = p });
                }

            var altered = new Dictionary<SculptTile, Vector3[]>();
            foreach (var pair in groups)
            {
                List<GridPoint> group = pair.Value;
                if (group.Count < 2) continue;
                bool joinsDifferentTiles = false;
                for (int n = 1; n < group.Count; n++)
                    if (group[n].tile != group[0].tile) { joinsDifferentTiles = true; break; }
                if (!joinsDifferentTiles) continue;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var pt in group)
                {
                    minY = Mathf.Min(minY, pt.world.y);
                    maxY = Mathf.Max(maxY, pt.world.y);
                }
                if (maxY - minY > 0.015f) continue;

                Vector3 sum = Vector3.zero;
                foreach (var pt in group)
                {
                    Vector3[] normals = pt.tile.visual.SculptMesh.normals;
                    if (pt.index >= normals.Length) continue;
                    sum += pt.tile.visual.SculptTransform.TransformDirection(normals[pt.index]);
                }
                if (sum.sqrMagnitude < 0.00001f) continue;
                Vector3 average = sum.normalized;
                foreach (var pt in group)
                {
                    Vector3[] normals;
                    if (!altered.TryGetValue(pt.tile, out normals))
                        altered[pt.tile] = normals = pt.tile.visual.SculptMesh.normals;
                    normals[pt.index] = pt.tile.visual.SculptTransform.InverseTransformDirection(average).normalized;
                }
            }
            foreach (var pair in altered)
                pair.Key.visual.SculptMesh.normals = pair.Value;
        }

        private string Encode()
        {
            string[] parts = new string[heights.Length];
            for (int i = 0; i < heights.Length; i++)
                parts[i] = heights[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(",", parts);
        }

        private void Decode(string encoded)
        {
            for (int i = 0; i < heights.Length; i++) heights[i] = baseHeights[i];
            if (string.IsNullOrEmpty(encoded)) return;
            string[] parts = encoded.Split(',');
            if (parts.Length != heights.Length) return;
            for (int i = 0; i < parts.Length; i++)
                float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out heights[i]);
        }

        private void ApplyMesh()
        {
            if (!visual.SculptReady) return;
            Mesh m = visual.SculptMesh;
            Vector3[] verts = m.vertices;
            if (verts.Length != heights.Length) return;
            for (int i = 0; i < verts.Length; i++) verts[i].y = heights[i];
            m.vertices = verts;
            m.RecalculateNormals();
            visual.RestoreDisplacedBounds();
            visual.RefreshFlatFloorSkirt();
            if (collision != null) collision.RefreshMesh(m);
        }
    }
}

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

        private int RiseSteps()
        {
            int i = geometry.IndexOf("Rise", StringComparison.Ordinal);
            if (i < 0) return 0;
            int value;
            return int.TryParse(geometry.Substring(i + 4), out value) ? value : 0;
        }

        private float ProfileHeight(float x, float z)
        {
            int rise = RiseSteps();
            if (rise <= 0) return 0f;
            float half = footprint * 0.5f;
            float u = Mathf.Clamp(x + half, 0f, footprint);
            float v = Mathf.Clamp(z + half, 0f, footprint);
            float travel = geometry.StartsWith("slopeInner", StringComparison.Ordinal) ? Mathf.Min(u, v) :
                geometry.StartsWith("slopeOuter", StringComparison.Ordinal) ? Mathf.Max(u, v) : u;
            return travel * rise * 0.5f;
        }

        private float SmoothCornerCentre(float x, float z, float height)
        {
            float rise = RiseSteps() * footprint * 0.5f;
            if (rise <= 0f) return height;
            float half = footprint * 0.5f;
            float u = Mathf.Clamp01((x + half) / footprint);
            float v = Mathf.Clamp01((z + half) / footprint);
            float boundaryFade = Mathf.Clamp01(Mathf.Min(u, 1f - u, v, 1f - v) * 8f);
            if (boundaryFade <= 0f) return height;
            float k = 0.16f;
            float diff = Mathf.Abs(u - v);
            if (diff >= k) return height;
            float blend = (k - diff) * (k - diff) / (4f * k);
            float correction = (geometry.StartsWith("slopeInner", StringComparison.Ordinal) ? -blend : blend) * rise;
            return height + correction * boundaryFade;
        }

        private void BuildVerticalWall(out Vector3[] vertices, out Vector2[] uv, out int[] triangles)
        {
            bool curved = geometry != "wall90";
            int segments = curved ? (footprint == 4 ? 16 : 8) : 1;
            float half = footprint * 0.5f;
            const float thickness = 0.06f;
            int frontCount = (segments + 1) * 2;
            vertices = new Vector3[frontCount * 2];
            uv = new Vector2[vertices.Length];
            triangles = new int[segments * 24 + 12];
            float a85 = 5f * Mathf.Deg2Rad;
            float co85 = Mathf.Cos(a85), si85 = Mathf.Sin(a85);
            for (int i = 0; i <= segments; ++i)
            {
                float t = i / (float)segments;
                float x, z;
                if (!curved) { x = -half + footprint * t; z = 0f; }
                else
                {
                    float a = t * Mathf.PI * 0.5f;
                    x = (geometry == "wall90Outer" ? -half : half) + half * Mathf.Cos(a);
                    z = -half + half * Mathf.Sin(a);
                }
                int f = i * 2;
                vertices[f] = new Vector3(x * co85, -x * si85, z);
                vertices[f + 1] = new Vector3(x * co85 + footprint * si85,
                    footprint * co85 - x * si85, z);
                uv[f] = new Vector2(t, 0f);
                uv[f + 1] = new Vector2(t, 1f);
            }
            for (int i = 0; i <= segments; ++i)
            {
                int previous = Mathf.Max(0, i - 1) * 2;
                int next = Mathf.Min(segments, i + 1) * 2;
                Vector3 tangent = vertices[next] - vertices[previous];
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.right;
                tangent.Normalize();
                Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
                int f = i * 2, b = frontCount + f;
                Vector3 displacement = normal * thickness;
                vertices[b] = vertices[f] + displacement;
                vertices[b + 1] = vertices[f + 1] + displacement;
                uv[b] = uv[f];
                uv[b + 1] = uv[f + 1];
            }
            int q = 0;
            for (int i = 0; i < segments; ++i)
            {
                int f = i * 2, b = frontCount + f;
                triangles[q++] = f; triangles[q++] = f + 1; triangles[q++] = f + 2;
                triangles[q++] = f + 1; triangles[q++] = f + 3; triangles[q++] = f + 2;
                triangles[q++] = b + 2; triangles[q++] = b + 1; triangles[q++] = b;
                triangles[q++] = b + 2; triangles[q++] = b + 3; triangles[q++] = b + 1;
                triangles[q++] = f + 1; triangles[q++] = b + 1; triangles[q++] = f + 3;
                triangles[q++] = f + 3; triangles[q++] = b + 1; triangles[q++] = b + 3;
                triangles[q++] = f + 2; triangles[q++] = b; triangles[q++] = f;
                triangles[q++] = f + 2; triangles[q++] = b + 2; triangles[q++] = b;
            }
            int last = segments * 2, rear = frontCount;
            triangles[q++] = 0; triangles[q++] = rear; triangles[q++] = 1;
            triangles[q++] = 1; triangles[q++] = rear; triangles[q++] = rear + 1;
            triangles[q++] = last + 1; triangles[q++] = rear + last; triangles[q++] = last;
            triangles[q++] = rear + last + 1; triangles[q++] = rear + last; triangles[q++] = last + 1;
        }

        private void BuildMesh()
        {
            int cells = footprint == 4 ? 16 : 8;
            int side = cells + 1;
            var vertices = new Vector3[side * side];
            var normals = new Vector3[vertices.Length];
            var tangents = new Vector4[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[cells * cells * 6];
            int ti = 0;
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    int i = z * side + x;
                    vertices[i] = new Vector3(footprint * x / (float)cells - footprint / 2f, 0f, footprint * z / (float)cells - footprint / 2f);
                    normals[i] = Vector3.up;
                    tangents[i] = new Vector4(1f, 0f, 0f, 1f);
                    colors[i] = ColorForBiome(biome);
                    uv[i] = new Vector2(x / (float)cells, z / (float)cells);
                    if (x == cells || z == cells) continue;
                    int a = i, b = i + 1, c = i + side, d = c + 1;
                    triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                    triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
                }
            if (geometry == "outer" || geometry == "inner" ||
                geometry.StartsWith("slopeOuter", StringComparison.Ordinal) ||
                geometry.StartsWith("slopeInner", StringComparison.Ordinal))
            {
                int radial = cells;
                int angular = cells * 2;
                int stride = radial + 1;
                int zoneVertices = (angular + 1) * stride;
                bool flatQuarter = geometry == "inner" || geometry == "outer";
                int zoneCount = flatQuarter ? 1 : 2;
                vertices = new Vector3[zoneVertices * zoneCount];
                normals = new Vector3[vertices.Length];
                tangents = new Vector4[vertices.Length];
                uv = new Vector2[vertices.Length];
                colors = new Color32[vertices.Length];
                triangles = new int[angular * radial * 6 * zoneCount];
                ti = 0;
                for (int zone = 0; zone < zoneCount; zone++)
                {
                    for (int a = 0; a <= angular; a++)
                    {
                        float angle = (Mathf.PI * 0.5f) * a / angular;
                        float co = Mathf.Cos(angle), si = Mathf.Sin(angle);
                        float maxRadius = footprint / Mathf.Max(co, si);
                        for (int r = 0; r <= radial; r++)
                        {
                            int i = zone * zoneVertices + a * stride + r;
                            int region = flatQuarter && geometry == "inner" ? 1 : zone;
                            float distance = region == 0
                                ? footprint * r / (float)radial
                                : footprint + (maxRadius - footprint) * r / radial;
                            float px = -footprint * 0.5f + distance * co;
                            float pz = -footprint * 0.5f + distance * si;
                            vertices[i] = new Vector3(px, 0f, pz);
                            normals[i] = Vector3.up;
                            tangents[i] = new Vector4(1f, 0f, 0f, 1f);
                            colors[i] = ColorForBiome(biome);
                            uv[i] = new Vector2(px / footprint + 0.5f, pz / footprint + 0.5f);
                            if (a == angular || r == radial) continue;
                            int v = i, next = i + 1, row = i + stride, diag = row + 1;
                            triangles[ti++] = v; triangles[ti++] = row; triangles[ti++] = next;
                            triangles[ti++] = next; triangles[ti++] = row; triangles[ti++] = diag;
                        }
                    }
                }
            }
            if (RiseSteps() > 0)
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    v.y = ProfileHeight(v.x, v.z);
                    if (geometry.StartsWith("slopeInner", StringComparison.Ordinal) ||
                        geometry.StartsWith("slopeOuter", StringComparison.Ordinal))
                        v.y = SmoothCornerCentre(v.x, v.z, v.y);
                    vertices[i] = v;
                }
            else if (geometry.StartsWith("wall90", StringComparison.Ordinal))
            {
                BuildVerticalWall(out vertices, out uv, out triangles);
                normals = new Vector3[vertices.Length];
                tangents = new Vector4[vertices.Length];
                colors = new Color32[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    normals[i] = Vector3.forward;
                    tangents[i] = new Vector4(1, 0, 0, 1);
                    colors[i] = ColorForBiome(biome);
                }
            }
            mesh = new Mesh();
            mesh.name = "VH_NativeSurfaceMesh_" + biome + "_" + mode;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.uv2 = uv;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            RestoreDisplacedBounds();
            filter.sharedMesh = mesh;
        }

        private void CreateFlatFloorSkirt(MeshRenderer sourceTerrain)
        {
            if (geometry != "square" || mode != "terrain" || IsVerticalWall ||
                floorSkirtRenderer != null || child == null || material == null) return;
            const float depth = 0.08f;
            int segments = footprint == 4 ? 16 : 8;
            int count = segments * 4;
            float half = footprint * 0.5f;
            var vertices = new Vector3[count * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[count * 6];
            Bounds worldBounds = sourceTerrain.bounds;
            for (int i = 0; i < count; ++i)
            {
                int edge = i / segments;
                float t = (i % segments) / (float)segments;
                float x = edge == 0 ? -half + footprint * t : edge == 1 ? half :
                    edge == 2 ? half - footprint * t : -half;
                float z = edge == 0 ? -half : edge == 1 ? -half + footprint * t :
                    edge == 2 ? half : half - footprint * t;
                int a = i * 2;
                vertices[a] = new Vector3(x, 0f, z);
                vertices[a + 1] = new Vector3(x, -depth, z);
                Vector3 world = child.transform.TransformPoint(vertices[a]);
                Vector2 mapped = new Vector2(
                    (world.x - worldBounds.min.x) / Mathf.Max(1f, worldBounds.size.x),
                    (world.z - worldBounds.min.z) / Mathf.Max(1f, worldBounds.size.z));
                uv[a] = mapped;
                uv[a + 1] = mapped;
                colors[a] = ColorForBiome(biome);
                colors[a + 1] = ColorForBiome(biome);
                int next = ((i + 1) % count) * 2;
                int q = i * 6;
                triangles[q] = a; triangles[q + 1] = a + 1; triangles[q + 2] = next;
                triangles[q + 3] = next; triangles[q + 4] = a + 1; triangles[q + 5] = next + 1;
            }
            floorSkirtMesh = new Mesh();
            floorSkirtMesh.name = "VH_FlatFloorSkirtMesh";
            floorSkirtMesh.vertices = vertices;
            floorSkirtMesh.uv = uv;
            floorSkirtMesh.uv2 = uv;
            floorSkirtMesh.colors32 = colors;
            floorSkirtMesh.triangles = triangles;
            floorSkirtMesh.RecalculateNormals();
            floorSkirtMesh.RecalculateBounds();
            var bounds = floorSkirtMesh.bounds;
            bounds.Expand(new Vector3(0f, 16f, 0f));
            floorSkirtMesh.bounds = bounds;
            var side = new GameObject("VH_FlatFloorSkirt");
            side.transform.SetParent(child.transform, false);
            side.AddComponent<MeshFilter>().sharedMesh = floorSkirtMesh;
            floorSkirtRenderer = side.AddComponent<MeshRenderer>();
            floorSkirtRenderer.sharedMaterial = material;
            floorSkirtRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            floorSkirtRenderer.receiveShadows = false;
            var pb = new MaterialPropertyBlock();
            sourceTerrain.GetPropertyBlock(pb);
            if (!pb.isEmpty) floorSkirtRenderer.SetPropertyBlock(pb);
            RefreshFlatFloorSkirt();
        }

        internal void RefreshFlatFloorSkirt()
        {
            if (floorSkirtMesh == null || mesh == null || geometry != "square" || IsVerticalWall) return;
            Vector3[] top = mesh.vertices;
            Vector3[] rim = floorSkirtMesh.vertices;
            int cells = footprint == 4 ? 16 : 8;
            int side = cells + 1;
            int count = cells * 4;
            if (top.Length != side * side || rim.Length != count * 2) return;
            const float depth = 0.08f;
            for (int i = 0; i < count; i++)
            {
                int edge = i / cells;
                int step = i % cells;
                int index = edge == 0 ? step :
                    edge == 1 ? step * side + cells :
                    edge == 2 ? cells * side + (cells - step) :
                    (cells - step) * side;
                rim[i * 2] = top[index];
                rim[i * 2 + 1] = top[index] + Vector3.down * depth;
            }
            floorSkirtMesh.vertices = rim;
            floorSkirtMesh.RecalculateNormals();
            floorSkirtMesh.RecalculateBounds();
            Bounds b = floorSkirtMesh.bounds;
            b.Expand(new Vector3(0f, 16f, 0f));
            floorSkirtMesh.bounds = b;
        }


        internal void RestoreDisplacedBounds()
        {
            if (mesh == null) return;
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 16f, 0f));
            mesh.bounds = bounds;
        }
    }
}

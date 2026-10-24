using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    public sealed partial class TerrainPiece : MonoBehaviour
    {

        private void MeasurePreviewAnchor()
        {
            if (previewAnchorMeasured) return;
            previewAnchorMeasured = true;
            previewAnchorLocal = Vector3.zero;
            if (IsVerticalWall)
            {
                float bottom = float.PositiveInfinity;
                if (previewRenderers != null)
                    foreach (Renderer r in previewRenderers)
                    {
                        if (r == null) continue;
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        Bounds bounds = mf.sharedMesh.bounds;
                        for (int x = -1; x <= 1; x += 2)
                            for (int y = -1; y <= 1; y += 2)
                                for (int z = -1; z <= 1; z += 2)
                                {
                                    Vector3 v = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                                    float rootY = transform.InverseTransformPoint(r.transform.TransformPoint(v)).y;
                                    bottom = Mathf.Min(bottom, rootY);
                                }
                    }
                if (!float.IsPositiveInfinity(bottom)) previewAnchorLocal = new Vector3(0f, bottom, 0f);
                return;
            }
            float highest = float.NegativeInfinity;
            if (previewRenderers == null) return;
            foreach (Renderer r in previewRenderers)
            {
                if (r == null) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Vector3 point = r.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                Vector3 rootLocal = transform.InverseTransformPoint(point);
                if (rootLocal.y > highest)
                {
                    highest = rootLocal.y;
                    previewAnchorLocal = rootLocal;
                }
            }
        }

        private bool IsHammerEquipped()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return false;
            try
            {
                var method = HarmonyLib.AccessTools.Method(player.GetType(), "GetRightItem");
                var item = method != null ? method.Invoke(player, null) as ItemDrop.ItemData : null;
                return item != null && item.m_dropPrefab != null &&
                    item.m_dropPrefab.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private void UpdateFootprintFrame()
        {
            if (Time.time < nextFrameCheck) return;
            nextFrameCheck = Time.time + 0.16f;
            bool isPlacementGhost = !realPiece && name.EndsWith("(Clone)", StringComparison.Ordinal);
            bool show = (realPiece || isPlacementGhost) && IsHammerEquipped() &&
                Player.m_localPlayer != null &&
                (Player.m_localPlayer.transform.position - transform.position).sqrMagnitude < 625f;
            if (!show)
            {
                if (guideFaces != null) guideFaces.SetActive(false);
                if (frameLine != null) frameLine.enabled = false;
                if (crestLine != null) crestLine.enabled = false;
                if (cornerGuides != null) cornerGuides.enabled = false;
                if (cornerUprights != null) foreach (var guide in cornerUprights) if (guide != null) guide.enabled = false;
                return;
            }
            if (frameLine == null)
            {
                var o = new GameObject("VH_FixedPlacementOutline");
                o.transform.SetParent(transform, false);
                frameLine = o.AddComponent<LineRenderer>();
                frameLine.useWorldSpace = false;
                frameLine.loop = true;
                frameLine.positionCount = 0;
                frameLine.widthMultiplier = 0.028f;
                frameLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                frameLine.receiveShadows = false;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    frameMaterial = new Material(shader);
                    frameLine.sharedMaterial = frameMaterial;
                }
                frameLine.startColor = new Color(0.25f, 0.95f, 0.95f, 0.20f);
                frameLine.endColor = frameLine.startColor;
            }
            if (IsVerticalWall)
            {
                UpdateWallGuide();
                return;
            }
            float h = footprint * 0.5f;
            float hx = footprint * 0.5f;
            float y = previewAnchorLocal.y + 0.025f;
            Vector3 c = new Vector3(previewAnchorLocal.x, 0f, previewAnchorLocal.z);
            int segments = footprint == 4 ? 16 : 8;
            frameLine.positionCount = 4 * segments;
            for (int i = 0; i < 4 * segments; ++i)
            {
                int edge = i / segments;
                float t = (i % segments) / (float)segments;
                float x = edge == 0 ? -hx + footprint * t : edge == 1 ? hx :
                          edge == 2 ? hx - footprint * t : -hx;
                float z = edge == 0 ? -h : edge == 1 ? -h + footprint * t :
                          edge == 2 ? h : h - footprint * t;
                Vector3 profile = new Vector3(x, y + ProfileHeight(x, z), z);
                frameLine.SetPosition(i, c + profile);
            }
            frameLine.enabled = true;
            bool isCornerSlope = geometry.StartsWith("slopeInner", StringComparison.Ordinal) ||
                geometry.StartsWith("slopeOuter", StringComparison.Ordinal);
            bool isStraightSlope = geometry.StartsWith("slope", StringComparison.Ordinal) && !isCornerSlope;
            if (isCornerSlope)
            {
                if (crestLine == null)
                {
                    GameObject o = new GameObject("VH_ProfileDiagonal");
                    o.transform.SetParent(transform, false);
                    crestLine = o.AddComponent<LineRenderer>();
                    crestLine.useWorldSpace = false;
                    crestLine.loop = false;
                    crestLine.widthMultiplier = 0.022f;
                    crestLine.sharedMaterial = frameMaterial;
                    crestLine.startColor = new Color(0.25f, 0.95f, 0.95f, 0.20f);
                    crestLine.endColor = crestLine.startColor;
                    crestLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                crestLine.positionCount = segments + 1;
                for (int i = 0; i <= segments; ++i)
                {
                    float t = i / (float)segments;
                    float x = -hx + footprint * t;
                    float z = -h + footprint * t;
                    crestLine.SetPosition(i, c + new Vector3(x, y + ProfileHeight(x, z), z));
                }
                crestLine.enabled = true;
            }
            else if (crestLine != null) crestLine.enabled = false;
            if (isCornerSlope || isStraightSlope)
            {
                if (cornerGuides == null)
                {
                    var guide = new GameObject("VH_PrismBaseOutline");
                    guide.transform.SetParent(transform, false);
                    cornerGuides = guide.AddComponent<LineRenderer>();
                    cornerGuides.useWorldSpace = false;
                    cornerGuides.loop = true;
                    cornerGuides.widthMultiplier = 0.024f;
                    cornerGuides.sharedMaterial = frameMaterial;
                    cornerGuides.startColor = frameLine.startColor;
                    cornerGuides.endColor = frameLine.endColor;
                    cornerGuides.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var lower = new Vector3[4];
                var upper = new Vector3[4];
                GetStructuralCorners(lower, upper);
                cornerGuides.positionCount = 4;
                for (int i = 0; i < 4; i++) cornerGuides.SetPosition(i, lower[i]);
                cornerGuides.enabled = true;
                if (cornerUprights == null)
                {
                    cornerUprights = new LineRenderer[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var obj = new GameObject("VH_PrismVertical_" + i);
                        obj.transform.SetParent(transform, false);
                        var line = obj.AddComponent<LineRenderer>();
                        line.useWorldSpace = false;
                        line.widthMultiplier = 0.024f;
                        line.sharedMaterial = frameMaterial;
                        line.startColor = frameLine.startColor;
                        line.endColor = frameLine.endColor;
                        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        line.positionCount = 2;
                        cornerUprights[i] = line;
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    cornerUprights[i].SetPosition(0, lower[i]);
                    cornerUprights[i].SetPosition(1, upper[i]);
                    cornerUprights[i].enabled = true;
                }
                UpdateGuideFaces(lower, upper);
            }
            else
            {
                if (guideFaces != null) guideFaces.SetActive(false);
                if (cornerGuides != null) cornerGuides.enabled = false;
                if (cornerUprights != null)
                    foreach (var guide in cornerUprights) if (guide != null) guide.enabled = false;
            }
        }

        internal void GetWallShape(out Vector3[] lower, out Vector3[] upper)
        {
            if (cachedWallLower == null || cachedWallUpper == null)
            {
                Vector3[] vertices;
                Vector2[] uv;
                int[] triangles;
                BuildVerticalWall(out vertices, out uv, out triangles);
                int count = (geometry == "wall90" ? 1 : (footprint == 4 ? 16 : 8)) + 1;
                cachedWallLower = new Vector3[count];
                cachedWallUpper = new Vector3[count];
                Quaternion tilt = Quaternion.Euler(0f, 0f, 5f);
                Vector3 origin = IsVerticalWall ? previewAnchorLocal : RenderAnchorLocal();
                for (int i = 0; i < count; ++i)
                {
                    cachedWallLower[i] = origin + tilt * vertices[i * 2];
                    cachedWallUpper[i] = origin + tilt * vertices[i * 2 + 1];
                }
            }
            lower = cachedWallLower;
            upper = cachedWallUpper;
        }

        private void UpdateWallGuide()
        {
            Vector3[] lower, upper;
            GetWallShape(out lower, out upper);
            int count = lower.Length;
            Vector3 lift = Vector3.up * WallOutlineLift;
            frameLine.loop = false;
            frameLine.positionCount = count;
            for (int i = 0; i < count; ++i) frameLine.SetPosition(i, upper[i] + lift);
            frameLine.enabled = true;
            if (cornerGuides == null)
            {
                var obj = new GameObject("VH_WallBottomOutline");
                obj.transform.SetParent(transform, false);
                cornerGuides = obj.AddComponent<LineRenderer>();
                cornerGuides.useWorldSpace = false;
                cornerGuides.widthMultiplier = 0.024f;
                cornerGuides.sharedMaterial = frameMaterial;
                cornerGuides.startColor = frameLine.startColor;
                cornerGuides.endColor = frameLine.endColor;
            }
            cornerGuides.loop = false;
            cornerGuides.positionCount = count;
            for (int i = 0; i < count; ++i) cornerGuides.SetPosition(i, lower[i] + lift);
            cornerGuides.enabled = true;
            if (cornerUprights == null)
            {
                cornerUprights = new LineRenderer[2];
                for (int i = 0; i < 2; ++i)
                {
                    var obj = new GameObject("VH_WallEnd_" + i);
                    obj.transform.SetParent(transform, false);
                    var line = obj.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.widthMultiplier = 0.024f;
                    line.sharedMaterial = frameMaterial;
                    line.startColor = frameLine.startColor;
                    line.endColor = frameLine.endColor;
                    cornerUprights[i] = line;
                }
            }
            for (int i = 0; i < 2; ++i)
            {
                int index = i == 0 ? 0 : count - 1;
                cornerUprights[i].SetPosition(0, lower[index] + lift);
                cornerUprights[i].SetPosition(1, upper[index] + lift);
                cornerUprights[i].enabled = true;
            }
            if (crestLine != null) crestLine.enabled = false;
            if (guideFaces != null) guideFaces.SetActive(false);
        }

        private void UpdateGuideFaces(Vector3[] lower, Vector3[] upper)
        {
            if (guideFaces == null)
            {
                guideFaces = new GameObject("VH_GuideFaces");
                guideFaces.transform.SetParent(transform, false);
                guideFaceFilter = guideFaces.AddComponent<MeshFilter>();
                guideFaceRenderer = guideFaces.AddComponent<MeshRenderer>();
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader != null)
                {
                    guideFaceMaterial = new Material(shader);
                    guideFaceMaterial.color = new Color(0.16f, 0.9f, 1f, 0.18f);
                    guideFaceRenderer.sharedMaterial = guideFaceMaterial;
                }
                guideFaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                guideFaceRenderer.receiveShadows = false;
                guideFaceMesh = new Mesh();
                guideFaceMesh.name = "VH_GuideFaceMesh";
                guideFaceFilter.sharedMesh = guideFaceMesh;
            }
            if (guideFaceGeometryBuilt)
            {
                guideFaces.SetActive(true);
                return;
            }
            guideFaceGeometryBuilt = true;
            var verts = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                if ((upper[i] - lower[i]).sqrMagnitude < 0.0001f &&
                    (upper[j] - lower[j]).sqrMagnitude < 0.0001f) continue;
                int k = verts.Count;
                verts.Add(lower[i]); verts.Add(lower[j]);
                verts.Add(upper[j]); verts.Add(upper[i]);
                triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 2);
                triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 3);
                triangles.Add(k + 2); triangles.Add(k + 1); triangles.Add(k);
                triangles.Add(k + 3); triangles.Add(k + 2); triangles.Add(k);
            }
            guideFaceMesh.Clear();
            guideFaceMesh.SetVertices(verts);
            guideFaceMesh.SetTriangles(triangles, 0);
            guideFaceMesh.RecalculateBounds();
            if (guideFaceRenderer != null) guideFaceRenderer.enabled = verts.Count > 0;
            guideFaces.SetActive(verts.Count > 0);
        }

        private void ShowGhostPreview()
        {
            if (mesh == null) BuildMesh();
            if (ghostVisual == null)
            {
                ghostVisual = new GameObject("VH_GhostFilledShape");
                ghostVisual.transform.SetParent(transform, false);
            }
            MeshFilter mf = ghostVisual.GetComponent<MeshFilter>();
            if (mf == null) mf = ghostVisual.AddComponent<MeshFilter>();
            if (mf.sharedMesh == null || mf.sharedMesh.name != "VH_OpaquePlacementPreview_" + biome + "_" + mode + "_" + geometry)
            {
                Mesh previewMesh = UnityEngine.Object.Instantiate(mesh);
                previewMesh.name = "VH_OpaquePlacementPreview_" + biome + "_" + mode + "_" + geometry;
                var opaque = new Color32[previewMesh.vertexCount];
                for (int i = 0; i < opaque.Length; i++) opaque[i] = new Color32(255, 255, 255, 255);
                previewMesh.colors32 = opaque;
                mf.sharedMesh = previewMesh;
            }
            if (ghostRenderer == null) ghostRenderer = ghostVisual.GetComponent<MeshRenderer>();
            if (ghostRenderer == null) ghostRenderer = ghostVisual.AddComponent<MeshRenderer>();
            if (ghostRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) return;
                ghostMaterial = new Material(shader);
                ghostMaterial.name = "VH_Ghost_Transparent";
                ghostMaterial.color = new Color(0.22f, 0.86f, 0.92f, 0.32f);
                ghostRenderer.sharedMaterial = ghostMaterial;
            }
            ghostVisual.transform.localPosition = previewAnchorLocal;
            ghostVisual.transform.localRotation = IsVerticalWall
                ? Quaternion.Euler(0f, 0f, 5f) : Quaternion.identity;
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostRenderer.receiveShadows = false;
            if (surface != null) surface.enabled = false;
            ghostVisual.SetActive(true);
            ghostRenderer.enabled = true;
            if (previewRenderers != null)
                foreach (Renderer r in previewRenderers)
                    if (r != null) r.enabled = false;
        }
    }
}

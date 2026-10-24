using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    internal static class TerrainRegistry
    {
        // DEBUG: Set to false before releasing the mod.
        private const bool ShowSnapPoints = true;

        internal static void CreateNewSurfacePieces()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateNewSurfacePieces;

            RegisterTerrainSet("ValeriusLava", "Lava", "Animated Ashlands lava. Deals fire damage.", 32, "terrain", true);
            RegisterTerrainSet("ValeriusWater", "Water", "Native water surface.", 0, "water", false);
            RegisterTerrainSet("ValeriusMeadows", "Meadows", "Meadows terrain surface.", 1, "terrain", false);
            RegisterTerrainSet("ValeriusBlackForest", "Black Forest", "Black Forest terrain surface.", 8, "terrain", false);
            RegisterTerrainSet("ValeriusSwamp", "Swamp", "Swamp biome terrain surface.", 2, "terrain", false);
            RegisterTerrainSet("ValeriusPlains", "Plains", "Plains terrain surface.", 16, "terrain", false);
            RegisterTerrainSet("ValeriusMountain", "Snow", "Snow terrain surface.", 4, "terrain", false);
            RegisterTerrainSet("ValeriusMistlands", "Mistlands", "Mistlands terrain surface.", 512, "terrain", false);
            RegisterTerrainSet("ValeriusAshlandsGround", "Ashlands", "Ashlands terrain surface.", 32, "terrain", false);
        }

        private static void RegisterTerrainSet(
            string id, string name, string description,
            int biome, string mode, bool damages)
        {
            // Flat and quarter surfaces.
            RegisterTerrainPiece(id, name + " 2x2", description, biome, mode, damages);

            foreach (string curve in new[] { "Outer", "Inner" })
                RegisterTerrainPiece(
                    id + "_Quarter" + curve,
                    name + " " + curve + " Quarter 2x2",
                    description, biome, mode, damages,
                    curve.ToLowerInvariant());

            // Straight and curved slopes, rising 1-4 metres.
            for (int rise = 1; rise <= 4; rise++)
            {
                foreach (string curve in new[] { "", "Inner", "Outer" })
                {
                    string suffix = "_Rise" + rise +
                        (curve == "" ? "" : "_Quarter" + curve);

                    string geometry = "slope" + curve + "Rise" + rise;

                    RegisterTerrainPiece(
                        id + suffix,
                        name + " " + rise + "m Rise " + curve + " 2x2",
                        description, biome, mode, damages, geometry);
                }
            }

            // Straight and curved sheer walls.
            RegisterTerrainPiece(
                id + "_Wall",
                name + " Wall 2x2",
                description, biome, mode, damages, "wall90");

            foreach (string curve in new[] { "Outer", "Inner" })
                RegisterTerrainPiece(
                    id + "_Wall_" + curve,
                    name + " " + curve + " Wall 2x2",
                    description, biome, mode, damages,
                    "wall90" + curve);
        }

        private static void RegisterTerrainPiece(
            string id, string name, string description,
            int biome, string mode, bool damages,
            string geometry = "square")
        {
            try
            {
                bool isWall = geometry.StartsWith("wall", StringComparison.Ordinal);
                string template = isWall ? "woodwall" : "wood_floor";

                var config = new PieceConfig
                {
                    Name = name,
                    Description = $"{description}\nPrefab ID: {id}",
                    PieceTable = "Hammer",
                    Category = "Terrain",
                    Icon = TerrainIcons.Get(id, name)
                };

                var piece = new CustomPiece(id, template, config);
                var prefab = piece.PiecePrefab
                    ?? throw new InvalidOperationException($"Missing template: {template}");

                prefab.transform.localScale = Vector3.one;

                foreach (var wear in prefab.GetComponentsInChildren<WearNTear>(true))
                    UnityEngine.Object.DestroyImmediate(wear);

                var visual = prefab.AddComponent<TerrainPiece>();
                visual.biome = biome;
                visual.mode = mode;
                visual.footprint = 2;
                visual.geometry = geometry;
                visual.renderOffsetY = id.StartsWith("ValeriusLava", StringComparison.Ordinal) ? -0.9f : 0f;
                visual.PrepareTemplateGhost();

                // Custom geometry-specific snaps for slopes and walls.
                TerrainSnaps.InstallSnapPoints(prefab, visual);

                // Optional testing markers; never used for actual snapping.
                if (ShowSnapPoints)
                    TerrainSnaps.ShowDebugSnapMarkers(prefab);

                prefab.AddComponent<TerrainCollision>();
                prefab.AddComponent<SculptTile>();

                if (damages)
                    prefab.AddComponent<HazardVolume>();

                PieceManager.Instance.AddPiece(piece);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogError($"[SurfaceTile] Failed {id}: {e}");
            }
        }


    }
}

namespace ValeriusCustomPrefabs
{
    public sealed partial class TerrainPiece : MonoBehaviour
    {
        public int biome = 32;
        public string mode = "terrain";
        public int footprint = 2;
        public string geometry = "square";
        public float renderOffsetY = 0f;
        private Mesh mesh;
        private Mesh floorSkirtMesh;
        private MeshRenderer floorSkirtRenderer;
        private Material material;
        private MeshRenderer surface;
        private MeshFilter filter;
        private GameObject child;
        private Renderer[] previewRenderers;
        private GameObject ghostVisual;
        private MeshRenderer ghostRenderer;
        private Material ghostMaterial;
        private bool realPiece;
        private float nextTry;
        private bool ready;
        private bool standaloneTerrain;
        private Vector3 previewAnchorLocal;
        private bool previewAnchorMeasured;
        public bool HasLivePiece { get { return realPiece; } }
        public bool IsVerticalWall { get { return !string.IsNullOrEmpty(geometry) && geometry.StartsWith("wall90", StringComparison.Ordinal); } }
        public Vector3 FrameCentreLocal
        {
            get
            {
                if (!previewAnchorMeasured) MeasurePreviewAnchor();
                return previewAnchorLocal;
            }
        }
        private LineRenderer frameLine;
        private LineRenderer crestLine;
        private LineRenderer cornerGuides;
        private LineRenderer[] cornerUprights;
        private Material frameMaterial;
        private GameObject guideFaces;
        private MeshFilter guideFaceFilter;
        private MeshRenderer guideFaceRenderer;
        private Mesh guideFaceMesh;
        private bool guideFaceGeometryBuilt;
        private Vector3[] cachedWallLower, cachedWallUpper;
        private Material guideFaceMaterial;
        private float nextFrameCheck;
        private const float WallOutlineLift = 0f;
        public bool SculptReady { get { return ready && mesh != null && child != null; } }
        public Mesh SculptMesh { get { return mesh; } }
        public Transform SculptTransform { get { return child != null ? child.transform : null; } }

        private void Awake()
        {
            Transform native = transform.Find("VH_NativeSurface");
            if (native == null)
            {
                child = new GameObject("VH_NativeSurface");
                child.transform.SetParent(transform, false);
            }
            else child = native.gameObject;
            filter = child.GetComponent<MeshFilter>();
            if (filter == null) filter = child.AddComponent<MeshFilter>();
            surface = child.GetComponent<MeshRenderer>();
            if (surface == null) surface = child.AddComponent<MeshRenderer>();
            surface.enabled = false;
            surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.receiveShadows = false;

            Transform existingGhost = transform.Find("VH_GhostFilledShape");
            if (existingGhost != null)
            {
                ghostVisual = existingGhost.gameObject;
                ghostRenderer = ghostVisual.GetComponent<MeshRenderer>();
            }
            var originals = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r != null && r.name != "DEBUG_SnapMarker" &&
    r != surface && r != ghostRenderer &&
                    !r.transform.IsChildOf(child.transform) &&
                    (ghostVisual == null || !r.transform.IsChildOf(ghostVisual.transform)))
                    originals.Add(r);
            previewRenderers = originals.ToArray();
        }

        public void PrepareTemplateGhost()
        {
            if (child == null || filter == null || surface == null) Awake();
            MeasurePreviewAnchor();
            ShowGhostPreview();
            if (ghostRenderer == null || ghostRenderer.sharedMaterial == null)
                throw new InvalidOperationException("Failed to initialize Valerius placement ghost");
            foreach (Renderer r in previewRenderers)
                if (r != null) r.enabled = false;
        }

        private void PrepareStandaloneTerrain()
        {
            if (standaloneTerrain || mode != "terrain" || child == null) return;
            child.transform.SetParent(null, true);
            child.transform.position = transform.TransformPoint(RenderAnchorLocal());
            child.transform.rotation = transform.rotation * (geometry.StartsWith("wall90", StringComparison.Ordinal)
                ? Quaternion.Euler(0f, 0f, 5f) : Quaternion.identity);
            child.transform.localScale = transform.lossyScale;
            standaloneTerrain = true;
        }

        private Vector3 RenderAnchorLocal() =>
    previewAnchorLocal + Vector3.up * renderOffsetY;

        private void LateUpdate()
        {
            if (!standaloneTerrain || child == null) return;
            Vector3 position = transform.TransformPoint(RenderAnchorLocal());
            Quaternion rotation = transform.rotation * (IsVerticalWall
                ? Quaternion.Euler(0f, 0f, 5f) : Quaternion.identity);
            Vector3 scale = transform.lossyScale;
            Transform t = child.transform;
            if ((t.position - position).sqrMagnitude > 0.000001f) t.position = position;
            if (Quaternion.Angle(t.rotation, rotation) > 0.01f) t.rotation = rotation;
            if ((t.localScale - scale).sqrMagnitude > 0.000001f) t.localScale = scale;
        }

        private void Update()
        {
            if (!realPiece)
            {
                ZNetView liveView = GetComponent<ZNetView>();
                if (liveView == null || !liveView.IsValid() || liveView.GetZDO() == null)
                {
                    MeasurePreviewAnchor();
                    if (name.EndsWith("(Clone)", StringComparison.Ordinal) &&
                        (ghostVisual == null || ghostRenderer == null || ghostRenderer.sharedMaterial == null))
                        ShowGhostPreview();
                    UpdateFootprintFrame();
                    return;
                }
                MeasurePreviewAnchor();
                if (child != null) child.transform.localPosition = RenderAnchorLocal();
                realPiece = true;
                if (ghostRenderer != null) ghostRenderer.enabled = false;
                if (ghostVisual != null) ghostVisual.SetActive(false);
                if (previewRenderers != null)
                    foreach (Renderer r in previewRenderers)
                        if (r != null) r.enabled = false;
            }
            UpdateFootprintFrame();
            if (guideFaceRenderer != null) guideFaceRenderer.enabled = false;
            if (realPiece && mode == "water" && child != null)
            {
                Vector3 wanted = RenderAnchorLocal();
                if ((child.transform.localPosition - wanted).sqrMagnitude > 0.000001f)
                    child.transform.localPosition = wanted;
            }
            if (ready)
            {
                if (realPiece && surface != null && !surface.enabled)
                    surface.enabled = true;
                return;
            }
            if (Time.time < nextTry || filter == null || surface == null) return;
            nextTry = Time.time + 5f;
            ZNetView nview = GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || nview.GetZDO() == null) return;
            try
            {
                if (mode == "terrain") PrepareStandaloneTerrain();
                if (mesh == null) BuildMesh();
                Material sourceMaterial = null;
                MeshRenderer terrain = null;
                if (mode == "terrain")
                {
                    terrain = FindTerrain(child.transform.position, biome);
                    if (terrain != null) sourceMaterial = terrain.sharedMaterial;
                }
                else if (mode == "tar")
                    sourceMaterial = FindNativeMaterial("Custom/Tar", "TarLiquid", "TarPit1", "TarPit2", "TarPit3");
                else if (mode == "water")
                    sourceMaterial = FindNativeMaterial("Custom/Water", "WaterLiquid", "piece_barber");

                if (sourceMaterial == null)
                {
                    return;
                }
                material = new Material(sourceMaterial);
                material.name = "VH_Native_" + mode + "_" + biome;
                if (terrain != null)
                {
                    var pb = new MaterialPropertyBlock();
                    terrain.GetPropertyBlock(pb);
                    if (!pb.isEmpty) surface.SetPropertyBlock(pb);
                    Bounds bounds = terrain.bounds;
                    Vector3[] verts = mesh.vertices;
                    var uvs = new Vector2[verts.Length];
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 wp = child.transform.TransformPoint(verts[i]);
                        uvs[i] = new Vector2((wp.x - bounds.min.x) / Mathf.Max(1f, bounds.size.x),
                                             (wp.z - bounds.min.z) / Mathf.Max(1f, bounds.size.z));
                    }
                    mesh.uv = uvs;
                    mesh.uv2 = uvs;
                }
                surface.sharedMaterial = material;
                if (terrain != null) CreateFlatFloorSkirt(terrain);
                surface.enabled = true;
                ready = true;

            }
            catch (Exception e) { Jotunn.Logger.LogError("[SurfaceTile] " + name + " error: " + e); }
        }

        private void OnDestroy()
        {
            if (ghostMaterial != null) Destroy(ghostMaterial);
            if (frameMaterial != null) Destroy(frameMaterial);
            if (guideFaceMaterial != null) Destroy(guideFaceMaterial);
            if (guideFaceMesh != null) Destroy(guideFaceMesh);
            if (floorSkirtMesh != null) Destroy(floorSkirtMesh);
            if (standaloneTerrain && child != null) Destroy(child);
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}

namespace ValeriusCustomPrefabs
{
    public sealed partial class TerrainPiece : MonoBehaviour
    {

        private static Color32 ColorForBiome(int value)
        {
            switch (value)
            {
                case 2: return new Color32(255, 0, 0, 0);
                case 4: case 64: return new Color32(0, 255, 0, 0);
                case 8: return new Color32(0, 0, 255, 0);
                case 16: return new Color32(0, 0, 0, 255);
                case 32: return new Color32(255, 0, 0, 255);
                case 512: return new Color32(0, 0, 255, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }


        private static MeshRenderer FindTerrain(Vector3 position, int requestedBiome)
        {
            MeshRenderer found = null;
            float best = float.MaxValue;
            foreach (Heightmap h in UnityEngine.Object.FindObjectsOfType<Heightmap>())
            {
                if (h == null) continue;
                MeshRenderer r = h.GetComponentInChildren<MeshRenderer>();
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null ||
                    r.sharedMaterial.shader.name != "Custom/Heightmap") continue;
                Bounds b = r.bounds;
                float dx = Mathf.Max(b.min.x - position.x, 0f, position.x - b.max.x);
                float dz = Mathf.Max(b.min.z - position.z, 0f, position.z - b.max.z);
                float distance = dx * dx + dz * dz;
                if (requestedBiome != 0 && NativeHeightmapBiomeMatches(h, requestedBiome))
                    distance -= 1000000f;
                if (distance < best) { best = distance; found = r; }
            }
            return found;
        }

        private static bool NativeHeightmapBiomeMatches(Heightmap heightmap, int requestedBiome)
        {
            try
            {
                var method = typeof(Heightmap).GetMethod("GetBiome",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(Vector3) }, null);
                if (method == null) return false;
                object value = method.Invoke(heightmap, new object[] { heightmap.transform.position });
                return value != null && (Convert.ToInt32(value) & requestedBiome) != 0;
            }
            catch { return false; }
        }

        private static Material FindNativeMaterial(string shader, params string[] prefabs)
        {
            foreach (string name in prefabs)
            {
                GameObject original = PrefabManager.Instance.GetPrefab(name);
                if (original == null) continue;
                foreach (Renderer r in original.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name == shader) return m;
            }
            return null;
        }
    }
}

namespace ValeriusCustomPrefabs
{
    internal static class TerrainIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            {'A', new[]{"01110","10001","10001","11111","10001","10001","10001"}},
            {'B', new[]{"11110","10001","10001","11110","10001","10001","11110"}},
            {'D', new[]{"11110","10001","10001","10001","10001","10001","11110"}},
            {'E', new[]{"11111","10000","10000","11110","10000","10000","11111"}},
            {'F', new[]{"11111","10000","10000","11110","10000","10000","10000"}},
            {'G', new[]{"01111","10000","10000","10111","10001","10001","01110"}},
            {'H', new[]{"10001","10001","10001","11111","10001","10001","10001"}},
            {'I', new[]{"11111","00100","00100","00100","00100","00100","11111"}},
            {'K', new[]{"10001","10010","10100","11000","10100","10010","10001"}},
            {'L', new[]{"10000","10000","10000","10000","10000","10000","11111"}},
            {'M', new[]{"10001","11011","10101","10101","10001","10001","10001"}},
            {'N', new[]{"10001","11001","10101","10011","10001","10001","10001"}},
            {'O', new[]{"01110","10001","10001","10001","10001","10001","01110"}},
            {'P', new[]{"11110","10001","10001","11110","10000","10000","10000"}},
            {'R', new[]{"11110","10001","10001","11110","10100","10010","10001"}},
            {'S', new[]{"01111","10000","10000","01110","00001","00001","11110"}},
            {'T', new[]{"11111","00100","00100","00100","00100","00100","00100"}},
            {'U', new[]{"10001","10001","10001","10001","10001","10001","01110"}},
            {'V', new[]{"10001","10001","10001","10001","10001","01010","00100"}},
            {'W', new[]{"10001","10001","10001","10101","10101","10101","01010"}}
        };
        public static Sprite Get(string id, string displayName)
        {
            string label = "??";
            if (id.StartsWith("ValeriusLava")) label = "LA";
            else if (id.StartsWith("ValeriusWater")) label = "WA";
            else if (id.StartsWith("ValeriusMountain") || id.StartsWith("ValeriusDeepNorth")) label = "SN";
            else if (id.StartsWith("ValeriusSwamp")) label = "SW";
            else if (id.StartsWith("ValeriusAshlands")) label = "AS";
            else if (id.StartsWith("ValeriusBlackForest")) label = "BL";
            else if (id.StartsWith("ValeriusMeadows")) label = "ME";
            else if (id.StartsWith("ValeriusMistlands")) label = "MI";
            else if (id.StartsWith("ValeriusPlains")) label = "PL";
            if (Cache.TryGetValue(label, out Sprite sprite)) return sprite;
            const int res = 96;
            Texture2D texture = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var background = new Color(0.19f, 0.19f, 0.19f, 1f);
            var border = new Color(0.49f, 0.44f, 0.35f, 1f);
            var foreground = new Color(1f, 0.78f, 0.30f, 1f);
            for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
                texture.SetPixel(x, y, x < 3 || y < 3 || x >= res - 3 || y >= res - 3 ? border : background);
            for (int i = 0; i < 2; i++)
            {
                if (!Glyphs.TryGetValue(label[i], out string[] rows)) continue;
                int startX = 13 + i * 38, startY = 26;
                for (int row = 0; row < 7; row++) for (int column = 0; column < 5; column++)
                    if (rows[row][column] == '1')
                        for (int dx = 0; dx < 6; dx++) for (int dy = 0; dy < 6; dy++)
                            texture.SetPixel(startX + column * 6 + dx, startY + (6 - row) * 6 + dy, foreground);
            }
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, false);
            sprite = Sprite.Create(texture, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            Cache[label] = sprite;
            return sprite;
        }
    }
}

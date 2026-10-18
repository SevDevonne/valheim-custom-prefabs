// Build to zip: dotnet build -c Release
// Build to game: dotnet build -c Debug
using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class ValeriusCustomPrefabs : BaseUnityPlugin
    {
        public const string PluginGUID = "com.valerius.customprefabs";
        public const string PluginName = "Valerius Custom Prefabs";
        public const string PluginVersion = "0.2.0";
        internal static ValeriusCustomPrefabs Instance;
        // All hazard gameplay and visual settings are deliberately hardcoded.
        // Change values here and rebuild the DLL to tune them.
        internal const float FireDamage = 15f;
        internal const float PoisonDamage = 12f;
        internal const float FrostDamage = 12f;
        internal const float LightningDamage = 20f;
        internal const float PhysicalDamage = 16f;
        internal const float TickSeconds = 1.5f;
        internal const bool AllowDamageToPlayers = true;

        internal const bool EnableCrust = true;
        internal const float CrustCoverage = 0.52f;
        internal const float CrustScale = 1.0f;
        internal const int CrustResolution = 128;
        internal const float CrustFlowSpeed = 1.16f; // standing-wave shoreline animation only
        internal const float CrustAnimationFPS = 12f;
        internal const float CrustAnimationDistance = 25f;
        internal const float BaseFlowMultiplier = 1.55f;
        internal const float LavaScrollU = 0.012f; // UV units per second, additive to native lava animation
        internal const float LavaScrollV = 0.075f;  // continuous GPU material scrolling, not CPU frame-stepped


        private void Awake()
        {
            Instance = this;
            Jotunn.Logger.LogInfo("Valerius Custom Prefabs is loading");
            new Harmony(PluginGUID).PatchAll();
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleWall;
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleFloor;
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleWall4x4;
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleFloor4x4;
            PrefabManager.OnVanillaPrefabsAvailable += CreateDungeonMarker;
            PrefabManager.OnVanillaPrefabsAvailable += CreateLava4x4;
        }


        private void CreateInvisibleWall()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleWall;
            try { AddInvisiblePiece("ValeriusInvisibleWall", "woodwall", "Invisible Boundary Wall", "A physical invisible boundary wall."); }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create Invisible Boundary Wall: " + e); }
        }
        private void CreateInvisibleFloor()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleFloor;
            try { AddInvisiblePiece("ValeriusInvisibleFloor", "wood_floor", "Invisible Boundary Floor", "A physical invisible boundary floor."); }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create Invisible Boundary Floor: " + e); }
        }
        private void CreateInvisibleWall4x4()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleWall4x4;
            try { AddInvisiblePiece("ValeriusInvisibleWall4x4", "woodwall", "Invisible Boundary Wall 4x4", "A physical invisible boundary wall.", new Vector3(2f, 2f, 1f)); }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create Invisible Boundary Wall 4x4: " + e); }
        }
        private void CreateInvisibleFloor4x4()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleFloor4x4;
            try { AddInvisiblePiece("ValeriusInvisibleFloor4x4", "wood_floor", "Invisible Boundary Floor 4x4", "A physical invisible boundary floor.", new Vector3(2f, 1f, 2f)); }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create Invisible Boundary Floor 4x4: " + e); }
        }
        private void AddInvisiblePiece(string prefabName, string basePrefabName, string displayName, string description, Vector3? scale = null)
        {
            var iconObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var icon = RenderManager.Instance.Render(iconObject, RenderManager.IsometricRotation);
            Destroy(iconObject);
            var config = new PieceConfig { Name = displayName, Description = description, PieceTable = "Hammer", Category = "Misc", Icon = icon };
            var piece = new CustomPiece(prefabName, basePrefabName, config);
            if (piece.PiecePrefab == null) throw new InvalidOperationException("Failed to create prefab '" + prefabName + "'.");
            if (scale.HasValue) piece.PiecePrefab.transform.localScale = scale.Value;
            var wearNTear = piece.PiecePrefab.GetComponent<WearNTear>();
            if (wearNTear != null) wearNTear.m_noSupportWear = true;
            piece.PiecePrefab.AddComponent<InvisiblePrefabBehaviour>();
            PieceManager.Instance.AddPiece(piece);
            Jotunn.Logger.LogInfo("Registered piece: " + displayName + " (" + prefabName + ")");
        }
        private void CreateDungeonMarker()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateDungeonMarker;
            try
            {
                var config = new PieceConfig
                {
                    Name = "Dungeon Marker",
                    Description = "A marker used to identify dungeon areas when scripting with Expand World Prefabs.",
                    PieceTable = "Hammer",
                    Category = "Misc"
                };
                var piece = new CustomPiece("ValeriusDungeonMarker", "guard_stone", config);
                if (piece.PiecePrefab == null) throw new InvalidOperationException("Failed to create Dungeon Marker.");
                if (piece.PiecePrefab.GetComponent<TeleportWorld>() == null) piece.PiecePrefab.AddComponent<TeleportWorld>();
                foreach (var trigger in piece.PiecePrefab.GetComponentsInChildren<TeleportWorldTrigger>(true)) DestroyImmediate(trigger, true);
                var privateArea = piece.PiecePrefab.GetComponent<PrivateArea>();
                if (privateArea != null) DestroyImmediate(privateArea, true);
                piece.PiecePrefab.transform.localScale = Vector3.one * 0.5f;
                PieceManager.Instance.AddPiece(piece);
                Jotunn.Logger.LogInfo("Registered piece: Dungeon Marker (ValeriusDungeonMarker)");
            }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create Dungeon Marker: " + e); }
        }

        private void CreateLava4x4()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateLava4x4;
            try
            {
                var config = new PieceConfig
                {
                    Name = "Valerius Lava 4x4",
                    Description = "Molten hazard. Configurable visuals and fire damage.",
                    PieceTable = "Hammer",
                    Category = "Misc"
                };
                var piece = new CustomPiece("ValeriusLava4x4", "wood_floor", config);
                if (piece.PiecePrefab == null)
                    throw new InvalidOperationException("Could not create ValeriusLava4x4");

                // Keep the original prefab ID so existing placed lava pieces can resolve.
                piece.PiecePrefab.transform.localScale = new Vector3(2f, 1f, 2f);
                piece.PiecePrefab.AddComponent<HazardVolume>();

                // Retain original floor collider and network view; hide only its wood renderers.
                var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                surface.name = "VH_LavaVisual";
                surface.transform.SetParent(piece.PiecePrefab.transform, false);
                surface.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                surface.transform.localScale = new Vector3(2f, 2f, 1f);
                var c = surface.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
                var surfaceRenderer = surface.GetComponent<MeshRenderer>();
                foreach (var renderer in piece.PiecePrefab.GetComponentsInChildren<Renderer>(true))
                    if (renderer != surfaceRenderer) renderer.enabled = false;

                var crust = GameObject.CreatePrimitive(PrimitiveType.Quad);
                crust.name = "VH_LavaCrust";
                crust.transform.SetParent(piece.PiecePrefab.transform, false);
                crust.transform.localPosition = new Vector3(0f, 0.157f, 0f);
                crust.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                crust.transform.localScale = new Vector3(2f, 2f, 1f);
                var crustCollider = crust.GetComponent<Collider>();
                if (crustCollider != null) DestroyImmediate(crustCollider);
                crust.GetComponent<MeshRenderer>().enabled = false;

                piece.PiecePrefab.AddComponent<ValeriusLavaVisual>();
                PieceManager.Instance.AddPiece(piece);
                Jotunn.Logger.LogInfo("Registered piece: Valerius Lava 4x4 (ValeriusLava4x4)");
            }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to register Lava 4x4: " + e); }
        }
    }

    internal class InvisiblePrefabBehaviour : MonoBehaviour
    {
        private WearNTear wearNTear;
        private Renderer[] renderers;
        private void Awake()
        {
            wearNTear = GetComponent<WearNTear>();
            renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = false;
        }
    }
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnPlaced))]
    internal static class WearNTearOnPlacedPatch
    {
        private static void Postfix(WearNTear __instance)
        {
            if (__instance == null || !__instance.TryGetComponent<InvisiblePrefabBehaviour>(out _)) return;
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || __instance.m_health <= 0f) return;
            float halfHealth = __instance.m_health * 0.5f;
            nview.GetZDO().Set(ZDOVars.s_health, halfHealth);
            nview.InvokeRPC(ZNetView.Everybody, "RPC_HealthChanged", halfHealth);
        }
    }

    // Visuals only. No damage, network messages, timers that affect gameplay,
    // or custom shaders. Old placed ValeriusLava4x4 instances
    // receive the new visuals automatically on reload.
    public sealed class ValeriusLavaVisual : MonoBehaviour
    {
        private MeshRenderer surface;
        private MeshRenderer crustRenderer;
        private Material crustMaterial;
        private Texture2D crustTexture;
        private Color32[] crustPixels;
        private Mesh worldAlignedMesh;
        private Vector3[] moltenVertices;
        private Vector2[] moltenBaseUVs;
        private Vector2[] moltenWorkingUVs;
        private float nextMoltenDistortion;
        // A dense local mesh bends the flowing lava UVs around nearby crust edges.
        // No extra material, collider or networked prefab is needed.
        private const int MoltenGridSegments = 18;
        private const float EddyUvStrength = 0.042f; // UV units
        private const float EddyFramesPerSecond = 20f;
        private float nextCrustFrame;
        private bool initialized;
        private float nextAttempt;
        private static Material chosenMaterial;
        private static string materialSource;
        private static bool loggedMaterials;
        private static float lastGlobalLavaUpdate = -1f;

        private void Awake()
        {
            var t = transform.Find("VH_LavaVisual");
            if (t != null) surface = t.GetComponent<MeshRenderer>();
            var crust = transform.Find("VH_LavaCrust");
            if (crust != null) crustRenderer = crust.GetComponent<MeshRenderer>();
        }

        private static Material FindMaterialInPrefab(string prefabName, string materialToken)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null) return null;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.IndexOf(materialToken, StringComparison.OrdinalIgnoreCase) >= 0)
                        return m;
            return null;
        }

        private void InitializeVisual()
        {
            if (surface == null)
            {
                var t = transform.Find("VH_LavaVisual");
                if (t != null) surface = t.GetComponent<MeshRenderer>();
            }
            if (surface == null) return;

            if (chosenMaterial == null)
            {
                // Prefer the animated lava monster's material. It has a flow shader,
                // while Black Forge is our previously tested fallback.
                var source = FindMaterialInPrefab("LavaBlob", "LavaBlob_mat")
                          ?? FindMaterialInPrefab("BlobLava", "LavaBlob_mat")
                          ?? FindMaterialInPrefab("blackforge", "BlackForgeLava");
                if (source == null) return;
                materialSource = source.name;
                chosenMaterial = new Material(source);
                chosenMaterial.name = "ValeriusLava_Visual_WorldAligned";
                var plugin = ValeriusCustomPrefabs.Instance;
                if (plugin != null)
                {
                    float factor = Mathf.Max(0f, ValeriusCustomPrefabs.BaseFlowMultiplier);
                    foreach (string speed in new[] { "_SpeedX", "_SpeedY", "_FlowSpeedX", "_FlowSpeedY" })
                        if (chosenMaterial.HasProperty(speed))
                            chosenMaterial.SetFloat(speed, chosenMaterial.GetFloat(speed) * factor);
                }
                // Normalize tiling on all available texture slots. This does not
                // alter Valheim's shared source material.
                foreach (var propertyName in chosenMaterial.GetTexturePropertyNames())
                {
                    try
                    {
                        if (chosenMaterial.GetTexture(propertyName) != null)
                        {
                            chosenMaterial.SetTextureScale(propertyName, Vector2.one);
                            chosenMaterial.SetTextureOffset(propertyName, Vector2.zero);
                        }
                    }
                    catch (Exception) { /* Some shader slots do not allow UV transforms. */ }
                }
            }

            surface.sharedMaterial = chosenMaterial;
            // Mesh UVs use WORLD X/Z: neighbouring tiles share the same molten texture coordinates.
            AlignSurfaceUVs();
            surface.enabled = true;
            SetupCrust();
            // WearNTear can reactivate wood renderers; keep only our quad visible.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r != surface && r != crustRenderer) r.enabled = false;
            initialized = true;
            if (!loggedMaterials)
            {
                loggedMaterials = true;
                Debug.Log("[VH LavaVisual] Using " + materialSource +
                          " shader=" + chosenMaterial.shader.name +
                          "; UV scales normalized to 1.0, 4x4 plane, wood hidden.");
            }
        }

        // A quad normally repeats UV=0..1 for every 4x4 tile. Instead, express its
        // UVs in world metres so the original flowing shader has no per-tile reset.
        // The shared material continues handling its own native animation.
        private void AlignSurfaceUVs()
        {
            if (worldAlignedMesh != null || surface == null) return;
            var filter = surface.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            var template = filter.sharedMesh;
            // The original quad's winding and normal determine which side is visible.
            Vector3 normal = template.normals != null && template.normals.Length > 0
                ? template.normals[0] : Vector3.back;
            int side = MoltenGridSegments + 1;
            moltenVertices = new Vector3[side * side];
            moltenBaseUVs = new Vector2[side * side];
            moltenWorkingUVs = new Vector2[side * side];
            var normals = new Vector3[side * side];
            var triangles = new int[MoltenGridSegments * MoltenGridSegments * 6];
            int tri = 0;
            for (int y = 0; y <= MoltenGridSegments; y++)
                for (int x = 0; x <= MoltenGridSegments; x++)
                {
                    int index = y * side + x;
                    var v = new Vector3(x / (float)MoltenGridSegments - 0.5f,
                                        y / (float)MoltenGridSegments - 0.5f, 0f);
                    moltenVertices[index] = v;
                    normals[index] = normal;
                    var world = surface.transform.TransformPoint(v);
                    moltenBaseUVs[index] = new Vector2(world.x / 4f, world.z / 4f);
                    moltenWorkingUVs[index] = moltenBaseUVs[index];
                    if (x == MoltenGridSegments || y == MoltenGridSegments) continue;
                    int a = index, b = index + 1, c = index + side, d = c + 1;
                    if (normal.z < 0f)
                    {
                        triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = b;
                        triangles[tri++] = b; triangles[tri++] = c; triangles[tri++] = d;
                    }
                    else
                    {
                        triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = c;
                        triangles[tri++] = b; triangles[tri++] = d; triangles[tri++] = c;
                    }
                }
            worldAlignedMesh = new Mesh();
            worldAlignedMesh.name = "VH_ShoreDeflectedLavaMesh";
            worldAlignedMesh.vertices = moltenVertices;
            worldAlignedMesh.normals = normals;
            worldAlignedMesh.uv = moltenWorkingUVs;
            worldAlignedMesh.triangles = triangles;
            worldAlignedMesh.RecalculateBounds();
            worldAlignedMesh.MarkDynamic();
            filter.sharedMesh = worldAlignedMesh;
        }

        // Keep the crust fixed; deform ONLY the UVs of lava near the crust edge.
        // The vector along the shore is perpendicular to the crust-noise gradient.
        private void UpdateMoltenShoreFlow()
        {
            if (worldAlignedMesh == null || moltenWorkingUVs == null || surface == null) return;
            var player = Player.m_localPlayer;
            if (player == null || !surface.isVisible ||
                (player.transform.position - transform.position).sqrMagnitude > 625f) return;

            float now = Time.time;
            float scale = Mathf.Max(0.15f, ValeriusCustomPrefabs.CrustScale);
            float coverage = Mathf.Clamp01(ValeriusCustomPrefabs.CrustCoverage);
            // Slow, continuously changing eddies, with stable world-space alignment.
            float waveTime = now * 0.90f;
            for (int i = 0; i < moltenWorkingUVs.Length; i++)
            {
                Vector3 world = surface.transform.TransformPoint(moltenVertices[i]);
                float wx = world.x, wz = world.z;
                // Use the crust *silhouette* for proximity, not its animated opacity.
                // Thus no global downward drift is introduced into the crust.
                float mid = SampleCrust(wx, wz, 0f, scale, coverage);
                float nearShore = 4f * mid * (1f - mid);
                nearShore *= nearShore;
                // Normal to the crust coastline, estimated on the fixed noise field.
                const float epsilon = 0.18f;
                float dx = SampleCrust(wx + epsilon, wz, 0f, scale, coverage)
                         - SampleCrust(wx - epsilon, wz, 0f, scale, coverage);
                float dz = SampleCrust(wx, wz + epsilon, 0f, scale, coverage)
                         - SampleCrust(wx, wz - epsilon, 0f, scale, coverage);
                var tangent = new Vector2(-dz, dx);
                if (tangent.sqrMagnitude > 0.000001f) tangent.Normalize();
                // Local standing oscillation + weaker secondary eddy. Tangential
                // displacement prevents the lava from sliding straight through rock.
                float phase = wx * 0.51f + wz * 0.39f;
                float motion = Mathf.Sin(waveTime + phase) * 0.75f +
                               Mathf.Sin(waveTime * 0.47f - phase * 1.4f) * 0.25f;
                moltenWorkingUVs[i] = moltenBaseUVs[i] +
                    tangent * (nearShore * EddyUvStrength * motion);
            }
            worldAlignedMesh.uv = moltenWorkingUVs;
        }

        // Same world-space noise inputs on every tile, so crust shapes continue
        // across boundaries. Time changes the coordinate WARP, not the position
        // of the entire crust pattern: islands mostly stay in place while their
        // edges gently advance and retreat like lava lapping against rock.
        private static float SmoothEdge(float a, float b, float value)
        {
            float t = Mathf.Clamp01((value - a) / Mathf.Max(0.0001f, b - a));
            return t * t * (3f - 2f * t);
        }

        private static float SampleCrust(float wx, float wz, float time, float scale, float coverage)
        {
            // Immutable crust silhouette: ALL noise inputs are world position only.
            // No UV scrolling, moving noise coordinates, or traveling wave phase.
            float warpX = Mathf.PerlinNoise(wx * 0.13f + 17.3f, wz * 0.13f + 42.1f);
            float warpZ = Mathf.PerlinNoise(wx * 0.12f + 73.7f, wz * 0.12f + 11.9f);
            float u = (wx + (warpX - 0.5f) * 1.05f) * scale;
            float v = (wz + (warpZ - 0.5f) * 1.05f) * scale;
            float large = Mathf.PerlinNoise(u * 0.82f + 8.3f, v * 0.82f + 21.5f);
            float medium = Mathf.PerlinNoise(u * 2.15f + 31.7f, v * 2.15f + 7.9f);
            float small = Mathf.PerlinNoise(u * 4.8f + 3.1f, v * 4.8f + 71.3f);
            float pattern = large * 0.60f + medium * 0.30f + small * 0.10f;
            float baseline = Mathf.Lerp(0.77f, 0.25f, coverage);

            // Each shoreline location has a FIXED amplitude. Time is a standing
            // oscillation (in/out), not a phase advancing along world Z.
            float amplitudeA = (Mathf.PerlinNoise(wx * 0.30f + 105f, wz * 0.30f + 17f) * 2f - 1f);
            float amplitudeB = (Mathf.PerlinNoise(wx * 0.51f + 13f, wz * 0.51f + 93f) * 2f - 1f);
            float lap = amplitudeA * Mathf.Sin(time * 0.72f) * 0.035f
                      + amplitudeB * Mathf.Sin(time * 1.13f) * 0.018f;

            // Restrict changes to the fixed crust shoreline. Beyond this band,
            // the entire rock interior is completely static and opaque.
            float distanceFromShore = Mathf.Abs(pattern - baseline);
            float shoreInfluence = 1f - SmoothEdge(0.045f, 0.14f, distanceFromShore);
            float threshold = baseline + lap * shoreInfluence;
            return SmoothEdge(threshold - 0.050f, threshold + 0.050f, pattern);
        }

        // One global continuously varying offset for the common material.
        // All world-aligned quads see identical motion, without per-tile resets.
        private static void AnimateMoltenLayer()
        {
            if (chosenMaterial == null || Time.time == lastGlobalLavaUpdate) return;
            lastGlobalLavaUpdate = Time.time;
            float t = Time.time;
            var offset = new Vector2(
                t * ValeriusCustomPrefabs.LavaScrollU,
                t * ValeriusCustomPrefabs.LavaScrollV);
            foreach (string property in chosenMaterial.GetTexturePropertyNames())
            {
                try
                {
                    if (chosenMaterial.GetTexture(property) != null)
                        chosenMaterial.SetTextureOffset(property, offset);
                }
                catch (Exception) { /* Some shader slots do not support UV transforms. */ }
            }
        }

        private void RenderCrust(float elapsedSeconds)
        {
            if (crustTexture == null || crustPixels == null || crustRenderer == null) return;
            var plugin = ValeriusCustomPrefabs.Instance;
            if (plugin == null) return;

            int res = crustTexture.width;
            float coverage = Mathf.Clamp01(ValeriusCustomPrefabs.CrustCoverage);
            float scale = Mathf.Max(0.15f, ValeriusCustomPrefabs.CrustScale);
            float time = Mathf.Max(0f, ValeriusCustomPrefabs.CrustFlowSpeed) * elapsedSeconds;
            Transform t = crustRenderer.transform;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // Exact edge coordinates, including rotated tiles.
                    Vector3 world = t.TransformPoint(new Vector3(
                        x / (float)(res - 1) - 0.5f,
                        y / (float)(res - 1) - 0.5f, 0f));
                    float crust = SampleCrust(world.x, world.z, time, scale, coverage);
                    // A small darker body with a warm, translucent shoreline.
                    float rim = 4f * crust * (1f - crust);
                    float red = Mathf.Lerp(0.105f, 0.22f, rim);
                    float green = Mathf.Lerp(0.062f, 0.095f, rim);
                    float blue = Mathf.Lerp(0.046f, 0.038f, rim);
                    // Solid crust never shows the molten animation bleeding through.
                    float opacity = Mathf.Clamp01(crust);
                    crustPixels[y * res + x] = new Color32(
                        (byte)(red * 255f), (byte)(green * 255f),
                        (byte)(blue * 255f), (byte)(opacity * 255f));
                }
            }
            crustTexture.SetPixels32(crustPixels);
            crustTexture.Apply(false, false);
        }

        private void SetupCrust()
        {
            var plugin = ValeriusCustomPrefabs.Instance;
            if (plugin == null) return;
            if (crustRenderer == null)
            {
                var child = transform.Find("VH_LavaCrust");
                if (child != null) crustRenderer = child.GetComponent<MeshRenderer>();
            }
            if (crustRenderer == null) return;
            if (!ValeriusCustomPrefabs.EnableCrust)
            {
                crustRenderer.enabled = false;
                return;
            }
            if (crustMaterial == null)
            {
                var shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogWarning("[VH LavaVisual] Transparent crust shader unavailable.");
                    crustRenderer.enabled = false;
                    return;
                }
                int res = Mathf.Clamp(ValeriusCustomPrefabs.CrustResolution, 64, 256);
                crustTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
                crustTexture.name = "VH_WorldAlignedCrust";
                crustTexture.wrapMode = TextureWrapMode.Clamp;
                crustTexture.filterMode = FilterMode.Bilinear;
                crustPixels = new Color32[res * res];
                crustMaterial = new Material(shader);
                crustMaterial.name = "VH_AnimatedCrust";
                crustMaterial.mainTexture = crustTexture;
                RenderCrust(Time.time);
            }
            crustRenderer.sharedMaterial = crustMaterial;
            crustRenderer.enabled = true;
            // Stagger refresh to avoid all tiles regenerating in the same frame.
            nextCrustFrame = Time.time + Mathf.Abs(transform.position.x * 0.173f +
                transform.position.z * 0.337f) % 0.5f;
        }

        private void OnDestroy()
        {
            if (crustMaterial != null) Destroy(crustMaterial);
            if (crustTexture != null) Destroy(crustTexture);
            if (worldAlignedMesh != null) Destroy(worldAlignedMesh);
        }

        private void Update()
        {
            if (!initialized && Time.time >= nextAttempt)
            {
                nextAttempt = Time.time + 3f;
                InitializeVisual();
            }
            // Molten texture scrolls smoothly on the GPU every rendered frame.
            if (initialized) AnimateMoltenLayer();
            if (initialized && Time.time >= nextMoltenDistortion)
            {
                nextMoltenDistortion = Time.time + 1f / EddyFramesPerSecond;
                UpdateMoltenShoreFlow();
            }
            // Procedural crust animation is throttled and only runs near the player.
            var plugin = ValeriusCustomPrefabs.Instance;
            if (initialized && plugin != null && crustRenderer != null && crustRenderer.enabled &&
                ValeriusCustomPrefabs.CrustAnimationFPS > 0f && Time.time >= nextCrustFrame)
            {
                nextCrustFrame = Time.time + 1f / Mathf.Clamp(ValeriusCustomPrefabs.CrustAnimationFPS, 0.2f, 12f);
                var player = Player.m_localPlayer;
                if (player != null && crustRenderer.isVisible &&
                    (player.transform.position - transform.position).sqrMagnitude <=
                    Mathf.Pow(Mathf.Max(4f, ValeriusCustomPrefabs.CrustAnimationDistance), 2f))
                    RenderCrust(Time.time);
            }

        }
    }

    public sealed class HazardVolume : MonoBehaviour
    {
        [SerializeField] private string kind = "lava";
        [SerializeField] private Vector3 size = new Vector3(4f, 0.65f, 4f);

        // Legacy local-debug flag retained for compatibility; no hotkeys or spawns.
        private bool localDebugVolume;
        private ZNetView nview;
        private float nextCheck;
        private readonly HashSet<int> deduplicated = new HashSet<int>();
        private static readonly Collider[] hits = new Collider[256];

        // All tiles of the same type share the same hit timer for each target.
        // This stops overlapping lava tiles repeatedly hitting one creature.
        private static readonly Dictionary<string, float> sharedLastHit =
            new Dictionary<string, float>();
        private static float lastCleanup;

        private void Awake()
        {
            nview = GetComponent<ZNetView>();
        }

        public void Configure(string hazardKind, Vector3 dimensions, bool isLocalDebug = false)
        {
            kind = (hazardKind ?? "lava").ToLowerInvariant();
            size = dimensions;
            localDebugVolume = isLocalDebug;
        }

        private void FixedUpdate()
        {
            var plugin = ValeriusCustomPrefabs.Instance;
            if (plugin == null) return;

            // Hammer placement ghosts and Jotunn prefab templates are not
            // live networked world objects, so they must never deal damage.
            // The F8 test volume is an explicit exception.
            if (!localDebugVolume && nview == null)
                nview = GetComponent<ZNetView>();
            if (!localDebugVolume &&
                (nview == null || !nview.IsValid() || nview.GetZDO() == null))
                return;

            float interval = Mathf.Max(0.25f, ValeriusCustomPrefabs.TickSeconds);
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + interval;
            deduplicated.Clear();

            // Clean up old shared cooldown records occasionally.
            if (Time.time - lastCleanup > 60f)
            {
                lastCleanup = Time.time;
                var expired = new List<string>();
                foreach (var entry in sharedLastHit)
                    if (Time.time - entry.Value > 120f) expired.Add(entry.Key);
                foreach (var key in expired) sharedLastHit.Remove(key);
            }

            int count = Physics.OverlapBoxNonAlloc(
                transform.position, size / 2f, hits, transform.rotation);

            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i];
                if (collider == null) continue;

                Character target = collider.GetComponentInParent<Character>();
                if (target == null || !deduplicated.Add(target.GetInstanceID())) continue;
                if (target is Player && !ValeriusCustomPrefabs.AllowDamageToPlayers) continue;

                // Damage should be calculated only by the owner of the target.
                // For now, skip targets without valid network ownership except
                // when testing with the F8 local-only prototype.
                ZNetView targetView = target.GetComponent<ZNetView>();
                if (!localDebugVolume &&
                    (targetView == null || !targetView.IsValid() || !targetView.IsOwner()))
                    continue;
                if (localDebugVolume && targetView != null &&
                    targetView.IsValid() && !targetView.IsOwner())
                    continue;

                string key = target.GetInstanceID() + ":" + kind;
                float last;
                if (sharedLastHit.TryGetValue(key, out last) &&
                    Time.time - last < interval)
                    continue;

                var hit = new HitData();
                float amount;
                switch (kind)
                {
                    case "poison":
                        amount = ValeriusCustomPrefabs.PoisonDamage;
                        hit.m_damage.m_poison = amount;
                        break;
                    case "frost":
                        amount = ValeriusCustomPrefabs.FrostDamage;
                        hit.m_damage.m_frost = amount;
                        break;
                    case "lightning":
                        amount = ValeriusCustomPrefabs.LightningDamage;
                        hit.m_damage.m_lightning = amount;
                        break;
                    case "physical":
                        amount = ValeriusCustomPrefabs.PhysicalDamage;
                        hit.m_damage.m_pierce = amount;
                        break;
                    default:
                        amount = ValeriusCustomPrefabs.FireDamage;
                        hit.m_damage.m_fire = amount;
                        break;
                }

                if (amount <= 0f) continue;
                sharedLastHit[key] = Time.time;
                target.Damage(hit);
            }
        }
    }
}

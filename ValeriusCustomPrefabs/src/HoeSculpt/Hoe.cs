using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;

namespace ValeriusCustomPrefabs
{
internal static class SculptRegistry
    {
        internal static void CreateHoeSculpt()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateHoeSculpt;
            try
            {
                var config = new PieceConfig
                {
                    Name = "Valerius Sculpt",
                    Description = "Aim at Valerius terrain and scroll to raise/lower. Alt+Scroll adjusts brush radius.",
                    PieceTable = "Hoe",
                    Category = "Misc",
                    Icon = PrefabManager.Instance.GetPrefab("wood_floor").GetComponent<Piece>().m_icon
                };
                var piece = new CustomPiece(SculptTool.PrefabId, "wood_floor", config);
                if (piece.PiecePrefab == null) throw new InvalidOperationException("Sculpt piece prefab missing.");
                Piece sculptPiece = piece.PiecePrefab.GetComponent<Piece>();
                if (sculptPiece != null)
                {
                    sculptPiece.m_resources = new Piece.Requirement[0];
                    sculptPiece.m_craftingStation = null;
                }
                foreach (Renderer renderer in piece.PiecePrefab.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
                foreach (Collider collider in piece.PiecePrefab.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (WearNTear wear in piece.PiecePrefab.GetComponentsInChildren<WearNTear>(true))
                    UnityEngine.Object.DestroyImmediate(wear);
                if (!PieceManager.Instance.AddPiece(piece))
                    throw new InvalidOperationException("Could not register Valerius Sculpt in Hoe.");
            }
            catch (Exception e) { Jotunn.Logger.LogError("[Sculpt] Hoe registration failed: " + e); }
        }
    }

    internal sealed class SculptTool : MonoBehaviour
    {
        internal const string PrefabId = "ValeriusSculpt";
        internal static SculptMode Mode = SculptMode.Raise;
        private static ConfigEntry<float> radius;
        private static ConfigEntry<float> strength;
        private static Rect panel;

        internal static bool IsSelected(Piece piece)
        {
            return piece != null && piece.gameObject != null &&
                piece.gameObject.name.Replace("(Clone)", "") == PrefabId;
        }

        internal static float Radius { get { return radius != null ? Mathf.Clamp(radius.Value, 0.5f, 8f) : 1.5f; } }
        internal static float Strength { get { return strength != null ? Mathf.Clamp(strength.Value, 0.01f, 1f) : 0.15f; } }

        private void Awake()
        {
            radius = ValeriusCustomPrefabs.Instance.Config.Bind("Hoe Sculpt", "Brush Radius", 1.5f,
                "Radius in metres of the brush (diameter is twice this value). Range 0.5-8.");
            strength = ValeriusCustomPrefabs.Instance.Config.Bind("Hoe Sculpt", "Brush Strength", 0.15f,
                "Raise/lower metres per scroll notch; smoothing uses the same control. Range 0.01-1.");
        }

        // Scroll sculpts; Alt+Scroll adjusts radius.
        private void Update()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !HoeTarget.IsHoeEquipped(player) ||
                !IsSelected(player.GetSelectedPiece()) ||
                (Hud.instance != null && Hud.IsPieceSelectionVisible())) return;

            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (alt && radius != null)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                    radius.Value = Mathf.Clamp(Mathf.Round((Radius + Mathf.Sign(scroll) * 0.25f) * 4f) / 4f, 0.5f, 8f);
            }
            if (alt && Input.GetKeyDown(KeyCode.S))
                Mode = Mode == SculptMode.Smooth ? SculptMode.Raise : SculptMode.Smooth;
            if (!alt)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    SculptTile tile;
                    Vector3 at;
                    if (HoeTarget.TryGetSculpt(player, player.GetSelectedPiece(), out tile, out at))
                    {
                        SculptMode editMode = Mode == SculptMode.Smooth
                            ? SculptMode.Smooth
                            : (wheel > 0f ? SculptMode.Raise : SculptMode.Lower);
                        SculptTile.SculptConnected(tile, at, Strength, Radius, editMode);
                    }
                }
            }
            if (strength != null)
            {
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                    strength.Value = Mathf.Min(1f, Mathf.Round((Strength + 0.01f) * 100f) / 100f);
                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                    strength.Value = Mathf.Max(0.01f, Mathf.Round((Strength - 0.01f) * 100f) / 100f);
            }
        }

        private void OnGUI()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !IsSelected(player.GetSelectedPiece()) ||
                !HoeTarget.IsHoeEquipped(player)) return;

            float x = Mathf.Clamp(Screen.width * 0.38f, 10f, Mathf.Max(10f, Screen.width - 540f));
            float width = Mathf.Min(520f, Screen.width - x - 10f);
            panel = new Rect(x, Screen.height - 230f, width, 165f);
            Color original = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.10f, 0.62f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = original;

            GUIStyle title = new GUIStyle(GUI.skin.label);
            title.fontSize = 17;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.normal.textColor = new Color(1f, 0.75f, 0.35f);
            GUIStyle line = new GUIStyle(GUI.skin.label);
            line.fontSize = 13;
            line.normal.textColor = new Color(0.96f, 0.93f, 0.84f);
            GUIStyle shortcut = new GUIStyle(line);
            shortcut.normal.textColor = new Color(1f, 0.82f, 0.16f);

            float left = panel.x + 12f;
            float yy = panel.y + 7f;
            GUI.Label(new Rect(left, yy, width - 24f, 25f), "Valerius Sculpt — " + Mode, title);
            yy += 33f;
            GUI.Label(new Rect(left, yy, width - 24f, 22f), "[Scroll] Raise / lower aimed Valerius terrain", line);
            yy += 24f;
            GUI.Label(new Rect(left, yy, width - 24f, 20f), "[Alt + Scroll] Brush radius: " + Radius.ToString("F2") + " m", shortcut);
            yy += 21f;
            GUI.Label(new Rect(left, yy, width - 24f, 20f), "[+ / -] Brush strength: " + Strength.ToString("F2"), shortcut);
            yy += 21f;
            GUI.Label(new Rect(left, yy, width - 24f, 20f), "[Alt + S] Toggle smooth / raise-lower", shortcut);
        }
    }

internal static class HoeTarget
    {
        internal static bool TryGet(Player player, Piece selected, out SculptTile tile, out TerrainOp op, out Vector3 hitPosition)
        {
            tile = null; op = null; hitPosition = Vector3.zero;
            if (player == null || player != Player.m_localPlayer || selected == null || Camera.main == null) return false;
            op = selected.GetComponent<TerrainOp>();
            if (op == null || (!op.m_settings.m_level && !op.m_settings.m_raise && !op.m_settings.m_smooth)) return false;
            if (SculptTool.IsSelected(selected) || !IsHoeEquipped(player)) return false;
            return TryGetTile(player, out tile, out hitPosition);
        }

        internal static bool IsHoeEquipped(Player player)
        {
            if (player == null) return false;
            ItemDrop.ItemData item = player.GetRightItem();
            if (item == null || item.m_dropPrefab == null) return false;
            string toolName = item.m_dropPrefab.name;
            string displayName = item.m_shared != null ? item.m_shared.m_name : "";
            return toolName.IndexOf("hoe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("hoe", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool TryGetSculpt(Player player, Piece piece,
            out SculptTile tile, out Vector3 hitPosition)
        {
            tile = null; hitPosition = Vector3.zero;
            if (!SculptTool.IsSelected(piece) || !IsHoeEquipped(player)) return false;
            return TryGetTile(player, out tile, out hitPosition);
        }

        private static bool TryGetTile(Player player, out SculptTile tile, out Vector3 hitPosition)
        {
            tile = null; hitPosition = Vector3.zero;
            if (player == null || player != Player.m_localPlayer || Camera.main == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), 10f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                SculptTile found = hit.collider.GetComponentInParent<SculptTile>();
                if (found == null)
                {
                    SculptColliderLink link = hit.collider.GetComponent<SculptColliderLink>();
                    if (link != null) found = link.owner;
                }
                if (found != null)
                {
                    tile = found; hitPosition = hit.point; return true;
                }
                // Do not stop at the natural heightmap: a Valerius sculpt
                // collider may be farther along the same ray.
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class HoeGhostPatch
    {
        private static readonly System.Reflection.FieldInfo PlacementStatus = AccessTools.Field(typeof(Player), "m_placementStatus");
        private static void Postfix(Player __instance)
        {
            if (PlacementStatus == null) return;
            SculptTile tile; TerrainOp op; Vector3 at;
            Piece selected = __instance.GetSelectedPiece();
            bool target = SculptTool.IsSelected(selected)
                ? HoeTarget.TryGetSculpt(__instance, selected, out tile, out at)
                : HoeTarget.TryGet(__instance, selected, out tile, out op, out at);
            if (target)
            {
                PlacementStatus.SetValue(__instance, Enum.ToObject(PlacementStatus.FieldType, 0));
            }
        }
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    internal static class HoePlacePatch
    {
        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            SculptTile tile; TerrainOp op; Vector3 at;
            if (SculptTool.IsSelected(piece))
            {
                __result = false;
                return false;
            }
            if (!HoeTarget.TryGet(__instance, piece, out tile, out op, out at)) return true;
            __result = SculptTile.ApplyHoeOperation(tile, at, op);
            return false;
        }
    }
}

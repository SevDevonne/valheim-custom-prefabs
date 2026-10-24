using BepInEx.Configuration;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    internal sealed class TerrainMenu : MonoBehaviour
    {
        private static TerrainMenu current;
        private readonly Dictionary<string, GameObject> visibleSlots = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Piece> hiddenPieces = new Dictionary<string, Piece>();
        private readonly Dictionary<string, ConfigEntry<string>> rememberedPrefabs = new Dictionary<string, ConfigEntry<string>>();
        private readonly Dictionary<string, ConfigEntry<string>> shapes = new Dictionary<string, ConfigEntry<string>>();
        private readonly Dictionary<string, ConfigEntry<int>> angles = new Dictionary<string, ConfigEntry<int>>();
        private static readonly System.Reflection.FieldInfo hoveredField = AccessTools.Field(typeof(Hud), "m_hoveredPiece");
        private string openBase;
        private Rect iconRect;
        private Rect popupRect;
        private bool switching;
        private GUIStyle buttonStyle;
        private GUIStyle selectedStyle;
        private GUIStyle panelStyle;
        private Texture2D uiPixel;
        private static readonly Color woodColor = new Color(0.20f, 0.13f, 0.075f, 0.98f);
        private static readonly Color buttonColor = new Color(0.30f, 0.29f, 0.28f, 1f);
        private static readonly Color goldColor = new Color(1f, 0.84f, 0.05f, 1f);

        private static bool TryGroup(Piece piece, out string baseId)
        {
            baseId = null;
            if (piece == null || piece.gameObject == null) return false;
            string prefabId = piece.gameObject.name.Replace("(Clone)", "").Trim();
            string found = BaseForVariant(prefabId);
            baseId = found;
            return found != null;
        }

        private static readonly string[] TerrainIds = {
            "ValeriusLava", "ValeriusWater", "ValeriusMeadows",
            "ValeriusBlackForest", "ValeriusSwamp", "ValeriusPlains",
            "ValeriusMountain", "ValeriusMistlands",
            "ValeriusAshlandsGround"
        };

        private static bool IsKnownTerrain(string name)
        {
            foreach (string id in TerrainIds)
                if (name == id || name.StartsWith(id + "_", StringComparison.Ordinal)) return true;
            return false;
        }

        private void Awake() { current = this; }
        private void OnDestroy()
        {
            if (current == this) current = null;
            if (uiPixel != null) Destroy(uiPixel);
        }

        private ConfigEntry<string> Shape(string id)
        {
            ConfigEntry<string> entry;
            if (!shapes.TryGetValue(id, out entry))
                shapes[id] = entry = ValeriusCustomPrefabs.Instance.Config.Bind(
                    "Hammer Terrain Shapes", id, "Flat", "Flat, Outer, Inner, or Slope");
            return entry;
        }

        private ConfigEntry<int> Angle(string id)
        {
            ConfigEntry<int> entry;
            if (!angles.TryGetValue(id, out entry))
                angles[id] = entry = ValeriusCustomPrefabs.Instance.Config.Bind(
                    "Hammer Terrain Rise", id, 1, "Rise in metres over 2 metres; 1, 2, 3 or 4");
            return entry;
        }

        private ConfigEntry<string> Remembered(string id)
        {
            ConfigEntry<string> entry;
            if (!rememberedPrefabs.TryGetValue(id, out entry))
                rememberedPrefabs[id] = entry = ValeriusCustomPrefabs.Instance.Config.Bind(
                    "Hammer Terrain Last Equipped", id, id, "Last successfully chosen prefab ID for this biome.");
            return entry;
        }

        private void CloseBuildMenu()
        {
            openBase = null;
            if (Hud.instance == null) return;
            try
            {
                var hide = AccessTools.Method(typeof(Hud), "HidePieceSelection");
                if (hide != null && hide.GetParameters().Length == 0)
                    hide.Invoke(Hud.instance, null);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning("[HammerMenu] Close: " + e.Message);
            }
        }

        private bool Choose(string id, string shape = null, int? angle = null)
        {
            if (Player.m_localPlayer == null) return false;
            string nextShape = shape ?? Shape(id).Value;
            int nextAngle = angle ?? Angle(id).Value;
            string requested = VariantId(id, nextShape, nextAngle);
            Piece target = FindPiece(requested);
            if (target == null)
            {
                Jotunn.Logger.LogWarning("[HammerMenu] Cannot equip missing terrain prefab: " + requested);
                return false;
            }

            PieceTable table = Player.m_localPlayer.GetBuildTool();
            if (table == null) return false;
            GameObject previousSlot = null;
            int slot = -1;
            for (int i = 0; i < table.m_pieces.Count; ++i)
                if (table.m_pieces[i] != null && BaseForVariant(table.m_pieces[i].name) == id)
                { slot = i; previousSlot = table.m_pieces[i]; break; }

            ReplaceBiomeSlot(id, target);
            RefreshAvailablePieces(Player.m_localPlayer);
            bool accepted = false;
            switching = true;
            try
            {
                Player.m_localPlayer.SetSelectedPiece(target);
                Piece actual = Player.m_localPlayer.GetSelectedPiece();
                accepted = actual != null && actual.gameObject != null && actual.gameObject.name == target.gameObject.name;
            }
            catch (Exception e) { Jotunn.Logger.LogWarning("[HammerMenu] Selection exception for " + requested + ": " + e); }
            finally { switching = false; }

            if (!accepted)
            {
                if (slot >= 0 && previousSlot != null)
                    ReplaceBiomeSlot(id, previousSlot.GetComponent<Piece>());
                else if (slot < 0)
                    table.m_pieces.Remove(target.gameObject);
                Jotunn.Logger.LogWarning("[HammerMenu] Valheim rejected prefab selection: " + requested);
                return false;
            }

            Shape(id).Value = nextShape;
            Angle(id).Value = nextAngle;
            Remembered(id).Value = target.gameObject.name;
            CloseBuildMenu();
            return true;
        }

        private static bool RefreshAvailablePieces(Player player)
        {
            if (player == null) return false;
            try
            {
                var method = AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList", Type.EmptyTypes);
                if (method == null)
                {
                    Jotunn.Logger.LogWarning("[HammerMenu] Player.UpdateAvailablePiecesList() not found");
                    return false;
                }
                method.Invoke(player, null);
                return true;
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning("[HammerMenu] UpdateAvailablePiecesList failed: " + e);
                return false;
            }
        }

        private static string BaseForVariant(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId)) return null;
            foreach (string known in TerrainIds)
                if (prefabId == known || prefabId.StartsWith(known + "_", StringComparison.Ordinal))
                    return known;
            return null;
        }

        private string VariantId(string id)
        {
            return VariantId(id, Shape(id).Value, Angle(id).Value);
        }

        private static string VariantId(string id, string shape, int rise)
        {
            if (shape == "Inner" || shape == "Outer") return id + "_Quarter" + shape;
            if (shape == "Slope") return id + "_Rise" + rise;
            if (shape == "SlopeInner" || shape == "SlopeOuter")
                return id + "_Rise" + rise + "_Quarter" + shape.Substring(5);
            if (shape == "Wall") return id + "_Wall";
            if (shape == "WallInner" || shape == "WallOuter")
                return id + "_Wall_" + shape.Substring(4);
            return id;
        }

        private static Piece FindPiece(string id)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return null;
            PieceTable table = player.GetBuildTool();
            if (table == null) return null;
            foreach (GameObject prefab in table.m_pieces)
                if (prefab != null && prefab.name == id) return prefab.GetComponent<Piece>();
            Piece hidden;
            if (current != null && current.hiddenPieces.TryGetValue(id, out hidden)) return hidden;
            return null;
        }

        private void ReplaceBiomeSlot(string baseId, Piece target)
        {
            Player player = Player.m_localPlayer;
            if (player == null || target == null) return;
            PieceTable table = player.GetBuildTool();
            if (table == null || table.m_pieces == null) return;
            int slot = -1;
            for (int i = 0; i < table.m_pieces.Count; ++i)
            {
                GameObject prefab = table.m_pieces[i];
                if (prefab != null && BaseForVariant(prefab.name) == baseId)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                table.m_pieces.Add(target.gameObject);
            }
            else
            {
                GameObject previous = table.m_pieces[slot];
                if (previous != target.gameObject)
                {
                    if (previous != null) hiddenPieces[previous.name] = previous.GetComponent<Piece>();
                    table.m_pieces[slot] = target.gameObject;
                }
            }
            hiddenPieces[target.gameObject.name] = target;
            visibleSlots[baseId] = target.gameObject;
        }

        [HarmonyPatch(typeof(Player), "SetSelectedPiece", new Type[] { typeof(Piece) })]
        private static class RememberedTerrainPatch
        {
            private static void Prefix(Player __instance, ref Piece __0)
            {
                if (current == null || current.switching || __instance != Player.m_localPlayer) return;
                string id;
                if (!TryGroup(__0, out id)) return;
                Piece target = FindPiece(current.Remembered(id).Value);
                if (target == null) target = FindPiece(current.VariantId(id));
                if (target != null)
                {
                    current.ReplaceBiomeSlot(id, target);
                    __0 = target;
                }
            }
        }

        private Texture2D Pixel()
        {
            if (uiPixel == null)
            {
                uiPixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                uiPixel.SetPixel(0, 0, Color.white);
                uiPixel.Apply();
            }
            return uiPixel;
        }

        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Pixel());
            GUI.color = old;
        }

        private void Outline(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        [HarmonyPatch(typeof(Player), "UpdateAvailablePiecesList")]
        [HarmonyPriority(Priority.First)]
        private static class FilterBeforeAvailabilityPatch
        {
            private static void Prefix(Player __instance)
            {
                if (current != null && __instance == Player.m_localPlayer)
                    current.FilterTerrainIcons();
            }
        }

        [HarmonyPatch(typeof(Hud), "Update")]
        [HarmonyPriority(Priority.First)]
        private static class FilterBeforeHudUpdatePatch
        {
            private static void Prefix()
            {
                if (current != null) current.FilterTerrainIcons();
            }
        }

        [HarmonyPatch(typeof(Hud), "UpdateBuild")]
        private static class FilterBeforeBuildPatch
        {
            private static void Prefix()
            {
                if (current != null) current.FilterTerrainIcons();
            }
        }

        private void FilterTerrainIcons()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            PieceTable table = player.GetBuildTool();
            if (table == null || table.m_pieces == null) return;

            var representatives = new Dictionary<string, GameObject>();
            foreach (GameObject prefab in table.m_pieces)
            {
                if (prefab == null) continue;
                string baseId = BaseForVariant(prefab.name);
                if (baseId == null) continue;
                GameObject selected;
                if (visibleSlots.TryGetValue(baseId, out selected) && selected != null)
                    representatives[baseId] = selected;
                else if (!representatives.ContainsKey(baseId) || prefab.name == baseId)
                    representatives[baseId] = prefab;
            }

            var seen = new HashSet<string>();
            for (int i = table.m_pieces.Count - 1; i >= 0; --i)
            {
                GameObject prefab = table.m_pieces[i];
                if (prefab == null || !IsKnownTerrain(prefab.name)) continue;
                string baseId = BaseForVariant(prefab.name);
                if (baseId == null)
                {
                    hiddenPieces[prefab.name] = prefab.GetComponent<Piece>();
                    table.m_pieces.RemoveAt(i);
                    continue;
                }
                if (seen.Contains(baseId))
                {
                    hiddenPieces[prefab.name] = prefab.GetComponent<Piece>();
                    table.m_pieces.RemoveAt(i);
                }
                else
                {
                    seen.Add(baseId);
                    GameObject representative = representatives[baseId];
                    if (representative != prefab)
                    {
                        hiddenPieces[prefab.name] = prefab.GetComponent<Piece>();
                        table.m_pieces[i] = representative;
                    }
                    visibleSlots[baseId] = representative;
                }
            }
        }

        private static bool TryHoveredIconRect(Vector2 mouseGui, out Rect result)
        {
            result = new Rect();
            if (Hud.instance == null) return false;
            RectTransform best = null;
            float bestArea = float.MaxValue;
            Vector3[] corners = new Vector3[4];
            foreach (RectTransform rt in Hud.instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                Rect candidate = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Screen.height - Mathf.Max(a.y, b.y),
                    Mathf.Max(a.x, b.x), Screen.height - Mathf.Min(a.y, b.y));
                if (candidate.width < 38f || candidate.width > 90f ||
                    candidate.height < 38f || candidate.height > 90f ||
                    !candidate.Contains(mouseGui)) continue;
                float area = candidate.width * candidate.height;
                if (area < bestArea) { bestArea = area; best = rt; result = candidate; }
            }
            return best != null;
        }

        private static Rect PositionPopup(Rect anchor)
        {
            const float width = 340f, height = 282f, gap = 2f;
            float x = Mathf.Clamp(anchor.xMin, 4f, Mathf.Max(4f, Screen.width - width - 4f));
            float y = anchor.yMax + gap;
            if (y + height > Screen.height - 4f)
                y = anchor.yMin - height - gap;
            return new Rect(x, Mathf.Clamp(y, 4f, Mathf.Max(4f, Screen.height - height - 4f)), width, height);
        }

        private void Update()
        {
            FilterTerrainIcons();
            if (Hud.instance == null || !Hud.IsPieceSelectionVisible() || Player.m_localPlayer == null)
            { openBase = null; return; }
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (openBase != null && popupRect.Contains(mouse)) return;
            Piece hovered = hoveredField == null ? null : hoveredField.GetValue(Hud.instance) as Piece;
            string id;
            if (TryGroup(hovered, out id))
            {
                if (openBase != id)
                {
                    openBase = id;
                    Rect actualIcon;
                    iconRect = TryHoveredIconRect(mouse, out actualIcon)
                        ? actualIcon : new Rect(mouse.x - 30f, mouse.y - 30f, 60f, 60f);
                    popupRect = PositionPopup(iconRect);
                }
            }
            else if (openBase != null)
            {
                Rect corridor = Rect.MinMaxRect(
                    Mathf.Min(iconRect.xMin, popupRect.xMin) - 3f,
                    Mathf.Min(iconRect.yMin, popupRect.yMin) - 3f,
                    Mathf.Max(iconRect.xMax, popupRect.xMax) + 3f,
                    Mathf.Max(iconRect.yMax, popupRect.yMax) + 3f);
                if (!iconRect.Contains(mouse) && !popupRect.Contains(mouse) &&
                    !corridor.Contains(mouse)) openBase = null;
            }
        }

        private static readonly int[] SlopeRises = { 1, 2, 3, 4 };

        private static Font FindHudFont()
        {
            if (Hud.instance != null)
            {
                Component[] components = Hud.instance.GetComponentsInChildren<Component>(true);
                Font candidate = null;
                foreach (Component component in components)
                {
                    if (component == null) continue;
                    Type type = component.GetType();
                    if (type.Name != "Text") continue;
                    var property = type.GetProperty("font");
                    if (property == null || !typeof(Font).IsAssignableFrom(property.PropertyType)) continue;
                    Font font = property.GetValue(component, null) as Font;
                    if (font == null) continue;
                    string path = component.gameObject.name;
                    if (path.IndexOf("piece", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0)
                        return font;
                    if (candidate == null) candidate = font;
                }
                if (candidate != null) return candidate;
            }
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (font != null && font.name.IndexOf("Averia", StringComparison.OrdinalIgnoreCase) >= 0)
                    return font;
            }
            return GUI.skin.label.font;
        }

        private void OnGUI()
        {
            if (openBase == null || Hud.instance == null || !Hud.IsPieceSelectionVisible()) return;
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
                panelStyle.normal.textColor = goldColor;
                buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
                buttonStyle.normal.textColor = Color.white;
                selectedStyle = new GUIStyle(buttonStyle);
                selectedStyle.normal.textColor = goldColor;
                Font hudFont = FindHudFont();
                if (hudFont != null)
                {
                    panelStyle.font = hudFont;
                    buttonStyle.font = hudFont;
                    selectedStyle.font = hudFont;
                }
                panelStyle.fontStyle = FontStyle.Normal;
                buttonStyle.fontStyle = FontStyle.Normal;
                selectedStyle.fontStyle = FontStyle.Normal;
                panelStyle.normal.textColor = goldColor;
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.hover.textColor = Color.white;
                buttonStyle.active.textColor = Color.white;
                selectedStyle.normal.textColor = goldColor;
                selectedStyle.hover.textColor = goldColor;
                selectedStyle.active.textColor = goldColor;
            }
            Fill(popupRect, woodColor);
            Outline(popupRect, new Color(0.55f, 0.36f, 0.10f, 1f), 2f);
            GUI.Label(new Rect(popupRect.x + 5f, popupRect.y + 4f, popupRect.width - 10f, 20f),
                "Terrain variants", panelStyle);
            const float gap = 4f;
            float colWidth = (popupRect.width - 16f - 2f * gap) / 3f;
            float originX = popupRect.x + 8f;
            float originY = popupRect.y + 30f;
            string id = openBase;
            for (int row = 0; row <= SlopeRises.Length + 1; row++)
            {
                int angle = row == 0 || row == SlopeRises.Length + 1 ? 1 : SlopeRises[row - 1];
                for (int col = 0; col < 3; col++)
                {
                    string shape = row == 0 ? (col == 0 ? "Flat" : col == 1 ? "Inner" : "Outer") :
                        row == SlopeRises.Length + 1 ? (col == 0 ? "Wall" : col == 1 ? "WallInner" : "WallOuter") :
                        (col == 0 ? "Slope" : col == 1 ? "SlopeInner" : "SlopeOuter");
                    string label = row == 0 ? (col == 0 ? "Floor" : col == 1 ? "Inner" : "Outer") :
                        row == SlopeRises.Length + 1 ? (col == 0 ? "90° Wall" : col == 1 ? "90° Inner" : "90° Outer") :
                        (angle == 1 ? "26° " : angle == 2 ? "45° " : angle == 3 ? "56° " : "63° ") + (col == 0 ? "Slope" : col == 1 ? "Inner" : "Outer");
                    Rect cell = new Rect(originX + col * (colWidth + gap), originY + row * 36f,
                        colWidth, 32f);
                    MenuCell(id, shape, angle, label, cell);
                }
            }
        }

        private void MenuCell(string id, string shape, int angle, string label, Rect rect)
        {
            string requested = VariantId(id, shape, angle);
            bool available = FindPiece(requested) != null;
            if (!available) return;
            bool selected = Shape(id).Value == shape &&
                (!shape.StartsWith("Slope", StringComparison.Ordinal) || Angle(id).Value == angle);
            Fill(rect, buttonColor);
            if (selected) Outline(rect, goldColor, 2f);
            if (GUI.Button(rect, label, selected ? selectedStyle : buttonStyle))
                Choose(id, shape, angle);
        }
    }
}

using System;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    internal static class InvisibleBarriers
    {
        internal static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;
            RegisterPiece("ValeriusInvisibleWall", "woodwall", "Invisible Boundary Wall",
                "A physical invisible boundary wall.");
            RegisterPiece("ValeriusInvisibleFloor", "wood_floor", "Invisible Boundary Floor",
                "A physical invisible boundary floor.");
            RegisterPiece("ValeriusInvisibleWall4x4", "woodwall", "Invisible Boundary Wall 4x4",
                "A physical invisible boundary wall.", new Vector3(2f, 2f, 1f));
            RegisterPiece("ValeriusInvisibleFloor4x4", "wood_floor", "Invisible Boundary Floor 4x4",
                "A physical invisible boundary floor.", new Vector3(2f, 1f, 2f));
        }

        private static void RegisterPiece(string id, string template, string name,
            string description, Vector3? scale = null)
        {
            try { AddInvisiblePiece(id, template, name, description, scale); }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create " + name + ": " + e); }
        }

        private static void AddInvisiblePiece(string prefabName, string basePrefabName, string displayName, string description, Vector3? scale = null)
        {
            var iconObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var icon = RenderManager.Instance.Render(iconObject, RenderManager.IsometricRotation);
            UnityEngine.Object.Destroy(iconObject);
            var config = new PieceConfig { Name = displayName, Description = description, PieceTable = "Hammer", Category = "Misc", Icon = icon };
            var piece = new CustomPiece(prefabName, basePrefabName, config);
            if (piece.PiecePrefab == null) throw new InvalidOperationException("Failed to create prefab '" + prefabName + "'.");
            if (scale.HasValue) piece.PiecePrefab.transform.localScale = scale.Value;
            var wearNTear = piece.PiecePrefab.GetComponent<WearNTear>();
            if (wearNTear != null) wearNTear.m_noSupportWear = true;
            piece.PiecePrefab.AddComponent<InvisiblePrefabBehaviour>();
            PieceManager.Instance.AddPiece(piece);
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
            if (__instance == null) return;
            if (!__instance.TryGetComponent<InvisiblePrefabBehaviour>(out _)) return;
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || __instance.m_health <= 0f) return;
            float halfHealth = __instance.m_health * 0.5f;
            nview.GetZDO().Set(ZDOVars.s_health, halfHealth);
            nview.InvokeRPC(ZNetView.Everybody, "RPC_HealthChanged", halfHealth);
        }
    }

}

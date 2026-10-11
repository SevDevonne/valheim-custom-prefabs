// Build to zip: dotnet build -c Release
// Build to game: dotnet build -c Debug
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
        public const string PluginVersion = "0.1.3";

        private void Awake()
        {
            Jotunn.Logger.LogInfo("Valerius Custom Prefabs is loading");

            Harmony harmony = new Harmony(PluginGUID);
            harmony.PatchAll();

            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleWall;
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleFloor;
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleWall4x4;
            PrefabManager.OnVanillaPrefabsAvailable += CreateInvisibleFloor4x4;

            PrefabManager.OnVanillaPrefabsAvailable += CreateDungeonMarker;
        }

        private void CreateInvisibleWall()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleWall;

            try
            {
                AddInvisiblePiece(
                    "ValeriusInvisibleWall",
                    "woodwall",
                    "Invisible Boundary Wall",
                    "A physical invisible boundary wall."
                );
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to create Invisible Boundary Wall: {e}");
            }
        }

        private void CreateInvisibleFloor()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleFloor;

            try
            {
                AddInvisiblePiece(
                    "ValeriusInvisibleFloor",
                    "wood_floor",
                    "Invisible Boundary Floor",
                    "A physical invisible boundary floor."
                );
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to create Invisible Boundary Floor: {e}");
            }
        }

        private void CreateInvisibleWall4x4()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleWall4x4;

            try
            {
                AddInvisiblePiece(
                    "ValeriusInvisibleWall4x4",
                    "woodwall",
                    "Invisible Boundary Wall 4x4",
                    "A physical invisible boundary wall.",
                    new Vector3(2f, 2f, 1f)
                );
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to create Invisible Boundary Wall 4x4: {e}");
            }
        }

        private void CreateInvisibleFloor4x4()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CreateInvisibleFloor4x4;

            try
            {
                AddInvisiblePiece(
                    "ValeriusInvisibleFloor4x4",
                    "wood_floor",
                    "Invisible Boundary Floor 4x4",
                    "A physical invisible boundary floor.",
                    new Vector3(2f, 1f, 2f)
                );
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to create Invisible Boundary Floor 4x4: {e}");
            }
        }

        private void AddInvisiblePiece(
            string prefabName,
            string basePrefabName,
            string displayName,
            string description,
            Vector3? scale = null)
        {
            var iconObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            var icon = RenderManager.Instance.Render(
                iconObject,
                RenderManager.IsometricRotation
            );

            Destroy(iconObject);

            var config = new PieceConfig
            {
                Name = displayName,
                Description = description,
                PieceTable = "Hammer",
                Category = "Misc",
                Icon = icon
            };

            var piece = new CustomPiece(
                prefabName,
                basePrefabName,
                config
            );

            if (piece.PiecePrefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Failed to create prefab '{prefabName}'."
                );
            }

            if (scale.HasValue)
            {
                piece.PiecePrefab.transform.localScale = scale.Value;
            }

            var wearNTear = piece.PiecePrefab.GetComponent<WearNTear>();

            if (wearNTear != null)
            {
                wearNTear.m_noSupportWear = true;
            }

            piece.PiecePrefab.AddComponent<InvisiblePrefabBehaviour>();

            PieceManager.Instance.AddPiece(piece);

            Jotunn.Logger.LogInfo(
                $"Registered piece: {displayName} ({prefabName})"
            );
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

                var piece = new CustomPiece(
                    "ValeriusDungeonMarker",
                    "guard_stone",
                    config
                );

                if (piece.PiecePrefab == null)
                    throw new System.InvalidOperationException(
                        "Failed to create Dungeon Marker.");

                // Keep guard_stone's appearance, colliders and networking.
                // Add the portal's text-entry component.
                if (piece.PiecePrefab.GetComponent<TeleportWorld>() == null)
                    piece.PiecePrefab.AddComponent<TeleportWorld>();

                // Remove any teleport trigger if present.
                foreach (var trigger in piece.PiecePrefab
                             .GetComponentsInChildren<TeleportWorldTrigger>(true))
                {
                    DestroyImmediate(trigger, true);
                }

                // The marker should not create a ward.
                var privateArea = piece.PiecePrefab.GetComponent<PrivateArea>();
                if (privateArea != null)
                    DestroyImmediate(privateArea, true);

                piece.PiecePrefab.transform.localScale = Vector3.one * 0.5f;

                PieceManager.Instance.AddPiece(piece);

                Jotunn.Logger.LogInfo(
                    "Registered piece: Dungeon Marker (ValeriusDungeonMarker)");
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to create Dungeon Marker: {e}");
            }
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
        }

        private void Update()
        {
            if (wearNTear == null || wearNTear.m_health <= 0f)
                return;

            bool visible = wearNTear.GetHealthPercentage() < 1f;

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }

    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnPlaced))]
    internal static class WearNTearOnPlacedPatch
    {
        private static void Postfix(WearNTear __instance)
        {
            if (__instance == null)
                return;

            if (!__instance.TryGetComponent<InvisiblePrefabBehaviour>(out _))
                return;

            ZNetView nview = __instance.GetComponent<ZNetView>();

            if (nview == null || !nview.IsValid())
                return;

            if (__instance.m_health <= 0f)
                return;

            float halfHealth = __instance.m_health * 0.5f;

            nview.GetZDO().Set(ZDOVars.s_health, halfHealth);

            nview.InvokeRPC(
                ZNetView.Everybody,
                "RPC_HealthChanged",
                halfHealth
            );
        }
    }

}
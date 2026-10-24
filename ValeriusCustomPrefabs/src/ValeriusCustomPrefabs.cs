using BepInEx;
using HarmonyLib;
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
        public const string PluginVersion = "0.3.0";
        internal static ValeriusCustomPrefabs Instance;

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<TerrainMenu>();
            gameObject.AddComponent<SculptTool>();
            new Harmony(PluginGUID).PatchAll();
            PrefabManager.OnVanillaPrefabsAvailable += InvisibleBarriers.Register;
            PrefabManager.OnVanillaPrefabsAvailable += DungeonMarkers.CreateDungeonMarker;
            PrefabManager.OnVanillaPrefabsAvailable += TerrainRegistry.CreateNewSurfacePieces;
            PrefabManager.OnVanillaPrefabsAvailable += SculptRegistry.CreateHoeSculpt;
        }
    }

}

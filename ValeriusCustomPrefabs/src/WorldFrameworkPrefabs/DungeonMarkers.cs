using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    internal static class DungeonMarkers
    {
        internal static void CreateDungeonMarker()
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
                foreach (var trigger in piece.PiecePrefab.GetComponentsInChildren<TeleportWorldTrigger>(true)) UnityEngine.Object.DestroyImmediate(trigger, true);
                var privateArea = piece.PiecePrefab.GetComponent<PrivateArea>();
                if (privateArea != null) UnityEngine.Object.DestroyImmediate(privateArea, true);
                piece.PiecePrefab.transform.localScale = Vector3.one * 0.5f;
                PieceManager.Instance.AddPiece(piece);
            }
            catch (Exception e) { Jotunn.Logger.LogError("Failed to create Dungeon Marker: " + e); }
        }
    }
}

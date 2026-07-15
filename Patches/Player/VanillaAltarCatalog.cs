using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Vanilla altar ownership from <c>Savepoint_menu.place_move*</c> / <c>Savepoint_on.fun_ALLreset</c>.
/// Prefer <c>_re_savepoint</c> over raw <c>_re_Scenename</c> (scene name can leak on walk);
/// coordinates are a secondary detector when the savepoint token is missing or unknown.
/// </summary>
internal static class VanillaAltarCatalog
{
    /// <summary>Match tolerance for altar transform vs fast-travel menu coords (menu may differ by a few units).</summary>
    private const float CoordMatchRadius = 28f;

    private struct AltarEntry
    {
        internal readonly string Savepoint;
        internal readonly string Scene;
        internal readonly Vector2 Position;

        internal AltarEntry(string savepoint, string scene, float x, float y)
        {
            Savepoint = savepoint;
            Scene = scene;
            Position = new Vector2(x, y);
        }
    }

    // Source: Savepoint_menu.place_move / place_move2…29 (Assembly-CSharp).
    private static readonly AltarEntry[] Altars =
    {
        new AltarEntry("savepoint", "Parishchurch", -134f, -46f),
        new AltarEntry("savepoint_village", "village_main", 197f, -36f),
        new AltarEntry("savepoint_village_zero", "village_main", 0f, -15f),
        new AltarEntry("savepoint_scape", "scapegoatEntrance", 216f, 55f),
        new AltarEntry("savepoint_scape_hill", "scapegoatEntrance", 396f, 81f),
        new AltarEntry("savepoint_Under", "UndergroundChurch", 307f, -79f),
        new AltarEntry("savepoint_InUnder", "InundergroundChurch", 2.58f, -270f),
        new AltarEntry("savepoint_InUnder_over", "InundergroundChurch", 46f, -233f),
        new AltarEntry("savepoint_InsomniaTown", "InsomniaTown", 449f, -189f),
        new AltarEntry("savepoint_Shop", "Shop", 621f, -314f),
        new AltarEntry("savepoint_InsomniaTownC", "InsomniaTownC", 774f, -262f),
        new AltarEntry("savepoint_InsomniaTownUnderRoad", "InsomniaTownUnderRoad", 803.6f, -443f),
        new AltarEntry("savepoint_InsomniaTownUnder", "InsomniaTownUnder", 936.44f, -403f),
        new AltarEntry("savepoint_Valley", "Valley", 1108.8f, -443.5f),
        new AltarEntry("savepoint_ForestOfRequiem", "ForestOfRequiem", 1245f, -275f),
        new AltarEntry("savepoint_ForestOfRequiemEXIT", "ForestOfRequiem", 1546.24f, -269f),
        new AltarEntry("savepoint_UndergroundLaboratory", "UndergroundLaboratory", 1658.89f, -362.81f),
        new AltarEntry("savepoint_PilgrimageEntrance", "PilgrimageEntrance", 1612.17f, -213.77f),
        new AltarEntry("savepoint_PilgrimageEntrance_over", "PilgrimageEntrance", 1416.8f, -189f),
        new AltarEntry("savepoint_WhiteCathedral", "WhiteCathedral", 1049.93f, -227.5f),
        new AltarEntry("savepoint_WhiteCathedralGarden", "WhiteCathedralGarden", 1049.93f, -227.5f),
        new AltarEntry("savepoint_WhiteCathedralRooftop", "WhiteCathedralRooftop", 980.32f, -594f),
        new AltarEntry("savepoint_prison", "Prison", -38.46f, -185.94f),
        new AltarEntry("savepoint_First", "FirstMap", -139f, -135.92f),
        new AltarEntry("savepoint_First2", "FirstMap", -41.57f, -121.17f),
        new AltarEntry("savepoint_Ranch", "Ranch", -15.9f, -237.23f),
        new AltarEntry("savepoint_Ranchover", "Ranch", 37.35f, -161.98f),
        new AltarEntry("savepoint_Lake", "Lake", -85.23f, -102.36f),
        new AltarEntry("savepoint_Last", "LastBoss", -106.92f, -132.11f),
    };

    private static readonly Dictionary<string, string> SavepointToScene =
        BuildSavepointMap();

    private static readonly HashSet<string> ScenesWithAltars =
        BuildSceneSet();

    private static Dictionary<string, string> BuildSavepointMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Altars.Length; i++)
            map[Altars[i].Savepoint] = Altars[i].Scene;
        return map;
    }

    private static HashSet<string> BuildSceneSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Altars.Length; i++)
            set.Add(Altars[i].Scene);
        return set;
    }

    internal static bool TryGetSceneForSavepoint(string savepoint, out string scene)
    {
        scene = null;
        if (string.IsNullOrEmpty(savepoint))
            return false;
        return SavepointToScene.TryGetValue(savepoint, out scene);
    }

    internal static bool SceneHasVanillaAltar(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        return ScenesWithAltars.Contains(sceneName);
    }

    /// <summary>
    /// Nearest catalog altar within <see cref="CoordMatchRadius"/>, or null if none.
    /// Prefer savepoint lookup when possible — some altars share near-identical menu coords.
    /// </summary>
    internal static bool TryGetSceneForCheckpointCoords(Vector2 checkpoint, out string scene, out string savepoint)
    {
        scene = null;
        savepoint = null;
        if (checkpoint == Vector2.zero)
            return false;

        float bestDist = CoordMatchRadius;
        int bestIndex = -1;
        for (int i = 0; i < Altars.Length; i++)
        {
            float d = Vector2.Distance(checkpoint, Altars[i].Position);
            if (d <= bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        scene = Altars[bestIndex].Scene;
        savepoint = Altars[bestIndex].Savepoint;
        return true;
    }

    /// <summary>
    /// True altar home for respawn: savepoint token → coords → claimed scene (if it hosts an altar).
    /// </summary>
    internal static string ResolveAltarHomeScene(game_fragmng frag)
    {
        if (frag == null)
            return null;

        if (TryGetSceneForSavepoint(frag._re_savepoint, out string fromSavepoint))
            return fromSavepoint;

        if (TryGetSceneForCheckpointCoords(frag._checkpoint, out string fromCoords, out _))
            return fromCoords;

        string claimed = frag._re_Scenename;
        if (SceneHasVanillaAltar(claimed))
            return claimed;

        return claimed;
    }

    /// <summary>
    /// Active gameplay zone differs from the altar that owns the stored checkpoint.
    /// </summary>
    internal static bool IsActiveZoneAwayFromAltarHome(string activeZone, game_fragmng frag)
    {
        if (string.IsNullOrEmpty(activeZone) || frag == null)
            return false;

        string home = ResolveAltarHomeScene(frag);
        if (string.IsNullOrEmpty(home))
            return false;

        return !activeZone.Equals(home, StringComparison.OrdinalIgnoreCase);
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.Compatibility.DeepForest;

/// <summary>
/// DeepForest copies vanilla UndergroundChurch altars and leaves GameObject name
/// <c>savepoint_Under</c> on every custom map. HellGate's void-respawn catalog then
/// routes death to <c>UndergroundChurch</c> with DeepForest coordinates → void fall.
///
/// This module renames those altar objects to unique tokens, fixes
/// <c>Savepoint_custom</c> fast-travel writes when present, and remaps unknown
/// <c>game_fragmng.aa</c> Invoke targets to a generic place-at-checkpoint method.
/// </summary>
internal static class DeepForestAltarIdFix
{
    internal const string StolenVanillaToken = "savepoint_Under";

    internal const string TokenWoodsHouse = "savepoint_WoodsHouse";
    internal const string TokenDeepForest = "savepoint_DeepForest";
    internal const string TokenCastleDung2 = "savepoint_CastleDung2";
    internal const string TokenFortLow = "savepoint_FortLow";

    private static readonly Dictionary<string, string> SceneToToken =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "WoodsHouse", TokenWoodsHouse },
            { "Forest1", TokenDeepForest },
            { "CastleDung2", TokenCastleDung2 },
            { "FortLow", TokenFortLow },
        };

    private static readonly Dictionary<string, string> CustomMoveToToken =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Custom_place_move0", TokenWoodsHouse },
            { "Custom_place_move1", TokenDeepForest },
            { "Custom_place_move2", TokenCastleDung2 },
            { "Custom_place_move3", TokenFortLow },
        };

    private static bool _initialized;
    private static AccessTools.FieldRef<Savepoint_on, string> _mainSceneNameRef;

    internal static bool IsDeepForestScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && SceneToToken.ContainsKey(sceneName);
    }

    internal static bool TryGetTokenForScene(string sceneName, out string token)
    {
        token = null;
        if (string.IsNullOrEmpty(sceneName))
            return false;
        return SceneToToken.TryGetValue(sceneName, out token);
    }

    internal static void Initialize(Harmony harmony)
    {
        if (_initialized || harmony == null)
            return;

        _initialized = true;
        try
        {
            _mainSceneNameRef = AccessTools.FieldRefAccess<Savepoint_on, string>("main_Scenename");
        }
        catch
        {
            _mainSceneNameRef = null;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        harmony.Patch(
            AccessTools.Method(typeof(Savepoint_on), "fun_ALLreset"),
            prefix: new HarmonyMethod(typeof(DeepForestAltarIdFix), nameof(SavepointOnFunAllResetPrefix)));

        harmony.Patch(
            AccessTools.Method(typeof(game_fragmng), "aa"),
            prefix: new HarmonyMethod(typeof(DeepForestAltarIdFix), nameof(GameFragAaPrefix)));

        TryPatchSavepointCustom(harmony);

        Plugin.Log?.LogInfo("[DeepForest] Altar ID fix armed (rename stolen savepoint_Under tokens).");
    }

    private static void TryPatchSavepointCustom(Harmony harmony)
    {
        Type customType = AccessTools.TypeByName("Savepoint_custom");
        if (customType == null)
        {
            Plugin.Log?.LogInfo("[DeepForest] Savepoint_custom not in Assembly-CSharp — rename-on-altar only.");
            return;
        }

        foreach (KeyValuePair<string, string> pair in CustomMoveToToken)
        {
            MethodInfo method = AccessTools.Method(customType, pair.Key);
            if (method == null)
                continue;

            harmony.Patch(
                method,
                postfix: new HarmonyMethod(typeof(DeepForestAltarIdFix), nameof(SavepointCustomPlaceMovePostfix)));
        }

        Plugin.Log?.LogInfo("[DeepForest] Patched Savepoint_custom fast-travel place_move writers.");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsDeepForestScene(scene.name))
            return;

        try
        {
            RenameStolenAltarsInScene(scene);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[DeepForest] Scene rename failed ({scene.name}): {ex.Message}");
        }
    }

    private static void RenameStolenAltarsInScene(Scene scene)
    {
        if (!TryGetTokenForScene(scene.name, out string token))
            return;

        Savepoint_on[] altars = UnityEngine.Object.FindObjectsOfType<Savepoint_on>();
        int renamed = 0;
        for (int i = 0; i < altars.Length; i++)
        {
            Savepoint_on altar = altars[i];
            if (altar == null)
                continue;

            Scene altarScene = altar.gameObject.scene;
            if (!altarScene.IsValid() || !altarScene.name.Equals(scene.name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryRenameAltar(altar, token))
                renamed++;
        }

        if (renamed > 0)
            Plugin.Log?.LogInfo($"[DeepForest] Renamed {renamed} altar(s) in \"{scene.name}\" → \"{token}\".");
    }

    private static bool SavepointOnFunAllResetPrefix(Savepoint_on __instance)
    {
        try
        {
            EnsureAltarRenamed(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[DeepForest] fun_ALLreset rename failed: {ex.Message}");
        }

        return true;
    }

    private static void EnsureAltarRenamed(Savepoint_on altar)
    {
        if (altar == null)
            return;

        string sceneHint = null;
        try
        {
            if (_mainSceneNameRef != null)
                sceneHint = _mainSceneNameRef(altar);
        }
        catch
        {
            sceneHint = null;
        }

        // Prefer the scene the altar actually lives in — main_Scenename on copied prefabs
        // can point at a neighbor map (DeepForest door/altar mixups).
        Scene sc = altar.gameObject.scene;
        if (sc.IsValid() && IsDeepForestScene(sc.name))
            sceneHint = sc.name;

        if (!TryGetTokenForScene(sceneHint, out string token))
            return;

        TryRenameAltar(altar, token);
    }

    private static bool TryRenameAltar(Savepoint_on altar, string token)
    {
        if (altar == null || string.IsNullOrEmpty(token))
            return false;

        if (!StolenVanillaToken.Equals(altar.gameObject.name, StringComparison.OrdinalIgnoreCase))
            return false;

        altar.gameObject.name = token;
        return true;
    }

    /// <summary>
    /// After DeepForest fast-travel writes stolen <c>savepoint_Under</c>, overwrite with unique token.
    /// </summary>
    private static void SavepointCustomPlaceMovePostfix(object __instance, MethodBase __originalMethod)
    {
        try
        {
            if (__instance == null || __originalMethod == null)
                return;

            if (!CustomMoveToToken.TryGetValue(__originalMethod.Name, out string token))
                return;

            FieldInfo flagField = AccessTools.Field(__instance.GetType(), "FlagMng");
            game_fragmng frag = flagField?.GetValue(__instance) as game_fragmng;
            if (frag == null)
                return;

            frag._re_savepoint = token;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[DeepForest] Savepoint_custom postfix failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Vanilla <c>aa</c> Invokes a private method named like the savepoint token.
    /// Custom tokens have no method — remap to <c>savepoint_Under</c> (place at <c>_checkpoint</c>).
    /// </summary>
    private static void GameFragAaPrefix(ref string a)
    {
        if (string.IsNullOrEmpty(a))
            return;

        if (a.Equals(TokenWoodsHouse, StringComparison.OrdinalIgnoreCase)
            || a.Equals(TokenDeepForest, StringComparison.OrdinalIgnoreCase)
            || a.Equals(TokenCastleDung2, StringComparison.OrdinalIgnoreCase)
            || a.Equals(TokenFortLow, StringComparison.OrdinalIgnoreCase))
        {
            a = StolenVanillaToken;
        }
    }
}

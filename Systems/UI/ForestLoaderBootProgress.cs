using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Tracks NoRSceneLoader's boot-time additive scene loads (enemy prefab harvest)
/// so Early Boot Loading can show "Forest" progress instead of a silent black wait.
/// </summary>
internal static class ForestLoaderBootProgress
{
    private static readonly string[] TrackedScenes =
    {
        "FirstMap",
        "village_main",
        "UndergroundChurch",
        "InundergroundChurch",
        "Prison",
        "InsomniaTownC",
        "UnderCemetery",
        "Ranch",
        "Valley",
        "UndergroundLaboratory",
        "WhiteCathedralRooftop"
    };

    private static readonly HashSet<string> TrackedSet = new HashSet<string>(TrackedScenes, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SeenScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static bool _hooked;
    private static bool _finished;
    private static string _currentScene = "";
    private static float _startedAt = -1f;

    internal static int TotalScenes => TrackedScenes.Length;
    internal static int CompletedScenes => SeenScenes.Count;
    internal static bool IsFinished => _finished;
    internal static string CurrentScene => _currentScene;

    internal static float Progress01
    {
        get
        {
            if (_finished)
                return 1f;
            if (TotalScenes <= 0)
                return 0f;
            return Mathf.Clamp01(CompletedScenes / (float)TotalScenes);
        }
    }

    internal static string StatusText
    {
        get
        {
            if (_finished)
                return "Forest content ready";
            if (CompletedScenes <= 0 && string.IsNullOrEmpty(_currentScene))
                return "Loading Forest content...";
            if (!string.IsNullOrEmpty(_currentScene))
                return $"Forest {CompletedScenes}/{TotalScenes}  {_currentScene}";
            return $"Forest {CompletedScenes}/{TotalScenes}";
        }
    }

    internal static void EnsureHooked()
    {
        if (_hooked)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        _hooked = true;
        _startedAt = Time.realtimeSinceStartup;
        Plugin.Log?.LogInfo($"[BOOT] Tracking NoRSceneLoader Forest scenes ({TotalScenes}).");
    }

    internal static void TickCompletion()
    {
        if (_finished)
            return;

        if (_startedAt < 0f)
            _startedAt = Time.realtimeSinceStartup;

        // All known Forest harvest scenes seen.
        if (CompletedScenes >= TotalScenes)
        {
            MarkFinished();
            return;
        }

        // NoRSceneLoader may skip a scene; if we saw at least one and then went idle, finish.
        if (CompletedScenes > 0)
        {
            // Idle detection is driven by BootSequence calling Tick while updating UI.
            // Completion without full count uses a short grace after last scene — handled in WaitRoutine.
        }
    }

    internal static void MarkFinished()
    {
        if (_finished)
            return;
        _finished = true;
        _currentScene = "";
        Plugin.Log?.LogInfo($"[BOOT] NoRSceneLoader Forest progress complete ({CompletedScenes}/{TotalScenes}).");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_finished)
            return;

        if (string.IsNullOrEmpty(scene.name) || !TrackedSet.Contains(scene.name))
            return;

        if (!SeenScenes.Add(scene.name))
            return;

        _currentScene = scene.name;
        Plugin.Log?.LogInfo($"[BOOT] Forest scene {CompletedScenes}/{TotalScenes}: {scene.name}");

        if (HellGateSplashScreen.IsEarlyBootActive)
        {
            HellGateSplashScreen.PushEarlyBootItem(scene.name);
            HellGateSplashScreen.SetEarlyBootProgressOnly(0.05f + Progress01 * 0.28f);
        }

        if (CompletedScenes >= TotalScenes)
            MarkFinished();
    }
}

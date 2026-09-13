using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Hides vanilla + HellGate overlay HUDs while F11 spawn authoring is active.
/// Keeps the top Spawn System Editor V2.0 banner; authoring itself is IMGUI.
/// </summary>
internal static class SpawnAuthoringUiSuppressor
{
    private static readonly List<Canvas> Stored = new List<Canvas>(32);
    private static readonly List<bool> StoredEnabled = new List<bool>(32);
    private static bool active;

    internal static void Begin()
    {
        End();

        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;
            if (ShouldKeepVisible(canvas))
                continue;

            Stored.Add(canvas);
            StoredEnabled.Add(canvas.enabled);
            canvas.enabled = false;
        }

        active = Stored.Count > 0;
        if (active)
            Plugin.Log?.LogInfo($"[SPAWN AUTHORING] Hid {Stored.Count} UI canvas(es).");
    }

    internal static void End()
    {
        for (int i = 0; i < Stored.Count; i++)
        {
            Canvas canvas = Stored[i];
            if (canvas != null)
                canvas.enabled = StoredEnabled[i];
        }

        Stored.Clear();
        StoredEnabled.Clear();
        active = false;
    }

    /// <summary>Re-scan after hot-reload / scene props recreate HUDs mid-session.</summary>
    internal static void RefreshIfActive()
    {
        if (!global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            return;
        if (SpawnAuthoringConfig.UiEnable != null && !SpawnAuthoringConfig.UiEnable.Value)
            return;

        Begin();
    }

    private static bool ShouldKeepVisible(Canvas canvas)
    {
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
            canvas.renderMode != RenderMode.ScreenSpaceCamera)
            return true; // world-space VFX / diegetic — leave alone

        string name = canvas.gameObject != null ? canvas.gameObject.name : string.Empty;
        if (string.IsNullOrEmpty(name))
            return false;

        // Top F11 "Spawn System Editor V2.0" banner
        if (name.IndexOf("SpawnRecorderIndicator", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Authoring host (IMGUI; keep if a canvas is ever attached)
        if (name.IndexOf("SpawnAuthoring", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Boot / title / splash must never be touched if somehow present
        if (name.IndexOf("Splash", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (name.IndexOf("TitleMenu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (name.IndexOf("EarlyBoot", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (name.IndexOf("LanguageSelection", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}

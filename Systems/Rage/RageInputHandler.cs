using UnityEngine;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Handles Rage and Time Slow-Mo key input via Input.GetKeyDown.
/// Keys come from <see cref="Plugin.rageActivationHotkey"/> / <see cref="Plugin.timeSlowMoHotkey"/> (defaults G / T).
/// During H-scene / active QTE, the Rage key is left for <see cref="QTESystem"/> escape activation.
/// </summary>
internal class RageInputHandler : MonoBehaviour
{
    private static RageInputHandler _instance;
    private float _lastRagePressTime = 0f;
    private float _lastSlowMoPressTime = 0f;
    private static float KeyPressCooldown => Plugin.rageKeyPressCooldown?.Value ?? 0.2f;

    public static void EnsureCreated()
    {
        if (_instance != null) return;

        GameObject obj = new GameObject("RageInputHandler");
        _instance = obj.AddComponent<RageInputHandler>();
        DontDestroyOnLoad(obj);
        Plugin.Log?.LogInfo("[RageInputHandler] Created successfully");
    }

    private void Update()
    {
        if (!RageSystem.Enabled) return;

        float currentTime = Time.time;
        KeyCode rageKey = Plugin.rageActivationHotkey?.Value ?? KeyCode.G;
        KeyCode slowMoKey = Plugin.timeSlowMoHotkey?.Value ?? KeyCode.T;

        if (rageKey != KeyCode.None
            && Input.GetKeyDown(rageKey)
            && (currentTime - _lastRagePressTime) > KeyPressCooldown)
        {
            _lastRagePressTime = currentTime;
            // Do not Toggle here in H/QTE — that can spend Rage before QTE escape runs.
            if (!RageSystem.ShouldDeferManualActivationToQte())
                RageSystem.Toggle();
        }

        if (slowMoKey != KeyCode.None
            && Input.GetKeyDown(slowMoKey)
            && (currentTime - _lastSlowMoPressTime) > KeyPressCooldown)
        {
            _lastSlowMoPressTime = currentTime;
            // F11 authoring owns T (Trap catalog). Do not start Rage slow-mo while editing.
            if (global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
                return;
            TimeSlowMoSystem.Toggle();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

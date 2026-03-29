using UnityEngine;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Handles G (Rage) and T (Time Slow-Mo) key input via Input.GetKeyDown.
/// </summary>
internal class RageInputHandler : MonoBehaviour
{
    private static RageInputHandler _instance;
    private float _lastGPressTime = 0f;
    private float _lastTPressTime = 0f;
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

        if (Input.GetKeyDown(KeyCode.G) && (currentTime - _lastGPressTime) > KeyPressCooldown)
        {
            _lastGPressTime = currentTime;
            RageSystem.Toggle();
        }

        if (Input.GetKeyDown(KeyCode.T) && (currentTime - _lastTPressTime) > KeyPressCooldown)
        {
            _lastTPressTime = currentTime;
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

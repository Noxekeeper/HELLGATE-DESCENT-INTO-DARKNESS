using System;
using System.Collections;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Time Slow-Mo system (T key)
/// Drains Rage per second, adds MindBroken per second
/// Effect ends on rage depletion or repeated key press
/// </summary>
internal static class TimeSlowMoSystem
{
    private static bool IsEnabled => Plugin.enableRageMode?.Value ?? false;

    private static bool _isActive = false;
    private static float _slowMoTimeScale => Plugin.timeSlowMoTimeScale?.Value ?? 0.5f; // 50% slowdown by default
    private static float _rageDrainPerSecond => (Plugin.timeSlowMoRageDrainPerSecond?.Value ?? 5.0f) * (Plugin.rageSlowMoDrainMultiplier?.Value ?? 2.0f);
    private static float? _originalTimeScale = null;
    private static Coroutine? _activeCoroutine;
    
    // HC effect constant in Slow-Mo (multiplied by config)
    private static float HC_GAIN_PER_SECOND => 0.005f * (Plugin.rageSlowMoMBGainMultiplier?.Value ?? 2.0f); // Base 0.5%/sec * multiplier

    // Events
    internal static event Action? OnActivated;
    internal static event Action? OnDeactivated;

    internal static bool Enabled => IsEnabled;
    internal static bool IsActive => _isActive;
    internal static float RageDrainPerSecond => _rageDrainPerSecond;

    /// <summary>
    /// Performance optimization: Using UnifiedPlayerCacheManager
    /// </summary>
    private static bool IsPlayerInHScene()
    {
        var playerCon = UnifiedPlayerCacheManager.GetPlayer();
        if (playerCon == null) return false;
        return playerCon.erodown != 0 && playerCon.eroflag;
    }

    /// <summary>
    /// Attempts to toggle slow-mo (T key)
    /// </summary>
    internal static bool Toggle()
    {
        if (!Enabled)
        {
            return false;
        }

        if (_isActive)
        {
            Deactivate();
            return true;
        }
        else
        {
            if (IsPlayerInHScene()) return false;
            // Activate if there's at least some rage
            if (RageSystem.Percent > 0f)
            {
                return Activate();
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Activates slow-mo
    /// </summary>
    private static bool Activate()
    {
        if (_isActive) return false;
        if (RageSystem.Percent <= 0f) return false;

        _isActive = true;
        _originalTimeScale = Time.timeScale;

        // Set slow-mo timeScale
        float targetTimeScale = _slowMoTimeScale;
        Time.timeScale = targetTimeScale;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Plugin.Log?.LogInfo($"[TimeSlowMo] Activated in scene '{sceneName}'. Setting timeScale to {targetTimeScale} (was {_originalTimeScale})");

        // Start coroutine for rage drain
        if (_activeCoroutine != null)
        {
            var mono = GetMonoBehaviour();
            if (mono != null)
            {
                mono.StopCoroutine(_activeCoroutine);
            }
        }

        var mb = GetMonoBehaviour();
        if (mb != null)
        {
            _activeCoroutine = mb.StartCoroutine(SlowMoDrainCoroutine());
        }

        try
        {
            OnActivated?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[TimeSlowMo] Error during OnActivated dispatch: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// Deactivates slow-mo
    /// </summary>
    private static void Deactivate()
    {
        if (!_isActive) return;

        _isActive = false;
        Time.timeScale = 1f;
        _originalTimeScale = null;

        // Stop coroutine
        if (_activeCoroutine != null)
        {
            var mono = GetMonoBehaviour();
            if (mono != null)
            {
                mono.StopCoroutine(_activeCoroutine);
            }
            _activeCoroutine = null;
        }

        try
        {
            OnDeactivated?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[TimeSlowMo] Error during OnDeactivated dispatch: {ex.Message}");
        }
    }

    /// <summary>
    /// Coroutine for rage drain per second
    /// </summary>
    private static IEnumerator SlowMoDrainCoroutine()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(0.1f); // Check every 0.1 sec

            if (!_isActive) break;

            // Drain rage
            float drainAmount = _rageDrainPerSecond * 0.1f; // For 0.1 seconds
            float before = RageSystem.Percent;
            float after = Mathf.Max(0f, before - drainAmount);

            if (after <= 0f)
            {
                // Rage depleted - deactivate
                RageSystem.AddRage(-before, "slowmo_drain_zero");
                Deactivate();
                break;
            }
            else
            {
                // Drain rage (uses config multiplier)
                RageSystem.AddRage(-drainAmount, "slowmo_drain");
                
                // Add MindBroken during Slow-Mo (uses config multiplier)
                if (MindBrokenSystem.Enabled)
                {
                    float hcGain = HC_GAIN_PER_SECOND * 0.1f; // For 0.1 seconds
                    MindBrokenSystem.AddPercent(hcGain, "slowmo_active");
                }
            }
        }
    }

    /// <summary>
    /// System update (checks rage and maintains slow-mo).
    /// timeScale must be applied every frame because other systems can override it.
    /// </summary>
    internal static void Update()
    {
        if (!Enabled) return;
        if (!_isActive) return;

        // If rage depleted - deactivate
        if (RageSystem.Percent <= 0f)
        {
            Deactivate();
            return;
        }

        // Maintain timeScale while active
        if (Time.timeScale != _slowMoTimeScale)
        {
            Time.timeScale = _slowMoTimeScale;
        }
    }

    /// <summary>
    /// Reset state
    /// </summary>
    internal static void Reset()
    {
        if (_isActive)
        {
            Deactivate();
        }
    }

    private static MonoBehaviour? GetMonoBehaviour()
    {
        // Use cached RageSystemMono (avoids FindObjectOfType)
        var cached = RageSystem.GetCachedMono();
        if (cached != null) return cached;

        RageSystem.Initialize(); // Ensures mono exists
        return RageSystem.GetCachedMono();
    }

}


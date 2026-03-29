using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage.Patches;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Centralized manager for the Rage Mode mechanic and its hooks.
/// Counter-mechanic to MindBroken - accumulates from enemy kills.
/// </summary>
internal static class RageSystem
{
    internal enum RageTier
    {
        None = 0,
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3,
    }

    private static bool IsEnabled => Plugin.enableRageMode?.Value ?? false;

    private static float _ragePercent = 0f;
    private static bool _isActive = false;
    
    // Activation time for auto-deactivation after duration
    private static float _activationTime = 0f;
    
    // Last kill time for timeout tracking
    private static float _lastKillTime = 0f;
    
    // Tier of current activation window
    private static RageTier _currentTier = RageTier.None;
    private static float _tier3ReadyUntilTime = 0f;

    // Events
    internal static event Action? OnChanged;
    internal static event Action<float, float>? OnPercentChanged; // oldPercent, newPercent
    internal static event Action? OnActivated;
    internal static event Action? OnDeactivated;

    internal static bool Enabled => IsEnabled;
    internal static float Percent => Enabled ? _ragePercent : 0f;
    internal static bool IsActive => _isActive;
    internal static bool IsOutburstFury => _isActive && _currentTier == RageTier.Tier3;
    internal static RageTier CurrentTier => _isActive ? _currentTier : RageTier.None;
    internal static bool IsTier3Ready => !_isActive && IsTier3ReadyInternal();

    // Config values
    private static float GainPerKill => Plugin.rageGainPerKill?.Value ?? 5f;
    private static float GainPerBossKill => Plugin.rageGainPerBossKill?.Value ?? 12.0f;
    
    // Config-based constants
    private static float TIER1_THRESHOLD => Plugin.rageTier1Threshold?.Value ?? 30f;
    private static float TIER2_THRESHOLD => Plugin.rageTier2Threshold?.Value ?? 60f;
    private static float TIER3_OVERFLOW_THRESHOLD => Plugin.rageTier3OverflowThreshold?.Value ?? 103f;
    private static float TIER1_DURATION => Plugin.rageTier1Duration?.Value ?? 5f;
    private static float TIER2_DURATION => Plugin.rageTier2Duration?.Value ?? 10f;
    private static float TIER3_DURATION => Plugin.rageTier3Duration?.Value ?? 15f;
    private static float AUTO_DEACTIVATE_DURATION => GetDurationForTier(_currentTier);
    private const float TIER3_UI_READY_START_PERCENT = 100f;
    private const float TIER3_READY_GRACE_SECONDS = 1.25f;
    
    // Kill timeout constants
    private static float KILL_TIMEOUT_THRESHOLD => Plugin.rageKillTimeoutSeconds?.Value ?? 5f;
    private const float HC_OVERDRIVE_GAIN_PER_SECOND = 0.02f; // +2% HC/sec (extra over base)
    
    // Base MindBroken gain during active Rage (0.5% per sec by default).
    // Value is expressed as percent-per-second in config, converted to 0..1 fraction here.
    private static float HC_BASE_GAIN_PER_SECOND => (Plugin.rageBaseMindBrokenGainPerSecondPercent?.Value ?? 0.5f) / 100f;
    
    // Tier3 drain constants (legacy outburst behavior)
    private static float OUTBURST_FURY_DRAIN_PER_SECOND => Plugin.rageOutburstFuryDrainPerSecond?.Value ?? 10f;

    internal static string GetDisplayText()
    {
        if (_isActive && _currentTier != RageTier.None)
        {
            float timeRemaining = AUTO_DEACTIVATE_DURATION - (Time.unscaledTime - _activationTime);
            if (timeRemaining < 0f) timeRemaining = 0f;
            
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            string tierLabel = _currentTier switch
            {
                RageTier.Tier1 => "I",
                RageTier.Tier2 => "II",
                RageTier.Tier3 => "III",
                _ => ""
            };
            return $"RAGE {tierLabel} - {minutes}:{seconds:D2}";
        }
        else
        {
            int shownPercent = Mathf.RoundToInt(Mathf.Clamp(_ragePercent, 0f, 100f));
            if (!_isActive && IsTier3Ready)
                return $"Rage {shownPercent}%";
            return $"Rage {shownPercent}%";
        }
    }

    private static bool IsInHScene()
    {
        var playerconInstance = UnifiedPlayerCacheManager.GetPlayer();
        return playerconInstance != null && playerconInstance.eroflag;
    }

    private static RageTier GetAvailableTierForPercent(float ragePercent, bool isInHScene)
    {
        if (ragePercent >= TIER3_OVERFLOW_THRESHOLD || (IsTier3ReadyInternal() && ragePercent >= TIER3_UI_READY_START_PERCENT))
            return RageTier.Tier3;
        if (ragePercent >= TIER2_THRESHOLD)
            return RageTier.Tier2;
        if (!isInHScene && ragePercent >= TIER1_THRESHOLD)
            return RageTier.Tier1;
        return RageTier.None;
    }

    private static bool IsTier3ReadyInternal()
    {
        if (_ragePercent > TIER3_UI_READY_START_PERCENT)
            return true;
        return Time.unscaledTime <= _tier3ReadyUntilTime;
    }

    private static float GetDurationForTier(RageTier tier)
    {
        return tier switch
        {
            RageTier.Tier1 => TIER1_DURATION,
            RageTier.Tier2 => TIER2_DURATION,
            RageTier.Tier3 => TIER3_DURATION,
            _ => 0f,
        };
    }

    private static float GetActivationCostForTier(RageTier tier)
    {
        return tier switch
        {
            RageTier.Tier1 => 30f,
            RageTier.Tier2 => 60f,
            RageTier.Tier3 => 100f,
            _ => 0f,
        };
    }
    
    /// <summary>
    /// Initializes the system - creates MonoBehaviour for Update()
    /// </summary>
    internal static void Initialize()
    {
        EnsureMonoBehaviour();
        RageComboUISystem.Initialize();
        RageVisualEffectsSystem.Initialize();
    }

    /// <summary>
    /// Registers enemy kill and adds Rage
    /// </summary>
    internal static void RegisterKill(string enemyName, bool isBoss)
    {
        if (!Enabled)
            return;

        float gain = isBoss ? GainPerBossKill : GainPerKill;

        // Reset timeout on kill
        _lastKillTime = Time.unscaledTime;

        AddRage(gain, isBoss ? $"boss_kill_{enemyName}" : $"kill_{enemyName}");
    }

    /// <summary>
    /// Resets last kill timer (for 5 second timeout refresh)
    /// Used on grab/down to restart timeout countdown
    /// </summary>
    internal static void ResetKillTimeout(string reason = "reset_timeout")
    {
        if (!Enabled) return;
        if (!_isActive) return; // Only if Rage is active
        
        _lastKillTime = Time.unscaledTime;
    }

    /// <summary>
    /// Attempts to activate Rage Mode (G key) - activation only, manual deactivation disabled
    /// Activation disabled during grab or knockdown
    /// </summary>
    internal static bool Toggle()
    {
        if (!Enabled)
            return false;

        // Manual deactivation disabled
        if (_isActive)
            return false;
        else
        {
            var playerconInstance = UnifiedPlayerCacheManager.GetPlayer();
            if (playerconInstance != null && playerconInstance.erodown != 0)
                return false;

            bool inHScene = IsInHScene();
            RageTier targetTier = GetAvailableTierForPercent(_ragePercent, inHScene);
            if (targetTier == RageTier.None)
                return false;

            float activationCost = GetActivationCostForTier(targetTier);
            if (_ragePercent < activationCost)
                return false;
            return Activate(targetTier, activationCost);
        }
    }

    /// <summary>
    /// QTE/H-scene: activate Rage without normal activation cost, then deduct QTE-specific rage cost.
    /// </summary>
    internal static bool TryActivateForQteEscape(float qteCostPercent)
    {
        _ = qteCostPercent; // Legacy parameter kept for compatibility with old call sites/config.
        if (!Enabled || _isActive)
            return false;
        // QTE escape requires at least Tier2 threshold in H-scene.
        if (_ragePercent < TIER2_THRESHOLD)
            return false;
        RageTier tierForEscape = (_ragePercent >= TIER3_OVERFLOW_THRESHOLD || (_ragePercent >= TIER3_UI_READY_START_PERCENT && IsTier3ReadyInternal()))
            ? RageTier.Tier3
            : RageTier.Tier2;
        float tierCost = GetActivationCostForTier(tierForEscape);
        if (_ragePercent < tierCost)
            return false;
        if (!Activate(tierForEscape, tierCost))
            return false;
        return true;
    }

    /// <summary>
    /// Activates Rage Mode
    /// </summary>
    /// <param name="activationCost">Cost to deduct before activation (0 if no spend)</param>
    private static bool Activate(RageTier tier, float activationCost = 0f)
    {
        if (_isActive)
            return false;
        if (tier == RageTier.None)
            return false;
        
        // For manual activation, deduct cost
        if (activationCost > 0f)
        {
            // Check if we have enough Rage for cost
            if (_ragePercent < activationCost)
            {
                Plugin.Log?.LogError($"[Rage] Activate failed: not enough Rage! current={_ragePercent:F1}%, required={activationCost}");
                return false;
            }
            
            // Deduct activation cost BEFORE setting active
            // Cost pre-check was done by caller.
            AddRage(-activationCost, "rage_activation_cost");
        }
        else if (_ragePercent <= 0f)
        {
            return false;
        }

        // Rage activation logging removed for cleaner logs

        _isActive = true;
        _currentTier = tier;
        _tier3ReadyUntilTime = 0f;

        // Save activation time for auto-deactivation (use unscaledTime)
        _activationTime = Time.unscaledTime;
        
        // Initialize last kill time (reset timeout on activation)
        _lastKillTime = Time.unscaledTime;
        
        // Ensure RageSystemMono exists for Update()
        EnsureMonoBehaviour();

        // Edge bars and flash remain disabled for performance.

        // Invoke events (don't block activation on errors)
        try
        {
            OnActivated?.Invoke();
            OnChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Rage] Error during OnActivated dispatch (activation continues): {ex.Message}");
        }

        // Update UI
        try
        {
            RageUISystem.RefreshLabel();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Rage] Error refreshing UI (activation continues): {ex.Message}");
        }

        // Camera shake on activation
        if (Plugin.rageActivationCameraShake?.Value ?? true)
        {
            try
            {
                // Performance optimization: Using UnifiedPlayerCacheManager
                var pc = UnifiedPlayerCacheManager.GetPlayer();
                if (pc != null)
                    pc.shake_fun("STAB");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Rage] Error camera shake: {ex.Message}");
            }
        }

        // Pushback removed for performance and by user request

        return true;
    }
    
    /// <summary>
    /// Deactivates Rage Mode - resets activation time
    /// </summary>
    private static void Deactivate()
    {
        if (!_isActive) return;

        _isActive = false;
        _currentTier = RageTier.None;
        // Reset activation time
        _activationTime = 0f;
        // Reset last kill time
        _lastKillTime = 0f;

        // Edge bars/flash cleanup disabled (not initialized)

        try
        {
            OnDeactivated?.Invoke();
            OnChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Rage] Error during OnDeactivated dispatch: {ex.Message}");
        }

        // Update UI
        RageUISystem.RefreshLabel();
    }


    /// <summary>
    /// Adds or subtracts Rage
    /// </summary>
    internal static void AddRage(float amount, string reason)
    {
        if (!Enabled)
            return;

        if (amount == 0f) return;

        float before = _ragePercent;
        _ragePercent = Mathf.Clamp(before + amount, 0f, TIER3_OVERFLOW_THRESHOLD);

        if (!_isActive && _ragePercent > TIER3_UI_READY_START_PERCENT)
        {
            // Keep short buffer so a single drain tick during H transitions
            // does not immediately demote Tier3 readiness back to Tier2.
            _tier3ReadyUntilTime = Mathf.Max(_tier3ReadyUntilTime, Time.unscaledTime + TIER3_READY_GRACE_SECONDS);
        }

        // Plugin.Log?.LogInfo($"[RageSystem] AddRage: {amount:F1}% ({reason}), before={before:F1}%, after={_ragePercent:F1}%"); // Disabled for cleaner logs

        if (!Mathf.Approximately(before, _ragePercent))
            NotifyChanged(before, _ragePercent);
    }

    /// <summary>
    /// Resets state - resets activation time
    /// </summary>
    internal static void ResetState()
    {
        float oldPercent = _ragePercent;
        _ragePercent = 0f;
        _isActive = false;
        _currentTier = RageTier.None;
        _tier3ReadyUntilTime = 0f;
        // Reset activation time
        _activationTime = 0f;
        // Reset last kill time
        _lastKillTime = 0f;

        // Deactivate if was active
        if (_isActive)
        {
            Deactivate();
        }

        // Reset slow-mo system
        TimeSlowMoSystem.Reset();
        
        // Edge bars/flash reset disabled (not initialized)
        
        // Clear registered enemies list
        Patches.RageUniversalKillTrackerPatch.ClearRegisteredEnemies();
        
        // Reset combo
        RageComboSystem.ResetState();

        NotifyChanged(oldPercent, 0f);
        Plugin.Log?.LogInfo("[Rage] State reset");
    }

    private static void NotifyChanged(float oldPercent = -1f, float newPercent = -1f)
    {
        if (oldPercent < 0f) oldPercent = _ragePercent;
        if (newPercent < 0f) newPercent = _ragePercent;

        RageUISystem.RefreshLabel();

        try
        {
            OnChanged?.Invoke();
            OnPercentChanged?.Invoke(oldPercent, newPercent);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Rage] Error during OnChanged dispatch: {ex.Message}");
        }
    }

    /// <summary>
    /// System update (called from MonoBehaviour) - drain and auto-deactivation
    /// </summary>
    internal static void Update()
    {
        if (!Enabled) return;
        
        // Update combo system (timeout check)
        RageComboSystem.Update();
        
        // Rage drain during grab (linearly depends on MindBroken: 1%/sec at MB=0%, 10%/sec at MB=100%)
        // Optimized: use UnifiedPlayerCacheManager instead of scene-wide search.
        var playerconInstance = UnifiedPlayerCacheManager.GetPlayer();
        if (playerconInstance != null && playerconInstance.eroflag && !_isActive)
        {
            // Player grabbed and Rage not active - drain Rage
            float mbPercent = MindBrokenSystem.Enabled ? Mathf.Clamp01(MindBrokenSystem.Percent) : 0f;
            
            // Get drain values from config
            float drainMin = Plugin.rageGrabDrainMin?.Value ?? 1.0f;
            float drainMax = Plugin.rageGrabDrainMax?.Value ?? 10.0f;
            
            // Linear interpolation: drainMin%/sec at MB=0%, drainMax%/sec at MB=100%
            float drainPerSecond = Mathf.Lerp(drainMin, drainMax, mbPercent);
            float drainAmount = drainPerSecond * Time.unscaledDeltaTime;
            
            if (_ragePercent > 0f)
            {
                AddRage(-drainAmount, "rage_drain_grabbed");
            }
        }
        
        // During active Rage: drain (Outburst Fury only), MindBroken growth, timer
        if (_isActive)
        {
            float deltaTime = Time.unscaledDeltaTime;
            
            // Tier model: activation cost is paid upfront (30/60/100).
            // Active window duration is controlled by timer per tier (5/10/15s), not by Rage drain.
            
            float timeSinceActivation = Time.unscaledTime - _activationTime;
            float timeSinceLastKill = Time.unscaledTime - _lastKillTime;
            bool isInOverdrive = timeSinceLastKill > KILL_TIMEOUT_THRESHOLD;
            
            if (MindBrokenSystem.Enabled)
            {
                float hcBaseGain = HC_BASE_GAIN_PER_SECOND * deltaTime;
                MindBrokenSystem.AddPercent(hcBaseGain, "rage_active");
            }
            if (isInOverdrive && MindBrokenSystem.Enabled && _currentTier != RageTier.Tier1)
            {
                float hcOverdriveGain = HC_OVERDRIVE_GAIN_PER_SECOND * deltaTime;
                MindBrokenSystem.AddPercent(hcOverdriveGain, "rage_overdrive");
            }
            
            // Deactivate after full duration (10 sec)
            if (timeSinceActivation >= AUTO_DEACTIVATE_DURATION)
            {
                Deactivate();
            }
        }

        // Passive MindBroken while holding a high Rage bar (optional tradeoff vs spending Rage).
        if (MindBrokenSystem.Enabled && (Plugin.mindBrokenHighRagePassiveEnable?.Value ?? true))
        {
            bool allowWhenInactive = Plugin.mindBrokenHighRagePassiveOnlyWhenRageInactive?.Value ?? true;
            if (!allowWhenInactive || !_isActive)
            {
                float thr = Plugin.mindBrokenHighRageThresholdPercent?.Value ?? 60f;
                if (_ragePercent > thr)
                {
                    float perSecPct = Mathf.Max(0f, Plugin.mindBrokenHighRagePassivePercentPerSecond?.Value ?? 0.1f);
                    if (perSecPct > 0f)
                    {
                        float perSecFrac = perSecPct / 100f;
                        MindBrokenSystem.AddPercent(perSecFrac * Time.unscaledDeltaTime, "high_rage_passive_mb");
                    }
                }
            }
        }

        // Update TimeSlowMo system
        TimeSlowMoSystem.Update();
    }
    
    private static RageSystemMono? _cachedMono;

    /// <summary>
    /// Returns cached RageSystemMono (used by TimeSlowMoSystem). Avoids FindObjectOfType every call.
    /// </summary>
    internal static RageSystemMono? GetCachedMono() => _cachedMono;

    internal static void ClearMonoCache()
    {
        _cachedMono = null;
    }

    /// <summary>
    /// Ensures RageSystemMono exists for Update() calls
    /// </summary>
    private static void EnsureMonoBehaviour()
    {
        if (_cachedMono != null) return;

        var existing = GameObject.FindObjectOfType<RageSystemMono>();
        if (existing != null)
        {
            _cachedMono = existing;
            return;
        }

        GameObject go = new GameObject("RageSystemMono");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _cachedMono = go.AddComponent<RageSystemMono>();
    }

    #region Harmony patches

    [HarmonyPatch(typeof(EnemyHandoffSystem), nameof(EnemyHandoffSystem.ResetAllData))]
    private static class ResetAllDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ResetState();
        }
    }

    #endregion
}

/// <summary>
/// MonoBehaviour for Update
/// </summary>
internal class RageSystemMono : MonoBehaviour
{
    private void Update()
    {
        RageSystem.Update();
    }

    private void OnDestroy()
    {
        RageSystem.ClearMonoCache();
    }
}

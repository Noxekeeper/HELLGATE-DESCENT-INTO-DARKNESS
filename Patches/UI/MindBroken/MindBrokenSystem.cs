using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NoREroMod.Patches.Enemy;
using NoREroMod.Patches.Enemy.Kakash;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.UI;
using System.Reflection;


namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Centralised manager for the Mind Broken mechanic and its hooks.
/// </summary>
internal static class MindBrokenSystem
{
    private static bool IsEnabled => Plugin.enableMindBroken?.Value ?? false;

    private static int _handoffCount;
    private static float _percent;

    internal static event Action? OnChanged;
    internal static event Action<float, float>? OnPercentChanged; // oldPercent, newPercent
    internal static event Action<float>? OnMilestoneReached; // milestone percent (0.5, 0.75, 0.9, 1.0)

    internal static bool Enabled => IsEnabled;
    internal static float Percent => Enabled ? _percent : 0f;

    /// <summary>True while lethal-trap vengeance shock (or similar) drives MB without auto Bad End / milestone FX.</summary>
    internal static bool IsScriptedSequenceActive { get; private set; }

    private static float PercentPerPass => Mathf.Max(Plugin.mindBrokenPercentPerPass?.Value ?? 0.01f, 0f);
    private static float MaxPercent => Mathf.Clamp01(Plugin.mindBrokenMaxPercent?.Value ?? 1f);
    private static float StruggleBonusPerStep => Mathf.Max(Plugin.mindBrokenStruggleBonusPerStep?.Value ?? 0.3f, 0f);

    internal static float Steps
    {
        get
        {
            if (!Enabled) return 0f;
            float stepSize = PercentPerPass;
            if (stepSize <= 0f)
            {
                return 0f;
            }

            return Percent / stepSize;
        }
    }

    // Countdown at 100%
    private static float _countdownTimer = 0f;
    private static float COUNTDOWN_DURATION => Plugin.mindBrokenBadEndCountdownDuration?.Value ?? 300f;
    private static float COUNTDOWN_RESET_THRESHOLD => Plugin.mindBrokenBadEndResetThreshold?.Value ?? 0.9f;
    
    internal static float CountdownTimeRemaining => _countdownTimer;
    internal static bool IsCountdownActive => _countdownTimer > 0f;
    
    internal static string GetDisplayText()
    {
        int percent = Mathf.RoundToInt(Percent * 100f);
        
        // If 100% - show "MIND BROKEN" with countdown
        if (percent >= 100)
        {
            if (IsCountdownActive)
            {
                int minutes = Mathf.FloorToInt(_countdownTimer / 60f);
                int seconds = Mathf.FloorToInt(_countdownTimer % 60f);
                return $"MIND BROKEN - {minutes}:{seconds:D2}";
            }
            else
            {
                return "MIND BROKEN";
            }
        }
        
        // Levels with different text
        if (percent >= 50)
        {
            return $"Mind Break: {percent}%";
        }
        else if (percent >= 20)
        {
            return $"Corruption: {percent}%";
        }
        else
        {
            return $"Temptation: {percent}%";
        }
    }
    
    /// <summary>
    /// Get color based on level and percent.
    /// </summary>
    internal static Color GetColorForPercent(float percent)
    {
        if (percent >= 1.0f)
        {
            // 100% - pure red
            return Color.red;
        }
        else if (percent >= 0.5f)
        {
            // 50-99%: Mind Break - pink → bright pink → red
            float t = (percent - 0.5f) / 0.5f; // 0.0 (50%) → 1.0 (100%)
            Color pink = new Color(1f, 0.4f, 0.7f); // Pink
            Color brightPink = new Color(1f, 0.3f, 0.5f); // Bright pink
            Color red = new Color(1f, 0.2f, 0.2f); // Red
            return Color.Lerp(Color.Lerp(pink, brightPink, t), red, t * 0.5f);
        }
        else if (percent >= 0.2f)
        {
            // 20-49%: Corruption - pink → bright pink
            float t = (percent - 0.2f) / 0.3f; // 0.0 (20%) → 1.0 (50%)
            Color pink = new Color(1f, 0.5f, 0.8f); // Pink
            Color brightPink = new Color(1f, 0.4f, 0.7f); // Bright pink
            return Color.Lerp(pink, brightPink, t);
        }
        else
        {
            // 0-19%: Temptation - white → light pink
            float t = percent / 0.2f; // 0.0 (0%) → 1.0 (20%)
            Color white = new Color(1f, 1f, 1f); // White
            Color lightPink = new Color(1f, 0.7f, 0.9f); // Light pink
            return Color.Lerp(white, lightPink, t);
        }
    }

    internal static float GetStruggleMultiplier()
    {
        if (!Enabled) return 1f;
        return 1f + Steps * StruggleBonusPerStep;
    }

    internal static void RegisterHandoff()
    {
        if (!Enabled) return;
 
         _handoffCount++;
         AddPercent(PercentPerPass, "handoff");
     }
 
    internal static void RegisterClimaxEvent(object? context = null)
    {
        // Climax event binding temporarily disabled.
    }

    internal static void ProcessAnimationEvent(object enemyInstance, string currentAnim, string eventName)
    {
        // Method kept for compatibility, but no longer adds percent for FIN/IKI/JIGO.
    }

    internal static void ResetState()
    {
        float oldPercent = _percent;
        _handoffCount = 0;
        _percent = 0f;
        _countdownTimer = 0f;
        MindBrokenBadEndSystem.StopCountdown();
        MindBrokenBadEndSystem.HideBadEnd();
        NotifyChanged(oldPercent, 0f);
    }

    /// <summary>
    /// Restores MindBroken fraction (0..1) and optional Bad-End countdown from save data.
    /// </summary>
    internal static void RestorePersistedState(float fraction01, float badEndCountdownRemaining)
    {
        if (!Enabled) return;

        float old = _percent;
        _percent = Mathf.Clamp(fraction01, 0f, MaxPercent);

        bool needCountdownDriver = false;
        if (_percent < 1f)
        {
            _countdownTimer = 0f;
            MindBrokenBadEndSystem.StopCountdown();
            if (old >= 1f)
                MindBrokenBadEndSystem.HideBadEnd();
        }
        else
        {
            if (badEndCountdownRemaining > 0.001f)
            {
                _countdownTimer = badEndCountdownRemaining;
                needCountdownDriver = true;
            }
            else
            {
                _countdownTimer = 0f;
            }
        }

        NotifyChanged(old, _percent);

        if (needCountdownDriver)
            MindBrokenBadEndSystem.StartCountdown();
    }
    
    /// <summary>
    /// Update countdown (called from MindBrokenBadEndSystem)
    /// </summary>
    internal static void UpdateCountdown(float deltaTime)
    {
        if (IsScriptedSequenceActive)
            return;

        // If percent dropped below 100%, stop timer
        if (_countdownTimer > 0f && _percent < 1.0f)
        {
            // Percent dropped - stop timer (but don't reset if >= threshold)
            if (_percent < COUNTDOWN_RESET_THRESHOLD)
            {
                // Dropped below reset threshold - reset timer
                _countdownTimer = 0f;
                MindBrokenBadEndSystem.StopCountdown();
            }
            else
            {
                // Stop and reset timer (threshold <= percent < 100%)
                _countdownTimer = 0f;
                MindBrokenBadEndSystem.StopCountdown();
            }
            return;
        }
        
        if (_countdownTimer > 0f && _percent >= 1.0f)
        {
            _countdownTimer -= deltaTime;
            
            if (_countdownTimer <= 0f)
            {
                _countdownTimer = 0f;
                // Timer reached 0 - trigger Bad End
                MindBrokenBadEndSystem.TriggerBadEnd();
            }
            else
            {
                // Update UI
                MindBrokenUIPatch.RefreshLabel();
            }
        }
    }

    internal static void BeginScriptedSequence()
    {
        IsScriptedSequenceActive = true;
        if (_countdownTimer > 0f)
        {
            _countdownTimer = 0f;
            MindBrokenBadEndSystem.StopCountdown();
        }
    }

    internal static void SetScriptedPercent(float fraction01)
    {
        if (!Enabled)
            return;

        _percent = Mathf.Clamp(fraction01, 0f, MaxPercent);
        MindBrokenUIPatch.RefreshLabel();
        OnChanged?.Invoke();
    }

    internal static void EndScriptedSequence(float finalFraction01)
    {
        IsScriptedSequenceActive = false;
        float before = _percent;
        _percent = Mathf.Clamp(finalFraction01, 0f, MaxPercent);
        MindBrokenUIPatch.RefreshLabel();
        OnChanged?.Invoke();
        if (!Mathf.Approximately(before, _percent))
            NotifyChanged(before, _percent);
    }

    internal static void AddPercent(float amount, string reason)
    {
        if (!Enabled)
        {
            return;
        }

        if (IsScriptedSequenceActive)
        {
            return;
        }

        if (amount == 0f)
        {
            return;
        }

        float before = _percent;
        _percent = Mathf.Clamp(before + amount, 0f, MaxPercent);

        if (Plugin.mindBrokenDebugLogAddPercent?.Value ?? false)
        {
            Plugin.Log?.LogInfo(
                "[MindBroken] AddPercent "
                + (amount >= 0f ? "+" : "")
                + (amount * 100f).ToString("0.####")
                + "% reason="
                + reason
                + " -> "
                + (_percent * 100f).ToString("0.##")
                + "%");
        }

        if (!Mathf.Approximately(before, _percent))
        {
            NotifyChanged(before, _percent);
        }
        else
        {
            if (_percent >= 1.0f && !IsCountdownActive)
            {
                if (before < 1.0f || (1.0f - before) > 0.001f)
                {
                    CheckMilestones(before, _percent);
                }
            }
        }
    }

    private static void NotifyChanged(float oldPercent = -1f, float newPercent = -1f)
    {
        if (oldPercent < 0f) oldPercent = _percent;
        if (newPercent < 0f) newPercent = _percent;

        MindBrokenUIPatch.RefreshLabel();

        OnChanged?.Invoke();
        OnPercentChanged?.Invoke(oldPercent, newPercent);
        CheckMilestones(oldPercent, newPercent);
    }

    private static void CheckMilestones(float oldPercent, float newPercent)
    {
        if (IsScriptedSequenceActive)
            return;

        if (newPercent >= 1.0f)
        {
            bool timerNotActive = !IsCountdownActive;
            bool shouldStart = timerNotActive;
            if (shouldStart)
            {
                _countdownTimer = COUNTDOWN_DURATION;
                MindBrokenBadEndSystem.StartCountdown();
                MindBrokenUIPatch.RefreshLabel();
            }
        }
        else if (newPercent < COUNTDOWN_RESET_THRESHOLD && IsCountdownActive)
        {
            _countdownTimer = 0f;
            MindBrokenBadEndSystem.StopCountdown();
        }
        else if (newPercent < 1.0f && oldPercent >= 1.0f)
        {
            _countdownTimer = 0f;
            MindBrokenBadEndSystem.StopCountdown();
        }
        
        float[] milestones = { 0.5f, 0.75f, 0.9f, 1.0f };
        
        foreach (float milestone in milestones)
        {
            // Check milestone crossing from bottom to top or top to bottom
            bool crossedUp = oldPercent < milestone && newPercent >= milestone;
            bool crossedDown = oldPercent >= milestone && newPercent < milestone;
            
            if (crossedUp || crossedDown)
            {
                OnMilestoneReached?.Invoke(milestone);
            }
        }
    }

    #region Harmony patches

    [HarmonyPatch(typeof(StruggleSystem), "setStruggleLevel")]
    private static class StruggleLevelPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!Enabled) return;
            StruggleSystem.struggleMultiplier *= GetStruggleMultiplier();
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), "ParalysisOrgasm")]
    private static class ClimaxDetectedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerStatus __instance)
        {
            // MindBroken no longer reacts to climax events
        }
    }
 
    #endregion
}

/// <summary>
/// Harmony patches + utilities for rendering the Mind Broken label.
/// </summary>
internal static class MindBrokenUIPatch
{
    internal const string OverlayCanvasObjectName = "MindBrokenOverlayCanvas_XUAIGNORE";
    private const string CanvasObjectName = OverlayCanvasObjectName;
    private const string LabelObjectName = "MindBrokenLabel_XUAIGNORE";
    internal static bool ForceShowLabelDuringBlackBackground = false;

    // Use approach as in Corruption/Recovery: fallback to Arial (built-in), no custom TMP asset

    // Absolute screen coordinates (Unity Explorer style): X from left, Y from bottom.
    // Converted for anchor (0,1): anchoredX = screenX, anchoredY = screenY - 1079.
    private const float TargetScreenX = 1460f;
    private const float TargetScreenY = 1059f;
    internal static Vector2 TargetAnchoredPosition =>
        new Vector2(TargetScreenX, TargetScreenY - 1079f);

    private static RectTransform? overlayCanvasRect;
    private static MindBrokenUILabel? currentLabel;

    internal static void InitializeFromPlugin()
    {
        try
        {
            RefreshLabel();
        }
        catch (Exception ex)
        {
        }
    }

    internal static void RefreshLabel()
    {
        if (!MindBrokenSystem.Enabled)
        {
            DestroyExisting();
            return;
        }

        EnsureOverlayCanvas();
        EnsureLabel();
        ForceLabelPosition();
        currentLabel?.ForceRefresh();
    }

    // New patch: On UImng.Start (more reliable)
    [HarmonyPatch(typeof(UImng), "Start")]
    private static class UImngStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UImng __instance)
        {
            try
            {
                if (!MindBrokenSystem.Enabled)
                {
                    DestroyExisting();
                    return;
                }
                
                // Wait a bit for UI to initialize
                __instance.StartCoroutine(DelayedUISetup());
            }
            catch (Exception ex)
            {
            }
        }
        
        private static System.Collections.IEnumerator DelayedUISetup()
        {
            // Wait 0.5 seconds for UI to fully initialize
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                EnsureOverlayCanvas();
                if (overlayCanvasRect == null)
                {
                    yield break;
                }

                EnsureLabel();
                ForceLabelPosition();
            }
            catch (Exception ex)
            {
            }
        }
    }

    // Keep old patch as fallback
    [HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
    private static class CanvasBadstatusStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CanvasBadstatusinfo __instance)
        {
            try
            {
                EnsureOverlayCanvas();
                EnsureLabel();
                ForceLabelPosition();
            }
            catch (Exception ex)
            {
            }
        }
    }

    private static void DestroyExisting()
    {
        if (currentLabel != null)
        {
            try
            {
                UnityEngine.Object.Destroy(currentLabel.gameObject);
                currentLabel = null;
            }
            catch (Exception ex)
            {
            }
        }
        
        GameObject existing = GameObject.Find(LabelObjectName);
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing);
        }
    }

    private static void EnsureOverlayCanvas()
    {
        if (!MindBrokenSystem.Enabled)
        {
            DestroyExisting();
            return;
        }

        try
        {
            if (overlayCanvasRect != null)
            {
                return;
            }

            GameObject existing = GameObject.Find(CanvasObjectName);
            if (existing != null)
            {
                overlayCanvasRect = existing.GetComponent<RectTransform>();
                return;
            }

            GameObject canvasGo = new GameObject(CanvasObjectName);
            overlayCanvasRect = canvasGo.AddComponent<RectTransform>();

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;
            canvasGo.layer = LayerMask.NameToLayer("UI");

            overlayCanvasRect.anchorMin = Vector2.zero;
            overlayCanvasRect.anchorMax = Vector2.one;
            overlayCanvasRect.pivot = new Vector2(0.5f, 0.5f);
            overlayCanvasRect.offsetMin = Vector2.zero;
            overlayCanvasRect.offsetMax = Vector2.zero;
            overlayCanvasRect.localScale = Vector3.one;

            canvasGo.SetActive(true);

            UnityEngine.Object.DontDestroyOnLoad(canvasGo);

        }
        catch (Exception ex)
        {
        }
    }

    private static void EnsureLabel()
    {
        if (!MindBrokenSystem.Enabled)
        {
            DestroyExisting();
            return;
        }

        if (overlayCanvasRect == null)
        {
            return;
        }


        // First check existing label
        if (currentLabel != null && currentLabel.gameObject != null)
        {
            var existingRect = currentLabel.GetComponent<RectTransform>();
            if (existingRect != null)
            {
                existingRect.anchorMin = new Vector2(0f, 1f);
                existingRect.anchorMax = new Vector2(0f, 1f);
                existingRect.pivot = new Vector2(0f, 1f);
                existingRect.anchoredPosition = TargetAnchoredPosition;
            }

            if (currentLabel.transform.parent != overlayCanvasRect)
            {
                currentLabel.transform.SetParent(overlayCanvasRect, false);
            }

            currentLabel.gameObject.SetActive(true);
            currentLabel.ForceRefresh();
            return;
        }

        // Create new GameObject
        GameObject go = new GameObject(LabelObjectName);
        go.transform.SetParent(overlayCanvasRect, false);

        // RectTransform setup (sync size with Rage UI)
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);  // Top-left anchor
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = TargetAnchoredPosition;
        rect.sizeDelta = new Vector2(276f, 50f); // Same size as Rage UI

        // CanvasGroup for proper rendering
        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var label = go.AddComponent<UnityEngine.UI.Text>();
        label.fontSize = 40; // Same base size as Rage UI
        label.alignment = TextAnchor.UpperLeft;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(1f, 0.8f, 0.2f); // Keep golden tint to distinguish from Rage
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();

        // Set layer same as parent
        go.layer = LayerMask.NameToLayer("UI");
        
        // Force activate
        go.SetActive(true);

        // Add update component
        MindBrokenUILabel updater = go.AddComponent<MindBrokenUILabel>();
        updater.Initialise(label);

        // Set initial text
        label.text = MindBrokenSystem.GetDisplayText();

        Canvas.ForceUpdateCanvases();


        currentLabel = updater;
    }

    private static void ForceLabelPosition()
    {
        try
        {
            if (overlayCanvasRect == null)
            {
                return;
            }

            // Keep canvas and label always active; visibility is controlled by CanvasGroup.alpha
            // inside MindBrokenUILabel.Refresh(), so LateUpdate keeps running even when hidden.
            if (!overlayCanvasRect.gameObject.activeSelf)
                overlayCanvasRect.gameObject.SetActive(true);

            var labelRect = currentLabel?.GetComponent<RectTransform>();
            if (labelRect == null)
            {
                return;
            }

            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = TargetAnchoredPosition;

            if (currentLabel != null && !currentLabel.gameObject.activeSelf)
                currentLabel.gameObject.SetActive(true);
        }
        catch (Exception)
        {
        }
    }

    internal static bool ShouldShowLabelForUI()
    {
        return ShouldShowLabelInternal();
    }

    // Mirror the vanilla HUD, but keep the label visible during the black-background
    // H-scene overlay (MindBroken is meaningful there even though UImng is hidden).
    private static bool ShouldShowLabelInternal()
    {
        if (ForceShowLabelDuringBlackBackground)
            return true;
        return HudVisibilityGate.ShouldShowGameplayHud();
    }

}

/// <summary>
/// Runtime component that keeps the label text in sync with the Mind Broken state.
/// </summary>
internal class MindBrokenUILabel : MonoBehaviour
{
    private UnityEngine.UI.Text? _label;
    private RectTransform? _rect;
    private CanvasGroup? _cg;
    private UnityEngine.UI.Image? _barBg;
    private UnityEngine.UI.Image? _barFill;
    private UnityEngine.UI.Text? _stageLabel;
    private RectTransform? _barFillRect;
    private float _blinkTimer = 0f;
    private const float BLINK_INTERVAL = 0.5f; // Blink every 0.5 sec
    private const float BarWidth = 240f;
    private const float BarHeight = 10f;
    private static readonly Vector2 BarOffset = new Vector2(0f, -42f);
    private static readonly Vector2 StageOffset = new Vector2(0f, -60f);
    private static bool _labelNullLoggedOnce;
    private static bool _refreshErrorLoggedOnce;

    internal void Initialise(UnityEngine.UI.Text label)
    {
        _label = label;
        _rect = GetComponent<RectTransform>();
        EnsureFont();
        Refresh();
    }

    private void OnEnable()
    {
        MindBrokenSystem.OnChanged += Refresh;
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
        }
        ApplyAnchors();
        Refresh();
    }

    private void OnDisable()
    {
        MindBrokenSystem.OnChanged -= Refresh;
    }

    private void LateUpdate()
    {
        if (MindBrokenBadEndSystem.IsBadEndActive) return;
        // Apply anchors every frame as precaution
        ApplyAnchors();

        // Update blink timer
        if (MindBrokenSystem.Percent >= 1.0f)
        {
            _blinkTimer += Time.unscaledDeltaTime;
        }

        // Always refresh so visibility mirrors the vanilla HUD in real time
        // (we can no longer rely on SetActive-driven OnEnable events).
        Refresh();
    }

    private void Refresh()
    {
        if (MindBrokenBadEndSystem.IsBadEndActive) return;
        try
        {
            if (_label == null)
            {
                _label = GetComponent<UnityEngine.UI.Text>();
                if (_label == null)
                {
                    if (!_labelNullLoggedOnce) { _labelNullLoggedOnce = true; }
                    return;
                }
            }
            
            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }

            bool enabled = MindBrokenSystem.Enabled;
            bool shouldShow = enabled && MindBrokenUIPatch.ShouldShowLabelForUI() && !EventCoreRuntime.IsSessionOpen;
            string newText = enabled ? MindBrokenSystem.GetDisplayText() : string.Empty;
            _label.text = newText;
            EnsureProgressUi();
            UpdateProgressUi(shouldShow);
            
            // Font applied only on label creation (as in old version)
            
            // Apply color with gradient or blinking
            if (enabled && _label != null)
            {
                float percent = MindBrokenSystem.Percent;
                
                if (percent >= 1.0f)
                {
                    bool isVisible = (_blinkTimer % (BLINK_INTERVAL * 2f)) < BLINK_INTERVAL;
                    _label.color = isVisible ? Color.red : new Color(1f, 0f, 0f, 0.3f);
                    _label.fontStyle = FontStyle.Bold;
                }
                else
                {
                    _label.color = MindBrokenSystem.GetColorForPercent(percent);
                    _label.fontStyle = FontStyle.Bold;
                }
            }
            
            // Drive visibility via CanvasGroup.alpha so LateUpdate keeps ticking
            // and we react to gate changes (scene load, H-scene end, etc.) immediately.
            if (_cg == null)
            {
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null)
                    _cg = gameObject.AddComponent<CanvasGroup>();
            }
            _cg.alpha = shouldShow ? 1f : 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }
        catch (Exception ex)
        {
            if (!_refreshErrorLoggedOnce) { _refreshErrorLoggedOnce = true; }
        }
    }

    internal void AssignLabel(UnityEngine.UI.Text label)
    {
        _label = label;
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
        }
        ApplyAnchors();
        ForceRefresh();
    }

    internal void ForceRefresh()
    {
        Refresh();
    }

    private void ApplyAnchors()
    {
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
        }

        if (_rect == null)
        {
            return;
        }

        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        
        if (_rect.anchoredPosition != MindBrokenUIPatch.TargetAnchoredPosition)
        {
            _rect.anchoredPosition = MindBrokenUIPatch.TargetAnchoredPosition;
        }
    }
    
    /// <summary>
    /// Ensure font is correctly set from game UI
    /// </summary>
    private void EnsureFont()
    {
        // UI.Text: font set on creation (Arial), no additional actions needed
    }

    private void EnsureProgressUi()
    {
        if (_barBg == null)
        {
            GameObject bgGo = new GameObject("MindBrokenBarBg");
            bgGo.transform.SetParent(transform, false);
            RectTransform rt = bgGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = BarOffset;
            rt.sizeDelta = new Vector2(BarWidth, BarHeight);

            _barBg = bgGo.AddComponent<UnityEngine.UI.Image>();
            _barBg.raycastTarget = false;
            _barBg.color = new Color(0f, 0f, 0f, 0.45f);
        }

        if (_barFill == null)
        {
            GameObject fillGo = new GameObject("MindBrokenBarFill");
            fillGo.transform.SetParent(transform, false);
            _barFillRect = fillGo.AddComponent<RectTransform>();
            _barFillRect.anchorMin = new Vector2(0f, 1f);
            _barFillRect.anchorMax = new Vector2(0f, 1f);
            _barFillRect.pivot = new Vector2(0f, 1f);
            _barFillRect.anchoredPosition = BarOffset;
            _barFillRect.sizeDelta = new Vector2(0f, BarHeight);

            _barFill = fillGo.AddComponent<UnityEngine.UI.Image>();
            _barFill.raycastTarget = false;
            _barFill.color = new Color(1f, 0.7f, 0.9f, 0.95f);
        }

        if (_stageLabel == null)
        {
            GameObject stageGo = new GameObject("MindBrokenStageLabel");
            stageGo.transform.SetParent(transform, false);
            RectTransform rt = stageGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = StageOffset;
            rt.sizeDelta = new Vector2(300f, 18f);

            _stageLabel = stageGo.AddComponent<UnityEngine.UI.Text>();
            _stageLabel.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
            _stageLabel.fontSize = 16;
            _stageLabel.fontStyle = FontStyle.Italic;
            _stageLabel.alignment = TextAnchor.UpperLeft;
            _stageLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _stageLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _stageLabel.raycastTarget = false;
        }
    }

    private void UpdateProgressUi(bool enabled)
    {
        if (_barBg == null || _barFill == null || _barFillRect == null || _stageLabel == null)
            return;

        if (!enabled)
        {
            _barBg.enabled = false;
            _barFill.enabled = false;
            _stageLabel.enabled = false;
            return;
        }

        float p = Mathf.Clamp01(MindBrokenSystem.Percent);
        _barBg.enabled = true;
        _barFill.enabled = true;
        _stageLabel.enabled = true;
        _barFillRect.sizeDelta = new Vector2(BarWidth * p, BarHeight);
        _barFill.color = MindBrokenSystem.GetColorForPercent(p);

        if (p >= 1f) _stageLabel.text = "Stage: Mind Broken";
        else if (p >= 0.5f) _stageLabel.text = "Stage: Mind Break";
        else if (p >= 0.2f) _stageLabel.text = "Stage: Corruption";
        else _stageLabel.text = "Stage: Temptation";
        _stageLabel.color = MindBrokenSystem.GetColorForPercent(p);
    }
}

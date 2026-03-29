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
    /// Update countdown (called from MindBrokenBadEndSystem)
    /// </summary>
    internal static void UpdateCountdown(float deltaTime)
    {
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

    internal static void AddPercent(float amount, string reason)
    {
        if (!Enabled)
        {
            return;
        }

        if (amount == 0f)
        {
            return;
        }

        float before = _percent;
        _percent = Mathf.Clamp(before + amount, 0f, MaxPercent);

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

    [HarmonyPatch(typeof(EnemyHandoffSystem), nameof(EnemyHandoffSystem.ResetAllData))]
    private static class ResetAllDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ResetState();
        }
    }

    [HarmonyPatch(typeof(TouzokuNormalPassPatch), nameof(TouzokuNormalPassPatch.ExecuteHandoff))]
    [HarmonyPatch(typeof(TouzokuAxePassPatch), nameof(TouzokuAxePassPatch.ExecuteHandoff))]
    [HarmonyPatch(typeof(InquisitionBlackPassPatch), nameof(InquisitionBlackPassPatch.ExecuteHandoff))]
    [HarmonyPatch(typeof(GoblinPassLogic), nameof(GoblinPassLogic.ExecuteHandoff))]
    [HarmonyPatch(typeof(BigoniBrotherPassLogic), nameof(BigoniBrotherPassLogic.ExecuteHandoff))]
    [HarmonyPatch(typeof(KakasiPassLogic), nameof(KakasiPassLogic.ExecuteHandoff))]
    private static class HandoffCompletedPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            RegisterHandoff();
        }
    }

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
    private const string CanvasObjectName = "MindBrokenOverlayCanvas_XUAIGNORE";
    private const string LabelObjectName = "MindBrokenLabel_XUAIGNORE";
    internal static bool ForceShowLabelDuringBlackBackground = false;

    // Use approach as in Corruption/Recovery: fallback to Arial (built-in), no custom TMP asset

    // Moved: screen coordinates ~ (1600, 1000) at anchor (0,1)
    internal static readonly Vector2 TargetAnchoredPosition = new Vector2(1500f, -50f);

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
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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

            bool shouldShow = ShouldShowLabelInternal();
            overlayCanvasRect.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                if (currentLabel != null)
                {
                    currentLabel.gameObject.SetActive(false);
                }
                return;
            }

            var labelRect = currentLabel?.GetComponent<RectTransform>();
            if (labelRect == null)
            {
                return;
            }

            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = TargetAnchoredPosition;
            labelRect.position = new Vector3(260f, 1040f, labelRect.position.z);

            currentLabel?.gameObject.SetActive(true);
        }
        catch (Exception ex)
        {
        }
    }

    internal static bool ShouldShowLabelForUI()
    {
        return ShouldShowLabelInternal();
    }

    private static bool? _cachedHasGameplayCanvas;
    private static float _canvasCacheTime;
    private const float CanvasCacheInterval = 2f;

    private static bool ShouldShowLabelInternal()
    {
        try
        {
            if (ForceShowLabelDuringBlackBackground)
            {
                return true;
            }
            var sceneName = SceneManager.GetActiveScene().name;
            bool isMenu = string.Equals(sceneName, "Gametitle", StringComparison.OrdinalIgnoreCase);
            if (isMenu) return false;
            // FPS: cache FindObjectOfType result - invalidates every 2 sec
            float now = Time.unscaledTime;
            if (!_cachedHasGameplayCanvas.HasValue || (now - _canvasCacheTime) > CanvasCacheInterval)
            {
                _cachedHasGameplayCanvas = UnityEngine.Object.FindObjectOfType<CanvasBadstatusinfo>() != null;
                _canvasCacheTime = now;
            }
            return _cachedHasGameplayCanvas.Value;
        }
        catch
        {
            return false;
        }
    }

}

/// <summary>
/// Runtime component that keeps the label text in sync with the Mind Broken state.
/// </summary>
internal class MindBrokenUILabel : MonoBehaviour
{
    private UnityEngine.UI.Text? _label;
    private RectTransform? _rect;
    private float _blinkTimer = 0f;
    private const float BLINK_INTERVAL = 0.5f; // Blink every 0.5 sec
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
            // Update color for blinking
            Refresh();
        }
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
            bool shouldShow = enabled && MindBrokenUIPatch.ShouldShowLabelForUI();
            string newText = enabled ? MindBrokenSystem.GetDisplayText() : string.Empty;
            _label.text = newText;
            
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
            
            gameObject.SetActive(shouldShow);
            
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
}

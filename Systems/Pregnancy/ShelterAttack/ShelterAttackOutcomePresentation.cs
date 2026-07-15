using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.UI;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>
/// After victory/defeat: clears assault HUD immediately, waits 3s, then shows a head thought
/// and a short outcome summary banner below the wave position.
/// </summary>
internal static class ShelterAttackOutcomePresentation
{
    private const string PlayerBoneName = "hair1";
    private const float ThoughtVerticalOffsetPx = 32f;
    private const float ThoughtBoneWorldOffsetY = 0.3f;
    private const float DelayBeforeShowSeconds = 3f;
    private const float DisplayDurationSeconds = 12f;

    private static readonly Color VictoryThoughtColor = new Color(1f, 0.82f, 0.2f, 1f);
    private static readonly Color DefeatThoughtColor = new Color(1f, 0.2f, 0.2f, 1f);
    private static readonly Color ThoughtOutlineColor = new Color(0f, 0f, 0f, 1f);
    private static readonly Color VictorySummaryColor = new Color(0.35f, 0.92f, 0.4f, 1f);
    private static readonly Color DefeatSummaryColor = new Color(1f, 0.28f, 0.25f, 1f);

    private static Coroutine _running;

    internal static void PlayVictory(int growthAdvancedCount)
    {
        StartPresentation(isVictory: true, isTimeout: false, kidnappedCount: 0, growthAdvancedCount);
    }

    internal static void PlayDefeat(int kidnappedCount)
    {
        StartPresentation(isVictory: false, isTimeout: false, kidnappedCount, growthAdvancedCount: 0);
    }

    internal static void PlayTimeoutDefeat(int kidnappedCount)
    {
        StartPresentation(isVictory: false, isTimeout: true, kidnappedCount, growthAdvancedCount: 0);
    }

    internal static void Cancel()
    {
        if (_running != null && Plugin.Instance != null)
            Plugin.Instance.StopCoroutine(_running);

        _running = null;
        ShelterAttackOutcomeHud.Hide();
    }

    private static void StartPresentation(bool isVictory, bool isTimeout, int kidnappedCount, int growthAdvancedCount)
    {
        Cancel();
        ShelterAttackTimerHud.Reset();
        ShelterAttackTimerHud.ClearTimeoutFlash();

        if (Plugin.Instance == null)
            return;

        _running = Plugin.Instance.StartCoroutine(PresentationSequence(isVictory, isTimeout, kidnappedCount, growthAdvancedCount));
    }

    private static IEnumerator PresentationSequence(bool isVictory, bool isTimeout, int kidnappedCount, int growthAdvancedCount)
    {
        yield return new WaitForSecondsRealtime(DelayBeforeShowSeconds);

        if (isVictory)
        {
            if (ShelterAttackPhrases.TryGetRandomVictoryThought(out string thought))
                ShowHeadThought(thought, VictoryThoughtColor);

            ShelterAttackOutcomeHud.Show(
                ShelterAttackPhrases.GetVictorySummary(growthAdvancedCount),
                VictorySummaryColor);
        }
        else if (isTimeout)
        {
            if (ShelterAttackPhrases.TryGetRandomTimeoutThought(out string timeoutThought))
                ShowHeadThought(timeoutThought, DefeatThoughtColor);
            else if (ShelterAttackPhrases.TryGetRandomDefeatThought(out string fallbackThought))
                ShowHeadThought(fallbackThought, DefeatThoughtColor);

            ShelterAttackOutcomeHud.Show(
                ShelterAttackPhrases.FormatTimeoutSummary(kidnappedCount),
                DefeatSummaryColor);
        }
        else
        {
            if (ShelterAttackPhrases.TryGetRandomDefeatThought(out string thought))
                ShowHeadThought(thought, DefeatThoughtColor);

            ShelterAttackOutcomeHud.Show(
                ShelterAttackPhrases.FormatDefeatSummary(kidnappedCount),
                DefeatSummaryColor);
        }

        yield return new WaitForSecondsRealtime(DisplayDurationSeconds);

        ShelterAttackOutcomeHud.Hide();
        _running = null;
    }

    private static void ShowHeadThought(string line, Color textColor)
    {
        if (string.IsNullOrEmpty(line))
            return;

        GameObject playerObj = UnifiedPlayerCacheManager.GetPlayer()?.gameObject;
        if (playerObj == null)
            playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj == null)
            return;

        try
        {
            if (!DialogueFramework.IsInitialized)
                DialogueFramework.Initialize();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.ShelterAttack] Outcome thought init failed: " + ex.Message);
            return;
        }

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
            return;

        float duration = DisplayDurationSeconds;
        float fontSize = (Plugin.dialogueFontSize?.Value ?? 16f) * 2f;

        DialogueStyle style = DialogueDisplay.BuildAradiaThoughtStyle(
            ThoughtVerticalOffsetPx,
            0f,
            true,
            textColor,
            ThoughtOutlineColor);
        style.FontSize = fontSize;

        display.ShowAradiaThought(
            playerObj,
            line,
            PlayerBoneName,
            style,
            duration,
            disableBoneFallbacks: false,
            boneWorldOffsetY: ThoughtBoneWorldOffsetY,
            textColor: textColor,
            outlineColor: ThoughtOutlineColor);
    }
}

/// <summary>Short green/red outcome line shown below the wave banner anchor.</summary>
internal static class ShelterAttackOutcomeHud
{
    private const float SummaryCenterYOffset = 280f;
    private const float SummaryFontScale = 2f;
    private const int LayoutVersion = 1;

    private static GameObject _canvasGo;
    private static int _layoutVersion;
    private static CanvasGroup _summaryCg;
    private static Text _summaryText;

    internal static void Show(string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        EnsureUi();
        if (_canvasGo == null || _summaryCg == null || _summaryText == null)
            return;

        _canvasGo.SetActive(true);
        _summaryText.text = text;
        _summaryText.color = color;
        _summaryText.fontSize = GetSummaryFontSize();
        _summaryCg.alpha = 1f;
    }

    internal static void Hide()
    {
        if (_summaryCg != null)
            _summaryCg.alpha = 0f;

        if (_canvasGo != null)
            _canvasGo.SetActive(false);
    }

    private static void EnsureUi()
    {
        if (_canvasGo != null && _layoutVersion != LayoutVersion)
        {
            Object.Destroy(_canvasGo);
            _canvasGo = null;
        }

        if (_canvasGo != null)
            return;

        _layoutVersion = LayoutVersion;
        _canvasGo = new GameObject("ShelterAttackOutcomeHud_XUAIGNORE");
        Object.DontDestroyOnLoad(_canvasGo);

        Canvas canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9811;
        CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject root = new GameObject("OutcomeSummaryRoot");
        root.transform.SetParent(_canvasGo.transform, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, SummaryCenterYOffset);
        rt.sizeDelta = new Vector2(920f, 80f);

        _summaryCg = root.AddComponent<CanvasGroup>();
        _summaryCg.alpha = 0f;
        _summaryCg.blocksRaycasts = false;
        _summaryCg.interactable = false;

        GameObject labelGo = new GameObject("OutcomeSummaryLabel");
        labelGo.transform.SetParent(root.transform, false);
        RectTransform labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        _summaryText = labelGo.AddComponent<Text>();
        _summaryText.font = HellGateFontProvider.GetUiFont();
        _summaryText.alignment = TextAnchor.MiddleCenter;
        _summaryText.fontSize = GetSummaryFontSize();
        _summaryText.color = Color.white;
        _summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _summaryText.verticalOverflow = VerticalWrapMode.Overflow;
        _summaryText.resizeTextForBestFit = false;

        _canvasGo.SetActive(false);
    }

    private static int GetSummaryFontSize()
    {
        float baseSize = Mathf.Max(16f, Plugin.dialogueFontSize != null ? Plugin.dialogueFontSize.Value : 16f);
        return Mathf.RoundToInt(baseSize * SummaryFontScale);
    }
}

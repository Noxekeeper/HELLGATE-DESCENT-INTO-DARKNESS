using NoREroMod.Systems.Pregnancy.Patches;
using NoREroMod.Systems.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>On-screen attack countdown, inter-wave timer, threat label, and wave banners.</summary>
internal static class ShelterAttackTimerHud
{
    // Raised +50px from the previous 90 baseline so the countdown clears the hotbar.
    private const float BottomHudOffsetY = 140f;
    private const float BarMaxWidth = 300f;
    private const float BarHeight = 10f;
    private const float LabelBarGap = 10f;
    private const float ThreatLabelGap = 4f;
    private const float PanelWidth = 420f;
    private const float PanelHeight = 72f;
    private const float TimerFontScale = 1.35f;
    private const float ThreatFontScale = 1.05f;
    private const float BannerFontScale = 2f;
    private const float BannerCenterYOffset = 330f;
    private const int LayoutVersion = 5;

    private static readonly Color TimerRed = new Color(1f, 0.18f, 0.16f, 1f);
    private static readonly Color TimerRedUrgent = new Color(1f, 0.08f, 0.08f, 1f);
    private static readonly Color BarRed = new Color(0.95f, 0.15f, 0.12f, 1f);
    private static readonly Color BarRedUrgent = new Color(1f, 0.08f, 0.08f, 1f);
    private static readonly Color ThreatRed = new Color(1f, 0.25f, 0.2f, 1f);

    private static GameObject _canvasGo;
    private static int _layoutVersion;
    private static CanvasGroup _attackCg;
    private static Text _attackLabel;
    private static Text _attackThreatLabel;
    private static Image _attackBarFill;
    private static RectTransform _attackBarFillRt;
    private static float _attackBarTotalSeconds;
    private static float _timeoutFlashUntilUnscaled;
    private static float _timeoutFlashTotalSeconds;

    private static CanvasGroup _waveBreakCg;
    private static Text _waveBreakLabel;
    private static Text _waveBreakThreatLabel;
    private static Image _waveBreakBarFill;
    private static RectTransform _waveBreakBarFillRt;
    private static float _waveBreakBarTotalSeconds;

    private static CanvasGroup _bannerCg;
    private static Text _bannerText;

    internal static void Process()
    {
        if (!ShouldTick())
        {
            HideAll();
            return;
        }

        EnsureUi();
        if (_canvasGo == null)
            return;

        _canvasGo.SetActive(true);
        ApplyFontSizes();

        if (ShelterAttackState.Phase == ShelterAttackPhase.Armed
            || ShelterAttackState.Phase == ShelterAttackPhase.Alerting)
        {
            if (!ShouldShowAttackCountdown())
            {
                HideAttackPanel();
                HideWaveBreakPanel();
                HideBanner();
                return;
            }

            ShowAttackCountdown();
            HideWaveBreakPanel();
            HideBanner();
            return;
        }

        if (ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak)
        {
            if (!HideoutSceneUtility.IsParishHideoutActive())
            {
                HideAll();
                return;
            }

            HideAttackPanel();
            ShowWaveBreakCountdown();
            ShowBanner(ShelterAttackPhrases.GetWaveAnnouncementText(ShelterAttackState.CurrentWave));
            return;
        }

        HideAll();
    }

    internal static void HideAll()
    {
        if (_canvasGo != null)
            _canvasGo.SetActive(false);
    }

    internal static void Reset()
    {
        HideAll();
        _attackBarTotalSeconds = 0f;
        _waveBreakBarTotalSeconds = 0f;
    }

    internal static void ClearTimeoutFlash()
    {
        _timeoutFlashUntilUnscaled = 0f;
        _timeoutFlashTotalSeconds = 0f;
    }

    internal static bool IsTimeoutFlashActive()
    {
        return _timeoutFlashUntilUnscaled > Time.unscaledTime;
    }

    internal static void BeginTimeoutFlash(float totalSeconds)
    {
        totalSeconds = Mathf.Max(1f, totalSeconds);
        _timeoutFlashTotalSeconds = totalSeconds;
        _timeoutFlashUntilUnscaled = Time.unscaledTime + totalSeconds;
        EnsureUi();
    }

    internal static void ProcessTimeoutFlash()
    {
        if (!IsTimeoutFlashActive())
        {
            HideAttackPanel();
            return;
        }

        EnsureUi();
        if (_canvasGo == null || _attackCg == null || _attackLabel == null)
            return;

        _canvasGo.SetActive(true);
        ApplyFontSizes();

        float remaining = _timeoutFlashUntilUnscaled - Time.unscaledTime;
        _attackCg.alpha = HudVisibilityGate.ShouldShowGameplayHud() ? 1f : 0f;
        _attackLabel.text = ShelterAttackPhrases.GetTimeoutLabel();
        _attackLabel.color = TimerRedUrgent;
        ApplyThreatLabel(_attackThreatLabel);

        if (_attackBarFillRt != null && _attackBarFill != null && _timeoutFlashTotalSeconds > 0.01f)
        {
            float ratio = Mathf.Clamp01(remaining / _timeoutFlashTotalSeconds);
            _attackBarFillRt.sizeDelta = new Vector2(BarMaxWidth * ratio, BarHeight);
            _attackBarFill.color = BarRedUrgent;
        }

        HideWaveBreakPanel();
        HideBanner();
    }

    internal static void NotifyAttackArmed(float totalSeconds)
    {
        _attackBarTotalSeconds = Mathf.Max(1f, totalSeconds);
    }

    internal static void NotifyWaveBreakStarted(float totalSeconds)
    {
        _waveBreakBarTotalSeconds = Mathf.Max(1f, totalSeconds);
    }

    private static void ApplyFontSizes()
    {
        int timerFont = GetTimerFontSize();
        int threatFont = GetThreatFontSize();
        int bannerFont = GetBannerFontSize();

        if (_attackLabel != null)
            _attackLabel.fontSize = timerFont;
        if (_attackThreatLabel != null)
            _attackThreatLabel.fontSize = threatFont;
        if (_waveBreakLabel != null)
            _waveBreakLabel.fontSize = timerFont;
        if (_waveBreakThreatLabel != null)
            _waveBreakThreatLabel.fontSize = threatFont;
        if (_bannerText != null)
            _bannerText.fontSize = bannerFont;
    }

    private static float GetBaseFontSize()
    {
        return Mathf.Max(16f, Plugin.dialogueFontSize != null ? Plugin.dialogueFontSize.Value : 16f);
    }

    private static int GetTimerFontSize()
    {
        return Mathf.RoundToInt(GetBaseFontSize() * TimerFontScale);
    }

    private static int GetThreatFontSize()
    {
        return Mathf.RoundToInt(GetBaseFontSize() * ThreatFontScale);
    }

    private static int GetBannerFontSize()
    {
        return Mathf.RoundToInt(GetBaseFontSize() * BannerFontScale);
    }

    internal static bool ShouldTick()
    {
        if (IsTimeoutFlashActive())
            return true;

        if (!ShouldRunHud())
            return false;

        return ShelterAttackState.Phase == ShelterAttackPhase.Armed
            || ShelterAttackState.Phase == ShelterAttackPhase.Alerting
            || ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak;
    }

    internal static bool ShouldShowAttackCountdown()
    {
        if (ShelterAttackDriver.IsArmRollPending())
            return false;

        if (!IsAttackPhaseVisible())
            return false;

        return ShelterAttackState.GetRemainingSeconds() > 0;
    }

    private static bool IsAttackPhaseVisible()
    {
        return ShelterAttackState.Phase == ShelterAttackPhase.Armed
            || ShelterAttackState.Phase == ShelterAttackPhase.Alerting;
    }

    private static bool ShouldRunHud()
    {
        if (!PregnancyConfig.IsEnabled)
            return false;
        if (PregnancyConfig.EnableShelterAttack == null || !PregnancyConfig.EnableShelterAttack.Value)
            return false;
        if (PregnancyConfig.ShelterAttackShowTimerHud == null || !PregnancyConfig.ShelterAttackShowTimerHud.Value)
            return false;

        return ShelterAttackState.IsEventActive;
    }

    private static void ApplyThreatLabel(Text label)
    {
        if (label == null)
            return;

        label.text = ShelterAttackPhrases.GetThreatLevelLabel(ShelterAttackWaves.GetActiveThreatTier());
        label.color = ThreatRed;
    }

    private static void ShowAttackCountdown()
    {
        if (_attackCg == null || _attackLabel == null)
            return;

        double remaining = ShelterAttackState.GetRemainingSeconds();
        if (remaining <= 0)
        {
            HideAttackPanel();
            return;
        }

        int seconds = Mathf.CeilToInt((float)remaining);
        bool urgent = seconds <= 10;
        _attackCg.alpha = HudVisibilityGate.ShouldShowGameplayHud() ? 1f : 0f;
        _attackLabel.text = ShelterAttackPhrases.FormatAttackCountdown(seconds);
        _attackLabel.color = urgent ? TimerRedUrgent : TimerRed;
        ApplyThreatLabel(_attackThreatLabel);

        if (_attackBarFillRt != null && _attackBarFill != null && _attackBarTotalSeconds > 0.01f)
        {
            float ratio = Mathf.Clamp01((float)remaining / _attackBarTotalSeconds);
            _attackBarFillRt.sizeDelta = new Vector2(BarMaxWidth * ratio, BarHeight);
            _attackBarFill.color = urgent ? BarRedUrgent : BarRed;
        }
    }

    private static void ShowWaveBreakCountdown()
    {
        if (_waveBreakCg == null || _waveBreakLabel == null)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive())
        {
            _waveBreakCg.alpha = 0f;
            return;
        }

        float remaining = ShelterAttackState.GetWaveBreakRemainingSeconds();
        int seconds = Mathf.CeilToInt(remaining);
        _waveBreakCg.alpha = HudVisibilityGate.ShouldShowGameplayHud() ? 1f : 0f;
        _waveBreakLabel.text = ShelterAttackPhrases.FormatWaveBreakCountdown(seconds);
        _waveBreakLabel.color = TimerRed;
        ApplyThreatLabel(_waveBreakThreatLabel);

        if (_waveBreakBarFillRt != null && _waveBreakBarFill != null && _waveBreakBarTotalSeconds > 0.01f)
        {
            float ratio = Mathf.Clamp01(remaining / _waveBreakBarTotalSeconds);
            _waveBreakBarFillRt.sizeDelta = new Vector2(BarMaxWidth * ratio, BarHeight);
            _waveBreakBarFill.color = BarRed;
        }
    }

    private static void ShowBanner(string text)
    {
        if (_bannerCg == null || _bannerText == null)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive() || string.IsNullOrEmpty(text))
        {
            HideBanner();
            return;
        }

        _bannerCg.alpha = HudVisibilityGate.ShouldShowGameplayHud() ? 1f : 0f;
        _bannerText.text = text;
    }

    private static void HideAttackPanel()
    {
        if (_attackCg != null)
            _attackCg.alpha = 0f;
    }

    private static void HideWaveBreakPanel()
    {
        if (_waveBreakCg != null)
            _waveBreakCg.alpha = 0f;
    }

    private static void HideBanner()
    {
        if (_bannerCg != null)
            _bannerCg.alpha = 0f;
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
        _canvasGo = new GameObject("ShelterAttackTimerHud_XUAIGNORE");
        Object.DontDestroyOnLoad(_canvasGo);

        Canvas canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9810;
        CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // No GraphicRaycaster — overlay must not steal mouse clicks (altar location map, UI).

        BuildAttackPanel(_canvasGo.transform);
        BuildWaveBreakPanel(_canvasGo.transform);
        BuildBanner(_canvasGo.transform);
        _canvasGo.SetActive(false);
    }

    private static void BuildAttackPanel(Transform parent)
    {
        GameObject root = CreateBottomPanelRoot(parent, "AttackCountdownRoot", new Vector2(0f, BottomHudOffsetY));
        _attackCg = root.AddComponent<CanvasGroup>();
        _attackCg.alpha = 0f;
        _attackCg.blocksRaycasts = false;
        _attackCg.interactable = false;

        BuildBottomBar(root.transform, "AttackBar", Vector2.zero, out _attackBarFillRt, out _attackBarFill,
            new Color(0.12f, 0.04f, 0.04f, 0.85f), BarRed);

        float labelBottom = BarHeight + LabelBarGap;
        _attackLabel = CreateBottomLabel(root.transform, "AttackLabel", new Vector2(0f, labelBottom),
            new Vector2(PanelWidth, 28f), GetTimerFontSize(), TimerRed);

        float threatBottom = labelBottom + 28f + ThreatLabelGap;
        _attackThreatLabel = CreateBottomLabel(root.transform, "AttackThreatLabel", new Vector2(0f, threatBottom),
            new Vector2(PanelWidth, 24f), GetThreatFontSize(), ThreatRed);
        _attackThreatLabel.fontStyle = FontStyle.Bold;
    }

    private static void BuildWaveBreakPanel(Transform parent)
    {
        GameObject root = CreateBottomPanelRoot(parent, "WaveBreakRoot", new Vector2(0f, BottomHudOffsetY));
        _waveBreakCg = root.AddComponent<CanvasGroup>();
        _waveBreakCg.alpha = 0f;
        _waveBreakCg.blocksRaycasts = false;
        _waveBreakCg.interactable = false;

        BuildBottomBar(root.transform, "WaveBreakBar", Vector2.zero, out _waveBreakBarFillRt, out _waveBreakBarFill,
            new Color(0.12f, 0.04f, 0.04f, 0.85f), BarRed);

        float labelBottom = BarHeight + LabelBarGap;
        _waveBreakLabel = CreateBottomLabel(root.transform, "WaveBreakLabel", new Vector2(0f, labelBottom),
            new Vector2(PanelWidth, 28f), GetTimerFontSize(), TimerRed);

        float threatBottom = labelBottom + 28f + ThreatLabelGap;
        _waveBreakThreatLabel = CreateBottomLabel(root.transform, "WaveBreakThreatLabel", new Vector2(0f, threatBottom),
            new Vector2(PanelWidth, 24f), GetThreatFontSize(), ThreatRed);
        _waveBreakThreatLabel.fontStyle = FontStyle.Bold;
    }

    private static void BuildBanner(Transform parent)
    {
        GameObject root = new GameObject("WaveBannerRoot");
        root.transform.SetParent(parent, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, BannerCenterYOffset);
        rt.sizeDelta = new Vector2(720f, 72f);

        _bannerCg = root.AddComponent<CanvasGroup>();
        _bannerCg.alpha = 0f;
        _bannerCg.blocksRaycasts = false;
        _bannerCg.interactable = false;

        _bannerText = CreateCenteredLabel(root.transform, "WaveBanner", Vector2.zero, new Vector2(720f, 72f),
            GetBannerFontSize(), TimerRed);
        _bannerText.fontStyle = FontStyle.Bold;
    }

    private static GameObject CreateBottomPanelRoot(Transform parent, string name, Vector2 offsetFromBottom)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = offsetFromBottom;
        rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        return root;
    }

    private static Text CreateBottomLabel(Transform parent, string name, Vector2 offsetFromBottom, Vector2 size, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = offsetFromBottom;
        rt.sizeDelta = size;

        return ConfigureText(go, fontSize, color);
    }

    private static Text CreateCenteredLabel(Transform parent, string name, Vector2 anchoredPos, Vector2 size, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        return ConfigureText(go, fontSize, color);
    }

    private static Text ConfigureText(GameObject go, int fontSize, Color color)
    {
        Text text = go.AddComponent<Text>();
        text.font = HellGateFontProvider.GetUiFont();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;
        return text;
    }

    private static void BuildBottomBar(
        Transform parent,
        string name,
        Vector2 offsetFromBottom,
        out RectTransform fillRt,
        out Image fillImg,
        Color bgColor,
        Color fillColor)
    {
        GameObject bg = new GameObject(name + "Bg");
        bg.transform.SetParent(parent, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.5f, 0f);
        bgRt.anchorMax = new Vector2(0.5f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = offsetFromBottom;
        bgRt.sizeDelta = new Vector2(BarMaxWidth, BarHeight);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = bgColor;

        GameObject fill = new GameObject(name + "Fill");
        fill.transform.SetParent(bg.transform, false);
        fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0.5f);
        fillRt.anchorMax = new Vector2(0f, 0.5f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = new Vector2(BarMaxWidth, BarHeight);
        fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
    }
}

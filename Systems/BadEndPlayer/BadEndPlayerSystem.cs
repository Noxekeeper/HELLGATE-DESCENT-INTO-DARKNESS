using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Systems.BadEndPlayer;

/// <summary>
/// BadEnd Player prototype: plays images + text from manifest.json with Forward/Back/Skip/AutoPlay.
/// Module path: Systems/BadEndPlayer. Content: sources/HellGate_sources/BadEndPlayer_Proto.
/// </summary>
internal static class BadEndPlayerSystem
{
    private static GameObject _canvas;
    private static RawImage _sceneImage;
    private static UnityEngine.UI.Text _sceneText;
    private static Button _btnBack;
    private static Button _btnRestart;
    private static Button _btnForward;
    private static Button _btnSkip;
    private static Button _btnAutoPlay;
    private static BadEndPlayerManifest _manifest;
    private static int _currentIndex;
    private static bool _autoPlayEnabled;
    private static MonoBehaviour _runner;
    private static Coroutine _autoCoroutine;
    private static Texture2D _currentTexture; // to destroy when changing scene
    private static bool _showingEndScreen;
    private static bool _fromBadEndTrigger; // when true, Close button quits game (as in classic BadEnd)
    private static bool _showingDiaryIntro; // when true, show diary title + intro; Next → first scene

    private const int CanvasSortOrder = 9999;
    private const float AutoPlayDefaultDelay = 5f;
    private const float TransitionFadeOutDuration = 0.28f;
    private const float TransitionFadeInDuration = 0.32f;
    
    private static Coroutine _transitionCoroutine;
    private static AudioSource _audioSource;
    private static Coroutine _audioLoadCoroutine;
    private static Button _btnMute;
    private static UnityEngine.UI.Text _btnMuteLabel;
    private static Slider _volumeSlider;
    private static Button _btnExit;
    private static Button _btnTakeVengeance;
    private static float _musicVolume = 0.7f;
    private static bool _musicMuted = false;
    private static UnityEngine.UI.Text _sceneCounterLabel;
    private static UnityEngine.UI.Text _creditsText;
    /// <summary>0 = dark semi-transparent bg + white text, 1 = opaque dark bg + white text, 2 = hidden. Cycle by Space. Global for all slides.</summary>
    private static int _textOverlayMode = 0;
    private static Image _textOverlayPanel;
    /// <summary>Current scene text split into ~4-line blocks. Index within current scene.</summary>
    private static string[] _currentSceneBlocks = new string[0];
    private static int _currentBlockIndex = 0;
    private const int LinesPerBlock = 8;

    /// <summary>
    /// Show the player if manifest and content exist. Call from config/test or from BadEnd trigger.
    /// </summary>
    public static void Show(bool fromBadEndTrigger = false)
    {
        _fromBadEndTrigger = fromBadEndTrigger;
        _manifest = BadEndPlayerLoader.LoadManifest();
        if (_manifest == null)
        {
            Plugin.Log?.LogWarning("[BadEndPlayer] Cannot show: no valid manifest (check path and manifest.json).");
            return;
        }

        _currentIndex = 0;
        _autoPlayEnabled = false;
        _showingEndScreen = false;
        _showingDiaryIntro = fromBadEndTrigger && HasDiaryIntro();

        EnsureEventSystem();
        CreateUI();
        if (_btnTakeVengeance != null) _btnTakeVengeance.gameObject.SetActive(_fromBadEndTrigger);
        _canvas.SetActive(true);
        StartBackgroundAudioIfSet();
        if (_showingDiaryIntro)
            ShowDiaryIntro();
        else
            ShowScene(_currentIndex);
        StartOrStopAutoPlay();
    }
    
    private static bool HasDiaryIntro()
    {
        if (_manifest == null) return false;
        return (!string.IsNullOrEmpty(_manifest.diaryTitle)) || (!string.IsNullOrEmpty(_manifest.diaryIntro));
    }
    
    private static void ShowDiaryIntro()
    {
        _showingEndScreen = false;
        if (_sceneCounterLabel != null) _sceneCounterLabel.gameObject.SetActive(true);
        if (_creditsText != null) _creditsText.gameObject.SetActive(false);
        if (_currentTexture != null)
        {
            UnityEngine.Object.Destroy(_currentTexture);
            _currentTexture = null;
        }
        if (_sceneImage != null) { _sceneImage.enabled = false; _sceneImage.color = Color.clear; }
        if (_textOverlayPanel != null)
        {
            var overlayRect = _textOverlayPanel.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                overlayRect.anchorMin = new Vector2(0.08f, 0.12f);
                overlayRect.anchorMax = new Vector2(0.92f, 0.88f);
                overlayRect.pivot = new Vector2(0.5f, 0.5f);
                overlayRect.anchoredPosition = Vector2.zero;
                overlayRect.sizeDelta = Vector2.zero;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
            }
        }
        string title = _manifest != null && !string.IsNullOrEmpty(_manifest.diaryTitle) ? _manifest.diaryTitle : "Raul's Diary";
        string intro = _manifest != null && !string.IsNullOrEmpty(_manifest.diaryIntro) ? _manifest.diaryIntro : "";
        if (_sceneText != null)
        {
            _sceneText.text = StripMarkdownBold(string.IsNullOrEmpty(intro) ? title : (title + "\n\n" + intro));
            _sceneText.fontSize = 24;
            _sceneText.alignment = TextAnchor.MiddleCenter;
            _sceneText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        ApplyTextOverlayMode();
        SetButtonsInteractable(true);
        if (_btnBack != null) _btnBack.interactable = false;
        if (_btnRestart != null) _btnRestart.interactable = true;
        if (_btnForward != null) _btnForward.interactable = true;
        var forwardLabel = _btnForward != null ? _btnForward.GetComponentInChildren<UnityEngine.UI.Text>() : null;
        if (forwardLabel != null) forwardLabel.text = "Next \u2192";
        if (_sceneCounterLabel != null) _sceneCounterLabel.text = "—";
        StopAutoPlay();
    }

    /// <summary>
    /// Hide and cleanup.
    /// </summary>
    public static void Hide()
    {
        if (_canvas != null)
            _canvas.SetActive(false);
        StopBackgroundAudio();
        StopTransition();
        StopAutoPlay();
        if (_currentTexture != null)
        {
            UnityEngine.Object.Destroy(_currentTexture);
            _currentTexture = null;
        }
    }

    /// <summary>
    /// Returns true if player is currently visible.
    /// </summary>
    public static bool IsVisible => _canvas != null && _canvas.activeSelf;

    internal static void OnImageClick(int mouseButton)
    {
        if (_manifest == null || _canvas == null || !_canvas.activeSelf) return;
        if (_showingEndScreen)
        {
            if (mouseButton == 1) OnBack();
            return;
        }
        if (mouseButton == 0) OnForward();
        else if (mouseButton == 1) OnBack();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("BadEndPlayerEventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }

    private static void CreateUI()
    {
        if (_canvas != null)
        {
            _canvas.SetActive(true);
            return;
        }

        _canvas = new GameObject("BadEndPlayerCanvas");
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;

        var scaler = _canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _canvas.AddComponent<GraphicRaycaster>().enabled = true;
        UnityEngine.Object.DontDestroyOnLoad(_canvas);

        // Black background (full-screen; click = Forward/Back so diary intro can continue on click)
        var bg = new GameObject("Background");
        bg.transform.SetParent(_canvas.transform, false);
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 1f);
        bgImage.raycastTarget = true;
        bg.AddComponent<ImageClickForwardBack>();
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        // Scene image (center, 1024x1024) — no click handler; clicks go to Background only to avoid duplicate handling / NRE
        var imgGo = new GameObject("SceneImage");
        imgGo.transform.SetParent(_canvas.transform, false);
        _sceneImage = imgGo.AddComponent<RawImage>();
        _sceneImage.color = Color.clear;
        _sceneImage.enabled = false;
        _sceneImage.raycastTarget = false;
        var imgRect = imgGo.GetComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.pivot = new Vector2(0.5f, 0.5f);
        imgRect.anchoredPosition = new Vector2(0f, 30f);
        imgRect.sizeDelta = new Vector2(1024f, 1024f);

        // Text overlay: dark panel + text (left/right margins, ~4 lines). Space cycles: semi-transparent bg → opaque bg → hide.
        var overlayGo = new GameObject("TextOverlay");
        overlayGo.transform.SetParent(_canvas.transform, false);
        _textOverlayPanel = overlayGo.AddComponent<Image>();
        _textOverlayPanel.color = new Color(0f, 0f, 0f, 0.5f);
        _textOverlayPanel.raycastTarget = false;
        var overlayRect = overlayGo.GetComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.05f, 0f);
        overlayRect.anchorMax = new Vector2(0.95f, 0f);
        overlayRect.pivot = new Vector2(0.5f, 0f);
        overlayRect.anchoredPosition = new Vector2(0f, 58f);
        overlayRect.sizeDelta = new Vector2(0f, 268f);
        var textGo = new GameObject("SceneText");
        textGo.transform.SetParent(overlayGo.transform, false);
        _sceneText = textGo.AddComponent<UnityEngine.UI.Text>();
        _sceneText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _sceneText.fontSize = 24;
        _sceneText.fontStyle = FontStyle.Italic;
        _sceneText.alignment = TextAnchor.MiddleCenter;
        _sceneText.color = new Color(1f, 1f, 1f, 1f);
        _sceneText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _sceneText.verticalOverflow = VerticalWrapMode.Truncate;
        _sceneText.raycastTarget = false;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -10f);

        // Scene counter (bottom-left): "3 / 23"
        var counterGo = new GameObject("SceneCounter");
        counterGo.transform.SetParent(_canvas.transform, false);
        _sceneCounterLabel = counterGo.AddComponent<UnityEngine.UI.Text>();
        _sceneCounterLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _sceneCounterLabel.fontSize = 16;
        _sceneCounterLabel.color = new Color(0.7f, 0.7f, 0.75f, 0.95f);
        _sceneCounterLabel.raycastTarget = false;
        var counterRect = counterGo.GetComponent<RectTransform>();
        counterRect.anchorMin = new Vector2(0f, 0f);
        counterRect.anchorMax = new Vector2(0f, 0f);
        counterRect.pivot = new Vector2(0f, 0f);
        counterRect.anchoredPosition = new Vector2(24f, 72f);
        counterRect.sizeDelta = new Vector2(120f, 28f);

        // Credits text (shown only on The End screen, below main text — smaller, professional)
        var creditsGo = new GameObject("CreditsText");
        creditsGo.transform.SetParent(_canvas.transform, false);
        _creditsText = creditsGo.AddComponent<UnityEngine.UI.Text>();
        _creditsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _creditsText.fontSize = 14;
        _creditsText.color = new Color(0.82f, 0.82f, 0.85f, 0.95f);
        _creditsText.alignment = TextAnchor.MiddleCenter;
        _creditsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _creditsText.verticalOverflow = VerticalWrapMode.Overflow;
        _creditsText.raycastTarget = false;
        var creditsRect = creditsGo.GetComponent<RectTransform>();
        creditsRect.anchorMin = new Vector2(0.2f, 0.18f);
        creditsRect.anchorMax = new Vector2(0.8f, 0.42f);
        creditsRect.pivot = new Vector2(0.5f, 0.5f);
        creditsRect.anchoredPosition = Vector2.zero;
        creditsRect.sizeDelta = Vector2.zero;
        creditsGo.SetActive(false);

        // Bottom bar (full width, ergonomic row)
        var barGo = new GameObject("BottomBar");
        barGo.transform.SetParent(_canvas.transform, false);
        var barImg = barGo.AddComponent<Image>();
        barImg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        var barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 52f);

        var layout = barGo.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(12, 12, 6, 6);

        const float btnW = 102f;
        _btnBack = CreateButtonInBarWithLayout(barGo.transform, "Back \u2190", btnW, OnBack);
        _btnRestart = CreateButtonInBarWithLayout(barGo.transform, "Replay [R]", btnW, OnRestart);
        _btnSkip = CreateButtonInBarWithLayout(barGo.transform, "Skip [S]", btnW, OnSkip);
        _btnAutoPlay = CreateButtonInBarWithLayout(barGo.transform, "Auto [A]", btnW, OnToggleAutoPlay);
        _btnForward = CreateButtonInBarWithLayout(barGo.transform, "Next \u2192", btnW, OnForward);
        _btnMute = CreateButtonInBarWithLayout(barGo.transform, _musicMuted ? "Sound OFF" : "Sound ON", btnW, OnToggleMute);
        _btnMuteLabel = _btnMute != null ? _btnMute.GetComponentInChildren<UnityEngine.UI.Text>() : null;
        _volumeSlider = CreateVolumeSliderInBarWithLayout(barGo.transform, 160f);
        ApplyMusicVolume();

        // Take Vengeance button — bottom right, left of Exit (only when from BadEnd trigger)
        var takeVengeanceGo = new GameObject("Btn_TakeVengeance");
        takeVengeanceGo.transform.SetParent(_canvas.transform, false);
        var tvRect = takeVengeanceGo.AddComponent<RectTransform>();
        tvRect.anchorMin = new Vector2(1f, 0f);
        tvRect.anchorMax = new Vector2(1f, 0f);
        tvRect.pivot = new Vector2(1f, 0f);
        tvRect.anchoredPosition = new Vector2(-136f, 7f);  // left of Exit
        tvRect.sizeDelta = new Vector2(120f, 38f);
        var tvImg = takeVengeanceGo.AddComponent<Image>();
        if (tvImg != null) tvImg.color = new Color(0.22f, 0.22f, 0.28f, 0.95f);
        _btnTakeVengeance = takeVengeanceGo.AddComponent<Button>();
        if (tvImg != null) _btnTakeVengeance.targetGraphic = tvImg;
        var tvColors = _btnTakeVengeance.colors;
        tvColors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        tvColors.pressedColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        _btnTakeVengeance.colors = tvColors;
        _btnTakeVengeance.onClick.AddListener(OnTakeVengeance);
        var tvTextGo = new GameObject("Text");
        tvTextGo.transform.SetParent(takeVengeanceGo.transform, false);
        var tvText = tvTextGo.AddComponent<UnityEngine.UI.Text>();
        tvText.text = "Take Vengeance";
        tvText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tvText.fontSize = 14;
        tvText.color = Color.white;
        tvText.alignment = TextAnchor.MiddleCenter;
        tvText.raycastTarget = false;
        var tvTextRect = tvTextGo.GetComponent<RectTransform>();
        tvTextRect.anchorMin = Vector2.zero;
        tvTextRect.anchorMax = Vector2.one;
        tvTextRect.sizeDelta = Vector2.zero;
        tvTextRect.anchoredPosition = Vector2.zero;
        takeVengeanceGo.SetActive(false);  // shown only when _fromBadEndTrigger

        // Exit button — bottom right corner (always visible)
        var exitGo = new GameObject("Btn_Exit");
        exitGo.transform.SetParent(_canvas.transform, false);
        var exitRect = exitGo.AddComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 0f);
        exitRect.anchorMax = new Vector2(1f, 0f);
        exitRect.pivot = new Vector2(1f, 0f);
        exitRect.anchoredPosition = new Vector2(-24f, 7f);
        exitRect.sizeDelta = new Vector2(100f, 38f);
        var exitImg = exitGo.AddComponent<Image>();
        if (exitImg != null) exitImg.color = new Color(0.22f, 0.22f, 0.28f, 0.95f);
        _btnExit = exitGo.AddComponent<Button>();
        if (exitImg != null) _btnExit.targetGraphic = exitImg;
        var exitColors = _btnExit.colors;
        exitColors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        exitColors.pressedColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        _btnExit.colors = exitColors;
        _btnExit.onClick.AddListener(OnExit);
        var exitTextGo = new GameObject("Text");
        exitTextGo.transform.SetParent(exitGo.transform, false);
        var exitText = exitTextGo.AddComponent<UnityEngine.UI.Text>();
        exitText.text = "Exit";
        exitText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        exitText.fontSize = 15;
        exitText.color = Color.white;
        exitText.alignment = TextAnchor.MiddleCenter;
        exitText.raycastTarget = false;
        var exitTextRect = exitTextGo.GetComponent<RectTransform>();
        exitTextRect.anchorMin = Vector2.zero;
        exitTextRect.anchorMax = Vector2.one;
        exitTextRect.sizeDelta = Vector2.zero;
        exitTextRect.anchoredPosition = Vector2.zero;

        // Own AudioSource for background music — not paused by game (created after BadEnd trigger)
        var audioGo = new GameObject("BadEndPlayerAudio");
        audioGo.transform.SetParent(_canvas.transform, false);
        _audioSource = audioGo.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.ignoreListenerPause = true;
        _audioSource.volume = _musicMuted ? 0f : _musicVolume;

        _runner = _canvas.AddComponent<BadEndPlayerRunnerBehaviour>();
    }
    
    private static void StartBackgroundAudioIfSet()
    {
        StopBackgroundAudio();
        if (_manifest == null || string.IsNullOrEmpty(_manifest.backgroundAudio)) return;
        if (_runner == null) return;
        _audioLoadCoroutine = _runner.StartCoroutine(LoadAndPlayBackgroundAudio());
    }
    
    private static IEnumerator LoadAndPlayBackgroundAudio()
    {
        if (_audioSource == null || _manifest == null) { _audioLoadCoroutine = null; yield break; }
        AudioClip clip = null;
        yield return BadEndPlayerLoader.LoadAudioClip(_manifest.backgroundAudio, c => clip = c);
        if (clip != null)
        {
            _audioSource.clip = clip;
            ApplyMusicVolume();
            _audioSource.Play();
        }
        else
            Plugin.Log?.LogWarning("[BadEndPlayer] Background audio clip is null, check file path and format (OGG/MP3).");
        _audioLoadCoroutine = null;
    }
    
    private static void StopBackgroundAudio()
    {
        if (_audioLoadCoroutine != null && _runner != null)
        {
            _runner.StopCoroutine(_audioLoadCoroutine);
            _audioLoadCoroutine = null;
        }
        if (_audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
        }
    }
    
    private static void OnToggleMute()
    {
        _musicMuted = !_musicMuted;
        if (_btnMuteLabel != null)
            _btnMuteLabel.text = _musicMuted ? "Sound OFF" : "Sound ON";
        ApplyMusicVolume();
    }
    
    private static void OnVolumeChanged(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
    }
    
    private static void ApplyMusicVolume()
    {
        if (_audioSource == null) return;
        _audioSource.volume = _musicMuted ? 0f : _musicVolume;
    }
    
    private static Button CreateButtonInBarWithLayout(Transform parent, string label, float preferredWidth, Action onClick)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.flexibleHeight = 1f;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.28f, 0.95f);
        var btn = go.AddComponent<Button>();
        if (img != null) btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { if (onClick != null) onClick(); });
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<UnityEngine.UI.Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 15;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        return btn;
    }

    private static Slider CreateVolumeSliderInBarWithLayout(Transform parent, float preferredWidth)
    {
        var go = new GameObject("VolumeSlider");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 1f;
        var bg = go.AddComponent<Image>();
        if (bg != null) bg.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = _musicVolume;
        slider.onValueChanged.AddListener(OnVolumeChanged);
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(go.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        if (fillImg != null) fillImg.color = new Color(0.4f, 0.4f, 0.5f, 0.8f);
        slider.fillRect = fillRect;
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaR = handleArea.AddComponent<RectTransform>();
        handleAreaR.anchorMin = Vector2.zero;
        handleAreaR.anchorMax = Vector2.one;
        handleAreaR.offsetMin = new Vector2(8f, 0f);
        handleAreaR.offsetMax = new Vector2(-8f, 0f);
        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 0f);
        slider.targetGraphic = handleImg;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    /// <summary>
    /// Show scene with optional smooth transition (fade out → swap → fade in).
    /// </summary>
    private static void ShowScene(int index)
    {
        if (_manifest == null || _manifest.scenes == null) return;
        if (index < 0 || index >= _manifest.scenes.Length)
        {
            ShowEndScreen();
            return;
        }
        StopTransition();
        if (_runner != null)
            _transitionCoroutine = _runner.StartCoroutine(TransitionToScene(index));
        else
            ShowSceneImmediate(index, 1f, 1f);
    }
    
    private static void StopTransition()
    {
        if (_transitionCoroutine != null && _runner != null)
        {
            _runner.StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }
    }
    
    private static IEnumerator TransitionToScene(int index)
    {
        if (_manifest == null || _manifest.scenes == null || index < 0 || index >= _manifest.scenes.Length)
        {
            _transitionCoroutine = null;
            yield break;
        }
        bool hadContent = (_sceneImage != null && _sceneImage.enabled && _sceneImage.color.a > 0.01f) ||
                          (_textOverlayPanel != null && _textOverlayPanel.gameObject.activeSelf);
        if (hadContent)
        {
            float t = 0f;
            while (t < TransitionFadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / TransitionFadeOutDuration);
                if (_sceneImage != null) _sceneImage.color = new Color(1f, 1f, 1f, a);
                if (_textOverlayPanel != null) _textOverlayPanel.color = new Color(0f, 0f, 0f, (_textOverlayMode == 0 ? 0.5f : 0.95f) * a);
                if (_sceneText != null) _sceneText.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
        }
        ShowSceneImmediate(index, 0f, 0f);
        float t2 = 0f;
        while (t2 < TransitionFadeInDuration)
        {
            t2 += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t2 / TransitionFadeInDuration);
            if (_sceneImage != null) _sceneImage.color = new Color(1f, 1f, 1f, a);
            if (_textOverlayPanel != null && _textOverlayPanel.gameObject.activeSelf) _textOverlayPanel.color = new Color(0f, 0f, 0f, (_textOverlayMode == 0 ? 0.5f : 0.95f) * a);
            if (_sceneText != null) _sceneText.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        if (_sceneImage != null) _sceneImage.color = Color.white;
        ApplyTextOverlayMode();
        _transitionCoroutine = null;
    }
    
    /// <summary>
    /// Set scene content immediately (optionally with alpha for transition).
    /// </summary>
    private static void ShowSceneImmediate(int index, float imageAlpha = 1f, float textAlpha = 1f)
    {
        if (_manifest == null || _manifest.scenes == null) return;
        if (index < 0 || index >= _manifest.scenes.Length) return;

        _showingEndScreen = false;
        if (_sceneCounterLabel != null) _sceneCounterLabel.gameObject.SetActive(true);
        if (_creditsText != null) _creditsText.gameObject.SetActive(false);
        if (_textOverlayPanel != null)
        {
            var overlayRect = _textOverlayPanel.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                overlayRect.anchorMin = new Vector2(0.05f, 0f);
                overlayRect.anchorMax = new Vector2(0.95f, 0f);
                overlayRect.pivot = new Vector2(0.5f, 0f);
                overlayRect.anchoredPosition = new Vector2(0f, 58f);
                overlayRect.sizeDelta = new Vector2(0f, 268f);
            }
        }
        var scene = _manifest.scenes[index];

        if (_currentTexture != null)
        {
            UnityEngine.Object.Destroy(_currentTexture);
            _currentTexture = null;
        }

        var tex = BadEndPlayerLoader.LoadImage(scene.file);
        if (_sceneImage != null)
        {
            if (tex != null)
            {
                _currentTexture = tex;
                _sceneImage.texture = tex;
                _sceneImage.enabled = true;
                _sceneImage.color = new Color(1f, 1f, 1f, imageAlpha);
            }
            else
            {
                _sceneImage.enabled = false;
                _sceneImage.color = Color.clear;
            }
        }

        _currentSceneBlocks = SplitIntoBlocks(scene.text ?? "");
        _currentBlockIndex = 0;
        if (_sceneText != null)
        {
            _sceneText.fontSize = 24;
            _sceneText.fontStyle = FontStyle.Italic;
            _sceneText.alignment = TextAnchor.MiddleCenter;
            ShowCurrentBlock();
        }

        SetButtonsInteractable(true);
        if (_btnBack != null) _btnBack.interactable = index > 0 || (_fromBadEndTrigger && HasDiaryIntro());
        if (_btnRestart != null) _btnRestart.interactable = true;
        UpdateSceneCounter(index + 1, _manifest.scenes.Length, _currentSceneBlocks?.Length ?? 0, _currentBlockIndex);

        StartOrStopAutoPlay();
    }

    private static void UpdateSceneCounter(int current, int total, int blocksTotal = 0, int blockIndex = 0)
    {
        if (_sceneCounterLabel == null) return;
        if (total <= 0) { _sceneCounterLabel.text = "—"; return; }
        if (blocksTotal > 1)
            _sceneCounterLabel.text = $"{current} · {blockIndex + 1}/{blocksTotal} / {total}";
        else
            _sceneCounterLabel.text = current + " / " + total;
    }

    private static void ApplyTextOverlayMode()
    {
        bool show = _textOverlayMode != 2;
        if (_textOverlayPanel != null)
        {
            _textOverlayPanel.gameObject.SetActive(show);
            if (show)
                _textOverlayPanel.color = _textOverlayMode == 0 ? new Color(0f, 0f, 0f, 0.5f) : new Color(0f, 0f, 0f, 0.95f);
        }
        if (_sceneText != null)
        {
            _sceneText.gameObject.SetActive(show);
            if (show) _sceneText.color = Color.white;
        }
    }

    private static void CycleTextOverlayMode()
    {
        _textOverlayMode = (_textOverlayMode + 1) % 3;
        ApplyTextOverlayMode();
    }

    /// <summary>Split text into blocks. If text contains "|||", split by it (explicit blocks). Else split by ~LinesPerBlock lines.</summary>
    private static string[] SplitIntoBlocks(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return new[] { "" };
        if (rawText.Contains("|||"))
        {
            var parts = rawText.Split(new[] { "|||" }, StringSplitOptions.None);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).DefaultIfEmpty("").ToArray();
        }
        var lines = rawText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Where(s => s != null).ToList();
        if (lines.Count == 0) return new[] { "" };
        var blocks = new List<string>();
        for (int i = 0; i < lines.Count; i += LinesPerBlock)
        {
            int count = Math.Min(LinesPerBlock, lines.Count - i);
            blocks.Add(string.Join("\n", lines.Skip(i).Take(count).ToArray()));
        }
        return blocks.ToArray();
    }

    private static string StripMarkdownBold(string s)
    {
        return string.IsNullOrEmpty(s) ? s : s.Replace("**", "");
    }

    private static void ShowCurrentBlock()
    {
        if (_sceneText == null || _currentSceneBlocks == null) return;
        int idx = Mathf.Clamp(_currentBlockIndex, 0, _currentSceneBlocks.Length - 1);
        _sceneText.text = StripMarkdownBold(_currentSceneBlocks[idx]);
        ApplyTextOverlayMode();
        if (_manifest != null && _manifest.scenes != null)
            UpdateSceneCounter(_currentIndex + 1, _manifest.scenes.Length, _currentSceneBlocks.Length, _currentBlockIndex);
    }

    private static void ShowEndScreen()
    {
        _showingEndScreen = true;
        if (_currentTexture != null)
        {
            UnityEngine.Object.Destroy(_currentTexture);
            _currentTexture = null;
        }
        if (_sceneImage != null) { _sceneImage.enabled = false; _sceneImage.color = Color.clear; }
        if (_textOverlayPanel != null)
        {
            var overlayRect = _textOverlayPanel.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                overlayRect.anchorMin = new Vector2(0.08f, 0.12f);
                overlayRect.anchorMax = new Vector2(0.92f, 0.88f);
                overlayRect.pivot = new Vector2(0.5f, 0.5f);
                overlayRect.anchoredPosition = Vector2.zero;
                overlayRect.sizeDelta = Vector2.zero;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
            }
        }
        if (_sceneText != null)
        {
            _sceneText.text = "The End";
            _sceneText.fontSize = 48;
            _sceneText.fontStyle = FontStyle.Bold;
            _sceneText.alignment = TextAnchor.MiddleCenter;
        }
        if (_creditsText != null)
        {
            _creditsText.text = !string.IsNullOrEmpty(_manifest?.credits) ? _manifest.credits : "";
            _creditsText.gameObject.SetActive(!string.IsNullOrEmpty(_creditsText.text));
        }
        if (_sceneCounterLabel != null)
            _sceneCounterLabel.gameObject.SetActive(false);
        ApplyTextOverlayMode();
        SetButtonsInteractable(false);
        if (_btnBack != null) _btnBack.interactable = true;
        if (_btnRestart != null) _btnRestart.interactable = true;
        if (_btnForward != null) _btnForward.interactable = false;
        if (_btnExit != null) _btnExit.interactable = true;
        if (_btnTakeVengeance != null) _btnTakeVengeance.interactable = true;
        StopAutoPlay();
    }

    private static void SetButtonsInteractable(bool value)
    {
        if (_btnBack != null) _btnBack.interactable = value;
        if (_btnRestart != null) _btnRestart.interactable = value;
        if (_btnForward != null) _btnForward.interactable = true;
        if (_btnSkip != null) _btnSkip.interactable = value;
        if (_btnAutoPlay != null) _btnAutoPlay.interactable = value;
        if (_btnExit != null) _btnExit.interactable = value;
        if (_btnTakeVengeance != null) _btnTakeVengeance.interactable = value;
    }

    private static void OnExit()
    {
        Hide();
        if (_fromBadEndTrigger)
        {
            try { Application.Quit(); }
            catch (Exception ex) { Plugin.Log?.LogError("[BadEndPlayer] Quit: " + ex.Message); }
        }
    }

    private static void OnTakeVengeance()
    {
        if (!_fromBadEndTrigger) return;
        Hide();
        MindBrokenBadEndSystem.HideBadEnd();
        // Vengeance Rage/MB: applied in Harmony Postfix after PlayerStatus.REstrat / REgame.pl_REstrat (Prefix ran too early and vanilla overwrote Rage).
        var ps = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetPlayerStatus();
        if (ps != null)
        {
            ps.REstrat();
            if (Plugin.badEndTakeVengeanceRespawnEnemies?.Value ?? true)
                ps.StartCoroutine(RespawnEnemiesAfterRestart());
        }
        else
        {
            Plugin.Log?.LogError("[BadEndPlayer] Take Vengeance: GameController/PlayerStatus not found");
        }
    }

    private static IEnumerator RespawnEnemiesAfterRestart()
    {
        float delay = Mathf.Max(0.5f, Plugin.badEndTakeVengeanceEnemyRespawnDelay?.Value ?? 1.2f);
        yield return new WaitForSecondsRealtime(delay);
        try
        {
            var gamemng = GameObject.FindWithTag("Gamemng");
            if (gamemng != null)
            {
                var spawnParent = gamemng.GetComponent<SpawnParent>();
                if (spawnParent != null)
                {
                    spawnParent._NotSpwan = false;
                    var enemies = GameObject.FindGameObjectsWithTag("Enemy");
                    foreach (var e in enemies)
                    {
                        if (e != null) UnityEngine.Object.Destroy(e);
                    }
                    spawnParent.fun_SpawnRE();
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BadEndPlayer] RespawnEnemiesAfterRestart: {ex.Message}");
        }
    }

    private static void OnForward()
    {
        if (_showingEndScreen) return;

        if (_showingDiaryIntro)
        {
            _showingDiaryIntro = false;
            _currentIndex = 0;
            ShowScene(_currentIndex);
            return;
        }

        if (_currentSceneBlocks != null && _currentSceneBlocks.Length > 1 && _currentBlockIndex < _currentSceneBlocks.Length - 1)
        {
            _currentBlockIndex++;
            ShowCurrentBlock();
            return;
        }
        _currentIndex++;
        _currentBlockIndex = 0;
        if (_currentIndex >= _manifest.scenes.Length)
            ShowEndScreen();
        else
            ShowScene(_currentIndex);
    }

    private static void OnBack()
    {
        if (_showingEndScreen)
        {
            _showingEndScreen = false;
            if (_manifest != null && _manifest.scenes != null && _manifest.scenes.Length > 0)
            {
                _currentIndex = _manifest.scenes.Length - 1;
                ShowScene(_currentIndex);
                _currentSceneBlocks = SplitIntoBlocks(_manifest.scenes[_currentIndex].text ?? "");
                _currentBlockIndex = _currentSceneBlocks.Length > 0 ? _currentSceneBlocks.Length - 1 : 0;
                ShowCurrentBlock();
            }
            return;
        }
        if (_showingDiaryIntro) return;
        if (_currentSceneBlocks != null && _currentSceneBlocks.Length > 1 && _currentBlockIndex > 0)
        {
            _currentBlockIndex--;
            ShowCurrentBlock();
            return;
        }
        if (_currentIndex <= 0)
        {
            if (_fromBadEndTrigger && HasDiaryIntro())
            {
                _showingDiaryIntro = true;
                ShowDiaryIntro();
            }
            return;
        }
        _currentIndex--;
        _currentSceneBlocks = SplitIntoBlocks(_manifest.scenes[_currentIndex].text ?? "");
        _currentBlockIndex = _currentSceneBlocks.Length > 0 ? _currentSceneBlocks.Length - 1 : 0;
        ShowScene(_currentIndex);
    }

    private static void OnRestart()
    {
        if (_manifest == null || _manifest.scenes == null || _manifest.scenes.Length == 0) return;
        _showingEndScreen = false;
        _showingDiaryIntro = _fromBadEndTrigger && HasDiaryIntro();
        if (_showingDiaryIntro)
        {
            ShowDiaryIntro();
            return;
        }
        _currentIndex = 0;
        ShowScene(_currentIndex);
    }

    private static void OnSkip()
    {
        if (_showingEndScreen) return;
        if (_showingDiaryIntro) { OnForward(); return; }
        OnForward();
    }

    private static void OnToggleAutoPlay()
    {
        _autoPlayEnabled = !_autoPlayEnabled;
        var label = _btnAutoPlay?.GetComponentInChildren<UnityEngine.UI.Text>();
        if (label != null)
            label.text = _autoPlayEnabled ? "Auto ON [A]" : "Auto [A]";
        StartOrStopAutoPlay();
    }

    private static void StartOrStopAutoPlay()
    {
        StopAutoPlay();
        if (!_autoPlayEnabled || _showingEndScreen || _showingDiaryIntro || _manifest == null) return;
        if (_currentIndex < 0 || _currentIndex >= _manifest.scenes.Length) return;

        float delay = _manifest.autoPlayDelay > 0 ? _manifest.autoPlayDelay : AutoPlayDefaultDelay;
        var scene = _manifest.scenes[_currentIndex];
        float duration = scene.duration > 0 ? scene.duration : delay;

        if (_runner != null)
            _autoCoroutine = _runner.StartCoroutine(AutoAdvanceAfter(duration));
    }

    private static void StopAutoPlay()
    {
        if (_autoCoroutine != null && _runner != null)
        {
            _runner.StopCoroutine(_autoCoroutine);
            _autoCoroutine = null;
        }
    }

    private static IEnumerator AutoAdvanceAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        _autoCoroutine = null;
        OnForward();
    }

    private sealed class BadEndPlayerRunnerBehaviour : MonoBehaviour
    {
        private static bool _nreLoggedOnce;
        private void Update()
        {
            try
            {
                if (_canvas == null) return;
                if (!_canvas.activeSelf || _manifest == null) return;
                if (_showingEndScreen)
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow))
                        OnBack();
                    return;
                }
                if (_showingDiaryIntro)
                {
                    if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                        OnForward();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    OnBack();
                else if (Input.GetKeyDown(KeyCode.Space))
                    CycleTextOverlayMode();
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Return))
                    OnForward();
                else if (Input.GetKeyDown(KeyCode.S))
                    OnSkip();
                else if (Input.GetKeyDown(KeyCode.A))
                    OnToggleAutoPlay();
                else if (Input.GetKeyDown(KeyCode.R))
                    OnRestart();
            }
            catch (Exception ex)
            {
                if (!_nreLoggedOnce)
                {
                    _nreLoggedOnce = true;
                    Plugin.Log?.LogError("[BadEndPlayer] Update NRE (once): " + ex.Message);
                }
            }
        }
        private void OnGUI()
        {
            if (_canvas == null || !_canvas.activeSelf || _manifest == null) return;
            var ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown) return;
            if (_showingEndScreen && ev.keyCode == KeyCode.LeftArrow)
            {
                OnBack();
                ev.Use();
            }
        }
    }

    private sealed class ImageClickForwardBack : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData == null) return;
                int button = (int)eventData.button;
                BadEndPlayerSystem.OnImageClick(button);
            }
            catch { }
        }
    }
}

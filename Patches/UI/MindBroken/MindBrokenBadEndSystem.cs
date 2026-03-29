using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using NoREroMod;
using NoREroMod.Systems.BadEndPlayer;
using DarkTonic.MasterAudio;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Bad End system triggered at 100% Mind Break and timer expiration
/// </summary>
internal static class MindBrokenBadEndSystem
{
    private static GameObject? _badEndCanvas;
    private static GameObject? _overlayObject;
    internal static GameObject? _youLoseText; // Internal for access from BadEndRunner
    internal static GameObject? _epilogueText; // Internal for access from BadEndRunner
    private static TextMeshProUGUI? _youLoseLabel; // TextMeshProUGUI for English text "YOU LOSE"
    private static UnityEngine.UI.Text? _epilogueLabel; // UnityEngine.UI.Text for Cyrillic, as in Corruption
    private static MonoBehaviour? _coroutineRunner;
    private static bool _isBadEndActive = false;
    private static bool _countdownActive = false;
    
    private static List<string> _epilogues = new List<string>();
    private static bool _epiloguesLoaded = false;
    
    private static GameObject? _reviveButton;
    private static Button? _reviveButtonComponent;
    
    /// <summary>
    /// True while BadEnd is active (lose screen or BadEnd Player visible from trigger). Use to stop QTE and other gameplay.
    /// </summary>
    internal static bool IsBadEndActive => _isBadEndActive;
    
    /// <summary>
    /// Start countdown
    /// </summary>
    internal static void StartCountdown()
    {
        // Always restart timer, even if already active
        _countdownActive = true;
        
        // Create coroutine runner object if missing
        if (_coroutineRunner == null)
        {
            GameObject runnerObj = new GameObject("MindBrokenBadEndRunner_XUAIGNORE");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _coroutineRunner = runnerObj.AddComponent<BadEndRunner>();
        }
        
        if (_coroutineRunner != null)
        {
            BadEndRunner runner = _coroutineRunner as BadEndRunner;
            if (runner != null)
            {
                // Stop old coroutine if exists
                runner.StopAllCoroutines();
                // Start new one
                runner.StartCountdownCoroutine();
            }
            else
            {
                Plugin.Log?.LogError("[MindBroken BadEnd] _coroutineRunner is not BadEndRunner!");
            }
        }
        else
        {
            Plugin.Log?.LogError("[MindBroken BadEnd] _coroutineRunner is null after creation!");
        }
    }
    
    /// <summary>
    /// Stop countdown
    /// </summary>
    internal static void StopCountdown()
    {
        _countdownActive = false;
    }
    
    // Lose screen shown before BadEnd Player (YOU LOSE + epilogue + Click to continue)
    private static GameObject _badEndPlayerLoseCanvas;
    private static string _badEndPlayerLoseEpilogue = "Aradia was lost in her lust; her body no longer belonged to her, her mind surrendered to violent orgasms — her consciousness went out forever.";
    
    // Saved camera state to restore on HideBadEnd (all cameras disabled to hide x-ray/ERO overlay)
    private static List<Camera> _savedBadEndCameras = new List<Camera>();
    private static List<int> _savedBadEndCullingMasks = new List<int>();
    private static List<bool> _savedBadEndCameraEnabled = new List<bool>();
    // Saved canvases/UI disabled by ClearScreenBeforeBadEnd — restore on HideBadEnd
    private static List<GameObject> _savedDisabledCanvases = new List<GameObject>();
    private static List<GameObject> _savedDisabledUIElements = new List<GameObject>();
    
    /// <summary>
    /// Trigger Bad End (timer reached 0)
    /// </summary>
    internal static void TriggerBadEnd()
    {
        if (_isBadEndActive) return;
        
        _isBadEndActive = true;
        _countdownActive = false;
        
        // Deactivate black background FIRST: stops CUM black screen and MindBroken tick (prevents MB growth and freezes)
        try
        {
            NoREroMod.Systems.Effects.HSceneBlackBackgroundSystem.Deactivate();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[MindBroken BadEnd] HSceneBlackBackgroundSystem.Deactivate: " + ex.Message);
        }
        
        // Full game stop immediately
        Time.timeScale = 0f;
        
        LoadEpilogues();
        
        // Clear screen - hide all UI elements before showing BadEnd
        ClearScreenBeforeBadEnd();
        
        // Pause game via Pauser (can NRE if targets contain destroyed objects — game typo: OnDestory)
        try { Pauser.Pause(); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[MindBroken BadEnd] Pauser.Pause skipped: " + ex.Message);
        }
        
        // Stop all audio (Unity AudioSource)
        try
        {
            AudioSource[] allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            foreach (AudioSource audio in allAudioSources)
            {
                if (audio != null && audio.isPlaying)
                {
                    audio.Pause();
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore
        }
        
        // Game's single "time stop": same as opening the menu — operation = false, MENU.enabled = true, Time.timeScale = 0, PauseBus.
        // This makes the game believe the menu is open so all systems that respect it will pause.
        SetGameMenuStateForBadEnd(open: true);
        
        // Stop MasterAudio buses (EroVoice = moans, EroSE = H-sounds)
        StopMasterAudioForBadEnd();
        
        // Hide game view: disable main camera so only our UI is visible
        DisableGameCameraForBadEnd();
        
        // If BadEnd Player is enabled and has content: show lose screen first (YOU LOSE + epilogue + Click to continue), then on click open player
        if (Plugin.enableBadEndPlayer != null && Plugin.enableBadEndPlayer.Value)
        {
            // For BadEnd Player, optionally select a random pack under sources/HellGate_sources/BadEndPlayer/[PackName]
            BadEndPlayerLoader.SelectRandomPackIfAny();
            var manifest = BadEndPlayerLoader.LoadManifest();
            if (manifest != null && manifest.scenes != null && manifest.scenes.Length > 0)
            {
                if (_coroutineRunner == null)
                {
                    GameObject runnerObj = new GameObject("MindBrokenBadEndRunner_XUAIGNORE");
                    UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                    _coroutineRunner = runnerObj.AddComponent<BadEndRunner>();
                }
                if (_coroutineRunner is BadEndRunner badEndRunner)
                {
                    badEndRunner.StopAllCoroutines();
                    badEndRunner.StartCoroutine(badEndRunner.KeepGamePausedWhileBadEnd());
                    badEndRunner.StartBadEndPlayerLoseScreenThenPlayer();
                }
                return;
            }
        }
        
        // Classic Bad End: YOU LOSE + epilogue (no BadEnd Player)
        // Create coroutine runner object if missing (for font application and text delay)
        if (_coroutineRunner == null)
        {
            GameObject runnerObj = new GameObject("MindBrokenBadEndRunner_XUAIGNORE");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _coroutineRunner = runnerObj.AddComponent<BadEndRunner>();
        }
        
        // Create UI for Bad End (show overlay immediately, texts hidden)
        CreateBadEndUI();
        
        // Show texts with shadow animation
        if (_coroutineRunner != null && _coroutineRunner is BadEndRunner runner)
        {
            runner.StartCoroutine(runner.KeepGamePausedWhileBadEnd());
            runner.StartCoroutine(runner.ShowTextsWithAnimation());
        }
        else
        {
            // Fallback: show texts immediately
            if (_youLoseText != null) _youLoseText.SetActive(true);
            if (_epilogueText != null) _epilogueText.SetActive(true);
        }
        
        // Stop game (pause)
        Time.timeScale = 0f;
    }
    
    /// <summary>
    /// Create and show the lose screen (YOU LOSE + epilogue + Click to continue) before opening BadEnd Player. On click anywhere, hide and open player.
    /// </summary>
    private static void EnsureBadEndPlayerLoseScreen()
    {
        if (_badEndPlayerLoseCanvas != null) return;
        
        _badEndPlayerLoseCanvas = new GameObject("BadEndPlayerLoseCanvas_XUAIGNORE");
        var canvas = _badEndPlayerLoseCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10002;
        var scaler = _badEndPlayerLoseCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _badEndPlayerLoseCanvas.AddComponent<GraphicRaycaster>().enabled = true;
        UnityEngine.Object.DontDestroyOnLoad(_badEndPlayerLoseCanvas);
        
        // Black overlay (starts transparent; coroutine fades in)
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(_badEndPlayerLoseCanvas.transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0f);
        var overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;
        overlay.name = "BadEndPlayerLoseOverlay";
        
        // YOU LOSE — use UI.Text to avoid TextMeshPro null/layout NRE spam when screen appears
        var youLoseGo = new GameObject("YouLose");
        youLoseGo.transform.SetParent(_badEndPlayerLoseCanvas.transform, false);
        var youLoseTxt = youLoseGo.AddComponent<UnityEngine.UI.Text>();
        youLoseTxt.text = "YOU LOSE";
        youLoseTxt.fontSize = 52;
        youLoseTxt.alignment = TextAnchor.MiddleCenter;
        youLoseTxt.fontStyle = FontStyle.Bold;
        youLoseTxt.color = new Color(1f, 1f, 1f, 0f);
        youLoseTxt.raycastTarget = false;
        youLoseTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var youLoseRect = youLoseGo.GetComponent<RectTransform>();
        youLoseRect.anchorMin = new Vector2(0.5f, 0.5f);
        youLoseRect.anchorMax = new Vector2(0.5f, 0.5f);
        youLoseRect.pivot = new Vector2(0.5f, 0.5f);
        youLoseRect.anchoredPosition = new Vector2(0f, 280f);
        youLoseRect.sizeDelta = new Vector2(800f, 80f);
        youLoseGo.SetActive(false);
        
        // Epilogue text (harsher)
        var epilogueGo = new GameObject("Epilogue");
        epilogueGo.transform.SetParent(_badEndPlayerLoseCanvas.transform, false);
        var epilogueTxt = epilogueGo.AddComponent<UnityEngine.UI.Text>();
        epilogueTxt.text = _badEndPlayerLoseEpilogue;
        epilogueTxt.fontSize = 22;
        epilogueTxt.alignment = TextAnchor.MiddleCenter;
        epilogueTxt.fontStyle = FontStyle.Bold;
        epilogueTxt.color = new Color(0.92f, 0.92f, 0.92f, 0f);
        epilogueTxt.raycastTarget = false;
        epilogueTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        epilogueTxt.verticalOverflow = VerticalWrapMode.Truncate;
        epilogueTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var epilogueRect = epilogueGo.GetComponent<RectTransform>();
        epilogueRect.anchorMin = new Vector2(0.5f, 0.5f);
        epilogueRect.anchorMax = new Vector2(0.5f, 0.5f);
        epilogueRect.pivot = new Vector2(0.5f, 0.5f);
        epilogueRect.anchoredPosition = new Vector2(0f, 80f);
        epilogueRect.sizeDelta = new Vector2(900f, 320f);
        epilogueGo.SetActive(false);
        
        // "Click to continue..." centered below epilogue text
        var clickGo = new GameObject("ClickToContinue");
        clickGo.transform.SetParent(_badEndPlayerLoseCanvas.transform, false);
        var clickTxt = clickGo.AddComponent<UnityEngine.UI.Text>();
        clickTxt.text = "Click to continue...";
        clickTxt.fontSize = 20;
        clickTxt.alignment = TextAnchor.MiddleCenter;
        clickTxt.color = new Color(0.75f, 0.75f, 0.75f, 0f);
        clickTxt.raycastTarget = false;
        clickTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var clickRect = clickGo.GetComponent<RectTransform>();
        clickRect.anchorMin = new Vector2(0.5f, 0.5f);
        clickRect.anchorMax = new Vector2(0.5f, 0.5f);
        clickRect.pivot = new Vector2(0.5f, 0.5f);
        clickRect.anchoredPosition = new Vector2(0f, -180f);
        clickRect.sizeDelta = new Vector2(320f, 40f);
        clickGo.SetActive(false);
        
        // Full-screen invisible button (catch click anywhere)
        var btnGo = new GameObject("ClickAnywhere");
        btnGo.transform.SetParent(_badEndPlayerLoseCanvas.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0.01f);
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.one;
        btnRect.sizeDelta = Vector2.zero;
        btnRect.anchoredPosition = Vector2.zero;
        var btn = btnGo.AddComponent<Button>();
        btn.interactable = false;
        btn.onClick.AddListener(OnBadEndPlayerLoseScreenClicked);
        
        _badEndPlayerLoseCanvas.SetActive(false);
    }
    
    /// <summary>
    /// Called when user clicks on the lose screen → hide it and open BadEnd Player.
    /// </summary>
    private static void OnBadEndPlayerLoseScreenClicked()
    {
        if (_badEndPlayerLoseCanvas != null)
            _badEndPlayerLoseCanvas.SetActive(false);
        BadEndPlayerSystem.Show(fromBadEndTrigger: true);
    }
    
    /// <summary>
    /// Set game into "menu open" state (operation = false, MENU.enabled = true, PauseBus) or restore (operation = true, MENU.enabled = false, UnpauseBus).
    /// This is the game's single "time stop" — same as when the player opens the menu.
    /// </summary>
    private static void SetGameMenuStateForBadEnd(bool open)
    {
        try
        {
            PlayerStatus ps = UnityEngine.Object.FindObjectOfType<PlayerStatus>();
            if (ps == null) return;
            if (open)
                Time.timeScale = 0f;
            var opField = AccessTools.Field(typeof(PlayerStatus), "operation");
            if (opField != null) opField.SetValue(ps, open ? false : true);
            var menuField = AccessTools.Field(typeof(PlayerStatus), "MENU");
            if (menuField != null)
            {
                object menuVal = menuField.GetValue(ps);
                Canvas menuCanvas = menuVal as Canvas;
                if (menuCanvas != null)
                    menuCanvas.enabled = open;
            }
            if (!open)
            {
                var menuContorollField = AccessTools.Field(typeof(PlayerStatus), "MENUcontoroll");
                if (menuContorollField != null)
                {
                    object cgVal = menuContorollField.GetValue(ps);
                    CanvasGroup cg = cgVal as CanvasGroup;
                    if (cg != null) cg.interactable = false;
                }
                Time.timeScale = 1f;
            }
            if (open)
            {
                try { MasterAudio.PauseBus("EroVoice"); } catch { }
                try { MasterAudio.PauseBus("EroSE"); } catch { }
                try { MasterAudio.PauseBus("Voice"); } catch { }
                try { MasterAudio.PauseBus("Sound"); } catch { }
            }
            else
            {
                try { MasterAudio.UnpauseBus("EroVoice"); } catch { }
                try { MasterAudio.UnpauseBus("EroSE"); } catch { }
                try { MasterAudio.UnpauseBus("Voice"); } catch { }
                try { MasterAudio.UnpauseBus("Sound"); } catch { }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[MindBroken BadEnd] SetGameMenuState: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Stop MasterAudio EroVoice/EroSE (PauseBus is done in SetGameMenuState; keep StopBus as backup).
    /// </summary>
    private static void StopMasterAudioForBadEnd()
    {
        try
        {
            MasterAudio.StopBus("EroVoice");
            MasterAudio.StopBus("EroSE");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[MindBroken BadEnd] MasterAudio stop: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Disable ALL cameras so game world, ERO overlay and x-ray/cum clip are not visible; only our UI shows.
    /// </summary>
    private static void DisableGameCameraForBadEnd()
    {
        try
        {
            _savedBadEndCameras.Clear();
            _savedBadEndCullingMasks.Clear();
            _savedBadEndCameraEnabled.Clear();
            Camera[] all = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < all.Length; i++)
            {
                Camera c = all[i];
                if (c == null || c.gameObject == null) continue;
                _savedBadEndCameras.Add(c);
                _savedBadEndCullingMasks.Add(c.cullingMask);
                _savedBadEndCameraEnabled.Add(c.enabled);
                c.cullingMask = 0;
                c.enabled = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[MindBroken BadEnd] Disable cameras: " + ex.Message);
        }
    }

    /// <summary>
    /// Restore all cameras when leaving BadEnd (e.g. revive).
    /// </summary>
    private static void RestoreGameCameraAfterBadEnd()
    {
        try
        {
            for (int i = 0; i < _savedBadEndCameras.Count; i++)
            {
                Camera c = _savedBadEndCameras[i];
                if (c == null) continue;
                c.enabled = i < _savedBadEndCameraEnabled.Count ? _savedBadEndCameraEnabled[i] : true;
                if (i < _savedBadEndCullingMasks.Count)
                    c.cullingMask = _savedBadEndCullingMasks[i];
            }
            _savedBadEndCameras.Clear();
            _savedBadEndCullingMasks.Clear();
            _savedBadEndCameraEnabled.Clear();
            // Fallback: force-enable any camera still disabled (prevents gray screen)
            Camera[] all = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < all.Length; i++)
            {
                Camera c = all[i];
                if (c == null || c.gameObject == null) continue;
                if (!c.enabled) c.enabled = true;
                if (c.cullingMask == 0) c.cullingMask = -1;
            }
        }
        catch (Exception ex)
        {
            // Ignore
        }
    }
    
    /// <summary>
    /// Restore canvases and UI elements that were disabled by ClearScreenBeforeBadEnd.
    /// </summary>
    private static void RestoreScreenAfterBadEnd()
    {
        try
        {
            foreach (GameObject go in _savedDisabledCanvases)
            {
                if (go != null) go.SetActive(true);
            }
            _savedDisabledCanvases.Clear();
            foreach (GameObject go in _savedDisabledUIElements)
            {
                if (go != null) go.SetActive(true);
            }
            _savedDisabledUIElements.Clear();
        }
        catch (Exception ex)
        {
            // Ignore
        }
    }
    
    /// <summary>
    /// Clear screen before showing BadEnd - hide all UI elements (including QTE, H-scene UI)
    /// </summary>
    private static void ClearScreenBeforeBadEnd()
    {
        try
        {
            _savedDisabledCanvases.Clear();
            _savedDisabledUIElements.Clear();
            // Hide all Canvas elements (except our BadEnd / BadEndPlayer canvases)
            Canvas[] allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in allCanvases)
            {
                if (canvas == null || canvas.gameObject == null) continue;
                string name = canvas.gameObject.name ?? "";
                if (name == "MindBrokenBadEndCanvas" || name == "MindBrokenBadEndCanvas_XUAIGNORE" ||
                    name == "EpilogueCanvas" || name == "EpilogueCanvas_XUAIGNORE" ||
                    name == "BadEndPlayerLoseCanvas_XUAIGNORE" || name == "BadEndPlayerCanvas")
                    continue;
                // Do not disable mod canvases (UnityExplorer etc.) to avoid NullReferenceException spam in log
                if (name.IndexOf("Explorer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("UnityExplorer", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (canvas.gameObject.activeSelf)
                {
                    _savedDisabledCanvases.Add(canvas.gameObject);
                    canvas.gameObject.SetActive(false);
                }
            }
            
            // Hide all UI elements
            UnityEngine.UI.Text[] allTexts = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>();
            foreach (var text in allTexts)
            {
                if (text != null && text.gameObject != null && 
                    text.gameObject.name != "YouLoseText" && 
                    text.gameObject.name != "EpilogueText" &&
                    text.gameObject.activeSelf)
                {
                    _savedDisabledUIElements.Add(text.gameObject);
                    text.gameObject.SetActive(false);
                }
            }
            
            TextMeshProUGUI[] allTMPTexts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>();
            foreach (var tmpText in allTMPTexts)
            {
                if (tmpText != null && tmpText.gameObject != null && 
                    tmpText.gameObject.name != "YouLoseText" && 
                    tmpText.gameObject.name != "EpilogueText" &&
                    tmpText.gameObject.activeSelf)
                {
                    _savedDisabledUIElements.Add(tmpText.gameObject);
                    tmpText.gameObject.SetActive(false);
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore
        }
    }
    
    /// <summary>
    /// Hide Bad End UI (on revive)
    /// </summary>
    internal static void HideBadEnd()
    {
        if (_badEndCanvas != null)
        {
            _badEndCanvas.SetActive(false);
        }
        if (_badEndPlayerLoseCanvas != null)
        {
            _badEndPlayerLoseCanvas.SetActive(false);
        }
        _isBadEndActive = false;
        
        SetGameMenuStateForBadEnd(open: false);
        RestoreGameCameraAfterBadEnd();
        RestoreScreenAfterBadEnd();
        
        // Resume game via Pauser
        try
        {
            Time.timeScale = 1f;
            Pauser.Resume();
        }
        catch (Exception ex)
        {
            // Ignore
        }
        
        try { AudioListener.pause = false; } catch { }
        // Resume audio
        try
        {
            AudioSource[] allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            foreach (AudioSource audio in allAudioSources)
            {
                if (audio != null)
                    audio.UnPause();
            }
        }
        catch (Exception ex) { }
        // Hide fade overlay (UIeffect) — prevents gray/black screen covering the game
        try
        {
            GameObject uiEffect = GameObject.Find("UIeffect");
            if (uiEffect != null)
            {
                var fade = uiEffect.GetComponent<fadein_out>();
                if (fade != null) fade.off();
            }
        }
        catch (Exception ex) { }
    }
    
    /// <summary>
    /// Reload epilogues with new language
    /// Called after language selection on splash screen
    /// </summary>
    internal static void ReloadEpilogues()
    {
        _epiloguesLoaded = false;
        _epilogues.Clear();
        LoadEpilogues();
    }
    
    private static void LoadEpilogues()
    {
        if (_epiloguesLoaded) return;
        
        try
        {
            string filePath = GetEpiloguesPath();
            if (!File.Exists(filePath))
            {
                _epilogues.Add("Your mind is forever broken. You became a toy for all enemies.");
                _epiloguesLoaded = true;
                return;
            }
            
            string json = File.ReadAllText(filePath);
            ParseEpilogues(json);
            _epiloguesLoaded = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MindBroken BadEnd] Error loading epilogues: {ex.Message}");
            _epilogues.Add("Your mind is forever broken.");
            _epiloguesLoaded = true;
        }
    }
    
    /// <summary>
    /// Parse epilogues from JSON
    /// </summary>
    private static void ParseEpilogues(string json)
    {
        _epilogues.Clear();
        
        // Find "mind_break_epilogues" array
        string pattern = "\"mind_break_epilogues\"\\s*:\\s*\\[([^\\]]+)\\]";
        Match match = Regex.Match(json, pattern, RegexOptions.Singleline);
        
        if (match.Success)
        {
            string content = match.Groups[1].Value;
            // Extract strings between quotes
            string stringPattern = "\"([^\"]+)\"";
            var matches = Regex.Matches(content, stringPattern);
            
            foreach (Match m in matches)
            {
                string epilogue = m.Groups[1].Value;
                // Replace \n with actual line breaks
                epilogue = epilogue.Replace("\\n", "\n");
                _epilogues.Add(epilogue);
            }
        }
        
        if (_epilogues.Count == 0)
        {
            _epilogues.Add("Your mind is forever broken.");
        }
    }
    
    /// <summary>
    /// Create UI for Bad End
    /// </summary>
    private static void CreateBadEndUI()
    {
        if (_badEndCanvas != null)
        {
            _badEndCanvas.SetActive(true);
            UpdateEpilogueText();
            // Hide texts on re-show (they will appear after 2 seconds)
            if (_youLoseText != null) _youLoseText.SetActive(false);
            if (_epilogueText != null) _epilogueText.SetActive(false);
            return;
        }
        
        // Create Canvas
        _badEndCanvas = new GameObject("MindBrokenBadEndCanvas_XUAIGNORE");
        Canvas canvas = _badEndCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // High priority
        
        CanvasScaler scaler = _badEndCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Enable GraphicRaycaster for button functionality
        GraphicRaycaster raycaster = _badEndCanvas.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
        
        // Ensure EventSystem exists for UI event handling
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem_XUAIGNORE");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(eventSystemObj);
        }
        
        UnityEngine.Object.DontDestroyOnLoad(_badEndCanvas);
        
        // Screen overlay - start transparent (will fade in)
        _overlayObject = new GameObject("BadEndOverlay_XUAIGNORE");
        _overlayObject.transform.SetParent(_badEndCanvas.transform, false);
        Image overlay = _overlayObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f); // Start transparent (smooth fade)
        
        RectTransform overlayRect = _overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;
        
        // "YOU LOSE" text - use TextMeshProUGUI as in MindBrokenSystem
        _youLoseText = new GameObject("YouLoseText_XUAIGNORE");
        _youLoseText.transform.SetParent(_badEndCanvas.transform, false);
        _youLoseLabel = _youLoseText.AddComponent<TextMeshProUGUI>();
        _youLoseLabel.text = "YOU LOSE";
        _youLoseLabel.fontSize = 52f;
        _youLoseLabel.alignment = TextAlignmentOptions.Center;
        _youLoseLabel.fontStyle = FontStyles.Bold;
        _youLoseLabel.color = new Color(1f, 1f, 1f, 0f); // Start transparent (for shadow animation)
        _youLoseLabel.raycastTarget = false;
        _youLoseLabel.enableWordWrapping = false;
        
        // Use defaultFontAsset (supports Cyrillic, as Arial in Corruption)
        if (TMP_Settings.defaultFontAsset != null)
        {
            _youLoseLabel.font = TMP_Settings.defaultFontAsset;
        }
        
        RectTransform youLoseRect = _youLoseText.GetComponent<RectTransform>();
        youLoseRect.anchorMin = new Vector2(0.5f, 0.5f);
        youLoseRect.anchorMax = new Vector2(0.5f, 0.5f);
        youLoseRect.pivot = new Vector2(0.5f, 0.5f);
        youLoseRect.anchoredPosition = new Vector2(0f, 300f);
        youLoseRect.sizeDelta = new Vector2(800f, 100f);
        
        // Hide text initially (will appear with animation)
        _youLoseText.SetActive(false);
        
        // Create invisible canvas for epilogue text (600 height, 800 width)
        GameObject epilogueCanvas = new GameObject("EpilogueCanvas_XUAIGNORE");
        epilogueCanvas.transform.SetParent(_badEndCanvas.transform, false);
        Canvas epilogueCanvasComponent = epilogueCanvas.AddComponent<Canvas>();
        epilogueCanvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        epilogueCanvasComponent.sortingOrder = 10001; // Above main canvas
        
        CanvasScaler epilogueScaler = epilogueCanvas.AddComponent<CanvasScaler>();
        epilogueScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        epilogueScaler.referenceResolution = new Vector2(1920, 1080);
        
        epilogueCanvas.AddComponent<GraphicRaycaster>().enabled = false;
        
        // Epilogue - use UnityEngine.UI.Text as in Corruption (for proper Cyrillic display)
        _epilogueText = new GameObject("EpilogueText_XUAIGNORE");
        _epilogueText.transform.SetParent(epilogueCanvas.transform, false);
        _epilogueLabel = _epilogueText.AddComponent<UnityEngine.UI.Text>();
        _epilogueLabel.fontSize = 20;
        _epilogueLabel.alignment = TextAnchor.MiddleCenter;
        _epilogueLabel.fontStyle = FontStyle.Bold;
        _epilogueLabel.color = new Color(0.9f, 0.9f, 0.9f, 0f); // Start transparent (for shadow animation)
        _epilogueLabel.raycastTarget = false;
        _epilogueLabel.horizontalOverflow = HorizontalWrapMode.Wrap; // Text wrapping
        _epilogueLabel.verticalOverflow = VerticalWrapMode.Truncate; // Truncate by height
        _epilogueLabel.resizeTextForBestFit = false;
        _epilogueLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // As in Corruption system
        
        RectTransform epilogueRect = _epilogueText.GetComponent<RectTransform>();
        epilogueRect.anchorMin = new Vector2(0.5f, 0.5f);
        epilogueRect.anchorMax = new Vector2(0.5f, 0.5f);
        epilogueRect.pivot = new Vector2(0.5f, 0.5f);
        epilogueRect.anchoredPosition = new Vector2(0f, 50f);
        epilogueRect.sizeDelta = new Vector2(800f, 600f); // 800 width, 600 height
        
        // Hide text initially (will appear 2 seconds after YOU LOSE with animation)
        _epilogueText.SetActive(false);
        
        UpdateEpilogueText();
        
        // Create revive button
        _reviveButton = new GameObject("ReviveButton_XUAIGNORE");
        _reviveButton.transform.SetParent(_badEndCanvas.transform, false);
        
        // Add button components
        Image buttonImage = _reviveButton.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark gray background
        
        _reviveButtonComponent = _reviveButton.AddComponent<Button>();
        
        // Ensure button is interactive
        _reviveButtonComponent.interactable = true;
        _reviveButtonComponent.enabled = true;
        
        // Setup RectTransform
        RectTransform buttonRect = _reviveButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.1f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(200f, 50f);
        
        // Add text to button
        GameObject buttonTextObj = new GameObject("ButtonText_XUAIGNORE");
        buttonTextObj.transform.SetParent(_reviveButton.transform, false);
        UnityEngine.UI.Text buttonText = buttonTextObj.AddComponent<UnityEngine.UI.Text>();
        buttonText.text = "The End";
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 24;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.raycastTarget = false; // Text should not block button clicks
        
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;
        buttonTextRect.anchoredPosition = Vector2.zero;
        
        // Setup onClick
        _reviveButtonComponent.onClick.AddListener(OnReviveButtonClicked);
        
        // Hide button initially (will appear after texts)
        _reviveButton.SetActive(false);
    }
    
    /// <summary>
    /// Update epilogue text (random selection)
    /// </summary>
    private static void UpdateEpilogueText()
    {
        if (_epilogueLabel == null || _epilogues.Count == 0) return;
        
        // Select random epilogue
        int index = UnityEngine.Random.Range(0, _epilogues.Count);
        _epilogueLabel.text = _epilogues[index];
    }
    
    /// <summary>
    /// Refresh fonts for all labels (called when showing Bad End)
    /// </summary>
    internal static void RefreshFonts()
    {
        // Font already applied on label creation (as in Corruption system)
    }
    
    /// <summary>
    /// Get path to epilogues file
    /// </summary>
    private static string GetEpiloguesPath()
    {
        try
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string bepInEx = Path.Combine(basePath, "BepInEx");
            string plugins = Path.Combine(bepInEx, "plugins");
            string hellGateJson = Path.Combine(plugins, "HellGateJson");
            
            if (Directory.Exists(hellGateJson))
            {
                // Get language from config, fallback to "EN" if not set
                string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
                if (string.IsNullOrEmpty(languageCode))
                {
                    languageCode = "EN"; // Fallback
                }
                
                // Add language folder
                string langPath = Path.Combine(hellGateJson, languageCode);
                
                // Check existence, if not - fallback to EN
                if (Directory.Exists(langPath))
                {
                    return Path.Combine(langPath, "MindBrokenBadEnd.json");
                }
                
                // Fallback to EN if selected language not found
                string enPath = Path.Combine(hellGateJson, "EN");
                if (Directory.Exists(enPath))
                {
                    return Path.Combine(enPath, "MindBrokenBadEnd.json");
                }
                
                // If even EN not found, return from root folder (for backward compatibility)
                return Path.Combine(hellGateJson, "MindBrokenBadEnd.json");
            }
        }
        catch { }
        
        // Fallback
        string basePathFallback = Path.Combine(Application.dataPath, "..");
        string bepInExFallback = Path.Combine(basePathFallback, "BepInEx");
        string pluginsFallback = Path.Combine(bepInExFallback, "plugins");
        string hellGateJsonFallback = Path.Combine(pluginsFallback, "HellGateJson");
        
        // Try to add language folder in fallback too
        try
        {
            string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "EN";
            }
            string langPathFallback = Path.Combine(hellGateJsonFallback, languageCode);
            if (Directory.Exists(langPathFallback))
            {
                return Path.Combine(langPathFallback, "MindBrokenBadEnd.json");
            }
            string enPathFallback = Path.Combine(hellGateJsonFallback, "EN");
            if (Directory.Exists(enPathFallback))
            {
                return Path.Combine(enPathFallback, "MindBrokenBadEnd.json");
            }
        }
        catch { }
        
        return Path.Combine(hellGateJsonFallback, "MindBrokenBadEnd.json");
    }
    
    /// <summary>
    /// Apply font to single TextMeshProUGUI label (only for "YOU LOSE")
    /// </summary>
    private static void ApplyFontToLabel(TextMeshProUGUI? label)
    {
        if (label == null) return;
        
        try
        {
            UImng uimg = UnityEngine.Object.FindObjectOfType<UImng>();
            if (uimg != null)
            {
                var lowTxtField = AccessTools.Field(typeof(UImng), "Lowtxt");
                if (lowTxtField?.GetValue(uimg) is TextMeshProUGUI template && template != null)
                {
                    if (template.font != null)
                    {
                        label.font = template.font;
                    }
                    if (template.fontSharedMaterial != null)
                    {
                        label.fontSharedMaterial = template.fontSharedMaterial;
                    }
                }
                else
                {
                    if (TMP_Settings.defaultFontAsset != null)
                    {
                        label.font = TMP_Settings.defaultFontAsset;
                    }
                }
            }
            else
            {
                if (TMP_Settings.defaultFontAsset != null)
                {
                    label.font = TMP_Settings.defaultFontAsset;
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore
        }
    }
    
    /// <summary>
    /// Apply font to labels (only for TextMeshProUGUI - "YOU LOSE")
    /// Epilogue uses UnityEngine.UI.Text with Arial, font already applied on creation
    /// </summary>
    private static void ApplyFontToLabels()
    {
        if (_youLoseLabel == null) return;
        
        try
        {
            UImng uimg = UnityEngine.Object.FindObjectOfType<UImng>();
            if (uimg != null)
            {
                var lowTxtField = AccessTools.Field(typeof(UImng), "Lowtxt");
                if (lowTxtField?.GetValue(uimg) is TextMeshProUGUI template && template != null)
                {
                    if (template.font != null && _youLoseLabel != null)
                    {
                        _youLoseLabel.font = template.font;
                    }
                    if (template.fontSharedMaterial != null && _youLoseLabel != null)
                    {
                        _youLoseLabel.fontSharedMaterial = template.fontSharedMaterial;
                    }
                }
                else
                {
                    if (TMP_Settings.defaultFontAsset != null && _youLoseLabel != null)
                    {
                        _youLoseLabel.font = TMP_Settings.defaultFontAsset;
                    }
                }
            }
            else
            {
                if (TMP_Settings.defaultFontAsset != null && _youLoseLabel != null)
                {
                    _youLoseLabel.font = TMP_Settings.defaultFontAsset;
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore
        }
    }
    
    /// <summary>
    /// MonoBehaviour for coroutines
    /// </summary>
    private class BadEndRunner : MonoBehaviour
    {
        private playercon? _lastPlayer;
        internal static bool ApplicationHasFocus = true;

        private void OnApplicationFocus(bool hasFocus)
        {
            ApplicationHasFocus = hasFocus;
        }

        /// <summary>Re-apply pause and MasterAudio stop. When window has no focus (Alt+Tab) skip heavy work to avoid NRE spam.</summary>
        public IEnumerator KeepGamePausedWhileBadEnd()
        {
            int tick = 0;
            while (MindBrokenBadEndSystem.IsBadEndActive)
            {
                if (!ApplicationHasFocus)
                {
                    tick++;
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }
                try
                {
                    MasterAudio.StopBus("EroVoice");
                    MasterAudio.StopBus("EroSE");
                }
                catch { }
                try
                {
                    Time.timeScale = 0f;
                    if (BadEndPlayerSystem.IsVisible)
                    {
                        try { if (!AudioListener.pause) AudioListener.pause = true; } catch { }
                        if (tick % 5 == 0)
                        {
                            AudioSource[] all = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                            if (all != null)
                                for (int i = 0; i < all.Length; i++)
                                {
                                    if (all[i] == null || all[i].gameObject == null) continue;
                                    if (all[i].gameObject.name == "BadEndPlayerAudio") { all[i].UnPause(); continue; }
                                    if (all[i].isPlaying) all[i].Pause();
                                }
                        }
                    }
                }
                catch { }
                tick++;
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        
        public void StartBadEndPlayerLoseScreenThenPlayer()
        {
            StartCoroutine(BadEndPlayerLoseScreenSequence());
        }
        
        private IEnumerator BadEndPlayerLoseScreenSequence()
        {
            EnsureBadEndPlayerLoseScreen();
            if (_badEndPlayerLoseCanvas == null) yield break;
            _badEndPlayerLoseCanvas.SetActive(true);
            Transform overlayT = _badEndPlayerLoseCanvas.transform.Find("BadEndPlayerLoseOverlay");
            Image overlayImg = overlayT != null ? overlayT.GetComponent<Image>() : null;
            if (overlayImg != null)
            {
                float t = 0f;
                while (t < 0.4f) { t += Time.unscaledDeltaTime; overlayImg.color = new Color(0.6f, 0f, 0f, Mathf.Clamp01(t / 0.4f) * 0.7f); yield return null; }
                t = 0f;
                while (t < 1.2f) { t += Time.unscaledDeltaTime; overlayImg.color = new Color(0.2f, 0f, 0f, 0.7f - (1f - Mathf.Clamp01(t / 1.2f)) * 0.3f); yield return null; }
                t = 0f;
                while (t < 1.5f) { t += Time.unscaledDeltaTime; overlayImg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / 1.5f) * 0.98f); yield return null; }
                overlayImg.color = new Color(0f, 0f, 0f, 0.98f);
            }
            Transform youLoseT = _badEndPlayerLoseCanvas.transform.Find("YouLose");
            if (youLoseT != null)
            {
                youLoseT.gameObject.SetActive(true);
                var txt = youLoseT.GetComponent<UnityEngine.UI.Text>();
                if (txt != null)
                {
                    float elapsed = 0f;
                    while (elapsed < 1.2f) { elapsed += Time.unscaledDeltaTime; txt.color = new Color(1f, 1f, 1f, Mathf.Clamp01(elapsed / 1.2f)); yield return null; }
                    txt.color = Color.white;
                }
            }
            yield return new WaitForSecondsRealtime(0.8f);
            Transform epilogueT = _badEndPlayerLoseCanvas.transform.Find("Epilogue");
            if (epilogueT != null)
            {
                epilogueT.gameObject.SetActive(true);
                var txt = epilogueT.GetComponent<UnityEngine.UI.Text>();
                if (txt != null) { float elapsed = 0f; while (elapsed < 1.2f) { elapsed += Time.unscaledDeltaTime; txt.color = new Color(0.92f, 0.92f, 0.92f, Mathf.Clamp01(elapsed / 1.2f)); yield return null; } txt.color = new Color(0.92f, 0.92f, 0.92f, 1f); }
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Transform clickT = _badEndPlayerLoseCanvas.transform.Find("ClickToContinue");
            if (clickT != null)
            {
                clickT.gameObject.SetActive(true);
                var txt = clickT.GetComponent<UnityEngine.UI.Text>();
                if (txt != null) { float elapsed = 0f; while (elapsed < 0.6f) { elapsed += Time.unscaledDeltaTime; txt.color = new Color(0.75f, 0.75f, 0.75f, Mathf.Clamp01(elapsed / 0.6f)); yield return null; } txt.color = new Color(0.75f, 0.75f, 0.75f, 1f); }
            }
            Transform btnT = _badEndPlayerLoseCanvas.transform.Find("ClickAnywhere");
            if (btnT != null) { var btn = btnT.GetComponent<Button>(); if (btn != null) btn.interactable = true; }
        }
        
        public void StartCountdownCoroutine()
        {
            StartCoroutine(CountdownUpdate());
            StartCoroutine(CheckPlayerRespawn());
        }
        
        /// <summary>
        /// Show texts with shadow animation: first YOU LOSE, then epilogue after 2 seconds
        /// </summary>
        public IEnumerator ShowTextsWithAnimation()
        {
            // Smooth screen overlay fade in
            if (MindBrokenBadEndSystem._overlayObject != null)
            {
                Image overlay = MindBrokenBadEndSystem._overlayObject.GetComponent<Image>();
                if (overlay != null)
                {
                    float fadeDuration = 2.0f; // 2 seconds for smooth fade
                    float elapsed = 0f;
                    
                    while (elapsed < fadeDuration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float alpha = Mathf.Clamp01(elapsed / fadeDuration) * 0.95f; // Up to 95% opacity
                        overlay.color = new Color(0f, 0f, 0f, alpha);
                        yield return null;
                    }
                    
                    // Ensure final color is set
                    overlay.color = new Color(0f, 0f, 0f, 0.95f);
                }
            }
            
            // Show "YOU LOSE" with fade in animation (from shadow)
            if (MindBrokenBadEndSystem._youLoseText != null && MindBrokenBadEndSystem._youLoseLabel != null)
            {
                MindBrokenBadEndSystem._youLoseText.SetActive(true);
                
                // Fade in animation (from shadow)
                float fadeDuration = 1.5f; // 1.5 seconds for appearance
                float elapsed = 0f;
                
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    MindBrokenBadEndSystem._youLoseLabel.color = new Color(1f, 1f, 1f, alpha);
                    yield return null;
                }
                
                // Ensure final color is opaque
                MindBrokenBadEndSystem._youLoseLabel.color = Color.white;
            }
            
            // Wait 2 seconds before showing epilogue
            yield return new WaitForSecondsRealtime(2.0f);
            
            // Show epilogue text with fade in animation (from shadow)
            if (MindBrokenBadEndSystem._epilogueText != null && MindBrokenBadEndSystem._epilogueLabel != null)
            {
                MindBrokenBadEndSystem._epilogueText.SetActive(true);
                
                // Fade in animation (from shadow)
                float fadeDuration = 1.5f; // 1.5 seconds for appearance
                float elapsed = 0f;
                Color targetColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    MindBrokenBadEndSystem._epilogueLabel.color = new Color(0.9f, 0.9f, 0.9f, alpha);
                    yield return null;
                }
                
                // Ensure final color is opaque
                MindBrokenBadEndSystem._epilogueLabel.color = targetColor;
            }
            
            // Show revive button 1 second after epilogue
            yield return new WaitForSecondsRealtime(1f);
            
            if (MindBrokenBadEndSystem._reviveButton != null)
            {
                MindBrokenBadEndSystem._reviveButton.SetActive(true);
                // Button appearance animation
                Image buttonImage = MindBrokenBadEndSystem._reviveButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    Color startColor = buttonImage.color;
                    startColor.a = 0f;
                    buttonImage.color = startColor;
                    
                    float duration = 0.5f;
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float alpha = Mathf.Lerp(0f, 0.8f, elapsed / duration);
                        Color color = buttonImage.color;
                        color.a = alpha;
                        buttonImage.color = color;
                        yield return null;
                    }
                }
            }
        }
        
        private IEnumerator CountdownUpdate()
        {
            while (MindBrokenSystem.IsCountdownActive)
            {
                MindBrokenSystem.UpdateCountdown(Time.unscaledDeltaTime);
                yield return null;
            }
        }
        
        /// <summary>
        /// Check player respawn (hide Bad End on respawn)
        /// </summary>
        private IEnumerator CheckPlayerRespawn()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f); // Check every 0.5 sec
                
                try
                {
                    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null)
                    {
                        playercon player = playerObj.GetComponent<playercon>();
                        if (player != null)
                        {
                            // If player was dead and now alive - hide Bad End
                            if (_lastPlayer != null && _lastPlayer._Death && !player._Death)
                            {
                                HideBadEnd();
                            }
                            _lastPlayer = player;
                        }
                    }
                }
                catch { }
            }
        }
    }
    
    /// <summary>
    /// Handler for "The End" button click - quit game
    /// </summary>
    private static void OnReviveButtonClicked()
    {
        try
        {
            // Quit game
            Application.Quit();
            
            // For Unity editor (Application.Quit() doesn't work in editor)
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MindBroken BadEnd] Error in OnReviveButtonClicked: {ex.Message}");
        }
    }
}


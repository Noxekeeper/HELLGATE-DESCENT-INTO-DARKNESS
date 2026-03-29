using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Rage activation effects: RAGE text, edge glow, blood sprites, time slow.
/// </summary>
internal static class RageVisualEffectsSystem
{
    private const string CanvasObjectName = "RageVisualEffectsCanvas_XUAIGNORE";
    
    // Canvas and UI elements
    private static GameObject? canvasObject;
    private static Canvas? overlayCanvas;
    private static RectTransform? canvasRect;
    
    // RAGE text
    private static GameObject? rageTextObject;
    private static TextMeshProUGUI? rageText;
    private static Coroutine? textScaleCoroutine;
    
    // Camera for effects
    private static UnityEngine.Camera? mainCamera;
    
    // Flash effect (crimson red)
    private static MonoBehaviour? flashComponent;
    private static Coroutine? flashCoroutine;
    
    // Edge glow gradient
    private static GameObject? edgeGlowObject;
    private static Image? edgeGlowImage;
    private static Coroutine? edgeGlowCoroutine;
    private static Sprite? _cachedEdgeGlowSprite;
    
    private static Color RageGlowColor => new Color(
        Plugin.rageGlowColorR?.Value ?? 1f,
        Plugin.rageGlowColorG?.Value ?? 0f,
        Plugin.rageGlowColorB?.Value ?? 0.15f,
        1f
    );
    
    // Time slowdown
    private static float? _originalTimeScale = null;
    private static Coroutine? _slowMoCoroutine;
    
    // Activation sound
    private static AudioClip? _activationSoundClip = null;
    private static AudioSource? _audioSource = null;
    private static bool _soundLoaded = false;
    
    // Background images (blood banners)
    private static Sprite? _blood1Sprite = null;
    private static Sprite? _blood2Sprite = null;
    private static Sprite? _blood3Sprite = null;
    private static Sprite? _skullSprite = null;
    private static bool _bloodSpritesLoaded = false;
    
    // Initialization flag
    private static bool _initialized = false;
    
    // Image components for background banners
    private static GameObject? _bloodBackgroundParent = null;
    private static Image? _blood1Image = null;
    private static Image? _blood2Image = null;
    private static Image? _blood3Image = null;
    private static Image? _skullImage = null;
    
    /// <summary>
    /// System initialization (called on game load)
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;

        try
        {
            EnsureCanvas();
            LoadActivationSound();
            LoadBloodSprites();

            // Preload edge glow sprite to avoid freeze on first activation
            EnsureEdgeGlowSprite();

            // Subscribe to Rage activation/deactivation events
            RageSystem.OnActivated += TriggerActivationEffects;
            RageSystem.OnDeactivated += StopAllEffects;

            _initialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE VISUAL] Error during initialization: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Triggers all visual effects on Rage Mode activation
    /// </summary>
    internal static void TriggerActivationEffects()
    {
        try
        {
            EnsureCanvas();
            EnsureCamera();

            // Start all effects simultaneously
            StartRageTextEffect();
            StartBloodBackgroundEffect(); // Blood background banners
            PlayActivationSound(); // Play activation sound
            StartFlashEffect(); // Flash effect restored
            // StartEdgeGlowEffect(); // Edge red glow disabled (not needed)
            // StartSlowMoEffect(); // Slow motion disabled (0%)
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE VISUAL] Error triggering effects: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Stops all visual effects
    /// </summary>
    internal static void StopAllEffects()
    {
        StopRageTextEffect();
        StopBloodBackgroundEffect(); // Stop blood background banners
        StopFlashEffect(); // Stop flash effect
        // StopSlowMoEffect(); // Slow motion disabled (0%)

        // Gradient stopped (red edge glow disabled)
        // StopEdgeGlowEffect();
    }
    
    /// <summary>
    /// Reset state
    /// </summary>
    internal static void Reset()
    {
        StopAllEffects();
        // StopSlowMoEffect(); // Slow-motion visual burst is intentionally disabled.
        
        if (rageTextObject != null)
        {
            UnityEngine.Object.Destroy(rageTextObject);
            rageTextObject = null;
            rageText = null;
        }
        
        if (edgeGlowObject != null)
        {
            UnityEngine.Object.Destroy(edgeGlowObject);
            edgeGlowObject = null;
            edgeGlowImage = null;
        }
        
        if (_audioSource != null)
        {
            UnityEngine.Object.Destroy(_audioSource.gameObject);
            _audioSource = null;
        }
        
        if (_bloodBackgroundParent != null)
        {
            UnityEngine.Object.Destroy(_bloodBackgroundParent);
            _bloodBackgroundParent = null;
            _blood1Image = null;
            _blood2Image = null;
            _blood3Image = null;
            _skullImage = null;
        }
    }
    
    #region Sound Loading and Playback
    
    /// <summary>
    /// Loads activation sound from file
    /// </summary>
    private static void LoadActivationSound()
    {
        if (_soundLoaded) return;
        
        try
        {
            // Sound file path
            string soundPath = GetSoundFilePath();
            
            if (!File.Exists(soundPath))
            {
                // Logging disabled to reduce log volume (keep only errors)
                // Plugin.Log?.LogWarning($"[RAGE SOUND] Sound file not found: {soundPath}");
                return;
            }
            
            // Start coroutine for sound loading
            if (canvasObject != null)
            {
                var coroutineRunner = canvasObject.GetComponent<RageVisualEffectsRunner>();
                if (coroutineRunner == null)
                {
                    coroutineRunner = canvasObject.AddComponent<RageVisualEffectsRunner>();
                }
                coroutineRunner.StartCoroutine(LoadSoundCoroutine(soundPath));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE SOUND] Failed to load activation sound: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Gets sound file path
    /// </summary>
    private static string GetSoundFilePath()
    {
        // Path relative to game folder: sources/HellGate_sources/Rage/ha-ha-ha.wav
        string basePath = Application.dataPath;
        if (basePath.EndsWith("_Data"))
        {
            basePath = basePath.Substring(0, basePath.Length - 5); // Strip "_Data" suffix.
        }
        
        // Try multiple installation layouts.
        string path1 = Path.Combine(basePath, "sources");
        path1 = Path.Combine(path1, "HellGate_sources");
        path1 = Path.Combine(path1, "Rage");
        path1 = Path.Combine(path1, "ha-ha-ha.wav");
        
        string path2 = Path.Combine(basePath, "..");
        path2 = Path.Combine(path2, "sources");
        path2 = Path.Combine(path2, "HellGate_sources");
        path2 = Path.Combine(path2, "Rage");
        path2 = Path.Combine(path2, "ha-ha-ha.wav");
        
        string path3 = Path.Combine(basePath, "BepInEx");
        path3 = Path.Combine(path3, "plugins");
        path3 = Path.Combine(path3, "NoR_HellGate");
        path3 = Path.Combine(path3, "sources");
        path3 = Path.Combine(path3, "HellGate_sources");
        path3 = Path.Combine(path3, "Rage");
        path3 = Path.Combine(path3, "ha-ha-ha.wav");
        
        string[] possiblePaths = {
            path1,
            path2,
            path3
        };
        
        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        // Return primary expected path for a deterministic fallback.
        return possiblePaths[0];
    }
    
    /// <summary>
    /// Coroutine for loading sound via WWW (legacy Unity API compatibility).
    /// </summary>
    private static IEnumerator LoadSoundCoroutine(string filePath)
    {
        string url = "file://" + filePath.Replace("\\", "/");

        WWW www = new WWW(url);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogError($"[RAGE SOUND] Audio download error: {www.error}");
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false);
        if (clip != null)
        {
            _activationSoundClip = clip;
            _soundLoaded = true;
            
            // Create AudioSource for playback
            if (canvasObject != null)
            {
                _audioSource = canvasObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.volume = 1.0f;
            }
            
            // Logging intentionally disabled to reduce noise.
            // Plugin.Log?.LogWarning($"[RAGE SOUND] Activation sound loaded: {filePath}");
        }
        else
        {
            Plugin.Log?.LogError("[RAGE SOUND] Failed to create AudioClip from downloaded data.");
        }
    }
    
    /// <summary>
    /// Plays Rage activation sound
    /// </summary>
    private static void PlayActivationSound()
    {
        try
        {
            if (!_soundLoaded || _activationSoundClip == null)
            {
                // Lazy-load on first use.
                LoadActivationSound();
                return;
            }
            
            if (_audioSource == null && canvasObject != null)
            {
                _audioSource = canvasObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.volume = 1.0f;
            }
            
            if (_audioSource != null && _activationSoundClip != null)
            {
                _audioSource.PlayOneShot(_activationSoundClip, 1.0f);
                // Logging intentionally disabled to reduce noise.
                // Plugin.Log?.LogWarning("[RAGE SOUND] Activation sound played.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE SOUND] Failed to play activation sound: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    #endregion
    
    #region Blood Sprites Loading
    
    /// <summary>
    /// Loads background images (blood banners) from files
    /// </summary>
    private static void LoadBloodSprites()
    {
        if (_bloodSpritesLoaded) return;
        
        try
        {
            // Start coroutine for loading all images
            if (canvasObject != null)
            {
                var coroutineRunner = canvasObject.GetComponent<RageVisualEffectsRunner>();
                if (coroutineRunner == null)
                {
                    coroutineRunner = canvasObject.AddComponent<RageVisualEffectsRunner>();
                }
                coroutineRunner.StartCoroutine(LoadBloodSpritesCoroutine());
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE BLOOD] Failed to load blood banners: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Gets path to blood banners folder
    /// </summary>
    private static string GetBloodSpritesDirectory()
    {
        string basePath = Application.dataPath;
        if (basePath.EndsWith("_Data"))
        {
            basePath = basePath.Substring(0, basePath.Length - 5); // Strip "_Data" suffix.
        }
        
        // Try multiple installation layouts.
        string path1 = Path.Combine(basePath, "sources");
        path1 = Path.Combine(path1, "HellGate_sources");
        path1 = Path.Combine(path1, "RAGE_FURY");
        
        string path2 = Path.Combine(basePath, "..");
        path2 = Path.Combine(path2, "sources");
        path2 = Path.Combine(path2, "HellGate_sources");
        path2 = Path.Combine(path2, "RAGE_FURY");
        
        string path3 = Path.Combine(basePath, "BepInEx");
        path3 = Path.Combine(path3, "plugins");
        path3 = Path.Combine(path3, "NoR_HellGate");
        path3 = Path.Combine(path3, "sources");
        path3 = Path.Combine(path3, "HellGate_sources");
        path3 = Path.Combine(path3, "RAGE_FURY");
        
        string[] possiblePaths = {
            path1,
            path2,
            path3
        };
        
        foreach (string path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }
        
        // Return primary expected path for a deterministic fallback.
        return possiblePaths[0];
    }
    
    /// <summary>
    /// Coroutine for loading all blood banners
    /// </summary>
    private static IEnumerator LoadBloodSpritesCoroutine()
    {
        string baseDir = GetBloodSpritesDirectory();
        
        string[] imageFiles = {
            "Blood1.png",
            "Blood2.png",
            "Blood3.png",
            "skull.png"
        };
        
        Sprite?[] sprites = new Sprite?[4];
        
        // Load all images in parallel
        for (int i = 0; i < imageFiles.Length; i++)
        {
            string filePath = Path.Combine(baseDir, imageFiles[i]);
            
            if (!File.Exists(filePath))
            {
                // Logging disabled to reduce log volume (keep only errors)
                // Plugin.Log?.LogWarning($"[RAGE BLOOD] File not found: {filePath}");
                continue;
            }
            
            string url = "file://" + filePath.Replace("\\", "/");
            WWW www = new WWW(url);
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Plugin.Log?.LogError($"[RAGE BLOOD] Download error for {imageFiles[i]}: {www.error}");
                continue;
            }

            Texture2D texture = www.texture;
            if (texture != null)
            {
                // Build sprite from decoded texture.
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f // pixels per unit
                );
                sprites[i] = sprite;
                // Logging intentionally disabled to reduce noise.
                // Plugin.Log?.LogWarning($"[RAGE BLOOD] Loaded: {imageFiles[i]} ({texture.width}x{texture.height})");
            }
        }
        
        // Save sprites
        _blood1Sprite = sprites[0];
        _blood2Sprite = sprites[1];
        _blood3Sprite = sprites[2];
        _skullSprite = sprites[3];
        
        _bloodSpritesLoaded = true;
        
        // Logging intentionally disabled to reduce noise.
        // Plugin.Log?.LogWarning($"[RAGE BLOOD] Banner loading completed. Blood1={_blood1Sprite != null}, Blood2={_blood2Sprite != null}, Blood3={_blood3Sprite != null}, Skull={_skullSprite != null}");
    }
    
    #endregion
    
    #region Canvas Setup
    
    private static void EnsureCanvas()
    {
        if (canvasObject != null) return;
        
        // Create overlay canvas.
        canvasObject = new GameObject(CanvasObjectName);
        UnityEngine.Object.DontDestroyOnLoad(canvasObject);
        
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 998; // Render below MindBroken fog (999).
        
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObject.AddComponent<GraphicRaycaster>();
        
        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.sizeDelta = Vector2.zero;
        canvasRect.anchoredPosition = Vector2.zero;
    }
    
    private static void EnsureCamera()
    {
        if (mainCamera == null || mainCamera.gameObject == null || !mainCamera.gameObject.activeInHierarchy)
        {
            var camObj = UnifiedCameraCacheManager.GetMainCamera();
            mainCamera = camObj != null ? camObj.GetComponent<UnityEngine.Camera>() : null;

            if (mainCamera != null && flashComponent != null && flashComponent.gameObject != mainCamera.gameObject)
            {
                flashComponent = null;
            }
        }
    }
    
    #endregion
    
    #region Blood Background Effect
    
    /// <summary>
    /// Starts blood background banners for RAGE/OUTBURST FURY text
    /// </summary>
    private static void StartBloodBackgroundEffect()
    {
        if (canvasRect == null) return;
        if (!_bloodSpritesLoaded)
        {
            // Try to load if not loaded yet
            LoadBloodSprites();
            return;
        }
        
        // Stop previous effect
        StopBloodBackgroundEffect();
        
        // Create parent object for all banners
        _bloodBackgroundParent = new GameObject("RageBloodBackground");
        _bloodBackgroundParent.transform.SetParent(canvasRect, false);
        
        // Place banners BEFORE text in hierarchy (so text appears on top)
        if (rageTextObject != null)
        {
            _bloodBackgroundParent.transform.SetSiblingIndex(rageTextObject.transform.GetSiblingIndex());
        }
        
        RectTransform parentRect = _bloodBackgroundParent.AddComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0.5f, 0.5f);
        parentRect.anchorMax = new Vector2(0.5f, 0.5f);
        parentRect.pivot = new Vector2(0.5f, 0.5f);
        parentRect.sizeDelta = new Vector2(1000, 400); // Size of banner area
        parentRect.anchoredPosition = new Vector2(0, 300f); // Position matches text
        
        bool isOutburstFury = RageSystem.IsOutburstFury;
        
        if (isOutburstFury)
        {
            // OUTBURST FURY: show all four banners at once.
            if (_blood1Sprite != null)
            {
                CreateBloodImage("Blood1", _blood1Sprite, new Vector2(-150, 0), 1.0f);
            }
            if (_blood2Sprite != null)
            {
                CreateBloodImage("Blood2", _blood2Sprite, new Vector2(150, 0), 1.0f);
            }
            if (_blood3Sprite != null)
            {
                CreateBloodImage("Blood3", _blood3Sprite, new Vector2(0, -50), 1.0f);
            }
            if (_skullSprite != null)
            {
                CreateBloodImage("Skull", _skullSprite, new Vector2(0, 50), 1.0f);
            }
            
            // Logging intentionally disabled to reduce noise.
            // Plugin.Log?.LogWarning("[RAGE BLOOD] OUTBURST FURY: all four banners activated.");
        }
        else
        {
            // RAGE: show only Blood1.png.
            if (_blood1Sprite != null)
            {
                CreateBloodImage("Blood1", _blood1Sprite, Vector2.zero, 1.0f);
                // Plugin.Log?.LogWarning("[RAGE BLOOD] RAGE: Blood1 banner activated.");
            }
            else
            {
                // Logging disabled; retain warning template if sprite is missing.
                // Plugin.Log?.LogWarning("[RAGE BLOOD] Warning: Blood1 sprite not loaded.");
            }
        }
        
        _bloodBackgroundParent.SetActive(true);
    }
    
    /// <summary>
    /// Creates an Image component for a blood banner.
    /// </summary>
    private static void CreateBloodImage(string name, Sprite sprite, Vector2 offset, float scale)
    {
        if (_bloodBackgroundParent == null) return;
        
        GameObject imageObject = new GameObject($"BloodImage_{name}");
        imageObject.transform.SetParent(_bloodBackgroundParent.transform, false);
        
        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(sprite.texture.width, sprite.texture.height);
        imageRect.anchoredPosition = offset;
        imageRect.localScale = Vector3.one * scale;
        
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, 0.8f); // Slight transparency for layering.
        image.raycastTarget = false;
        
        // Store references for runtime control.
        switch (name)
        {
            case "Blood1":
                _blood1Image = image;
                break;
            case "Blood2":
                _blood2Image = image;
                break;
            case "Blood3":
                _blood3Image = image;
                break;
            case "Skull":
                _skullImage = image;
                break;
        }
    }
    
    /// <summary>
    /// Stops background banners.
    /// </summary>
    private static void StopBloodBackgroundEffect()
    {
        try
        {
            if (_bloodBackgroundParent != null)
            {
                _bloodBackgroundParent.SetActive(false);
                // Destroy object on stop; it is recreated on next activation.
                UnityEngine.Object.Destroy(_bloodBackgroundParent);
                _bloodBackgroundParent = null;
                _blood1Image = null;
                _blood2Image = null;
                _blood3Image = null;
                _skullImage = null;
            }
        }
        catch (Exception ex)
        {
            // Intentionally ignore stop errors.
            // Plugin.Log?.LogError($"[Rage] Error stopping blood background effects: {ex.Message}");
        }
    }
    
    #endregion
    
    #region RAGE Text Effect
    
    private static void StartRageTextEffect()
    {
        if (canvasRect == null) return;
        
        // Stop previous effect
        StopRageTextEffect();
        
        // Create text object.
        if (rageTextObject == null)
        {
            rageTextObject = new GameObject("RageText");
            rageTextObject.transform.SetParent(canvasRect, false);
            
            RectTransform textRect = rageTextObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(800, 200);
            textRect.anchoredPosition = new Vector2(0, 300f); // 300px above center.
            
            rageText = rageTextObject.AddComponent<TextMeshProUGUI>();
            
            // Ensure object is active before setup.
            rageTextObject.SetActive(true);
            
            // OUTBURST FURY: display "OUTBURST FURY" for auto activation, otherwise "RAGE".
            if (RageSystem.IsOutburstFury)
            {
                rageText.text = "OUTBURST FURY";
            }
            else
            {
            rageText.text = "RAGE";
            }
            rageText.fontSize = 60; // Reduced for layout stability.
            rageText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            rageText.color = RageGlowColor;
            rageText.alignment = TextAlignmentOptions.Center;
            rageText.enableWordWrapping = false;
            
            // Strong outline for readability.
            rageText.outlineWidth = 0.5f; // Increased from 0.2f.
            rageText.outlineColor = new Color(0f, 0f, 0f, 1f);
            
            // Ensure text is visible.
            rageText.alpha = 1f;
            rageText.raycastTarget = false; // Disable raycast for performance.
            
            // Logging intentionally disabled to reduce noise.
            // Plugin.Log?.LogWarning("[RAGE TEXT] RAGE text created and visible.");
        }
        else
        {
            rageTextObject.SetActive(true);
            if (rageText != null)
            {
                // OUTBURST FURY: refresh text on repeated activation.
                if (RageSystem.IsOutburstFury)
                {
                    rageText.text = "OUTBURST FURY";
                }
                else
                {
                    rageText.text = "RAGE";
                }
                rageText.alpha = 1f;
            }
            
            // Refresh banners on repeated activation.
            StartBloodBackgroundEffect();
        }
        
        // Start text scale coroutine.
        var coroutineRunner = canvasObject.GetComponent<RageVisualEffectsRunner>();
        if (coroutineRunner == null)
        {
            coroutineRunner = canvasObject.AddComponent<RageVisualEffectsRunner>();
        }
        
        textScaleCoroutine = coroutineRunner.StartCoroutine(RageTextScaleCoroutine());
    }
    
    private static void StopRageTextEffect()
    {
        try
        {
            if (textScaleCoroutine != null && canvasObject != null)
            {
                var runner = canvasObject.GetComponent<RageVisualEffectsRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(textScaleCoroutine);
                }
                textScaleCoroutine = null;
            }

            if (rageTextObject != null)
            {
                rageTextObject.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            // Intentionally ignore stop errors.
            // Plugin.Log?.LogError($"[Rage] Error stopping text effects: {ex.Message}");
        }
    }
    
    private static IEnumerator RageTextScaleCoroutine()
    {
        if (rageTextObject == null || rageText == null) yield break;
        
        float duration = 3f;
        float elapsed = 0f;
        
        // Initial scale (large to normal quickly).
        Vector3 startScale = Vector3.one * 2.5f;
        Vector3 midScale = Vector3.one * 1.0f;
        Vector3 endScale = Vector3.one * 0.8f;
        
        rageTextObject.transform.localScale = startScale;
        rageText.alpha = 1f;
        
        // Initial banner alpha.
        float bloodAlpha = 0.8f;
        if (_blood1Image != null) _blood1Image.color = new Color(1f, 1f, 1f, bloodAlpha);
        if (_blood2Image != null) _blood2Image.color = new Color(1f, 1f, 1f, bloodAlpha);
        if (_blood3Image != null) _blood3Image.color = new Color(1f, 1f, 1f, bloodAlpha);
        if (_skullImage != null) _skullImage.color = new Color(1f, 1f, 1f, bloodAlpha);
        
        // Phase 1: quick shrink (0.3s).
        float phase1Duration = 0.3f;
        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / phase1Duration;
            rageTextObject.transform.localScale = Vector3.Lerp(startScale, midScale, progress);
            yield return null;
        }
        
        // Phase 2: pulse and gradual fade.
        float phase2Start = elapsed;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float phase2Progress = (elapsed - phase2Start) / (duration - phase2Start);
            
            // Pulse
            float pulse = Mathf.Sin((elapsed - phase2Start) * 10f) * 0.1f + 1f;
            rageTextObject.transform.localScale = Vector3.Lerp(midScale, endScale, phase2Progress) * pulse;
            
            // Text fade
            float textAlpha = Mathf.Lerp(1f, 0f, phase2Progress);
            rageText.alpha = textAlpha;
            
            // Banner fade synchronized with text.
            float currentBloodAlpha = Mathf.Lerp(bloodAlpha, 0f, phase2Progress);
            if (_blood1Image != null) _blood1Image.color = new Color(1f, 1f, 1f, currentBloodAlpha);
            if (_blood2Image != null) _blood2Image.color = new Color(1f, 1f, 1f, currentBloodAlpha);
            if (_blood3Image != null) _blood3Image.color = new Color(1f, 1f, 1f, currentBloodAlpha);
            if (_skullImage != null) _skullImage.color = new Color(1f, 1f, 1f, currentBloodAlpha);
            
            yield return null;
        }
        
        // Final state: hide text and banners together.
        rageTextObject.SetActive(false);
        StopBloodBackgroundEffect(); // Stop banners together with text.
        textScaleCoroutine = null;
    }
    
    #endregion
    
    #region Edge Glow Effect (persistent while Rage is active)
    
    private static void StartEdgeGlowEffect()
    {
        if (canvasRect == null) return;
        
        // Stop previous effect
        StopEdgeGlowEffect();
        
        // Create gradient object.
        if (edgeGlowObject == null)
        {
            edgeGlowObject = new GameObject("RageEdgeGlow");
            edgeGlowObject.transform.SetParent(canvasRect, false);
            
            RectTransform glowRect = edgeGlowObject.AddComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.sizeDelta = Vector2.zero;
            glowRect.anchoredPosition = Vector2.zero;
            
            edgeGlowImage = edgeGlowObject.AddComponent<Image>();
            edgeGlowImage.color = new Color(RageGlowColor.r, RageGlowColor.g, RageGlowColor.b, 0f);
            
            // Use cached sprite instead of creating new one
            edgeGlowImage.sprite = _cachedEdgeGlowSprite;
            edgeGlowImage.type = Image.Type.Simple;
        }
        else
        {
            edgeGlowObject.SetActive(true);
        }
        
        // Start coroutine: glow remains while Rage is active.
        var coroutineRunner = canvasObject.GetComponent<RageVisualEffectsRunner>();
        if (coroutineRunner == null)
        {
            coroutineRunner = canvasObject.AddComponent<RageVisualEffectsRunner>();
        }
        
        edgeGlowCoroutine = coroutineRunner.StartCoroutine(EdgeGlowCoroutine());
    }
    
    private static void EnsureEdgeGlowSprite()
    {
        if (_cachedEdgeGlowSprite != null) return;
        _cachedEdgeGlowSprite = CreateHorizontalBarsSprite(64, 64, RageGlowColor);
    }
    
    private static void StopEdgeGlowEffect()
    {
        try
        {
            if (edgeGlowCoroutine != null && canvasObject != null)
            {
                var runner = canvasObject.GetComponent<RageVisualEffectsRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(edgeGlowCoroutine);
                }
                edgeGlowCoroutine = null;
            }

            if (edgeGlowObject != null)
            {
                edgeGlowObject.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            // Intentionally ignore stop errors.
            // Plugin.Log?.LogError($"[Rage] Error stopping edge glow effects: {ex.Message}");
        }
    }
    
    private static IEnumerator EdgeGlowCoroutine()
    {
        if (edgeGlowImage == null) yield break;
        
        // Phase 1: fade in (0.3s).
        float phase1Duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / phase1Duration;
            float alpha = Mathf.Lerp(0f, 0.6f, progress);
            edgeGlowImage.color = new Color(RageGlowColor.r, RageGlowColor.g, RageGlowColor.b, alpha);
            yield return null;
        }
        
        float timeElapsed = 0f;
        float pulseSpeed = 0.75f;
        float baseAlpha = Plugin.rageGlowMaxAlpha?.Value ?? 0.55f;
        float minAlpha = baseAlpha - 0.03f;
        float maxAlpha = baseAlpha + 0.03f;
        
        while (RageSystem.IsActive) // Keep glow active while Rage remains active.
        {
            timeElapsed += Time.deltaTime;
            
            // Smooth sinusoidal pulse in normalized 0..1 range.
            float normalizedPulse = (Mathf.Sin(timeElapsed * pulseSpeed * 2f * Mathf.PI) + 1f) * 0.5f; // 0..1
            float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, normalizedPulse);
            
            edgeGlowImage.color = new Color(RageGlowColor.r, RageGlowColor.g, RageGlowColor.b, currentAlpha);
            yield return null;
        }
        
        // Phase 3: fade out on deactivation (0.5s).
        elapsed = 0f;
        float phase3Duration = 0.5f;
        float startAlpha = edgeGlowImage.color.a;
        while (elapsed < phase3Duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / phase3Duration;
            float alpha = Mathf.Lerp(startAlpha, 0f, progress);
            edgeGlowImage.color = new Color(RageGlowColor.r, RageGlowColor.g, RageGlowColor.b, alpha);
            yield return null;
        }
        
        // Disable object.
        edgeGlowObject.SetActive(false);
        edgeGlowCoroutine = null;
    }
    
    private static Sprite CreateHorizontalBarsSprite(int width, int height, Color color)
    {
        int textureWidth = width;
        int textureHeight = height;

        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

        Color[] pixels = new Color[textureWidth * textureHeight];
        float barHeight = Mathf.Max(4f, textureHeight * 0.2f);
        float alphaMax = 1f;

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float alpha = 0f;
                if (y < barHeight)
                    alpha = (1f - y / barHeight) * alphaMax;
                else if (y >= textureHeight - barHeight)
                    alpha = ((y - (textureHeight - barHeight)) / barHeight) * alphaMax;

                pixels[y * textureWidth + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        return Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
    }
    
    #endregion
    
    #region Flash Effect (crimson highlight)
    
    private static void StartFlashEffect()
    {
        EnsureCamera();
        if (mainCamera == null || mainCamera.gameObject == null)
        {
            // Logging intentionally disabled to reduce noise (keep errors only).
            // Plugin.Log?.LogWarning("[RAGE VISUAL] Camera not found, flash effect skipped.");
            return;
        }
        
        // Stop previous effect
        StopFlashEffect();
        
        // Get or create Flash Color component.
        if (flashComponent == null)
        {
            flashComponent = mainCamera.GetComponent<CameraFilterPack_Drawing_Manga_Flash_Color>();
            if (flashComponent == null)
            {
                flashComponent = mainCamera.gameObject.AddComponent<CameraFilterPack_Drawing_Manga_Flash_Color>();
            }
        }
        
        if (flashComponent is CameraFilterPack_Drawing_Manga_Flash_Color flash)
        {
            flash.enabled = true;
            flash.Color = RageGlowColor; // Crimson highlight color.
            flash.Size = 10f;
            flash.Speed = 30;
            flash.PosX = 0.5f; // Screen center.
            flash.PosY = 0.8f; // Slightly above center, near the text.
            flash.Intensity = 0f;
            
            // Start coroutine.
            var coroutineRunner = mainCamera.GetComponent<RageVisualEffectsRunner>();
            if (coroutineRunner == null)
            {
                coroutineRunner = mainCamera.gameObject.AddComponent<RageVisualEffectsRunner>();
            }
            
            flashCoroutine = coroutineRunner.StartCoroutine(FlashCoroutine());
        }
    }
    
    private static void StopFlashEffect()
    {
        try
        {
            if (flashCoroutine != null && mainCamera != null)
            {
                var runner = mainCamera.GetComponent<RageVisualEffectsRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(flashCoroutine);
                }
                flashCoroutine = null;
            }

            if (flashComponent is CameraFilterPack_Drawing_Manga_Flash_Color flash)
            {
                flash.enabled = false;
                flash.Intensity = 0f;
            }
        }
        catch (Exception ex)
        {
            // Intentionally ignore stop errors.
            // Plugin.Log?.LogError($"[Rage] Error stopping flash effects: {ex.Message}");
        }
    }
    
    private static IEnumerator FlashCoroutine()
    {
        if (flashComponent == null) yield break;
        
        CameraFilterPack_Drawing_Manga_Flash_Color flash = flashComponent as CameraFilterPack_Drawing_Manga_Flash_Color;
        if (flash == null) yield break;
        
        // Phase 1: quick flash (0.2s).
        float elapsed = 0f;
        float phase1Duration = 0.2f;
        
        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / phase1Duration;
            flash.Intensity = Mathf.Lerp(0f, 1f, progress);
            yield return null;
        }
        
        flash.Intensity = 1f;
        
        // Phase 2: quick fade (0.3s).
        elapsed = 0f;
        float phase2Duration = 0.3f;
        
        while (elapsed < phase2Duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / phase2Duration;
            flash.Intensity = Mathf.Lerp(1f, 0f, progress);
            yield return null;
        }
        
        // Disable effect.
        flash.enabled = false;
        flash.Intensity = 0f;
        flashCoroutine = null;
    }
    
    #endregion
    
    #region Slow-Mo Effect (legacy burst on activation)
    
    private static void StartSlowMoEffect()
    {
        // Stop previous effect
        StopSlowMoEffect();
        
        // Store original timeScale.
        _originalTimeScale = Time.timeScale;
        
        // Start slowdown coroutine.
        var coroutineRunner = canvasObject?.GetComponent<RageVisualEffectsRunner>();
        if (coroutineRunner == null && canvasObject != null)
        {
            coroutineRunner = canvasObject.AddComponent<RageVisualEffectsRunner>();
        }
        
        if (coroutineRunner != null)
        {
            _slowMoCoroutine = coroutineRunner.StartCoroutine(SlowMoCoroutine());
        }
    }
    
    private static void StopSlowMoEffect()
    {
        if (_slowMoCoroutine != null && canvasObject != null)
        {
            var runner = canvasObject.GetComponent<RageVisualEffectsRunner>();
            if (runner != null)
            {
                runner.StopCoroutine(_slowMoCoroutine);
            }
            _slowMoCoroutine = null;
        }
        
        // Restore timeScale.
        if (_originalTimeScale.HasValue)
        {
            Time.timeScale = _originalTimeScale.Value;
            _originalTimeScale = null;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
    
    private static IEnumerator SlowMoCoroutine()
    {
        // Phase 1: slowdown disabled (target 1.0x speed).
        float targetTimeScale = 1.0f; // No slowdown.
        float duration = 0.2f; // Quick transition.
        float elapsed = 0f;
        float startTimeScale = Time.timeScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime / Time.timeScale; // Unscaled timing approximation.
            float progress = Mathf.Clamp01(elapsed / duration);
            Time.timeScale = Mathf.Lerp(startTimeScale, targetTimeScale, progress);
            yield return null;
        }
        
        Time.timeScale = targetTimeScale;
        
        // Phase 2: hold for 1 second.
        elapsed = 0f;
        duration = 1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime / Time.timeScale; // Unscaled timing approximation.
            // Force hold in case other systems attempt to overwrite timeScale.
            if (Mathf.Abs(Time.timeScale - targetTimeScale) > 0.001f)
            {
                Time.timeScale = targetTimeScale;
            }
            yield return null;
        }
        
        // Phase 3: smooth restore.
        elapsed = 0f;
        duration = 0.3f;
        float restoreTimeScale = _originalTimeScale ?? 1f;
        startTimeScale = targetTimeScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime / Time.timeScale; // Unscaled timing approximation.
            float progress = Mathf.Clamp01(elapsed / duration);
            Time.timeScale = Mathf.Lerp(startTimeScale, restoreTimeScale, progress);
            yield return null;
        }
        
        // Restore original timeScale.
        Time.timeScale = restoreTimeScale;
        _originalTimeScale = null;
        _slowMoCoroutine = null;
    }
    
    #endregion
    
    
    // Helper MonoBehaviour used to run coroutines.
    private class RageVisualEffectsRunner : MonoBehaviour { }
}

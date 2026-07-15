using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Systems.Effects;

/// <summary>
/// HSceneBlackBackgroundSystem - black background system for H-scenes
/// 
/// MECHANICS:
/// - On grab (START se_count==1) hides entire world
/// - Leaves only the H-scene (enemy + player in animation)
/// - Creates black background on full screen
/// - Holds the effect for 5 seconds
/// - Restores everything
/// </summary>
internal static class HSceneBlackBackgroundSystem
{
    private const string BackgroundObjectName = "HSceneBlackBackground";
    
    private static GameObject? _blackBackgroundObject;
    private static MonoBehaviour? _coroutineRunner;
    private static Coroutine? _restoreCoroutine;
    private static Coroutine? _mindBrokenCoroutine;
    private static float _lastActivationTime = -999f;
    
    private static bool _isActive = false;
    private const float EFFECT_DURATION = 5f; // fixed: hold 5 seconds for all triggers

    private static bool _lethalTrapDeathClipMode;
    private static bool _holdUntilManualDeactivate;
    private static readonly List<Renderer> _lethalTrapHiddenRenderers = new List<Renderer>();
    
    // List of hidden objects for restore
    private static readonly List<GameObject> _hiddenObjects = new List<GameObject>();
    private static readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    
    // Saved camera clear color and flags
    private static Color? _originalClearColor;
    private static CameraClearFlags? _originalClearFlags;
    private static UnityEngine.Camera? _mainCamera;
    
    // Current background parameters
    private static BackgroundParams _currentParams = new BackgroundParams
    {
        BaseColor = Color.black,
        PulseColor = Color.black,
        UsePulse = false,
        PulseSpeed = 0f,
        Duration = EFFECT_DURATION,
        EnemyName = "Unknown",
        AnimationName = "Unknown"
    };
    private static Coroutine? _colorEffectCoroutine;
    
    // ========== CUSTOM PARAMETER SYSTEM ==========
    
    /// <summary>
    /// Background parameters for different events and enemies
    /// </summary>
    public struct BackgroundParams
    {
        public Color BaseColor;           // Base background color
        public Color PulseColor;          // Pulse color (if used)
        public bool UsePulse;             // Whether to use pulsing
        public float PulseSpeed;          // Pulse speed
        public float Duration;            // Effect duration
        public string EnemyName;          // Enemy name (for filtering)
        public string AnimationName;      // Animation name (for filtering)
    }
    
    /// <summary>
    /// Get background parameters for a specific enemy and event
    /// </summary>
    private static BackgroundParams GetBackgroundParams(string enemyName, string animationName)
    {
        // Normalize enemy name
        string normalizedEnemyName = enemyName ?? "Unknown";
        
        // Default: black background for 5 seconds
        return new BackgroundParams
        {
            BaseColor = Color.black,
            PulseColor = Color.black,
            UsePulse = false,
            PulseSpeed = 0f,
            Duration = EFFECT_DURATION, // 5 seconds for FIN
            EnemyName = normalizedEnemyName,
            AnimationName = animationName ?? "Unknown"
        };
    }
    
    // ========== SUBSCRIPTION SYSTEM ==========
    
    /// <summary>
    /// Black background activation event
    /// Parameters: black background GameObject, effect duration
    /// </summary>
    public static event Action<GameObject, float>? OnActivated;
    
    /// <summary>
    /// Black background deactivation event
    /// </summary>
    public static event Action? OnDeactivated;
    
    /// <summary>
    /// Get black background GameObject (for adding effects such as X-Ray)
    /// </summary>
    public static GameObject? GetBackgroundObject()
    {
        return _blackBackgroundObject;
    }
    
    /// <summary>
    /// Check if effect is active
    /// </summary>
    public static bool IsActive => _isActive;
    
    // ========================================
    
    /// <summary>
    /// Activate background for H-scenes with custom parameters
    /// </summary>
    internal static void Activate(string enemyName = null, string animationName = null, float? customDuration = null, bool useSmoothFade = false)
    {
        if (Plugin.enableHSceneBlackBackground != null && !Plugin.enableHSceneBlackBackground.Value)
            return;

        if (MindBrokenBadEndSystem.IsBadEndActive) return;
        // Block restart if less than EFFECT_DURATION has passed since the previous activation
        float now = Time.unscaledTime;
        float durationCheck = customDuration ?? EFFECT_DURATION;
        if (now - _lastActivationTime < durationCheck - 0.05f)
        {
            // Plugin.Log?.LogInfo($"[HScene Black Background] Skipped: cooldown active (last {_lastActivationTime}, now {now})"); // Disabled for release
            return;
        }
        
        // If background is already active — ignore the repeat call (do not restart the timer)
        if (_isActive)
        {
            // Plugin.Log?.LogInfo("[HScene Black Background] Already active, skip re-activate"); // Disabled for release
            return;
        }
        
        _isActive = true;
        
        try
        {
            // Get parameters for the specific enemy and event
            _currentParams = GetBackgroundParams(enemyName, animationName);
            if (customDuration.HasValue)
            {
                _currentParams.Duration = customDuration.Value;
            }
            _lastActivationTime = now;
            
            // Create a coroutine runner if missing
            if (_coroutineRunner == null)
            {
                GameObject runnerObj = new GameObject("HSceneBlackBackgroundRunner");
                UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                _coroutineRunner = runnerObj.AddComponent<HSceneBlackBackgroundRunner>();
            }
            
            SetCameraBlackBackground();
            CreateBlackBackgroundSprite();
            HideWorldObjects(hideOverlayCanvases: true);
            ForceShowMindBrokenUI();
            MindBrokenUIPatch.RefreshLabel();              // ensure the MB label is created and active
            MindBrokenVisualEffectsSystem.Initialize();    // ensure the effects overlay is raised
            MindBrokenUIPatch.ForceShowLabelDuringBlackBackground = true; // always show the MB label on the black background
            
            // Show WombMeter during black background (pregnancy progress should remain visible)
            try { NoREroMod.Systems.Pregnancy.WombMeterHud.Ensure(); } catch { }
            StartRestoreTimer(_currentParams.Duration);
            StartMindBrokenTick();
            
            // Plugin.Log?.LogInfo($"[HScene Black Background] ✅ Activated (enemy: {_currentParams.EnemyName}, anim: {_currentParams.AnimationName}, color: {_currentParams.BaseColor}, pulse: {_currentParams.UsePulse})"); // Disabled for release

            // Start the pulse effect if needed
            if (_currentParams.UsePulse && _blackBackgroundObject != null)
            {
                SpriteRenderer spriteRenderer = _blackBackgroundObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && _coroutineRunner != null)
                {
                    _colorEffectCoroutine = _coroutineRunner.StartCoroutine(PulseColorEffect(spriteRenderer));
                }
            }
            
            // Invoke event for subscribers
            if (_blackBackgroundObject != null)
            {
                OnActivated?.Invoke(_blackBackgroundObject, _currentParams.Duration);
            }
        }
        catch (Exception ex)
        {
            Deactivate();
        }
    }
    
    /// <summary>
    /// Color pulse effect of background color
    /// </summary>
    private static IEnumerator PulseColorEffect(SpriteRenderer spriteRenderer)
    {
        float elapsed = 0f;
        
        while (_isActive && elapsed < _currentParams.Duration)
        {
            elapsed += Time.deltaTime;
            
            // Pulse between BaseColor and PulseColor
            float t = (Mathf.Sin(elapsed * _currentParams.PulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
            Color currentColor = Color.Lerp(_currentParams.BaseColor, _currentParams.PulseColor, t);
            spriteRenderer.color = currentColor;
            
            yield return null;
        }
        
        // Restore the base color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = _currentParams.BaseColor;
        }
    }
    
    /// <summary>
    /// Same black screen as H-scene FIN: hides world + skeletons, keeps death clip + death menu UI.
    /// Held until <see cref="DeactivateForLethalTrapDeathClip"/> (no 5s auto-restore, no MB tick).
    /// </summary>
    internal static void ActivateForLethalTrapDeathClip()
    {
        if (MindBrokenBadEndSystem.IsBadEndActive)
            return;

        _lethalTrapDeathClipMode = true;
        _holdUntilManualDeactivate = true;

        if (_isActive)
        {
            HideLethalTrapDeathVisuals(restoreFirst: true);
            return;
        }

        _isActive = true;

        try
        {
            _currentParams = GetBackgroundParams("LethalTrapDeath", "CocoonClip");
            _currentParams.Duration = float.MaxValue;
            _lastActivationTime = Time.unscaledTime;

            EnsureRunner();

            SetCameraBlackBackground();
            CreateBlackBackgroundSprite();
            HideWorldObjects(hideOverlayCanvases: false);
            HideLethalTrapDeathVisuals(restoreFirst: true);

            Plugin.Log?.LogInfo("[HScene Black Background] Lethal trap death clip black screen active.");
        }
        catch (Exception)
        {
            DeactivateForLethalTrapDeathClip();
        }
    }

    internal static void RefreshLethalTrapDeathVisuals()
    {
        if (!_isActive || !_lethalTrapDeathClipMode)
            return;

        HideLethalTrapDeathVisuals(restoreFirst: false);
    }

    internal static void DeactivateForLethalTrapDeathClip()
    {
        _holdUntilManualDeactivate = false;
        _lethalTrapDeathClipMode = false;
        RestoreLethalTrapHiddenRenderers();
        Deactivate();
    }

    private static void EnsureRunner()
    {
        if (_coroutineRunner != null)
            return;

        GameObject runnerObj = new GameObject("HSceneBlackBackgroundRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObj);
        _coroutineRunner = runnerObj.AddComponent<HSceneBlackBackgroundRunner>();
    }

    /// <summary>
    /// Deactivate background and restore world
    /// </summary>
    internal static void Deactivate()
    {
        if (!_isActive) return;

        _isActive = false;
        _holdUntilManualDeactivate = false;

        // Hide black sprite first so it is always turned off even if restore throws
        HideBlackBackground();

        try
        {
            if (_colorEffectCoroutine != null && _coroutineRunner != null)
            {
                _coroutineRunner.StopCoroutine(_colorEffectCoroutine);
                _colorEffectCoroutine = null;
            }
            if (_mindBrokenCoroutine != null && _coroutineRunner != null)
            {
                _coroutineRunner.StopCoroutine(_mindBrokenCoroutine);
                _mindBrokenCoroutine = null;
            }
            RestoreLethalTrapHiddenRenderers();
            _lethalTrapDeathClipMode = false;

            MindBrokenUIPatch.ForceShowLabelDuringBlackBackground = false;
            // Note: WombMeterHud follows normal HUD visibility via HudVisibilityGate

            RestoreWorldObjects();
            RestoreCameraBackground();
            // Plugin.Log?.LogInfo("[HScene Black Background] ✅ Deactivated"); // Disabled for release

            // Invoke event for subscribers
            OnDeactivated?.Invoke();
        }
        catch (Exception ex)
        {
        }
    }

    private static void HideLethalTrapDeathVisuals(bool restoreFirst)
    {
        if (restoreFirst)
            RestoreLethalTrapHiddenRenderers();

        SkeletonAnimation[] skeletons = null;
        try
        {
            skeletons = UnityEngine.Object.FindObjectsOfType<SkeletonAnimation>();
        }
        catch (Exception) { }

        if (skeletons != null)
        {
            for (int i = 0; i < skeletons.Length; i++)
            {
                SkeletonAnimation skeleton = skeletons[i];
                if (skeleton == null || IsHellGateDeathClipHierarchy(skeleton.transform))
                    continue;

                Renderer[] renderers = skeleton.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                    DisableRendererForLethalTrap(renderers[j]);
            }
        }

        SpriteRenderer[] sprites = null;
        try
        {
            sprites = UnityEngine.Object.FindObjectsOfType(typeof(SpriteRenderer)) as SpriteRenderer[];
        }
        catch (Exception) { }

        if (sprites != null)
        {
            for (int i = 0; i < sprites.Length; i++)
                DisableRendererForLethalTrap(sprites[i]);
        }

        MeshRenderer[] meshes = null;
        try
        {
            meshes = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
        }
        catch (Exception) { }

        if (meshes != null)
        {
            for (int i = 0; i < meshes.Length; i++)
                DisableRendererForLethalTrap(meshes[i]);
        }
    }

    private static void DisableRendererForLethalTrap(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
            return;

        if (IsHellGateDeathClipHierarchy(renderer.transform))
            return;

        if (IsProtectedFromLethalTrapHide(renderer.gameObject))
            return;

        renderer.enabled = false;
        if (!_lethalTrapHiddenRenderers.Contains(renderer))
            _lethalTrapHiddenRenderers.Add(renderer);
    }

    private static bool IsHellGateDeathClipHierarchy(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            string name = current.name;
            if (name == "HellGateLethalMagicTrapDeathClip" ||
                name == "HellGateLethalMagicTrapDeathSprite" ||
                name == "HellGateLethalCocoonBoneEmptySprite")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsProtectedFromLethalTrapHide(GameObject go)
    {
        if (go == null)
            return true;

        string objName = go.name;
        if (objName == BackgroundObjectName ||
            objName == "HSceneBlackBackgroundRunner" ||
            objName.Contains("MindBroken") ||
            objName.Contains("Rage") ||
            objName.Contains("CumDisplay") ||
            objName.Contains("Corruption") ||
            objName.Contains("Recovery"))
        {
            return true;
        }

        return false;
    }

    private static void RestoreLethalTrapHiddenRenderers()
    {
        for (int i = 0; i < _lethalTrapHiddenRenderers.Count; i++)
        {
            Renderer renderer = _lethalTrapHiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        _lethalTrapHiddenRenderers.Clear();
    }

    private static void HideWorldObjects(bool hideOverlayCanvases)
    {
        _hiddenObjects.Clear();
        _hiddenCanvases.Clear();
        
        try
        {
            // Collect all H-scene objects (with SkeletonAnimation) and their children
            // IMPORTANT: include ALL SkeletonAnimation objects, even inactive (enemies in gangbang queue)
            HashSet<GameObject> hSceneObjects = new HashSet<GameObject>();
            
            // Guard against NullReferenceException when using UnityExplorer
            Spine.Unity.SkeletonAnimation[] allSkeletons = null;
            try
            {
                allSkeletons = UnityEngine.Object.FindObjectsOfType<Spine.Unity.SkeletonAnimation>();
            }
            catch (Exception) { }
            
            if (allSkeletons != null)
            {
                foreach (Spine.Unity.SkeletonAnimation skeleton in allSkeletons)
                {
                    try
                    {
                        if (skeleton != null && skeleton.gameObject != null)
                        {
                            hSceneObjects.Add(skeleton.gameObject);
                            // Also add the parent object if present
                            if (skeleton.gameObject.transform.parent != null)
                            {
                                hSceneObjects.Add(skeleton.gameObject.transform.parent.gameObject);
                            }
                            foreach (Transform child in skeleton.gameObject.GetComponentsInChildren<Transform>())
                            {
                                if (child != null && child.gameObject != null)
                                {
                                    hSceneObjects.Add(child.gameObject);
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            // Guard against NullReferenceException when using UnityExplorer
            SpriteRenderer[] allSprites = null;
            try
            {
                allSprites = UnityEngine.Object.FindObjectsOfType(typeof(SpriteRenderer)) as SpriteRenderer[];
            }
            catch (Exception) { }
            
            if (allSprites != null)
            {
                foreach (SpriteRenderer sprite in allSprites)
            {
                    try
                    {
                        if (sprite == null || sprite.gameObject == null) continue;
                        if (hSceneObjects.Contains(sprite.gameObject)) continue;
                        
                        string objName = sprite.gameObject.name;
                        if (objName == BackgroundObjectName ||
                            objName.Contains("MindBroken") ||
                            objName.Contains("Rage") ||
                            objName.Contains("CumDisplay") ||
                            objName.Contains("Corruption") ||
                            objName.Contains("Recovery") ||
                            objName == "HSceneBlackBackgroundRunner" ||
                            objName.Contains("HellGateLethal"))
                        {
                            continue;
                        }
                        
                        // Do not hide objects with physics components (Collider, Rigidbody, etc.)
                        if (sprite.gameObject.GetComponent<Collider>() != null ||
                            sprite.gameObject.GetComponent<Collider2D>() != null ||
                            sprite.gameObject.GetComponent<Rigidbody>() != null ||
                            sprite.gameObject.GetComponent<Rigidbody2D>() != null)
                        {
                            continue; // Important physics objects — do not hide
                        }
                        
                        // Do not hide objects with important tags
                        string tag = sprite.gameObject.tag;
                        if (tag == "Ground" || tag == "Floor" || tag == "Platform" || 
                            tag == "Player" || tag == "Enemy" || tag == "MainCamera")
                        {
                            continue; // Important objects — do not hide
                        }
                        
                        // Hide all visual objects except system ones
                        // (physics components and tags already checked above)
                        if (sprite.gameObject.activeSelf)
                        {
                            _hiddenObjects.Add(sprite.gameObject);
                            sprite.gameObject.SetActive(false);
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            // Guard against NullReferenceException when using UnityExplorer
            MeshRenderer[] allMeshRenderers = null;
            try
            {
                allMeshRenderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            }
            catch (Exception) { }
            
            if (allMeshRenderers != null)
            {
                foreach (MeshRenderer meshRenderer in allMeshRenderers)
            {
                    try
                    {
                        if (meshRenderer == null || meshRenderer.gameObject == null) continue;
                        
                        // Skip H-scene objects (including inactive enemies in the queue)
                        if (hSceneObjects.Contains(meshRenderer.gameObject))
                        {
                            continue;
                        }
                        
                        // Also check whether this object or its parents have SkeletonAnimation
                        bool hasSkeletonAnimation = false;
                        Transform current = meshRenderer.gameObject.transform;
                        while (current != null)
                        {
                            try
                            {
                                if (current.GetComponent<Spine.Unity.SkeletonAnimation>() != null)
                                {
                                    hasSkeletonAnimation = true;
                                    break;
                                }
                            }
                            catch (Exception) { break; }
                            current = current.parent;
                        }
                        if (hasSkeletonAnimation)
                        {
                            continue; // Do not hide objects with SkeletonAnimation (enemies in queue)
                        }
                        
                        string objName = meshRenderer.gameObject.name;
                        if (objName == BackgroundObjectName ||
                            objName.Contains("MindBroken") ||
                            objName.Contains("Rage") ||
                            objName.Contains("CumDisplay") ||
                            objName.Contains("Corruption") ||
                            objName.Contains("Recovery") ||
                            objName == "HSceneBlackBackgroundRunner" ||
                            objName.Contains("HellGateLethal"))
                        {
                            continue;
                        }
                        
                        // Do not hide objects with physics components (Collider, Rigidbody, etc.)
                        if (meshRenderer.gameObject.GetComponent<Collider>() != null ||
                            meshRenderer.gameObject.GetComponent<Collider2D>() != null ||
                            meshRenderer.gameObject.GetComponent<Rigidbody>() != null ||
                            meshRenderer.gameObject.GetComponent<Rigidbody2D>() != null)
                        {
                            continue; // Important physics objects — do not hide
                        }
                        
                        // Do not hide objects with important tags
                        string tag = meshRenderer.gameObject.tag;
                        if (tag == "Ground" || tag == "Floor" || tag == "Platform" || 
                            tag == "Player" || tag == "Enemy" || tag == "MainCamera")
                        {
                            continue; // Important objects — do not hide
                        }
                        
                        // Hide all visual objects except system ones
                        // (physics components and tags already checked above)
                        if (meshRenderer.gameObject.activeSelf)
                        {
                            _hiddenObjects.Add(meshRenderer.gameObject);
                            meshRenderer.gameObject.SetActive(false);
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            // Guard against NullReferenceException when using UnityExplorer
            Canvas[] allCanvases = null;
            try
            {
                allCanvases = UnityEngine.Object.FindObjectsOfType(typeof(Canvas)) as Canvas[];
            }
            catch (Exception) { }
            
            if (hideOverlayCanvases && allCanvases != null)
            {
                foreach (Canvas canvas in allCanvases)
            {
                    try
                    {
                        if (canvas == null || canvas.gameObject == null) continue;
                        
                        string canvasName = canvas.gameObject.name;
                        if (canvasName == BackgroundObjectName ||
                            canvasName == "MindBrokenBadEndCanvas" ||
                            canvasName == "RageOverlayCanvas" ||
                            canvasName == "MindBrokenOverlayCanvas" ||
                            canvasName == "MindBrokenVisualEffectsCanvas" ||
                            canvasName == "RageComboCanvas" ||
                            canvasName == "RageComboBloodCanvas" ||
                            canvasName == "DialogueSystemCanvas" ||
                            canvasName == "CanvasBadstatusinfo" || // base Canvas for the MB label
                            canvasName == "CorruptionCaptionsCanvas" ||
                            canvasName == "RecoveryCaptionsCanvas" ||
                            canvasName == "QTECanvas3" || // QTE system — do not hide
                            canvasName == "WombMeterHudCanvas" || // Pregnancy Womb Meter HUD
                            canvasName == "UIeffect" || // NoREroMod orgasm white flash (GameObject.Find)
                            canvasName.Contains("Badstatus") ||
                            canvasName.Contains("Dialogue") ||
                            canvasName.Contains("MindBroken") ||
                            canvasName.Contains("CumDisplay") ||
                            canvasName.Contains("Corruption") ||
                            canvasName.Contains("Recovery") ||
                            canvasName.Contains("QTE") ||
                            canvasName.Contains("WombMeter")) // Any Canvas with WombMeter in the name
                        {
                            continue;
                        }
                        
                        if (canvas.gameObject.activeSelf && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            _hiddenCanvases.Add(canvas);
                            canvas.gameObject.SetActive(false);
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            // Plugin.Log?.LogInfo($"[HScene Black Background] Hidden {_hiddenObjects.Count} objects, {_hiddenCanvases.Count} canvases"); // Disabled for release
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void SetCameraBlackBackground()
    {
        try
        {
            _mainCamera = UnityEngine.Camera.main;
            if (_mainCamera != null)
            {
                // Save original settings
                _originalClearColor = _mainCamera.backgroundColor;
                _originalClearFlags = _mainCamera.clearFlags;
                
                _mainCamera.backgroundColor = Color.black;
                _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            }
            else
            {
                // Plugin.Log?.LogWarning("[HScene Black Background] Main camera not found!"); // Disabled for release
            }
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void RestoreCameraBackground()
    {
        try
        {
            if (_mainCamera != null)
            {
                if (_originalClearColor.HasValue)
                {
                    _mainCamera.backgroundColor = _originalClearColor.Value;
                }
                if (_originalClearFlags.HasValue)
                {
                    _mainCamera.clearFlags = _originalClearFlags.Value;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }
    
    
    private static void RestoreWorldObjects()
    {
        try
        {
            foreach (Canvas canvas in _hiddenCanvases)
            {
                if (canvas != null && canvas.gameObject != null)
                {
                    canvas.gameObject.SetActive(true);
                }
            }
            
            foreach (GameObject obj in _hiddenObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
            
            _hiddenCanvases.Clear();
            _hiddenObjects.Clear();
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void CreateBlackBackgroundSprite()
    {
        try
        {
            if (_blackBackgroundObject != null)
            {
                // Update background color if it changed
                SpriteRenderer existingRenderer = _blackBackgroundObject.GetComponent<SpriteRenderer>();
                if (existingRenderer != null)
                {
                    existingRenderer.color = _currentParams.BaseColor;
                }
                _blackBackgroundObject.SetActive(true);
                return;
            }
            
            _blackBackgroundObject = new GameObject(BackgroundObjectName);
            
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            float spriteWidth = 1000f;
            float spriteHeight = 1000f;
            Vector3 spritePosition = Vector3.zero;
            
            if (mainCamera != null)
            {
                float height = mainCamera.orthographicSize * 2f;
                float width = height * mainCamera.aspect;
                spriteWidth = width * 1.5f;
                spriteHeight = height * 1.5f;
                spritePosition = mainCamera.transform.position;
                spritePosition.z += 10f;
            }
            
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            
            SpriteRenderer spriteRenderer = _blackBackgroundObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            // Use color from current parameters (or black by default)
            spriteRenderer.color = _currentParams.BaseColor;
            
            int hSceneSortingOrder;
            string hSceneLayer;
            GetHSceneSortingInfo(out hSceneSortingOrder, out hSceneLayer);
            
            spriteRenderer.sortingLayerName = hSceneLayer;
            spriteRenderer.sortingOrder = Math.Min(hSceneSortingOrder - 100, -5000);
            
            _blackBackgroundObject.transform.localScale = new Vector3(spriteWidth / 100f, spriteHeight / 100f, 1f);
            _blackBackgroundObject.transform.position = spritePosition;
            
            UnityEngine.Object.DontDestroyOnLoad(_blackBackgroundObject);
            _blackBackgroundObject.SetActive(true);
            
            // Detailed debug logging — disabled for release
            // Plugin.Log?.LogInfo($"[HScene Black Background] Black sprite created:");
            // Plugin.Log?.LogInfo($"  - Position: {spritePosition}");
            // Plugin.Log?.LogInfo($"  - Scale: {_blackBackgroundObject.transform.localScale}");
            // Plugin.Log?.LogInfo($"  - SortingLayer: {hSceneLayer}, SortingOrder: {spriteRenderer.sortingOrder} (H-scene: {hSceneSortingOrder})");
            // Plugin.Log?.LogInfo($"  - Active: {_blackBackgroundObject.activeSelf}, Sprite.enabled: {spriteRenderer.enabled}");
            // Plugin.Log?.LogInfo($"  - Color: {spriteRenderer.color}");
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void GetHSceneSortingInfo(out int sortingOrder, out string sortingLayer)
    {
        try
        {
            // Find the minimum sortingOrder among all H-scenes (SkeletonAnimation)
            int minSortingOrder = int.MaxValue;
            string targetLayer = "Default";
            bool found = false;
            
            // Guard against NullReferenceException when using UnityExplorer
            Spine.Unity.SkeletonAnimation[] allSkeletons = null;
            try
            {
                allSkeletons = UnityEngine.Object.FindObjectsOfType<Spine.Unity.SkeletonAnimation>();
            }
            catch (Exception) { }
            
            if (allSkeletons != null)
            {
                foreach (Spine.Unity.SkeletonAnimation skeleton in allSkeletons)
                {
                    try
                    {
                        if (skeleton != null && skeleton.gameObject != null)
                        {
                            // Spine renders via MeshRenderer
                            MeshRenderer meshRenderer = skeleton.GetComponent<MeshRenderer>();
                            if (meshRenderer != null)
                            {
                                int order = meshRenderer.sortingOrder;
                                string layer = meshRenderer.sortingLayerName ?? "Default";
                                
                                if (!found || order < minSortingOrder)
                                {
                                    minSortingOrder = order;
                                    targetLayer = layer;
                                    found = true;
                                }
                            }
                            
                            // Also check child objects
                            foreach (Transform child in skeleton.gameObject.GetComponentsInChildren<Transform>())
                            {
                                try
                                {
                                    if (child != null && child.gameObject != null)
                                    {
                                        MeshRenderer childRenderer = child.GetComponent<MeshRenderer>();
                                        if (childRenderer != null)
                                        {
                                            int order = childRenderer.sortingOrder;
                                            string layer = childRenderer.sortingLayerName ?? "Default";
                                            
                                            if (!found || order < minSortingOrder)
                                            {
                                                minSortingOrder = order;
                                                targetLayer = layer;
                                                found = true;
                                            }
                                        }
                                    }
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            if (found)
            {
                sortingOrder = minSortingOrder;
                sortingLayer = targetLayer;
            }
            else
            {
                sortingOrder = -1000;
                sortingLayer = "Default";
            }
        }
        catch (Exception ex)
        {
            sortingOrder = -1000;
            sortingLayer = "Default";
        }
    }
    
    private static void HideBlackBackground()
    {
        if (_blackBackgroundObject != null)
        {
            _blackBackgroundObject.SetActive(false);
        }
    }
    
    private static void StartRestoreTimer(float duration = EFFECT_DURATION)
    {
        if (_holdUntilManualDeactivate)
            return;

        // Create or reuse the runner
        if (_coroutineRunner == null)
        {
            GameObject runnerObj = new GameObject("HSceneBlackBackgroundRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _coroutineRunner = runnerObj.AddComponent<HSceneBlackBackgroundRunner>();
        }
        else
        {
            // Stop only the previous restore, without destroying the runner
            if (_restoreCoroutine != null)
            {
                (_coroutineRunner as HSceneBlackBackgroundRunner)?.StopCoroutine(_restoreCoroutine);
                _restoreCoroutine = null;
            }
        }
        
        if (_coroutineRunner is HSceneBlackBackgroundRunner runner)
        {
            _restoreCoroutine = runner.StartCoroutine(RestoreAfterDelay(duration));
        }
    }

    private static void ForceShowMindBrokenUI()
    {
        try
        {
            Canvas[] allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in allCanvases)
            {
                if (canvas == null || canvas.gameObject == null) continue;
                string name = canvas.gameObject.name;
                if (name.Contains("MindBroken") || name.Contains("Badstatus") || name.Contains("WombMeter"))
                {
                    canvas.gameObject.SetActive(true);
                    _hiddenCanvases.Remove(canvas); // ensure it does not remain in the hidden list
                }
                // Also raise overlay canvases for MB / Rage if layer/sorting was reset
                if (name == "MindBrokenOverlayCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 2000; // raised higher so it is not covered by the fade
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                if (name == "MindBrokenVisualEffectsCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1999; // below MindBrokenLabel, above Rage/others
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                if (name == "WombMeterHudCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1800; // between MindBroken (2000) and Rage (1500)
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                if (name == "RageOverlayCanvas" || name == "RageComboCanvas" || name == "RageComboBloodCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1500; // below MB
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
            // Force-refresh the label (in case alpha/color were reset)
            try { MindBrokenUIPatch.RefreshLabel(); } catch {}
        }
        catch (Exception ex)
        {
        }
    }

    private static void StartMindBrokenTick()
    {
        if (_coroutineRunner is HSceneBlackBackgroundRunner runner)
        {
            // Stop the previous one if it was running
            if (_mindBrokenCoroutine != null)
            {
                runner.StopCoroutine(_mindBrokenCoroutine);
                _mindBrokenCoroutine = null;
            }
            _mindBrokenCoroutine = runner.StartCoroutine(MindBrokenTick());
        }
    }
    
    private static IEnumerator RestoreAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        Deactivate();
    }

    private static IEnumerator MindBrokenTick()
    {
        // MindBroken growth while black background is active (percent-per-second from config)
        while (_isActive)
        {
            if (MindBrokenSystem.Enabled)
            {
                // Uses unscaledDeltaTime, so MindBroken growth is independent of SlowMo timeScale.
                float perSecondPercent = Plugin.hsceneBlackBackgroundMindBrokenPerSecondPercent?.Value ?? 0.2f;
                MindBrokenSystem.AddPercent((perSecondPercent / 100f) * Time.unscaledDeltaTime, "black-bg");
            }
            yield return null;
        }
    }
    
    private class HSceneBlackBackgroundRunner : MonoBehaviour { }
}
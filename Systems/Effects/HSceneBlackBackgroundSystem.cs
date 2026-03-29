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
/// HSceneBlackBackgroundSystem - Система черного background for H-scenes
/// 
/// MECHANICS:
/// - On grab (START se_count==1) hides entire world
/// - Leaves only H-сцену (enemy + player in animation)
/// - Creates black background on full screen
/// - Holds effect 5 секунд
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
    private const float EFFECT_DURATION = 5f; // фикс: держим 5 сек for всех триггеров
    
    // Список скрытых объектоin for восстановления
    private static readonly List<GameObject> _hiddenObjects = new List<GameObject>();
    private static readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    
    // Сохраненный clear color и flags камеры
    private static Color? _originalClearColor;
    private static CameraClearFlags? _originalClearFlags;
    private static UnityEngine.Camera? _mainCamera;
    
    // Текущие параметры фона
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
    
    // ========== СИСТЕМА КАСТОМНЫХ ПАРАМЕТРОВ ==========
    
    /// <summary>
    /// Параметры background for different events и enemies
    /// </summary>
    public struct BackgroundParams
    {
        public Color BaseColor;           // Базовый цвет фона
        public Color PulseColor;          // Цвет for пульсации (if используется)
        public bool UsePulse;             // Использовать пульсацию
        public float PulseSpeed;          // Скорость пульсации
        public float Duration;            // Длительность эффекта
        public string EnemyName;          // Имя enemy (for фильтрации)
        public string AnimationName;      // Имя animation (for фильтрации)
    }
    
    /// <summary>
    /// Get параметры background for specific enemy и events
    /// </summary>
    private static BackgroundParams GetBackgroundParams(string enemyName, string animationName)
    {
        // Нормализуем имя enemy
        string normalizedEnemyName = enemyName ?? "Unknown";
        
        // By умолчанию - черный фон on 5 секунд
        return new BackgroundParams
        {
            BaseColor = Color.black,
            PulseColor = Color.black,
            UsePulse = false,
            PulseSpeed = 0f,
            Duration = EFFECT_DURATION, // 5 секунд for FIN
            EnemyName = normalizedEnemyName,
            AnimationName = animationName ?? "Unknown"
        };
    }
    
    // ========== СИСТЕМА ПОДПИСОК ==========
    
    /// <summary>
    /// Activation event черного фона
    /// Параметры: GameObject черного фона, effect duration
    /// </summary>
    public static event Action<GameObject, float>? OnActivated;
    
    /// <summary>
    /// Deactivation event черного фона
    /// </summary>
    public static event Action? OnDeactivated;
    
    /// <summary>
    /// Get GameObject черного background (for добавления effects of type X-Ray)
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
    /// Activate background for H-scenesы with custom parameters
    /// </summary>
    internal static void Activate(string enemyName = null, string animationName = null, float? customDuration = null, bool useSmoothFade = false)
    {
        if (MindBrokenBadEndSystem.IsBadEndActive) return;
        // Блокируем повторный старт, if прошло меньше EFFECT_DURATION c предыдущits включения
        float now = Time.unscaledTime;
        float durationCheck = customDuration ?? EFFECT_DURATION;
        if (now - _lastActivationTime < durationCheck - 0.05f)
        {
            // Plugin.Log?.LogInfo($"[HScene Black Background] Skipped: cooldown active (last {_lastActivationTime}, now {now})"); // Disabled for release
            return;
        }
        
        // If фон already активен — ignore повторный call (таймер not перезапускаем)
        if (_isActive)
        {
            // Plugin.Log?.LogInfo("[HScene Black Background] Already active, skip re-activate"); // Disabled for release
            return;
        }
        
        _isActive = true;
        
        try
        {
            // Get параметры for specific enemy и events
            _currentParams = GetBackgroundParams(enemyName, animationName);
            if (customDuration.HasValue)
            {
                _currentParams.Duration = customDuration.Value;
            }
            _lastActivationTime = now;
            
            // Создаем coroutine runner if its нет
            if (_coroutineRunner == null)
            {
                GameObject runnerObj = new GameObject("HSceneBlackBackgroundRunner");
                UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                _coroutineRunner = runnerObj.AddComponent<HSceneBlackBackgroundRunner>();
            }
            
            SetCameraBlackBackground();
            CreateBlackBackgroundSprite();
            HideWorldObjects();
            ForceShowMindBrokenUI();
            MindBrokenUIPatch.RefreshLabel();              // гарантируем, that MB-лейбл создан и активен
            MindBrokenVisualEffectsSystem.Initialize();    // гарантируем, that оверлей effects поднят
            MindBrokenUIPatch.ForceShowLabelDuringBlackBackground = true; // always показываем MB лейбл on черном фоне
            StartRestoreTimer(_currentParams.Duration);
            StartMindBrokenTick();
            
            // Plugin.Log?.LogInfo($"[HScene Black Background] ✅ Activated (enemy: {_currentParams.EnemyName}, anim: {_currentParams.AnimationName}, color: {_currentParams.BaseColor}, pulse: {_currentParams.UsePulse})"); // Disabled for release

            // Запускаем эффект пульсации, if нужно
            if (_currentParams.UsePulse && _blackBackgroundObject != null)
            {
                SpriteRenderer spriteRenderer = _blackBackgroundObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && _coroutineRunner != null)
                {
                    _colorEffectCoroutine = _coroutineRunner.StartCoroutine(PulseColorEffect(spriteRenderer));
                }
            }
            
            // Invoke event for подписчиков
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
            
            // Пульсация между BaseColor и PulseColor
            float t = (Mathf.Sin(elapsed * _currentParams.PulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
            Color currentColor = Color.Lerp(_currentParams.BaseColor, _currentParams.PulseColor, t);
            spriteRenderer.color = currentColor;
            
            yield return null;
        }
        
        // Возвращаем базовый цвет
        if (spriteRenderer != null)
        {
            spriteRenderer.color = _currentParams.BaseColor;
        }
    }
    
    /// <summary>
    /// Deactivate background and restore world
    /// </summary>
    internal static void Deactivate()
    {
        if (!_isActive) return;
        
        _isActive = false;
        
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
            MindBrokenUIPatch.ForceShowLabelDuringBlackBackground = false;
            
            RestoreWorldObjects();
            RestoreCameraBackground();
            // Plugin.Log?.LogInfo("[HScene Black Background] ✅ Deactivated"); // Disabled for release
            
            // Invoke event for подписчиков
            OnDeactivated?.Invoke();
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void HideWorldObjects()
    {
        _hiddenObjects.Clear();
        _hiddenCanvases.Clear();
        
        try
        {
            // Собираем все объекты H-scene (with SkeletonAnimation) и their дочерние объекты
            // IMPORTANT: Включаем ВСЕ SkeletonAnimation, даже неактивные (enemyи in очереди гангбанг)
            HashSet<GameObject> hSceneObjects = new HashSet<GameObject>();
            
            // Защита from NullReferenceException on работе with UnityExplorer
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
                            // Также добавляем parent object, if exists
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
            
            // Защита from NullReferenceException on работе with UnityExplorer
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
                            objName == "HSceneBlackBackgroundRunner")
                        {
                            continue;
                        }
                        
                        // DO NOT скрываем объекты with физическими componentами (Collider, Rigidbody etc.)
                        if (sprite.gameObject.GetComponent<Collider>() != null ||
                            sprite.gameObject.GetComponent<Collider2D>() != null ||
                            sprite.gameObject.GetComponent<Rigidbody>() != null ||
                            sprite.gameObject.GetComponent<Rigidbody2D>() != null)
                        {
                            continue; // Важные физические объекты - not скрываем
                        }
                        
                        // DO NOT скрываем объекты with важными тегами
                        string tag = sprite.gameObject.tag;
                        if (tag == "Ground" || tag == "Floor" || tag == "Platform" || 
                            tag == "Player" || tag == "Enemy" || tag == "MainCamera")
                        {
                            continue; // Важные объекты - not скрываем
                        }
                        
                        // Скрываем ВСЕ визуальные объекты, кроме системных
                        // (but already проверor физические components и теги выше)
                        if (sprite.gameObject.activeSelf)
                        {
                            _hiddenObjects.Add(sprite.gameObject);
                            sprite.gameObject.SetActive(false);
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            // Защита from NullReferenceException on работе with UnityExplorer
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
                        
                        // Skip объекты H-scene (включая неactive enemies in очереди)
                        if (hSceneObjects.Contains(meshRenderer.gameObject))
                        {
                            continue;
                        }
                        
                        // Также проверяем, есть ли у этого объекта or its родителей SkeletonAnimation
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
                            continue; // Do not скрываем объекты with SkeletonAnimation (enemyи in очереди)
                        }
                        
                        string objName = meshRenderer.gameObject.name;
                        if (objName == BackgroundObjectName ||
                            objName.Contains("MindBroken") ||
                            objName.Contains("Rage") ||
                            objName.Contains("CumDisplay") ||
                            objName.Contains("Corruption") ||
                            objName.Contains("Recovery") ||
                            objName == "HSceneBlackBackgroundRunner")
                        {
                            continue;
                        }
                        
                        // DO NOT скрываем объекты with физическими componentами (Collider, Rigidbody etc.)
                        if (meshRenderer.gameObject.GetComponent<Collider>() != null ||
                            meshRenderer.gameObject.GetComponent<Collider2D>() != null ||
                            meshRenderer.gameObject.GetComponent<Rigidbody>() != null ||
                            meshRenderer.gameObject.GetComponent<Rigidbody2D>() != null)
                        {
                            continue; // Важные физические объекты - not скрываем
                        }
                        
                        // DO NOT скрываем объекты with важными тегами
                        string tag = meshRenderer.gameObject.tag;
                        if (tag == "Ground" || tag == "Floor" || tag == "Platform" || 
                            tag == "Player" || tag == "Enemy" || tag == "MainCamera")
                        {
                            continue; // Важные объекты - not скрываем
                        }
                        
                        // Скрываем ВСЕ визуальные объекты, кроме системных
                        // (but already проверor физические components и теги выше)
                        if (meshRenderer.gameObject.activeSelf)
                        {
                            _hiddenObjects.Add(meshRenderer.gameObject);
                            meshRenderer.gameObject.SetActive(false);
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            // Защита from NullReferenceException on работе with UnityExplorer
            Canvas[] allCanvases = null;
            try
            {
                allCanvases = UnityEngine.Object.FindObjectsOfType(typeof(Canvas)) as Canvas[];
            }
            catch (Exception) { }
            
            if (allCanvases != null)
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
                            canvasName == "CanvasBadstatusinfo" || // базовый Canvas for MB-лейбла
                            canvasName == "CorruptionCaptionsCanvas" ||
                            canvasName == "RecoveryCaptionsCanvas" ||
                            canvasName == "QTECanvas3" || // QTE system - not скрываем
                            canvasName.Contains("Badstatus") ||
                            canvasName.Contains("Dialogue") ||
                            canvasName.Contains("MindBroken") ||
                            canvasName.Contains("CumDisplay") ||
                            canvasName.Contains("Corruption") ||
                            canvasName.Contains("Recovery") ||
                            canvasName.Contains("QTE")) // Любые Canvas with QTE in названии
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
                // Сохраняем оригинальные настройки
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
                // Update цвет background if он изменился
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
            // Use цвет from текущtheir параметроin (or черный by умолчанию)
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
            
            // Detailed logging for debugging - отключеbut for релиза
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
            // Находим минимальный sortingOrder среди всех H-сцен (SkeletonAnimation)
            int minSortingOrder = int.MaxValue;
            string targetLayer = "Default";
            bool found = false;
            
            // Защита from NullReferenceException on работе with UnityExplorer
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
                            // Spine рендерится через MeshRenderer
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
                            
                            // Также проверяем дочерние объекты
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
        // Создаем/переиспользуем runner
        if (_coroutineRunner == null)
        {
            GameObject runnerObj = new GameObject("HSceneBlackBackgroundRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _coroutineRunner = runnerObj.AddComponent<HSceneBlackBackgroundRunner>();
        }
        else
        {
            // Stop only предыдущий restore, without разрушения runner
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
                if (name.Contains("MindBroken") || name.Contains("Badstatus"))
                {
                    canvas.gameObject.SetActive(true);
                    _hiddenCanvases.Remove(canvas); // гарантируем, that not останется in списке скрытых
                }
                // Дополнительbut поднимаем overlay canvas for MB / Rage, if слой/сортинг сброшен
                if (name == "MindBrokenOverlayCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 2000; // подняли выше, to avoid перекрывал fade
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                if (name == "MindBrokenVisualEffectsCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1999; // ниже MindBrokenLabel, выше Rage/прочего
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                if (name == "RageOverlayCanvas" || name == "RageComboCanvas" || name == "RageComboBloodCanvas")
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1500; // ниже MB
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
            // Форс-обновляем лейбл (in case alpha/цвет сброшены)
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
            // Stop предыдущую, if была
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
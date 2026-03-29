using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using Spine;
using Spine.Unity;

namespace NoREroMod.Systems.HSceneEffects;

/// <summary>
/// Custom ISizeOverrider that overrides ProCamera2DZoomToFitTargets during zoom animation.
/// ProCamera2DZoomToFitTargets recalculates _targetCamSize every frame in UpdateTargetCamSize(),
/// overwriting our values. This overrider runs last (high SOOrder) and returns our desired size.
/// </summary>
internal class ZoomEffectSizeOverrider : ISizeOverrider
{
    public float TargetSize { get; set; }
    public int SOOrder { get; set; } = 9999; // Run last to override fitzoom
    
    public float OverrideSize(float deltaTime, float originalSize) => TargetSize;
}

internal class HSceneStartZoomEffect
{
    private static bool _wasHSceneActive = false;
    private static bool _effectActive = false;
    private static Coroutine _effectCoroutine;
    private static MonoBehaviour _coroutineRunner;
    
    private static ProCamera2DZoomToFitTargets _fitzoom;
    private static ProCamera2D _proCamera2D;
    private static ZoomEffectSizeOverrider _sizeOverrider;
    private static float _originalTimeScale = 1f;
    
    private static GameObject _centerTargetObject;
    private static Transform _centerTargetTransform;
    
    private static FieldInfo _targetCamSizeField;
    private static FieldInfo _initialCamSizeField;
    private static FieldInfo _targetCamSizeSmoothedField;
    
    internal static void Initialize()
    {
        if (!(Plugin.enableStartZoomEffect?.Value ?? true))
        {
            return;
        }
        
        GameObject runnerObj = new GameObject("HSceneStartZoomEffectRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObj);
        _coroutineRunner = runnerObj.AddComponent<ZoomEffectCoroutineRunner>();
        
        InitializeReflection();
    }
    
    private static void InitializeReflection()
    {
        if (_targetCamSizeField != null)
        {
            return;
        }
        
        var type = typeof(ProCamera2DZoomToFitTargets);
        _targetCamSizeField = type.GetField("_targetCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        _initialCamSizeField = type.GetField("_initialCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        _targetCamSizeSmoothedField = type.GetField("_targetCamSizeSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);
    }
    
    private static void FindFitzoom(playercon playerCon)
    {
        if (_fitzoom != null && _proCamera2D != null)
        {
            return;
        }
        
        try
        {
            CameraCache.InitializePlayerconReflection();
            if (CameraCache.FitzoomField != null)
            {
                _fitzoom = CameraCache.FitzoomField.GetValue(playerCon) as ProCamera2DZoomToFitTargets;
            }
            
            // Optimization: Используем кэшированную камеру
            _proCamera2D = NoREroMod.Systems.Cache.UnifiedCameraCacheManager.GetProCamera2D();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Failed to find fitzoom/camera: {ex.Message}");
        }
    }
    
    private static Vector3 GetAnimationCenter(playercon playerCon)
    {
        Vector3 playerCenter = playerCon.transform.position;
        Vector3 enemyCenter = Vector3.zero;
        bool enemyFound = false;
        
        try
        {
            GameObject playerObj = playerCon.gameObject;
            SkeletonAnimation playerSpine = playerObj.GetComponentInChildren<SkeletonAnimation>();
            if (playerSpine != null && playerSpine.skeleton != null && playerSpine.skeleton.RootBone != null)
            {
                Bone rootBone = playerSpine.skeleton.RootBone;
                Vector3 playerBonePos = playerObj.transform.TransformPoint(rootBone.WorldX, rootBone.WorldY, 0f);
                playerCenter = playerBonePos;
            }
            
            // Optimization: Получаем текущits enemy from QTESystem instead of поиска всех enemies!
            object currentEnemy = QTESystem.GetCurrentEnemyInstance();
            if (currentEnemy != null && currentEnemy is MonoBehaviour enemyMB)
            {
                GameObject enemyObj = enemyMB.gameObject;
                EnemyDate enemyDate = enemyObj.GetComponent<EnemyDate>();
                
                if (enemyDate != null && enemyDate.erodata != null && enemyDate.erodata.activeInHierarchy)
                {
                    enemyCenter = enemyObj.transform.position;
                    SkeletonAnimation enemySpine = enemyObj.GetComponentInChildren<SkeletonAnimation>();
                    if (enemySpine != null && enemySpine.skeleton != null && enemySpine.skeleton.RootBone != null)
                    {
                        Bone rootBone = enemySpine.skeleton.RootBone;
                        Vector3 enemyBonePos = enemyObj.transform.TransformPoint(rootBone.WorldX, rootBone.WorldY, 0f);
                        enemyCenter = enemyBonePos;
                    }
                    enemyFound = true;
                }
            }
            
            // Fallback: if QTESystem not вернул enemy, ищем manually (редкий случай)
            // Ограничеbut 10 enemyми for производительности (FindGameObjectsWithTag дорогой)
            if (!enemyFound)
            {
                GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
                const int maxFallbackCheck = 10;
                for (int i = 0; i < Mathf.Min(enemies?.Length ?? 0, maxFallbackCheck); i++)
                {
                    GameObject enemyObj = enemies[i];
                    if (enemyObj == null) continue;
                    EnemyDate enemyDate = enemyObj.GetComponent<EnemyDate>();
                    if (enemyDate != null && enemyDate.erodata != null && enemyDate.erodata.activeInHierarchy)
                    {
                        enemyCenter = enemyObj.transform.position;
                        SkeletonAnimation enemySpine = enemyObj.GetComponentInChildren<SkeletonAnimation>();
                        if (enemySpine != null && enemySpine.skeleton != null && enemySpine.skeleton.RootBone != null)
                        {
                            Bone rootBone = enemySpine.skeleton.RootBone;
                            Vector3 enemyBonePos = enemyObj.transform.TransformPoint(rootBone.WorldX, rootBone.WorldY, 0f);
                            enemyCenter = enemyBonePos;
                        }
                        enemyFound = true;
                        break;
                    }
                }
            }
            
            if (!enemyFound)
            {
                return playerCenter;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error calculating center: {ex.Message}");
            return playerCenter;
        }
        
        Vector3 center = (playerCenter + enemyCenter) / 2f;
        float yOffset = (enemyCenter.y - playerCenter.y) * 0.1f;
        center.y += yOffset;
        center.y += Plugin.startCenterYOffset?.Value ?? 0f;
        
        return center;
    }
    
    private static IEnumerator CenterCameraSmooth(Vector3 targetPosition, float duration)
    {
        if (_proCamera2D == null)
        {
            yield break;
        }
        
        if (_centerTargetObject == null)
        {
            _centerTargetObject = new GameObject("HSceneCenterTarget");
            _centerTargetTransform = _centerTargetObject.transform;
            UnityEngine.Object.DontDestroyOnLoad(_centerTargetObject);
        }
        
        Vector3 startPosition = _proCamera2D.LocalPosition;
        Vector3 endPosition = new Vector3(targetPosition.x, targetPosition.y, _proCamera2D.LocalPosition.z);
        
        _centerTargetTransform.position = startPosition;
        _proCamera2D.AddCameraTarget(_centerTargetTransform, 1f, 1f, 0f, Vector2.zero);
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            
            try
            {
                if (_centerTargetTransform == null)
                {
                    yield break;
                }
                _centerTargetTransform.position = Vector3.Lerp(startPosition, endPosition, t);
            }
            catch (Exception ex)
            {
                // Disabled: too many ошибок, not критично
                // Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error centering camera: {ex.Message}");
                yield break;
            }
            
            yield return null;
        }
        
        try
        {
            _centerTargetTransform.position = endPosition;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error setting final camera position: {ex.Message}");
        }
    }
    
    internal static void RemoveCenterTarget()
    {
        if (_centerTargetTransform != null && _proCamera2D != null)
        {
            try
            {
                _proCamera2D.RemoveCameraTarget(_centerTargetTransform, 0f);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error removing center target: {ex.Message}");
            }
        }
        
        if (_centerTargetObject != null)
        {
            UnityEngine.Object.Destroy(_centerTargetObject);
            _centerTargetObject = null;
            _centerTargetTransform = null;
        }
    }
    
    private static void StartZoomEffect(playercon playerCon)
    {
        if (_effectActive)
        {
            return;
        }
        
        FindFitzoom(playerCon);
        
        if (_fitzoom == null)
        {
            Plugin.Log?.LogWarning("[HSceneEffects.StartZoom] Cannot start effect: fitzoom not found");
            return;
        }
        
        _effectActive = true;
        _originalTimeScale = Time.timeScale;
        
        if (_effectCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_effectCoroutine);
        }
        
        _effectCoroutine = _coroutineRunner.StartCoroutine(ZoomEffectCoroutine());
    }
    
    private static IEnumerator ZoomEffectCoroutine()
    {
        float zoomAmount = Plugin.startZoomAmount?.Value ?? 3.0f;
        float zoomDuration = Plugin.startZoomDuration?.Value ?? 0.03f;
        float centerDuration = Plugin.startCenterDuration?.Value ?? 1.0f;
        
        if (_initialCamSizeField == null)
        {
            _effectActive = false;
            _effectCoroutine = null;
            yield break;
        }
        
        if (Plugin.enableStartCenter?.Value ?? true)
        {
            // Optimization: use cached playercon
            playercon playerCon = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayer();
            if (playerCon != null)
            {
                Vector3 center = GetAnimationCenter(playerCon);
                yield return _coroutineRunner.StartCoroutine(CenterCameraSmooth(center, centerDuration));
            }
        }
        
        // 1. СНАЧАЛА быстрый зум (in 2 раза быстрее)
        float effectiveZoomDuration = zoomDuration * 0.5f;
        float initialCamSize;
        float startCamSize;
        float targetCamSize;
        
        try
        {
            initialCamSize = (float)_initialCamSizeField.GetValue(_fitzoom);
            startCamSize = initialCamSize;
            targetCamSize = initialCamSize / zoomAmount;
            
            _fitzoom.MaxZoomInAmount = zoomAmount;
            _fitzoom.MaxZoomOutAmount = zoomAmount;
            _fitzoom.enabled = true;
            _fitzoom.DisableWhenOneTarget = false;
            
            if (_proCamera2D != null && _sizeOverrider == null)
            {
                _sizeOverrider = new ZoomEffectSizeOverrider { TargetSize = startCamSize };
                _proCamera2D.AddSizeOverrider(_sizeOverrider);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error initializing zoom: {ex.Message}\n{ex.StackTrace}");
            Time.timeScale = 1f;
            _effectActive = false;
            _effectCoroutine = null;
            RemoveSizeOverrider();
            yield break;
        }
        
        float elapsed = 0f;
        while (elapsed < effectiveZoomDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / effectiveZoomDuration);
            t = t * t * (3f - 2f * t);
            float currentCamSize = Mathf.Lerp(startCamSize, targetCamSize, t);
            
            if (_sizeOverrider != null)
            {
                _sizeOverrider.TargetSize = currentCamSize;
            }
            
            yield return null;
        }
        
        if (_sizeOverrider != null)
        {
            _sizeOverrider.TargetSize = targetCamSize;
        }
        
        // Only зум и центрирование — without замедления (slowmo перенесён in GrabViaAttackPatch)
        
        try
        {
            Time.timeScale = 1f;
            _effectActive = false;
            _effectCoroutine = null;
            RemoveSizeOverrider();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error ending effect: {ex.Message}\n{ex.StackTrace}");
            Time.timeScale = 1f;
            _effectActive = false;
            _effectCoroutine = null;
            RemoveSizeOverrider();
        }
    }
    
    private static void RemoveSizeOverrider()
    {
        if (_sizeOverrider != null && _proCamera2D != null)
        {
            try
            {
                _proCamera2D.RemoveSizeOverrider(_sizeOverrider);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[HSceneEffects.StartZoom] Error removing size overrider: {ex.Message}");
            }
            _sizeOverrider = null;
        }
    }
    
    internal static void CheckHSceneStart(playercon playerCon)
    {
        if (!(Plugin.enableStartZoomEffect?.Value ?? true))
        {
            return;
        }
        
        bool isHSceneActive = playerCon.erodown != 0 && playerCon.eroflag;
        
        if (isHSceneActive && !_wasHSceneActive && !_effectActive)
        {
            var controller = HSceneCameraController.Instance;
            if (controller != null)
            {
                controller.OnHSceneStart();
            }
            StartZoomEffect(playerCon);
        }
        else if (!isHSceneActive && _wasHSceneActive)
        {
            var controller = HSceneCameraController.Instance;
            if (controller != null)
            {
                controller.OnHSceneEnd();
            }
            _effectActive = false;
            if (_effectCoroutine != null)
            {
                _coroutineRunner?.StopCoroutine(_effectCoroutine);
                _effectCoroutine = null;
            }
            Time.timeScale = 1f;
            RemoveCenterTarget();
            RemoveSizeOverrider();
        }
        
        _wasHSceneActive = isHSceneActive;
    }
}

internal class ZoomEffectCoroutineRunner : MonoBehaviour
{
}

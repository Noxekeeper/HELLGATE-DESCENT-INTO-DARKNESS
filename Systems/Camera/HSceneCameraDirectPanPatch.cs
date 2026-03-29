using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using NoREroMod.Systems.HSceneEffects;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for arrow key camera control during H-scenes.
/// Uses persistent camera target to fix position when arrow keys are released.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "Move")]
internal class HSceneCameraDirectPanPatch
{
    private static bool _hSceneActive = false;
    private static Vector2 _cameraPanOffset = Vector2.zero;
    private static GameObject _panTargetObject = null;
    private static Transform _panTargetTransform = null;
    private static bool _panWasUsed = false; // Pan usage flag
    private static Vector3 _basePanPosition = Vector3.zero; // Base pan target position (animation center)
    private static float _cachedPanSpeed = 0.8f; // Cached pan speed
    private static float _lastPanSpeedUpdate = 0f; // Last panSpeed update time
    private const float PAN_SPEED_UPDATE_INTERVAL = 1f; // Update panSpeed once per second
    
    /// <summary>
    /// Checks if pan was used (for other patches).
    /// </summary>
    internal static bool HasPanOffset()
    {
        return _panWasUsed && _panTargetTransform != null;
    }
    
    /// <summary>
    /// Gets pan target transform (for other patches).
    /// </summary>
    internal static Transform GetPanTargetTransform()
    {
        return _panTargetTransform;
    }
    
    [HarmonyPrefix]
    private static void Move_Prefix(ProCamera2D __instance, float deltaTime)
    {
        // Check if H-scene is active (use cache)
        try
        {
            var playercon = CameraCache.GetPlayerCon();
            if (playercon != null)
            {
                bool wasActive = _hSceneActive;
                _hSceneActive = playercon.eroflag && playercon.erodown != 0;
                
                // Reset state when H-scene starts
                if (_hSceneActive && !wasActive)
                {
                    _cameraPanOffset = Vector2.zero;
                    _panWasUsed = false;
                    _basePanPosition = __instance.LocalPosition; // Save base position
                }
                // Remove pan target when H-scene ends
                else if (!_hSceneActive && wasActive)
                {
                    RemovePanTarget(__instance);
                }
            }
            else
            {
                _hSceneActive = false;
            }
        }
        catch
        {
            _hSceneActive = false;
        }
        
        if (!_hSceneActive)
        {
            return;
        }
        
        // Get pan speed from settings (cached, update once per second)
        float panSpeed = _cachedPanSpeed;
        float currentTime = Time.time;
        if (currentTime - _lastPanSpeedUpdate > PAN_SPEED_UPDATE_INTERVAL)
        {
            try
            {
                var cameraSettings = CameraSettings.Load();
                var controller = HSceneCameraController.Instance;
                if (controller != null)
                {
                    var enemyName = controller.GetCurrentEnemyNamePublic();
                    var settings = cameraSettings.GetEnemySettings(enemyName);
                    _cachedPanSpeed = settings.PanSpeed;
                    panSpeed = _cachedPanSpeed;
                    _lastPanSpeedUpdate = currentTime;
                }
            }
            catch
            {
                // Use cached value
            }
        }
        
        // Process ONLY arrow keys (not WASD, used in QTE)
        Vector2 arrowPanDelta = Vector2.zero;
        
        bool leftPressed = Input.GetKey(KeyCode.LeftArrow);
        bool rightPressed = Input.GetKey(KeyCode.RightArrow);
        bool upPressed = Input.GetKey(KeyCode.UpArrow);
        bool downPressed = Input.GetKey(KeyCode.DownArrow);
        
        if (leftPressed)
        {
            arrowPanDelta.x -= panSpeed * deltaTime;
        }
        if (rightPressed)
        {
            arrowPanDelta.x += panSpeed * deltaTime;
        }
        if (upPressed)
        {
            arrowPanDelta.y += panSpeed * deltaTime;
        }
        if (downPressed)
        {
            arrowPanDelta.y -= panSpeed * deltaTime;
        }
        
        // Create pan target on first pan usage
        if ((leftPressed || rightPressed || upPressed || downPressed) && !_panWasUsed)
        {
            _panWasUsed = true;
            _cameraPanOffset = Vector2.zero;
            CreatePanTarget(__instance);
            
            // Remove centerTarget from HSceneStartZoomEffect to avoid conflict
            RemoveCenterTargetFromZoomEffect(__instance);
        }
        
        // Update offset and panTarget position in Prefix, BEFORE all ProCamera2D calculations
        if (_panTargetTransform != null && _panWasUsed)
        {
            // Update offset only when arrow keys are pressed
            if (arrowPanDelta != Vector2.zero)
            {
                _cameraPanOffset += arrowPanDelta;
            }
            
            // CRITICAL: Update panTarget position in Prefix, BEFORE ProCamera2D calculations
            // Ensures ProCamera2D uses current position
            Vector3 expectedPosition = _basePanPosition + new Vector3(_cameraPanOffset.x, _cameraPanOffset.y, 0f);
            _panTargetTransform.position = expectedPosition;
        }
    }
    
    [HarmonyPostfix]
    private static void Move_Postfix(ProCamera2D __instance, float deltaTime)
    {
        if (!_hSceneActive || _panTargetTransform == null || !_panWasUsed)
        {
            return;
        }
        
        // CRITICAL: Fix panTarget position in Postfix, AFTER all ProCamera2D calculations
        // Prevents position changes by other systems
        Vector3 expectedPosition = _basePanPosition + new Vector3(_cameraPanOffset.x, _cameraPanOffset.y, 0f);
        _panTargetTransform.position = expectedPosition;
        
        // Periodically check and remove other targets (every 10 frames)
        if (Time.frameCount % 10 == 0)
        {
            CameraCache.InitializeCameraTargetsReflection();
            if (CameraCache.CameraTargetsField != null)
            {
                var cameraTargets = CameraCache.CameraTargetsField.GetValue(__instance);
                if (cameraTargets != null && CameraCache.CountProperty != null && 
                    CameraCache.GetItemMethod != null && CameraCache.RemoveAtMethod != null &&
                    CameraCache.TargetTransformField != null)
                {
                    int count = (int)CameraCache.CountProperty.GetValue(cameraTargets, null);
                    
                    // Remove all targets except our pan target (from end to preserve indices)
                    for (int i = count - 1; i >= 0; i--)
                    {
                        var target = CameraCache.GetItemMethod.Invoke(cameraTargets, new object[] { i });
                        if (target != null)
                        {
                            Transform targetTransform = CameraCache.TargetTransformField.GetValue(target) as Transform;
                            // Remove all targets except pan target
                            if (targetTransform != _panTargetTransform)
                            {
                                CameraCache.RemoveAtMethod.Invoke(cameraTargets, new object[] { i });
                            }
                        }
                    }
                }
            }
        }
    }
    
    private static float _originalHorizontalSmoothness = 0.15f;
    private static float _originalVerticalSmoothness = 0.15f;
    private static bool _smoothnessSaved = false;
    
    private static void CreatePanTarget(ProCamera2D proCamera2D)
    {
        if (_panTargetObject != null)
        {
            return;
        }
        
        _panTargetObject = new GameObject("HScenePanTarget");
        _panTargetTransform = _panTargetObject.transform;
        UnityEngine.Object.DontDestroyOnLoad(_panTargetObject);
        
        // CRITICAL: Get current camera position from _targetsMidPoint to avoid jump
        // This ensures pan target starts exactly where camera currently is
        CameraCache.InitializeProCamera2DReflection();
        Vector3 currentCameraPosition;
        
        if (CameraCache.TargetsMidPointField != null)
        {
            // Use _targetsMidPoint - this is where camera is currently looking at
            currentCameraPosition = (Vector3)CameraCache.TargetsMidPointField.GetValue(proCamera2D);
        }
        else
        {
            // Fallback to LocalPosition if reflection fails
            currentCameraPosition = proCamera2D.LocalPosition;
        }
        
        // Set base position to current camera target position (not LocalPosition)
        // This prevents the "jump" when switching from auto-positioning to pan
        _basePanPosition = currentCameraPosition;
        _panTargetTransform.position = _basePanPosition;
        
        // CRITICAL: Save original smoothness values and disable smoothing completely
        if (!_smoothnessSaved)
        {
            _originalHorizontalSmoothness = proCamera2D.HorizontalFollowSmoothness;
            _originalVerticalSmoothness = proCamera2D.VerticalFollowSmoothness;
            _smoothnessSaved = true;
        }
        proCamera2D.HorizontalFollowSmoothness = 0f; // Completely disable smoothing
        proCamera2D.VerticalFollowSmoothness = 0f;
        
        // CRITICAL: Add pan target FIRST, then remove other targets
        // This ensures smooth transition without camera jump
        proCamera2D.AddCameraTarget(_panTargetTransform, 1f, 1f, 0f, Vector2.zero);
        
        // Now remove other targets AFTER pan target is added
        // This prevents jump because pan target is already in correct position
        CameraCache.InitializeCameraTargetsReflection();
        if (CameraCache.CameraTargetsField != null)
        {
            var cameraTargets = CameraCache.CameraTargetsField.GetValue(proCamera2D);
            if (cameraTargets != null && CameraCache.CountProperty != null && 
                CameraCache.GetItemMethod != null && CameraCache.RemoveAtMethod != null &&
                CameraCache.TargetTransformField != null)
            {
                int count = (int)CameraCache.CountProperty.GetValue(cameraTargets, null);
                
                // Remove all targets except our pan target (from end to preserve indices)
                for (int i = count - 1; i >= 0; i--)
                {
                    var target = CameraCache.GetItemMethod.Invoke(cameraTargets, new object[] { i });
                    if (target != null)
                    {
                        Transform targetTransform = CameraCache.TargetTransformField.GetValue(target) as Transform;
                        // Remove all targets except pan target
                        if (targetTransform != _panTargetTransform)
                        {
                            CameraCache.RemoveAtMethod.Invoke(cameraTargets, new object[] { i });
                        }
                    }
                }
            }
        }
    }
    
    private static void RemovePanTarget(ProCamera2D proCamera2D)
    {
        if (_panTargetTransform != null && proCamera2D != null)
        {
            proCamera2D.RemoveCameraTarget(_panTargetTransform, 0f);
        }
        
        if (_panTargetObject != null)
        {
            UnityEngine.Object.Destroy(_panTargetObject);
            _panTargetObject = null;
            _panTargetTransform = null;
        }
        
        // Restore original smoothness values
        if (_smoothnessSaved && proCamera2D != null)
        {
            proCamera2D.HorizontalFollowSmoothness = _originalHorizontalSmoothness;
            proCamera2D.VerticalFollowSmoothness = _originalVerticalSmoothness;
            _smoothnessSaved = false;
        }
        
        _cameraPanOffset = Vector2.zero;
        _panWasUsed = false;
        _basePanPosition = Vector3.zero;
    }
    
    /// <summary>
    /// Removes centerTarget from HSceneStartZoomEffect to avoid conflict with pan.
    /// </summary>
    private static void RemoveCenterTargetFromZoomEffect(ProCamera2D proCamera2D)
    {
        try
        {
            HSceneStartZoomEffect.RemoveCenterTarget();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[HSceneCameraDirectPan] Failed to remove centerTarget: {ex.Message}");
        }
    }
}

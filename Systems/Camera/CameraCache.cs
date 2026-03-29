using UnityEngine;
using System.Reflection;
using Com.LuisPedroFonseca.ProCamera2D;
using System;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Cache for camera patch optimization - avoids repeated GameObject searches and reflection operations.
/// </summary>
internal static class CameraCache
{
    private static GameObject _cachedPlayerObject = null;
    private static playercon _cachedPlayerCon = null;
    private static float _lastCacheUpdate = 0f;
    private const float CACHE_UPDATE_INTERVAL = 0.5f; // Update cache every 0.5 seconds
    
    // Reflection cache for ProCamera2D
    private static FieldInfo _vector3HField = null;
    private static FieldInfo _vector3VField = null;
    private static FieldInfo _targetsMidPointField = null;
    private static FieldInfo _cameraTargetPositionField = null;
    private static FieldInfo _cameraTargetHorizontalPositionSmoothedField = null;
    private static FieldInfo _cameraTargetVerticalPositionSmoothedField = null;
    private static FieldInfo _previousCameraTargetHorizontalPositionSmoothedField = null;
    private static FieldInfo _previousCameraTargetVerticalPositionSmoothedField = null;
    private static FieldInfo _previousTargetsMidPointField = null;
    private static bool _reflectionCacheInitialized = false;
    
    // Reflection cache for playercon
    private static FieldInfo _fitzoomField = null;
    private static FieldInfo _keyJumpField = null;
    private static bool _playerconReflectionCacheInitialized = false;
    
    // Reflection cache for CameraTargets (ProCamera2D)
    private static FieldInfo _cameraTargetsField = null;
    private static System.Type _cameraTargetListType = null;
    private static PropertyInfo _countProperty = null;
    private static MethodInfo _getItemMethod = null;
    private static MethodInfo _removeAtMethod = null;
    private static FieldInfo _targetTransformField = null;
    private static bool _cameraTargetsReflectionCacheInitialized = false;
    
    /// <summary>
    /// Get player (cached).
    /// </summary>
    internal static GameObject GetPlayer()
    {
        float currentTime = Time.time;
        if (_cachedPlayerObject == null || (currentTime - _lastCacheUpdate) > CACHE_UPDATE_INTERVAL)
        {
            _cachedPlayerObject = GameObject.FindGameObjectWithTag("Player");
            _cachedPlayerCon = _cachedPlayerObject?.GetComponent<playercon>();
            _lastCacheUpdate = currentTime;
        }
        return _cachedPlayerObject;
    }
    
    /// <summary>
    /// Get playercon component (cached).
    /// </summary>
    internal static playercon GetPlayerCon()
    {
        if (_cachedPlayerCon == null)
        {
            GetPlayer();
        }
        return _cachedPlayerCon;
    }
    
    /// <summary>
    /// Check if H-scene is active (fast check).
    /// </summary>
    internal static bool IsHSceneActive()
    {
        var playerCon = GetPlayerCon();
        return playerCon != null && playerCon.eroflag && playerCon.erodown != 0;
    }
    
    /// <summary>
    /// Initialize reflection cache for ProCamera2D.
    /// </summary>
    internal static void InitializeProCamera2DReflection()
    {
        if (_reflectionCacheInitialized)
        {
            return;
        }
        
        var type = typeof(ProCamera2D);
        _vector3HField = type.GetField("Vector3H", BindingFlags.NonPublic | BindingFlags.Instance);
        _vector3VField = type.GetField("Vector3V", BindingFlags.NonPublic | BindingFlags.Instance);
        _targetsMidPointField = type.GetField("_targetsMidPoint", BindingFlags.NonPublic | BindingFlags.Instance);
        _cameraTargetPositionField = type.GetField("_cameraTargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        _cameraTargetHorizontalPositionSmoothedField = type.GetField("_cameraTargetHorizontalPositionSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);
        _cameraTargetVerticalPositionSmoothedField = type.GetField("_cameraTargetVerticalPositionSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);
        _previousCameraTargetHorizontalPositionSmoothedField = type.GetField("_previousCameraTargetHorizontalPositionSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);
        _previousCameraTargetVerticalPositionSmoothedField = type.GetField("_previousCameraTargetVerticalPositionSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);
        _previousTargetsMidPointField = type.GetField("_previousTargetsMidPoint", BindingFlags.NonPublic | BindingFlags.Instance);
        
        _reflectionCacheInitialized = true;
    }
    
    /// <summary>
    /// Initialize reflection cache for playercon.
    /// </summary>
    internal static void InitializePlayerconReflection()
    {
        if (_playerconReflectionCacheInitialized)
        {
            return;
        }
        
        var type = typeof(playercon);
        _fitzoomField = type.GetField("fitzoom", BindingFlags.NonPublic | BindingFlags.Instance);
        _keyJumpField = type.GetField("key_jump", BindingFlags.NonPublic | BindingFlags.Instance);
        
        _playerconReflectionCacheInitialized = true;
    }
    
    // FieldInfo getters
    internal static FieldInfo Vector3HField => _vector3HField;
    internal static FieldInfo Vector3VField => _vector3VField;
    internal static FieldInfo TargetsMidPointField => _targetsMidPointField;
    internal static FieldInfo CameraTargetPositionField => _cameraTargetPositionField;
    internal static FieldInfo CameraTargetHorizontalPositionSmoothedField => _cameraTargetHorizontalPositionSmoothedField;
    internal static FieldInfo CameraTargetVerticalPositionSmoothedField => _cameraTargetVerticalPositionSmoothedField;
    internal static FieldInfo PreviousCameraTargetHorizontalPositionSmoothedField => _previousCameraTargetHorizontalPositionSmoothedField;
    internal static FieldInfo PreviousCameraTargetVerticalPositionSmoothedField => _previousCameraTargetVerticalPositionSmoothedField;
    internal static FieldInfo PreviousTargetsMidPointField => _previousTargetsMidPointField;
    internal static FieldInfo FitzoomField => _fitzoomField;
    internal static FieldInfo KeyJumpField => _keyJumpField;
    
    /// <summary>
    /// Initialize reflection cache for CameraTargets.
    /// </summary>
    internal static void InitializeCameraTargetsReflection()
    {
        if (_cameraTargetsReflectionCacheInitialized)
        {
            return;
        }
        
        var type = typeof(ProCamera2D);
        _cameraTargetsField = type.GetField("CameraTargets", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (_cameraTargetsField != null)
        {
            // Get CameraTargets list type
            _cameraTargetListType = _cameraTargetsField.FieldType;
            
            // Cache PropertyInfo and MethodInfo
            _countProperty = _cameraTargetListType.GetProperty("Count");
            _getItemMethod = _cameraTargetListType.GetMethod("get_Item");
            _removeAtMethod = _cameraTargetListType.GetMethod("RemoveAt");
            
            // Get CameraTarget type and its TargetTransform field
            if (_cameraTargetListType.IsGenericType)
            {
                var genericArgs = _cameraTargetListType.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    var cameraTargetType = genericArgs[0];
                    _targetTransformField = cameraTargetType.GetField("TargetTransform");
                }
            }
        }
        
        _cameraTargetsReflectionCacheInitialized = true;
    }
    
    // CameraTargets reflection getters
    internal static FieldInfo CameraTargetsField => _cameraTargetsField;
    internal static PropertyInfo CountProperty => _countProperty;
    internal static MethodInfo GetItemMethod => _getItemMethod;
    internal static MethodInfo RemoveAtMethod => _removeAtMethod;
    internal static FieldInfo TargetTransformField => _targetTransformField;
    
    /// <summary>
    /// Reset cache (call on scene change).
    /// </summary>
    internal static void ResetCache()
    {
        _cachedPlayerObject = null;
        _cachedPlayerCon = null;
        _lastCacheUpdate = 0f;
    }
}


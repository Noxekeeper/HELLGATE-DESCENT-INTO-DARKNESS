using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Cycles combat camera zoom presets via V key: Standard -> Far -> UltraFar -> Standard.
/// Only active outside H-scenes. Resets on H-scene entry and fun_cameraReset.
/// </summary>
internal class CombatCameraPresetSystem
{
    private enum ZoomPreset { Standard, Far, UltraFar }

    private static ZoomPreset _currentPreset = ZoomPreset.Standard;
    private static float _baseHalfSize = 0f;
    private static bool _baseHalfSizeCaptured = false;

    private static ProCamera2D _proCamera;
    private static ProCamera2DZoomToFitTargets _fitzoom;
    private static ProCamera2DSpeedBasedZoom _speedZoom;

    private static FieldInfo _startScreenSizeField;
    private static FieldInfo _fitz_initialCamSizeField;
    private static FieldInfo _fitz_targetCamSizeField;
    private static FieldInfo _fitz_targetCamSizeSmoothedField;
    private static FieldInfo _spd_initialCamSizeField;
    private static FieldInfo _spd_previousCamSizeField;

    private static bool _reflectionInitialized = false;
    private static bool _prevHScene = false;
    private static float _activeTargetHalfSize = 0f;

    private static void InitReflection()
    {
        if (_reflectionInitialized) return;

        _startScreenSizeField = typeof(ProCamera2D).GetField(
            "_startScreenSizeInWorldCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);

        var fitzType = typeof(ProCamera2DZoomToFitTargets);
        _fitz_initialCamSizeField = fitzType.GetField("_initialCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        _fitz_targetCamSizeField = fitzType.GetField("_targetCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        _fitz_targetCamSizeSmoothedField = fitzType.GetField("_targetCamSizeSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);

        var spdType = typeof(ProCamera2DSpeedBasedZoom);
        _spd_initialCamSizeField = spdType.GetField("_initialCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        _spd_previousCamSizeField = spdType.GetField("_previousCamSize", BindingFlags.NonPublic | BindingFlags.Instance);

        _reflectionInitialized = true;
    }

    private static void FindComponents(playercon pc)
    {
        if (_proCamera != null) return;

        var camObj = UnifiedCameraCacheManager.GetMainCamera();
        if (camObj == null) return;

        _proCamera = camObj.GetComponent<ProCamera2D>();
        _fitzoom = camObj.GetComponent<ProCamera2DZoomToFitTargets>();
        _speedZoom = camObj.GetComponent<ProCamera2DSpeedBasedZoom>();
    }

    private static void CaptureBaseSize()
    {
        if (_baseHalfSizeCaptured || _proCamera == null) return;
        InitReflection();

        if (_startScreenSizeField != null)
        {
            var startSize = (Vector2)_startScreenSizeField.GetValue(_proCamera);
            _baseHalfSize = startSize.y * 0.5f;
        }
        else
        {
            _baseHalfSize = _proCamera.ScreenSizeInWorldCoordinates.y * 0.5f;
        }

        if (_baseHalfSize > 0f)
            _baseHalfSizeCaptured = true;
    }

    private static float GetFarMultiplier()
    {
        float mult = Plugin.combatCameraFarZoom?.Value ?? 1.4f;
        return mult <= 1.1f ? 1.4f : mult;
    }

    private static float GetUltraFarMultiplier()
    {
        float mult = Plugin.combatCameraUltraFarZoom?.Value ?? 1.8f;
        return mult <= 1.1f ? 1.8f : mult;
    }

    private static float GetMultiplierForPreset(ZoomPreset preset)
    {
        switch (preset)
        {
            case ZoomPreset.Far:      return GetFarMultiplier();
            case ZoomPreset.UltraFar: return GetUltraFarMultiplier();
            default:                  return 1f;
        }
    }

    private static ZoomPreset NextPreset(ZoomPreset current)
    {
        switch (current)
        {
            case ZoomPreset.Standard: return ZoomPreset.Far;
            case ZoomPreset.Far:      return ZoomPreset.UltraFar;
            default:                  return ZoomPreset.Standard;
        }
    }

    /// <summary>
    /// Rebases internal sizes of ZoomToFitTargets and SpeedBasedZoom extensions
    /// so they treat the new half-size as their origin and stop pulling back.
    /// </summary>
    private static void SyncAllBaselines(float targetHalfSize)
    {
        InitReflection();
        try
        {
            if (_fitzoom != null)
            {
                _fitz_initialCamSizeField?.SetValue(_fitzoom, targetHalfSize);
                _fitz_targetCamSizeField?.SetValue(_fitzoom, targetHalfSize);
                _fitz_targetCamSizeSmoothedField?.SetValue(_fitzoom, targetHalfSize);
            }
            if (_speedZoom != null)
            {
                _spd_initialCamSizeField?.SetValue(_speedZoom, targetHalfSize);
                _spd_previousCamSizeField?.SetValue(_speedZoom, targetHalfSize * 2f);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[CombatCamera] SyncAllBaselines failed: {ex.Message}");
        }
    }

    private static void ApplyZoom(float targetHalfSize)
    {
        if (_proCamera == null) return;
        _activeTargetHalfSize = targetHalfSize;
        SyncAllBaselines(targetHalfSize);
        _proCamera.UpdateScreenSize(targetHalfSize, 0.3f, EaseType.EaseInOut);
    }

    internal static void ResetToStandard()
    {
        if (_currentPreset == ZoomPreset.Standard) return;
        _currentPreset = ZoomPreset.Standard;
        _activeTargetHalfSize = 0f;

        if (_baseHalfSizeCaptured && _proCamera != null)
        {
            SyncAllBaselines(_baseHalfSize);
            _proCamera.UpdateScreenSize(_baseHalfSize, 0.3f, EaseType.EaseInOut);
        }
    }

    /// <summary>Invoked from PlayerConUpdateDispatcher</summary>
    internal static void Process(playercon __instance)
    {
        try
        {
            if (!(Plugin.enableCombatCameraPresets?.Value ?? true)) return;

            bool isHScene = __instance.eroflag && __instance.erodown != 0;

            if (isHScene && !_prevHScene)
                ResetToStandard();
            _prevHScene = isHScene;

            if (isHScene) return;

            if (_currentPreset != ZoomPreset.Standard && _activeTargetHalfSize > 0f)
                SyncAllBaselines(_activeTargetHalfSize);

            if (!Input.GetKeyDown(KeyCode.V)) return;

            FindComponents(__instance);
            CaptureBaseSize();
            if (_proCamera == null || _baseHalfSize <= 0f) return;

            _currentPreset = NextPreset(_currentPreset);
            ApplyZoom(_baseHalfSize * GetMultiplierForPreset(_currentPreset));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[CombatCamera] Update failed: {ex.Message}");
        }
    }
}

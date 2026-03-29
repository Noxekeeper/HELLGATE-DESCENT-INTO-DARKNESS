using System;
using System.Collections;
using UnityEngine;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Vision_Blood_Fast on Rage activation. Duration from config.
/// </summary>
internal static class RageFireGradientEffect
{
    private static float EffectDuration => Plugin.rageBloodEffectDuration?.Value ?? 0.5f;

    private static UnityEngine.Camera? _mainCamera;
    private static CameraFilterPack_Vision_Blood_Fast? _bloodFastComponent;
    private static GameObject? _runnerObj;
    private static RageBloomRunner? _runner;
    private static Coroutine? _stopCoroutine;

    internal static void Initialize()
    {
        try
        {
            RageSystem.OnActivated += OnRageActivated;
            RageSystem.OnDeactivated += OnRageDeactivated;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageShieldEffect] Init error: {ex.Message}");
        }
    }

    private static void OnRageActivated()
    {
        try
        {
            EnsureCamera();
            if (_mainCamera == null) return;

            if (_bloodFastComponent == null)
            {
                _bloodFastComponent = _mainCamera.gameObject.AddComponent<CameraFilterPack_Vision_Blood_Fast>();
            }
            if (_bloodFastComponent != null)
            {
                _bloodFastComponent.HoleSize = 0.5f;
                _bloodFastComponent.HoleSmooth = 0.3f;
                _bloodFastComponent.Color1 = 0.2f;
                _bloodFastComponent.Color2 = 0.9f;
                _bloodFastComponent.enabled = true;
            }

            EnsureRunner();
            if (_runner != null && _stopCoroutine != null)
            {
                _runner.StopCoroutine(_stopCoroutine);
            }
            _stopCoroutine = _runner?.StartCoroutine(StopEffectAfterDelay());
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageShieldEffect] Activate error: {ex.Message}");
        }
    }

    private static IEnumerator StopEffectAfterDelay()
    {
        yield return new WaitForSeconds(EffectDuration);
        if (_bloodFastComponent != null) _bloodFastComponent.enabled = false;
        _stopCoroutine = null;
    }

    private static void OnRageDeactivated()
    {
        try
        {
            if (_bloodFastComponent != null) _bloodFastComponent.enabled = false;
            if (_runner != null && _stopCoroutine != null)
            {
                _runner.StopCoroutine(_stopCoroutine);
                _stopCoroutine = null;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageShieldEffect] Deactivate error: {ex.Message}");
        }
    }

    private static void EnsureCamera()
    {
        if (_mainCamera == null)
        {
            var camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null)
            {
                _mainCamera = camObj.GetComponent<UnityEngine.Camera>();
            }
        }
    }

    private static void EnsureRunner()
    {
        if (_runner != null) return;
        _runnerObj = new GameObject("RageShieldEffectRunner_XUAIGNORE");
        UnityEngine.Object.DontDestroyOnLoad(_runnerObj);
        _runner = _runnerObj.AddComponent<RageBloomRunner>();
    }
}

internal class RageBloomRunner : MonoBehaviour { }

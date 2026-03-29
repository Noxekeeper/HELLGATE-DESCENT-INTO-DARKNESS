using System.Collections;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Camera effects management: slowmo, shake, zoom.
/// </summary>
internal class CameraEffectsManager
{
    private MonoBehaviour _coroutineRunner;
    private Coroutine _slowmoCoroutine;
    private ProCamera2DShake _proShake;
    private float _originalTimeScale = 1f;
    private bool _slowmoActive = false;
    
    internal CameraEffectsManager()
    {
        GameObject runnerObj = new GameObject("CameraEffectsRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObj);
        _coroutineRunner = runnerObj.AddComponent<CameraEffectsRunner>();
        
        // Find ProCamera2DShake
        GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCameraObj != null)
        {
            _proShake = mainCameraObj.GetComponent<ProCamera2DShake>();
        }
    }
    
    /// <summary>
    /// Start slowmo effect.
    /// </summary>
    internal void StartSlowmo(float timeScale, float duration)
    {
        if (_slowmoCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_slowmoCoroutine);
        }
        
        // Save original timeScale only if slowmo is not active
        if (!_slowmoActive)
        {
            _originalTimeScale = Time.timeScale;
        }
        
        _slowmoCoroutine = _coroutineRunner.StartCoroutine(SlowmoCoroutine(timeScale, duration));
    }
    
    /// <summary>
    /// Stop slowmo.
    /// </summary>
    internal void StopSlowmo()
    {
        if (_slowmoCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_slowmoCoroutine);
            _slowmoCoroutine = null;
        }
        
        _slowmoActive = false;
        Time.timeScale = _originalTimeScale;
    }
    
    /// <summary>
    /// Slowmo coroutine.
    /// </summary>
    private IEnumerator SlowmoCoroutine(float timeScale, float duration)
    {
        _slowmoActive = true;
        float startTimeScale = Time.timeScale;
        
        // Smooth slowdown
        float elapsed = 0f;
        float fadeInDuration = 0.2f;
        
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            Time.timeScale = Mathf.Lerp(startTimeScale, timeScale, t);
            yield return null;
        }
        
        Time.timeScale = timeScale;
        
        // Hold slowmo for specified duration
        yield return new WaitForSecondsRealtime(duration);
        
        // Smooth recovery
        elapsed = 0f;
        float fadeOutDuration = 0.3f;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            Time.timeScale = Mathf.Lerp(timeScale, _originalTimeScale, t);
            yield return null;
        }
        
        Time.timeScale = _originalTimeScale;
        _slowmoActive = false;
        _slowmoCoroutine = null;
    }
    
    /// <summary>
    /// Stop shake.
    /// </summary>
    internal void StopShake()
    {
        if (_proShake != null)
        {
            _proShake.StopShaking();
        }
    }
}

/// <summary>
/// Component for running effect coroutines.
/// </summary>
internal class CameraEffectsRunner : MonoBehaviour
{
}



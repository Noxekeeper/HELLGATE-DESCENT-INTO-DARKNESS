using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// MindBroken visual effects: negative effect (periodic flash at configurable thresholds).
/// </summary>
internal static class MindBrokenVisualEffectsSystem
{
    private static bool IsEnabled => Plugin.enableMindBroken?.Value ?? false;
    
    private const string CanvasObjectName = "MindBrokenVisualEffectsCanvas_XUAIGNORE";
    
    private static Camera? mainCamera;
    private static MonoBehaviour? negativeEffectComponent;
    private static MonoBehaviour? dreamEffectComponent;
    private static GameObject? flashImageObject;
    private static Image? flashImage;
    private static Coroutine? negativeBlinkCoroutine;
    private static Coroutine? dreamEffectCoroutine;
    private static Coroutine? flashEffectCoroutine;
    private static RectTransform? overlayCanvasRect;
    
    internal static void Initialize()
    {
        if (!IsEnabled) return;
        try {
            SubscribeToEvents();
            EnsureOverlayCanvas();
            EnsureCameraEffects();
        } catch { }
    }
    
    private static void SubscribeToEvents()
    {
        MindBrokenSystem.OnPercentChanged += OnMindBrokenPercentChanged;
        MindBrokenSystem.OnMilestoneReached += OnMilestoneReached;
    }
    
    private static void OnMindBrokenPercentChanged(float oldPercent, float newPercent)
    {
        if (!IsEnabled) return;
        try {
            if (newPercent <= oldPercent) return;
            
            // Negative effect logic
            float negativeThreshold = Plugin.mbNegativeActivationThreshold?.Value ?? 0.5f;
            float negativeStep = Plugin.mbNegativeActivationStep?.Value ?? 0.1f;
            float negativeDuration = Plugin.mbNegativeEffectDuration?.Value ?? 3f;
            float stepMultiplier = 1f / negativeStep;
            float oldStep = Mathf.Floor(oldPercent * stepMultiplier) / stepMultiplier;
            float newStep = Mathf.Floor(newPercent * stepMultiplier) / stepMultiplier;
            if (newPercent >= negativeThreshold) {
                if (oldPercent < negativeThreshold && newPercent >= negativeThreshold)
                    TriggerNegativeEffectSimple(negativeDuration);
                else if (newStep > oldStep && newStep >= (negativeThreshold + negativeStep)) {
                    float stepPercent = newStep;
                    if (stepPercent >= (negativeThreshold + negativeStep) && stepPercent < 1.0f)
                        TriggerNegativeEffectSimple(negativeDuration);
                }
            }
            
            // Flash + Dream effect logic - triggers every 10% starting from configurable threshold
            float effectStep = 0.1f; // Every 10%
            float flashStartThreshold = Plugin.mbFlashStartThreshold?.Value ?? 0.2f;
            float flashDuration = Plugin.mbFlashDuration?.Value ?? 3f;
            float dreamDuration = Plugin.mbDreamDuration?.Value ?? 5f;
            float effectStepMultiplier = 1f / effectStep;
            float oldEffectStep = Mathf.Floor(oldPercent * effectStepMultiplier) / effectStepMultiplier;
            float newEffectStep = Mathf.Floor(newPercent * effectStepMultiplier) / effectStepMultiplier;
            
            // Start flash effect from configurable threshold
            if (newEffectStep > oldEffectStep && newEffectStep >= flashStartThreshold) {
                TriggerFlashEffect(flashDuration);
                
                // Dream effect only at 100%
                if (newPercent >= 1.0f) {
                    TriggerDreamEffect(dreamDuration);
                }
            }
        } catch { }
    }
    
    private static void OnMilestoneReached(float milestone)
    {
        // Milestone больше not используются for негатива, логика перенесеon in OnMindBrokenPercentChanged
        // Оставляем метод for compatibility, but not используем
    }
    
    private static void EnsureOverlayCanvas()
    {
        if (!IsEnabled) return;
        
        try
        {
            if (overlayCanvasRect != null) return;
            
            GameObject existing = GameObject.Find(CanvasObjectName);
            if (existing != null)
            {
                overlayCanvasRect = existing.GetComponent<RectTransform>();
                // Use существующий canvas (логи отключены)
                return;
            }
            
            GameObject canvasGo = new GameObject(CanvasObjectName);
            overlayCanvasRect = canvasGo.AddComponent<RectTransform>();
            
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999; // Ниже CorruptionCaptions (1000)
            
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;
            canvasGo.layer = LayerMask.NameToLayer("UI");
            
            overlayCanvasRect.anchorMin = Vector2.zero;
            overlayCanvasRect.anchorMax = Vector2.one;
            overlayCanvasRect.pivot = new Vector2(0.5f, 0.5f);
            overlayCanvasRect.offsetMin = Vector2.zero;
            overlayCanvasRect.offsetMax = Vector2.zero;
            overlayCanvasRect.localScale = Vector3.one;
            
            canvasGo.SetActive(true);
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            
            // Canvas создан (логи отключены)
        }
        catch (Exception ex)
        {
        }
    }
    
    // Negative effect (periodic camera invert)
    private static void EnsureCameraEffects()
    {
        try
        {
            // Try найти камеру несколько раз, так as it может быть not готова on инициализации
            if (mainCamera == null)
            {
                GameObject cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (cameraObj != null)
                {
                    mainCamera = cameraObj.GetComponent<Camera>();
                }
                
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                    }
                }
            }
            
            if (mainCamera == null)
            {
                // Start coroutine for повторной попытки
                if (overlayCanvasRect != null)
                {
                    var runner = overlayCanvasRect.GetComponent<CameraFinderRunner>();
                    if (runner == null)
                    {
                        runner = overlayCanvasRect.gameObject.AddComponent<CameraFinderRunner>();
                    }
                    runner.StartCoroutine(RetryFindCamera());
                }
                return;
            }
            
            // Эффект негатива
            if (negativeEffectComponent == null)
            {
                negativeEffectComponent = mainCamera.GetComponent<CameraFilterPack_Color_Invert>();
                if (negativeEffectComponent == null)
                {
                    negativeEffectComponent = mainCamera.gameObject.AddComponent<CameraFilterPack_Color_Invert>();
                }
                else
                {
                }
                
                if (negativeEffectComponent is CameraFilterPack_Color_Invert invert)
                {
                    invert.enabled = false;
                    invert._Fade = 0f;
                }
            }
            
            // Эффект искажения сon (Dream2)
            if (dreamEffectComponent == null)
            {
                dreamEffectComponent = mainCamera.GetComponent<CameraFilterPack_Distortion_Dream2>();
                if (dreamEffectComponent == null)
                {
                    dreamEffectComponent = mainCamera.gameObject.AddComponent<CameraFilterPack_Distortion_Dream2>();
                }
                
                if (dreamEffectComponent is CameraFilterPack_Distortion_Dream2 dream)
                {
                    dream.enabled = false;
                    dream.Speed = Plugin.mbDreamEffectSpeed?.Value ?? 3f;
                    dream.Distortion = Plugin.mbDreamEffectDistortion?.Value ?? 4f;
                }
            }
            
            // Простая розовая вспышка через UI Image
            if (flashImageObject == null && overlayCanvasRect != null)
            {
                flashImageObject = new GameObject("MindBrokenFlashImage_XUAIGNORE");
                flashImageObject.transform.SetParent(overlayCanvasRect, false);
                
                RectTransform flashRect = flashImageObject.AddComponent<RectTransform>();
                flashRect.anchorMin = Vector2.zero;
                flashRect.anchorMax = Vector2.one;
                flashRect.offsetMin = Vector2.zero;
                flashRect.offsetMax = Vector2.zero;
                
                flashImage = flashImageObject.AddComponent<Image>();
                float colorR = Plugin.mbFlashColorR?.Value ?? 1f;
                float colorG = Plugin.mbFlashColorG?.Value ?? 0.75f;
                float colorB = Plugin.mbFlashColorB?.Value ?? 0.88f;
                flashImage.color = new Color(colorR, colorG, colorB, 0f); // Initially transparent
                flashImage.raycastTarget = false;
                
                flashImageObject.SetActive(false);
            }
            
        }
        catch (Exception ex)
        {
        }
    }
    
    private static IEnumerator RetryFindCamera()
    {
        int attempts = 0;
        while (mainCamera == null && attempts < 20)
        {
            yield return new WaitForSeconds(0.5f);
            attempts++;
            
            GameObject cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObj != null)
            {
                mainCamera = cameraObj.GetComponent<Camera>();
                if (mainCamera != null)
                {
                    // Камера найдеon on попытке {attempts} (логи отключены)
                    EnsureCameraEffects();
                    yield break;
                }
            }
            
            if (Camera.main != null)
            {
                mainCamera = Camera.main;
                // Camera.main найден on попытке {attempts} (логи отключены)
                EnsureCameraEffects();
                yield break;
            }
        }
        
        if (mainCamera == null)
        {
        }
    }
    
    
    // Простой эффект негатива on 1 second
    private static void TriggerNegativeEffectSimple(float duration)
    {
        if (negativeEffectComponent == null || mainCamera == null)
        {
            EnsureCameraEffects();
        }
        
        if (negativeEffectComponent == null || mainCamera == null)
        {
            return;
        }
        
        try
        {
            // Stop предыдущую корутину
            if (negativeBlinkCoroutine != null && mainCamera != null)
            {
                var runner = mainCamera.GetComponent<NegativeEffectRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(negativeBlinkCoroutine);
                }
            }
            
            // Запускаем новую корутину
            var coroutineRunner = mainCamera.GetComponent<NegativeEffectRunner>();
            if (coroutineRunner == null)
            {
                coroutineRunner = mainCamera.gameObject.AddComponent<NegativeEffectRunner>();
            }
            
            negativeBlinkCoroutine = coroutineRunner.StartCoroutine(NegativeEffectSimpleCoroutine(duration));
            
        }
        catch (Exception ex)
        {
        }
    }
    
    private static IEnumerator NegativeEffectSimpleCoroutine(float duration)
    {
        if (negativeEffectComponent == null) yield break;
        
        CameraFilterPack_Color_Invert invert = negativeEffectComponent as CameraFilterPack_Color_Invert;
        if (invert == null) yield break;
        
        // Включаем негатив
        invert.enabled = true;
        invert._Fade = 1f;
        
        yield return new WaitForSeconds(duration);
        
        // Выключаем негатив
        invert.enabled = false;
        invert._Fade = 0f;
        
        negativeBlinkCoroutine = null;
    }
    
    /// <summary>
    /// Forces dream distortion wave effect (can be called externally).
    /// </summary>
    internal static void TriggerDreamEffectForced(float duration)
    {
        TriggerDreamEffect(duration);
    }

    // Dream distortion effect trigger (only at 100%)
    private static void TriggerDreamEffect(float duration)
    {
        if (dreamEffectComponent == null || mainCamera == null)
        {
            EnsureCameraEffects();
        }
        
        if (dreamEffectComponent == null || mainCamera == null)
        {
            return;
        }
        
        try
        {
            // Stop previous coroutine
            if (dreamEffectCoroutine != null && mainCamera != null)
            {
                var runner = mainCamera.GetComponent<DreamEffectRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(dreamEffectCoroutine);
                }
            }
            
            // Start new coroutine
            var coroutineRunner = mainCamera.GetComponent<DreamEffectRunner>();
            if (coroutineRunner == null)
            {
                coroutineRunner = mainCamera.gameObject.AddComponent<DreamEffectRunner>();
            }
            
            dreamEffectCoroutine = coroutineRunner.StartCoroutine(DreamEffectCoroutine(duration));
        }
        catch (Exception ex)
        {
        }
    }
    
    private static IEnumerator DreamEffectCoroutine(float duration)
    {
        if (dreamEffectComponent == null) yield break;
        
        CameraFilterPack_Distortion_Dream2 dream = dreamEffectComponent as CameraFilterPack_Distortion_Dream2;
        if (dream == null) yield break;
        
        dream.enabled = true;
        
        float targetSpeed = Plugin.mbDreamEffectSpeed?.Value ?? 3f;
        float targetDistortion = Plugin.mbDreamEffectDistortion?.Value ?? 4f;
        float fadeInTime = Plugin.mbDreamFadeInTime?.Value ?? 1.2f;
        float fadeOutTime = Plugin.mbDreamFadeOutTime?.Value ?? 1.5f;
        float holdTime = duration - fadeInTime - fadeOutTime;
        
        if (holdTime < 0f)
        {
            // If duration is too short, adjust times proportionally
            float totalTime = fadeInTime + fadeOutTime;
            fadeInTime = duration * (fadeInTime / totalTime);
            fadeOutTime = duration * (fadeOutTime / totalTime);
            holdTime = 0f;
        }
        
        float elapsed = 0f;
        
        // Smooth fade in with easing
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeInTime;
            // Use smoothstep for gentle start
            float smoothProgress = progress * progress * (3f - 2f * progress);
            
            dream.Speed = Mathf.Lerp(0f, targetSpeed, smoothProgress);
            dream.Distortion = Mathf.Lerp(0f, targetDistortion, smoothProgress);
            
            yield return null;
        }
        
        // Hold at full strength
        dream.Speed = targetSpeed;
        dream.Distortion = targetDistortion;
        
        if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }
        
        // Smooth fade out with easing
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeOutTime;
            // Use smootherstep for even gentler end
            float smoothProgress = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);
            
            dream.Speed = Mathf.Lerp(targetSpeed, 0f, smoothProgress);
            dream.Distortion = Mathf.Lerp(targetDistortion, 0f, smoothProgress);
            
            yield return null;
        }
        
        // Ensure fully disabled
        dream.Speed = 0f;
        dream.Distortion = 0f;
        dream.enabled = false;
        
        dreamEffectCoroutine = null;
    }
    
    // Simple pink flash effect trigger (using UI Image)
    private static void TriggerFlashEffect(float duration)
    {
        if (flashImageObject == null || flashImage == null)
        {
            EnsureCameraEffects();
        }
        
        if (flashImageObject == null || flashImage == null || overlayCanvasRect == null)
        {
            return;
        }
        
        try
        {
            // Stop previous coroutine
            if (flashEffectCoroutine != null)
            {
                var runner = overlayCanvasRect.GetComponent<FlashEffectRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(flashEffectCoroutine);
                }
            }
            
            // Start new coroutine
            var coroutineRunner = overlayCanvasRect.GetComponent<FlashEffectRunner>();
            if (coroutineRunner == null)
            {
                coroutineRunner = overlayCanvasRect.gameObject.AddComponent<FlashEffectRunner>();
            }
            
            flashEffectCoroutine = coroutineRunner.StartCoroutine(FlashEffectCoroutine(duration));
        }
        catch (Exception ex)
        {
        }
    }
    
    private static IEnumerator FlashEffectCoroutine(float duration)
    {
        if (flashImageObject == null || flashImage == null) yield break;
        
        flashImageObject.SetActive(true);
        
        float elapsed = 0f;
        int totalCycles = Plugin.mbFlashPulseCycles?.Value ?? 3;
        float minAlpha = Plugin.mbFlashMinAlpha?.Value ?? 0.08f;
        float maxAlpha = Plugin.mbFlashMaxAlpha?.Value ?? 0.22f;
        float fadeOutTime = Plugin.mbFlashFadeOutTime?.Value ?? 0.8f;
        float colorR = Plugin.mbFlashColorR?.Value ?? 1f;
        float colorG = Plugin.mbFlashColorG?.Value ?? 0.75f;
        float colorB = Plugin.mbFlashColorB?.Value ?? 0.88f;
        Color flashColor = new Color(colorR, colorG, colorB, 0f);
        
        // Smooth pulsating effect for configurable cycles
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Calculate pulsating alpha using sine wave
            float cycleProgress = (elapsed / duration) * totalCycles;
            float pulseValue = Mathf.Sin(cycleProgress * 2f * Mathf.PI);
            
            // Use smoothstep for more gentle transitions
            float normalizedPulse = (pulseValue + 1f) * 0.5f;
            float smoothPulse = normalizedPulse * normalizedPulse * (3f - 2f * normalizedPulse);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, smoothPulse);
            
            // Apply gentle fade out at the end
            if (elapsed > duration - fadeOutTime)
            {
                float fadeOutProgress = (duration - elapsed) / fadeOutTime;
                alpha *= fadeOutProgress;
            }
            
            flashColor.a = alpha;
            flashImage.color = flashColor;
            
            yield return null;
        }
        
        // Ensure fully transparent at the end
        flashColor.a = 0f;
        flashImage.color = flashColor;
        flashImageObject.SetActive(false);
        flashEffectCoroutine = null;
    }
    
    // Старый метод for milestone (больше not используется, but оставляем for compatibility)
    private static void TriggerNegativeEffect(float percent)
    {
        // Ensure камера и component готовы
        if (mainCamera == null)
        {
            EnsureCameraEffects();
        }
        
        if (negativeEffectComponent == null || mainCamera == null)
        {
            return;
        }
        
        try
        {
            // Определяем параметры depending on процента
            // Use диапазоны, так as milestone могут быть 0.5, 0.75, 0.9, 1.0
            float duration = 0f;
            float frequency = 0f;
            int blinkCount = 0;
            
            if (percent >= 0.9f) // 90% и выше (включая 100%)
            {
                duration = 0.6f;
                frequency = 4f;
                blinkCount = 6;
            }
            else if (percent >= 0.75f) // 75% и выше (примерbut 80%)
            {
                duration = 0.5f;
                frequency = 3f;
                blinkCount = 5;
            }
            else if (percent >= 0.5f) // 50% и выше
            {
                duration = 0.4f;
                frequency = 2.5f;
                blinkCount = 4;
            }
            else if (percent >= 0.3f) // 30% и выше
            {
                duration = 0.3f;
                frequency = 2f;
                blinkCount = 3;
            }
            else
            {
                return; // Do not запускаем for other процентов
            }
            
            // Stop предыдущую корутину
            if (negativeBlinkCoroutine != null && mainCamera != null)
            {
                var runner = mainCamera.GetComponent<NegativeEffectRunner>();
                if (runner != null)
                {
                    runner.StopCoroutine(negativeBlinkCoroutine);
                }
            }
            
            // Запускаем новую корутину
            var coroutineRunner = mainCamera.GetComponent<NegativeEffectRunner>();
            if (coroutineRunner == null)
            {
                coroutineRunner = mainCamera.gameObject.AddComponent<NegativeEffectRunner>();
            }
            
            negativeBlinkCoroutine = coroutineRunner.StartCoroutine(NegativeBlinkCoroutine(duration, frequency, blinkCount));
            
            // Эффект негатива запущен (логи отключены)
        }
        catch (Exception ex)
        {
        }
    }
    
    private static IEnumerator NegativeBlinkCoroutine(float duration, float frequency, int blinkCount)
    {
        if (negativeEffectComponent == null) yield break;
        
        CameraFilterPack_Color_Invert invert = negativeEffectComponent as CameraFilterPack_Color_Invert;
        if (invert == null) yield break;
        
        float timeBetweenBlinks = 1f / frequency;
        
        for (int i = 0; i < blinkCount; i++)
        {
            // Включаем негатив
            invert.enabled = true;
            invert._Fade = 1f;
            
            yield return new WaitForSeconds(duration);
            
            // Выключаем негатив
            invert.enabled = false;
            invert._Fade = 0f;
            
            if (i < blinkCount - 1) // Do not ждем after последнits моргания
            {
                yield return new WaitForSeconds(timeBetweenBlinks - duration);
            }
        }
        
        negativeBlinkCoroutine = null;
    }
    
    
    private class NegativeEffectRunner : MonoBehaviour { }
    private class DreamEffectRunner : MonoBehaviour { }
    private class FlashEffectRunner : MonoBehaviour { }
    private class CameraFinderRunner : MonoBehaviour { }
    
    internal static void Cleanup()
    {
        try {
            MindBrokenSystem.OnPercentChanged -= OnMindBrokenPercentChanged;
            MindBrokenSystem.OnMilestoneReached -= OnMilestoneReached;
            if (negativeBlinkCoroutine != null && mainCamera != null)
            {
                var runner = mainCamera.GetComponent<NegativeEffectRunner>();
                if (runner != null)
                    runner.StopCoroutine(negativeBlinkCoroutine);
            }
            if (dreamEffectCoroutine != null && mainCamera != null)
            {
                var dreamRunner = mainCamera.GetComponent<DreamEffectRunner>();
                if (dreamRunner != null)
                    dreamRunner.StopCoroutine(dreamEffectCoroutine);
            }
            if (flashEffectCoroutine != null && overlayCanvasRect != null)
            {
                var flashRunner = overlayCanvasRect.GetComponent<FlashEffectRunner>();
                if (flashRunner != null)
                    flashRunner.StopCoroutine(flashEffectCoroutine);
            }
            if (negativeEffectComponent != null && negativeEffectComponent is CameraFilterPack_Color_Invert invert)
                invert.enabled = false;
            if (dreamEffectComponent != null && dreamEffectComponent is CameraFilterPack_Distortion_Dream2 dream)
                dream.enabled = false;
            if (flashImageObject != null)
                flashImageObject.SetActive(false);
        } catch { }
    }
}


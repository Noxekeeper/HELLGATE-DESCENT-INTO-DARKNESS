using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Full-screen scarlet UI blink overlay for combat fatality start.
/// (Vanilla orgasm WhiteFadeIn is white-only; this module uses a dedicated overlay.)
/// </summary>
internal static class EnemyFatalityFlash
{
    private static readonly Color Scarlet = new Color(0.89f, 0.07f, 0.17f, 1f);

    private static Coroutine _blinkRoutine;
    private static MonoBehaviour _host;
    private static Canvas _blinkCanvas;
    private static Image _blinkImage;

    /// <summary>Three quick scarlet full-screen blinks at fatality start.</summary>
    internal static void PlayScarletTripleBlink()
    {
        if (!EnsureHost())
            return;

        EnsureBlinkOverlay();
        if (_blinkImage == null)
            return;

        if (_blinkRoutine != null)
            _host.StopCoroutine(_blinkRoutine);

        _blinkRoutine = _host.StartCoroutine(ScarletTripleBlinkRoutine());
    }

    internal static void ForceStop()
    {
        try
        {
            if (_blinkRoutine != null && _host != null)
                _host.StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;

            if (_blinkImage != null)
            {
                _blinkImage.color = new Color(Scarlet.r, Scarlet.g, Scarlet.b, 0f);
                _blinkImage.enabled = false;
            }

            // Clear leftover CameraFilterPack manga flash (shared with Rage / other systems).
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var manga = cam.GetComponent<CameraFilterPack_Drawing_Manga_Flash_Color>();
                if (manga != null)
                {
                    manga.Intensity = 0f;
                    manga.enabled = false;
                }
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static bool EnsureHost()
    {
        UnityEngine.Camera cam = UnityEngine.Camera.main;
        if (cam == null)
            return false;

        _host = cam.GetComponent<EnemyFatalityFlashHost>();
        if (_host == null)
            _host = cam.gameObject.AddComponent<EnemyFatalityFlashHost>();
        return _host != null;
    }

    private static void EnsureBlinkOverlay()
    {
        if (_blinkImage != null && _blinkCanvas != null)
            return;

        var go = new GameObject("HellGate_EnemyFatalityScarletFlash");
        Object.DontDestroyOnLoad(go);

        _blinkCanvas = go.AddComponent<Canvas>();
        _blinkCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _blinkCanvas.sortingOrder = 32000;

        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>().enabled = false;

        var imageGo = new GameObject("Flash");
        imageGo.transform.SetParent(go.transform, false);
        _blinkImage = imageGo.AddComponent<Image>();
        _blinkImage.raycastTarget = false;
        _blinkImage.color = new Color(Scarlet.r, Scarlet.g, Scarlet.b, 0f);
        _blinkImage.enabled = false;

        RectTransform rt = _blinkImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static IEnumerator ScarletTripleBlinkRoutine()
    {
        const float up = 0.04f;
        const float down = 0.09f;
        const float gap = 0.05f;
        const float peakAlpha = 0.72f;
        const int blinks = 3;

        for (int i = 0; i < blinks; i++)
        {
            yield return BlinkOnce(up, down, peakAlpha);
            if (i < blinks - 1)
                yield return new WaitForSecondsRealtime(gap);
        }

        if (_blinkImage != null)
        {
            _blinkImage.color = new Color(Scarlet.r, Scarlet.g, Scarlet.b, 0f);
            _blinkImage.enabled = false;
        }

        _blinkRoutine = null;
    }

    private static IEnumerator BlinkOnce(float up, float down, float peakAlpha)
    {
        if (_blinkImage == null)
            yield break;

        _blinkImage.enabled = true;
        float t = 0f;
        while (t < up)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, peakAlpha, Mathf.Clamp01(t / up));
            _blinkImage.color = new Color(Scarlet.r, Scarlet.g, Scarlet.b, a);
            yield return null;
        }

        t = 0f;
        while (t < down)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peakAlpha, 0f, Mathf.Clamp01(t / down));
            _blinkImage.color = new Color(Scarlet.r, Scarlet.g, Scarlet.b, a);
            yield return null;
        }

        _blinkImage.color = new Color(Scarlet.r, Scarlet.g, Scarlet.b, 0f);
    }
}

internal sealed class EnemyFatalityFlashHost : MonoBehaviour
{
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// After Take Vengeance from lethal trap death: rise 2s, MindShock, peak flash, hold 3s,
/// HeartBeat loop from rise start, decay to 15% in 3s, white flash only.
/// </summary>
internal static class LethalTrapVengeanceMindBrokenShock
{
    private const string CanvasName = "LethalVengeanceShockOverlay_XUAIGNORE";

    private static bool _sequenceRunning;
    private static Coroutine _sequenceCoroutine;
    private static Coroutine _glowCoroutine;

    private static GameObject _canvasObject;
    private static Image _flashImage;
    private static Image _glowImage;

    internal static bool IsFeatureEnabled()
    {
        bool magic = Plugin.enableLethalMagicTrap?.Value ?? false;
        bool cocoon = Plugin.enableLethalCocoonTrap?.Value ?? false;
        return magic || cocoon;
    }

    internal static void TryStartAfterVengeance()
    {
        if (_sequenceRunning)
            return;

        if (!IsFeatureEnabled())
        {
            if (LethalTrapVengeanceShockSession.HasPending)
                LethalTrapVengeanceShockSession.ClearPending();
            return;
        }

        if (!LethalTrapVengeanceShockSession.TryConsumePending())
            return;

        if (!MindBrokenSystem.Enabled)
            return;

        if (Plugin.Instance == null)
            return;

        _sequenceCoroutine = Plugin.Instance.StartCoroutine(ShockSequenceCoroutine());
    }

    internal static void StopIfRunning()
    {
        if (!_sequenceRunning && _sequenceCoroutine == null)
            return;

        if (Plugin.Instance != null && _sequenceCoroutine != null)
            Plugin.Instance.StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = null;
        _sequenceRunning = false;
        LethalTrapVengeanceShockAudio.StopHeartBeatLoop();
        StopGlow();
        HideOverlay();
        MindBrokenSystem.EndScriptedSequence(LethalTrapVengeanceShockTuning.FloorPercent);
    }

    private static IEnumerator ShockSequenceCoroutine()
    {
        _sequenceRunning = true;
        MindBrokenSystem.BeginScriptedSequence();

        float startPercent = MindBrokenSystem.Percent;
        Plugin.Log?.LogInfo(
            "[LethalTrapVengeanceShock] Start — MB "
            + (startPercent * 100f).ToString("0.#")
            + "% -> 100% over "
            + LethalTrapVengeanceShockTuning.RiseToMaxSeconds
            + "s");

        EnsureOverlay();
        StartPinkGlow();
        LethalTrapVengeanceShockAudio.StartHeartBeatLoop();

        float riseDuration = LethalTrapVengeanceShockTuning.RiseToMaxSeconds;
        float riseElapsed = 0f;
        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.deltaTime;
            float tRaw = riseDuration > 0f ? Mathf.Clamp01(riseElapsed / riseDuration) : 1f;
            float t = SmoothStep01(tRaw);
            float p = Mathf.Lerp(startPercent, LethalTrapVengeanceShockTuning.MaxPercent, t);
            MindBrokenSystem.SetScriptedPercent(p);
            yield return null;
        }

        MindBrokenSystem.SetScriptedPercent(LethalTrapVengeanceShockTuning.MaxPercent);
        LethalTrapVengeanceShockAudio.TryPlayMindShockSound();

        yield return FlashCoroutine(
            LethalTrapVengeanceShockTuning.PeakFlashSeconds,
            pink: true);

        float holdElapsed = 0f;
        while (holdElapsed < LethalTrapVengeanceShockTuning.HoldAtMaxSeconds)
        {
            holdElapsed += Time.deltaTime;
            MindBrokenSystem.SetScriptedPercent(LethalTrapVengeanceShockTuning.MaxPercent);
            yield return null;
        }

        float floor = LethalTrapVengeanceShockTuning.FloorPercent;
        float decayDuration = LethalTrapVengeanceShockTuning.DecayToFloorSeconds;
        float decayElapsed = 0f;
        float decayFrom = LethalTrapVengeanceShockTuning.MaxPercent;
        while (decayElapsed < decayDuration)
        {
            decayElapsed += Time.deltaTime;
            float tRaw = decayDuration > 0f ? Mathf.Clamp01(decayElapsed / decayDuration) : 1f;
            float t = SmoothStep01(tRaw);
            float p = Mathf.Lerp(decayFrom, floor, t);
            MindBrokenSystem.SetScriptedPercent(p);
            yield return null;
        }

        MindBrokenSystem.SetScriptedPercent(floor);
        StopGlow();

        yield return FlashCoroutine(
            LethalTrapVengeanceShockTuning.FloorFlashSeconds,
            pink: false);

        Plugin.Log?.LogInfo(
            "[LethalTrapVengeanceShock] Complete — MB at "
            + (MindBrokenSystem.Percent * 100f).ToString("0.#")
            + "%");

        LethalTrapVengeanceShockAudio.StopHeartBeatLoop();
        StopGlow();
        HideOverlay();
        MindBrokenSystem.EndScriptedSequence(LethalTrapVengeanceShockTuning.FloorPercent);
        _sequenceRunning = false;
        _sequenceCoroutine = null;
    }

    private static void EnsureOverlay()
    {
        if (_canvasObject != null && _flashImage != null && _glowImage != null)
            return;

        GameObject existing = GameObject.Find(CanvasName);
        if (existing != null)
        {
            _canvasObject = existing;
            _flashImage = existing.transform.Find("Flash")?.GetComponent<Image>();
            _glowImage = existing.transform.Find("PinkGlow")?.GetComponent<Image>();
            if (_flashImage != null && _glowImage != null)
                return;
        }

        _canvasObject = new GameObject(CanvasName);
        UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

        Canvas canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        _canvasObject.AddComponent<CanvasScaler>();
        _canvasObject.AddComponent<GraphicRaycaster>();

        _glowImage = CreateFullscreenImage(
            _canvasObject.transform,
            "PinkGlow",
            new Color(
                LethalTrapVengeanceShockTuning.PinkColorR,
                LethalTrapVengeanceShockTuning.PinkColorG,
                LethalTrapVengeanceShockTuning.PinkColorB,
                0f));
        _flashImage = CreateFullscreenImage(_canvasObject.transform, "Flash", new Color(1f, 1f, 1f, 0f));
        _glowImage.raycastTarget = false;
        _flashImage.raycastTarget = false;
        _glowImage.gameObject.SetActive(false);
        _flashImage.gameObject.SetActive(false);
    }

    private static Image CreateFullscreenImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static void HideOverlay()
    {
        if (_flashImage != null)
        {
            _flashImage.color = new Color(_flashImage.color.r, _flashImage.color.g, _flashImage.color.b, 0f);
            _flashImage.gameObject.SetActive(false);
        }

        if (_glowImage != null)
        {
            _glowImage.color = new Color(_glowImage.color.r, _glowImage.color.g, _glowImage.color.b, 0f);
            _glowImage.gameObject.SetActive(false);
        }
    }

    private static void StartPinkGlow()
    {
        EnsureOverlay();
        if (_glowImage == null || Plugin.Instance == null)
            return;

        StopGlow();
        _glowImage.gameObject.SetActive(true);
        _glowCoroutine = Plugin.Instance.StartCoroutine(PinkGlowCoroutine());
    }

    private static void StopGlow()
    {
        if (Plugin.Instance != null && _glowCoroutine != null)
            Plugin.Instance.StopCoroutine(_glowCoroutine);

        _glowCoroutine = null;

        if (_glowImage != null)
        {
            Color c = _glowImage.color;
            _glowImage.color = new Color(c.r, c.g, c.b, 0f);
            _glowImage.gameObject.SetActive(false);
        }
    }

    private static IEnumerator PinkGlowCoroutine()
    {
        if (_glowImage == null)
            yield break;

        float minA = LethalTrapVengeanceShockTuning.PinkGlowMinAlpha;
        float maxA = LethalTrapVengeanceShockTuning.PinkGlowMaxAlpha;
        float hz = LethalTrapVengeanceShockTuning.PinkGlowPulseHz;
        Color baseColor = new Color(
            LethalTrapVengeanceShockTuning.PinkColorR,
            LethalTrapVengeanceShockTuning.PinkColorG,
            LethalTrapVengeanceShockTuning.PinkColorB,
            0f);

        while (true)
        {
            float pulse = (Mathf.Sin(Time.time * hz * Mathf.PI * 2f) + 1f) * 0.5f;
            float smoothPulse = SmoothStep01(pulse);
            float alpha = Mathf.Lerp(minA, maxA, smoothPulse);
            baseColor.a = alpha;
            _glowImage.color = baseColor;
            yield return null;
        }
    }

    private static IEnumerator FlashCoroutine(float duration, bool pink)
    {
        EnsureOverlay();
        if (_flashImage == null)
            yield break;

        Color flashColor = pink
            ? new Color(
                LethalTrapVengeanceShockTuning.PinkColorR,
                LethalTrapVengeanceShockTuning.PinkColorG,
                LethalTrapVengeanceShockTuning.PinkColorB,
                0f)
            : new Color(1f, 1f, 1f, 0f);

        _flashImage.gameObject.SetActive(true);

        float elapsed = 0f;
        int cycles = pink ? 2 : 3;
        float minAlpha = pink ? 0.06f : 0.08f;
        float maxAlpha = pink ? 0.17f : 0.22f;
        float fadeOutTime = Mathf.Min(pink ? 1.0f : 0.8f, duration * 0.45f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float cycleProgress = duration > 0f ? (elapsed / duration) * cycles : 0f;
            float pulseValue = Mathf.Sin(cycleProgress * 2f * Mathf.PI);
            float normalizedPulse = (pulseValue + 1f) * 0.5f;
            float smoothPulse = SmoothStep01(normalizedPulse);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, smoothPulse);

            if (elapsed > duration - fadeOutTime && fadeOutTime > 0f)
            {
                float fadeOutProgress = (duration - elapsed) / fadeOutTime;
                alpha *= fadeOutProgress;
            }

            flashColor.a = alpha;
            _flashImage.color = flashColor;
            yield return null;
        }

        flashColor.a = 0f;
        _flashImage.color = flashColor;
        _flashImage.gameObject.SetActive(false);
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

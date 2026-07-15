using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.CombatAi.Factions;

internal static class MercyEventUISystem
{
    private static GameObject _canvasGo;
    private static CanvasGroup _barCg;
    private static Image _barFill;
    private static RectTransform _barFillRt;
    private static Text _titleText;
    private static Text _resultText;
    private static float _resultUntil;
    private const float BarMaxWidth = 360f;

    internal static void Process(playercon player)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.EnableDeescalationRollEvent || player == null)
            return;

        EnsureUi();
        if (_canvasGo == null || _barCg == null || _barFill == null)
            return;

        if (FactionDeescalationRuntime.IsEventActive)
        {
            _barCg.alpha = 1f;
            float progress = FactionDeescalationRuntime.GetProgress01();
            float remaining01 = Mathf.Clamp01(1f - progress);
            if (_barFillRt != null)
                _barFillRt.sizeDelta = new Vector2(BarMaxWidth * remaining01, 14f);
            _barFill.color = FactionDeescalationRuntime.IsLatePenaltyWindow()
                ? new Color(0.96f, 0.58f, 0.14f, 1f)
                : new Color(0.36f, 0.82f, 1f, 1f); // diplomacy blue
        }
        else
        {
            _barCg.alpha = 0f;
        }

        if (FactionDeescalationRuntime.TryConsumeUiResult(out string result))
        {
            _resultUntil = Time.unscaledTime + 2.2f;
            if (_resultText != null)
            {
                _resultText.text = result;
                _resultText.enabled = true;
            }
        }

        if (_resultText != null && _resultText.enabled && Time.unscaledTime > _resultUntil)
        {
            _resultText.enabled = false;
        }
    }

    private static void EnsureUi()
    {
        if (_canvasGo != null)
            return;

        _canvasGo = new GameObject("MercyEventCanvas");
        Canvas canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9800;
        _canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        _canvasGo.AddComponent<GraphicRaycaster>();

        GameObject root = new GameObject("MercyRoot");
        root.transform.SetParent(_canvasGo.transform, false);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -32f);
        rootRt.sizeDelta = new Vector2(420f, 56f);

        _barCg = root.AddComponent<CanvasGroup>();
        _barCg.alpha = 0f;

        GameObject title = new GameObject("Title");
        title.transform.SetParent(root.transform, false);
        RectTransform titleRt = title.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -2f);
        titleRt.sizeDelta = new Vector2(300f, 22f);
        _titleText = title.AddComponent<Text>();
        _titleText.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
        _titleText.text = "Mercy";
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = new Color(0.74f, 0.93f, 1f, 1f);
        _titleText.fontSize = 17;

        GameObject bg = new GameObject("BarBg");
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.5f, 1f);
        bgRt.anchorMax = new Vector2(0.5f, 1f);
        bgRt.pivot = new Vector2(0.5f, 1f);
        bgRt.anchoredPosition = new Vector2(0f, -24f);
        bgRt.sizeDelta = new Vector2(BarMaxWidth, 14f);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.17f, 0.75f);

        GameObject fill = new GameObject("BarFill");
        fill.transform.SetParent(bg.transform, false);
        _barFillRt = fill.AddComponent<RectTransform>();
        _barFillRt.anchorMin = new Vector2(0f, 0.5f);
        _barFillRt.anchorMax = new Vector2(0f, 0.5f);
        _barFillRt.pivot = new Vector2(0f, 0.5f);
        _barFillRt.anchoredPosition = Vector2.zero;
        _barFillRt.sizeDelta = new Vector2(BarMaxWidth, 14f);
        _barFill = fill.AddComponent<Image>();
        _barFill.color = new Color(0.36f, 0.82f, 1f, 1f);

        GameObject result = new GameObject("Result");
        result.transform.SetParent(_canvasGo.transform, false);
        RectTransform resultRt = result.AddComponent<RectTransform>();
        resultRt.anchorMin = new Vector2(0.5f, 1f);
        resultRt.anchorMax = new Vector2(0.5f, 1f);
        resultRt.pivot = new Vector2(0.5f, 1f);
        resultRt.anchoredPosition = new Vector2(0f, -94f);
        resultRt.sizeDelta = new Vector2(520f, 26f);
        _resultText = result.AddComponent<Text>();
        _resultText.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
        _resultText.alignment = TextAnchor.MiddleCenter;
        _resultText.fontSize = 18;
        _resultText.color = new Color(0.77f, 0.94f, 1f, 1f);
        _resultText.enabled = false;
    }
}

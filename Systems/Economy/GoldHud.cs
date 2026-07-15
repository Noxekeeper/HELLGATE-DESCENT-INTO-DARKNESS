using System;
using NoREroMod.Systems.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// On-screen HUD for the gold wallet. Built from code, no prefab. Mirrors the lifecycle
/// of <see cref="NoREroMod.Systems.CombatAi.Factions.FactionReputationHud"/>: idempotent
/// <see cref="Ensure"/>, root <c>DontDestroyOnLoad</c>, re-bootstrapped from
/// <c>UImng.Start</c> and <c>CanvasBadstatusinfo.Start</c> so it survives scene transitions.
///
/// The HUD is an <see cref="Image"/> + <see cref="Text"/> placed directly on the canvas
/// (no intermediate "Row" container). RectTransforms are created via the GameObject
/// constructor — adding RectTransform after a regular Transform exists is unreliable
/// in Unity 2018-era builds and was the cause of the HUD ignoring anchor settings.
/// </summary>
internal sealed class GoldHud : MonoBehaviour
{
    private const string RootObjectName = "GoldHud_XUAIGNORE";
    private const string CanvasObjectName = "GoldHudCanvas";

    private static GoldHud _instance;

    private Canvas _canvas;
    private Image _icon;
    private Text _label;
    private long _displayedValue = -1;

    public static void Ensure()
    {
        if (!EconomicConfig.Enable || !EconomicConfig.Hud.Enable)
        {
            Destroy();
            return;
        }
        if (_instance != null && _instance.gameObject != null)
        {
            _instance.RebuildLayout();
            return;
        }
        try
        {
            GameObject root = new GameObject(RootObjectName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            _instance = root.AddComponent<GoldHud>();
            _instance.Build();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldHud] Failed to create HUD: " + ex.Message);
        }
    }

    public static void Destroy()
    {
        if (_instance != null && _instance.gameObject != null)
        {
            try { UnityEngine.Object.Destroy(_instance.gameObject); } catch { }
            _instance = null;
        }
    }

    private void OnEnable()
    {
        GoldWallet.OnChanged += HandleWalletChanged;
        Refresh(GoldWallet.Current, force: true);
    }

    private void OnDisable()
    {
        GoldWallet.OnChanged -= HandleWalletChanged;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void LateUpdate()
    {
        if (_canvas == null)
            return;

        // Keep gold visible in the status/inventory menu, but hide during NPC diary readers
        // so overlay sorting does not cover diary CG pages.
        bool show = !HudVisibilityGate.IsNpcDiaryReaderOpen();
        if (_canvas.enabled != show)
            _canvas.enabled = show;
    }

    private void HandleWalletChanged(long oldValue, long newValue) => Refresh(newValue, force: false);

    private void Refresh(long value, bool force)
    {
        if (!force && value == _displayedValue) return;
        _displayedValue = value;
        if (_label != null) _label.text = value.ToString();
    }

    private void Build()
    {
        // Canvas root.
        GameObject canvasGo = new GameObject(CanvasObjectName, typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 850; // below FactionReputationHud (900) and MindBroken (1000)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

        // Icon (Image with RectTransform auto-added by Image's [RequireComponent]).
        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(canvasGo.transform, false);
        _icon = iconGo.AddComponent<Image>();
        if (GoldAssetLoader.HasUiIcon)
            _icon.sprite = GoldAssetLoader.UiIcon;
        _icon.color = Color.white;
        _icon.raycastTarget = false;

        // Label.
        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(canvasGo.transform, false);
        _label = labelGo.AddComponent<Text>();
        _label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
        _label.alignment = TextAnchor.MiddleLeft;
        _label.text = "0";
        _label.raycastTarget = false;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;

        RebuildLayout();
        Refresh(GoldWallet.Current, force: true);

        if (EconomicConfig.DebugLogging)
        {
            EconomicHudSettings hud = EconomicConfig.Hud;
            Plugin.Log?.LogInfo($"[GoldHud] Built. anchor=({hud.AnchorX},{hud.AnchorY}) pos=({hud.AnchoredPositionX},{hud.AnchoredPositionY}) icon={(_icon.sprite!=null?"ok":"null")}");
        }
    }

    private void RebuildLayout()
    {
        if (_canvas == null || _icon == null || _label == null) return;

        EconomicHudSettings hud = EconomicConfig.Hud;
        float iconSize = Mathf.Max(8, hud.IconSizePx);
        float fontSize = Mathf.Max(8, hud.FontSize);
        float gap = 6f;
        float labelWidth = 220f;

        Vector2 anchor = new Vector2(hud.AnchorX, hud.AnchorY);
        Vector2 anchoredPos = new Vector2(hud.AnchoredPositionX, hud.AnchoredPositionY);
        bool anchorBottom = hud.AnchorY <= 0.01f;

        RectTransform iconRt = _icon.rectTransform;
        iconRt.anchorMin = anchor;
        iconRt.anchorMax = anchor;
        iconRt.pivot = anchorBottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        iconRt.anchoredPosition = anchoredPos;
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);

        RectTransform labelRt = _label.rectTransform;
        labelRt.anchorMin = anchor;
        labelRt.anchorMax = anchor;
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.anchoredPosition = anchorBottom
            ? new Vector2(anchoredPos.x + iconSize + gap, anchoredPos.y + iconSize * 0.5f)
            : new Vector2(anchoredPos.x + iconSize + gap, anchoredPos.y - iconSize * 0.5f);
        labelRt.sizeDelta = new Vector2(labelWidth, iconSize);

        _label.fontSize = (int)fontSize;
        if (ColorUtility.TryParseHtmlString(hud.TextColorHex, out Color c))
            _label.color = c;
        else
            _label.color = new Color(1f, 0.78f, 0.24f, 1f);
    }
}

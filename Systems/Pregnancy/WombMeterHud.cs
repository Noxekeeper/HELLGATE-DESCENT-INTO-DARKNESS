using System;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.Effects;
using NoREroMod.Systems.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Minimal on-screen womb fill meter (bar + percentage), built from code (no prefab).
/// Mirrors the lifecycle of <see cref="NoREroMod.Systems.Economy.GoldHud"/>: idempotent
/// <see cref="Ensure"/>, root <c>DontDestroyOnLoad</c>, re-bootstrapped from <c>UImng.Start</c>
/// and <c>CanvasBadstatusinfo.Start</c>. Polls <see cref="WitchWombMeter"/> each frame.
///
/// Visibility follows the vanilla HUD but stays visible during HSceneBlackBackgroundSystem
/// (same behaviour as MindBroken UI).
/// </summary>
internal sealed class WombMeterHud : MonoBehaviour
{
    private const string RootObjectName = "WombMeterHud_XUAIGNORE";
    private const string CanvasObjectName = "WombMeterHudCanvas";

    private static WombMeterHud _instance;
    private static Sprite _solidSprite;

    private Canvas _canvas;
    private Image _fill;
    private Text _label;
    private Text _debuffLabel;

    /// <summary>
    /// When true, HUD stays visible even when the vanilla HUD is hidden
    /// (e.g., during H-scene black background). Mirroring MindBrokenUI behaviour.
    /// </summary>
    internal static bool ForceShowDuringBlackBackground { get; set; } = true;

    public static void Ensure()
    {
        if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value ||
            PregnancyConfig.ShowWombMeter == null || !PregnancyConfig.ShowWombMeter.Value)
        {
            Destroy();
            return;
        }
        if (_instance != null && _instance.gameObject != null)
            return;

        try
        {
            GameObject root = new GameObject(RootObjectName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            _instance = root.AddComponent<WombMeterHud>();
            _instance.Build();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[WombMeterHud] Failed to create HUD: " + ex.Message);
        }
    }

    public static void Destroy()
    {
        if (_instance != null && _instance.gameObject != null)
        {
            try { UnityEngine.Object.Destroy(_instance.gameObject); } catch { }
        }
        _instance = null;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// Determines if the HUD should be visible.
    /// Follows vanilla HUD visibility but stays visible during HSceneBlackBackgroundSystem
    /// when <see cref="ForceShowDuringBlackBackground"/> is true (default).
    /// </summary>
    private bool ShouldShowHud()
    {
        if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
            return false;
        if (PregnancyConfig.ShowWombMeter == null || !PregnancyConfig.ShowWombMeter.Value)
            return false;

        // Check vanilla HUD gate
        bool vanillaHudVisible = HudVisibilityGate.ShouldShowGameplayHud();

        // If vanilla HUD is visible, we're definitely showing
        if (vanillaHudVisible)
            return true;

        // Vanilla HUD is hidden - check if we should stay visible (like MindBroken UI)
        if (!ForceShowDuringBlackBackground)
            return false;

        // Only force-show during black background H-scenes, not in menus/loading
        try
        {
            if (HSceneBlackBackgroundSystem.IsActive)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[WombMeterHud] Error checking HSceneBlackBackgroundSystem: {ex.Message}");
        }

        return false;
    }

    private void LateUpdate()
    {
        if (_canvas == null)
            return;

        bool show = ShouldShowHud();

        if (_canvas.enabled != show)
            _canvas.enabled = show;
        if (!show)
            return;

        if (WitchPregnancyState.IsActive)
        {
            // Pregnancy in gestation: show overall progress and trimester info.
            float progress = WitchPregnancyState.ProgressRatio;
            if (_fill != null)
            {
                _fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, WombMeterHudLayout.BarWidth * progress), WombMeterHudLayout.BarHeight);
                _fill.color = TrimesterColor(WitchPregnancyState.CurrentTrimester);
            }
            if (_label != null)
            {
                float remaining = Mathf.Max(0f, WitchPregnancyState.GestationTotalSeconds - WitchPregnancyState.GestationElapsedSeconds);
                _label.text = $"Pregnant: {FactionKey(WitchPregnancyState.SourceFaction)} | " +
                              $"Trimester {WitchPregnancyState.CurrentTrimester} | {remaining:0}s";
            }
            if (_debuffLabel != null)
                _debuffLabel.text = TrimesterBuffLabel(WitchPregnancyState.SourceFaction);
            return;
        }

        if (WitchPregnancyState.HasPending)
        {
            if (_fill != null)
            {
                _fill.rectTransform.sizeDelta = new Vector2(WombMeterHudLayout.BarWidth, WombMeterHudLayout.BarHeight);
                _fill.color = new Color(0.95f, 0.45f, 0.65f, 0.97f);
            }
            if (_label != null)
                _label.text = "Conceiving: " + FactionKey(WitchPregnancyState.PendingFaction);
            if (_debuffLabel != null)
                _debuffLabel.text = string.Empty;
            return;
        }

        float ratio = WitchWombMeter.FillRatio;
        if (_fill != null)
        {
            _fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, WombMeterHudLayout.BarWidth * ratio), WombMeterHudLayout.BarHeight);
            _fill.color = ColorForRatio(ratio);
        }
        if (_label != null)
        {
            string faction = DescribeDominant();
            _label.text = $"Womb {ratio * 100f:0}%  ({WitchWombMeter.TotalMl:0}/{WitchWombMeter.Capacity:0}ml){faction}";
        }
        if (_debuffLabel != null)
            _debuffLabel.text = string.Empty;
    }

    private static string FactionKey(int factionId)
    {
        if (factionId == FactionIds.Neutral)
            return "?";
        try { return EconomicFactionUtil.FactionIdToKey(factionId); }
        catch { return "?"; }
    }

    private static string DescribeDominant()
    {
        if (WitchWombMeter.TotalMl <= 0f)
            return "";
        int dom = WitchWombMeter.GetDominantFaction();
        if (dom == FactionIds.Neutral)
            return "";
        try { return "  " + EconomicFactionUtil.FactionIdToKey(dom); }
        catch { return ""; }
    }

    private static Color ColorForRatio(float r)
    {
        // Green -> amber -> red as the womb fills.
        if (r < 0.5f) return new Color(0.45f, 0.85f, 0.45f, 0.95f);
        if (r < 0.85f) return new Color(0.95f, 0.75f, 0.30f, 0.95f);
        return new Color(0.95f, 0.30f, 0.45f, 0.97f);
    }

    private static Color TrimesterColor(int trimester)
    {
        return trimester switch
        {
            1 => new Color(0.45f, 0.85f, 0.45f, 0.95f), // green, hidden
            2 => new Color(0.95f, 0.75f, 0.30f, 0.95f), // amber, visible
            3 => new Color(0.95f, 0.30f, 0.45f, 0.97f), // red, hardcore
            _ => ColorForRatio(0f)
        };
    }

    private static string TrimesterBuffLabel(int factionId)
    {
        int penalty = TrimesterDebuffs.StrPenalty; // negative, e.g. -6
        if (penalty >= 0)
            return string.Empty;

        // "Debuff:  STR INT DEX LUCK -6" — no confusing double dash, stats grouped, value once.
        string label = $"Debuff:  STR INT DEX LUCK {penalty}";

        if (WitchPregnancyState.CurrentTrimester >= 2)
        {
            float spdPenalty = PregnancyConfig.TrimesterMoveSpeedPenalty?.Value ?? 0.30f;
            int spdPct = Mathf.RoundToInt(Mathf.Clamp01(spdPenalty) * 100f);
            label += $"   SPD -{spdPct}%";
        }

        return label;
    }

    private void Build()
    {
        GameObject canvasGo = new GameObject(CanvasObjectName, typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 860;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

        Sprite solid = GetSolidSprite();

        // Position under MindBroken UI (right side of screen, below MindBroken label).
        Vector2 anchorTopRight = new Vector2(0f, 1f); // Top-left anchor (for right-side positioning)
        float rightSideX = WombMeterHudLayout.RightSideX;
        float topPosY = WombMeterHudLayout.TopPosY;
        Vector2 barPos = new Vector2(rightSideX, topPosY);

        // Background.
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = solid;
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        RectTransform bgRt = bg.rectTransform;
        bgRt.anchorMin = anchorTopRight; bgRt.anchorMax = anchorTopRight; bgRt.pivot = anchorTopRight;
        bgRt.anchoredPosition = barPos;
        bgRt.sizeDelta = new Vector2(WombMeterHudLayout.BarWidth, WombMeterHudLayout.BarHeight);

        // Fill (anchored to the left edge of the background, grows rightward).
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        _fill = fillGo.AddComponent<Image>();
        _fill.sprite = solid;
        _fill.color = ColorForRatio(0f);
        _fill.raycastTarget = false;
        RectTransform fillRt = _fill.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = new Vector2(0f, WombMeterHudLayout.BarHeight);

        // Main label above the bar, centered horizontally over the bar.
        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(canvasGo.transform, false);
        _label = labelGo.AddComponent<Text>();
        _label.font = HellGateFontProvider.GetUiFont();
        _label.alignment = TextAnchor.LowerCenter; // centered over the bar
        _label.fontSize = 18;
        _label.color = new Color(1f, 0.92f, 0.95f, 1f);
        _label.raycastTarget = false;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        _label.text = "Womb 0%";
        AddReadableOutline(_label);
        RectTransform labelRt = _label.rectTransform;
        labelRt.anchorMin = anchorTopRight; labelRt.anchorMax = anchorTopRight; labelRt.pivot = new Vector2(0f, 0f);
        labelRt.anchoredPosition = new Vector2(rightSideX, topPosY + 17f); // lowered 10px, sits just above the bar
        labelRt.sizeDelta = new Vector2(WombMeterHudLayout.BarWidth, 24f); // width == bar so centering aligns to the bar center

        // Debuff label 10px below the bar, centered horizontally over the bar.
        GameObject debuffGo = new GameObject("DebuffLabel");
        debuffGo.transform.SetParent(canvasGo.transform, false);
        _debuffLabel = debuffGo.AddComponent<Text>();
        _debuffLabel.font = HellGateFontProvider.GetUiFont();
        _debuffLabel.alignment = TextAnchor.UpperCenter; // centered over the bar
        _debuffLabel.fontSize = 17;
        _debuffLabel.fontStyle = FontStyle.Bold;
        _debuffLabel.color = new Color(1f, 0.62f, 0.6f, 1f);
        _debuffLabel.raycastTarget = false;
        _debuffLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _debuffLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _debuffLabel.text = string.Empty;
        AddReadableOutline(_debuffLabel);
        RectTransform debuffRt = _debuffLabel.rectTransform;
        debuffRt.anchorMin = anchorTopRight; debuffRt.anchorMax = anchorTopRight; debuffRt.pivot = new Vector2(0f, 1f);
        debuffRt.anchoredPosition = new Vector2(rightSideX, topPosY - WombMeterHudLayout.BarHeight - 10f);
        debuffRt.sizeDelta = new Vector2(WombMeterHudLayout.BarWidth, 24f); // width == bar so centering aligns to the bar center
    }

    /// <summary>
    /// Adds a dark outline + drop shadow so light text stays legible over bright backgrounds.
    /// </summary>
    private static void AddReadableOutline(Text text)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.6f, -1.6f);

        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    private static Sprite GetSolidSprite()
    {
        if (_solidSprite != null)
            return _solidSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        _solidSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        return _solidSprite;
    }
}

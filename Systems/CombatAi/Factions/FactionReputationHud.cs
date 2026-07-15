using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod.Systems.Pregnancy.Patches;
using NoREroMod.Systems.UI;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Minimal player ↔ faction reputation HUD.
/// Renders a vertical column of faction rows on the right side of the screen.
/// Each row shows:
///   - a colored dot (faction color)
///   - the faction's display name
///   - a percent value (-100%..+100%) for standard factions, or offspring count for Witch.
///
/// The HUD is driven by <see cref="PlayerFactionReputation"/> and reads faction colors from
/// <see cref="EnemyFactionRuntime.TryGetFactionTintColor"/>. Intentionally built from code
/// so it does not require any custom art assets.
/// </summary>
internal sealed class FactionReputationHud : MonoBehaviour
{
    private const string RootObjectName = "FactionReputationHud_XUAIGNORE";
    private const string CanvasObjectName = "FactionReputationCanvas";
    private const float ColumnOffsetX = -24f;
    private const float ColumnOffsetY = 70f;
    private const float RowHeight = 30f;
    private const int HudFontSize = 18;
    private const float RowPadLeft = 8f;
    private const float RowPadRight = 10f;
    private const float RowGap = 7f;
    private const float IconSize = 16f;
    private const float MinNameWidth = 60f;
    private const float MinRowWidth = 150f;

    // Colors used for the reputation tint (red -> cyan/light-blue).
    private static readonly Color HostileColor = new Color(0.95f, 0.22f, 0.27f, 1f);
    private static readonly Color NeutralColor = new Color(0.75f, 0.75f, 0.78f, 1f);
    private static readonly Color FriendlyColor = new Color(0.35f, 0.80f, 1.00f, 1f);

    private struct FactionEntry
    {
        public int FactionId;
        public string Label;
        public FactionEntry(int factionId, string label) { FactionId = factionId; Label = label; }
    }

    private static readonly FactionEntry[] DisplayedFactions =
    {
        new FactionEntry(FactionIds.Bandits,                 "Bandits"),
        new FactionEntry(FactionIds.BanditsInquisitionLoyal, "Bandits (Inq)"),
        new FactionEntry(FactionIds.BanditsMafiaLoyal,       "Bandits (Maf)"),
        new FactionEntry(FactionIds.BanditsDemonsLoyal,      "Bandits (Dem)"),
        new FactionEntry(FactionIds.Church,                  "Church"),
        new FactionEntry(FactionIds.Mafia,                   "Mafia"),
        new FactionEntry(FactionIds.Demons,                  "Demons"),
        new FactionEntry(FactionIds.Undead,                  "Undead"),
        new FactionEntry(FactionIds.Monsters,                "Monsters"),
        new FactionEntry(FactionIds.Witch,                   "Witch"),
    };

    private static FactionReputationHud _instance;
    // Remember collapsed/expanded state across HUD re-creations (scene changes, etc).
    private static bool _columnVisible = false;

    public const KeyCode ToggleKey = KeyCode.H;

    private readonly List<Row> _rows = new List<Row>();
    private Canvas _canvas;
    private RectTransform _column;
    private GameObject _collapsedHint;
    private Image _collapsedHintBg;
    private float _refreshTimer;

    public static void Ensure()
    {
        if (_instance != null && _instance.gameObject != null)
            return;

        try
        {
            GameObject root = new GameObject(RootObjectName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            _instance = root.AddComponent<FactionReputationHud>();
            _instance.Build();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[FactionReputationHud] Failed to create HUD: " + ex.Message);
        }
    }

    public static void Destroy()
    {
        if (_instance != null)
        {
            try { UnityEngine.Object.Destroy(_instance.gameObject); } catch { }
            _instance = null;
        }
    }

    private void Build()
    {
        GameObject canvasGo = new GameObject(CanvasObjectName);
        canvasGo.transform.SetParent(transform, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 900; // below MindBroken (1000), above most HUDs.

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject columnGo = new GameObject("Column");
        columnGo.transform.SetParent(canvasGo.transform, false);
        _column = columnGo.AddComponent<RectTransform>();
        // Right-center of the screen, slightly in from the edge.
        _column.anchorMin = new Vector2(1f, 0.5f);
        _column.anchorMax = new Vector2(1f, 0.5f);
        _column.pivot = new Vector2(1f, 0.5f);
        _column.anchoredPosition = new Vector2(ColumnOffsetX, ColumnOffsetY);
        _column.sizeDelta = new Vector2(220f, 540f);

        VerticalLayoutGroup vlg = columnGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.MiddleRight;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = columnGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Font font = null;
        try { font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont(); }
        catch { font = null; }

        float pctWidth = Mathf.Ceil(CalculateTextWidth("-100%", font, HudFontSize)) + 6f;
        float maxNameWidth = MinNameWidth;
        for (int i = 0; i < DisplayedFactions.Length; i++)
        {
            float w = Mathf.Ceil(CalculateTextWidth(DisplayedFactions[i].Label, font, HudFontSize)) + 2f;
            if (w > maxNameWidth)
                maxNameWidth = w;
        }

        foreach (FactionEntry entry in DisplayedFactions)
        {
            _rows.Add(BuildRow(columnGo.transform, entry.FactionId, entry.Label, font, pctWidth, maxNameWidth));
        }

        BuildCollapsedHint(canvasGo.transform, font);
        ApplyVisibility();
        RefreshRows();
    }

    private void BuildCollapsedHint(Transform parent, Font font)
    {
        GameObject hintGo = new GameObject("CollapsedHint");
        hintGo.transform.SetParent(parent, false);
        hintGo.layer = LayerMask.NameToLayer("UI");

        RectTransform hintRt = hintGo.AddComponent<RectTransform>();
        // Same right-edge anchor as the column so the hint appears in the HUD's place.
        hintRt.anchorMin = new Vector2(1f, 0.5f);
        hintRt.anchorMax = new Vector2(1f, 0.5f);
        hintRt.pivot = new Vector2(1f, 0.5f);
        hintRt.anchoredPosition = new Vector2(ColumnOffsetX, ColumnOffsetY);
        hintRt.sizeDelta = new Vector2(44f, 44f);

        _collapsedHintBg = hintGo.AddComponent<Image>();
        _collapsedHintBg.color = new Color(0f, 0f, 0f, 0.6f);
        _collapsedHintBg.sprite = GetCircleSprite();
        _collapsedHintBg.type = Image.Type.Simple;
        _collapsedHintBg.raycastTarget = false;

        GameObject textGo = new GameObject("KeyLabel");
        textGo.transform.SetParent(hintGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text keyText = textGo.AddComponent<Text>();
        keyText.text = "H";
        keyText.font = font;
        keyText.fontSize = 26;
        keyText.fontStyle = FontStyle.Bold;
        keyText.color = new Color(1f, 0.95f, 0.75f, 1f);
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.raycastTarget = false;

        _collapsedHint = hintGo;
    }

    private void ApplyVisibility()
    {
        // Mirror vanilla HUD: while the game hides its own HUD (menus, loading, BadEnd,
        // some cutscenes) we hide the entire reputation column AND the collapsed hint,
        // regardless of whether the player toggled us off with H.
        bool hudShown = HudVisibilityGate.ShouldShowGameplayHud();

        if (_column != null)
            _column.gameObject.SetActive(hudShown && _columnVisible);
        if (_collapsedHint != null)
            _collapsedHint.SetActive(hudShown && !_columnVisible);
    }

    private Row BuildRow(Transform parent, int factionId, string label, Font font, float pctWidth, float maxNameWidth)
    {
        // Keep all rows perfectly aligned while still compact: width is based on the
        // longest faction label in the current display list.
        float nameWidth = Mathf.Max(MinNameWidth, maxNameWidth);
        float rowWidth = Mathf.Max(MinRowWidth, RowPadLeft + pctWidth + RowGap + IconSize + RowGap + nameWidth + RowPadRight);

        GameObject rowGo = new GameObject("Row_" + label);
        rowGo.transform.SetParent(parent, false);
        rowGo.layer = LayerMask.NameToLayer("UI");

        RectTransform rowRt = rowGo.AddComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(rowWidth, RowHeight);

        LayoutElement le = rowGo.AddComponent<LayoutElement>();
        le.preferredWidth = rowWidth;
        le.minWidth = rowWidth;
        le.preferredHeight = RowHeight;
        le.minHeight = RowHeight;

        // Background strip: tinted by reputation (red -> cyan).
        GameObject bgGo = new GameObject("BG");
        bgGo.transform.SetParent(rowGo.transform, false);
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        bgImg.raycastTarget = false;

        // Reputation strip on the left edge of the row — its color slides from red -> cyan.
        GameObject stripGo = new GameObject("RepStrip");
        stripGo.transform.SetParent(rowGo.transform, false);
        RectTransform stripRt = stripGo.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(0f, 1f);
        stripRt.pivot = new Vector2(0f, 0.5f);
        stripRt.sizeDelta = new Vector2(6f, 0f);
        stripRt.anchoredPosition = new Vector2(0f, 0f);
        Image stripImg = stripGo.AddComponent<Image>();
        stripImg.color = NeutralColor;
        stripImg.raycastTarget = false;

        // Percent text (+/- value).
        GameObject pctGo = new GameObject("Pct");
        pctGo.transform.SetParent(rowGo.transform, false);
        RectTransform pctRt = pctGo.AddComponent<RectTransform>();
        pctRt.anchorMin = new Vector2(0f, 0f);
        pctRt.anchorMax = new Vector2(0f, 1f);
        pctRt.pivot = new Vector2(0f, 0.5f);
        pctRt.sizeDelta = new Vector2(pctWidth, 0f);
        pctRt.anchoredPosition = new Vector2(RowPadLeft, 0f);
        Text pctText = pctGo.AddComponent<Text>();
        pctText.text = "0%";
        pctText.font = font;
        pctText.fontSize = HudFontSize;
        pctText.fontStyle = FontStyle.Bold;
        pctText.color = NeutralColor;
        pctText.alignment = TextAnchor.MiddleRight;
        pctText.horizontalOverflow = HorizontalWrapMode.Overflow;
        pctText.verticalOverflow = VerticalWrapMode.Overflow;
        pctText.raycastTarget = false;

        // Faction icon (same png as world faction markers, when available).
        float iconX = RowPadLeft + pctWidth + RowGap;
        GameObject dotGo = new GameObject("FactionIcon");
        dotGo.transform.SetParent(rowGo.transform, false);
        RectTransform dotRt = dotGo.AddComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(0f, 0.5f);
        dotRt.anchorMax = new Vector2(0f, 0.5f);
        dotRt.pivot = new Vector2(0f, 0.5f);
        dotRt.sizeDelta = new Vector2(IconSize, IconSize);
        dotRt.anchoredPosition = new Vector2(iconX, 0f);
        Image dotImg = dotGo.AddComponent<Image>();
        ApplyFactionIconToImage(dotImg, factionId);
        dotImg.type = Image.Type.Simple;
        dotImg.raycastTarget = false;

        // Label text (faction name).
        float nameX = iconX + IconSize + RowGap;
        GameObject nameGo = new GameObject("Name");
        nameGo.transform.SetParent(rowGo.transform, false);
        RectTransform nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(0f, 1f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.sizeDelta = new Vector2(nameWidth, 0f);
        nameRt.anchoredPosition = new Vector2(nameX, 0f);
        Text nameText = nameGo.AddComponent<Text>();
        nameText.text = label;
        nameText.font = font;
        nameText.fontSize = HudFontSize;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;
        nameText.raycastTarget = false;

        return new Row
        {
            FactionId = factionId,
            Background = bgImg,
            Strip = stripImg,
            Dot = dotImg,
            NameText = nameText,
            PercentText = pctText,
        };
    }

    private void Update()
    {
        // Toggle visibility on the H key. Pulsing the hint avatar on the edge
        // when collapsed makes it obvious what the key does.
        if (Input.GetKeyDown(ToggleKey))
        {
            _columnVisible = !_columnVisible;
            ApplyVisibility();
        }

        if (_collapsedHint != null && _collapsedHint.activeSelf && _collapsedHintBg != null)
        {
            // Soft pulse to draw attention to the hint.
            float pulse = 0.45f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f));
            Color c = _collapsedHintBg.color;
            c.a = pulse;
            _collapsedHintBg.color = c;
        }

        // Avoid spamming UI refresh every frame; 0.2s is smooth enough for a reputation meter.
        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer < 0.2f)
            return;
        _refreshTimer = 0f;

        // Re-check vanilla HUD visibility on the same cadence so we hide/reappear
        // together with it when the player enters a cutscene / H-scene / menu.
        ApplyVisibility();
        RefreshRows();
    }

    private void RefreshRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            if (row == null || row.PercentText == null || row.Strip == null)
                continue;

            // Refresh faction color: Factions.json might have been hot-reloaded.
            if (row.Dot != null)
                ApplyFactionIconToImage(row.Dot, row.FactionId);

            if (row.FactionId == FactionIds.Witch)
            {
                int offspringCount = WitchFactionReputation.GetAliveOffspringCount();
                row.Strip.color = FriendlyColor;
                row.PercentText.color = FriendlyColor;
                row.PercentText.text = offspringCount.ToString();
                continue;
            }

            float score = PlayerFactionReputation.GetScore(row.FactionId);
            float clamped = Mathf.Clamp(score, PlayerFactionReputation.MinScore, PlayerFactionReputation.MaxScore);
            float t = Mathf.InverseLerp(PlayerFactionReputation.MinScore, PlayerFactionReputation.MaxScore, clamped);

            // Red -> Neutral grey around zero -> Cyan/Light-blue.
            Color tint = t < 0.5f
                ? Color.Lerp(HostileColor, NeutralColor, t * 2f)
                : Color.Lerp(NeutralColor, FriendlyColor, (t - 0.5f) * 2f);

            row.Strip.color = tint;
            row.PercentText.color = tint;

            int percent = Mathf.RoundToInt(clamped);
            string sign = percent > 0 ? "+" : string.Empty;
            row.PercentText.text = sign + percent + "%";
        }
    }

    private static Color ResolveFactionColor(int factionId)
    {
        if (EnemyFactionRuntime.TryGetFactionTintColor(factionId, out Color c))
            return c;
        return Color.white;
    }

    private static void ApplyFactionIconToImage(Image image, int factionId)
    {
        if (image == null)
            return;

        if (FactionStyle.TryGetIconStyle(factionId, out FactionStyle.IconStyle style) &&
            style != null && style.Icon != null)
        {
            image.sprite = style.Icon;
            image.color = Color.white;
            return;
        }

        image.sprite = GetCircleSprite();
        image.color = ResolveFactionColor(factionId);
    }

    private static float CalculateTextWidth(string text, Font font, int fontSize)
    {
        if (string.IsNullOrEmpty(text) || font == null)
            return 0f;

        TextGenerationSettings settings = new TextGenerationSettings
        {
            textAnchor = TextAnchor.MiddleLeft,
            generateOutOfBounds = true,
            horizontalOverflow = HorizontalWrapMode.Overflow,
            verticalOverflow = VerticalWrapMode.Overflow,
            resizeTextForBestFit = false,
            richText = false,
            scaleFactor = 1f,
            font = font,
            color = Color.white,
            fontSize = fontSize,
            lineSpacing = 1f
        };

        var generator = new TextGenerator();
        return generator.GetPreferredWidth(text, settings);
    }

    // ----- sprite helpers -----
    private static Sprite _circleSprite;
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
            return _circleSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float cx = (size - 1) * 0.5f;
        float radius = size * 0.46f;
        float edge = size * 0.06f;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cx;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= radius - edge) tex.SetPixel(x, y, solid);
                else if (d <= radius + edge)
                {
                    float a = Mathf.InverseLerp(radius + edge, radius - edge, d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                else tex.SetPixel(x, y, clear);
            }
        }
        tex.Apply(false, false);
        _circleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }

    private sealed class Row
    {
        public int FactionId;
        public Image Background;
        public Image Strip;
        public Image Dot;
        public Text NameText;
        public Text PercentText;
    }
}

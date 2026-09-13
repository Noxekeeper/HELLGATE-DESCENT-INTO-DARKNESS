using System;
using System.Collections.Generic;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Faction labels + HellGate faction emblems for F11 authoring UI.</summary>
internal static class SpawnAuthoringFactionUi
{
    private static readonly Dictionary<string, string> LocKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = "faction.none",
            ["bandits"] = "faction.bandits",
            ["bandits_inquisition"] = "faction.banditsInq",
            ["bandits_mafia"] = "faction.banditsMafia",
            ["bandits_demons"] = "faction.banditsDemons",
            ["church"] = "faction.church",
            ["demons"] = "faction.demons",
            ["mafia"] = "faction.mafia",
            ["undead"] = "faction.undead",
            ["monsters"] = "faction.monsters",
            ["witch"] = "faction.witch",
            ["eventcore_encounter"] = "faction.event",
        };

    internal static string DisplayName(string token)
    {
        string key = token ?? string.Empty;
        if (LocKeys.TryGetValue(key, out string locKey))
            return SpawnAuthoringLoc.T(locKey);
        return string.IsNullOrEmpty(key) ? SpawnAuthoringLoc.T("faction.none") : key;
    }

    internal static Color ResolveColor(string token)
    {
        if (string.IsNullOrEmpty(token))
            return new Color(0.55f, 0.55f, 0.55f, 1f);

        if (FactionIds.TryParse(token, out int id) &&
            EnemyFactionRuntime.TryGetFactionTintColor(id, out Color tint))
            return tint;

        return Color.white;
    }

    /// <summary>Faction emblem (PNG) when available; otherwise tinted letter badge.</summary>
    internal static void DrawBadge(Rect rect, string token)
    {
        if (TryDrawIcon(rect, token))
            return;

        DrawLetterBadge(rect, token);
    }

    /// <summary>Legacy name kept for call sites — now draws emblem/badge.</summary>
    internal static void DrawSwatch(Rect rect, string token)
    {
        DrawBadge(rect, token);
    }

    private static bool TryDrawIcon(Rect rect, string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;
        if (!FactionIds.TryParse(token, out int id))
            return false;
        if (!FactionStyle.TryGetIconStyle(id, out FactionStyle.IconStyle style) ||
            style == null || style.Icon == null || style.Icon.texture == null)
            return false;

        Color prev = GUI.color;
        GUI.color = Color.white;
        // Dark plate behind emblem
        GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(Inset(rect, 2f), style.Icon.texture, ScaleMode.ScaleToFit);
        DrawBorder(rect, new Color(0f, 0f, 0f, 0.7f));
        GUI.color = prev;
        return true;
    }

    private static void DrawLetterBadge(Rect rect, string token)
    {
        Color fill = ResolveColor(token);
        Color prev = GUI.color;

        GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = new Color(fill.r, fill.g, fill.b, 0.92f);
        GUI.DrawTexture(Inset(rect, 3f), Texture2D.whiteTexture);

        DrawBorder(rect, new Color(0f, 0f, 0f, 0.75f));

        string letter = LetterFor(token);
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.55f), 11, 18)
        };
        style.normal.textColor = ContrastText(fill);
        GUI.color = Color.white;
        GUI.Label(rect, letter, style);
        GUI.color = prev;
    }

    private static string LetterFor(string token)
    {
        if (string.IsNullOrEmpty(token))
            return "–";
        if (token.StartsWith("eventcore", StringComparison.OrdinalIgnoreCase))
            return "E";
        if (token.StartsWith("bandits_inquisition", StringComparison.OrdinalIgnoreCase))
            return "Bi";
        if (token.StartsWith("bandits_mafia", StringComparison.OrdinalIgnoreCase))
            return "Bm";
        if (token.StartsWith("bandits_demons", StringComparison.OrdinalIgnoreCase))
            return "Bd";
        if (token.StartsWith("bandits", StringComparison.OrdinalIgnoreCase))
            return "B";
        return char.ToUpperInvariant(token[0]).ToString();
    }

    private static Color ContrastText(Color fill)
    {
        float lum = 0.2126f * fill.r + 0.7152f * fill.g + 0.0722f * fill.b;
        return lum > 0.55f ? new Color(0.08f, 0.08f, 0.1f, 1f) : Color.white;
    }

    private static Rect Inset(Rect r, float pad)
    {
        return new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f);
    }

    private static void DrawBorder(Rect rect, Color border)
    {
        Color prev = GUI.color;
        GUI.color = border;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }
}

using System;
using UnityEngine;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Single font provider for HellGate's legacy UnityEngine.UI.Text surfaces.
/// Vanilla TextMeshPro content is intentionally unaffected.
/// </summary>
internal static class HellGateFontProvider
{
    private static Font _cachedWestern;
    private static Font _cachedAsian;
    private static bool _resolvedWestern;
    private static bool _resolvedAsian;

    internal static Font GetUiFont()
    {
        return IsAsianLanguage() ? GetAsianFont() : GetWesternFont();
    }

    private static Font GetWesternFont()
    {
        if (_resolvedWestern && _cachedWestern != null)
            return _cachedWestern;

        _resolvedWestern = true;
        _cachedWestern = ResolveConfiguredFont(
            Plugin.fontFamilyWestern != null ? Plugin.fontFamilyWestern.Value : null,
            new[] { "Georgia", "Constantia", "Segoe UI", "Arial", "Calibri", "Tahoma" });

        return _cachedWestern;
    }

    private static Font GetAsianFont()
    {
        if (_resolvedAsian && _cachedAsian != null)
            return _cachedAsian;

        _resolvedAsian = true;
        _cachedAsian = ResolveConfiguredFont(
            Plugin.fontFamilyAsian != null ? Plugin.fontFamilyAsian.Value : null,
            GetAsianFallbackFamilies());

        return _cachedAsian;
    }

    private static Font ResolveConfiguredFont(string familyName, string[] fallbackFamilies)
    {
        try
        {
            if (!string.IsNullOrEmpty(familyName))
            {
                bool visible = IsFamilyVisibleToUnity(familyName);
                Log($"[Fonts] Family '{familyName}' in Unity OS font list: {visible}.");

                Font configured = Font.CreateDynamicFontFromOSFont(familyName, 48);
                if (configured != null)
                {
                    Log($"[Fonts] Using OS dynamic family '{familyName}' -> font.name='{configured.name}'");
                    return configured;
                }
                Log($"[Fonts] CreateDynamicFontFromOSFont returned null for family '{familyName}'. Falling back.");
            }
            else
            {
                Log("[Fonts] No FontFamily configured; using fallback families.");
            }
        }
        catch (Exception ex)
        {
            Log($"[Fonts] Failed to load configured font: {ex.Message}");
        }

        try
        {
            Font fallback = Font.CreateDynamicFontFromOSFont(fallbackFamilies, 48);
            if (fallback != null)
            {
                Log($"[Fonts] Using OS fallback family -> font.name='{fallback.name}'");
                return fallback;
            }
        }
        catch
        {
            // Dynamic OS font creation can fail on some systems; use the built-in fallback below.
        }

        Log("[Fonts] Using built-in Arial.ttf (last-resort fallback).");
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static bool IsFamilyVisibleToUnity(string familyName)
    {
        try
        {
            string[] names = Font.GetOSInstalledFontNames();
            if (names == null)
                return false;

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], familyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Log($"[Fonts] GetOSInstalledFontNames failed: {ex.Message}");
        }

        return false;
    }

    private static void Log(string message)
    {
        try { Plugin.Log?.LogInfo(message); }
        catch { }
    }

    private static bool IsAsianLanguage()
    {
        string lang = Plugin.hellGateLanguage != null ? Plugin.hellGateLanguage.Value : null;
        if (string.IsNullOrEmpty(lang))
            return false;

        return string.Equals(lang, "Cn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(lang, "Jp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(lang, "Kr", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// No single default Windows font covers Simplified Chinese, Japanese and Korean equally well,
    /// so we pick the best preinstalled family for the active language and chain the others as
    /// secondary fallbacks. Used when FontFamilyAsian is left empty in config.
    /// </summary>
    private static string[] GetAsianFallbackFamilies()
    {
        string lang = Plugin.hellGateLanguage != null ? Plugin.hellGateLanguage.Value : null;

        if (string.Equals(lang, "Jp", StringComparison.OrdinalIgnoreCase))
            return new[] { "Yu Gothic UI", "Yu Gothic", "MS Gothic", "Microsoft YaHei", "Segoe UI", "Arial" };

        if (string.Equals(lang, "Kr", StringComparison.OrdinalIgnoreCase))
            return new[] { "Malgun Gothic", "Microsoft YaHei", "Segoe UI", "Arial" };

        // Cn and any other Asian default.
        return new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimSun", "Yu Gothic UI", "Segoe UI", "Arial" };
    }
}

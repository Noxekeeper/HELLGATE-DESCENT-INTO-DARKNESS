using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Unified color parser for all dialogue systems
/// </summary>
internal static class ColorParser
{
    /// <summary>
    /// Parse color from JSON string format "r,g,b,a"
    /// </summary>
    public static Color ParseColorString(string colorString, Color defaultColor = default)
    {
        if (string.IsNullOrEmpty(colorString)) return defaultColor;

        try
        {
            string[] parts = colorString.Split(',');
            if (parts.Length >= 3)
            {
                float r = float.Parse(parts[0]);
                float g = float.Parse(parts[1]);
                float b = float.Parse(parts[2]);
                float a = parts.Length > 3 ? float.Parse(parts[3]) : 1f;
                return new Color(r, g, b, a);
            }
        }
        catch { }

        return defaultColor;
    }

    /// <summary>
    /// Parse color from JSON settings section
    /// </summary>
    public static Color ParseColorFromJson(string settingsSection, string colorKey, Color defaultColor = default)
    {
        try
        {
            // Find color block
            Match colorMatch = Regex.Match(settingsSection, $"\"{colorKey}\"\\s*:\\s*\\{{([^}}]+)\\}}");
            if (colorMatch.Success)
            {
                string colorBlock = colorMatch.Groups[1].Value;

                // Parse components
                float r = ParseFloatComponent(colorBlock, "r", defaultColor.r);
                float g = ParseFloatComponent(colorBlock, "g", defaultColor.g);
                float b = ParseFloatComponent(colorBlock, "b", defaultColor.b);
                float a = ParseFloatComponent(colorBlock, "a", defaultColor.a);

                return new Color(r, g, b, a);
            }
        }
        catch { }

        return defaultColor;
    }

    /// <summary>
    /// Parse individual color component from block
    /// </summary>
    private static float ParseFloatComponent(string colorBlock, string component, float defaultValue)
    {
        Match match = Regex.Match(colorBlock, $"\"{component}\"\\s*:\\s*([0-9.]+)");
        return match.Success ? float.Parse(match.Groups[1].Value) : defaultValue;
    }
}
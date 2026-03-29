using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Base class for dialogue systems with common loading and parsing methods
/// </summary>
internal abstract class DialogueSystemBase
{
    protected static DialogueDisplay _display;

    /// <summary>
    /// Set display system
    /// </summary>
    internal static void SetDisplay(DialogueDisplay display)
    {
        _display = display;
    }

    /// <summary>
    /// Load JSON file and return its content
    /// </summary>
    protected static string LoadJsonFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }
        return "{}";
    }

    /// <summary>
    /// Parse settings section from JSON
    /// </summary>
    protected static Dictionary<string, object> ParseSettingsSection(string jsonContent, string sectionName)
    {
        var settings = new Dictionary<string, object>();

        try
        {
            // Find settings section
            Match settingsMatch = Regex.Match(jsonContent, $"\"{sectionName}\"\\s*:\\s*\\{{([^}}]+)\\}}");
            if (!settingsMatch.Success)
            {
                return settings;
            }

            string settingsSection = settingsMatch.Groups[1].Value;

            // Parse simple values (numbers, strings)
            ParseSimpleValues(settingsSection, settings);

            // Parse arrays
            ParseArrays(settingsSection, settings);

            // Parse objects (colors, complex structures)
            ParseObjects(settingsSection, settings);
        }
        catch (Exception ex)
        {
            // Settings parsing failed silently
        }

        return settings;
    }

    /// <summary>
    /// Parse simple values (numbers, strings)
    /// </summary>
    private static void ParseSimpleValues(string settingsSection, Dictionary<string, object> settings)
    {
        MatchCollection simpleMatches = Regex.Matches(settingsSection, "\"([^\"]+)\"\\s*:\\s*([^,\\{\\[]+)");
        foreach (Match match in simpleMatches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();

            if (float.TryParse(value, out float floatValue))
            {
                settings[key] = floatValue;
            }
            else if (int.TryParse(value, out int intValue))
            {
                settings[key] = intValue;
            }
            else if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                settings[key] = value.Trim('"');
            }
            else if (value == "true" || value == "false")
            {
                settings[key] = bool.Parse(value);
            }
        }
    }

    /// <summary>
    /// Parse arrays
    /// </summary>
    private static void ParseArrays(string settingsSection, Dictionary<string, object> settings)
    {
        MatchCollection arrayMatches = Regex.Matches(settingsSection, "\"([^\"]+)\"\\s*:\\s*\\[([^\\]]+)\\]");
        foreach (Match match in arrayMatches)
        {
            string key = match.Groups[1].Value;
            string arrayContent = match.Groups[2].Value;

            var array = new List<object>();
            string[] elements = arrayContent.Split(',');
            foreach (string element in elements)
            {
                string trimmedElement = element.Trim().Trim('"');
                if (float.TryParse(trimmedElement, out float floatValue))
                {
                    array.Add(floatValue);
                }
                else
                {
                    array.Add(trimmedElement);
                }
            }
            settings[key] = array;
        }
    }

    /// <summary>
    /// Parse objects (colors, complex structures)
    /// </summary>
    private static void ParseObjects(string settingsSection, Dictionary<string, object> settings)
    {
        MatchCollection objectMatches = Regex.Matches(settingsSection, "\"([^\"]+)\"\\s*:\\s*\\{([^\\}]*)\\}");
        foreach (Match match in objectMatches)
        {
            string key = match.Groups[1].Value;
            string objectContent = match.Groups[2].Value;

            var obj = new Dictionary<string, object>();

            // Parse object properties
            MatchCollection propertyMatches = Regex.Matches(objectContent, "\"([^\"]+)\"\\s*:\\s*([^,\\}]+)");
            foreach (Match propMatch in propertyMatches)
            {
                string propKey = propMatch.Groups[1].Value;
                string propValue = propMatch.Groups[2].Value.Trim();

                if (float.TryParse(propValue, out float floatValue))
                {
                    obj[propKey] = floatValue;
                }
                else if (propValue.StartsWith("\"") && propValue.EndsWith("\""))
                {
                    obj[propKey] = propValue.Trim('"');
                }
            }

            settings[key] = obj;
        }
    }

    /// <summary>
    /// Convert object to string for logging
    /// </summary>
    protected static string ObjectToString(object obj)
    {
        if (obj == null) return "null";

        switch (obj)
        {
            case string str: return $"\"{str}\"";
            case Dictionary<string, object> dict:
                return "{" + string.Join(", ", dict.Select(kv => $"\"{kv.Key}\": {ObjectToString(kv.Value)}").ToArray()) + "}";
            case List<object> list:
                return "[" + string.Join(", ", list.Select(ObjectToString).ToArray()) + "]";
            default: return obj.ToString();
        }
    }
}
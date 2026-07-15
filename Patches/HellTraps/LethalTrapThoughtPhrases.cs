using System;
using System.Collections.Generic;
using System.IO;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Loads <c>lethalTrapThoughts</c> from <c>EventCore/&lt;Lang&gt;/event_trap_gate/phrases.json</c> only (no cross-language fallback).
/// </summary>
internal static class LethalTrapThoughtPhrases
{
    private const string PhrasesFolder = "event_trap_gate";
    private const string PhrasesFileName = "phrases.json";

    private static string[] _cachedLines = new string[0];
    private static string _cachedLangKey = string.Empty;

    [Serializable]
    private sealed class PhrasesFile
    {
        // "lines" is used by EventTrap; only lethalTrapThoughts is read here.
        public string[] lines = new string[0];
        public string[] lethalTrapThoughts = new string[0];
    }

    internal static bool TryGetRandomLine(out string line)
    {
        line = string.Empty;
        EnsureLoaded();

        if (_cachedLines == null || _cachedLines.Length == 0)
            return false;

        line = _cachedLines[UnityEngine.Random.Range(0, _cachedLines.Length)];
        return !string.IsNullOrEmpty(line);
    }

    private static void EnsureLoaded()
    {
        string activeLang = EventCoreLanguage.ResolveFolderCode();
        if (_cachedLines != null &&
            _cachedLines.Length > 0 &&
            string.Equals(_cachedLangKey, activeLang, StringComparison.OrdinalIgnoreCase))
            return;

        string[] loaded = LoadThoughtLines(activeLang);
        if (loaded.Length > 0)
        {
            _cachedLangKey = activeLang;
            _cachedLines = loaded;
        }
        else
        {
            _cachedLangKey = string.Empty;
            _cachedLines = new string[0];
        }
    }

    private static string[] LoadThoughtLines(string activeLang)
    {
        EventCorePaths.Initialize();
        string root = EventCorePaths.JsonRoot;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return new string[0];

        if (TryLoadFromLang(root, activeLang, out string[] lines, out string pathUsed) && lines.Length > 0)
        {
            Plugin.Log?.LogInfo(
                "[LethalTrapThought] Loaded "
                + lines.Length
                + " lines from "
                + pathUsed);
            return lines;
        }

        Plugin.Log?.LogWarning(
            "[LethalTrapThought] No lethalTrapThoughts for lang '"
            + activeLang
            + "' in EventCore/"
            + activeLang
            + "/event_trap_gate/phrases.json");
        return new string[0];
    }

    private static bool TryLoadFromLang(
        string jsonRoot,
        string lang,
        out string[] lines,
        out string pathUsed)
    {
        lines = new string[0];
        pathUsed = Path.Combine(
            Path.Combine(Path.Combine(jsonRoot, lang), PhrasesFolder),
            PhrasesFileName);

        if (!File.Exists(pathUsed))
            return false;

        try
        {
            PhrasesFile file = JsonUtility.FromJson<PhrasesFile>(File.ReadAllText(pathUsed));
            if (file?.lethalTrapThoughts == null || file.lethalTrapThoughts.Length == 0)
                return false;

            var cleaned = new List<string>();
            for (int i = 0; i < file.lethalTrapThoughts.Length; i++)
            {
                string s = file.lethalTrapThoughts[i];
                if (string.IsNullOrEmpty(s))
                    continue;

                s = s.Trim();
                if (s.Length > 0)
                    cleaned.Add(s);
            }

            lines = cleaned.ToArray();
            return lines.Length > 0;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "[LethalTrapThought] Failed to read "
                + pathUsed
                + ": "
                + ex.Message);
            return false;
        }
    }
}

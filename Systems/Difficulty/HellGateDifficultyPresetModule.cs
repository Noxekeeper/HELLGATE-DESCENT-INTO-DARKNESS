using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Difficulty;

/// <summary>
/// Swaps BepInEx/config NoREroMod*.cfg from EASY / MEDIUM / HARD preset folders.
/// Selection is stored in a sidecar file so applying a preset does not fight Config.Save().
/// Player prefs on the HellGate cfg (language, gore, fonts, splash) are restored into [General]
/// after copy (and orphan duplicate key lines are stripped). Splash Simple QTE
/// (`[QTEFreeStruggle] Enable`) and remappable G/T/V hotkeys are restored into their sections.
/// Active balance values still require a full game restart for systems that
/// cached numbers at Awake; in-memory ConfigEntry values are reloaded from disk
/// immediately so a later Config.Save() cannot wipe the preset with stale data.
/// </summary>
internal static class HellGateDifficultyPresetModule
{
    internal enum DifficultyLevel
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
    }

    private const string SelectionFileName = "HellGateDifficulty.selection";
    private const string NorCfgName = "NoREroMod.cfg";
    private const string HellGateCfgName = "NoREroMod_HellGate.cfg";

    /// <summary>
    /// True after a successful preset copy this session — OnDestroy re-applies
    /// from the selection folder so a late Config.Save cannot restore stale RAM.
    /// </summary>
    private static bool _reassertPresetOnExit;

    /// <summary>
    /// Keys that are player/install prefs, not balance — must survive preset copy.
    /// </summary>
    private static readonly string[] PreservedHellGateKeys =
    {
        "HellGateLanguage",
        "EnableGoreContent",
        "ShowSplashScreenOnStartup",
        "FontFamilyWestern",
        "FontFamilyAsian",
        "FontFileWestern",
        "FontFileAsian",
    };

    private static readonly Color EasyColor = new Color(0.25f, 0.88f, 0.35f, 1f);
    private static readonly Color MediumColor = new Color(1f, 0.86f, 0.18f, 1f);
    private static readonly Color HardColor = new Color(1f, 0.22f, 0.22f, 1f);

    private static string ConfigRoot => Path.Combine(Paths.BepInExRootPath, "config");

    private static string SelectionFilePath => Path.Combine(ConfigRoot, SelectionFileName);

    internal static string FolderName(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return "EASY";
            case DifficultyLevel.Hard: return "HARD";
            default: return "MEDIUM";
        }
    }

    internal static string DisplayName(DifficultyLevel level) => FolderName(level);

    internal static Color ColorFor(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return EasyColor;
            case DifficultyLevel.Hard: return HardColor;
            default: return MediumColor;
        }
    }

    internal static DifficultyLevel GetSelected()
    {
        try
        {
            string path = SelectionFilePath;
            if (!File.Exists(path))
                return DifficultyLevel.Easy;

            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Failed to read selection: " + ex.Message);
            return DifficultyLevel.Easy;
        }
    }

    internal static DifficultyLevel Parse(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return DifficultyLevel.Easy;

        string s = raw.Trim();
        if (s.Equals("EASY", StringComparison.OrdinalIgnoreCase) || s == "0")
            return DifficultyLevel.Easy;
        if (s.Equals("HARD", StringComparison.OrdinalIgnoreCase) || s == "2")
            return DifficultyLevel.Hard;
        // Unknown / legacy empty → Easy (MEDIUM/HARD only when explicitly selected).
        if (s.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase) || s == "1")
            return DifficultyLevel.Medium;
        return DifficultyLevel.Easy;
    }

    /// <summary>
    /// First install (no selection sidecar): copy EASY preset cfgs into the active
    /// config folder before SetUpConfigs binds, so Bind defaults are not left as the
    /// live balance. Preserves language / gore / fonts / splash / Simple QTE.
    /// No-op when HellGateDifficulty.selection already exists.
    /// </summary>
    internal static void EnsureDefaultEasyPresetBeforeBind()
    {
        try
        {
            if (File.Exists(SelectionFilePath))
                return;

            string folder = Path.Combine(ConfigRoot, FolderName(DifficultyLevel.Easy));
            string srcNor = Path.Combine(folder, NorCfgName);
            string srcHg = Path.Combine(folder, HellGateCfgName);
            if (!File.Exists(srcNor) || !File.Exists(srcHg))
            {
                SaveSelected(DifficultyLevel.Easy);
                Plugin.Log?.LogWarning("[Difficulty] EASY preset files missing — selection defaulted to EASY only.");
                return;
            }

            if (!TryApplyPreset(DifficultyLevel.Easy, out string error))
                Plugin.Log?.LogWarning("[Difficulty] Default EASY apply failed: " + error);
            else
                Plugin.Log?.LogInfo("[Difficulty] First run — applied EASY preset before config bind.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] EnsureDefaultEasyPresetBeforeBind failed: " + ex.Message);
            try { SaveSelected(DifficultyLevel.Easy); } catch { }
        }
    }

    private static void SaveSelected(DifficultyLevel level)
    {
        try
        {
            Directory.CreateDirectory(ConfigRoot);
            File.WriteAllText(SelectionFilePath, FolderName(level));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Failed to save selection: " + ex.Message);
        }
    }

    /// <summary>
    /// Copies both preset cfg files into the active config folder, then restores
    /// preserved HellGate preference keys into [General] and [QTEFreeStruggle] Enable.
    /// Copies preset files, restores prefs, reloads HellGate ConfigEntry values from
    /// disk (so Config.Save cannot wipe with pre-apply RAM), and marks exit re-assert.
    /// Gameplay systems that cached Awake values still need a full restart.
    /// </summary>
    internal static bool TryApplyPreset(DifficultyLevel level, out string error)
    {
        error = null;
        string folder = Path.Combine(ConfigRoot, FolderName(level));
        string srcNor = Path.Combine(folder, NorCfgName);
        string srcHg = Path.Combine(folder, HellGateCfgName);
        string dstNor = Path.Combine(ConfigRoot, NorCfgName);
        string dstHg = Path.Combine(ConfigRoot, HellGateCfgName);

        if (!Directory.Exists(folder))
        {
            error = "Preset folder missing: " + FolderName(level);
            return false;
        }

        if (!File.Exists(srcNor) || !File.Exists(srcHg))
        {
            error = "Preset files missing in " + FolderName(level);
            return false;
        }

        try
        {
            Dictionary<string, string> preserve = CollectPreservedHellGateValues(dstHg);
            bool? freeStruggle = null;
            if (Plugin.qteFreeStruggleEnable != null)
                freeStruggle = Plugin.qteFreeStruggleEnable.Value;
            else if (TryReadQteFreeStruggleEnable(out bool fromFile))
                freeStruggle = fromFile;
            Dictionary<string, KeyCode> hotkeys = CollectPreservedHotkeys(dstHg);

            File.Copy(srcNor, dstNor, overwrite: true);
            File.Copy(srcHg, dstHg, overwrite: true);
            WritePreservedKeysIntoGeneral(dstHg, preserve);
            if (freeStruggle.HasValue)
                WriteQteFreeStruggleEnable(dstHg, freeStruggle.Value);
            WritePreservedHotkeys(dstHg, hotkeys);

            SaveSelected(level);
            ReloadHellGateConfigFromDisk();
            _reassertPresetOnExit = true;
            Plugin.Log?.LogInfo("[Difficulty] Applied preset " + FolderName(level) +
                " → " + NorCfgName + " + " + HellGateCfgName +
                " (prefs preserved; Config reloaded; restart required for cached gameplay).");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Plugin.Log?.LogError("[Difficulty] Apply failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Pull HellGate ConfigEntry values from the active cfg on disk.
    /// Prevents BepInEx Config.Save from writing stale pre-preset RAM over the copy.
    /// </summary>
    private static void ReloadHellGateConfigFromDisk()
    {
        try
        {
            if (Plugin.Instance?.Config == null)
                return;
            Plugin.Instance.Config.Reload();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Config.Reload after preset failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Call from Plugin OnDestroy — if a preset was applied this session, write it
    /// again after any late Save so the next launch still matches the selection.
    /// </summary>
    internal static void ReassertSelectedPresetOnExitIfNeeded()
    {
        if (!_reassertPresetOnExit)
            return;

        try
        {
            DifficultyLevel level = GetSelected();
            // Silent disk restore (no second Reload / flag churn).
            string folder = Path.Combine(ConfigRoot, FolderName(level));
            string srcNor = Path.Combine(folder, NorCfgName);
            string srcHg = Path.Combine(folder, HellGateCfgName);
            string dstNor = Path.Combine(ConfigRoot, NorCfgName);
            string dstHg = Path.Combine(ConfigRoot, HellGateCfgName);
            if (!File.Exists(srcNor) || !File.Exists(srcHg))
                return;

            Dictionary<string, string> preserve = CollectPreservedHellGateValues(dstHg);
            bool? freeStruggle = null;
            if (Plugin.qteFreeStruggleEnable != null)
                freeStruggle = Plugin.qteFreeStruggleEnable.Value;
            else if (TryReadQteFreeStruggleEnable(out bool fromFile))
                freeStruggle = fromFile;
            Dictionary<string, KeyCode> hotkeys = CollectPreservedHotkeys(dstHg);

            File.Copy(srcNor, dstNor, overwrite: true);
            File.Copy(srcHg, dstHg, overwrite: true);
            WritePreservedKeysIntoGeneral(dstHg, preserve);
            if (freeStruggle.HasValue)
                WriteQteFreeStruggleEnable(dstHg, freeStruggle.Value);
            WritePreservedHotkeys(dstHg, hotkeys);

            Plugin.Log?.LogInfo("[Difficulty] Re-asserted " + FolderName(level) + " on exit.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Exit re-assert failed: " + ex.Message);
        }
        finally
        {
            _reassertPresetOnExit = false;
        }
    }

    internal static bool TryReadEnableGoreContent(out bool enabled)
    {
        enabled = true;
        try
        {
            string path = Path.Combine(ConfigRoot, HellGateCfgName);
            if (!File.Exists(path))
                return false;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ReadCfgKeysPreferGeneral(File.ReadAllText(path), new[] { "EnableGoreContent" }, values);
            if (!values.TryGetValue("EnableGoreContent", out string raw) || string.IsNullOrEmpty(raw))
                return false;

            raw = raw.Trim();
            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            {
                enabled = true;
                return true;
            }

            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0")
            {
                enabled = false;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Could not read EnableGoreContent: " + ex.Message);
            return false;
        }
    }

    internal static bool TryReadQteFreeStruggleEnable(out bool enabled)
    {
        enabled = false;
        try
        {
            string path = Path.Combine(ConfigRoot, HellGateCfgName);
            if (!File.Exists(path))
                return false;

            string body = ExtractSectionBody(File.ReadAllText(path), "QTEFreeStruggle");
            if (string.IsNullOrEmpty(body))
                return false;

            Match m = Regex.Match(body, @"(?m)^Enable\s*=\s*(.*)$");
            if (!m.Success)
                return false;

            string raw = m.Groups[1].Value.Trim();
            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            {
                enabled = true;
                return true;
            }

            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0")
            {
                enabled = false;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Could not read QTEFreeStruggle.Enable: " + ex.Message);
            return false;
        }
    }

    private static Dictionary<string, string> CollectPreservedHellGateValues(string existingHgPath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(existingHgPath))
        {
            try
            {
                ReadCfgKeysPreferGeneral(File.ReadAllText(existingHgPath), PreservedHellGateKeys, values);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[Difficulty] Could not read prefs from active cfg: " + ex.Message);
            }
        }

        if (Plugin.hellGateLanguage != null)
        {
            string lang = (Plugin.hellGateLanguage.Value ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(lang))
                values["HellGateLanguage"] = lang;
        }

        if (Plugin.enableGoreContent != null)
            values["EnableGoreContent"] = Plugin.enableGoreContent.Value ? "true" : "false";

        if (Plugin.showSplashScreenOnStartup != null)
            values["ShowSplashScreenOnStartup"] = Plugin.showSplashScreenOnStartup.Value ? "true" : "false";

        if (Plugin.fontFamilyWestern != null)
            values["FontFamilyWestern"] = Plugin.fontFamilyWestern.Value ?? string.Empty;

        if (Plugin.fontFamilyAsian != null)
            values["FontFamilyAsian"] = Plugin.fontFamilyAsian.Value ?? string.Empty;

        return values;
    }

    private static void ReadCfgKeysPreferGeneral(string text, string[] keys, Dictionary<string, string> into)
    {
        string general = ExtractSectionBody(text, "General");
        string source = !string.IsNullOrEmpty(general) ? general : text;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            Match m = Regex.Match(
                source,
                @"(?m)^" + Regex.Escape(key) + @"\s*=\s*(.*)$");
            if (m.Success)
                into[key] = m.Groups[1].Value.TrimEnd();
        }
    }

    /// <summary>
    /// Upserts preserved keys inside [General] and removes orphan duplicates elsewhere
    /// (those orphans made BepInEx read an empty/stale language and re-show the picker).
    /// </summary>
    private static void WritePreservedKeysIntoGeneral(string path, Dictionary<string, string> keys)
    {
        if (keys == null || keys.Count == 0 || !File.Exists(path))
            return;

        string text = File.ReadAllText(path);
        text = StripOrphanPreservedAssignments(text, keys.Keys);

        int sectionHeader = IndexOfSectionHeader(text, "General");
        if (sectionHeader < 0)
        {
            var sb = new StringBuilder(text);
            if (!text.EndsWith("\n") && text.Length > 0)
                sb.Append('\n');
            sb.Append("\n[General]\n\n");
            foreach (KeyValuePair<string, string> kv in keys)
                sb.Append(kv.Key).Append(" = ").Append(kv.Value ?? string.Empty).Append('\n');
            File.WriteAllText(path, sb.ToString());
            return;
        }

        int bodyStart = text.IndexOf('\n', sectionHeader);
        if (bodyStart < 0)
            bodyStart = text.Length;
        else
            bodyStart++;

        int bodyEnd = IndexOfNextSectionHeader(text, bodyStart);
        if (bodyEnd < 0)
            bodyEnd = text.Length;

        string before = text.Substring(0, bodyStart);
        string body = text.Substring(bodyStart, bodyEnd - bodyStart);
        string after = text.Substring(bodyEnd);

        foreach (KeyValuePair<string, string> kv in keys)
            body = UpsertKeyLine(body, kv.Key, kv.Value ?? string.Empty);

        File.WriteAllText(path, before + body + after);
    }

    /// <summary>
    /// Upserts Enable under [QTEFreeStruggle] so difficulty presets do not wipe
    /// the splash Simple QTE preference.
    /// </summary>
    private static void WriteQteFreeStruggleEnable(string path, bool enabled)
    {
        if (!File.Exists(path))
            return;

        string value = enabled ? "true" : "false";
        string text = File.ReadAllText(path);
        int sectionHeader = IndexOfSectionHeader(text, "QTEFreeStruggle");
        if (sectionHeader < 0)
        {
            var sb = new StringBuilder(text);
            if (!text.EndsWith("\n") && text.Length > 0)
                sb.Append('\n');
            sb.Append("\n[QTEFreeStruggle]\n\n");
            sb.Append("Enable = ").Append(value).Append('\n');
            File.WriteAllText(path, sb.ToString());
            return;
        }

        int bodyStart = text.IndexOf('\n', sectionHeader);
        if (bodyStart < 0)
            bodyStart = text.Length;
        else
            bodyStart++;

        int bodyEnd = IndexOfNextSectionHeader(text, bodyStart);
        if (bodyEnd < 0)
            bodyEnd = text.Length;

        string before = text.Substring(0, bodyStart);
        string body = text.Substring(bodyStart, bodyEnd - bodyStart);
        string after = text.Substring(bodyEnd);
        body = UpsertKeyLine(body, "Enable", value);
        File.WriteAllText(path, before + body + after);
    }

    /// <summary>
    /// Collect remappable combat hotkeys from live ConfigEntry values, falling back
    /// to the active cfg on disk (before a preset overwrite).
    /// Keys: RageMode.ActivationHotkey, RageMode.TimeSlowMoHotkey, CombatCamera.ToggleHotkey.
    /// </summary>
    private static Dictionary<string, KeyCode> CollectPreservedHotkeys(string existingHgPath)
    {
        var values = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase);

        if (Plugin.rageActivationHotkey != null)
            values["RageMode.ActivationHotkey"] = Plugin.rageActivationHotkey.Value;
        if (Plugin.timeSlowMoHotkey != null)
            values["RageMode.TimeSlowMoHotkey"] = Plugin.timeSlowMoHotkey.Value;
        if (Plugin.combatCameraHotkey != null)
            values["CombatCamera.ToggleHotkey"] = Plugin.combatCameraHotkey.Value;

        if (!File.Exists(existingHgPath))
            return values;

        try
        {
            string text = File.ReadAllText(existingHgPath);
            TryAddHotkeyFromSection(values, text, "RageMode", "ActivationHotkey", "RageMode.ActivationHotkey");
            TryAddHotkeyFromSection(values, text, "RageMode", "TimeSlowMoHotkey", "RageMode.TimeSlowMoHotkey");
            TryAddHotkeyFromSection(values, text, "CombatCamera", "ToggleHotkey", "CombatCamera.ToggleHotkey");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Difficulty] Could not read preserved hotkeys: " + ex.Message);
        }

        return values;
    }

    private static void TryAddHotkeyFromSection(
        Dictionary<string, KeyCode> values,
        string text,
        string section,
        string key,
        string mapKey)
    {
        if (values.ContainsKey(mapKey))
            return;

        string body = ExtractSectionBody(text, section);
        if (string.IsNullOrEmpty(body))
            return;

        Match m = Regex.Match(body, @"(?m)^" + Regex.Escape(key) + @"\s*=\s*(.*)$");
        if (!m.Success)
            return;

        string raw = m.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(raw))
            return;

        try
        {
            object boxed = Enum.Parse(typeof(KeyCode), raw, true);
            KeyCode parsed = (KeyCode)boxed;
            if (parsed != KeyCode.None && Enum.IsDefined(typeof(KeyCode), parsed))
                values[mapKey] = parsed;
        }
        catch (ArgumentException)
        {
            // Invalid KeyCode name in cfg — keep existing / skip.
        }
    }

    /// <summary>
    /// Restores remappable G/T/V hotkeys after a difficulty preset copy.
    /// </summary>
    private static void WritePreservedHotkeys(string path, Dictionary<string, KeyCode> hotkeys)
    {
        if (!File.Exists(path) || hotkeys == null || hotkeys.Count == 0)
            return;

        if (hotkeys.TryGetValue("RageMode.ActivationHotkey", out KeyCode rage))
            WriteSectionKey(path, "RageMode", "ActivationHotkey", rage.ToString());
        if (hotkeys.TryGetValue("RageMode.TimeSlowMoHotkey", out KeyCode slow))
            WriteSectionKey(path, "RageMode", "TimeSlowMoHotkey", slow.ToString());
        if (hotkeys.TryGetValue("CombatCamera.ToggleHotkey", out KeyCode cam))
            WriteSectionKey(path, "CombatCamera", "ToggleHotkey", cam.ToString());
    }

    private static void WriteSectionKey(string path, string section, string key, string value)
    {
        if (!File.Exists(path))
            return;

        string text = File.ReadAllText(path);
        int sectionHeader = IndexOfSectionHeader(text, section);
        if (sectionHeader < 0)
        {
            var sb = new StringBuilder(text);
            if (!text.EndsWith("\n") && text.Length > 0)
                sb.Append('\n');
            sb.Append('\n').Append('[').Append(section).Append("]\n\n");
            sb.Append(key).Append(" = ").Append(value).Append('\n');
            File.WriteAllText(path, sb.ToString());
            return;
        }

        int bodyStart = text.IndexOf('\n', sectionHeader);
        if (bodyStart < 0)
            bodyStart = text.Length;
        else
            bodyStart++;

        int bodyEnd = IndexOfNextSectionHeader(text, bodyStart);
        if (bodyEnd < 0)
            bodyEnd = text.Length;

        string before = text.Substring(0, bodyStart);
        string body = text.Substring(bodyStart, bodyEnd - bodyStart);
        string after = text.Substring(bodyEnd);
        body = UpsertKeyLine(body, key, value);
        File.WriteAllText(path, before + body + after);
    }

    private static string StripOrphanPreservedAssignments(string text, IEnumerable<string> keys)
    {
        var keySet = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = new List<string>(lines.Length);
        string currentSection = "";

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && trimmed.Length >= 3)
            {
                currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                kept.Add(line);
                continue;
            }

            int eq = trimmed.IndexOf('=');
            if (eq > 0)
            {
                string key = trimmed.Substring(0, eq).Trim();
                if (keySet.Contains(key) &&
                    !string.Equals(currentSection, "General", StringComparison.OrdinalIgnoreCase))
                {
                    // Drop orphan / duplicate preference lines outside [General].
                    continue;
                }
            }

            kept.Add(line);
        }

        return string.Join("\n", kept.ToArray());
    }

    private static string UpsertKeyLine(string sectionBody, string key, string value)
    {
        string pattern = @"(?m)^(" + Regex.Escape(key) + @"\s*=\s*).*$";
        if (Regex.IsMatch(sectionBody, pattern))
            return Regex.Replace(sectionBody, pattern, m => m.Groups[1].Value + value);

        string insert = key + " = " + value + "\n";
        if (!sectionBody.EndsWith("\n") && sectionBody.Length > 0)
            sectionBody += "\n";
        return sectionBody + insert;
    }

    private static string ExtractSectionBody(string text, string sectionName)
    {
        int header = IndexOfSectionHeader(text, sectionName);
        if (header < 0)
            return null;

        int bodyStart = text.IndexOf('\n', header);
        if (bodyStart < 0)
            return string.Empty;
        bodyStart++;

        int bodyEnd = IndexOfNextSectionHeader(text, bodyStart);
        if (bodyEnd < 0)
            bodyEnd = text.Length;

        return text.Substring(bodyStart, bodyEnd - bodyStart);
    }

    private static int IndexOfSectionHeader(string text, string sectionName)
    {
        Match m = Regex.Match(
            text,
            @"(?m)^\[\s*" + Regex.Escape(sectionName) + @"\s*\]\s*$");
        return m.Success ? m.Index : -1;
    }

    private static int IndexOfNextSectionHeader(string text, int startIndex)
    {
        if (startIndex < 0 || startIndex >= text.Length)
            return -1;

        Match m = Regex.Match(text.Substring(startIndex), @"(?m)^\[.+\]\s*$");
        return m.Success ? startIndex + m.Index : -1;
    }
}

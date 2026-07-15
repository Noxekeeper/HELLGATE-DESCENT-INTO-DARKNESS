using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

internal static class ShelterAttackPhrases
{
    private const string PhrasesFolder = "Shelter event";
    private const string PhrasesFileName = "phrases.json";

    private static string[] _cachedAlertLines = new string[0];
    private static string[] _cachedVictoryThoughts = new string[0];
    private static string[] _cachedDefeatThoughts = new string[0];
    private static string[] _cachedTimeoutThoughts = new string[0];
    private static string _cachedLangKey = string.Empty;
    private static UiStrings _cachedUi = UiStrings.CreateDefaults();

    [Serializable]
    private sealed class PhrasesFile
    {
        public string[] shelterAttackThoughts = new string[0];
        public string[] victoryThoughts = new string[0];
        public string[] defeatThoughts = new string[0];
        public string[] timeoutThoughts = new string[0];
        public string attackCountdownFormat = "Shelter raid in {0} seconds";
        public string timeoutLabel = "TIME OUT";
        public string timeoutSummary = "You failed to return to the shelter in time. The Witch faction suffered losses: ";
        public string waveBreakFormat = "Next wave in {0} seconds";
        public string wave1Text = "WAVE 1";
        public string wave2Text = "WAVE 2";
        public string wave3Text = "WAVE 3";
        public string finalWaveText = "FINAL";
        public string victorySummary = "Witch faction experience grows; all members have advanced one level.";
        public string defeatSummaryPrefix = "Raid ended in defeat. The Witch faction suffered losses: ";
        public string defeatSummaryOne = "{0} member kidnapped from the shelter.";
        public string defeatSummaryMany = "{0} members kidnapped from the shelter.";
        public string threatLow = "Medium threat";
        public string threatMid = "High threat";
        public string threatHigh = "Very high threat";
    }

    private sealed class UiStrings
    {
        public string AttackCountdownFormat;
        public string WaveBreakFormat;
        public string Wave1Text;
        public string Wave2Text;
        public string Wave3Text;
        public string FinalWaveText;
        public string VictorySummary;
        public string DefeatSummaryPrefix;
        public string DefeatSummaryOne;
        public string DefeatSummaryMany;
        public string TimeoutLabel;
        public string TimeoutSummary;
        public string ThreatLow;
        public string ThreatMid;
        public string ThreatHigh;

        public static UiStrings CreateDefaults()
        {
            return new UiStrings
            {
                AttackCountdownFormat = "Shelter raid in {0} seconds",
                WaveBreakFormat = "Next wave in {0} seconds",
                Wave1Text = "WAVE 1",
                Wave2Text = "WAVE 2",
                Wave3Text = "WAVE 3",
                FinalWaveText = "FINAL",
                VictorySummary = "Witch faction experience grows; all members have advanced one level.",
                DefeatSummaryPrefix = "Raid ended in defeat. The Witch faction suffered losses: ",
                DefeatSummaryOne = "{0} member kidnapped from the shelter.",
                DefeatSummaryMany = "{0} members kidnapped from the shelter.",
                TimeoutLabel = "TIME OUT",
                TimeoutSummary = "You failed to return to the shelter in time. The Witch faction suffered losses: ",
                ThreatLow = "Medium threat",
                ThreatMid = "High threat",
                ThreatHigh = "Very high threat"
            };
        }
    }

    internal static bool TryGetRandomLine(out string line) => TryGetRandomFrom(_cachedAlertLines, out line);

    internal static bool TryGetRandomVictoryThought(out string line) => TryGetRandomFrom(_cachedVictoryThoughts, out line);

    internal static bool TryGetRandomDefeatThought(out string line) => TryGetRandomFrom(_cachedDefeatThoughts, out line);

    internal static bool TryGetRandomTimeoutThought(out string line) => TryGetRandomFrom(_cachedTimeoutThoughts, out line);

    internal static string GetTimeoutLabel()
    {
        EnsureLoaded();
        return string.IsNullOrEmpty(_cachedUi.TimeoutLabel) ? "TIME OUT" : _cachedUi.TimeoutLabel;
    }

    internal static string FormatTimeoutSummary(int lostCount)
    {
        EnsureLoaded();
        lostCount = Mathf.Max(0, lostCount);

        if (IsRussianLanguage())
            return FormatRussianTimeoutSummary(lostCount);

        string prefix = _cachedUi.TimeoutSummary ?? _cachedUi.DefeatSummaryPrefix ?? string.Empty;
        string tail = lostCount == 1
            ? string.Format(_cachedUi.DefeatSummaryOne ?? "{0} member kidnapped.", lostCount)
            : string.Format(_cachedUi.DefeatSummaryMany ?? "{0} members kidnapped.", lostCount);

        return prefix + tail;
    }

    internal static string GetVictorySummary(int growthAdvancedCount)
    {
        EnsureLoaded();
        string summary = _cachedUi.VictorySummary;
        if (growthAdvancedCount > 0 && summary.IndexOf("{0}", StringComparison.Ordinal) >= 0)
            return string.Format(summary, growthAdvancedCount);

        return summary;
    }

    internal static string FormatDefeatSummary(int lostCount)
    {
        EnsureLoaded();
        lostCount = Mathf.Max(0, lostCount);

        if (IsRussianLanguage())
            return FormatRussianDefeatSummary(lostCount);

        string prefix = _cachedUi.DefeatSummaryPrefix ?? string.Empty;
        string tail = lostCount == 1
            ? string.Format(_cachedUi.DefeatSummaryOne ?? "{0} member kidnapped.", lostCount)
            : string.Format(_cachedUi.DefeatSummaryMany ?? "{0} members kidnapped.", lostCount);

        return prefix + tail;
    }

    internal static string FormatAttackCountdown(int seconds)
    {
        EnsureLoaded();
        seconds = Mathf.Max(0, seconds);
        return string.Format(_cachedUi.AttackCountdownFormat, seconds);
    }

    internal static string GetThreatLevelLabel(ShelterAttackWaves.ThreatTier tier)
    {
        EnsureLoaded();
        return tier switch
        {
            ShelterAttackWaves.ThreatTier.Low =>
                string.IsNullOrEmpty(_cachedUi.ThreatLow) ? "Medium threat" : _cachedUi.ThreatLow,
            ShelterAttackWaves.ThreatTier.Mid =>
                string.IsNullOrEmpty(_cachedUi.ThreatMid) ? "High threat" : _cachedUi.ThreatMid,
            _ =>
                string.IsNullOrEmpty(_cachedUi.ThreatHigh) ? "Very high threat" : _cachedUi.ThreatHigh
        };
    }

    internal static string FormatWaveBreakCountdown(int seconds)
    {
        EnsureLoaded();
        return string.Format(_cachedUi.WaveBreakFormat, Mathf.Max(0, seconds));
    }

    internal static string GetWaveAnnouncementText(int waveIndex)
    {
        EnsureLoaded();
        if (waveIndex >= ShelterAttackState.TotalWaves - 1)
            return _cachedUi.FinalWaveText;

        return waveIndex switch
        {
            0 => _cachedUi.Wave1Text,
            1 => _cachedUi.Wave2Text,
            2 => _cachedUi.Wave3Text,
            _ => _cachedUi.FinalWaveText
        };
    }

    private static bool TryGetRandomFrom(string[] lines, out string line)
    {
        line = string.Empty;
        EnsureLoaded();

        if (lines == null || lines.Length == 0)
            return false;

        line = lines[UnityEngine.Random.Range(0, lines.Length)];
        return !string.IsNullOrEmpty(line);
    }

    private static void EnsureLoaded()
    {
        string activeLang = EventCoreLanguage.ResolveFolderCode();
        if (!string.IsNullOrEmpty(_cachedLangKey) &&
            string.Equals(_cachedLangKey, activeLang, StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryLoad(activeLang, out string[] alert, out string[] victory, out string[] defeat, out string[] timeout, out UiStrings ui))
        {
            _cachedLangKey = string.Empty;
            _cachedAlertLines = new string[0];
            _cachedVictoryThoughts = new string[0];
            _cachedDefeatThoughts = new string[0];
            _cachedTimeoutThoughts = new string[0];
            _cachedUi = UiStrings.CreateDefaults();
            return;
        }

        _cachedLangKey = activeLang;
        _cachedAlertLines = alert;
        _cachedVictoryThoughts = victory;
        _cachedDefeatThoughts = defeat;
        _cachedTimeoutThoughts = timeout;
        _cachedUi = ui;
    }

    private static bool TryLoad(
        string activeLang,
        out string[] alertThoughts,
        out string[] victoryThoughts,
        out string[] defeatThoughts,
        out string[] timeoutThoughts,
        out UiStrings ui)
    {
        alertThoughts = new string[0];
        victoryThoughts = new string[0];
        defeatThoughts = new string[0];
        timeoutThoughts = new string[0];
        ui = UiStrings.CreateDefaults();

        try
        {
            string root = Path.Combine(Paths.PluginPath, "HellGateJson");
            if (!Directory.Exists(root))
                return false;

            string path = Path.Combine(
                Path.Combine(Path.Combine(root, "Pregnancy"), PhrasesFolder),
                Path.Combine(activeLang, PhrasesFileName));

            if (!File.Exists(path))
            {
                Plugin.Log?.LogWarning($"[Pregnancy.ShelterAttack] Phrases not found: {path}");
                return false;
            }

            PhrasesFile file = JsonUtility.FromJson<PhrasesFile>(File.ReadAllText(path));
            if (file == null)
                return false;

            alertThoughts = CleanThoughts(file.shelterAttackThoughts);
            victoryThoughts = CleanThoughts(file.victoryThoughts);
            defeatThoughts = CleanThoughts(file.defeatThoughts);
            timeoutThoughts = CleanThoughts(file.timeoutThoughts);
            ui = new UiStrings
            {
                AttackCountdownFormat = Pick(file.attackCountdownFormat, ui.AttackCountdownFormat),
                WaveBreakFormat = Pick(file.waveBreakFormat, ui.WaveBreakFormat),
                Wave1Text = Pick(file.wave1Text, ui.Wave1Text),
                Wave2Text = Pick(file.wave2Text, ui.Wave2Text),
                Wave3Text = Pick(file.wave3Text, ui.Wave3Text),
                FinalWaveText = Pick(file.finalWaveText, ui.FinalWaveText),
                VictorySummary = Pick(file.victorySummary, ui.VictorySummary),
                DefeatSummaryPrefix = Pick(file.defeatSummaryPrefix, ui.DefeatSummaryPrefix),
                DefeatSummaryOne = Pick(file.defeatSummaryOne, ui.DefeatSummaryOne),
                DefeatSummaryMany = Pick(file.defeatSummaryMany, ui.DefeatSummaryMany),
                TimeoutLabel = Pick(file.timeoutLabel, ui.TimeoutLabel),
                TimeoutSummary = Pick(file.timeoutSummary, ui.TimeoutSummary),
                ThreatLow = Pick(file.threatLow, ui.ThreatLow),
                ThreatMid = Pick(file.threatMid, ui.ThreatMid),
                ThreatHigh = Pick(file.threatHigh, ui.ThreatHigh)
            };

            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Loaded phrases from {path} " +
                $"(alert={alertThoughts.Length}, victory={victoryThoughts.Length}, defeat={defeatThoughts.Length}, timeout={timeoutThoughts.Length})");
            return alertThoughts.Length > 0
                || victoryThoughts.Length > 0
                || defeatThoughts.Length > 0
                || timeoutThoughts.Length > 0
                || !string.IsNullOrEmpty(ui.Wave1Text);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy.ShelterAttack] Failed to load phrases: {ex.Message}");
            return false;
        }
    }

    private static string FormatRussianDefeatSummary(int lostCount)
    {
        string prefix = string.IsNullOrEmpty(_cachedUi.DefeatSummaryPrefix)
            ? "Рейд завершён поражением. Фракция Ведьмы несёт потери: из убежища похищено "
            : _cachedUi.DefeatSummaryPrefix;

        return prefix + FormatRussianParticipants(lostCount) + ".";
    }

    private static string FormatRussianTimeoutSummary(int lostCount)
    {
        string prefix = string.IsNullOrEmpty(_cachedUi.TimeoutSummary)
            ? "Время вышло — вы не успели вернуться в убежище. Фракция Ведьмы несёт потери: из убежища похищено "
            : _cachedUi.TimeoutSummary;

        return prefix + FormatRussianParticipants(lostCount) + ".";
    }

    private static string FormatRussianParticipants(int count)
    {
        int n = Mathf.Max(0, count);
        int mod10 = n % 10;
        int mod100 = n % 100;

        if (mod100 >= 11 && mod100 <= 14)
            return n + " участников";

        if (mod10 == 1)
            return n + " участник";

        if (mod10 >= 2 && mod10 <= 4)
            return n + " участника";

        return n + " участников";
    }

    private static bool IsRussianLanguage()
    {
        return string.Equals(EventCoreLanguage.ResolveFolderCode(), "RU", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] CleanThoughts(string[] raw)
    {
        if (raw == null || raw.Length == 0)
            return new string[0];

        var cleaned = new List<string>();
        for (int i = 0; i < raw.Length; i++)
        {
            string s = raw[i];
            if (string.IsNullOrEmpty(s))
                continue;

            s = s.Trim();
            if (s.Length > 0)
                cleaned.Add(s);
        }

        return cleaned.ToArray();
    }

    private static string Pick(string value, string fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        value = value.Trim();
        return value.Length == 0 ? fallback : value;
    }
}

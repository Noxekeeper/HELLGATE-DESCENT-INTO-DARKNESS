using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using NoREroMod.Patches.UI.MindBroken;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Player ↔ faction reputation with per-save-slot JSON persistence.
///
/// The vanilla game has three manual save slots (1..3) and no autosave.
/// We mirror that: reputation data is written to
///   BepInEx/plugins/HellGateJson/CombatAi/PlayerReputation_Slot{NN}.json
/// whenever the player presses "Save" in-game, and is re-loaded whenever the player
/// presses "Load" from the title menu. This keeps reputation consistent with the
/// ES2 save state — loading a save always restores the reputation that was active
/// when that save was created.
///
/// Until the player binds an active slot (i.e. between launch and the first save/load)
/// the in-memory state is volatile: ModifyScore still updates the HUD, but nothing is
/// written to disk. This matches vanilla behaviour — progress is only persisted when
/// the player explicitly saves.
/// </summary>
internal static class PlayerFactionReputation
{
    private static readonly Dictionary<int, float> _scoreByFaction = new Dictionary<int, float>();
    private static int _activeSlotZeroBased = -1;
    private static bool _dirty;
    private const string SaveFileNameFormat = "PlayerReputation_Slot{0:00}.json";

    public const float MinScore = -100f;
    public const float MaxScore = 100f;
    public const float HostileThreshold = -25f;
    public const float FriendlyThreshold = 25f;

    // ---- query API ----

    public static float GetScore(int factionId)
    {
        if (FactionIds.IsPassiveNonCombat(factionId))
            return 0f;
        if (FactionIds.IsPlayerNativeFaction(factionId))
            return MaxScore;
        float value;
        return _scoreByFaction.TryGetValue(factionId, out value) ? value : 0f;
    }

    public static void SetScore(int factionId, float value)
    {
        if (FactionIds.IsPassiveNonCombat(factionId) || FactionIds.IsPlayerNativeFaction(factionId))
            return;
        float clamped = Mathf.Clamp(value, MinScore, MaxScore);
        float prev;
        if (_scoreByFaction.TryGetValue(factionId, out prev) && Mathf.Approximately(prev, clamped))
            return;
        _scoreByFaction[factionId] = clamped;
        _dirty = true;
    }

    public static void ModifyScore(int factionId, float delta)
    {
        if (FactionIds.IsPassiveNonCombat(factionId) || FactionIds.IsPlayerNativeFaction(factionId) || Mathf.Approximately(delta, 0f))
            return;
        float mbThreshold01 = Mathf.Clamp01(EnemyFactionsConfig.PositiveDeltaMindBrokenThreshold / 100f);
        float mbMultiplier = Mathf.Max(0f, EnemyFactionsConfig.PositiveDeltaMindBrokenMultiplier);
        if (delta > 0f && mbMultiplier > 0f && MindBrokenSystem.Enabled && MindBrokenSystem.Percent >= mbThreshold01)
        {
            // At high MindBroken, positive social shifts accelerate.
            delta *= mbMultiplier;
        }
        SetScore(factionId, GetScore(factionId) + delta);
        if (EnemyFactionsConfig.DebugLogging)
        {
            Plugin.Log?.LogInfo("[Reputation] faction=" + factionId + " delta=" + delta.ToString("0.##") + " -> " + GetScore(factionId).ToString("0.##"));
        }
    }

    public static bool IsHostile(int factionId)
    {
        if (FactionIds.IsPlayerNativeFaction(factionId))
            return false;
        return GetScore(factionId) <= HostileThreshold;
    }

    public static bool IsFriendly(int factionId)
    {
        if (FactionIds.IsPlayerNativeFaction(factionId))
            return true;
        return GetScore(factionId) >= FriendlyThreshold;
    }

    public static string DescribeRelation(int factionId)
    {
        if (FactionIds.IsPlayerNativeFaction(factionId))
            return "native";
        float s = GetScore(factionId);
        if (s <= HostileThreshold) return "hostile";
        if (s >= FriendlyThreshold) return "friendly";
        return "neutral";
    }

    /// <summary>Called by the faction runtime whenever the player provokes an enemy by attacking them.</summary>
    public static void NotifyPlayerAttackedFaction(int factionId)
    {
        if (FactionIds.IsPassiveNonCombat(factionId) || FactionIds.IsPlayerNativeFaction(factionId))
            return;
        ModifyScore(factionId, EnemyFactionsConfig.PlayerAttackReputationDelta);
    }

    /// <summary>
    /// Called when an H-scene ends and the enemy faction is known.
    /// Raises relation with that faction by +10 score points.
    /// </summary>
    public static void NotifyCompletedHSceneWithFaction(int factionId)
    {
        if (FactionIds.IsPassiveNonCombat(factionId))
            return;
        ModifyScore(factionId, EnemyFactionsConfig.HSceneCompletedReputationDelta);
    }

    public static void ResetAll()
    {
        _scoreByFaction.Clear();
        _dirty = true;
    }

    // ---- slot binding ----

    /// <summary>Current save slot (1..3) or 0 if nothing is bound yet.</summary>
    public static int ActiveSlotOneBased => _activeSlotZeroBased < 0 ? 0 : _activeSlotZeroBased + 1;

    /// <summary>Bind the active save slot. Does NOT touch in-memory state.</summary>
    public static void BindActiveSlot(int slotZeroBased)
    {
        if (slotZeroBased < 0 || slotZeroBased > 2)
        {
            Plugin.Log?.LogWarning("[Reputation] BindActiveSlot rejected: slot=" + slotZeroBased + " (expected 0..2)");
            return;
        }
        _activeSlotZeroBased = slotZeroBased;
        Plugin.Log?.LogInfo("[Reputation] Active slot bound to " + (slotZeroBased + 1));
    }

    /// <summary>
    /// Replace the in-memory reputation with the contents of the currently active
    /// slot's JSON file. Used after the player presses "Load" in the title menu.
    /// If the slot has no JSON yet, the dictionary is cleared so the save starts fresh.
    /// </summary>
    public static void LoadFromActiveSlot()
    {
        if (_activeSlotZeroBased < 0)
        {
            Plugin.Log?.LogWarning("[Reputation] LoadFromActiveSlot called with no active slot bound");
            return;
        }

        string path = GetSavePathForSlot(_activeSlotZeroBased);
        bool exists = File.Exists(path);
        Plugin.Log?.LogInfo("[Reputation] Loading slot " + ActiveSlotOneBased + " from '" + path + "' (exists=" + exists + ")");

        _scoreByFaction.Clear();
        _dirty = false;

        if (!exists)
        {
            Plugin.Log?.LogInfo("[Reputation] Slot " + ActiveSlotOneBased + " has no file yet, starting empty.");
            return;
        }

        try
        {
            string raw = File.ReadAllText(path);
            int loaded = ParseAndApplyScores(raw);
            Plugin.Log?.LogInfo("[Reputation] Loaded " + loaded + " entries from slot " + ActiveSlotOneBased);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Reputation] Load failed for slot " + ActiveSlotOneBased + ": " + ex.Message);
        }
    }

    // Regex-based parser — independent of Unity's JsonUtility which has quirks with
    // nested arrays on older Unity versions and can silently return null on valid input.
    private static readonly Regex EntryPattern = new Regex(
        "\"FactionId\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"Score\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
        RegexOptions.CultureInvariant);

    private static int ParseAndApplyScores(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return 0;

        MatchCollection matches = EntryPattern.Matches(raw);
        int applied = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            Match m = matches[i];
            if (!m.Success || m.Groups.Count < 3) continue;

            int factionId;
            if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out factionId))
                continue;
            float score;
            if (!float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out score))
                continue;
            if (FactionIds.IsPassiveNonCombat(factionId) || FactionIds.IsPlayerNativeFaction(factionId))
                continue;

            _scoreByFaction[factionId] = Mathf.Clamp(score, MinScore, MaxScore);
            applied++;
        }
        return applied;
    }

    // ---- save API ----
    // Intentionally no periodic autosave: reputation persistence must mirror vanilla
    // manual Save/Load slot behavior. Writes are performed only via SaveHook.

    public static void SaveToActiveSlot(bool force)
    {
        if (_activeSlotZeroBased < 0)
        {
            if (force)
                Plugin.Log?.LogWarning("[Reputation] SaveToActiveSlot(force=true) skipped — no active slot bound");
            return;
        }

        if (!force && !_dirty)
            return;

        try
        {
            string path = GetSavePathForSlot(_activeSlotZeroBased);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"Scores\": [\n");
            bool first = true;
            foreach (KeyValuePair<int, float> kvp in _scoreByFaction)
            {
                if (FactionIds.IsPlayerNativeFaction(kvp.Key))
                    continue;
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("    { \"FactionId\": ").Append(kvp.Key)
                  .Append(", \"Score\": ").Append(kvp.Value.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(" }");
            }
            sb.Append("\n  ]\n}\n");

            File.WriteAllText(path, sb.ToString());
            _dirty = false;

            Plugin.Log?.LogInfo("[Reputation] Saved " + _scoreByFaction.Count + " entries to slot " + ActiveSlotOneBased + " (force=" + force + ", path=" + path + ")");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Reputation] Save failed for slot " + ActiveSlotOneBased + ": " + ex.Message);
        }
    }

    // ---- path helpers ----

    private static string GetSavePathForSlot(int slotZeroBased)
    {
        string fileName = string.Format(SaveFileNameFormat, slotZeroBased + 1);
        try
        {
            string combatAiDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "CombatAi");
            return Path.Combine(combatAiDir, fileName);
        }
        catch
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            return Path.Combine(Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson"), Path.Combine("CombatAi", fileName));
        }
    }

    // Note: deserialization uses a hand-rolled regex parser (see ParseAndApplyScores) —
    // UnityEngine.JsonUtility silently returned null arrays after Clear() in edge cases,
    // so the old SaveFileDto/SaveEntryDto classes were removed along with JsonUtility usage.
}

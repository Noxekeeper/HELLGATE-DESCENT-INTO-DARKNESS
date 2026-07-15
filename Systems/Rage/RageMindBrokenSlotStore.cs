using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using NoREroMod.Patches.UI.MindBroken;
using UnityEngine;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Per-save-slot persistence for Rage bar and MindBroken (0..1 fraction), mirroring
/// <see cref="NoREroMod.Systems.CombatAi.Factions.PlayerFactionReputation"/> and
/// <see cref="NoREroMod.Systems.Economy.GoldWallet"/>.
/// Files: <c>BepInEx/plugins/HellGateJson/PlayerState/PlayerRageMindBroken_Slot{NN}.json</c>.
/// </summary>
internal static class RageMindBrokenSlotStore
{
    private const string SaveFileNameFormat = "PlayerRageMindBroken_Slot{0:00}.json";

    private static int _activeSlotZeroBased = -1;

    private static readonly Regex RagePattern = new Regex(
        "\"RagePercent\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
        RegexOptions.CultureInvariant);

    private static readonly Regex MbPattern = new Regex(
        "\"MindBrokenFraction\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
        RegexOptions.CultureInvariant);

    private static readonly Regex MbCdPattern = new Regex(
        "\"MindBrokenBadEndCountdownRemaining\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
        RegexOptions.CultureInvariant);

    public static int ActiveSlotOneBased => _activeSlotZeroBased < 0 ? 0 : _activeSlotZeroBased + 1;

    public static void BindActiveSlot(int slotZeroBased)
    {
        if (slotZeroBased < 0 || slotZeroBased > 2)
        {
            Plugin.Log?.LogWarning("[RageMindBrokenSave] BindActiveSlot rejected: slot=" + slotZeroBased + " (expected 0..2)");
            return;
        }
        _activeSlotZeroBased = slotZeroBased;
    }

    public static void SaveToActiveSlot()
    {
        if (_activeSlotZeroBased < 0)
        {
            Plugin.Log?.LogWarning("[RageMindBrokenSave] SaveToActiveSlot skipped — no active slot bound");
            return;
        }

        try
        {
            float rage = RageSystem.Enabled ? RageSystem.Percent : 0f;
            float mbFrac = MindBrokenSystem.Enabled ? MindBrokenSystem.Percent : 0f;
            float mbCd = MindBrokenSystem.Enabled ? MindBrokenSystem.CountdownTimeRemaining : 0f;

            string path = GetSavePathForSlot(_activeSlotZeroBased);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"RagePercent\": ").Append(rage.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"MindBrokenFraction\": ").Append(mbFrac.ToString("0.######", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"MindBrokenBadEndCountdownRemaining\": ").Append(mbCd.ToString("0.###", CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("}\n");
            File.WriteAllText(path, sb.ToString());

            Plugin.Log?.LogInfo("[RageMindBrokenSave] Saved slot " + ActiveSlotOneBased + " (rage=" + rage.ToString("0.#") + "%, MB=" + (mbFrac * 100f).ToString("0.#") + "%)");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[RageMindBrokenSave] Save failed: " + ex.Message);
        }
    }

    public static void LoadFromActiveSlot()
    {
        if (_activeSlotZeroBased < 0)
        {
            Plugin.Log?.LogWarning("[RageMindBrokenSave] LoadFromActiveSlot called with no active slot bound");
            return;
        }

        string path = GetSavePathForSlot(_activeSlotZeroBased);
        if (!File.Exists(path))
        {
            Plugin.Log?.LogInfo("[RageMindBrokenSave] Slot " + ActiveSlotOneBased + " has no file yet; resetting Rage/MindBroken (mirrors reputation/gold)");
            if (RageSystem.Enabled)
                RageSystem.ResetState();
            if (MindBrokenSystem.Enabled)
                MindBrokenSystem.ResetState();
            return;
        }

        try
        {
            string raw = File.ReadAllText(path);
            float rage = 0f;
            float mb = 0f;
            float mbCd = 0f;

            Match m = RagePattern.Match(raw);
            if (m.Success)
                float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out rage);

            m = MbPattern.Match(raw);
            if (m.Success)
                float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out mb);

            m = MbCdPattern.Match(raw);
            if (m.Success)
                float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out mbCd);

            if (RageSystem.Enabled)
                RageSystem.RestorePersistedBarPercent(rage);
            if (MindBrokenSystem.Enabled)
                MindBrokenSystem.RestorePersistedState(mb, mbCd);

            Plugin.Log?.LogInfo("[RageMindBrokenSave] Loaded slot " + ActiveSlotOneBased + " (rage=" + rage.ToString("0.#") + "%, MB=" + (mb * 100f).ToString("0.#") + "%)");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[RageMindBrokenSave] Load failed for slot " + ActiveSlotOneBased + ": " + ex.Message);
        }
    }

    private static string GetSavePathForSlot(int slotZeroBased)
    {
        string fileName = string.Format(SaveFileNameFormat, slotZeroBased + 1);
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "PlayerState");
        return Path.Combine(dir, fileName);
    }
}

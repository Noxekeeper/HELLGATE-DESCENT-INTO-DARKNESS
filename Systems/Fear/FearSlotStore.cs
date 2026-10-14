using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions.Patches;
using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Separate from Rage/MB: HellGateJson/PlayerState/PlayerFear_Slot{NN}.json
/// </summary>
internal static class FearSlotStore
{
    private const string SaveFileNameFormat = "PlayerFear_Slot{0:00}.json";
    private static int _activeSlotZeroBased = -1;

    private static readonly Regex MoralePattern = new Regex(
        "\"Morale\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
        RegexOptions.CultureInvariant);

    public static void BindActiveSlot(int slotZeroBased)
    {
        if (slotZeroBased < 0 || slotZeroBased > 2)
        {
            Plugin.Log?.LogWarning("[FearSave] BindActiveSlot rejected: slot=" + slotZeroBased);
            return;
        }
        _activeSlotZeroBased = slotZeroBased;
    }

    public static void SaveToActiveSlot()
    {
        if (!FearConfig.IsEnabled) return;
        if (_activeSlotZeroBased < 0)
        {
            Plugin.Log?.LogWarning("[FearSave] SaveToActiveSlot skipped — no active slot");
            return;
        }

        try
        {
            string path = GetSavePathForSlot(_activeSlotZeroBased);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            float morale = FearSystem.Morale;
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"Morale\": ").Append(morale.ToString("0.###", CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("}\n");
            File.WriteAllText(path, sb.ToString());
            Plugin.Log?.LogInfo("[FearSave] Saved slot " + (_activeSlotZeroBased + 1) + " Morale=" + morale.ToString("0.#"));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[FearSave] Save failed: " + ex.Message);
        }
    }

    public static void LoadFromActiveSlot()
    {
        if (!FearConfig.IsEnabled) return;
        if (_activeSlotZeroBased < 0)
        {
            Plugin.Log?.LogWarning("[FearSave] LoadFromActiveSlot — no active slot");
            return;
        }

        string path = GetSavePathForSlot(_activeSlotZeroBased);
        if (!File.Exists(path))
        {
            FearSystem.ResetState();
            Plugin.Log?.LogInfo("[FearSave] Slot " + (_activeSlotZeroBased + 1) + " has no file; Morale reset");
            return;
        }

        try
        {
            string raw = File.ReadAllText(path);
            float morale = 0f;
            Match m = MoralePattern.Match(raw);
            if (m.Success)
                float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out morale);
            FearSystem.RestorePersistedMorale(morale);
            Plugin.Log?.LogInfo("[FearSave] Loaded slot " + (_activeSlotZeroBased + 1) + " Morale=" + morale.ToString("0.#"));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[FearSave] Load failed: " + ex.Message);
        }
    }

    private static string GetSavePathForSlot(int slotZeroBased)
    {
        string fileName = string.Format(SaveFileNameFormat, slotZeroBased + 1);
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "PlayerState");
        return Path.Combine(dir, fileName);
    }
}

[HarmonyPatch(typeof(SaveFile), "SetYesButtonClicked")]
internal static class FearSaveHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveFile __instance)
    {
        try
        {
            if (!FearConfig.IsEnabled) return;
            int slot = PlayerFactionReputationSaveHookPatch.ReadCurrentSelectNum(typeof(SaveFile), __instance);
            if (slot < 0) return;
            FearSlotStore.BindActiveSlot(slot);
            FearSlotStore.SaveToActiveSlot();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[FearSave] SaveHook threw: " + ex);
        }
    }
}

[HarmonyPatch(typeof(LoadFile), "SetYesButtonClicked")]
internal static class FearLoadHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadFile __instance)
    {
        try
        {
            if (!FearConfig.IsEnabled) return;
            int slot = PlayerFactionReputationSaveHookPatch.ReadCurrentSelectNum(typeof(LoadFile), __instance);
            if (slot < 0) return;
            FearSlotStore.BindActiveSlot(slot);
            FearSlotStore.LoadFromActiveSlot();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[FearSave] LoadHook threw: " + ex);
        }
    }
}

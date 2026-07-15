using System;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions.Patches;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// Persists Rage bar and MindBroken on the same manual Save/Load flow as reputation and gold.
/// </summary>
[HarmonyPatch(typeof(SaveFile), "SetYesButtonClicked")]
internal static class RageMindBrokenSaveHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveFile __instance)
    {
        try
        {
            int slot = PlayerFactionReputationSaveHookPatch.ReadCurrentSelectNum(typeof(SaveFile), __instance);
            if (slot < 0)
            {
                Plugin.Log?.LogWarning("[RageMindBrokenSave] SaveHook: could not read CurrentSelectNum");
                return;
            }
            RageMindBrokenSlotStore.BindActiveSlot(slot);
            RageMindBrokenSlotStore.SaveToActiveSlot();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[RageMindBrokenSave] SaveHook threw: " + ex);
        }
    }
}

[HarmonyPatch(typeof(LoadFile), "SetYesButtonClicked")]
internal static class RageMindBrokenLoadHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadFile __instance)
    {
        try
        {
            int slot = PlayerFactionReputationSaveHookPatch.ReadCurrentSelectNum(typeof(LoadFile), __instance);
            if (slot < 0)
            {
                Plugin.Log?.LogWarning("[RageMindBrokenSave] LoadHook: could not read CurrentSelectNum");
                return;
            }
            RageMindBrokenSlotStore.BindActiveSlot(slot);
            RageMindBrokenSlotStore.LoadFromActiveSlot();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[RageMindBrokenSave] LoadHook threw: " + ex);
        }
    }
}

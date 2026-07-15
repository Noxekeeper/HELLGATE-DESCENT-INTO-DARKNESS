using System;
using HarmonyLib;
using NoREroMod.Systems.Pregnancy.ShelterAttack;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Keeps <see cref="PregnancySlotStore"/> aligned with vanilla save slots (1..3).
/// Mirrors <see cref="NoREroMod.Systems.Economy.Patches.GoldWalletSaveHookPatch"/>.
/// </summary>
[HarmonyPatch(typeof(SaveFile), "SetYesButtonClicked")]
internal static class PregnancySaveHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveFile __instance)
    {
        try
        {
            if (!PregnancyConfig.IsEnabled)
                return;

            int slotZeroBased = PregnancyPersistenceHooks.ReadCurrentSelectNum(typeof(SaveFile), __instance);
            if (slotZeroBased < 0)
            {
                Plugin.Log?.LogWarning("[Pregnancy] SaveHook: could not read CurrentSelectNum from SaveFile");
                return;
            }

            int slotOneBased = slotZeroBased + 1;
            PregnancySlotStore.BindActiveSlot(slotOneBased);
            OffspringHideoutHealth.SyncHideoutHealthToStore();
            PregnancySlotStore.SaveToActiveSlot();

            Plugin.Log?.LogInfo($"[Pregnancy] Saved slot {slotOneBased}, children={PregnancySlotStore.CountAliveChildren()}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy] SaveHook failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(LoadFile), "SetYesButtonClicked")]
internal static class PregnancyLoadHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadFile __instance)
    {
        try
        {
            if (!PregnancyConfig.IsEnabled)
                return;

            int slotZeroBased = PregnancyPersistenceHooks.ReadCurrentSelectNum(typeof(LoadFile), __instance);
            if (slotZeroBased < 0)
            {
                Plugin.Log?.LogWarning("[Pregnancy] LoadHook: could not read CurrentSelectNum from LoadFile");
                return;
            }

            int slotOneBased = slotZeroBased + 1;
            PregnancySlotStore.BindActiveSlot(slotOneBased);
            PregnancySlotStore.LoadFromActiveSlot();
            ShelterAttackDriver.OnAfterSlotLoad();

            Plugin.Log?.LogInfo($"[Pregnancy] Loaded slot {slotOneBased}, children={PregnancySlotStore.CountAliveChildren()} (hideout={PregnancySlotStore.CountChildrenInHideout()})");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy] LoadHook failed: {ex.Message}");
        }
    }
}

internal static class PregnancyPersistenceHooks
{
    internal static int ReadCurrentSelectNum(Type ownerType, object instance)
    {
        if (instance == null)
            return -1;

        var field = AccessTools.Field(ownerType, "CurrentSelectNum");
        if (field == null)
            return -1;

        object raw = field.GetValue(instance);
        return raw is int i ? i : -1;
    }
}

using System;
using HarmonyLib;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Keeps <see cref="PlayerFactionReputation"/> aligned with the vanilla save slots.
///
/// <para>
/// Vanilla flow:
///  - Title menu → Load → player picks slot → <see cref="LoadFile.SetYesButtonClicked"/> fires.
///  - In-game save point → "Save" → <see cref="SaveFile.SetYesButtonClicked"/> fires.
///  - Both methods read <c>this.CurrentSelectNum</c> (0..2) to identify the slot.
/// </para>
///
/// We read that field via reflection (it is private) and bind our active slot so
/// subsequent writes go to the correct per-slot JSON file.
/// </summary>
[HarmonyPatch(typeof(SaveFile), "SetYesButtonClicked")]
internal static class PlayerFactionReputationSaveHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveFile __instance)
    {
        try
        {
            int slot = ReadCurrentSelectNum(typeof(SaveFile), __instance);
            Plugin.Log?.LogInfo("[Reputation] SaveHook fired, slot=" + slot);
            if (slot < 0)
            {
                Plugin.Log?.LogWarning("[Reputation] SaveHook: could not read CurrentSelectNum from SaveFile");
                return;
            }

            PlayerFactionReputation.BindActiveSlot(slot);
            PlayerFactionReputation.SaveToActiveSlot(force: true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[Reputation] SaveHook threw: " + ex);
        }
    }

    internal static int ReadCurrentSelectNum(Type ownerType, object instance)
    {
        if (instance == null) return -1;
        var field = AccessTools.Field(ownerType, "CurrentSelectNum");
        if (field == null) return -1;
        object raw = field.GetValue(instance);
        return raw is int i ? i : -1;
    }
}

[HarmonyPatch(typeof(LoadFile), "SetYesButtonClicked")]
internal static class PlayerFactionReputationLoadHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadFile __instance)
    {
        try
        {
            int slot = PlayerFactionReputationSaveHookPatch.ReadCurrentSelectNum(typeof(LoadFile), __instance);
            Plugin.Log?.LogInfo("[Reputation] LoadHook fired, slot=" + slot);
            if (slot < 0)
            {
                Plugin.Log?.LogWarning("[Reputation] LoadHook: could not read CurrentSelectNum from LoadFile");
                return;
            }

            PlayerFactionReputation.BindActiveSlot(slot);
            PlayerFactionReputation.LoadFromActiveSlot();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[Reputation] LoadHook threw: " + ex);
        }
    }
}

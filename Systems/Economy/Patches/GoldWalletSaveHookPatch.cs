using System;
using HarmonyLib;

namespace NoREroMod.Systems.Economy.Patches;

/// <summary>
/// Wallet save hook on <see cref="SaveFile.SetYesButtonClicked"/>.
/// Mirrors <c>PlayerFactionReputationSaveHookPatch</c> 1:1, except for the storage path.
/// </summary>
[HarmonyPatch(typeof(SaveFile), "SetYesButtonClicked")]
internal static class GoldWalletSaveHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveFile __instance)
    {
        try
        {
            int slot = ReadCurrentSelectNum(typeof(SaveFile), __instance);
            if (slot < 0)
            {
                Plugin.Log?.LogWarning("[GoldWallet] SaveHook: could not read CurrentSelectNum from SaveFile");
                return;
            }

            GoldWallet.BindActiveSlot(slot);
            GoldWallet.SaveToActiveSlot(force: true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[GoldWallet] SaveHook threw: " + ex);
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

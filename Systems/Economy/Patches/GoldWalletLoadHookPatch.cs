using System;
using HarmonyLib;

namespace NoREroMod.Systems.Economy.Patches;

/// <summary>
/// Wallet load hook on <see cref="LoadFile.SetYesButtonClicked"/>.
/// Mirrors <c>PlayerFactionReputationLoadHookPatch</c> 1:1.
/// </summary>
[HarmonyPatch(typeof(LoadFile), "SetYesButtonClicked")]
internal static class GoldWalletLoadHookPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadFile __instance)
    {
        try
        {
            int slot = GoldWalletSaveHookPatch.ReadCurrentSelectNum(typeof(LoadFile), __instance);
            if (slot < 0)
            {
                Plugin.Log?.LogWarning("[GoldWallet] LoadHook: could not read CurrentSelectNum from LoadFile");
                return;
            }

            GoldWallet.BindActiveSlot(slot);
            GoldWallet.LoadFromActiveSlot();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[GoldWallet] LoadHook threw: " + ex);
        }
    }
}

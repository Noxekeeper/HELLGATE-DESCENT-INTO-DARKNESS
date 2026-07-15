using HarmonyLib;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.UI;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Vanilla story EVs reuse <see cref="playercon.eroflag"/> and disable the root HUD canvas
/// without always clearing <see cref="PlayerStatus._SOUSA"/>. Mouse / E then advances dialog
/// and leaks into Rewired Attack on the same click (sword swings during cutscenes).
/// </summary>
internal static class VanillaStoryEventInputGuard
{
    internal static bool IsStoryEventFakeEroflag(playercon player)
    {
        if (player == null || !player.eroflag || player.erodown != 0)
            return false;

        return !PlayerEroContextUtility.IsAnyEnemyEroActive();
    }

    internal static bool IsRealEnemyHScene(playercon player)
    {
        if (player == null || !player.eroflag)
            return false;

        return player.erodown != 0 || PlayerEroContextUtility.IsAnyEnemyEroActive();
    }

    internal static bool ShouldSuppressCombatInput(playercon player, PlayerStatus status)
    {
        if (player == null || status == null || player._Death)
            return false;

        if (EventCorePause.IsFrozen)
            return true;

        if (IsStoryEventFakeEroflag(player))
            return true;

        if (HudVisibilityGate.IsNpcDiaryReaderOpen())
            return true;

        // Dialog / nightmare CG / TALK: vanilla disables root Canvas (see HudVisibilityGate).
        if (!HudVisibilityGate.ShouldShowGameplayHud() && !IsRealEnemyHScene(player))
            return true;

        return false;
    }

    internal static void ClearCombatInputFields(playercon player)
    {
        if (player == null)
            return;

        CombatInputFields.KeyAtk?.SetValue(player, false);
        CombatInputFields.KeyAtkPress?.SetValue(player, false);
        CombatInputFields.KeyAtkUp?.SetValue(player, false);
        CombatInputFields.KeyMagic?.SetValue(player, false);
        CombatInputFields.KeyGuard?.SetValue(player, false);
        CombatInputFields.KeyStep?.SetValue(player, false);
        CombatInputFields.KeyDash?.SetValue(player, false);
        CombatInputFields.KeyDash2?.SetValue(player, false);
    }

    internal static void ClearStaleCombatState(playercon player)
    {
        if (player == null)
            return;

        if (player.Attacknow)
        {
            player.Attacknow = false;
            player.Actstate = false;
            player.Atkcount = 0;
            player.Atkcombo = 0;
        }

        if (player.magicnow)
            player.magicnow = false;
    }

    private static class CombatInputFields
    {
        internal static readonly System.Reflection.FieldInfo KeyAtk =
            AccessTools.Field(typeof(playercon), "key_atk");
        internal static readonly System.Reflection.FieldInfo KeyAtkPress =
            AccessTools.Field(typeof(playercon), "key_atk_press");
        internal static readonly System.Reflection.FieldInfo KeyAtkUp =
            AccessTools.Field(typeof(playercon), "key_atk_up");
        internal static readonly System.Reflection.FieldInfo KeyMagic =
            AccessTools.Field(typeof(playercon), "key_magic");
        internal static readonly System.Reflection.FieldInfo KeyGuard =
            AccessTools.Field(typeof(playercon), "key_guard");
        internal static readonly System.Reflection.FieldInfo KeyStep =
            AccessTools.Field(typeof(playercon), "key_step");
        internal static readonly System.Reflection.FieldInfo KeyDash =
            AccessTools.Field(typeof(playercon), "key_dash");
        internal static readonly System.Reflection.FieldInfo KeyDash2 =
            AccessTools.Field(typeof(playercon), "key_dash2");
    }
}

[HarmonyPatch(typeof(playercon), "Update")]
internal static class VanillaStoryEventCombatStatePrefixPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (!VanillaStoryEventInputGuard.ShouldSuppressCombatInput(__instance, ___playerstatus))
            return;

        VanillaStoryEventInputGuard.ClearStaleCombatState(__instance);
    }
}

/// <summary>
/// Same pattern as <see cref="EventCorePlayerAttackInputBlockPatch"/> for vanilla story EVs.
/// </summary>
[HarmonyPatch(typeof(playercon), "Getinput")]
internal static class VanillaStoryEventCombatInputBlockPatch
{
    [HarmonyPostfix]
    private static void Postfix(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (!VanillaStoryEventInputGuard.ShouldSuppressCombatInput(__instance, ___playerstatus))
            return;

        VanillaStoryEventInputGuard.ClearCombatInputFields(__instance);
    }
}

[HarmonyPatch(typeof(playercon), "atk_fun")]
internal static class VanillaStoryEventAtkFunBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix(playercon __instance, PlayerStatus ___playerstatus)
    {
        return !VanillaStoryEventInputGuard.ShouldSuppressCombatInput(__instance, ___playerstatus);
    }
}

[HarmonyPatch(typeof(playercon), "Airatk_fun")]
internal static class VanillaStoryEventAirAtkFunBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix(playercon __instance, PlayerStatus ___playerstatus)
    {
        return !VanillaStoryEventInputGuard.ShouldSuppressCombatInput(__instance, ___playerstatus);
    }
}

[HarmonyPatch(typeof(playercon), "charge_atk")]
internal static class VanillaStoryEventChargeAtkBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix(playercon __instance, PlayerStatus ___playerstatus)
    {
        return !VanillaStoryEventInputGuard.ShouldSuppressCombatInput(__instance, ___playerstatus);
    }
}

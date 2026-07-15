using HarmonyLib;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Adjusts MindBroken and Rage on block and parry.
/// Block (perfect guard): +1.5% Rage (no MindBroken change).
/// Parry: -1% MindBroken and +5% Rage.
///
/// Event hooks:
/// Successful parry: <see cref="playercon.Acttext"/> with "PARRY!!".
/// Successful block: <c>guradcount</c> set to 0.2f (perfect guard only).
/// </summary>
internal static class GuardParryMindBrokenPatch
{
    private const float MB_LOSS_ON_PARRY = -0.01f;
    private const float RAGE_GAIN_ON_BLOCK = 1.5f;
    private const float RAGE_GAIN_ON_PARRY = 5f;

    private static float _guradcountBefore_Damage = -1f;
    private static float _guradcountBefore_Improvement = -1f;

    /// <summary>Detects successful parry via Acttext("PARRY!!").</summary>
    [HarmonyPatch(typeof(playercon), "Acttext")]
    [HarmonyPostfix]
    static void OnActtextCalled(string text)
    {
        if (text == "PARRY!!")
        {
            if (MindBrokenSystem.Enabled)
                MindBrokenSystem.AddPercent(MB_LOSS_ON_PARRY, "parry");
            if (RageSystem.Enabled)
                RageSystem.AddRage(RAGE_GAIN_ON_PARRY, "parry");
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPrefix]
    static void SaveGuradcountBeforeDamage(float ___guradcount)
    {
        _guradcountBefore_Damage = ___guradcount;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPostfix]
    static void CheckBlockAfterDamage(float ___guradcount)
    {
        // Perfect block: guradcount 0f -> 0.2f awards Rage only (no MindBroken).
        if (_guradcountBefore_Damage == 0f && ___guradcount == 0.2f)
        {
            if (RageSystem.Enabled)
                RageSystem.AddRage(RAGE_GAIN_ON_BLOCK, "block");
        }

        _guradcountBefore_Damage = -1f;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPrefix]
    static void SaveGuradcountBeforeDamage_Improvement(float ___guradcount)
    {
        _guradcountBefore_Improvement = ___guradcount;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPostfix]
    static void CheckBlockAfterDamage_Improvement(float ___guradcount)
    {
        if (_guradcountBefore_Improvement == 0f && ___guradcount == 0.2f)
        {
            if (RageSystem.Enabled)
                RageSystem.AddRage(RAGE_GAIN_ON_BLOCK, "block");
        }

        _guradcountBefore_Improvement = -1f;
    }
}

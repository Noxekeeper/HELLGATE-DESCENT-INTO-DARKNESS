using HarmonyLib;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Patches.Player;

/// <summary>
/// GuardParryMindBrokenPatch - Изменение MindBroken и Rage on блоке и парировании
/// - Блок: +1.5% Rage (MB not начисляется)
/// - Парирование: -1% MB + 10 Rage
/// 
/// ПРИВЯЗКА К СОБЫТИЯМ:
/// - Успешное парирование: Acttext("PARRY!!")
/// - Успешный блок: guradcount is set in 0.2f (only on perfect guard)
/// </summary>
internal static class GuardParryMindBrokenPatch
{
    private const float MB_LOSS_ON_PARRY = -0.01f;  // -1% MB за парирование
    private const float RAGE_GAIN_ON_BLOCK = 1.5f;    // +1.5% Rage за идеальный блок
    private const float RAGE_GAIN_ON_PARRY = 10f;   // +10% Rage за парирование
    
    // Сохраняем guradcount until выполнения original method
    private static float _guradcountBefore_Damage = -1f;
    private static float _guradcountBefore_Improvement = -1f;
    
    /// <summary>
    /// Patch on Acttext - отслеживаем успешное парирование by textу "PARRY!!"
    /// </summary>
    [HarmonyPatch(typeof(playercon), "Acttext")]
    [HarmonyPostfix]
    static void OnActtextCalled(string text)
    {
        // Успешное парирование определяется by textу "PARRY!!"
        if (text == "PARRY!!")
        {
            if (MindBrokenSystem.Enabled)
                MindBrokenSystem.AddPercent(MB_LOSS_ON_PARRY, "parry");
            if (RageSystem.Enabled)
                RageSystem.AddRage(RAGE_GAIN_ON_PARRY, "parry");
        }
    }
    
    /// <summary>
    /// Patch on fun_damage - отслеживаем успешный блок by установке guradcount = 0.2f
    /// </summary>
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
        // Успешный блок: guradcount был 0f, became 0.2f — +1 Rage, MB not начисляется
        if (_guradcountBefore_Damage == 0f && ___guradcount == 0.2f)
        {
            if (RageSystem.Enabled)
                RageSystem.AddRage(RAGE_GAIN_ON_BLOCK, "block");
        }
        
        _guradcountBefore_Damage = -1f; // Сброс
    }
    
    /// <summary>
    /// Patch on fun_damage_Improvement - отслеживаем успешный блок by установке guradcount = 0.2f
    /// </summary>
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
        // Успешный блок: guradcount был 0f, became 0.2f — +1 Rage, MB not начисляется
        if (_guradcountBefore_Improvement == 0f && ___guradcount == 0.2f)
        {
            if (RageSystem.Enabled)
                RageSystem.AddRage(RAGE_GAIN_ON_BLOCK, "block");
        }
        
        _guradcountBefore_Improvement = -1f; // Сброс
    }
}
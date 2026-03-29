using System;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Patch for отслеживания убийстin enemies через OnDestroy конкретных классов
/// Безопасный подход - патчим only конкретные классы enemies, not MonoBehaviour
/// </summary>
[HarmonyPatch]
internal static class EnemyKillRecoveryPatch
{
    // Патчим конкретные классы enemies
    [HarmonyPatch(typeof(TouzokuNormal), "OnDestroy")]
    [HarmonyPostfix]
    private static void TouzokuNormal_OnDestroy_Postfix(TouzokuNormal __instance)
    {
        try
        {
            if (!MindBrokenRecoverySystem.IsEnabled) return;
            if (__instance == null) return;
            
            if (__instance.state == TouzokuNormal.enemystate.DEATH)
            {
                MindBrokenRecoverySystem.RegisterKill("touzokunormal");
            }
        }
        catch { }
    }
    
    [HarmonyPatch(typeof(TouzokuAxe), "OnDestroy")]
    [HarmonyPostfix]
    private static void TouzokuAxe_OnDestroy_Postfix(TouzokuAxe __instance)
    {
        try
        {
            if (!MindBrokenRecoverySystem.IsEnabled) return;
            if (__instance == null) return;
            if (__instance.state == TouzokuAxe.enemystate.DEATH)
            {
                MindBrokenRecoverySystem.RegisterKill("touzokuaxe");
            }
        }
        catch { }
    }
    
    [HarmonyPatch(typeof(Bigoni), "OnDestroy")]
    [HarmonyPostfix]
    private static void Bigoni_OnDestroy_Postfix(Bigoni __instance)
    {
        try
        {
            if (!MindBrokenRecoverySystem.IsEnabled) return;
            if (__instance == null) return;
            if (__instance.state == Bigoni.enemystate.DEATH)
            {
                MindBrokenRecoverySystem.RegisterKill("bigonibrother");
            }
        }
        catch { }
    }
    
    [HarmonyPatch(typeof(suraimu), "OnDestroy")]
    [HarmonyPostfix]
    private static void Suraimu_OnDestroy_Postfix(suraimu __instance)
    {
        try
        {
            if (!MindBrokenRecoverySystem.IsEnabled) return;
            if (__instance == null) return;
            if (__instance.state == suraimu.enemystate.DEATH)
            {
                MindBrokenRecoverySystem.RegisterKill("dorei");
            }
        }
        catch { }
    }
    
    [HarmonyPatch(typeof(Mutude), "OnDestroy")]
    [HarmonyPostfix]
    private static void Mutude_OnDestroy_Postfix(Mutude __instance)
    {
        try
        {
            if (!MindBrokenRecoverySystem.IsEnabled) return;
            if (__instance == null) return;
            if (__instance.state == Mutude.enemystate.DEATH)
            {
                MindBrokenRecoverySystem.RegisterKill("mutude");
            }
        }
        catch { }
    }
    
    [HarmonyPatch(typeof(SinnerslaveCrossbow), "OnDestroy")]
    [HarmonyPostfix]
    private static void SinnerslaveCrossbow_OnDestroy_Postfix(SinnerslaveCrossbow __instance)
    {
        try
        {
            if (!MindBrokenRecoverySystem.IsEnabled) return;
            if (__instance == null) return;
            if (__instance.state == SinnerslaveCrossbow.enemystate.DEATH)
            {
                MindBrokenRecoverySystem.RegisterKill("dorei");
            }
        }
        catch { }
    }
}


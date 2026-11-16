using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Audio;

/// <summary>
/// Plays death sound on rising edge into DEATH animation (not every re-set of DEATH).
/// Humans: AttackSounds/Human/Death. Undead skeletons: AttackSounds/Undead/Death/BoneDeath.
/// </summary>
[HarmonyPatch]
internal static class DeathSoundPatch
{
    private static readonly Dictionary<int, string> _lastAnimByEnemy = new Dictionary<int, string>();

    [HarmonyPatch(typeof(TouzokuNormal), "setanimation")]
    [HarmonyPostfix]
    private static void TouzokuNormal_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(TouzokuAxe), "setanimation")]
    [HarmonyPostfix]
    private static void TouzokuAxe_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(SinnerslaveCrossbow), "setanimation")]
    [HarmonyPostfix]
    private static void SinnerslaveCrossbow_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(Vagrant), "setanimation")]
    [HarmonyPostfix]
    private static void Vagrant_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(Mafiamuscle), "setanimation")]
    [HarmonyPostfix]
    private static void Mafiamuscle_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(BlackMafia), "setanimation")]
    [HarmonyPostfix]
    private static void BlackMafia_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(SlaveBigAxe), "setanimation")]
    [HarmonyPostfix]
    private static void SlaveBigAxe_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(OtherSlavebigAxe), "setanimation")]
    [HarmonyPostfix]
    private static void OtherSlavebigAxe_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(Inquisition), "setanimation")]
    [HarmonyPostfix]
    private static void Inquisition_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(InquisitionWhite), "setanimation")]
    [HarmonyPostfix]
    private static void InquisitionWhite_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(InquisitionRED), "setanimation")]
    [HarmonyPostfix]
    private static void InquisitionRED_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    // Undead skeleton base classes (reskins of non-human combat prefabs).
    [HarmonyPatch(typeof(BigMerman), "setanimation")]
    [HarmonyPostfix]
    private static void BigMerman_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(Bigoni), "setanimation")]
    [HarmonyPostfix]
    private static void Bigoni_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    [HarmonyPatch(typeof(Pilgrim), "setanimation")]
    [HarmonyPostfix]
    private static void Pilgrim_Death(EnemyDate __instance, string name) => OnDeathTransition(__instance, name);

    private static void OnDeathTransition(EnemyDate enemy, string animName)
    {
        if (enemy == null || string.IsNullOrEmpty(animName)) return;

        int id = enemy.GetInstanceID();
        string prev;
        _lastAnimByEnemy.TryGetValue(id, out prev);
        _lastAnimByEnemy[id] = animName;

        // Only the first transition into DEATH — looping/re-applied DEATH must not re-trigger.
        if (animName != "DEATH") return;
        if (prev == "DEATH") return;

        AttackSoundSystem.TryPlayDeathSound(enemy);
    }
}

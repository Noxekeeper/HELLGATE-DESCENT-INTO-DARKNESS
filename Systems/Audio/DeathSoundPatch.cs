using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Audio;

/// <summary>
/// Plays death sound when human enemies transition to DEATH animation.
/// Patches setanimation() - when name == "DEATH", plays random clip from Death folder.
/// </summary>
[HarmonyPatch]
internal static class DeathSoundPatch
{
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

    private static void OnDeathTransition(EnemyDate enemy, string animName)
    {
        if (animName != "DEATH") return;
        if (enemy == null) return;
        AttackSoundSystem.TryPlayDeathSound(enemy);
    }
}

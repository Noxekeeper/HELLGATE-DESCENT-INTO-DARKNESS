using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Animation-based grab threat system.
/// When enemy transitions TO "IDLE" (stops after attack), show threat phrase.
/// Patches setanimation() - called only when animation changes.
/// 100% probability, 10s cooldown per enemy, spam control.
/// </summary>
[HarmonyPatch]
internal static class GrabThreatIdlePatch
{
    [HarmonyPatch(typeof(TouzokuNormal), "setanimation")]
    [HarmonyPostfix]
    private static void TouzokuNormal_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(TouzokuAxe), "setanimation")]
    [HarmonyPostfix]
    private static void TouzokuAxe_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(SinnerslaveCrossbow), "setanimation")]
    [HarmonyPostfix]
    private static void SinnerslaveCrossbow_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(Vagrant), "setanimation")]
    [HarmonyPostfix]
    private static void Vagrant_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(Mafiamuscle), "setanimation")]
    [HarmonyPostfix]
    private static void Mafiamuscle_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(BlackMafia), "setanimation")]
    [HarmonyPostfix]
    private static void BlackMafia_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(SlaveBigAxe), "setanimation")]
    [HarmonyPostfix]
    private static void SlaveBigAxe_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    [HarmonyPatch(typeof(OtherSlavebigAxe), "setanimation")]
    [HarmonyPostfix]
    private static void OtherSlavebigAxe_Idle(EnemyDate __instance, string name) => OnIdleTransition(__instance, name);

    private static void OnIdleTransition(EnemyDate enemy, string animName)
    {
        if (animName != "IDLE" && animName != "IDLE2") return;
        if (enemy == null || enemy.Hp <= 0) return;
        GrabThreatDialogues.TryShowThreatOnIdle(enemy);
    }
}

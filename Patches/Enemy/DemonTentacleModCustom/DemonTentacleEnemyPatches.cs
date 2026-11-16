using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.DemonTentacleModCustom;

/// <summary>Hooks on vanilla AngelStatue. Every hook is a no-op for non-DemonTentacle instances.</summary>
[HarmonyPatch]
internal static class DemonTentacleEnemyPatches
{
    [HarmonyPatch(typeof(AngelStatue), "Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void AngelStatue_Start(AngelStatue __instance) => DemonTentacleRuntime.ApplyStats(__instance);

    [HarmonyPatch(typeof(AngelStatue), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool AngelStatue_OnTriggerEnter2D(AngelStatue __instance, Collider2D col)
        => DemonTentacleRuntime.HandleHit(__instance, col);

    [HarmonyPatch(typeof(AngelStatue), "OnEvent")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool AngelStatue_OnEvent(AngelStatue __instance, Spine.Event e)
        => DemonTentacleRuntime.HandleSpineEvent(__instance, e);
}

using HarmonyLib;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.CombatAi.Patches;

/// <summary>
/// Postfix on EnemyDate.Distance_fun. Distance check unchanged (standard Atkdistance).
/// In melee, speed up statecount accumulation so melee attacks occur more often.
/// Config: HellGateJson/CombatAi/CombatAi.json (MeleeAttackRateMultiplier).
/// </summary>
[HarmonyPatch(typeof(EnemyDate), "Distance_fun")]
internal static class EnemyDateDistanceFunPatch
{
    private static bool _loggedStartup;

    [HarmonyPostfix]
    public static void Postfix(EnemyDate __instance)
    {
        if (!CombatAiConfig.Enable)
            return;

        float mult = CombatAiConfig.MeleeAttackRateMultiplier;
        if (mult <= 1f)
            return;

        if (!_loggedStartup)
        {
            _loggedStartup = true;
            Plugin.Log?.LogInfo("[CombatAi] Module active (standard distance check, melee attack rate boost).");
        }

        float atk = __instance.Atkdistance;
        if (atk <= 0f)
            return;

        float distance = __instance.distance;
        float distance_y = __instance.distance_y;
        // Standard check: in attack zone horizontally and vertically
        bool inMeleeRange = Mathf.Abs(distance) <= atk && Mathf.Abs(distance_y) <= atk;

        if (inMeleeRange)
        {
            float extra = (mult - 1f) * Time.deltaTime;
            __instance.statecount += extra;
        }
    }
}

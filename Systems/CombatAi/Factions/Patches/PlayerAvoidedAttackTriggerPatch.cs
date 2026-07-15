using HarmonyLib;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Hooks player "Avoided" branch (dash evade while being hit) and uses it
/// as the event trigger for de-escalation relation rolls.
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
internal static class PlayerAvoidedAttackTriggerImprovedPatch
{
    [HarmonyPrefix]
    private static void Prefix(playercon __instance, ref bool __state)
    {
        __state = __instance != null && __instance.stepfrag;
    }

    [HarmonyPostfix]
    private static void Postfix(playercon __instance, bool __state)
    {
        if (!__state || __instance == null)
            return;
        FactionDeescalationRuntime.NotifyPlayerAvoidedEnemyAttack(__instance);
    }
}

[HarmonyPatch(typeof(playercon), "fun_damage")]
internal static class PlayerAvoidedAttackTriggerLegacyPatch
{
    [HarmonyPrefix]
    private static void Prefix(playercon __instance, ref bool __state)
    {
        __state = __instance != null && __instance.stepfrag;
    }

    [HarmonyPostfix]
    private static void Postfix(playercon __instance, bool __state)
    {
        if (!__state || __instance == null)
            return;
        FactionDeescalationRuntime.NotifyPlayerAvoidedEnemyAttack(__instance);
    }
}

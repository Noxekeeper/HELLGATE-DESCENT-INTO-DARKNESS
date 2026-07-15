using HarmonyLib;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Bootstrap patch for Enemy Factions module.
/// Safe by default: does nothing unless EnemyFactionsConfig.Enable is true.
/// </summary>
[HarmonyPatch(typeof(EnemyDate), "start_fun")]
internal static class EnemyDateFactionBootstrapPatch
{
    private static bool _loggedStartup;

    [HarmonyPostfix]
    private static void Postfix(EnemyDate __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;

        if (!EnemyFactionsConfig.Enable)
            return;

        EnemyFactionRuntime.RegisterEnemy(__instance);

        SpawnManagedInstance managed = __instance.GetComponent<SpawnManagedInstance>();
        if (managed != null && managed.SpawnHostileToPlayer)
            EnemyFactionRuntime.MarkSessionHostileToPlayer(__instance);

        if (EnemyFactionsConfig.DebugLogging && !_loggedStartup)
        {
            _loggedStartup = true;
            Plugin.Log?.LogInfo("[EnemyFactions] Bootstrap active (isolated module).");
        }
    }
}

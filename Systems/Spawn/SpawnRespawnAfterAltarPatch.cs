using HarmonyLib;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Ensures HellGate custom spawn configs are re-applied immediately after altar reset
/// in the same location (without scene change).
/// </summary>
[HarmonyPatch(typeof(Savepoint_on), "fun_ALLreset")]
internal static class SpawnRespawnAfterAltarPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        HellGateLocationSpawnRefresh.RefreshAfterAltar();
    }

    /// <summary>
    /// Re-run HellGate spawn configs for the current scene (same logic as the Harmony postfix after a real altar <c>fun_ALLreset</c>).
    /// </summary>
    internal static void RunHellGateRespawnAfterVanillaAltarReset()
    {
        HellGateLocationSpawnRefresh.RefreshAfterAltar();
    }

    /// <summary>
    /// Spawn-point recorder (F11): same combat respawn path as touching an altar.
    /// </summary>
    internal static void TriggerSpawnEditHotReload()
    {
        HellGateLocationSpawnRefresh.TriggerSpawnEditHotReload();
    }
}

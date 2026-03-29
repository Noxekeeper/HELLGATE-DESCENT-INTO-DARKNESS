using HarmonyLib;

namespace NoREroMod;

[HarmonyPatch(typeof(SpawnParent), "fun_SpawnRE")]
internal static class SpawnResetPatch
{
    [HarmonyPostfix]
    private static void ClearSpawnCaches()
    {
        try
        {
            SpawnPointAnalyzer.Reset();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[SPAWN RESET] Failed to clear spawn caches: {ex.Message}");
        }
    }
}


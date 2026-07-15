using System;
using BepInEx;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Systems.EventCore.Reinforcement;

internal static class ReinforcementEncounterBootstrap
{
    private static bool _installed;

    internal static void Install(BaseUnityPlugin plugin)
    {
        if (_installed)
            return;

        _installed = true;
        EventCorePaths.Initialize();

        if (plugin == null || plugin.gameObject == null)
            return;

        ReinforcementEncounterDriver driver = plugin.gameObject.GetComponent<ReinforcementEncounterDriver>();
        if (driver == null)
        {
            driver = plugin.gameObject.AddComponent<ReinforcementEncounterDriver>();
            Plugin.Log?.LogInfo("[Reinforcement] Driver installed (registry reloads on boot + scene changes except Gametitle).");
        }

        try
        {
            driver.ReloadFromDisk();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Reinforcement] Initial ReloadFromDisk failed: {ex.Message}");
        }
    }
}

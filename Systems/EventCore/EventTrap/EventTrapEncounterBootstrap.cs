using System;
using BepInEx;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Systems.EventCore.EventTrap;

internal static class EventTrapEncounterBootstrap
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

        EventTrapEncounterDriver driver = plugin.gameObject.GetComponent<EventTrapEncounterDriver>();
        if (driver == null)
        {
            driver = plugin.gameObject.AddComponent<EventTrapEncounterDriver>();
            Plugin.Log?.LogInfo(
                "[EventTrap] Driver installed (registry reloads on boot + on scene changes except Gametitle).");
        }

        // Do not rely on MonoBehaviour Start() on the plugin GameObject — it may not run before gameplay in some setups.
        try
        {
            driver.ReloadFromDisk();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[EventTrap] Initial ReloadFromDisk failed: {ex.Message}");
        }
    }
}

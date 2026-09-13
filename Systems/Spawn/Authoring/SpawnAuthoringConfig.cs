using BepInEx.Configuration;

namespace NoREroMod.Systems.Spawn;

internal static class SpawnAuthoringConfig
{
    internal static ConfigEntry<bool> UiEnable;
    internal static ConfigEntry<bool> PauseGameplay;

    internal static void Bind(Plugin plugin)
    {
        if (plugin == null)
            return;

        UiEnable = plugin.Config.Bind(
            "SpawnTemplates",
            "AuthoringUiEnable",
            true,
            "F11 Spawn System Editor V2.0: Point, catalogs (Enemies / Hostage / Trap / Lethal / Decor / Gold / EventTrap / EventCore), Place / Edit / Delete.");

        PauseGameplay = plugin.Config.Bind(
            "SpawnTemplates",
            "AuthoringPauseGameplay",
            true,
            "While F11 authoring is ON, set timeScale=0 and lock player control (IMGUI still works). Cuts FPS hit from live combat/AI.");
    }
}

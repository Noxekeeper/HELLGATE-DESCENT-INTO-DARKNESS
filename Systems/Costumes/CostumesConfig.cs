using BepInEx.Configuration;

namespace NoREroMod.Systems.Costumes;

/// <summary>
/// BepInEx settings for Costumes: unlock alt skins in the costume-change menu.
/// Does not open Trade NPC or mark Trade purchases as done.
/// </summary>
internal static class CostumesConfig
{
    private const string Section = "Costumes";

    public static ConfigEntry<bool> Enable;

    private static bool _initialized;

    public static bool IsEnabled => Enable != null && Enable.Value;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        Enable = cfg.Bind(Section, "Enable", true,
            "Unlock alt costumes in the costume-change menu without Trade purchase (gunner + Vendetta illusory outfits). Does not unlock the Trade NPC or auto-complete Trade slots.");
    }
}

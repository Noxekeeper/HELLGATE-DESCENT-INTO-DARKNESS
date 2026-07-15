using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.UI.Portrait;

/// <summary>
/// Resolves which Portrait_mod state folder to use; <see cref="PortraitAssetLoader"/> picks the first existing alias on disk.
/// </summary>
internal static class PortraitStateResolver
{
    internal static string ResolveKey(bool eroflag, int costumeBreak, float mindBrokenFraction)
    {
        float threshold = Plugin.portraitModBrainwashThreshold.Value;

        if (eroflag)
            return "Sex";

        if (RageSystem.Enabled && RageSystem.IsActive)
            return costumeBreak == 1 ? "NakedRage" : "Rage";

        if (MindBrokenSystem.Enabled && mindBrokenFraction >= threshold)
            return "Brainwash";

        return costumeBreak == 1 ? "NakedNormal" : "Normal";
    }
}

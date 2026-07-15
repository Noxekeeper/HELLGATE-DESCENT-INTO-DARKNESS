using NoREroMod.Systems.Effects;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Full H-scene style black screen (hide world + skeletons) for lethal trap death clips.</summary>
internal static class LethalTrapDeathBlackScreen
{
    internal static void Show()
    {
        HSceneBlackBackgroundSystem.ActivateForLethalTrapDeathClip();
    }

    internal static void Hide()
    {
        HSceneBlackBackgroundSystem.DeactivateForLethalTrapDeathClip();
    }

    internal static void RefreshHiddenVisuals()
    {
        HSceneBlackBackgroundSystem.RefreshLethalTrapDeathVisuals();
    }
}

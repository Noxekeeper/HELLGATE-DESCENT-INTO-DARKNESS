using UnityEngine;
using NoREroMod.Systems.UI;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>
/// Compatibility wrapper for EventCore callers; shared font resolution lives in HellGateFontProvider.
/// </summary>
internal static class EventCoreUiFont
{
    internal static Font GetUiFont()
    {
        return HellGateFontProvider.GetUiFont();
    }
}

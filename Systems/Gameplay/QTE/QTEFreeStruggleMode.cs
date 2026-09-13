namespace NoREroMod.Systems.Gameplay.QTE;

/// <summary>
/// Free Struggle — alternate QTE mode: WASD windows stay open during Struggle,
/// every WASD press awards click SP
/// (<see cref="QTESPCalculator.CalculateSPGainClick"/>), and hint buttons
/// use a compact 36px d-pad cross (W/A/S/D). Yellow/red and cooldown penalties
/// are off.
/// Config: <c>[QTEFreeStruggle] Enable</c> (requires <c>[QTE] EnableQTESystem</c>).
/// </summary>
public static class QTEFreeStruggleMode
{
    /// <summary>
    /// True when Free Struggle is on and the main QTE system is enabled.
    /// </summary>
    public static bool IsEnabled =>
        Plugin.enableQTESystem?.Value == true
        && Plugin.qteFreeStruggleEnable?.Value == true;
}

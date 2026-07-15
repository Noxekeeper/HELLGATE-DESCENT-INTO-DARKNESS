namespace NoREroMod.Systems.Diagnostics.TrapBody;

/// <summary>
/// Trap H-scene player-body diagnostics entry point.
/// Enable via <c>HellGateJson/Diagnostics/TrapPlayerBodyDiagnostics.json</c>.
/// Dedicated log: <c>BepInEx/LogOutput/HellGate_TrapPlayerBodyDiag.log</c>.
/// </summary>
internal static class TrapPlayerBodyDiagnostics
{
    public static void Initialize()
    {
        TrapPlayerBodyMonitor.Ensure();
        Plugin.Log?.LogInfo(
            "[TrapBodyDiag] module initialized (JSON: HellGateJson/Diagnostics/TrapPlayerBodyDiagnostics.json;" +
            " log: LogOutput/HellGate_TrapPlayerBodyDiag.log)");
    }
}

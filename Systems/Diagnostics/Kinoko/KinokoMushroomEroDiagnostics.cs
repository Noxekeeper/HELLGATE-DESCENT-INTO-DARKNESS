namespace NoREroMod.Systems.Diagnostics.Kinoko;

/// <summary>
/// Kinoko / MushroomERO H-scene event diagnostics entry point.
/// Enable via <c>HellGateJson/Diagnostics/KinokoMushroomEroDiagnostics.json</c>.
/// Dedicated log: <c>BepInEx/LogOutput/HellGate_KinokoMushroomEroDiag.log</c>.
/// </summary>
internal static class KinokoMushroomEroDiagnostics
{
    public static void Initialize()
    {
        KinokoMushroomEroMonitor.Ensure();
        Plugin.Log?.LogInfo(
            "[KinokoEroDiag] module initialized (JSON: HellGateJson/Diagnostics/KinokoMushroomEroDiagnostics.json;" +
            " log: LogOutput/HellGate_KinokoMushroomEroDiag.log)");
    }
}

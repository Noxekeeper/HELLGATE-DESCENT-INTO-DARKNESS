namespace NoREroMod.Systems.Diagnostics.Tentacle;

/// <summary>
/// Top-level entry point for the tentacle H-scene diagnostics module.
///
/// Wired from <c>Plugin.Awake</c>:
///   <c>NoREroMod.Systems.Diagnostics.Tentacle.TentacleDiagnostics.Initialize();</c>
///
/// Harmony patches are registered separately via <c>PatchType(typeof(TentacleDiagnosticsLifecyclePatches))</c>.
/// The monitor and patches are no-ops while <see cref="TentacleDiagnosticsConfig.Enable"/> is false,
/// so this module costs effectively zero when not in use.
/// </summary>
internal static class TentacleDiagnostics
{
    public static void Initialize()
    {
        // Always create the monitor host; it self-gates per-frame by config flag so toggling
        // the JSON does not require a game restart.
        TentacleHSceneMonitor.Ensure();
        Plugin.Log?.LogInfo("[TentacleDiag] module initialized (enable via HellGateJson/Diagnostics/TentacleDiagnostics.json)");
    }
}

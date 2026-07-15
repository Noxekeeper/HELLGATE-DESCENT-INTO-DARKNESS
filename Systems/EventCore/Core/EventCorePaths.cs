using System.IO;
using BepInEx;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Centralized filesystem paths for EventCore content under <c>BepInEx/plugins/HellGateJson/EventCore</c>.
/// </summary>
internal static class EventCorePaths
{
    internal static string JsonRoot { get; private set; } = string.Empty;

    internal static string ManifestFile =>
        Path.Combine(JsonRoot, "eventcore_manifest.json");

    internal static void Initialize()
    {
        JsonRoot = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "EventCore");
    }

    internal static string ResolveRootFile(string fileName) =>
        Path.Combine(JsonRoot, fileName);

    internal static string ResolveEventFile(string relativeName) =>
        Path.Combine(JsonRoot, relativeName);
}

using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Portable content paths: <c>sources/HellGate_sources/VengeanceStrike/</c> next to the game exe.</summary>
internal static class VengeanceStrikePaths
{
    internal static string GetGameRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    internal static string GetHellGateSourcesRoot()
    {
        return Path.GetFullPath(Path.Combine(Path.Combine(GetGameRoot(), "sources"), "HellGate_sources"));
    }

    internal static string GetVengeanceStrikeContentDirectory()
    {
        return Path.GetFullPath(Path.Combine(GetHellGateSourcesRoot(), "VengeanceStrike"));
    }
}

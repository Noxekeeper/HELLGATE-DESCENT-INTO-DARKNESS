using System;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>Resolves <c>sources/HellGate_sources/EventCore/AradiaAva</c> and <c>TouzokuAva</c>.</summary>
internal static class EventCorePortraitPaths
{
    internal const string RelativeEventCoreRoot = "sources/HellGate_sources/EventCore";
    internal const string AradiaFolderName = "AradiaAva";
    internal const string TouzokuFolderName = "TouzokuAva";

    internal static string ResolveAradiaRoot()
    {
        return ResolveCharacterRoot(AradiaFolderName);
    }

    internal static string ResolveTouzokuRoot()
    {
        return ResolveCharacterRoot(TouzokuFolderName);
    }

    private static string ResolveCharacterRoot(string characterFolder)
    {
        string rel = Path.Combine(RelativeEventCoreRoot, characterFolder);
        rel = rel.Replace('/', Path.DirectorySeparatorChar);

        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.Ordinal))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        string pluginSide = Path.Combine(
            Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "NoR_HellGate"),
            rel);

        string[] candidates =
        {
            Path.Combine(gameRoot, rel),
            Path.Combine(Path.GetFullPath(Path.Combine(gameRoot, "..")), rel),
            pluginSide,
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (Directory.Exists(full))
                return full;
        }

        return Path.GetFullPath(Path.Combine(gameRoot, rel));
    }
}

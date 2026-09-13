using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.LostSounds;

/// <summary>Resolves LostSounds asset folder under the game install / sources tree.</summary>
internal static class LostSoundsPaths
{
    internal const string DefaultAssetsRelative = "sources/HellGate_sources/LostSounds";

    internal static string ResolveAssetsDirectory()
    {
        string rel = LostSoundsConfig.AssetsPath != null ? LostSoundsConfig.AssetsPath.Value : null;
        if (string.IsNullOrEmpty(rel) || rel.Trim().Length == 0)
            rel = DefaultAssetsRelative;
        else
            rel = rel.Trim();

        return ResolveRelativeDirectory(rel);
    }

    private static string ResolveRelativeDirectory(string rel)
    {
        rel = rel.Replace('/', Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new List<string>(8);
        AddCandidate(candidates, Path.Combine(gameRoot, rel));
        AddCandidate(candidates, Path.Combine(Path.GetFullPath(Path.Combine(gameRoot, "..")), rel));
        AddCandidate(candidates, Path.Combine(
            Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "NoR_HellGate"),
            rel));

        for (int i = 0; i < candidates.Count; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (Directory.Exists(full))
                return full;
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static void AddCandidate(List<string> list, string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        string full = Path.GetFullPath(path);
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], full, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        list.Add(full);
    }
}

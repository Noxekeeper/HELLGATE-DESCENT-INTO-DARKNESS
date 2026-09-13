using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Resolves fatality clip folders relative to the game install root.</summary>
internal static class EnemyFatalityPaths
{
    internal static string ResolveClipDirectory(IEnemyFatalityProfile profile)
    {
        if (profile == null)
            return null;

        return ResolveRelativeDirectory(profile.ClipPathRelative);
    }

    internal static string ResolveRelativeDirectory(string rel)
    {
        if (string.IsNullOrEmpty(rel))
            return null;

        rel = rel.Replace('/', Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new List<string>(12);
        AddCandidate(candidates, Path.Combine(gameRoot, rel));

        // Walk parents — exe may live under a nested folder while sources/ sits at install root.
        string walk = gameRoot;
        for (int up = 0; up < 4; up++)
        {
            string parent = Path.GetFullPath(Path.Combine(walk, ".."));
            if (string.Equals(parent, walk, System.StringComparison.OrdinalIgnoreCase))
                break;
            AddCandidate(candidates, Path.Combine(parent, rel));
            walk = parent;
        }

        AddCandidate(candidates, Path.Combine(
            Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "NoR_HellGate"),
            rel));
        AddCandidate(candidates, Path.Combine(
            Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"),
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

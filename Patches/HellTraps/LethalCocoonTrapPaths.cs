using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Portable paths under <c>sources/HellGate_sources/CustomDeath/WebSpike_Death</c>.</summary>
internal static class LethalCocoonTrapPaths
{
    internal const string TemplateKey = "lethal_cocoontrap";

    /// <summary>Spawn recorder alias (mixed case).</summary>
    internal const string LegacyTemplateKeyAlias = "Lethal_cocoontrap";

    internal const string DefaultDeathClipRelative =
        "sources/HellGate_sources/CustomDeath/WebSpike_Death";

    internal static bool IsLethalCocoonTrapKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        string normalized = key.Replace("(Clone)", string.Empty).Trim().ToLowerInvariant();
        return normalized == TemplateKey || normalized == LegacyTemplateKeyAlias.ToLowerInvariant();
    }

    internal static string ResolveDeathClipDirectory(string configuredRelativePath)
    {
        string rel = configuredRelativePath;
        if (string.IsNullOrEmpty(rel) || rel.Trim().Length == 0)
            rel = DefaultDeathClipRelative;
        else
            rel = rel.Trim();

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

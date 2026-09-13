using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>Resolves DeadArmor asset folders under the game install / sources tree.</summary>
internal static class DeadArmorPaths
{
    internal const string DefaultPhysicalRelative = "sources/HellGate_sources/DeadArmor/PhisicDamage";
    internal const string DefaultMagicRelative = "sources/HellGate_sources/DeadArmor/MagicDamage";
    internal const string DefaultSoundsRelative = "sources/HellGate_sources/DeadArmor/DeathSounds";

    internal static string ResolvePhysicalClipDirectory()
    {
        return ResolveConfiguredOrDefault(
            DeadArmorConfig.PhysicalClipPath != null ? DeadArmorConfig.PhysicalClipPath.Value : null,
            DefaultPhysicalRelative);
    }

    internal static string ResolveMagicClipDirectory()
    {
        return ResolveConfiguredOrDefault(
            DeadArmorConfig.MagicClipPath != null ? DeadArmorConfig.MagicClipPath.Value : null,
            DefaultMagicRelative);
    }

    internal static string ResolveDeathSoundsDirectory()
    {
        return ResolveConfiguredOrDefault(
            DeadArmorConfig.DeathSoundsPath != null ? DeadArmorConfig.DeathSoundsPath.Value : null,
            DefaultSoundsRelative);
    }

    private static string ResolveConfiguredOrDefault(string configured, string defaultRelative)
    {
        string rel = configured;
        if (string.IsNullOrEmpty(rel) || rel.Trim().Length == 0)
            rel = defaultRelative;
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

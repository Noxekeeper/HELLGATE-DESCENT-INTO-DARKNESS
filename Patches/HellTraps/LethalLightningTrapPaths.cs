using System.Collections.Generic;
using System.IO;
using NoREroMod.Systems.Spawn;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Portable paths under <c>sources/HellGate_sources/CustomDeath/LightningFatalDead</c>.</summary>
internal static class LethalLightningTrapPaths
{
    internal const string TemplateKey = "lightningTrap_button";

    /// <summary>Legacy typo alias (Lighting).</summary>
    internal const string LegacyTemplateKeyAlias = "lightingTrap_button";

    internal const string DefaultDeathClipRelative =
        "sources/HellGate_sources/CustomDeath/LightningFatalDead";

    internal static bool IsLethalLightningTrapKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return SpawnTemplateCatalog.TemplateKeysMatch(key, TemplateKey) ||
               SpawnTemplateCatalog.TemplateKeysMatch(key, LegacyTemplateKeyAlias) ||
               SpawnTemplateCatalog.TemplateKeysMatch(key, "lethallightningbutton");
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

        // Legacy typo folder name (pre-rename).
        string legacyRel = rel.Replace("LightningFatalDead", "LightingFatalDead");
        if (!string.Equals(legacyRel, rel, System.StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate(candidates, Path.Combine(gameRoot, legacyRel));
            AddCandidate(candidates, Path.Combine(Path.GetFullPath(Path.Combine(gameRoot, "..")), legacyRel));
        }

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

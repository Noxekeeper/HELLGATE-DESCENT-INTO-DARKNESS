using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Pre-cache keys listed in HellGateSpawnPoint/SPAWN_TEMPLATE_WHITELIST.txt via Resources scan.
/// Scene preload was removed — it broke Gametitle; use SpawnTemplateDiskCache instead.
/// </summary>
internal static class SpawnTemplateWhitelist
{
    internal sealed class Entry
    {
        public string Key = string.Empty;
        public string SceneName = string.Empty;
    }

    private static readonly List<Entry> entries = new List<Entry>();
    private static ConfigEntry<bool> enabledConfig;

    internal static void BindConfig(Plugin plugin)
    {
        enabledConfig = plugin.Config.Bind(
            "SpawnTemplates",
            "EnableWhitelist",
            true,
            "Pre-cache keys from SPAWN_TEMPLATE_WHITELIST.txt via Resources scan (scene keys are saved to disk cache when visited).");
    }

    internal static void ReloadAndCache(Plugin plugin)
    {
        if (enabledConfig != null && !enabledConfig.Value)
            return;

        LoadFile();
        SpawnTemplateCatalog.CacheWhitelistedKeysFromResources(entries);
    }

    private static void LoadFile()
    {
        entries.Clear();

        try
        {
            string path = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
            path = Path.Combine(path, "SPAWN_TEMPLATE_WHITELIST.txt");
            if (!File.Exists(path))
                return;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int at = line.IndexOf('@');
                Entry entry = new Entry();
                if (at >= 0)
                {
                    entry.Key = line.Substring(0, at).Trim();
                    entry.SceneName = line.Substring(at + 1).Trim();
                }
                else
                {
                    entry.Key = line;
                }

                if (!string.IsNullOrEmpty(entry.Key))
                    entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN WHITELIST] Failed to read whitelist: {ex.Message}");
        }
    }
}

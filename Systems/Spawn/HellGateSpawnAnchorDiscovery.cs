using System;
using System.Collections.Generic;
using System.IO;
using NoREroMod;

namespace NoREroMod.Systems.Spawn;

/// <summary>Builds a registry entry from a parsed spawn anchor line (avoids <c>System.Func</c> name clash with game assemblies).</summary>
internal delegate TEntry SpawnAnchorEntryFactory<TEntry>(
    string anchorId,
    string eventFolder,
    string sceneJoined,
    float anchorX,
    float anchorY);

/// <summary>
/// Scans spawn point text files for <c>REINFORCEMENT</c> / <c>EVENTTRAP</c> anchor lines.
/// </summary>
internal static class HellGateSpawnAnchorDiscovery
{
    internal static List<TEntry> Scan<TEntry>(
        string spawnPointDirectory,
        string anchorCommand,
        string logTag,
        string[] allowedFolders,
        string registrySceneExtra,
        SpawnAnchorEntryFactory<TEntry> createEntry)
    {
        var results = new List<TEntry>();
        if (string.IsNullOrEmpty(spawnPointDirectory) || !Directory.Exists(spawnPointDirectory))
            return results;

        if (createEntry == null)
            return results;

        try
        {
            string[] files = Directory.GetFiles(spawnPointDirectory, "*.txt", SearchOption.TopDirectoryOnly);
            for (int fi = 0; fi < files.Length; fi++)
            {
                string path = files[fi];
                string fileName = Path.GetFileName(path);
                if (!fileName.StartsWith("HellGateSpawn_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!HellGateSpawnSceneHints.SpawnFileSceneHints.TryGetValue(fileName, out string[] sceneHints))
                {
                    Plugin.Log?.LogInfo(
                        $"{logTag} Spawn discovery: '{fileName}' is not in the scene map — add it to {nameof(HellGateSpawnSceneHints)} if you use {anchorCommand} lines there.");
                    continue;
                }

                string sceneJoined = HellGateSpawnSceneHints.JoinSceneHints(sceneHints);
                string extra = registrySceneExtra != null ? registrySceneExtra.Trim() : string.Empty;
                if (extra.Length > 0)
                {
                    if (sceneJoined.Length > 0)
                        sceneJoined += ";" + extra;
                    else
                        sceneJoined = extra;
                }

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(path);
                }
                catch
                {
                    continue;
                }

                for (int li = 0; li < lines.Length; li++)
                {
                    string trimmed = lines[li].Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#')
                        continue;

                    if (!HellGateSpawnLineFormat.TryParseEventAnchorLine(
                            anchorCommand,
                            trimmed,
                            out string anchorId,
                            out string eventFolder,
                            out float ax,
                            out float ay))
                        continue;

                    if (!HellGateSpawnSceneHints.IsAllowedEventFolder(eventFolder, allowedFolders))
                        continue;

                    results.Add(createEntry(anchorId, eventFolder, sceneJoined, ax, ay));
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"{logTag} Spawn discovery failed: {ex.Message}");
        }

        return results;
    }
}

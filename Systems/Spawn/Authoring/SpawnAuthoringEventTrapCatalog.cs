using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.EventCore.EventTrap;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>F11 EventTrap picker: pack folders from <c>event_trap_registry.json</c>.</summary>
internal static class SpawnAuthoringEventTrapCatalog
{
    private static readonly string[] FallbackFolders =
    {
        "etrap_touzoku_touzokuaxe",
        "etrap_undead",
        "etrap_dorei",
        "etrap_goblin",
        "etrap_kakasi",
        "etrap_mafiabosscustom",
        "etrap_mafia",
        "etrap_mutude",
        "etrap_prisonofficer",
        "etrap_slaughterer",
        "etrap_vagrant",
        "etrap_bigonibrother",
        "etrap_angelstatue"
    };

    private static string[] cached = new string[0];
    private static float nextRebuildUnscaledTime;

    internal static string[] GetKeys()
    {
        if (cached.Length == 0 || Time.unscaledTime >= nextRebuildUnscaledTime)
            Rebuild();
        return cached;
    }

    internal static void Invalidate()
    {
        cached = new string[0];
        nextRebuildUnscaledTime = 0f;
    }

    private static void Rebuild()
    {
        nextRebuildUnscaledTime = Time.unscaledTime + 4f;
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] fromRegistry = TryReadRegistryFolders();
        string[] source = fromRegistry != null && fromRegistry.Length > 0
            ? fromRegistry
            : FallbackFolders;

        for (int i = 0; i < source.Length; i++)
        {
            string folder = source[i] != null ? source[i].Trim() : string.Empty;
            if (string.IsNullOrEmpty(folder))
                continue;
            if (!HellGateSpawnLineFormat.IsValidEventFolderToken(folder))
                continue;
            if (!seen.Add(folder))
                continue;
            list.Add(folder);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        cached = list.ToArray();
    }

    private static string[] TryReadRegistryFolders()
    {
        try
        {
            string path = ResolveRegistryPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<EventTrapRegistryFile>(json);
            if (file == null || file.eventFoldersAllowed == null || file.eventFoldersAllowed.Length == 0)
                return null;

            return file.eventFoldersAllowed;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] EventTrap registry read failed: " + ex.Message);
            return null;
        }
    }

    private static string ResolveRegistryPath()
    {
        if (!string.IsNullOrEmpty(EventCorePaths.JsonRoot))
            return EventCorePaths.ResolveRootFile("event_trap_registry.json");

        string root = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "EventCore");
        return Path.Combine(root, "event_trap_registry.json");
    }
}

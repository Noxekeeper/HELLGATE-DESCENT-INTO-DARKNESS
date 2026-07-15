using System;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Reads <c>TRAP,Key,X,Y,Count</c> / <c>OBJECT,Key,X,Y,Count</c> / <c>SPAWN,Trap|Object,Key,X,Y,Count</c> lines from HellGate spawn configs so other systems
/// (e.g. EventTrap) can reuse the same coordinates as <see cref="SpawnConfigExecutor"/> without duplicating numbers in JSON.
/// </summary>
internal static class SpawnTrapAnchorLookup
{
    /// <summary>
    /// Scans <paramref name="absoluteSpawnFilePath"/> top-to-bottom; returns the first template trap line whose key matches <paramref name="trapKey"/> (ordinal case-insensitive).
    /// </summary>
    internal static bool TryGetFirstTrapAnchor(string absoluteSpawnFilePath, string trapKey, out Vector2 anchor)
    {
        anchor = default;
        if (string.IsNullOrEmpty(absoluteSpawnFilePath) || !File.Exists(absoluteSpawnFilePath))
            return false;
        if (string.IsNullOrEmpty(trapKey))
            return false;

        string want = trapKey.Trim();
        if (want.Length == 0)
            return false;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(absoluteSpawnFilePath);
        }
        catch
        {
            return false;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
                continue;

            if (TryParseTrapLine(trimmed, want, out anchor))
                return true;
        }

        return false;
    }

    private static bool TryParseTrapLine(string trimmed, string wantKey, out Vector2 anchor)
    {
        anchor = default;
        string[] parts = trimmed.Split(',');
        if (parts.Length < 4)
            return false;

        string cmd = parts[0].Trim();
        if (HellGateSpawnLineFormat.IsTrapShortcut(cmd) || HellGateSpawnLineFormat.IsObjectShortcut(cmd))
        {
            if (parts.Length < 5)
                return false;
            string key = parts[1].Trim();
            if (!string.Equals(key, wantKey, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!float.TryParse(parts[2].Trim(), out float x))
                return false;
            if (!float.TryParse(parts[3].Trim(), out float y))
                return false;
            anchor = new Vector2(x, y);
            return true;
        }

        // SPAWN,Trap|Object,Key,X,Y,Count[,Description]
        if (parts.Length >= 6 &&
            string.Equals(cmd, "SPAWN", StringComparison.OrdinalIgnoreCase) &&
            (HellGateSpawnLineFormat.IsTrapCategory(parts[1].Trim()) ||
             HellGateSpawnLineFormat.IsSpawnObjectTemplateCategory(parts[1].Trim())))
        {
            string key = parts[2].Trim();
            if (!string.Equals(key, wantKey, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!float.TryParse(parts[3].Trim(), out float x))
                return false;
            if (!float.TryParse(parts[4].Trim(), out float y))
                return false;
            anchor = new Vector2(x, y);
            return true;
        }

        return false;
    }
}

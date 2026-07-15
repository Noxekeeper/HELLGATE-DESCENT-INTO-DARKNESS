using System;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Shared parsing helpers for HellGate spawn point text lines (TRAP / OBJECT / DECOR / SPAWN,Trap|Object / EVENTTRAP).
/// </summary>
internal static class HellGateSpawnLineFormat
{
    internal static bool IsIgnorableConfigLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return true;

        string trimmed = line.Trim();
        if (trimmed.Length == 0)
            return true;
        if (trimmed.StartsWith("#"))
            return true;

        // Typo: Cyrillic yo + '#' at line start — treat as comment, not spawn data.
        int hash = trimmed.IndexOf('#');
        return hash >= 0 && hash <= 1;
    }

    internal static bool IsTrapShortcut(string command)
    {
        return string.Equals(command, "TRAP", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "TRAPS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shortcut lines <c>OBJECT,Key,X,Y,Count</c> — same scene-template catalog as <see cref="IsTrapShortcut"/>, different log semantics.</summary>
    internal static bool IsObjectShortcut(string command)
    {
        return string.Equals(command, "OBJECT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "OBJECTS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shortcut <c>DECOR,Key,X,Y,Count</c> — corpses/scene props from decor catalog (same cache as OBJECT).</summary>
    internal static bool IsDecorShortcut(string command)
    {
        return string.Equals(command, "DECOR", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "DECORATION", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "DECORATIONS", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "SCENEPROP", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "SCENEPROPS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shortcut <c>HOSTAGE,Key,X,Y,Count</c> — vanilla rescue prefab from visited scenes (SpawnSlave root).</summary>
    internal static bool IsHostageShortcut(string command)
    {
        return string.Equals(command, "HOSTAGE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "HOSTAGES", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "SLAVE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "RESCUE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Optional 6th+ fields on TRAP/HOSTAGE lines: flip, mirror, -1, left / rot90, rot180, rot270.</summary>
    internal static bool TryParseFlipField(string token, out bool flipX)
    {
        return SpawnFlipUtility.TryParseFlipToken(token, out flipX);
    }

    /// <summary>Parse optional trailing tokens after count (flip + Z rotation + depth).</summary>
    internal static void ParseOptionalPlacementFields(
        string[] parts,
        int firstOptionalIndex,
        out bool flipX,
        out float rotationZ,
        out SpawnDepthSettings depth)
    {
        flipX = false;
        rotationZ = 0f;
        depth = SpawnDepthSettings.Empty;
        if (parts == null || firstOptionalIndex >= parts.Length)
            return;

        for (int i = firstOptionalIndex; i < parts.Length; i++)
        {
            string token = parts[i]?.Trim();
            if (string.IsNullOrEmpty(token))
                continue;

            if (SpawnFlipUtility.TryParseFlipToken(token, out bool flip))
                flipX = flip;
            else if (SpawnRotationUtility.TryParseRotationToken(token, out float rot))
                rotationZ = rot;
            else if (SpawnDepthUtility.TryParseDepthToken(token, out SpawnDepthSettings tokenDepth))
                depth.Merge(in tokenDepth);
        }
    }

    /// <summary>Backward-compatible wrapper (depth offsets default to 0).</summary>
    internal static void ParseOptionalOrientationFields(
        string[] parts,
        int firstOptionalIndex,
        out bool flipX,
        out float rotationZ)
    {
        ParseOptionalPlacementFields(parts, firstOptionalIndex, out flipX, out rotationZ, out _);
    }

    /// <summary>Walk backward from end of line; orientation/depth tokens only (never consumes numeric count).</summary>
    internal static int ResolveTrailingCountIndex(
        string[] parts,
        int minimumCountIndex,
        out bool flipX,
        out float rotationZ,
        out SpawnDepthSettings depth)
    {
        flipX = false;
        rotationZ = 0f;
        depth = SpawnDepthSettings.Empty;
        if (parts == null || parts.Length <= minimumCountIndex)
            return parts != null ? parts.Length - 1 : 0;

        int index = parts.Length - 1;
        while (index > minimumCountIndex)
        {
            string token = parts[index]?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                index--;
                continue;
            }

            if (int.TryParse(token, out _))
                break;

            if (SpawnFlipUtility.TryParseFlipToken(token, out bool flip))
            {
                flipX = flip;
                index--;
                continue;
            }

            if (SpawnRotationUtility.TryParseRotationToken(token, out float rot))
            {
                rotationZ = rot;
                index--;
                continue;
            }

            if (SpawnDepthUtility.TryParseDepthToken(token, out SpawnDepthSettings tokenDepth))
            {
                depth.Merge(in tokenDepth);
                index--;
                continue;
            }

            break;
        }

        return index;
    }

    /// <summary>Walk backward from end of line; orientation tokens only (never consumes numeric count).</summary>
    internal static int ResolveTrailingCountIndex(
        string[] parts,
        int minimumCountIndex,
        out bool flipX,
        out float rotationZ)
    {
        return ResolveTrailingCountIndex(parts, minimumCountIndex, out flipX, out rotationZ, out _);
    }

    internal static bool IsHostageOrObjectTemplateSemantic(string category)
    {
        if (string.IsNullOrEmpty(category))
            return false;
        string c = category.Trim();
        return IsHostageShortcut(c) || IsSceneObjectTemplateSemantic(c);
    }

    internal static bool IsTrapCategory(string category)
    {
        return string.Equals(category, "Trap", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(category, "TRAP", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(category, "TRAPS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>SPAWN,Object,Key,...</c> (and aliases) — resolves through the same template dictionary as Trap.</summary>
    internal static bool IsSpawnObjectTemplateCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return false;
        string c = category.Trim();
        return string.Equals(c, "Object", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(c, "OBJECT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(c, "OBJECTS", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(c, "SceneObject", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(c, "Prop", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(c, "Props", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether spawn logs should tag this line as a scene-object template (not an enemy prefab).</summary>
    internal static bool IsSceneObjectTemplateSemantic(string category)
    {
        if (string.IsNullOrEmpty(category))
            return false;
        return IsObjectShortcut(category.Trim()) || IsDecorShortcut(category.Trim()) || IsSpawnObjectTemplateCategory(category);
    }

    /// <summary>
    /// <c>COMMAND,anchorId,packFolder,x,y</c> (preferred) or legacy <c>COMMAND,packFolder,x,y</c> (auto anchor id).
    /// <paramref name="expectedCommand"/> is <c>REINFORCEMENT</c> or <c>EVENTTRAP</c>.
    /// </summary>
    internal static bool TryParseEventAnchorLine(
        string expectedCommand,
        string trimmed,
        out string anchorId,
        out string packFolder,
        out float anchorX,
        out float anchorY)
    {
        anchorId = string.Empty;
        packFolder = string.Empty;
        anchorX = 0f;
        anchorY = 0f;

        if (string.IsNullOrEmpty(trimmed) || string.IsNullOrEmpty(expectedCommand))
            return false;

        string[] parts = trimmed.Split(',');
        if (parts.Length < 4)
            return false;

        if (!string.Equals(parts[0].Trim(), expectedCommand, StringComparison.OrdinalIgnoreCase))
            return false;

        if (parts.Length >= 5)
        {
            anchorId = parts[1].Trim();
            packFolder = parts[2].Trim();
            if (!IsValidEventFolderToken(anchorId) || !IsValidEventFolderToken(packFolder))
                return false;
            if (!float.TryParse(parts[3].Trim(), out anchorX))
                return false;
            if (!float.TryParse(parts[4].Trim(), out anchorY))
                return false;
            return true;
        }

        packFolder = parts[1].Trim();
        if (!IsValidEventFolderToken(packFolder))
            return false;
        if (!float.TryParse(parts[2].Trim(), out anchorX))
            return false;
        if (!float.TryParse(parts[3].Trim(), out anchorY))
            return false;

        anchorId = BuildLegacyAnchorId(packFolder, anchorX, anchorY);
        return true;
    }

    internal static string BuildLegacyAnchorId(string packFolder, float anchorX, float anchorY)
    {
        string fx = anchorX.ToString("F2").Replace('.', 'p').Replace('-', 'm');
        string fy = anchorY.ToString("F2").Replace('.', 'p').Replace('-', 'm');
        return packFolder + "_" + fx + "_" + fy;
    }

    /// <summary>Valid event folder token: <c>[a-zA-Z_][a-zA-Z0-9_]*</c> (no Regex — safe on Unity 5.6 Mono).</summary>
    internal static bool IsValidEventFolderToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        token = token.Trim();
        if (token.Length == 0)
            return false;

        char c0 = token[0];
        if (!char.IsLetter(c0) && c0 != '_')
            return false;

        for (int i = 1; i < token.Length; i++)
        {
            char c = token[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }
}

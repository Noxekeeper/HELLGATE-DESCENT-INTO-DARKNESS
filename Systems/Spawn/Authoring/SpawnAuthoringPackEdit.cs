using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>Rewrite / delete a single line in a HellGate spawn pack by 0-based line index.</summary>
internal static class SpawnAuthoringPackEdit
{
    private const float CoordMatchTolerance = 0.55f;

    internal static readonly string[] FactionTokens =
    {
        "",
        "bandits",
        "bandits_inquisition",
        "bandits_mafia",
        "bandits_demons",
        "church",
        "demons",
        "mafia",
        "undead",
        "monsters",
        "witch",
        "eventcore_encounter"
    };

    internal static string BuildEnemyLine(
        float x,
        float y,
        string enemyKey,
        string factionIdRaw,
        float chance,
        int count,
        bool flipX,
        bool forceElite = false,
        string eventCoreEventId = null,
        float eventCoreChance = 1f)
    {
        if (count < 1)
            count = 1;

        string keyPart = enemyKey ?? string.Empty;
        if (!string.IsNullOrEmpty(factionIdRaw))
            keyPart = keyPart + "|faction=" + factionIdRaw.Trim();
        if (forceElite)
            keyPart = keyPart + "|elite=1";
        if (!string.IsNullOrEmpty(eventCoreEventId))
        {
            keyPart = keyPart + "|ec_event=" + eventCoreEventId.Trim();
            if (eventCoreChance > 0f && eventCoreChance < 0.999f)
                keyPart = keyPart + "|ec_chance=" +
                          eventCoreChance.ToString("0.##", CultureInfo.InvariantCulture);
        }

        string fx = x.ToString("F2", CultureInfo.InvariantCulture);
        string fy = y.ToString("F2", CultureInfo.InvariantCulture);
        string countStr = count.ToString(CultureInfo.InvariantCulture);

        bool isRandom = chance > 0f && chance < 0.999f;
        string line;
        if (isRandom)
        {
            string ch = chance.ToString("0.##", CultureInfo.InvariantCulture);
            line = "RANDOM," + ch + "," + fx + "," + fy + "," + keyPart + "," + countStr;
        }
        else
        {
            line = fx + "," + fy + "," + keyPart + "," + countStr;
        }

        if (flipX)
            line = line + ",flip";

        return line;
    }

    /// <summary>True for TRAP / OBJECT / DECOR / HOSTAGE / SPAWN template pack lines.</summary>
    internal static bool IsTemplateAuthoringLine(string line)
    {
        if (IsBlank(line))
            return false;
        string raw = line.Trim();
        if (raw.StartsWith("#", StringComparison.Ordinal) || raw.StartsWith("//", StringComparison.Ordinal))
            return false;
        int comma = raw.IndexOf(',');
        string cmd = comma >= 0 ? raw.Substring(0, comma).Trim() : raw;
        return HellGateSpawnLineFormat.IsTrapShortcut(cmd) ||
               HellGateSpawnLineFormat.IsObjectShortcut(cmd) ||
               HellGateSpawnLineFormat.IsDecorShortcut(cmd) ||
               HellGateSpawnLineFormat.IsHostageShortcut(cmd) ||
               string.Equals(cmd, "RANDOM_HOSTAGE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cmd, "SPAWN", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cmd, "TEMPLATE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TRAP / OBJECT / DECOR / HOSTAGE pack lines — trigger-body prefabs for F11 pick.
    /// SPAWN / TEMPLATE enemy lines stay tight (solid collider / sprite).
    /// </summary>
    internal static bool IsPropTemplateAuthoringLine(string line)
    {
        if (IsBlank(line))
            return false;
        string raw = line.Trim();
        if (raw.StartsWith("#", StringComparison.Ordinal) || raw.StartsWith("//", StringComparison.Ordinal))
            return false;
        int comma = raw.IndexOf(',');
        string cmd = comma >= 0 ? raw.Substring(0, comma).Trim() : raw;
        return HellGateSpawnLineFormat.IsTrapShortcut(cmd) ||
               HellGateSpawnLineFormat.IsObjectShortcut(cmd) ||
               HellGateSpawnLineFormat.IsDecorShortcut(cmd) ||
               HellGateSpawnLineFormat.IsHostageShortcut(cmd) ||
               string.Equals(cmd, "RANDOM_HOSTAGE", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsGoldLine(string line)
    {
        if (IsBlank(line))
            return false;
        string raw = line.Trim();
        if (raw.StartsWith("GOLD,", StringComparison.OrdinalIgnoreCase))
            return true;
        return raw.IndexOf("gold=", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool IsGoldManagedInstance(SpawnManagedInstance managed)
    {
        if (managed == null)
            return false;
        if (managed.GetComponentInChildren<NoREroMod.Systems.Economy.GoldPickup>(true) != null)
            return true;
        return IsGoldLine(managed.AuthoringSourceLineRaw);
    }

    internal static bool LooksLikeGoldRangeToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;
        string t = SpawnAuthoringGoldCatalog.NormalizeRangeToken(token);
        if (t.Length == 0)
            return false;
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (c != '-' && !char.IsDigit(c))
                return false;
        }

        return char.IsDigit(t[0]);
    }

    internal static bool IsAnchorLine(string line)
    {
        if (IsBlank(line))
            return false;
        string raw = line.Trim();
        return raw.StartsWith("EVENTTRAP,", StringComparison.OrdinalIgnoreCase) ||
               raw.StartsWith("REINFORCEMENT,", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAnchorManagedInstance(SpawnManagedInstance managed)
    {
        if (managed == null)
            return false;
        if (managed.GetComponent<SpawnAuthoringAnchorMarker>() != null)
            return true;
        return IsAnchorLine(managed.AuthoringSourceLineRaw);
    }

    /// <summary>True when the live instance is a template trap/prop (even if pack line was wrongly saved as enemy XY).</summary>
    internal static bool IsTrapManagedInstance(SpawnManagedInstance managed)
    {
        if (managed == null)
            return false;
        if (IsGoldManagedInstance(managed))
            return false;
        if (IsAnchorManagedInstance(managed))
            return false;
        if (managed.AuthoringIsTemplate)
            return true;
        if (IsTemplateAuthoringLine(managed.AuthoringSourceLineRaw))
            return true;
        if (managed.GetComponentInChildren<Trapdata>(true) != null)
            return true;
        string key = managed.AuthoringEnemyKey;
        if (!string.IsNullOrEmpty(key) &&
            SpawnTemplateCatalog.TryGetTrapTemplate(key, out GameObject tmpl) &&
            tmpl != null)
            return true;
        return false;
    }

    /// <summary>Prefer real trap key; avoid literal command tokens like "trap"/"TRAP" from bad pack lines.</summary>
    internal static string ResolveTrapAuthoringKey(SpawnManagedInstance managed)
    {
        if (managed == null)
            return string.Empty;

        string key = managed.AuthoringEnemyKey ?? string.Empty;
        if (IsUsableTrapKey(key))
            return CanonicalizeTrapKey(key);

        if (TryExtractKeyAndCoords(managed.AuthoringSourceLineRaw, out string fromLine, out _, out _) &&
            IsUsableTrapKey(fromLine))
            return CanonicalizeTrapKey(fromLine);

        string fromName = CleanSpawnObjectName(managed.gameObject != null ? managed.gameObject.name : null);
        if (IsUsableTrapKey(fromName))
            return CanonicalizeTrapKey(fromName);

        if (!string.IsNullOrEmpty(key))
            return CanonicalizeTrapKey(key);
        return CanonicalizeTrapKey(fromName);
    }

    internal static bool IsUsableTrapKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        string k = key.Trim();

        // Valid spawn aliases that collide with TRAP command names (trap / trapnormal / trap_hari).
        if (SpawnSpikeKeys.IsTrapHariKey(k) || SpawnSpikeKeys.IsSpikeLikeKey(k))
            return true;

        if (HellGateSpawnLineFormat.IsTrapShortcut(k) ||
            HellGateSpawnLineFormat.IsObjectShortcut(k) ||
            HellGateSpawnLineFormat.IsDecorShortcut(k) ||
            HellGateSpawnLineFormat.IsHostageShortcut(k) ||
            string.Equals(k, "SPAWN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(k, "TEMPLATE", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>
    /// Keep the selected catalog key. Only fold scene-name aliases
    /// (trap / trap_hari / Trap_hari) into <c>trapnormal</c> — that is what
    /// existing working packs already use.
    /// </summary>
    internal static string CanonicalizeTrapKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;
        string k = key.Trim();
        if (SpawnSpikeKeys.IsTrapHariKey(k))
            return "trapnormal";
        return k;
    }

    internal static bool KeysMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            return true;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        string goldA = SpawnAuthoringGoldCatalog.NormalizeRangeToken(a);
        string goldB = SpawnAuthoringGoldCatalog.NormalizeRangeToken(b);
        if (LooksLikeGoldRangeToken(goldA) && LooksLikeGoldRangeToken(goldB))
            return string.Equals(goldA, goldB, StringComparison.OrdinalIgnoreCase);

        return string.Equals(
            CanonicalizeTrapKey(a),
            CanonicalizeTrapKey(b),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanSpawnObjectName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        string cleaned = name.Replace("(Clone)", string.Empty).Trim();
        int paren = cleaned.IndexOf('(');
        if (paren > 0)
            cleaned = cleaned.Substring(0, paren).Trim();
        return cleaned;
    }

    internal static string ExtractFactionFromSourceLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        const string prefix = "|faction=";
        int start = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;

        start += prefix.Length;
        int end = start;
        while (end < line.Length)
        {
            char c = line[end];
            if (c == ',' || c == '|' || c == ' ')
                break;
            end++;
        }

        return end > start ? line.Substring(start, end - start).Trim() : string.Empty;
    }

    /// <summary>Read <c>|ec_event=</c> / <c>|ec=</c> and optional <c>|ec_chance=</c> from a pack line.</summary>
    internal static void ExtractEventCoreFromSourceLine(string line, out string eventId, out float chance)
    {
        eventId = string.Empty;
        chance = 1f;
        if (string.IsNullOrEmpty(line))
            return;

        string poolCsv = null;
        string[] chunks = line.Split('|');
        for (int i = 0; i < chunks.Length; i++)
        {
            string chunk = chunks[i].Trim();
            if (chunk.StartsWith("ec_event=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("ec_event=".Length).Trim();
                int cut = value.IndexOfAny(new[] { ',', ' ' });
                if (cut >= 0)
                    value = value.Substring(0, cut).Trim();
                if (!string.IsNullOrEmpty(value))
                    eventId = value;
            }
            else if (chunk.StartsWith("ec=", StringComparison.OrdinalIgnoreCase) &&
                     !chunk.StartsWith("ec_event=", StringComparison.OrdinalIgnoreCase) &&
                     !chunk.StartsWith("ec_chance=", StringComparison.OrdinalIgnoreCase) &&
                     !chunk.StartsWith("ec_p=", StringComparison.OrdinalIgnoreCase) &&
                     !chunk.StartsWith("ec_pool=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("ec=".Length).Trim();
                int cut = value.IndexOfAny(new[] { ',', ' ' });
                if (cut >= 0)
                    value = value.Substring(0, cut).Trim();
                if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(eventId))
                    eventId = value;
            }
            else if (chunk.StartsWith("ec_pool=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("ec_pool=".Length).Trim();
                int cut = value.IndexOfAny(new[] { ' ' });
                if (cut >= 0)
                    value = value.Substring(0, cut).Trim();
                if (!string.IsNullOrEmpty(value))
                    poolCsv = value;
            }
            else if (chunk.StartsWith("ec_chance=", StringComparison.OrdinalIgnoreCase) ||
                     chunk.StartsWith("ec_p=", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = chunk.StartsWith("ec_chance=", StringComparison.OrdinalIgnoreCase)
                    ? "ec_chance="
                    : "ec_p=";
                string value = chunk.Substring(prefix.Length).Trim();
                int cut = value.IndexOfAny(new[] { ',', ' ', '|' });
                if (cut >= 0)
                    value = value.Substring(0, cut).Trim();
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    chance = Mathf.Clamp01(parsed);
            }
        }

        if (string.IsNullOrEmpty(eventId) && !string.IsNullOrEmpty(poolCsv))
        {
            string[] ids = poolCsv.Split(',');
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i].Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    eventId = id;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(eventId))
            chance = 1f;
        else if (chance <= 0f)
            chance = 0.01f;
    }

    internal static string FormatRotationToken(float rotationZ)
    {
        float angle = SpawnRotationUtility.NormalizeAngle(rotationZ);
        if (Mathf.Abs(angle) < 0.01f)
            return null;
        if (Mathf.Abs(angle - 90f) < 0.01f)
            return "rot90";
        if (Mathf.Abs(angle - 180f) < 0.01f)
            return "rot180";
        if (Mathf.Abs(angle - 270f) < 0.01f)
            return "rot270";
        return "rot" + Mathf.RoundToInt(angle).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Write pack token <c>sort+N</c> / <c>sort-N</c> (relative SortingOrder delta).</summary>
    internal static string FormatSortToken(int sortOffset)
    {
        if (sortOffset == 0)
            return null;
        if (sortOffset > 0)
            return "sort+" + sortOffset.ToString(CultureInfo.InvariantCulture);
        return "sort" + sortOffset.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Authoring → runtime depth: only <c>sort±N</c> (not near/far player-layer presets).</summary>
    internal static SpawnDepthSettings ToDepthSettings(int sortOffset)
    {
        SpawnDepthSettings settings = SpawnDepthSettings.Empty;
        if (sortOffset != 0)
            settings.SortingOrderOffset = sortOffset;
        return settings;
    }

    /// <summary>Read SortingOrder delta from parsed depth (sort± / near→+10 / far→−10).</summary>
    internal static int SortOffsetFromSettings(in SpawnDepthSettings depth)
    {
        return depth.SortingOrderOffset;
    }

    internal const float RotationStepDegrees = 30f;

    internal static float CycleRotationZ(float rotationZ)
    {
        float angle = SpawnRotationUtility.NormalizeAngle(rotationZ);
        int steps = Mathf.RoundToInt(360f / RotationStepDegrees);
        if (steps < 1)
            steps = 12;
        int step = Mathf.RoundToInt(angle / RotationStepDegrees) % steps;
        if (step < 0)
            step += steps;
        return SpawnRotationUtility.NormalizeAngle((step + 1) * RotationStepDegrees);
    }

    /// <summary>
    /// Spike keys: <c>X,Y,Key,Count[,flip][,rot…][,sort±N]</c> — same form as
    /// the working txt packs. Other traps: <c>TRAP,Key,X,Y,Count,...</c>.
    /// </summary>
    internal static string BuildGoldLine(float x, float y, string rangeToken, float chance, int count)
    {
        if (count < 1)
            count = 1;

        string range = SpawnAuthoringGoldCatalog.NormalizeRangeToken(rangeToken);
        if (string.IsNullOrEmpty(range))
            range = "100";

        string fx = x.ToString("F2", CultureInfo.InvariantCulture);
        string fy = y.ToString("F2", CultureInfo.InvariantCulture);
        string countStr = count.ToString(CultureInfo.InvariantCulture);
        bool isRandom = chance > 0f && chance < 0.999f;
        if (isRandom)
        {
            string ch = chance.ToString("0.##", CultureInfo.InvariantCulture);
            return "RANDOM," + ch + "," + fx + "," + fy + ",gold=" + range + "," + countStr;
        }

        return "GOLD," + fx + "," + fy + "," + range + "," + countStr;
    }

    internal static string BuildEventTrapLine(
        float x,
        float y,
        string packFolder,
        string extrasSuffix = null)
    {
        string folder = packFolder != null ? packFolder.Trim() : string.Empty;
        string fx = x.ToString("F2", CultureInfo.InvariantCulture);
        string fy = y.ToString("F2", CultureInfo.InvariantCulture);
        string line = "EVENTTRAP," + folder + "," + fx + "," + fy;
        if (!string.IsNullOrEmpty(extrasSuffix))
            line = line + extrasSuffix;
        return line;
    }

    internal static string BuildTrapLine(
        float x,
        float y,
        string trapKey,
        int count,
        bool flipX,
        float rotationZ = 0f,
        int sortOffset = 0,
        string factionIdRaw = null,
        float chance = 1f)
    {
        if (count < 1)
            count = 1;

        string fx = x.ToString("F2", CultureInfo.InvariantCulture);
        string fy = y.ToString("F2", CultureInfo.InvariantCulture);
        string countStr = count.ToString(CultureInfo.InvariantCulture);
        string key = CanonicalizeTrapKey(trapKey);
        if (!SpawnAuthoringState.KeyAllowsRotation(key))
            rotationZ = 0f;
        if (SpawnAuthoringHostageCatalog.AcceptsFaction(key) && !string.IsNullOrEmpty(factionIdRaw))
            key = key + "|faction=" + factionIdRaw.Trim();
        string line;
        bool hostageRandom = SpawnAuthoringHostageCatalog.IsHostageLineKey(key) &&
                             chance > 0f && chance < 0.999f;
        if (hostageRandom)
        {
            string ch = chance.ToString("0.##", CultureInfo.InvariantCulture);
            line = "RANDOM_HOSTAGE," + ch + "," + key + "," + fx + "," + fy + "," + countStr;
        }
        else if (SpawnSpikeKeys.IsSpikeLikeKey(key))
            line = fx + "," + fy + "," + key + "," + countStr;
        else if (SpawnAuthoringHostageCatalog.IsHostageLineKey(key))
            line = "HOSTAGE," + key + "," + fx + "," + fy + "," + countStr;
        else if (SpawnDecorCatalog.IsKnownDecorKey(key) || SpawnAuthoringHostageCatalog.IsOtherSceneKey(key))
            line = "DECOR," + key + "," + fx + "," + fy + "," + countStr;
        else
            line = "TRAP," + key + "," + fx + "," + fy + "," + countStr;
        if (flipX)
            line = line + ",flip";
        string rot = FormatRotationToken(rotationZ);
        if (!string.IsNullOrEmpty(rot))
            line = line + "," + rot;
        string sort = FormatSortToken(sortOffset);
        if (!string.IsNullOrEmpty(sort))
            line = line + "," + sort;
        return line;
    }

    /// <summary>
    /// Find the real pack line for a managed spawn when AuthoringLineIndex drifted
    /// (deletes / appends / rewrite). Prefers exact raw match, then key+coords.
    /// </summary>
    internal static bool TryResolveLineIndex(
        string packPath,
        int preferredIndex,
        string sourceLineRaw,
        float spawnX,
        float spawnY,
        string enemyKey,
        out int resolvedIndex,
        out string matchedLine,
        out string error)
    {
        resolvedIndex = -1;
        matchedLine = null;
        error = null;

        if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
        {
            error = "Pack file missing.";
            return false;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(packPath);
        }
        catch (Exception ex)
        {
            error = "Read failed: " + ex.Message;
            return false;
        }

        string wantRaw = NormalizeLine(sourceLineRaw);

        // 1) Preferred index still valid + content still matches this spawn.
        if (preferredIndex >= 0 && preferredIndex < lines.Length)
        {
            string at = lines[preferredIndex];
            if (LineLooksLikeSpawn(at) &&
                (LinesEqual(wantRaw, at) || LineMatchesSpawn(at, enemyKey, spawnX, spawnY)))
            {
                resolvedIndex = preferredIndex;
                matchedLine = at;
                return true;
            }
        }

        // 2) Exact stored source line (best after nudge without Save).
        if (!string.IsNullOrEmpty(wantRaw))
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (!LinesEqual(wantRaw, lines[i]))
                    continue;
                resolvedIndex = i;
                matchedLine = lines[i];
                return true;
            }
        }

        // 3) Key + XY near AuthoringSpawn (or Current after nudge).
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!TryExtractKeyAndCoords(lines[i], out string key, out float x, out float y))
                continue;
            if (!KeysMatch(key, enemyKey))
                continue;

            float dx = x - spawnX;
            float dy = y - spawnY;
            float d2 = dx * dx + dy * dy;
            if (d2 > CoordMatchTolerance * CoordMatchTolerance)
                continue;
            if (d2 < bestDist)
            {
                bestDist = d2;
                best = i;
            }
        }

        if (best >= 0)
        {
            resolvedIndex = best;
            matchedLine = lines[best];
            return true;
        }

        // 4) Unique key match in the whole pack (last resort).
        if (!string.IsNullOrEmpty(enemyKey))
        {
            int only = -1;
            int hits = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!TryExtractKeyAndCoords(lines[i], out string key, out _, out _))
                    continue;
                if (!KeysMatch(key, enemyKey))
                    continue;
                hits++;
                only = i;
            }

            if (hits == 1)
            {
                resolvedIndex = only;
                matchedLine = lines[only];
                return true;
            }
        }

        error = "Pack line not found (index stale / coords moved). Re-select after RMB reload.";
        return false;
    }

    internal static bool TryReplaceLinkedLine(
        SpawnManagedInstance managed,
        string newLine,
        out int resolvedIndex,
        out string previousLine,
        out string error)
    {
        resolvedIndex = -1;
        previousLine = null;
        error = null;
        if (managed == null || !managed.HasAuthoringLink)
        {
            error = SpawnAuthoringLoc.T("status.noPackLink");
            return false;
        }

        if (!TryResolveLineIndex(
                managed.AuthoringPackPath,
                managed.AuthoringLineIndex,
                managed.AuthoringSourceLineRaw,
                managed.AuthoringSpawnX,
                managed.AuthoringSpawnY,
                managed.AuthoringEnemyKey,
                out resolvedIndex,
                out previousLine,
                out error))
        {
            Vector3 p = managed.transform.position;
            if (!TryResolveLineIndex(
                    managed.AuthoringPackPath,
                    managed.AuthoringLineIndex,
                    managed.AuthoringSourceLineRaw,
                    p.x,
                    p.y,
                    managed.AuthoringEnemyKey,
                    out resolvedIndex,
                    out previousLine,
                    out error))
                return false;
        }

        if (!TryReplaceLine(managed.AuthoringPackPath, resolvedIndex, newLine, out error))
            return false;

        managed.AuthoringLineIndex = resolvedIndex;
        managed.AuthoringSourceLineRaw = newLine ?? string.Empty;
        return true;
    }

    internal static bool TryDeleteLinkedLine(
        SpawnManagedInstance managed,
        out int resolvedIndex,
        out string removedLine,
        out string error)
    {
        resolvedIndex = -1;
        removedLine = null;
        error = null;
        if (managed == null || !managed.HasAuthoringLink)
        {
            error = SpawnAuthoringLoc.T("status.noPackLink");
            return false;
        }

        string packPath = managed.AuthoringPackPath;
        if (!TryResolveLineIndex(
                packPath,
                managed.AuthoringLineIndex,
                managed.AuthoringSourceLineRaw,
                managed.AuthoringSpawnX,
                managed.AuthoringSpawnY,
                managed.AuthoringEnemyKey,
                out resolvedIndex,
                out removedLine,
                out error))
        {
            // Nudge may have moved AuthoringSpawn away from pack XY — retry with live transform.
            Vector3 p = managed.transform.position;
            if (!TryResolveLineIndex(
                    packPath,
                    managed.AuthoringLineIndex,
                    managed.AuthoringSourceLineRaw,
                    p.x,
                    p.y,
                    managed.AuthoringEnemyKey,
                    out resolvedIndex,
                    out removedLine,
                    out error))
                return false;
        }

        if (!TryDeleteLine(packPath, resolvedIndex, out error))
            return false;

        // Destroy siblings first (same pack line), then shift remaining indices.
        DestroyInstancesForDeletedLine(packPath, resolvedIndex, removedLine);
        ReindexAfterDelete(packPath, resolvedIndex);
        return true;
    }

    /// <summary>Destroy every live instance that pointed at the deleted pack line.</summary>
    internal static void DestroyInstancesForDeletedLine(string packPath, int deletedIndex, string removedLine)
    {
        SpawnManagedInstance[] all = Object.FindObjectsOfType<SpawnManagedInstance>();
        if (all == null || all.Length == 0)
            return;

        string wantRaw = NormalizeLine(removedLine);
        string packName = Path.GetFileName(packPath ?? string.Empty);

        for (int i = 0; i < all.Length; i++)
        {
            SpawnManagedInstance m = all[i];
            if (m == null)
                continue;

            bool samePack =
                string.Equals(m.AuthoringPackPath, packPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(m.AuthoringPackPath ?? string.Empty), packName, StringComparison.OrdinalIgnoreCase);
            if (!samePack)
                continue;

            bool hit =
                (!string.IsNullOrEmpty(wantRaw) && LinesEqual(wantRaw, m.AuthoringSourceLineRaw)) ||
                (m.HasAuthoringLink && m.AuthoringLineIndex == deletedIndex);

            if (!hit)
                continue;

            GameObject go = m.gameObject;
            m.ClearAuthoringLink();
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    internal static void ReindexAfterDelete(string packPath, int deletedIndex)
    {
        if (string.IsNullOrEmpty(packPath) || deletedIndex < 0)
            return;

        SpawnManagedInstance[] all = Object.FindObjectsOfType<SpawnManagedInstance>();
        if (all == null)
            return;

        string packName = Path.GetFileName(packPath);
        for (int i = 0; i < all.Length; i++)
        {
            SpawnManagedInstance m = all[i];
            if (m == null || !m.HasAuthoringLink)
                continue;

            bool samePack =
                string.Equals(m.AuthoringPackPath, packPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(m.AuthoringPackPath ?? string.Empty), packName, StringComparison.OrdinalIgnoreCase);
            if (!samePack)
                continue;

            if (m.AuthoringLineIndex > deletedIndex)
                m.AuthoringLineIndex--;
            else if (m.AuthoringLineIndex == deletedIndex)
            {
                // Orphaned link — about to be destroyed; clear so Save/Delete don't re-hit.
                m.HasAuthoringLink = false;
                m.AuthoringLineIndex = -1;
            }
        }
    }

    internal static bool TryReplaceLine(string packPath, int lineIndex, string newLine, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
        {
            error = "Pack file missing.";
            return false;
        }

        if (lineIndex < 0)
        {
            error = "Invalid line index.";
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(packPath);
            if (lineIndex >= lines.Length)
            {
                error = "Line index out of range (file changed?).";
                return false;
            }

            lines[lineIndex] = newLine ?? string.Empty;
            File.WriteAllLines(packPath, lines, Encoding.UTF8);
            Plugin.Log?.LogInfo(
                "[SPAWN AUTHORING] Replaced line " + (lineIndex + 1) + " in " +
                Path.GetFileName(packPath) + ": " + newLine);
            return true;
        }
        catch (Exception ex)
        {
            error = "Replace failed: " + ex.Message;
            return false;
        }
    }

    internal static bool TryDeleteLine(string packPath, int lineIndex, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
        {
            error = "Pack file missing.";
            return false;
        }

        if (lineIndex < 0)
        {
            error = "Invalid line index.";
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(packPath);
            if (lineIndex >= lines.Length)
            {
                error = "Line index out of range (file changed?).";
                return false;
            }

            var list = new List<string>(lines);
            string removed = list[lineIndex];
            list.RemoveAt(lineIndex);
            File.WriteAllLines(packPath, list.ToArray(), Encoding.UTF8);
            Plugin.Log?.LogInfo(
                "[SPAWN AUTHORING] Deleted line " + (lineIndex + 1) + " from " +
                Path.GetFileName(packPath) + ": " + removed);
            return true;
        }
        catch (Exception ex)
        {
            error = "Delete failed: " + ex.Message;
            return false;
        }
    }

    internal static bool TryInsertLine(string packPath, int lineIndex, string line, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
        {
            error = "Pack file missing.";
            return false;
        }

        try
        {
            var list = new List<string>(File.ReadAllLines(packPath));
            if (lineIndex < 0)
                lineIndex = 0;
            if (lineIndex > list.Count)
                lineIndex = list.Count;
            list.Insert(lineIndex, line ?? string.Empty);
            File.WriteAllLines(packPath, list.ToArray(), Encoding.UTF8);

            // Shift live indices for entries at/after insert point.
            SpawnManagedInstance[] all = Object.FindObjectsOfType<SpawnManagedInstance>();
            if (all != null)
            {
                string packName = Path.GetFileName(packPath);
                for (int i = 0; i < all.Length; i++)
                {
                    SpawnManagedInstance m = all[i];
                    if (m == null || !m.HasAuthoringLink)
                        continue;
                    bool samePack =
                        string.Equals(m.AuthoringPackPath, packPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(m.AuthoringPackPath ?? string.Empty), packName, StringComparison.OrdinalIgnoreCase);
                    if (!samePack)
                        continue;
                    if (m.AuthoringLineIndex >= lineIndex)
                        m.AuthoringLineIndex++;
                }
            }

            Plugin.Log?.LogInfo(
                "[SPAWN AUTHORING] Inserted line " + (lineIndex + 1) + " in " +
                Path.GetFileName(packPath) + ": " + line);
            return true;
        }
        catch (Exception ex)
        {
            error = "Insert failed: " + ex.Message;
            return false;
        }
    }

    /// <summary>Content lines only — trailing blank lines from WriteAllLines must not inflate append index.</summary>
    internal static int CountFileLines(string packPath)
    {
        if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
            return 0;
        try
        {
            string[] lines = File.ReadAllLines(packPath);
            int n = lines.Length;
            while (n > 0 && IsBlank(lines[n - 1]))
                n--;
            return n;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsBlank(string s)
    {
        return string.IsNullOrEmpty(s) || s.Trim().Length == 0;
    }

    private static bool LineLooksLikeSpawn(string line)
    {
        if (IsBlank(line))
            return false;
        string t = line.TrimStart();
        if (t.StartsWith("#", StringComparison.Ordinal) || t.StartsWith("//", StringComparison.Ordinal))
            return false;
        return t.IndexOf(',') >= 0;
    }

    private static string NormalizeLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;
        return line.Trim().Replace("\r", string.Empty);
    }

    private static bool LinesEqual(string a, string b)
    {
        return string.Equals(NormalizeLine(a), NormalizeLine(b), StringComparison.OrdinalIgnoreCase);
    }

    private static bool LineMatchesSpawn(string line, string enemyKey, float spawnX, float spawnY)
    {
        if (!TryExtractKeyAndCoords(line, out string key, out float x, out float y))
            return false;
        if (!string.IsNullOrEmpty(enemyKey) && !KeysMatch(key, enemyKey))
            return false;
        float dx = x - spawnX;
        float dy = y - spawnY;
        return dx * dx + dy * dy <= CoordMatchTolerance * CoordMatchTolerance;
    }

    /// <summary>
    /// Supports static <c>X,Y,Key…,count</c>, <c>RANDOM,chance,X,Y,Key…,count</c>,
    /// and template shortcuts <c>TRAP|OBJECT|DECOR|HOSTAGE,Key,X,Y,Count…</c> /
    /// <c>SPAWN|TEMPLATE,Category,Key,X,Y,Count…</c>.
    /// </summary>
    internal static bool TryExtractKeyAndCoords(string line, out string enemyKey, out float x, out float y)
    {
        enemyKey = null;
        x = 0f;
        y = 0f;
        if (IsBlank(line))
            return false;

        string raw = line.Trim();
        if (raw.StartsWith("#", StringComparison.Ordinal) || raw.StartsWith("//", StringComparison.Ordinal))
            return false;

        string[] parts = raw.Split(',');
        if (parts.Length < 3)
            return false;

        string cmd = parts[0].Trim();
        int keyIdx;
        int xIdx;

        if (HellGateSpawnLineFormat.IsTrapShortcut(cmd) ||
            HellGateSpawnLineFormat.IsObjectShortcut(cmd) ||
            HellGateSpawnLineFormat.IsDecorShortcut(cmd) ||
            HellGateSpawnLineFormat.IsHostageShortcut(cmd))
        {
            if (parts.Length < 4)
                return false;
            keyIdx = 1;
            xIdx = 2;
        }
        else if (string.Equals(cmd, "SPAWN", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "TEMPLATE", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 5)
                return false;
            keyIdx = 2;
            xIdx = 3;
        }
        else if (string.Equals(cmd, "RANDOM_HOSTAGE", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 5)
                return false;
            keyIdx = 2;
            xIdx = 3;
        }
        else if (string.Equals(cmd, "GOLD", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 4)
                return false;
            xIdx = 1;
            keyIdx = 3;
        }
        else if (string.Equals(cmd, "EVENTTRAP", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "REINFORCEMENT", StringComparison.OrdinalIgnoreCase))
        {
            if (!HellGateSpawnLineFormat.TryParseEventAnchorLine(
                    cmd, raw, out _, out enemyKey, out x, out y, out _))
                return false;
            return !string.IsNullOrEmpty(enemyKey);
        }
        else if (cmd.Equals("RANDOM", StringComparison.OrdinalIgnoreCase) ||
                 cmd.Equals("RND", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 5)
                return false;
            xIdx = 2;
            keyIdx = 4;
        }
        else
        {
            xIdx = 0;
            keyIdx = 2;
        }

        if (keyIdx >= parts.Length || xIdx + 1 >= parts.Length)
            return false;

        if (!float.TryParse(parts[xIdx].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
            !float.TryParse(parts[xIdx + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            return false;

        string keyPart = parts[keyIdx].Trim();
        int pipe = keyPart.IndexOf('|');
        if (pipe >= 0)
            keyPart = keyPart.Substring(0, pipe).Trim();
        if (string.IsNullOrEmpty(keyPart))
            return false;

        enemyKey = keyPart;
        return true;
    }

    /// <summary>Rewrite the XY pair in a pack line, keeping command / extras / EventTrap tokens.</summary>
    internal static bool TryRelocateLine(string line, float x, float y, out string relocated)
    {
        relocated = null;
        if (IsBlank(line))
            return false;

        string raw = line.Trim();
        string[] parts = raw.Split(',');
        if (parts.Length < 3)
            return false;

        string cmd = parts[0].Trim();
        int xIdx;
        if (string.Equals(cmd, "EVENTTRAP", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cmd, "REINFORCEMENT", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length >= 5 &&
                HellGateSpawnLineFormat.IsValidEventFolderToken(parts[1].Trim()) &&
                HellGateSpawnLineFormat.IsValidEventFolderToken(parts[2].Trim()))
                xIdx = 3;
            else
                xIdx = 2;
        }
        else if (HellGateSpawnLineFormat.IsTrapShortcut(cmd) ||
                 HellGateSpawnLineFormat.IsObjectShortcut(cmd) ||
                 HellGateSpawnLineFormat.IsDecorShortcut(cmd) ||
                 HellGateSpawnLineFormat.IsHostageShortcut(cmd))
            xIdx = 2;
        else if (string.Equals(cmd, "SPAWN", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "TEMPLATE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "RANDOM_HOSTAGE", StringComparison.OrdinalIgnoreCase))
            xIdx = 3;
        else if (string.Equals(cmd, "GOLD", StringComparison.OrdinalIgnoreCase))
            xIdx = 1;
        else if (string.Equals(cmd, "RANDOM", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "RND", StringComparison.OrdinalIgnoreCase))
            xIdx = 2;
        else
            xIdx = 0;

        if (xIdx + 1 >= parts.Length)
            return false;

        parts[xIdx] = x.ToString("F2", CultureInfo.InvariantCulture);
        parts[xIdx + 1] = y.ToString("F2", CultureInfo.InvariantCulture);
        relocated = string.Join(",", parts);
        return true;
    }

    /// <summary>Read flip / rot / sort± (or near/far → ±10) from a template or enemy pack line.</summary>
    internal static void TryExtractPlacementExtras(
        string line,
        out bool flipX,
        out float rotationZ,
        out int sortOffset)
    {
        flipX = false;
        rotationZ = 0f;
        sortOffset = 0;
        if (IsBlank(line))
            return;

        string[] parts = line.Trim().Split(',');
        if (parts.Length < 2)
            return;

        string cmd = parts[0].Trim();
        int firstOptional;
        if (HellGateSpawnLineFormat.IsTrapShortcut(cmd) ||
            HellGateSpawnLineFormat.IsObjectShortcut(cmd) ||
            HellGateSpawnLineFormat.IsDecorShortcut(cmd) ||
            HellGateSpawnLineFormat.IsHostageShortcut(cmd))
            firstOptional = 5;
        else if (string.Equals(cmd, "SPAWN", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "TEMPLATE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmd, "RANDOM_HOSTAGE", StringComparison.OrdinalIgnoreCase))
            firstOptional = 6;
        else
            firstOptional = 4;

        HellGateSpawnLineFormat.ParseOptionalPlacementFields(
            parts, firstOptional, out flipX, out rotationZ, out SpawnDepthSettings depth);
        sortOffset = SortOffsetFromSettings(in depth);
    }
}

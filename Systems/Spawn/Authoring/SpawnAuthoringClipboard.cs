using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 clipboard: Ctrl+C / Ctrl+V duplicates a HellGate pack spawn at Last LMB
/// (then nudge with arrows).
/// </summary>
internal static class SpawnAuthoringClipboard
{
    internal static bool HasClip { get; private set; }

    internal static string EnemyKey = string.Empty;
    internal static string Faction = string.Empty;
    internal static bool Elite;
    internal static bool FlipX;
    internal static bool Random;
    internal static float Chance = 1f;
    internal static bool IsTrap;
    internal static bool IsGold;
    internal static float RotationZ;
    internal static int SortOffset;
    internal static string EventCore = string.Empty;
    internal static float EventCoreChance = 1f;

    private static readonly List<ChunkItem> Chunk = new List<ChunkItem>(32);

    internal static bool HasChunk => Chunk.Count > 0;
    internal static int ChunkCount => Chunk.Count;

    private sealed class ChunkItem
    {
        internal string Line;
        internal string Key;
        internal float RelX;
        internal float RelY;
    }

    internal static void Clear()
    {
        HasClip = false;
        EnemyKey = string.Empty;
        Faction = string.Empty;
        Elite = false;
        FlipX = false;
        Random = false;
        Chance = 1f;
        IsTrap = false;
        IsGold = false;
        RotationZ = 0f;
        SortOffset = 0;
        EventCore = string.Empty;
        EventCoreChance = 1f;
        Chunk.Clear();
    }

    internal static string FormatShort()
    {
        if (HasChunk)
            return "chunk × " + Chunk.Count.ToString(CultureInfo.InvariantCulture);
        if (!HasClip)
            return "—";
        string name = EnemyKey;
        if (IsGold)
        {
            name = "GOLD " + name;
            if (Random)
                name = name + " RND" + Chance.ToString("0.##", CultureInfo.InvariantCulture);
            return name;
        }
        if (IsTrap)
        {
            name = "TRAP " + name;
            if (!string.IsNullOrEmpty(Faction))
                name = name + " / " + SpawnAuthoringFactionUi.DisplayName(Faction);
            if (FlipX)
                name = name + " flip";
            if (SpawnAuthoringState.KeyAllowsRotation(EnemyKey))
            {
                string rot = SpawnAuthoringPackEdit.FormatRotationToken(RotationZ);
                if (!string.IsNullOrEmpty(rot))
                    name = name + " " + rot;
            }
            string sort = SpawnAuthoringPackEdit.FormatSortToken(SortOffset);
            if (!string.IsNullOrEmpty(sort))
                name = name + " " + sort;
            return name;
        }

        if (!string.IsNullOrEmpty(Faction))
            name = name + " / " + SpawnAuthoringFactionUi.DisplayName(Faction);
        if (Elite)
            name = name + " ★";
        if (FlipX)
            name = name + " flip";
        if (Random)
            name = name + " RND" + Chance.ToString("0.##", CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(EventCore))
        {
            name = name + " EC";
            if (EventCoreChance > 0f && EventCoreChance < 0.999f)
                name = name + EventCoreChance.ToString("0.##", CultureInfo.InvariantCulture);
        }
        return name;
    }

    internal static bool TryCopyFrom(SpawnManagedInstance managed, out string status)
    {
        status = null;
        if (managed == null || !managed.HasAuthoringLink)
        {
            status = SpawnAuthoringLoc.T("status.copyNeedSpawn");
            return false;
        }

        IsGold = SpawnAuthoringPackEdit.IsGoldManagedInstance(managed);
        IsTrap = !IsGold && SpawnAuthoringPackEdit.IsTrapManagedInstance(managed);
        EventCore = string.Empty;
        EventCoreChance = 1f;
        if (IsGold)
        {
            string goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(managed.AuthoringEnemyKey);
            if (string.IsNullOrEmpty(goldKey))
                SpawnAuthoringPackEdit.TryExtractKeyAndCoords(
                    managed.AuthoringSourceLineRaw, out goldKey, out _, out _);
            goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(goldKey);
            if (string.IsNullOrEmpty(goldKey))
            {
                status = SpawnAuthoringLoc.T("status.goldUnresolved");
                return false;
            }

            EnemyKey = goldKey;
            Faction = string.Empty;
            Elite = false;
            FlipX = false;
            Random = managed.AuthoringChance > 0f && managed.AuthoringChance < 0.999f;
            Chance = Random ? managed.AuthoringChance : 1f;
            RotationZ = 0f;
            SortOffset = 0;
            EventCore = string.Empty;
            EventCoreChance = 1f;
        }
        else if (IsTrap)
        {
            string trapKey = SpawnAuthoringPackEdit.ResolveTrapAuthoringKey(managed);
            if (string.IsNullOrEmpty(trapKey) || !SpawnAuthoringPackEdit.IsUsableTrapKey(trapKey))
            {
                status = SpawnAuthoringLoc.T("status.trapKeyUnresolved");
                return false;
            }

            EnemyKey = trapKey;
            Faction = SpawnAuthoringHostageCatalog.AcceptsFaction(trapKey)
                ? (managed.AuthoringFactionIdRaw ?? string.Empty)
                : string.Empty;
            Elite = false;
            Random = false;
            Chance = 1f;
            FlipX = managed.AuthoringFlipX;
            RotationZ = managed.AuthoringRotationZ;
            SortOffset = managed.AuthoringSortOffset;

            if (Mathf.Abs(RotationZ) < 0.01f && SortOffset == 0 && !FlipX)
            {
                SpawnAuthoringPackEdit.TryExtractPlacementExtras(
                    managed.AuthoringSourceLineRaw, out FlipX, out RotationZ, out SortOffset);
            }

            if (SpawnAuthoringState.KeyAllowsRotation(trapKey))
            {
                if (managed.gameObject != null)
                {
                    float liveRot = SpawnRotationUtility.ReadRotationZ(managed.gameObject);
                    if (Mathf.Abs(liveRot) > 0.01f)
                        RotationZ = liveRot;
                }
            }
            else
            {
                RotationZ = 0f;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(managed.AuthoringEnemyKey))
            {
                status = SpawnAuthoringLoc.T("status.copyNeedSpawn");
                return false;
            }

            EnemyKey = managed.AuthoringEnemyKey;
            Faction = managed.AuthoringFactionIdRaw ?? string.Empty;
            Elite = managed.AuthoringForceElite || managed.ForceElite;
            FlipX = managed.AuthoringFlipX;
            Random = managed.AuthoringChance > 0f && managed.AuthoringChance < 0.999f;
            Chance = Random ? managed.AuthoringChance : 1f;
            RotationZ = 0f;
            SortOffset = 0;
            SpawnAuthoringPackEdit.ExtractEventCoreFromSourceLine(
                managed.AuthoringSourceLineRaw, out EventCore, out EventCoreChance);
            if (string.IsNullOrEmpty(EventCore))
            {
                NoREroMod.Systems.EventCore.Host.EventCoreHost host =
                    managed.GetComponent<NoREroMod.Systems.EventCore.Host.EventCoreHost>() ??
                    managed.GetComponentInChildren<NoREroMod.Systems.EventCore.Host.EventCoreHost>(true);
                if (host != null && !string.IsNullOrEmpty(host.EventId))
                    EventCore = host.EventId;
            }
        }

        HasClip = true;

        // Also mirror into Create pending so Place matches the clip.
        SpawnAuthoringState.PendingEnemyKey = EnemyKey;
        SpawnAuthoringState.PendingFactionIndex = IndexOfFaction(Faction);
        SpawnAuthoringState.PendingForceElite = Elite;
        SpawnAuthoringState.PendingFlipX = FlipX;
        SpawnAuthoringState.PendingRandom = Random;
        SpawnAuthoringState.PendingChanceStr = Chance.ToString("0.##", CultureInfo.InvariantCulture);
        SpawnAuthoringState.PendingRotationZ = RotationZ;
        SpawnAuthoringState.PendingSortOffset = SortOffset;
        SpawnAuthoringState.PendingEventCoreChanceStr =
            (EventCoreChance > 0f ? EventCoreChance : 1f).ToString("0.##", CultureInfo.InvariantCulture);
        if (IsGold)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Gold;
        else if (!string.IsNullOrEmpty(EventCore))
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.EventCore;
            SpawnAuthoringState.PendingEnemyKey = EventCore;
        }
        else if (IsTrap && SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.None)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Trap;

        Chunk.Clear();
        status = SpawnAuthoringLoc.Tf("status.copiedAtPoint", FormatShort());
        return true;
    }

    internal static bool TryCopyRegion(out string status)
    {
        status = null;
        List<SpawnAuthoringRegion.Item> items;
        SpawnAuthoringRegion.Collect(out items);
        if (items == null || items.Count == 0)
        {
            status = SpawnAuthoringLoc.T("status.regionEmptyBox");
            return false;
        }

        float originX = SpawnAuthoringRegion.MinX;
        float originY = SpawnAuthoringRegion.MinY;
        Chunk.Clear();
        var portable = new StringBuilder();
        portable.Append("# HellGate chunk  origin=")
            .Append(originX.ToString("F2", CultureInfo.InvariantCulture))
            .Append(",")
            .Append(originY.ToString("F2", CultureInfo.InvariantCulture))
            .Append("  items=")
            .Append(items.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine();

        for (int i = 0; i < items.Count; i++)
        {
            SpawnAuthoringRegion.Item src = items[i];
            float relX = src.WorldX - originX;
            float relY = src.WorldY - originY;
            Chunk.Add(new ChunkItem
            {
                Line = src.Line,
                Key = src.Key ?? string.Empty,
                RelX = relX,
                RelY = relY
            });

            string portableLine;
            if (SpawnAuthoringPackEdit.TryRelocateLine(src.Line, relX, relY, out portableLine))
                portable.AppendLine(portableLine);
            else
                portable.AppendLine(src.Line);
        }

        HasClip = false;
        EnemyKey = string.Empty;
        try
        {
            GUIUtility.systemCopyBuffer = portable.ToString();
        }
        catch
        {
        }

        status = SpawnAuthoringLoc.Tf(
            "status.copiedChunk", items.Count.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    internal static bool TryPasteChunkAt(float originX, float originY, out string status)
    {
        status = null;
        if (!HasChunk)
        {
            status = SpawnAuthoringLoc.T("status.chunkEmpty");
            return false;
        }

        int written = 0;
        int previewed = 0;
        string fileName = string.Empty;
        for (int i = 0; i < Chunk.Count; i++)
        {
            ChunkItem item = Chunk[i];
            float x = originX + item.RelX;
            float y = originY + item.RelY;
            string line;
            if (!SpawnAuthoringPackEdit.TryRelocateLine(item.Line, x, y, out line))
                continue;

            string packPath;
            int lineIndex;
            string writeErr;
            if (!SpawnAuthoringPackWriter.TryAppendRawLine(line, out packPath, out lineIndex, out writeErr))
            {
                status = writeErr ?? SpawnAuthoringLoc.T("status.chunkWriteFailed");
                return written > 0;
            }

            fileName = System.IO.Path.GetFileName(packPath);
            SpawnAuthoringUndo.RecordAppend(
                packPath, lineIndex, line, item.Key, x, y, string.Empty, 1f, false, false);
            written++;
            if (TryPreviewPastedLine(line, packPath, lineIndex, item.Key, x, y))
                previewed++;
        }

        if (written <= 0)
        {
            status = SpawnAuthoringLoc.T("status.chunkPasteFailed");
            return false;
        }

        SpawnAuthoringState.SetLastClickSilent(originX, originY, fileName);
        SpawnAuthoringState.LastClickPackHint = fileName;
        status = SpawnAuthoringLoc.Tf(
            "status.pastedChunk",
            written.ToString(CultureInfo.InvariantCulture),
            originX.ToString("F2", CultureInfo.InvariantCulture),
            originY.ToString("F2", CultureInfo.InvariantCulture),
            fileName,
            previewed.ToString(CultureInfo.InvariantCulture));
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Paste chunk x" + written + " → " + fileName);
        return true;
    }

    private static bool TryPreviewPastedLine(
        string line,
        string packPath,
        int lineIndex,
        string key,
        float x,
        float y)
    {
        if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            return false;

        string cmd = line.Trim();
        int comma = cmd.IndexOf(',');
        if (comma > 0)
            cmd = cmd.Substring(0, comma).Trim();

        if (string.Equals(cmd, "EVENTTRAP", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cmd, "REINFORCEMENT", System.StringComparison.OrdinalIgnoreCase))
        {
            return SpawnAuthoringAnchorMarker.Spawn(cmd, key, new Vector2(x, y), packPath, lineIndex, line) != null;
        }

        SpawnAuthoringPackEdit.TryExtractPlacementExtras(line, out bool flip, out float rot, out int sort);
        if (!SpawnAuthoringState.KeyAllowsRotation(key))
            rot = 0f;
        string faction = SpawnAuthoringPackEdit.ExtractFactionFromSourceLine(line);
        if (string.IsNullOrEmpty(faction))
            faction = null;

        GameObject spawned = null;
        if (string.Equals(cmd, "GOLD", System.StringComparison.OrdinalIgnoreCase) ||
            LooksLikeRandomGold(line))
        {
            if (!EconomicConfig.Enable)
                return false;
            int amount = 100;
            string range = SpawnAuthoringGoldCatalog.NormalizeRangeToken(key);
            int dash = range.IndexOf('-');
            if (dash > 0)
                int.TryParse(range.Substring(0, dash).Trim(), out amount);
            else
                int.TryParse(range, out amount);
            if (amount < 1)
                amount = 100;
            spawned = GoldDropAwarder.TrySpawnPlacedPickup(new Vector2(x, y), amount);
        }
        else if (HellGateSpawnLineFormat.IsTrapShortcut(cmd) ||
                 HellGateSpawnLineFormat.IsDecorShortcut(cmd) ||
                 HellGateSpawnLineFormat.IsHostageShortcut(cmd) ||
                 HellGateSpawnLineFormat.IsObjectShortcut(cmd) ||
                 SpawnSpikeKeys.IsSpikeLikeKey(key) ||
                 SpawnDecorCatalog.IsKnownDecorKey(key) ||
                 SpawnAuthoringHostageCatalog.IsHostageLineKey(key) ||
                 SpawnAuthoringHostageCatalog.IsOtherSceneKey(key))
        {
            SpawnDepthSettings depth = SpawnAuthoringPackEdit.ToDepthSettings(sort);
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            SpawnTemplateCatalog.TrySpawn(
                SpawnTemplateCatalog.ResolveTemplateCategory(key),
                key,
                new Vector2(x, y),
                1,
                "[SPAWN AUTHORING]",
                sceneName,
                flip,
                rot,
                depth,
                faction,
                out spawned);
        }
        else
        {
            EnemyPrefabRegistry.Initialize();
            SpawnAuthoringPackEdit.ExtractEventCoreFromSourceLine(line, out string eventCore, out _);
            spawned = SpawnConfigExecutor.TrySpawnRuntimeEnemy(
                key, new Vector2(x, y), faction, false, false, false, flip, eventCore);
        }

        if (spawned == null)
            return false;

        if (Mathf.Abs(rot) > 0.001f)
            SpawnRotationUtility.LockAuthoringRotation(spawned, rot);
        if (flip)
            SpawnFlipUtility.LockHorizontalFlipLeft(spawned);

        SpawnConfigExecutor.ApplyAuthoringLink(
            spawned,
            packPath,
            lineIndex,
            line,
            x,
            y,
            key,
            faction,
            1f,
            1,
            flip,
            false,
            rot,
            sort,
            true);
        return true;
    }

    private static bool LooksLikeRandomGold(string line)
    {
        return !string.IsNullOrEmpty(line) &&
               line.IndexOf("gold=", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Append pack line + preview spawn at XY. Returns the new instance when spawn succeeds.</summary>
    internal static bool TryPasteAt(float x, float y, out GameObject spawned, out string status)
    {
        spawned = null;
        status = null;
        if (!HasClip || string.IsNullOrEmpty(EnemyKey))
        {
            status = SpawnAuthoringLoc.T("status.clipboardEmpty");
            return false;
        }

        if (IsGold)
            return TryPasteGoldAt(x, y, out spawned, out status);

        if (IsTrap)
            return TryPasteTrapAt(x, y, out spawned, out status);

        string faction = string.IsNullOrEmpty(Faction) ? null : Faction;
        float chance = Random ? Chance : 1f;
        if (chance >= 0.999f)
            chance = Random ? 0.99f : 1f;
        if (Random && chance <= 0f)
            chance = 0.01f;

        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        if (!SpawnAuthoringPackWriter.TryAppendStaticEnemy(
                EnemyKey,
                x,
                y,
                1,
                faction,
                out packPath,
                out line,
                out lineIndex,
                out writeErr,
                Elite,
                FlipX,
                chance,
                EventCore,
                EventCoreChance))
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.pasteWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath, lineIndex, line, EnemyKey, x, y, faction ?? string.Empty, chance, FlipX, Elite);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.SetLastClickSilent(x, y, fileName);
        SpawnAuthoringState.LastClickPackHint = fileName;

        EnemyPrefabRegistry.Initialize();
        spawned = SpawnConfigExecutor.TrySpawnRuntimeEnemy(
            EnemyKey, new Vector2(x, y), faction, false, false, Elite, FlipX, EventCore);

        if (spawned != null)
        {
            SpawnFlipUtility.ApplyAuthoringFlip(spawned, FlipX);
            SpawnConfigExecutor.ApplyAuthoringLink(
                spawned,
                packPath,
                lineIndex,
                line,
                x,
                y,
                EnemyKey,
                faction,
                chance,
                1,
                FlipX,
                Elite);
        }

        status = FormatPastedAt(x, y, fileName, spawned != null);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Paste: " + line + " → " + fileName);
        return true;
    }

    private static bool TryPasteGoldAt(float x, float y, out GameObject spawned, out string status)
    {
        spawned = null;
        status = null;
        float chance = Random ? Chance : 1f;
        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        if (!SpawnAuthoringPackWriter.TryAppendGold(
                EnemyKey, x, y, chance, out packPath, out line, out lineIndex, out writeErr))
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.pasteWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath, lineIndex, line, EnemyKey, x, y, string.Empty, chance, false, false);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.SetLastClickSilent(x, y, fileName);
        SpawnAuthoringState.LastClickPackHint = fileName;

        if (EconomicConfig.Enable)
        {
            string range = SpawnAuthoringGoldCatalog.NormalizeRangeToken(EnemyKey);
            int amount = 100;
            int dash = range.IndexOf('-');
            if (dash > 0)
                int.TryParse(range.Substring(0, dash).Trim(), out amount);
            else
                int.TryParse(range, out amount);
            if (amount < 1)
                amount = 100;
            spawned = GoldDropAwarder.TrySpawnPlacedPickup(new Vector2(x, y), amount);
            if (spawned != null)
            {
                SpawnConfigExecutor.ApplyAuthoringLink(
                    spawned, packPath, lineIndex, line, x, y, EnemyKey, null, chance, 1, false);
            }
        }

        status = FormatPastedAt(x, y, fileName, spawned != null);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Paste gold: " + line + " → " + fileName);
        return true;
    }

    private static bool TryPasteTrapAt(float x, float y, out GameObject spawned, out string status)
    {
        spawned = null;
        status = null;

        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        string faction = SpawnAuthoringHostageCatalog.AcceptsFaction(EnemyKey) && !string.IsNullOrEmpty(Faction)
            ? Faction
            : null;
        float pasteRot = SpawnAuthoringState.KeyAllowsRotation(EnemyKey) ? RotationZ : 0f;

        if (!SpawnAuthoringPackWriter.TryAppendTrap(
                EnemyKey,
                x,
                y,
                1,
                FlipX,
                out packPath,
                out line,
                out lineIndex,
                out writeErr,
                pasteRot,
                SortOffset,
                faction))
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.pasteWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath, lineIndex, line, EnemyKey, x, y, faction ?? string.Empty, 1f, FlipX, false);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.SetLastClickSilent(x, y, fileName);
        SpawnAuthoringState.LastClickPackHint = fileName;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        SpawnDepthSettings depth = SpawnAuthoringPackEdit.ToDepthSettings(SortOffset);
        bool ok = SpawnTemplateCatalog.TrySpawn(
            SpawnTemplateCatalog.ResolveTemplateCategory(EnemyKey),
            EnemyKey,
            new Vector2(x, y),
            1,
            "[SPAWN AUTHORING]",
            sceneName,
            FlipX,
            pasteRot,
            depth,
            faction,
            out spawned);

        if (spawned != null)
        {
            SpawnConfigExecutor.ApplyAuthoringLink(
                spawned,
                packPath,
                lineIndex,
                line,
                x,
                y,
                EnemyKey,
                faction,
                1f,
                1,
                FlipX,
                false,
                pasteRot,
                SortOffset,
                true);
        }

        status = FormatPastedAt(x, y, fileName, ok && spawned != null);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Paste trap: " + line + " → " + fileName);
        return true;
    }

    private static string FormatPastedAt(float x, float y, string fileName, bool previewOk)
    {
        return SpawnAuthoringLoc.Tf(
            "status.pastedAt",
            FormatShort(),
            x.ToString("F2", CultureInfo.InvariantCulture),
            y.ToString("F2", CultureInfo.InvariantCulture),
            fileName,
            previewOk
                ? SpawnAuthoringLoc.T("status.pastedNudge")
                : SpawnAuthoringLoc.T("status.pastedPreviewFail"));
    }

    private static int IndexOfFaction(string raw)
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        if (string.IsNullOrEmpty(raw))
            return 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], raw.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }
}

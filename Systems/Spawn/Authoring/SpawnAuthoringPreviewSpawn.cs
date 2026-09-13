using NoREroMod.Systems.Economy;
using NoREroMod.Systems.EventCore.Content;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Confirm flow: write pack line + preview-spawn at last F11 click + authoring link.</summary>
internal static class SpawnAuthoringPreviewSpawn
{
    internal static bool TryConfirmSpawnAndSave(out string status)
    {
        status = null;
        string key = SpawnAuthoringState.SelectedEnemyKey;
        if (string.IsNullOrEmpty(key))
        {
            status = SpawnAuthoringLoc.T("status.noConfirmedEnemy");
            return false;
        }

        if (!SpawnAuthoringState.HasLastClick)
        {
            status = SpawnAuthoringLoc.T("status.needPoint");
            return false;
        }

        string faction = SpawnAuthoringState.GetSelectedFactionToken();
        if (string.IsNullOrEmpty(faction))
            faction = null;

        const int count = 1;
        bool flipX = SpawnAuthoringState.SelectedFlipX;
        bool elite = SpawnAuthoringState.SelectedForceElite;
        float chance = SpawnAuthoringState.SelectedChance;
        if (!SpawnAuthoringState.SelectedRandom)
            chance = 1f;

        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        string eventCore = SpawnAuthoringState.GetSelectedEventCoreId();
        float eventCoreChance = SpawnAuthoringState.SelectedEventCoreChance;

        if (EventCoreDefinitionRegistry.TryGetBoundEnemyKey(eventCore, out string boundKey) &&
            !string.IsNullOrEmpty(boundKey))
            key = boundKey;

        bool saved = SpawnAuthoringPackWriter.TryAppendStaticEnemy(
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            count,
            faction,
            out packPath,
            out line,
            out lineIndex,
            out writeErr,
            elite,
            flipX,
            chance,
            eventCore,
            eventCoreChance);

        if (!saved)
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.packWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath,
            lineIndex,
            line,
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            faction ?? string.Empty,
            chance,
            flipX,
            elite);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.LastClickPackHint = fileName;

        EnemyPrefabRegistry.Initialize();
        Vector2 pos = new Vector2(SpawnAuthoringState.LastClickX, SpawnAuthoringState.LastClickY);

        // Pass flip into spawn so facing is applied inside SpawnSingle (after presentation).
        GameObject spawned = SpawnConfigExecutor.TrySpawnRuntimeEnemy(
            key, pos, faction, false, false, elite, flipX, eventCore);

        if (spawned != null)
        {
            // Re-apply after spawn pipeline — some enemies reset DIR on enable.
            SpawnFlipUtility.ApplyAuthoringFlip(spawned, flipX);

            SpawnConfigExecutor.ApplyAuthoringLink(
                spawned,
                packPath,
                lineIndex,
                line,
                pos.x,
                pos.y,
                key,
                faction,
                chance,
                count,
                flipX,
                elite);
        }

        if (spawned == null)
        {
            status = SpawnAuthoringLoc.Tf("status.savedPrefabNotReady", fileName);
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Pack saved (" + line + ") but preview spawn failed.");
            return true;
        }

        string factionNote = string.IsNullOrEmpty(faction) ? "" : " [" + faction + "]";
        string eliteNote = elite ? " [ELITE]" : "";
        string flipNote = flipX ? " [flip]" : "";
        string rndNote = chance < 0.999f
            ? (" [RANDOM " + chance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "]")
            : "";
        string ecNote = string.IsNullOrEmpty(eventCore)
            ? ""
            : (eventCoreChance < 0.999f
                ? (" [EC p=" + eventCoreChance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "]")
                : " [EC]");
        status = SpawnAuthoringLoc.Tf(
            "status.savedSpawned",
            factionNote + eliteNote + flipNote + rndNote + ecNote,
            fileName);
        Plugin.Log?.LogInfo(
            "[SPAWN AUTHORING] Saved+spawned line: " + line + " → " + fileName);
        return true;
    }

    internal static bool TryConfirmTrapSpawnAndSave(out string status)
    {
        status = null;
        string key = SpawnAuthoringState.SelectedEnemyKey;
        if (string.IsNullOrEmpty(key))
        {
            status = SpawnAuthoringLoc.T("status.noConfirmedTrap");
            return false;
        }

        if (!SpawnAuthoringState.HasLastClick)
        {
            status = SpawnAuthoringLoc.T("status.needPoint");
            return false;
        }

        const int count = 1;
        bool flipX = SpawnAuthoringState.SelectedFlipX;
        float rotationZ = SpawnAuthoringState.KeyAllowsRotation(key)
            ? SpawnAuthoringState.PendingRotationZ
            : 0f;
        int sortOffset = SpawnAuthoringState.SelectedSortOffset;
        SpawnDepthSettings depth = SpawnAuthoringPackEdit.ToDepthSettings(sortOffset);

        string faction = null;
        if (SpawnAuthoringHostageCatalog.AcceptsFaction(key))
        {
            faction = SpawnAuthoringState.GetSelectedFactionToken();
            if (string.IsNullOrEmpty(faction))
                faction = null;
        }

        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        float chance = 1f;
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Hostage &&
            SpawnAuthoringState.SelectedRandom)
            chance = SpawnAuthoringState.SelectedChance;

        bool saved = SpawnAuthoringPackWriter.TryAppendTrap(
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            count,
            flipX,
            out packPath,
            out line,
            out lineIndex,
            out writeErr,
            rotationZ,
            sortOffset,
            faction,
            chance);

        if (!saved)
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.packWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath,
            lineIndex,
            line,
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            faction ?? string.Empty,
            1f,
            flipX,
            false);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.LastClickPackHint = fileName;

        Vector2 pos = new Vector2(SpawnAuthoringState.LastClickX, SpawnAuthoringState.LastClickY);
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        GameObject spawned;
        bool spawnedOk = SpawnTemplateCatalog.TrySpawn(
            SpawnTemplateCatalog.ResolveTemplateCategory(key),
            key,
            pos,
            count,
            "[SPAWN AUTHORING]",
            sceneName,
            flipX,
            rotationZ,
            depth,
            faction,
            out spawned);

        if (spawned != null)
        {
            if (Mathf.Abs(rotationZ) > 0.001f)
                SpawnRotationUtility.LockAuthoringRotation(spawned, rotationZ);

            SpawnFlipUtility.ApplyAuthoringFlip(spawned, flipX);

            SpawnConfigExecutor.ApplyAuthoringLink(
                spawned,
                packPath,
                lineIndex,
                line,
                pos.x,
                pos.y,
                key,
                faction,
                1f,
                count,
                flipX,
                false,
                rotationZ,
                sortOffset,
                true);
        }

        if (!spawnedOk || spawned == null)
        {
            status = SpawnAuthoringLoc.Tf("status.savedTemplateNotReady", fileName);
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Trap pack saved (" + line + ") but preview spawn failed.");
            return true;
        }

        string flipNote = flipX ? " [flip]" : "";
        string rotNote = SpawnAuthoringPackEdit.FormatRotationToken(rotationZ);
        rotNote = string.IsNullOrEmpty(rotNote) ? "" : " [" + rotNote + "]";
        string sortNote = SpawnAuthoringPackEdit.FormatSortToken(sortOffset);
        sortNote = string.IsNullOrEmpty(sortNote) ? "" : " [" + sortNote + "]";
        string rndNote = chance < 0.999f
            ? (" [RANDOM " + chance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "]")
            : "";
        status = SpawnAuthoringLoc.Tf(
            "status.savedSpawnedTrap",
            flipNote + rotNote + sortNote + rndNote,
            fileName);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Saved+spawned trap: " + line + " → " + fileName);
        return true;
    }

    internal static bool TryConfirmGoldSpawnAndSave(out string status)
    {
        status = null;
        string key = SpawnAuthoringState.SelectedEnemyKey;
        if (string.IsNullOrEmpty(key))
        {
            status = SpawnAuthoringLoc.T("status.noGold");
            return false;
        }

        if (!SpawnAuthoringState.HasLastClick)
        {
            status = SpawnAuthoringLoc.T("status.needPoint");
            return false;
        }

        float chance = SpawnAuthoringState.SelectedRandom ? SpawnAuthoringState.SelectedChance : 1f;
        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        bool saved = SpawnAuthoringPackWriter.TryAppendGold(
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            chance,
            out packPath,
            out line,
            out lineIndex,
            out writeErr);

        if (!saved)
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.packWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath,
            lineIndex,
            line,
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            string.Empty,
            chance,
            false,
            false);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.LastClickPackHint = fileName;

        if (!EconomicConfig.Enable)
        {
            status = SpawnAuthoringLoc.Tf("status.savedGoldDisabled", fileName);
            return true;
        }

        string range = SpawnAuthoringGoldCatalog.NormalizeRangeToken(key);
        int previewAmount = 100;
        int dash = range.IndexOf('-');
        if (dash > 0)
            int.TryParse(range.Substring(0, dash).Trim(), out previewAmount);
        else
            int.TryParse(range, out previewAmount);
        if (previewAmount < 1)
            previewAmount = 100;

        Vector2 pos = new Vector2(SpawnAuthoringState.LastClickX, SpawnAuthoringState.LastClickY);
        GameObject spawned = GoldDropAwarder.TrySpawnPlacedPickup(pos, previewAmount);
        if (spawned != null)
        {
            SpawnConfigExecutor.ApplyAuthoringLink(
                spawned,
                packPath,
                lineIndex,
                line,
                pos.x,
                pos.y,
                key,
                null,
                chance,
                1,
                false,
                false);
        }

        status = spawned != null
            ? SpawnAuthoringLoc.Tf("status.savedGoldOk", fileName)
            : SpawnAuthoringLoc.Tf("status.savedGoldPreviewFail", fileName);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Saved gold: " + line + " → " + fileName);
        return true;
    }

    internal static bool TryConfirmEventTrapAndSave(out string status)
    {
        status = null;
        string key = SpawnAuthoringState.SelectedEnemyKey;
        if (string.IsNullOrEmpty(key))
        {
            status = SpawnAuthoringLoc.T("status.noEventTrapFolder");
            return false;
        }

        if (!SpawnAuthoringState.HasLastClick)
        {
            status = SpawnAuthoringLoc.T("status.needPoint");
            return false;
        }

        string packPath;
        string line;
        int lineIndex;
        string writeErr;
        bool saved = SpawnAuthoringPackWriter.TryAppendEventTrap(
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            out packPath,
            out line,
            out lineIndex,
            out writeErr);

        if (!saved)
        {
            status = writeErr ?? SpawnAuthoringLoc.T("status.packWriteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordAppend(
            packPath,
            lineIndex,
            line,
            key,
            SpawnAuthoringState.LastClickX,
            SpawnAuthoringState.LastClickY,
            string.Empty,
            1f,
            false,
            false);

        string fileName = System.IO.Path.GetFileName(packPath);
        SpawnAuthoringState.LastClickPackHint = fileName;
        GameObject marker = SpawnAuthoringAnchorMarker.Spawn(
            "EVENTTRAP",
            key,
            new Vector2(SpawnAuthoringState.LastClickX, SpawnAuthoringState.LastClickY),
            packPath,
            lineIndex,
            line);
        if (marker != null)
            SpawnAuthoringWorldPick.ForceLock(marker);
        status = SpawnAuthoringLoc.Tf("status.savedEventTrap", fileName);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Saved EventTrap: " + line + " → " + fileName);
        return true;
    }
}

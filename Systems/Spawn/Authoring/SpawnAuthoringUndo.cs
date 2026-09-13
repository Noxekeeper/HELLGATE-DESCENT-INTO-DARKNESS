using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 authoring undo (Ctrl+Z): restore last Append / Replace / Delete against the pack file
/// and best-effort live instances.
/// </summary>
internal static class SpawnAuthoringUndo
{
    private const int MaxDepth = 64;

    private enum Kind
    {
        Append,
        Replace,
        Delete
    }

    private sealed class Entry
    {
        internal Kind Kind;
        internal string PackPath;
        internal int LineIndex;
        internal string BeforeLine;
        internal string AfterLine;
        internal string EnemyKey;
        internal float SpawnX;
        internal float SpawnY;
        internal string Faction;
        internal float Chance;
        internal bool FlipX;
        internal bool Elite;
    }

    private static readonly List<Entry> Stack = new List<Entry>(MaxDepth);

    internal static int Count => Stack.Count;

    internal static bool CanUndo => Stack.Count > 0;

    internal static void Clear()
    {
        Stack.Clear();
    }

    internal static void RecordAppend(
        string packPath,
        int lineIndex,
        string line,
        string enemyKey,
        float x,
        float y,
        string faction,
        float chance,
        bool flipX,
        bool elite)
    {
        Push(new Entry
        {
            Kind = Kind.Append,
            PackPath = packPath,
            LineIndex = lineIndex,
            AfterLine = line ?? string.Empty,
            EnemyKey = enemyKey ?? string.Empty,
            SpawnX = x,
            SpawnY = y,
            Faction = faction ?? string.Empty,
            Chance = chance,
            FlipX = flipX,
            Elite = elite
        });
    }

    internal static void RecordReplace(
        string packPath,
        int lineIndex,
        string beforeLine,
        string afterLine)
    {
        Push(new Entry
        {
            Kind = Kind.Replace,
            PackPath = packPath,
            LineIndex = lineIndex,
            BeforeLine = beforeLine ?? string.Empty,
            AfterLine = afterLine ?? string.Empty
        });
    }

    internal static void RecordDelete(
        string packPath,
        int lineIndex,
        string removedLine,
        string enemyKey,
        float x,
        float y,
        string faction,
        float chance,
        bool flipX,
        bool elite)
    {
        Push(new Entry
        {
            Kind = Kind.Delete,
            PackPath = packPath,
            LineIndex = lineIndex,
            BeforeLine = removedLine ?? string.Empty,
            EnemyKey = enemyKey ?? string.Empty,
            SpawnX = x,
            SpawnY = y,
            Faction = faction ?? string.Empty,
            Chance = chance,
            FlipX = flipX,
            Elite = elite
        });
    }

    internal static bool TryUndo(out string status)
    {
        status = null;
        if (Stack.Count == 0)
        {
            status = SpawnAuthoringLoc.T("status.undoEmpty");
            return false;
        }

        Entry e = Stack[Stack.Count - 1];
        Stack.RemoveAt(Stack.Count - 1);

        switch (e.Kind)
        {
            case Kind.Append:
                return UndoAppend(e, out status);
            case Kind.Replace:
                return UndoReplace(e, out status);
            case Kind.Delete:
                return UndoDelete(e, out status);
            default:
                status = SpawnAuthoringLoc.T("status.undoUnknown");
                return false;
        }
    }

    private static void Push(Entry e)
    {
        if (e == null || string.IsNullOrEmpty(e.PackPath))
            return;
        Stack.Add(e);
        while (Stack.Count > MaxDepth)
            Stack.RemoveAt(0);
    }

    private static bool UndoAppend(Entry e, out string status)
    {
        string err;
        if (!SpawnAuthoringPackEdit.TryDeleteLine(e.PackPath, e.LineIndex, out err))
        {
            // Line may have shifted — try match by content.
            if (!SpawnAuthoringPackEdit.TryResolveLineIndex(
                    e.PackPath, e.LineIndex, e.AfterLine, e.SpawnX, e.SpawnY, e.EnemyKey,
                    out int resolved, out _, out err) ||
                !SpawnAuthoringPackEdit.TryDeleteLine(e.PackPath, resolved, out err))
            {
                status = err ?? SpawnAuthoringLoc.T("status.undoAppendFailed");
                return false;
            }

            SpawnAuthoringPackEdit.DestroyInstancesForDeletedLine(e.PackPath, resolved, e.AfterLine);
            SpawnAuthoringPackEdit.ReindexAfterDelete(e.PackPath, resolved);
        }
        else
        {
            SpawnAuthoringPackEdit.DestroyInstancesForDeletedLine(e.PackPath, e.LineIndex, e.AfterLine);
            SpawnAuthoringPackEdit.ReindexAfterDelete(e.PackPath, e.LineIndex);
        }

        status = SpawnAuthoringLoc.T("status.undoRemovedLast");
        return true;
    }

    private static bool UndoReplace(Entry e, out string status)
    {
        string err;
        int idx = e.LineIndex;
        if (!SpawnAuthoringPackEdit.TryReplaceLine(e.PackPath, idx, e.BeforeLine, out err))
        {
            if (!SpawnAuthoringPackEdit.TryResolveLineIndex(
                    e.PackPath, idx, e.AfterLine, 0f, 0f, null,
                    out idx, out _, out err) ||
                !SpawnAuthoringPackEdit.TryReplaceLine(e.PackPath, idx, e.BeforeLine, out err))
            {
                status = err ?? SpawnAuthoringLoc.T("status.undoReplaceFailed");
                return false;
            }
        }

        // Refresh live linked instances that still point at this line.
        SpawnManagedInstance[] all = Object.FindObjectsOfType<SpawnManagedInstance>();
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                SpawnManagedInstance m = all[i];
                if (m == null || !m.HasAuthoringLink)
                    continue;
                if (!string.Equals(m.AuthoringPackPath, e.PackPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (m.AuthoringLineIndex != idx &&
                    !string.Equals(m.AuthoringSourceLineRaw, e.AfterLine, StringComparison.OrdinalIgnoreCase))
                    continue;

                m.AuthoringSourceLineRaw = e.BeforeLine ?? string.Empty;
                if (SpawnAuthoringPackEdit.TryExtractKeyAndCoords(e.BeforeLine, out string key, out float x, out float y))
                {
                    m.AuthoringEnemyKey = key;
                    m.AuthoringSpawnX = x;
                    m.AuthoringSpawnY = y;
                    m.gameObject.transform.position = new Vector3(x, y, m.transform.position.z);
                }

                bool flip = LineHasFlip(e.BeforeLine);
                m.AuthoringFlipX = flip;
                SpawnFlipUtility.ApplyAuthoringFlip(m.gameObject, flip);
            }
        }

        status = SpawnAuthoringLoc.T("status.undoRestoredLine");
        return true;
    }

    private static bool UndoDelete(Entry e, out string status)
    {
        string err;
        if (!SpawnAuthoringPackEdit.TryInsertLine(e.PackPath, e.LineIndex, e.BeforeLine, out err))
        {
            status = err ?? SpawnAuthoringLoc.T("status.undoDeleteFailed");
            return false;
        }

        // Best-effort preview respawn so the author sees the enemy again.
        if (!string.IsNullOrEmpty(e.EnemyKey))
        {
            EnemyPrefabRegistry.Initialize();
            GameObject spawned = SpawnConfigExecutor.TrySpawnRuntimeEnemy(
                e.EnemyKey,
                new Vector2(e.SpawnX, e.SpawnY),
                string.IsNullOrEmpty(e.Faction) ? null : e.Faction,
                false,
                false,
                e.Elite,
                e.FlipX);

            if (spawned != null)
            {
                SpawnConfigExecutor.ApplyAuthoringLink(
                    spawned,
                    e.PackPath,
                    e.LineIndex,
                    e.BeforeLine,
                    e.SpawnX,
                    e.SpawnY,
                    e.EnemyKey,
                    e.Faction,
                    e.Chance <= 0f ? 1f : e.Chance,
                    1,
                    e.FlipX,
                    e.Elite);
            }
        }

        status = SpawnAuthoringLoc.Tf(
            "status.undoRestoredDeleted",
            string.IsNullOrEmpty(e.EnemyKey) ? "." : (" + " + e.EnemyKey));
        return true;
    }

    private static bool LineHasFlip(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;
        string[] parts = line.Split(',');
        if (parts.Length == 0)
            return false;
        return SpawnFlipUtility.TryParseFlipToken(parts[parts.Length - 1].Trim(), out bool flip) && flip;
    }
}

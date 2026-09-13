using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Appends authoring lines into the active runtime <c>HellGateSpawn_*.txt</c>
/// under <c>BepInEx/plugins/HellGateJson/HellGateSpawnPoint/</c>.
/// </summary>
internal static class SpawnAuthoringPackWriter
{
    internal static bool TryAppendStaticEnemy(
        string enemyKey,
        float x,
        float y,
        int count,
        string factionIdRaw,
        out string packPath,
        out string line,
        out int lineIndex,
        out string error,
        bool forceElite = false,
        bool flipX = false,
        float chance = 1f,
        string eventCoreEventId = null,
        float eventCoreChance = 1f)
    {
        packPath = null;
        line = null;
        lineIndex = -1;
        error = null;

        if (string.IsNullOrEmpty(enemyKey))
        {
            error = SpawnAuthoringLoc.T("status.noConfirmedEnemy");
            return false;
        }

        if (count < 1)
            count = 1;

        line = SpawnAuthoringPackEdit.BuildEnemyLine(
            x, y, enemyKey, factionIdRaw, chance, count, flipX, forceElite, eventCoreEventId, eventCoreChance);
        return TryAppendPreparedLine(line, out packPath, out lineIndex, out error);
    }

    internal static bool TryAppendTrap(
        string trapKey,
        float x,
        float y,
        int count,
        bool flipX,
        out string packPath,
        out string line,
        out int lineIndex,
        out string error,
        float rotationZ = 0f,
        int sortOffset = 0,
        string factionIdRaw = null,
        float chance = 1f)
    {
        packPath = null;
        line = null;
        lineIndex = -1;
        error = null;

        if (string.IsNullOrEmpty(trapKey))
        {
            error = SpawnAuthoringLoc.T("status.noConfirmedTrap");
            return false;
        }

        if (count < 1)
            count = 1;

        line = SpawnAuthoringPackEdit.BuildTrapLine(
            x, y, trapKey, count, flipX, rotationZ, sortOffset, factionIdRaw, chance);
        return TryAppendPreparedLine(line, out packPath, out lineIndex, out error);
    }

    internal static bool TryAppendGold(
        string rangeToken,
        float x,
        float y,
        float chance,
        out string packPath,
        out string line,
        out int lineIndex,
        out string error)
    {
        packPath = null;
        line = null;
        lineIndex = -1;
        error = null;

        if (string.IsNullOrEmpty(SpawnAuthoringGoldCatalog.NormalizeRangeToken(rangeToken)))
        {
            error = SpawnAuthoringLoc.T("status.noGold");
            return false;
        }

        line = SpawnAuthoringPackEdit.BuildGoldLine(x, y, rangeToken, chance, 1);
        return TryAppendPreparedLine(line, out packPath, out lineIndex, out error);
    }

    internal static bool TryAppendEventTrap(
        string packFolder,
        float x,
        float y,
        out string packPath,
        out string line,
        out int lineIndex,
        out string error)
    {
        packPath = null;
        line = null;
        lineIndex = -1;
        error = null;

        if (string.IsNullOrEmpty(packFolder))
        {
            error = SpawnAuthoringLoc.T("status.noEventTrapFolder");
            return false;
        }

        line = SpawnAuthoringPackEdit.BuildEventTrapLine(
            x, y, packFolder, SpawnAuthoringState.BuildPendingEventTrapExtrasSuffix());
        return TryAppendPreparedLine(line, out packPath, out lineIndex, out error);
    }

    internal static bool TryAppendRawLine(
        string preparedLine,
        out string packPath,
        out int lineIndex,
        out string error)
    {
        return TryAppendPreparedLine(preparedLine, out packPath, out lineIndex, out error);
    }

    private static bool TryAppendPreparedLine(
        string preparedLine,
        out string packPath,
        out int lineIndex,
        out string error)
    {
        packPath = null;
        lineIndex = -1;
        error = null;

        packPath = HellGateLocationSpawnRefresh.GetActiveSpawnConfigPath();
        if (string.IsNullOrEmpty(packPath))
        {
            error = SpawnAuthoringLoc.T("status.noSpawnPack");
            return false;
        }

        string dir = Path.GetDirectoryName(packPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        try
        {
            EnsureTrailingNewline(packPath);
            int before = SpawnAuthoringPackEdit.CountFileLines(packPath);
            File.AppendAllText(packPath, preparedLine + Environment.NewLine, Encoding.UTF8);
            lineIndex = before;
            Plugin.Log?.LogInfo(
                "[SPAWN AUTHORING] Appended line " + (lineIndex + 1) + " to " +
                Path.GetFileName(packPath) + ": " + preparedLine);
            return true;
        }
        catch (Exception ex)
        {
            error = SpawnAuthoringLoc.Tf("status.writeFailed", ex.Message);
            return false;
        }
    }

    internal static string GetActivePackFileName()
    {
        string path = HellGateLocationSpawnRefresh.GetActiveSpawnConfigPath();
        return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
    }

    private static void EnsureTrailingNewline(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                if (stream.Length == 0)
                    return;

                stream.Seek(-1, SeekOrigin.End);
                int last = stream.ReadByte();
                if (last == '\n')
                    return;

                byte[] nl = Encoding.UTF8.GetBytes(Environment.NewLine);
                stream.Seek(0, SeekOrigin.End);
                stream.Write(nl, 0, nl.Length);
            }
        }
        catch
        {
        }
    }
}

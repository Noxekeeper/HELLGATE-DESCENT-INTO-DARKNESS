using System;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Old / truncated saves can load <c>_EnemyLibraryEROstatus</c> smaller than vanilla's
/// <c>new float[70, 10]</c>. Kinoko uses LibraryID 59; writing that slot throws
/// <see cref="IndexOutOfRangeException"/> inside <c>Library_rape</c>, which aborts
/// <c>MushroomERO.OnEvent</c> and freezes the H-scene at START6 (ERO never starts).
///
/// This guard grows the array (and keeps <see cref="StaticMng"/> in sync) before any write.
/// </summary>
internal static class EnemyLibraryEroStatusGuard
{
    // Vanilla StaticMng / PlayerStatus default.
    private const int VanillaRows = 70;
    private const int VanillaCols = 10;
    // Highest known enemy LibraryID in 1.07 decompile is 61 (IbaranoMajyo).
    private const int MinRowsForKnownIds = 64;

    private static bool _loggedGrow;

    public static void EnsureCapacity(PlayerStatus ps, int id)
    {
        if (ps == null || id < 0)
            return;

        float[,] arr = null;
        try
        {
            arr = ps._EnemyLibraryEROstatus;
        }
        catch
        {
            arr = null;
        }

        int needRows = Math.Max(VanillaRows, Math.Max(MinRowsForKnownIds, id + 1));
        int needCols = VanillaCols;

        int curRows = arr != null ? arr.GetLength(0) : 0;
        int curCols = arr != null ? arr.GetLength(1) : 0;

        // Need at least columns 0..3 for rape/naka/drink/defeat.
        if (arr != null && curRows > id && curCols >= 4)
            return;

        int rows = Math.Max(curRows, needRows);
        int cols = Math.Max(curCols, needCols);
        float[,] grown = new float[rows, cols];

        if (arr != null)
        {
            int copyR = Math.Min(curRows, rows);
            int copyC = Math.Min(curCols, cols);
            for (int r = 0; r < copyR; r++)
            {
                for (int c = 0; c < copyC; c++)
                    grown[r, c] = arr[r, c];
            }
        }

        ps._EnemyLibraryEROstatus = grown;
        try
        {
            StaticMng.EnemyLibraryEROstatus = grown;
        }
        catch
        {
            // StaticMng may be unavailable in edge boot paths.
        }

        if (!_loggedGrow)
        {
            _loggedGrow = true;
            Plugin.Log?.LogWarning(
                "[LibraryEroGuard] Expanded _EnemyLibraryEROstatus "
                + curRows + "x" + curCols + " -> " + rows + "x" + cols
                + " (requested id=" + id + "). Old/truncated save array was too small for Kinoko/etc.");
        }
    }

    public static void NormalizeAfterLoad()
    {
        try
        {
            float[,] arr = StaticMng.EnemyLibraryEROstatus;
            int curRows = arr != null ? arr.GetLength(0) : 0;
            int curCols = arr != null ? arr.GetLength(1) : 0;
            if (arr != null && curRows >= VanillaRows && curCols >= 4)
                return;

            // Force grow to vanilla size even without a pending write.
            GameObject go = GameObject.FindWithTag("GameController");
            PlayerStatus ps = go != null ? go.GetComponent<PlayerStatus>() : null;
            EnsureCapacity(ps, VanillaRows - 1);
            if (ps == null && (arr == null || curRows < VanillaRows || curCols < 4))
            {
                int rows = Math.Max(curRows, VanillaRows);
                int cols = Math.Max(curCols, VanillaCols);
                float[,] grown = new float[rows, cols];
                if (arr != null)
                {
                    int copyR = Math.Min(curRows, rows);
                    int copyC = Math.Min(curCols, cols);
                    for (int r = 0; r < copyR; r++)
                        for (int c = 0; c < copyC; c++)
                            grown[r, c] = arr[r, c];
                }
                StaticMng.EnemyLibraryEROstatus = grown;
                Plugin.Log?.LogWarning(
                    "[LibraryEroGuard] Normalized StaticMng.EnemyLibraryEROstatus after load "
                    + curRows + "x" + curCols + " -> " + rows + "x" + cols);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[LibraryEroGuard] NormalizeAfterLoad failed: " + ex.Message);
        }
    }
}

[HarmonyPatch]
internal static class EnemyLibraryEroStatusGuardPatches
{
    [HarmonyPatch(typeof(EnemyDate), "Library_rape")]
    [HarmonyPatch(typeof(EnemyDate), "Library_Naka")]
    [HarmonyPatch(typeof(EnemyDate), "Library_Drink")]
    [HarmonyPatch(typeof(EnemyDate), "Library_defeat")]
    [HarmonyPrefix]
    private static void EnemyDate_LibraryWrite_Prefix(EnemyDate __instance, int ID, PlayerStatus ___playerstatus)
    {
        PlayerStatus ps = ___playerstatus ?? (__instance != null ? AccessTools.Field(typeof(EnemyDate), "playerstatus")?.GetValue(__instance) as PlayerStatus : null);
        EnemyLibraryEroStatusGuard.EnsureCapacity(ps, ID);
    }

    [HarmonyPatch(typeof(Trapdata), "Library_rape")]
    [HarmonyPatch(typeof(Trapdata), "Library_Naka")]
    [HarmonyPatch(typeof(Trapdata), "Library_Drink")]
    [HarmonyPatch(typeof(Trapdata), "Library_defeat")]
    [HarmonyPrefix]
    private static void Trapdata_LibraryWrite_Prefix(Trapdata __instance, int ID, PlayerStatus ___playerstatus)
    {
        PlayerStatus ps = ___playerstatus ?? (__instance != null ? AccessTools.Field(typeof(Trapdata), "playerstatus")?.GetValue(__instance) as PlayerStatus : null);
        EnemyLibraryEroStatusGuard.EnsureCapacity(ps, ID);
    }

    [HarmonyPatch(typeof(LoadFile), "SetYesButtonClicked")]
    [HarmonyPostfix]
    private static void LoadFile_SetYesButtonClicked_Postfix()
    {
        EnemyLibraryEroStatusGuard.NormalizeAfterLoad();
    }
}

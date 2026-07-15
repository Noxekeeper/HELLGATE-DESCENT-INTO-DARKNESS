using System;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// MushroomERO (Kinoko) FIN climax: vanilla order is Player_Orgsm then Nakadasi.
/// If orgasm white-flash NREs (hidden UIeffect under black BG), Nakadasi never runs and
/// the womb meter stays empty. Mirror TouzokuAxe: force deposit when the climax SE fails.
/// </summary>
[HarmonyPatch(typeof(MushroomERO), "OnEvent")]
internal static class MushroomEroPregnancyPatch
{
    [HarmonyFinalizer]
    private static Exception Finalizer(
        MushroomERO __instance,
        Spine.Event e,
        SkeletonAnimation ___myspine,
        int ___se_count,
        Exception __exception)
    {
        try
        {
            if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
                return __exception;

            // Only recover the climax SE that vanilla uses for Nakadasi.
            if (__exception == null)
                return null;

            string ev = e != null ? e.ToString() : string.Empty;
            string anim = ___myspine != null ? (___myspine.AnimationName ?? string.Empty) : string.Empty;
            if (!string.Equals(ev, "SE", StringComparison.OrdinalIgnoreCase))
                return __exception;
            if (!string.Equals(anim, "FIN", StringComparison.OrdinalIgnoreCase))
                return __exception;
            if (___se_count != 1)
                return __exception;

            Kinoko oya = Traverse.Create(__instance).Field("oya").GetValue<Kinoko>();
            if (oya == null)
            {
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                    Plugin.Log?.LogWarning("[Pregnancy.MushroomERO] climax recover: oya is null");
                return SwallowOrgasmFlashNre(__exception);
            }

            int faction = PregnancySourceResolver.Resolve(oya, out string diag);
            PregnancyPartnerTrackerPatch.LastFaction = faction;
            PregnancyPartnerTrackerPatch.LastUnscaledTime = Time.unscaledTime;

            int semenValue = oya._Semenvalue;
            float variance = UnityEngine.Random.Range(0.8f, 1.2f);
            int mlAmount = Mathf.CeilToInt((float)semenValue * variance);

            oya.Nakadasi(mlAmount);
            try
            {
                oya.Library_Naka(oya._LibraryID, mlAmount);
            }
            catch
            {
                // Library array issues are handled by EnemyLibraryEroStatusGuard; never block deposit.
            }

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                Plugin.Log?.LogInfo(
                    "[Pregnancy.MushroomERO] FIN SE climax recovered after "
                    + __exception.GetType().Name
                    + " -> Nakadasi ml=" + mlAmount
                    + " faction=" + faction
                    + " (" + diag + ")");
            }
            else
            {
                Plugin.Log?.LogInfo(
                    "[Pregnancy.MushroomERO] FIN climax Nakadasi recovered (ml=" + mlAmount + ")");
            }

            return SwallowOrgasmFlashNre(__exception);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.MushroomERO] recover failed: " + ex.Message);
            return __exception;
        }
    }

    private static Exception SwallowOrgasmFlashNre(Exception ex)
    {
        // Allow Spine OnEvent chain to continue; the flash failure is non-fatal once Nakadasi ran.
        if (ex is NullReferenceException)
            return null;
        return ex;
    }
}

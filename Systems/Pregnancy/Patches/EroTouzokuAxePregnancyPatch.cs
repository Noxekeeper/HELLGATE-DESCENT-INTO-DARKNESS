using System;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// TouzokuAxe / EroTouzokuAXE creampie timing.
///
/// Vanilla deposits on <c>SE</c> while <c>AnimationName == "FIN"</c> and <c>se_count == 1</c>
/// (Orgsm → Nakadasi → Library_Naka). The old HellGate patch wrongly keyed on Spine event
/// name <c>"FIN"</c> (the pre-climax speed-up / SetAnimation transition), so the womb meter
/// jumped at the start of the FIN sequence instead of the creampie beat.
///
/// Now: only recover Nakadasi if that climax SE aborts (same WhiteFadeIn NRE class as Kinoko).
/// Successful vanilla Nakadasi is already tracked by <see cref="PregnancyPartnerTrackerPatch"/>.
/// </summary>
[HarmonyPatch(typeof(EroTouzokuAXE), "OnEvent")]
internal static class EroTouzokuAxePregnancyPatch
{
    [HarmonyFinalizer]
    private static Exception Finalizer(
        EroTouzokuAXE __instance,
        Spine.Event e,
        SkeletonAnimation ___myspine,
        int ___se_count,
        Exception __exception)
    {
        try
        {
            if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
                return __exception;

            if (__exception == null)
                return null;

            string ev = e != null ? e.ToString() : string.Empty;
            string anim = ___myspine != null ? (___myspine.AnimationName ?? string.Empty) : string.Empty;

            // Must match vanilla SE branch under anim FIN (NOT the top-level event named "FIN").
            if (!string.Equals(ev, "SE", StringComparison.OrdinalIgnoreCase))
                return __exception;
            if (!string.Equals(anim, "FIN", StringComparison.OrdinalIgnoreCase))
                return __exception;
            if (___se_count != 1)
                return __exception;

            TouzokuAxe oya = Traverse.Create(__instance).Field("oya").GetValue<TouzokuAxe>();
            if (oya == null)
            {
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                    Plugin.Log?.LogWarning("[Pregnancy.EroTouzokuAXE] climax recover: oya is null");
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
                // Library array guard handles growth; never block deposit.
            }

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                Plugin.Log?.LogInfo(
                    "[Pregnancy.EroTouzokuAXE] FIN SE climax recovered after "
                    + __exception.GetType().Name
                    + " -> Nakadasi ml=" + mlAmount
                    + " faction=" + faction
                    + " (" + diag + ")");
            }
            else
            {
                Plugin.Log?.LogInfo(
                    "[Pregnancy.EroTouzokuAXE] FIN SE climax Nakadasi recovered (ml=" + mlAmount + ")");
            }

            return SwallowOrgasmFlashNre(__exception);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.EroTouzokuAXE] recover failed: " + ex.Message);
            return __exception;
        }
    }

    private static Exception SwallowOrgasmFlashNre(Exception ex)
    {
        if (ex is NullReferenceException)
            return null;
        return ex;
    }
}

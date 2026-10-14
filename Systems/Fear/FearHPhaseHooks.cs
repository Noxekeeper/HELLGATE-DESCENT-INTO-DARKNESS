using System;
using System.Reflection;
using HarmonyLib;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// H-phase Fear on vanilla spine events: the event name is looked up in the enemy's JSON <c>hPhases</c>.
/// </summary>
internal static class FearHPhaseCueHooks
{
    private static readonly FieldInfo KakashField =
        typeof(EroAnimation).GetField("kakash", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(goblinero), "OnEvent")]
    [HarmonyPostfix]
    private static void Goblin(Spine.Event e) => Cue("goblin", e);

    [HarmonyPatch(typeof(InquisitionRedERO), "OnEvent")]
    [HarmonyPostfix]
    private static void InquisitionRed(Spine.Event e) => Cue("InquisitionRED", e);

    [HarmonyPatch(typeof(MermanERO), "OnEvent")]
    [HarmonyPostfix]
    private static void BigMerman(Spine.Event e) => Cue("BigMerman", e);

    [HarmonyPatch(typeof(GorotukiERO), "OnEvent")]
    [HarmonyPostfix]
    private static void Gorotuki(Spine.Event e) => Cue("Gorotuki", e);

    [HarmonyPatch(typeof(MinotaurosuERO), "OnEvent")]
    [HarmonyPostfix]
    private static void Minotaurosu(Spine.Event e) => Cue("Minotaurosu", e);

    /// <summary>Kakasi cross: vanilla cue <c>next1</c> switches the clip to <c>start2</c>.</summary>
    [HarmonyPatch(typeof(EroAnimation), "OnEvent")]
    [HarmonyPostfix]
    private static void Kakasi(EroAnimation __instance, Spine.Event e)
    {
        if (e?.Data == null || !IsKakasiCross(__instance))
            return;
        if (string.Equals(e.Data.Name, "next1", StringComparison.OrdinalIgnoreCase))
            FearHPhases.Enter("Kakash", "start2");
    }

    internal static bool IsKakasiCross(EroAnimation instance)
    {
        if (instance == null || KakashField == null)
            return false;
        try
        {
            return KakashField.GetValue(instance) is Kakash;
        }
        catch
        {
            return false;
        }
    }

    private static void Cue(string enemyKey, Spine.Event e)
    {
        if (e?.Data != null)
            FearHPhases.Enter(enemyKey, e.Data.Name);
    }
}

/// <summary>
/// H-phase Fear on clip change: attaches <see cref="FearAnimPhaseTracker"/> to the H controller.
/// Only patch methods that exist on the target type — a missing one aborts this whole class.
/// </summary>
internal static class FearHPhaseTrackerHooks
{
    [HarmonyPatch(typeof(EroTouzoku), "Start")]
    [HarmonyPostfix]
    private static void TouzokuNormal(EroTouzoku __instance) => FearAnimPhaseTracker.AttachTo(__instance, "TouzokuNormal");

    [HarmonyPatch(typeof(EroTouzokuAXE), "Start")]
    [HarmonyPostfix]
    private static void TouzokuAxe(EroTouzokuAXE __instance) => FearAnimPhaseTracker.AttachTo(__instance, "TouzokuAxe");

    [HarmonyPatch(typeof(InquiBlackEro), "Start")]
    [HarmonyPostfix]
    private static void Inquisition(InquiBlackEro __instance) => FearAnimPhaseTracker.AttachTo(__instance, "Inquisition");

    [HarmonyPatch(typeof(SisiruiruiERO), "Start")]
    [HarmonyPostfix]
    private static void Sisiruirui(SisiruiruiERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "Sisiruirui");

    [HarmonyPatch(typeof(EroAnimation), "Start")]
    [HarmonyPostfix]
    private static void Kakasi(EroAnimation __instance)
    {
        if (FearHPhaseCueHooks.IsKakasiCross(__instance))
            FearAnimPhaseTracker.AttachTo(__instance, "Kakash");
    }

    [HarmonyPatch(typeof(SlaveBigAxeEro), "Start")]
    [HarmonyPostfix]
    private static void SlaveBigAxe(SlaveBigAxeEro __instance) => FearAnimPhaseTracker.AttachTo(__instance, "SlaveBigAxe");

    [HarmonyPatch(typeof(SinnerslaveCrossbowERO), "Start")]
    [HarmonyPostfix]
    private static void Dorei(SinnerslaveCrossbowERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "SinnerslaveCrossbow");

    /// <summary>Butcher reuses this controller (only the skeleton asset is swapped).</summary>
    [HarmonyPatch(typeof(SlaughterFatality), "Start")]
    [HarmonyPostfix]
    private static void Slaughterer(SlaughterFatality __instance) => FearAnimPhaseTracker.AttachTo(__instance, "Slaughterer");

    [HarmonyPatch(typeof(skeltonOozeERO), "Start")]
    [HarmonyPostfix]
    private static void SkeltonOoze(skeltonOozeERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "SkeltonOoze");

    [HarmonyPatch(typeof(MummyManERO), "Start")]
    [HarmonyPostfix]
    private static void MummyMan(MummyManERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "MummyMan");

    [HarmonyPatch(typeof(CoolmaidenERO), "Start")]
    [HarmonyPostfix]
    private static void Coolmaiden(CoolmaidenERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "Coolmaiden");

    [HarmonyPatch(typeof(UndeadERO), "Start")]
    [HarmonyPostfix]
    private static void Undead(UndeadERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "Undead");

    /// <summary>Wolf reuses the MummyDog prefab (skeleton swapped by WolfSkeletonLoader).</summary>
    [HarmonyPatch(typeof(MummyDogERO), "Start")]
    [HarmonyPostfix]
    private static void MummyDog(MummyDogERO __instance) => FearAnimPhaseTracker.AttachTo(__instance, "MummyDog");

    /// <summary>GA wolf boss fatality (skeleton kyoukainokemono_kitune_ero2).</summary>
    [HarmonyPatch(typeof(GABOSS2Fatality), "Start")]
    [HarmonyPostfix]
    private static void WolfBoss(GABOSS2Fatality __instance) => FearAnimPhaseTracker.AttachTo(__instance, "WolfBoss");
}

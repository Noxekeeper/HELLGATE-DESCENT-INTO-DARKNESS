using System;
using HarmonyLib;
using Spine.Unity;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using System.Reflection;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// SlaveBigAxe / OtherSlavebigAxe H-scene dialogue + MindBroken hooks only.
/// This enemy is NOT part of the gangbang handoff chain.
/// </summary>
class SlaveBigAxePassLogic
{
    [HarmonyPatch(typeof(SlaveBigAxeEro), "OnEvent")]
    [HarmonyPostfix]
    private static void SlaveBigAxePass(SlaveBigAxeEro __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return;
            }

            // Illusive rescue event — vanilla dialogue / fade only.
            if (SlaveBigAxeIllusiveEventGate.ShouldSkipHellGateLogic())
            {
                return;
            }

            // MeatArmor owns Spine during post-fade patrol — skip H dialogue on FIN/IDLE stops.
            if (NoREroMod.Patches.Enemy.SlaveBigAxeMeatArmor.SlaveBigAxeMeatArmorRuntime.IsSwapped(__instance))
            {
                return;
            }

            var spine = GetSpine(__instance);
            if (spine == null)
            {
                return;
            }

            string currentAnim = spine.AnimationName ?? string.Empty;
            if (!IsHAnimation(currentAnim))
            {
                return;
            }

            string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
            int seCount = __instance.se_count;

            try
            {
                ProcessDialogueEvents(__instance, currentAnim, eventName, seCount);

                NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                    __instance,
                    currentAnim,
                    eventName,
                    seCount);
            }
            catch
            {
            }

            MindBrokenSystem.ProcessAnimationEvent(__instance, currentAnim, eventName);
        }
        catch
        {
        }
    }

    private static SkeletonAnimation GetSpine(SlaveBigAxeEro instance)
    {
        var spineField = typeof(SlaveBigAxeEro).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance)
                      ?? typeof(SlaveBigAxeEro).GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
        return spineField?.GetValue(instance) as SkeletonAnimation;
    }

    private static readonly string[] PhaseEventNames =
    {
        "START",
        "ERO", "ERO1", "ERO2", "ERO3", "ERO4",
        "FIN", "FIN2",
        "JIGO", "JIGO2",
        "JIGOERO", "JIGOERO2", "JIGOERO3", "JIGOERO4",
        "JIGOFIN", "JIGOFIN2",
        "JIGOPOST3", "JIGOPOST4",
        "JIGOPOSTERO", "JIGOPOSTERO2", "JIGOPOSTERO3",
        "JIGOPOSTFIN", "JIGOPOSTFIN2",
        "JIGOTOFADE",
        "ZGAMEOVER", "ZGAMEOVER2LOOP",
        "4ERO"
    };

    private static bool IsHAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return false;
        }

        foreach (string hAnim in PhaseEventNames)
        {
            if (animationName.Equals(hAnim, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPhaseEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            return false;
        }

        foreach (string phase in PhaseEventNames)
        {
            if (eventName.Equals(phase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ProcessDialogueEvents(SlaveBigAxeEro enemy, string currentAnim, string eventName, int seCount)
    {
        // Spine often fires the NEXT phase event while AnimationName is still the previous clip
        // (e.g. event "JIGO" during "FIN2"). Look up phrases by the phase event name.
        if (IsPhaseEvent(eventName))
        {
            NoREroMod.Systems.Dialogue.SlaveBigAxeHSceneDialogues.ProcessHSceneEvent(
                enemy, eventName, eventName, 0);
            return;
        }

        // SE / other ticks against the currently playing animation
        if (!string.IsNullOrEmpty(eventName) &&
            eventName.StartsWith("SE", StringComparison.OrdinalIgnoreCase))
        {
            NoREroMod.Systems.Dialogue.SlaveBigAxeHSceneDialogues.ProcessHSceneEvent(
                enemy, currentAnim, eventName, seCount);
        }
    }

    /// <summary>No-op kept for call sites that reset handoff state lists.</summary>
    internal static void ResetAll()
    {
    }
}

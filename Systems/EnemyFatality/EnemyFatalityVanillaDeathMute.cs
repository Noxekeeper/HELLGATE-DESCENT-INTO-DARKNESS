using System;
using DarkTonic.MasterAudio;
using HarmonyLib;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Blocks / stops vanilla MasterAudio <c>death1</c> during HeavyCritical so only
/// Quick Death WAVs play. Mirrors HellTraps death-audio blocking, scoped to HC.
/// </summary>
internal static class EnemyFatalityVanillaDeathMute
{
    private static bool _applied;
    private static bool _blockDeath1;

    internal static void Apply(Harmony harmony)
    {
        if (_applied || harmony == null)
            return;

        try
        {
            var play7 = AccessTools.Method(
                typeof(MasterAudio),
                "PlaySound",
                new[]
                {
                    typeof(string), typeof(float), typeof(UnityEngine.Transform),
                    typeof(float), typeof(UnityEngine.Transform), typeof(bool), typeof(bool),
                });
            if (play7 != null)
            {
                harmony.Patch(
                    play7,
                    prefix: new HarmonyMethod(
                        typeof(EnemyFatalityVanillaDeathMute),
                        nameof(PlaySound_Prefix)));
            }

            var play2 = AccessTools.Method(
                typeof(MasterAudio),
                "PlaySound",
                new[] { typeof(string), typeof(float) });
            if (play2 != null)
            {
                harmony.Patch(
                    play2,
                    prefix: new HarmonyMethod(
                        typeof(EnemyFatalityVanillaDeathMute),
                        nameof(PlaySound_Prefix)));
            }

            _applied = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "[EnemyFatality] Vanilla death1 mute patches failed: " + ex.Message);
        }
    }

    /// <summary>Call when HeavyCritical fatality starts (before/alongside ForceVanillaDeath).</summary>
    internal static void BeginHeavyCritical()
    {
        _blockDeath1 = true;
        try
        {
            MasterAudio.StopAllOfSound("death1");
        }
        catch
        {
        }
    }

    internal static void End()
    {
        _blockDeath1 = false;
    }

    private static bool PlaySound_Prefix(string soundName)
    {
        if (!_blockDeath1 || string.IsNullOrEmpty(soundName))
            return true;

        if (string.Equals(soundName, "death1", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}

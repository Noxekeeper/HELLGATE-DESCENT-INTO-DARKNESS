using System;
using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.Camera;
using NoREroMod.Systems.Effects;
using System.Reflection;

namespace NoREroMod.Patches.Effects
{
    /// <summary>
    /// Shows pregnancy PNG clip (Pregnant_action_000..095) when creampie gauge starts growing
    /// (PlayerStatus.CreampieVal_UI). Banner shown only on conception, not at 100%.
    /// </summary>
    [HarmonyPatch(typeof(Buff), "CreampieTime")]
    internal static class PregnancyClipTrigger
    {
        private const string PregnantClipFolder = @"Pregnant\Pregnant_action\Pregnant_action";
        private static bool _shown = false;
        private static bool _pending = false;

        // Fires on each creampie update, but doesn't show at 100% - banner only on conception
        [HarmonyPostfix]
        private static void Postfix(Buff __instance)
        {
            try
            {
                // Access pl (PlayerStatus)
                var pl = Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>();
                if (pl == null) return;

                // BadstatusVal[2] - creampie/pregnancy gauge
                float[] bad = Traverse.Create(pl).Field("_BadstatusVal").GetValue<float[]>();
                if (bad == null || bad.Length < 3) return;

                // Reset flag if gauge dropped again (after reset)
                if (bad[2] <= 0.01f)
                {
                    _shown = false;
                    _pending = false;
                    return;
                }

            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[PregnancyClipTrigger] error: {ex}");
            }
        }

        // Fires when creampie gauge starts growing (enemy climax event)
        [HarmonyPatch(typeof(PlayerStatus), "CreampieVal_UI")]
        [HarmonyPostfix]
        private static void OnCreampieStart(PlayerStatus __instance)
        {
            try
            {
                // Check if H-scene is active
                var player = GameObject.FindWithTag("Player")?.GetComponent<playercon>();
                if (player == null || !player.eroflag || player.erodown == 0)
                {
                    return; // H-scene not active, ignore creampie events
                }
                
                _shown = false;
                _pending = false;
                ShowClipWithBlackBgSync();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[PregnancyClipTrigger] creampie start error: {ex}");
            }
        }

        private static void ShowClipWithBlackBgSync()
        {
            // Exit if already shown
            if (_shown) return;
            if (HSceneBlackBackgroundSystem.IsActive)
            {
                _pending = true;
                HSceneBlackBackgroundSystem.OnDeactivated += ShowPending;
                return;
            }
            // Otherwise show immediately
            DoShowClip();
        }

        private static void DoShowClip()
        {
            try
            {
                HSceneCameraController.Instance?.GetCumDisplay()?.ShowClimax("pregnancy", PregnantClipFolder);
                _shown = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[PregnancyClipTrigger] show error: {ex}");
            }
        }

        private static void ShowPending()
        {
            try
            {
                HSceneBlackBackgroundSystem.OnDeactivated -= ShowPending;
                if (_pending && !_shown)
                {
                    DoShowClip();
                }
            }
            finally
            {
                _pending = false;
            }
        }
    }
}


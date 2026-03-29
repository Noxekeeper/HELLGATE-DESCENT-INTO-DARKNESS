using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;

namespace NoREroMod;

/// <summary>
/// Disables camera shake during struggle QTE (Hellachaz).
/// Identifies struggle shake by duration=0.2f, vibrato=8, randomness=0.
/// </summary>
class StruggleCameraShakeDisabler {

    [HarmonyPatch(typeof(ProCamera2DShake), nameof(ProCamera2DShake.Shake), new System.Type[] {
        typeof(float), typeof(Vector2), typeof(int), typeof(float), typeof(float), typeof(Vector3), typeof(float), typeof(bool)
    })]
    [HarmonyPrefix]
    static bool BlockStruggleShake(float duration, int vibrato, float randomness) {
        if (!(Plugin.disableStruggleCameraShake?.Value ?? true)) {
            return true;
        }

        // Hellachaz struggle shake: duration=0.2f, vibrato=8, randomness=0
        const float kStruggleDuration = 0.2f;
        const int kStruggleVibrato = 8;
        const float kStruggleRandomness = 0f;

        if (Mathf.Approximately(duration, kStruggleDuration) && vibrato == kStruggleVibrato && Mathf.Approximately(randomness, kStruggleRandomness)) {
            return false;
        }

        return true;
    }
}

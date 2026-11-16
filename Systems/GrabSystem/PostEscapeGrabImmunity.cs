using UnityEngine;

namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// HellGate-owned post-escape grab/knockdown shield (unscaled time).
/// Mirrors <see cref="NoREroMod.Systems.Rage.RageSystem.IsGrabKnockdownImmuneWhileRageActive"/>:
/// GrabViaAttack, collision elite grab, and power-knockdown→DOWN grab are all blocked
/// while active. Armed from <c>StruggleSystem.startGrabInvul</c> (escape / struggle free).
/// </summary>
internal static class PostEscapeGrabImmunity
{
    private static float _untilUnscaled = -1f;

    /// <summary>True while within the post-escape window.</summary>
    internal static bool IsActive =>
        _untilUnscaled > 0f && Time.unscaledTime < _untilUnscaled;

    internal static float RemainingSeconds
    {
        get
        {
            if (!IsActive)
                return 0f;
            return Mathf.Max(0f, _untilUnscaled - Time.unscaledTime);
        }
    }

    /// <summary>Arm / refresh from escape. Duration from <c>[GrabSystemNG] PostEscapeGrabInvulSeconds</c>.</summary>
    internal static void ArmFromEscape()
    {
        float seconds = Plugin.postEscapeGrabInvulSeconds?.Value ?? 3f;
        if (seconds <= 0f)
        {
            Clear();
            return;
        }

        // Refresh window (startGrabInvul may fire multiple postfixes per escape).
        float until = Time.unscaledTime + seconds;
        if (until > _untilUnscaled)
            _untilUnscaled = until;
    }

    internal static void Clear()
    {
        _untilUnscaled = -1f;
    }
}

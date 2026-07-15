using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// After birth spine JIGO, reset SP and require struggle clicks + full SP before standing.
/// Does not re-lock <see cref="playercon._easyESC"/> — that would block NoREroMod/QTE input.
/// </summary>
internal static class BirthRecoveryStruggleState
{
    private static bool _active;
    private static bool _struggleInputReceived;
    private static bool _standPermitted;

    internal static bool IsActive => _active;

    internal static void OnBirthJigo(playercon player, PlayerStatus status)
    {
        _active = true;
        _struggleInputReceived = false;
        _standPermitted = false;

        if (player != null)
        {
            try { Traverse.Create(player).Field("downup").SetValue(0); } catch { }
        }

        if (status != null)
        {
            status.Sp = 0f;
            status._SOUSA = true;
            status._SOUSAMNG = true;
            StruggleSystem.setStruggleLevel(-1);
        }
    }

    internal static void NotifyStruggleInput()
    {
        if (!_active)
            return;

        _struggleInputReceived = true;
    }

    internal static bool IsReadyToStand(PlayerStatus status)
    {
        if (!_active || _standPermitted)
            return true;

        if (!_struggleInputReceived || status == null)
            return false;

        return status.Sp >= status.AllMaxSP();
    }

    internal static bool CanForceStruggleEscape(playercon player, PlayerStatus status)
    {
        if (!_active)
            return true;

        if (!PlayerEroContextUtility.IsActivePregnancyBirth(player))
            return true;

        return IsReadyToStand(status);
    }

    internal static void PermitStandAndReleaseEasyEsc(playercon player)
    {
        _standPermitted = true;
        if (player != null)
            player._easyESC = false;
    }

    internal static void EndRecovery()
    {
        _active = false;
        _struggleInputReceived = false;
        _standPermitted = false;
    }
}

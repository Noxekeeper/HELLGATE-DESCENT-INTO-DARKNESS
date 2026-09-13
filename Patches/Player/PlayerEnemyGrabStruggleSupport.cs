using HarmonyLib;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.EventCore.Host;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Vanilla struggle / HellGate QTE require <see cref="PlayerStatus._SOUSA"/> during enemy grabs.
/// Many ERO Start() paths and intentional control locks leave it false; field bosses also skip
/// <c>KeepPlayerControl</c> while <c>eroflag</c> is set.
/// </summary>
internal static class PlayerEnemyGrabStruggleSupport
{
    internal static void Process(playercon player, PlayerStatus status)
    {
        if (player == null || status == null)
            return;

        if (EventCorePause.IsFrozen || player._Death)
            return;

        if (EventCoreHost.IsAnyConsentStruggleLocked())
        {
            EventCoreHost.ActiveHandoffHost?.ApplyConsentStruggleLockIfActive();
            return;
        }

        if (BirthRecoveryStruggleState.IsActive && PlayerEroContextUtility.IsActivePregnancyBirth(player))
        {
            EnableStruggleFlags(player, status);
            return;
        }

        if (PlayerEroContextUtility.ShouldBlockEnemyStruggleAutomation(player))
            return;

        if (VanillaStoryEventInputGuard.IsStoryEventFakeEroflag(player))
            return;

        if (!IsEnemyGrabStruggleContext(player))
            return;

        EnableStruggleFlags(player, status);
    }

    internal static void PrepareForGrab(playercon player, PlayerStatus status)
    {
        if (player == null || status == null)
            return;

        EnableStruggleFlags(player, status);
        StruggleSystem.setStruggleLevel(-1);
    }

    internal static void EnableStruggleFlags(playercon player, PlayerStatus status)
    {
        if (player == null || status == null)
            return;

        // Never (re-)enable struggle control while dead. Several callers run on the downed path
        // every frame (e.g. StrugglePotionPrepareFunNowdamagePatch on fun_nowdamage). Without this
        // guard they resurrect _SOUSA after death, so the player could keep filling SP and the
        // "Struggle Out!" window never closed (it, the QTE and vanilla get-up all gate on _SOUSA).
        if (player._Death || status.Hp <= 0f)
            return;

        // Vanilla NOTESCAPE / fade lockouts and pregnancy birth overlays: leave _easyESC alone.
        // Clearing it here re-opened Struggle Out / QTE during intentional no-escape phases.
        if (player._easyESC && !BirthRecoveryStruggleState.IsActive)
            return;

        if (!status._SOUSA)
            status._SOUSA = true;

        if (!status._SOUSAMNG)
            status._SOUSAMNG = true;

        if (player._easyESC)
            player._easyESC = false;
    }

    private static bool IsEnemyGrabStruggleContext(playercon player)
    {
        if (player.erodown == 0 && !player.eroflag)
            return false;

        if (!PlayerEroContextUtility.IsAnyEnemyEroActive())
            return false;

        return player.eroflag || player.erodown != 0;
    }
}

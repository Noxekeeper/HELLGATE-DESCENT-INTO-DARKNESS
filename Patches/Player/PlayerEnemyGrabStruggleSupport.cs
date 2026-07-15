using HarmonyLib;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.EventCore.Host;
using UnityEngine;

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
            UnlockImpossibleStruggleLevel();
            return;
        }

        if (PlayerEroContextUtility.ShouldBlockEnemyStruggleAutomation(player))
            return;

        if (VanillaStoryEventInputGuard.IsStoryEventFakeEroflag(player))
            return;

        if (!IsEnemyGrabStruggleContext(player))
            return;

        EnableStruggleFlags(player, status);
        UnlockImpossibleStruggleLevel();
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

        // Pregnancy birth / badstatus overlays: _easyESC blocks mash escape until birth spine JIGO.
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

    // Anti-soft-lock only. NoREroMod uses struggle level 10 ("impossible") as the intended
    // per-animation penetration/orgasm lockout, so we must NOT clear it every frame — that is what
    // kept the Struggle Out window permanently open. We let NoREroMod's OnEvent phases drive the
    // window and only force it open if level 10 is genuinely stuck far longer than any real phase.
    private const float ImpossibleLevelMaxSeconds = 15f;
    private static float _impossibleLevelSince = -1f;

    private static void UnlockImpossibleStruggleLevel()
    {
        try
        {
            var levelField = AccessTools.Field(typeof(StruggleSystem), "struggleLevel");
            if (levelField == null)
                return;

            if ((int)levelField.GetValue(null) != 10)
            {
                _impossibleLevelSince = -1f; // not locked — reset the stuck timer
                return;
            }

            float now = Time.unscaledTime;
            if (_impossibleLevelSince < 0f)
            {
                _impossibleLevelSince = now; // entered the lockout this frame; let NoREroMod run it
                return;
            }

            if (now - _impossibleLevelSince >= ImpossibleLevelMaxSeconds)
            {
                // Stuck at 10 for an unreasonable time (animation never reached its open phase).
                StruggleSystem.setStruggleLevel(-1);
                _impossibleLevelSince = -1f;
            }
        }
        catch
        {
        }
    }
}

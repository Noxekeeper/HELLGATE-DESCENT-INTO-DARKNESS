using NoREroMod.Patches.HellTraps;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Safety net after struggle escape / trap exit: many ERO Start() paths set
/// <see cref="PlayerStatus._SOUSA"/> = false and never restore it when the player breaks out.
/// Without _SOUSA, vanilla <c>atk_fun</c> ignores attack input entirely.
/// </summary>
internal static class PlayerCombatControlRecovery
{
    private static bool _wasEroFlagLastFrame;

    internal static void Process(playercon player, PlayerStatus status, bool eroflag)
    {
        if (player == null || status == null)
            return;

        bool leftHScene = _wasEroFlagLastFrame && !eroflag;
        _wasEroFlagLastFrame = eroflag;

        if (leftHScene)
        {
            // Enemy handoff sets erodown=1 on purpose (lie down, mash to stand). Full struggle-escape
            // cleanup clears erodown and would make the heroine pop upright.
            if (player.erodown != 0)
                HSceneEscapeStateCleanup.RestoreVisualsOnly(player);
            else
                HSceneEscapeStateCleanup.RestoreAfterStruggleEscape(player);
        }

        if (EventCorePause.IsFrozen || player._Death)
            return;

        // Insomnia bar and other additive EV scenes: eroflag clears before REstrat unloads _BossScene.
        if (VanillaCutsceneSceneGuard.IsAdditiveEvSceneActive())
            return;

        // Solo pleasure (FEEL*): vanilla blocks combat via nowdamage — not a stuck H-scene escape.
        if (PlayerEroContextUtility.IsSoloPleasureState(player))
            return;

        if (eroflag)
            return;

        if (player.erodown != 0)
            return;

        TryClearStaleTrapSuppression();

        // Vanilla TALK triggers, boss/EV dialog, shops, and menus intentionally clear _SOUSA.
        // Do not "unstick" combat flags (nowdamage, Attacknow, timeScale) while that lock is active.
        bool vanillaControlLock = IsVanillaIntentionalControlLock(status);

        // leftHScene and explicit RestoreAfterStruggleEscape() still restore control after H-scene escape.
        bool needsRecovery =
            leftHScene ||
            (!vanillaControlLock && (
                player.nowdamage ||
                player._eroflag2 ||
                IsDownLikeState(player.state) ||
                (player.Attacknow && !LooksLikeAttackState(player.state)) ||
                (player.rigi2d != null && !player.rigi2d.simulated && (status._SOUSA || status._SOUSAMNG)) ||
                Time.timeScale == 0f));

        if (!needsRecovery)
            return;

        RestoreMovementControl(player, status);
        ClearStuckHSceneSecondaryFlags(player);
        ClearStuckCombatFlags(player);
        FixStuckTimeScale();
    }

    internal static void RestoreAfterStruggleEscape()
    {
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (player == null || status == null || player._Death)
            return;

        HSceneEscapeStateCleanup.RestoreAfterStruggleEscape(player);

        RestoreMovementControl(player, status);
        ClearStuckHSceneSecondaryFlags(player);
        ClearStuckCombatFlags(player);
        FixStuckTimeScale();
    }

    private static void RestoreMovementControl(playercon player, PlayerStatus status)
    {
        if (!status._SOUSA)
            status._SOUSA = true;

        if (!status._SOUSAMNG)
            status._SOUSAMNG = true;

        // Struggle/QTE may call RestoreAfterStruggleEscape while eroflag/erodown are still set
        // (trap/enemy abort runs later in the same escape). Re-enabling physics here drops the
        // invisible mid-air body (Ivy etc.) before H cleanup. Keep SOUSA, defer simulated —
        // vanilla / Trapdata abort / playercon Update turn physics back on after H ends.
        if (player.eroflag || player.erodown != 0)
            return;

        if (player.rigi2d != null && !player.rigi2d.simulated)
            player.rigi2d.simulated = true;
    }

    private static void ClearStuckHSceneSecondaryFlags(playercon player)
    {
        if (player._eroflag2)
            player._eroflag2 = false;
    }

    private static void ClearStuckCombatFlags(playercon player)
    {
        if (player.nowdamage && player.erodown == 0)
            player.nowdamage = false;

        if (IsDownLikeState(player.state))
            player.state = "IDLE";

        if (player.Attacknow && !LooksLikeAttackState(player.state))
        {
            player.Attacknow = false;
            player.Actstate = false;
            player.Atkcount = 0;
            player.Atkcombo = 0;
        }
    }

    private static void FixStuckTimeScale()
    {
        if (Time.timeScale == 0f && !EventCorePause.IsFrozen)
            Time.timeScale = 1f;
    }

    private static void TryClearStaleTrapSuppression()
    {
        if (LethalMagicTrapDeathDisplay.HasActiveClip || LethalCocoonTrapDeathDisplay.HasActiveClip)
            return;

        if (LethalMagicTrapDeathContext.IsEroSuppressionActive &&
            !LethalMagicTrapDeathContext.IsCustomDeathActive &&
            !LethalMagicTrapDeathContext.IsLethalHitInProgress)
        {
            LethalMagicTrapDeathContext.ClearMagicHitState();
        }

        if (LethalCocoonTrapDeathContext.IsEroSuppressionActive &&
            !LethalCocoonTrapDeathContext.IsCustomDeathActive &&
            !LethalCocoonTrapDeathContext.IsLethalHitInProgress)
        {
            LethalCocoonTrapDeathContext.ClearStaleEroSuppression();
        }
    }

    /// <summary>
    /// Vanilla dialogue / shop / cutscene paths set <see cref="PlayerStatus._SOUSA"/> = false on purpose
    /// (TalkStartMng.Talkset, DialogController*, BossTouzoku.flagCall_Dialog, etc.).
    /// </summary>
    internal static bool IsVanillaIntentionalControlLock(PlayerStatus status)
    {
        if (EventCorePause.IsFrozen)
            return true;

        if (status != null && !status._SOUSA)
            return true;

        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        return player != null && VanillaStoryEventInputGuard.ShouldSuppressCombatInput(player, status);
    }

    private static bool IsDownLikeState(string state)
    {
        return state == "DOWN";
    }

    private static bool LooksLikeAttackState(string state)
    {
        if (string.IsNullOrEmpty(state))
            return false;

        return state.StartsWith("ATK") ||
               state.StartsWith("AIRATK") ||
               state == "STAB" ||
               state == "THROW" ||
               state == "GUN" ||
               state == "GUN2";
    }
}

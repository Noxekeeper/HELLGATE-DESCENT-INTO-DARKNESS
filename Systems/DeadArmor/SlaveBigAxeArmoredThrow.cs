using System.Collections;
using System.Reflection;
using HarmonyLib;
using NoREroMod;
using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>
/// SlaveBigAxe + NikuArmor: replace GrabViaAttack EliteGrab snap with a short hold,
/// then vanilla knockback (damedir + nockbackspeed via fun_nowdamage_move).
/// </summary>
internal static class SlaveBigAxeArmoredThrow
{
    private static FieldInfo _nikuArmorField;
    private static FieldInfo _dirField;
    private static FieldInfo _nockbackspeedField;
    private static Coroutine _slowmoRoutine;
    private static Coroutine _throwRoutine;
    private static bool _inProgress;

    internal static bool TryThrowInsteadOfGrab(EnemyDate attacker, playercon player)
    {
        if (attacker == null || player == null)
            return false;

        if (DeadArmorConfig.ArmoredGrabThrowEnable == null ||
            !DeadArmorConfig.ArmoredGrabThrowEnable.Value)
            return false;

        if (!(attacker is SlaveBigAxe))
            return false;

        if (NoREroMod.Patches.Enemy.SlaveBigAxeIllusiveEventGate.ShouldSkipHellGateLogic())
            return false;

        if (!IsNikuArmorActive(attacker))
            return false;

        if (_inProgress)
            return true;

        if (player.eroflag || player.erodown != 0)
            return false;

        float awayVel = DeadArmorConfig.ArmoredGrabThrowAwayVelocity != null
            ? DeadArmorConfig.ArmoredGrabThrowAwayVelocity.Value
            : 24f;
        float upVel = DeadArmorConfig.ArmoredGrabThrowUpVelocity != null
            ? DeadArmorConfig.ArmoredGrabThrowUpVelocity.Value
            : 0f;
        float holdSec = DeadArmorConfig.ArmoredGrabThrowHoldSeconds != null
            ? DeadArmorConfig.ArmoredGrabThrowHoldSeconds.Value
            : 0.7f;
        float holdPull = DeadArmorConfig.ArmoredGrabThrowHoldPull != null
            ? DeadArmorConfig.ArmoredGrabThrowHoldPull.Value
            : 0.85f;
        float holdLift = DeadArmorConfig.ArmoredGrabThrowHoldLift != null
            ? DeadArmorConfig.ArmoredGrabThrowHoldLift.Value
            : 0.5f;

        int throwDir = ResolveThrowDirection(attacker, player);

        StartArmoredThrowSlowmo(attacker);
        DrainSpIfNeeded(player);

        if (_throwRoutine != null)
        {
            try { attacker.StopCoroutine(_throwRoutine); } catch { }
            _throwRoutine = null;
        }

        _inProgress = true;
        _throwRoutine = attacker.StartCoroutine(
            HoldThenThrowCoroutine(attacker, player, throwDir, awayVel, upVel, holdSec, holdPull, holdLift));

        return true;
    }

    private static void DrainSpIfNeeded(playercon player)
    {
        PlayerStatus status = null;
        try
        {
            GameObject gc = GameObject.FindGameObjectWithTag("GameController");
            if (gc != null)
                status = gc.GetComponent<PlayerStatus>();
        }
        catch
        {
        }

        if (status != null && player.erodown != 1)
            status.Sp = 0f;
    }

    /// <summary>Hold/pull phase, then ImmediatelyERO + nockbackspeed knockback.</summary>
    private static IEnumerator HoldThenThrowCoroutine(
        EnemyDate attacker,
        playercon player,
        int throwDir,
        float awayVel,
        float upVel,
        float holdRealSeconds,
        float holdPull,
        float holdLift)
    {
        try
        {
            Vector3 holdPos = player.transform.position;
            if (attacker != null)
            {
                float ax = attacker.transform.position.x;
                float px = player.transform.position.x;
                float side = px >= ax ? 1f : -1f;
                if (holdPull > 0.01f)
                    holdPos.x = ax + side * holdPull;
                holdPos.y = player.transform.position.y + Mathf.Max(0f, holdLift);
            }
            else
            {
                holdPos.y += Mathf.Max(0f, holdLift);
            }

            // Kickback kind 6 zeros horizontal velocity in fun_nowdamage_move (soft lock).
            player.nowdamage = true;
            player.ToKickbackkind = 6;
            player.damedir = throwDir;

            float holdEnd = Time.realtimeSinceStartup + Mathf.Max(0f, holdRealSeconds);
            while (Time.realtimeSinceStartup < holdEnd)
            {
                if (attacker == null || player == null)
                    yield break;

                player.transform.position = holdPos;
                if (player.rigi2d != null)
                {
                    player.rigi2d.simulated = true;
                    player.rigi2d.velocity = Vector2.zero;
                }

                player.nowdamage = true;
                player.ToKickbackkind = 6;
                yield return null;
            }

            if (player == null)
                yield break;

            // ImmediatelyERO sets kind/erodown but not damedir/nockbackspeed — set those explicitly.
            player.ImmediatelyERO();
            player.damedir = throwDir;
            player.ToKickbackkind = 3;
            SetNockbackSpeed(player, Mathf.Max(0.1f, awayVel));

            try { player.fun_costume(); } catch { }
            try { StruggleSystem.setStruggleLevel(-1); } catch { }

            float savedGravity = 1f;
            bool flat = Mathf.Abs(upVel) < 0.01f;
            if (player.rigi2d != null)
            {
                player.rigi2d.simulated = true;
                savedGravity = player.rigi2d.gravityScale;
                if (flat)
                    player.rigi2d.gravityScale = 0f;
                player.rigi2d.velocity = new Vector2(throwDir * awayVel, upVel);
            }

            DeadArmorConfig.LogDebug(
                "[DeadArmor] Armored throw dir=" + throwDir
                + " speed=" + awayVel
                + " hold=" + holdRealSeconds);

            if (flat && player.rigi2d != null)
            {
                float flatEnd = Time.realtimeSinceStartup + 0.55f;
                while (Time.realtimeSinceStartup < flatEnd && player != null && player.rigi2d != null)
                {
                    Vector2 v = player.rigi2d.velocity;
                    player.rigi2d.velocity = new Vector2(v.x, 0f);
                    yield return null;
                }

                if (player != null && player.rigi2d != null)
                    player.rigi2d.gravityScale = savedGravity;
            }
        }
        finally
        {
            _throwRoutine = null;
            _inProgress = false;
        }
    }

    private static void SetNockbackSpeed(playercon player, float speed)
    {
        try
        {
            if (_nockbackspeedField == null)
                _nockbackspeedField = AccessTools.Field(typeof(playercon), "nockbackspeed");
            if (_nockbackspeedField != null)
                _nockbackspeedField.SetValue(player, speed);
        }
        catch
        {
        }
    }

    private static void StartArmoredThrowSlowmo(EnemyDate attacker)
    {
        if (attacker == null)
            return;
        if (DeadArmorConfig.ArmoredGrabThrowSlowmo == null ||
            !DeadArmorConfig.ArmoredGrabThrowSlowmo.Value)
            return;

        float scale = DeadArmorConfig.ArmoredGrabThrowSlowmoTimeScale != null
            ? DeadArmorConfig.ArmoredGrabThrowSlowmoTimeScale.Value
            : 0.7f;
        float dur = DeadArmorConfig.ArmoredGrabThrowSlowmoDuration != null
            ? DeadArmorConfig.ArmoredGrabThrowSlowmoDuration.Value
            : 0.7f;
        if (dur <= 0f || scale <= 0f)
            return;

        if (_slowmoRoutine != null)
        {
            try { attacker.StopCoroutine(_slowmoRoutine); } catch { }
            _slowmoRoutine = null;
        }

        _slowmoRoutine = attacker.StartCoroutine(ArmoredThrowSlowmoCoroutine(attacker, scale, dur));
    }

    private static IEnumerator ArmoredThrowSlowmoCoroutine(EnemyDate host, float targetScale, float realSeconds)
    {
        float endTime = Time.realtimeSinceStartup + realSeconds;
        while (Time.realtimeSinceStartup < endTime)
        {
            if (host == null)
                break;
            if (Time.timeScale != targetScale && Time.timeScale != 0f)
                Time.timeScale = targetScale;
            yield return null;
        }

        if (Time.timeScale != 0f)
            Time.timeScale = 1f;
        _slowmoRoutine = null;
    }

    private static bool IsNikuArmorActive(EnemyDate attacker)
    {
        try
        {
            if (_nikuArmorField == null)
                _nikuArmorField = AccessTools.Field(typeof(SlaveBigAxe), "NikuArmor");
            if (_nikuArmorField == null)
                return false;
            return (bool)_nikuArmorField.GetValue(attacker);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Throw away from the slave based on relative X (fallback: enemy DIR / player.dir).</summary>
    private static int ResolveThrowDirection(EnemyDate attacker, playercon player)
    {
        float dx = player.transform.position.x - attacker.transform.position.x;
        if (Mathf.Abs(dx) > 0.05f)
            return dx >= 0f ? 1 : -1;

        try
        {
            if (_dirField == null)
                _dirField = AccessTools.Field(typeof(EnemyDate), "DIR");
            if (_dirField != null)
            {
                object raw = _dirField.GetValue(attacker);
                if (raw is int dir && dir != 0)
                    return dir > 0 ? 1 : -1;
            }
        }
        catch
        {
        }

        return player.dir >= 0f ? 1 : -1;
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// While a fatality session is active the player is hidden under a PNG clip.
/// Enemies are settled to IDLE and AI Behaviours are disabled until session end
/// so they do not attack the invisible player. Some profiles also hide the killer
/// (e.g. CrawlingCreatures / StillAlive) for the same window.
/// </summary>
internal static class EnemyFatalityEroSuppression
{
    private static readonly Dictionary<Type, EnemyStateAccess> EnemyStateCache =
        new Dictionary<Type, EnemyStateAccess>();

    private static readonly List<EnemyDate> DisabledAi = new List<EnemyDate>(32);
    private static readonly List<Renderer> HiddenKillerRenderers = new List<Renderer>(16);
    private static readonly List<SkeletonAnimation> HiddenKillerSpines = new List<SkeletonAnimation>(4);

    private static EnemyDate _hiddenKiller;
    private static FieldInfo _enmAtkNowField;
    private static FieldInfo _rigiField;
    private static float _nextEnemyScanUnscaledTime;
    private const float EnemyScanIntervalSeconds = 0.15f;

    private sealed class EnemyStateAccess
    {
        internal FieldInfo StateField;
        internal FieldInfo LookField;
        internal FieldInfo SpineField;
        internal object IdleState;
    }

    /// <summary>Called each player Update while a fatality session is active.</summary>
    internal static void Process(playercon player)
    {
        if (!EnemyFatalitySession.IsActive || player == null)
            return;

        PinPlayerBody(player);
        MaintainEnemyFreeze(forceImmediate: false);
    }

    internal static void PinPlayerBody(playercon player)
    {
        if (player == null)
            return;

        Rigidbody2D body = player.rigi2d;
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (player.erodown != 0)
            player.erodown = 0;

        if (player.nowdamage)
            player.nowdamage = false;

        if (player.eroflag)
            player.eroflag = false;

        string state = player.state;
        if (state == "DOWN" || state == "DAMAGE" || state == "DAMAGE2")
            player.state = "IDLE";
    }

    /// <summary>Immediate settle + AI disable at fatality start.</summary>
    internal static void SettleEnemiesOnce()
    {
        MaintainEnemyFreeze(forceImmediate: true);
    }

    /// <summary>Periodic maintain of enemy freeze while the clip is playing.</summary>
    internal static void MaintainEnemyFreeze(bool forceImmediate)
    {
        if (!EnemyFatalitySession.IsActive)
            return;

        if (!forceImmediate && Time.unscaledTime < _nextEnemyScanUnscaledTime)
            return;

        _nextEnemyScanUnscaledTime = Time.unscaledTime + EnemyScanIntervalSeconds;

        EnemyDate[] enemies = Object.FindObjectsOfType<EnemyDate>();
        for (int i = 0; i < enemies.Length; i++)
            FreezeOrDisableEnemy(enemies[i]);

        KeepKillerHidden();
    }

    /// <summary>
    /// Hide the killer enemy mesh/spine for profiles that bake the attacker into the PNG
    /// (e.g. StillAlive). Restored with <see cref="RestoreCombatAi"/> / session end.
    /// </summary>
    internal static void HideKillerIfNeeded(EnemyDate enemy, IEnemyFatalityProfile profile)
    {
        if (enemy == null || profile == null || !profile.HideKillerDuringClip)
            return;

        RestoreKillerVisuals();
        _hiddenKiller = enemy;

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            renderer.enabled = false;
            HiddenKillerRenderers.Add(renderer);
        }

        SkeletonAnimation[] spines = enemy.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null)
                continue;

            if (spine.enabled)
            {
                spine.enabled = false;
                HiddenKillerSpines.Add(spine);
            }

            if (spine.skeleton != null)
            {
                Color color = spine.skeleton.GetColor();
                if (color.a > 0.001f)
                    spine.skeleton.SetColor(new Color(color.r, color.g, color.b, 0f));
            }
        }

        EnemyFatalityConfig.LogDebug("[" + profile.Id + "] Killer visuals hidden");
    }

    private static void KeepKillerHidden()
    {
        if (_hiddenKiller == null && HiddenKillerRenderers.Count == 0)
            return;

        for (int i = 0; i < HiddenKillerRenderers.Count; i++)
        {
            Renderer renderer = HiddenKillerRenderers[i];
            if (renderer != null && renderer.enabled)
                renderer.enabled = false;
        }

        for (int i = 0; i < HiddenKillerSpines.Count; i++)
        {
            SkeletonAnimation spine = HiddenKillerSpines[i];
            if (spine == null)
                continue;

            if (spine.enabled)
                spine.enabled = false;

            if (spine.skeleton != null)
            {
                Color color = spine.skeleton.GetColor();
                if (color.a > 0.001f)
                    spine.skeleton.SetColor(new Color(color.r, color.g, color.b, 0f));
            }
        }
    }

    private static void RestoreKillerVisuals()
    {
        for (int i = 0; i < HiddenKillerRenderers.Count; i++)
        {
            Renderer renderer = HiddenKillerRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        for (int i = 0; i < HiddenKillerSpines.Count; i++)
        {
            SkeletonAnimation spine = HiddenKillerSpines[i];
            if (spine == null)
                continue;

            spine.enabled = true;
            if (spine.skeleton != null)
                spine.skeleton.SetColor(Color.white);
        }

        HiddenKillerRenderers.Clear();
        HiddenKillerSpines.Clear();
        _hiddenKiller = null;
    }

    /// <summary>Re-enable AI Behaviours disabled for this session.</summary>
    internal static void RestoreCombatAi()
    {
        RestoreKillerVisuals();

        for (int i = 0; i < DisabledAi.Count; i++)
        {
            EnemyDate enemy = DisabledAi[i];
            if (enemy != null)
                enemy.enabled = true;
        }

        DisabledAi.Clear();
    }

    private static void FreezeOrDisableEnemy(EnemyDate enemy)
    {
        if (enemy == null || enemy.eroflag)
            return;

        if (!enemy.enabled)
        {
            if (!DisabledAi.Contains(enemy))
                DisabledAi.Add(enemy);
            return;
        }

        EnemyStateAccess access = ResolveEnemyStateAccess(enemy.GetType());
        if (access == null || access.StateField == null)
            return;

        object currentState = access.StateField.GetValue(enemy);
        string stateName = currentState != null ? currentState.ToString() : string.Empty;

        if (string.Equals(stateName, "DEATH", StringComparison.OrdinalIgnoreCase))
            return;

        if (access.IdleState != null)
            access.StateField.SetValue(enemy, access.IdleState);

        if (access.LookField != null)
            access.LookField.SetValue(enemy, false);

        if (_enmAtkNowField == null)
            _enmAtkNowField = AccessTools.Field(typeof(EnemyDate), "enmATKnow");
        if (_enmAtkNowField != null)
            _enmAtkNowField.SetValue(enemy, false);

        if (_rigiField == null)
            _rigiField = AccessTools.Field(typeof(EnemyDate), "rigi2D");
        if (_rigiField != null)
        {
            Rigidbody2D body = _rigiField.GetValue(enemy) as Rigidbody2D;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        TryForceIdleSpine(enemy, access);

        enemy.enabled = false;
        if (!DisabledAi.Contains(enemy))
            DisabledAi.Add(enemy);
    }

    private static void TryForceIdleSpine(EnemyDate enemy, EnemyStateAccess access)
    {
        try
        {
            SkeletonAnimation spine = null;
            if (access.SpineField != null)
                spine = access.SpineField.GetValue(enemy) as SkeletonAnimation;

            if (spine == null)
                spine = enemy.GetComponentInChildren<SkeletonAnimation>(true);

            if (spine == null || spine.state == null)
                return;

            if (string.Equals(spine.AnimationName, "IDLE", StringComparison.OrdinalIgnoreCase))
                return;

            spine.state.SetAnimation(0, "IDLE", true);
        }
        catch
        {
            // Spine set is best-effort; missing IDLE clip is acceptable.
        }
    }

    private static EnemyStateAccess ResolveEnemyStateAccess(Type enemyType)
    {
        if (enemyType == null)
            return null;

        if (EnemyStateCache.TryGetValue(enemyType, out EnemyStateAccess cached))
            return cached;

        FieldInfo stateField = AccessTools.Field(enemyType, "state");
        Type stateEnum = enemyType.GetNestedType("enemystate", BindingFlags.Public | BindingFlags.NonPublic);
        if (stateField == null || stateEnum == null)
            return null;

        FieldInfo look = AccessTools.Field(enemyType, "Look")
            ?? AccessTools.Field(typeof(EnemyDate), "Look");

        FieldInfo spine = AccessTools.Field(enemyType, "myspine")
            ?? AccessTools.Field(enemyType, "spineanime")
            ?? AccessTools.Field(enemyType, "mySpine");

        var access = new EnemyStateAccess
        {
            StateField = stateField,
            LookField = look,
            SpineField = spine,
            IdleState = ParseState(stateEnum, "IDLE"),
        };

        EnemyStateCache[enemyType] = access;
        return access;
    }

    private static object ParseState(Type stateEnum, string name)
    {
        try
        {
            return Enum.Parse(stateEnum, name);
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.Rage;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using XftWeapon;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>
/// Field-spawn BossTouzoku: vanilla boss combat/AI after BattleStart bootstrap; only intro/death/scene hooks trimmed.
/// </summary>
internal static class BossTouzokuCustomRuntime
{
    private const string SuperTag = "<SUPER>";
    private const float VanillaMaxHp = 2000f;
    private const float VanillaMaxTough = 3f;
    internal const string CombatPatchVersion = "field-mob-v16-safe-eroanime";

    private static FieldInfo _wallMoveCameraField;
    private static FieldInfo _bossFlagField;
    private static FieldInfo _performanceFlagField;
    private static FieldInfo _dameColField;
    private static FieldInfo _meshField;
    private static FieldInfo _meshAllField;
    private static FieldInfo _erodataField;
    private static FieldInfo _eroComponentField;
    private static FieldInfo _erospineField;
    private static FieldInfo _xWeaponCompField;
    private static FieldInfo _xwepFlagField;
    private static FieldInfo _alleffectField;
    private static FieldInfo _bgmField;
    private static FieldInfo _bgmVolField;
    private static MethodInfo _eroStartSetMethod;
    private static MethodInfo _cameraGetComponentMethod;
    private static MethodInfo _eroCameraMethod;
    private static MethodInfo _spineLateUpdateMethod;
    private static int _visibilityDiagLogged;
    private static FieldInfo _hpUiField;
    private static FieldInfo _uiField;
    private static FieldInfo _hpBarField;
    private static FieldInfo _xWeaponField;
    private static FieldInfo _pro2dField;
    private static FieldInfo _playerStatusField;
    private static FieldInfo _playerField;
    private static FieldInfo _playerUiField;
    private static FieldInfo _objPlayerUiField;
    private static FieldInfo _jpNameField;
    private static FieldInfo _exclamationField;
    private static MethodInfo _startFunMethod;
    private static MethodInfo _stateMethod;
    private static MethodInfo _sousaMethod;
    private static MethodInfo _setStatusMethod;
    private static MethodInfo _setAnimationMethod;
    private static MethodInfo _atkResistArraySetMethod;
    private static MethodInfo _weaponDamageMethod;
    private static MethodInfo _damagePopMethod;
    private static MethodInfo _distanceFunMethod;
    private static MethodInfo _getMgDameFunMethod;
    private static MethodInfo _totalAtkValMethod;
    private static FieldInfo _mySpineField;
    private static FieldInfo _bloodField;
    private static FieldInfo _damegeIdField;

    private static readonly string[] IntroInvokeNames = { "next", "BattleStart" };

    private static void ResetVanillaHitTimeScale()
    {
        if (TimeSlowMoSystem.IsActive || Time.timeScale >= 1f)
            return;
        Time.timeScale = 1f;
    }

    internal static void PrepareSpawnedInstance(GameObject spawned)
    {
        if (spawned == null)
            return;

        if (spawned.activeSelf)
            spawned.SetActive(false);

        if (spawned.GetComponent<HellGateBossTouzokuCustomMarker>() == null)
            spawned.AddComponent<HellGateBossTouzokuCustomMarker>();

        spawned.name = BossTouzokuCustomStats.ObjectNameKey;

        DisableStoryIntroComponents(spawned);
        EnsureDeathSafeReferencesOnPrefab(spawned);

        Collider2D[] triggers = spawned.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            Collider2D col = triggers[i];
            if (col == null || !col.isTrigger)
                continue;

            if (col.GetComponent<BossStartFlagALL>() != null
                || col.GetComponent<BOSSEvMainDialog>() != null
                || col.GetComponent<BossPerformanceFlag>() != null
                || col.GetComponent<BossStartFlag>() != null)
            {
                col.enabled = false;
            }
        }

        if (spawned.GetComponent<HellGateBossTouzokuCustomActivator>() == null)
            spawned.AddComponent<HellGateBossTouzokuCustomActivator>();

        Transform canvas = spawned.transform.Find("Canvas");
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private static void EnsureDeathSafeReferencesOnPrefab(GameObject root)
    {
        BossTouzoku boss = root.GetComponent<BossTouzoku>();
        if (boss != null)
            EnsureDeathSafeReferences(boss);
    }

    internal static void DisableStoryIntroComponents(GameObject root)
    {
        DestroyIntroComponents<BossStartFlagALL>(root);
        DestroyIntroComponents<BOSSEvMainDialog>(root);
        DestroyIntroComponents<BossPerformanceFlag>(root);

        movecameraWall[] walls = root.GetComponentsInChildren<movecameraWall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
                walls[i].enabled = false;
        }
    }

    private static void DestroyIntroComponents<T>(GameObject root) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
                UnityEngine.Object.Destroy(components[i]);
        }
    }

    internal static void ApplyFieldMobCombat(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();

        StripSuperEliteTag(boss);
        ResetSkeletonWhite(boss);
        EnsureCombatReferences(boss);
        EnsureBossUiRefs(boss);
        EnsureValidHp(boss, marker);

        if (boss.GetComponentInChildren<SkeletonAnimation>(true) == null)
            return;

        bool firstSetup = marker == null || !marker.CombatApplied;

        try
        {
            ApplyFieldMobHpScale(boss, fullHeal: firstSetup);
            RunBattleStartBootstrap(boss, hideBossUi: true);

            if (marker != null)
                marker.CombatApplied = true;

            if (firstSetup)
                Plugin.Log?.LogInfo($"[BossTouzokuCustom] Field mob combat enabled ({CombatPatchVersion}).");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] Field mob bootstrap failed: {FormatException(ex)}");
        }
    }

    /// <summary>Vanilla BossTouzoku.BattleStart() without arena intro chain.</summary>
    internal static void RunBattleStartBootstrap(BossTouzoku boss, bool hideBossUi = true)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return;

        CancelIntroInvokes(boss);
        KeepPlayerControl(boss);
        EnsureWeaponResistInitialized(boss);

        SetField(_performanceFlagField ??= AccessTools.Field(typeof(BossTouzoku), "PerformanceFlag"), boss, false);

        MeshRenderer mesh = GetField<MeshRenderer>(_meshField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder"), boss);
        if (mesh != null)
            mesh.enabled = true;

        Canvas hpUi = GetField<Canvas>(_hpUiField ??= AccessTools.Field(typeof(BossTouzoku), "HPUI"), boss);
        if (hpUi != null)
            hpUi.enabled = !hideBossUi;

        GameObject uiRoot = GetField<GameObject>(_uiField ??= AccessTools.Field(typeof(BossTouzoku), "UI"), boss);
        if (uiRoot != null && hideBossUi)
            uiRoot.SetActive(false);

        HideExclamation(boss);

        Image hpBar = GetField<Image>(_hpBarField ??= AccessTools.Field(typeof(BossTouzoku), "hpbar"), boss);
        if (hpBar != null)
            hpBar.fillAmount = boss.MaxHp > 0f ? boss.Hp / boss.MaxHp : 1f;

        BoxCollider2D dameCol = GetField<BoxCollider2D>(_dameColField ??= AccessTools.Field(typeof(BossTouzoku), "DameCol"), boss);
        if (dameCol != null)
            dameCol.enabled = true;

        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        if (body != null)
            body.simulated = true;

        boss.gameObject.layer = LayerMask.NameToLayer("Enemy");
        ResetPro2dOffset(boss);

        SetBossFlag(boss, true);
        AlignFieldMobMoveDistance(boss);
        InvokeSousa(boss);
        TransitionToIdle(boss);
        ForceIdleAnimation(boss);
        EnsureDeathSafeReferences(boss);
        EnsureEroReferences(boss);
        TryActivateWeapon(boss);
        EnsureHostileToPlayer(boss);
        RefreshDistanceAndAggro(boss);
    }

    /// <summary>Pre-Update upkeep so BOSSflag/IDLE are set before vanilla StateMachine runs.</summary>
    internal static void EnsureFieldMobActive(BossTouzoku boss)
    {
        if (boss == null || IsFieldMobDead(boss))
            return;

        if (boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>() == null)
            boss.gameObject.AddComponent<HellGateBossTouzokuCustomMarker>();

        CancelIntroInvokes(boss);
        StripSuperEliteTag(boss);
        HideExclamation(boss);
        if (!ShouldSkipPlayerControlForEro(boss))
            KeepPlayerControl(boss);

        if (!IsBossFlagSet(boss))
            SetBossFlag(boss, true);

        AlignFieldMobMoveDistance(boss);

        if (EnemyFactionsConfig.Enable)
            EnemyFactionRuntime.ApplyRelationMoveSpeed(boss);

        if (boss.enmTough <= 0f)
            boss.enmTough = boss.enmMAXtough;

        if (boss.state == BossTouzoku.enemystate.DAMAGE
            || boss.state == BossTouzoku.enemystate.DAMAGE2)
        {
            TransitionToIdle(boss);
        }

        if (IsIntroState(boss.state))
        {
            TransitionToIdle(boss);
            ForceIdleAnimation(boss);
        }

        if (boss.Hp > 0f && !IsPlayerControlIntentionallyLocked(boss))
            ResetVanillaHitTimeScale();

        boss.CancelInvoke("REtimescale");
        EnsureEroReferences(boss);
        if (IsEroSceneActive(boss))
            HideCombatVisualsForEro(boss);
        else
            EnsureVisible(boss);
        EnsureEnemyLayerWhenNotDodging(boss);
        RefreshDistanceAndAggro(boss);

        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        if (body != null && !boss.eroflag && !body.simulated)
            body.simulated = true;
    }

    internal static bool IsFieldMobDead(BossTouzoku boss)
    {
        if (boss == null)
            return false;

        if (boss.state == BossTouzoku.enemystate.DEATH)
            return true;

        if (boss.Hp > 0f)
            return false;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        return marker != null && marker.DeathHandled;
    }

    internal static bool IsEroSceneActive(BossTouzoku boss)
    {
        if (boss == null)
            return false;

        if (boss.eroflag)
            return true;

        FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
        GameObject erodata = erodataField?.GetValue(boss) as GameObject;
        return erodata != null && erodata.activeSelf;
    }

    private static bool ShouldSkipPlayerControlForEro(BossTouzoku boss)
    {
        if (boss == null)
            return false;

        if (boss.eroflag || boss.state == BossTouzoku.enemystate.EROWALK)
            return true;

        playercon player = boss.com_player;
        return player != null && (player.eroflag || player.erodown != 0);
    }

    /// <summary>
    /// Vanilla EROstartset, but never deactivates erodata while eroflag is set.
    /// EroBOSSTouzoku.Start also calls this on first erodata activation.
    /// </summary>
    internal static void ApplyEroStartSet(BossTouzoku boss)
    {
        if (boss == null)
            return;

        FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
        GameObject erodata = erodataField?.GetValue(boss) as GameObject;
        if (erodata == null)
            return;

        FieldInfo eroField = _eroComponentField ??= AccessTools.Field(typeof(BossTouzoku), "ero");
        FieldInfo erospineField = _erospineField ??= AccessTools.Field(typeof(BossTouzoku), "erospine");
        eroField?.SetValue(boss, erodata.GetComponent<EroBOSSTouzoku>());
        erospineField?.SetValue(boss, erodata.GetComponent<SkeletonAnimation>());

        if (!boss.eroflag && erodata.activeSelf)
            erodata.SetActive(false);
    }

    internal static void EnsureEroReferences(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        if (marker != null && marker.EroRefsReady)
            return;

        try
        {
            FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
            if (erodataField?.GetValue(boss) is not GameObject erodata || erodata == null)
            {
                Plugin.Log?.LogWarning("[BossTouzokuCustom] erodata missing — H-scene/grab unavailable.");
                return;
            }

            ApplyEroStartSet(boss);

            FieldInfo eroField = _eroComponentField ??=
                AccessTools.Field(typeof(BossTouzoku), "ero");
            if (eroField?.GetValue(boss) == null)
            {
                Plugin.Log?.LogWarning("[BossTouzokuCustom] EroBOSSTouzoku ref missing after EROstartset.");
                return;
            }

            if (marker != null)
                marker.EroRefsReady = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "[BossTouzokuCustom] ERO bootstrap failed: "
                + FormatException(ex));
        }
    }

    /// <summary>
    /// Run EroBOSSTouzoku.Start once before combat so OnEvent is registered
    /// and first grab does not toggle erodata off mid-scene.
    /// </summary>
    internal static void BeginEroScriptWarmUp(BossTouzoku boss)
    {
        if (boss == null)
            return;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        if (marker == null || marker.EroScriptsWarmedUp)
            return;

        boss.StartCoroutine(WarmUpEroScriptsRoutine(boss, marker));
    }

    private static IEnumerator WarmUpEroScriptsRoutine(
        BossTouzoku boss,
        HellGateBossTouzokuCustomMarker marker)
    {
        yield return null;

        if (boss == null || marker == null || marker.EroScriptsWarmedUp)
            yield break;

        EnsureEroReferences(boss);

        FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
        GameObject erodata = erodataField?.GetValue(boss) as GameObject;
        if (erodata == null)
            yield break;

        if (!erodata.activeSelf)
            erodata.SetActive(true);

        yield return null;

        if (boss != null && !boss.eroflag && erodata.activeSelf)
            erodata.SetActive(false);

        marker.EroScriptsWarmedUp = true;
        Plugin.Log?.LogInfo("[BossTouzokuCustom] EroBOSSTouzoku scripts warmed up.");
    }

    /// <summary>Mirror of BossTouzoku.OnTriggerStay2D grab block (decompiled L2861–2876).</summary>
    internal static bool ApplyVanillaGrabStart(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return false;

        playercon player = boss.com_player;
        if (player == null || player.eroflag || boss.eroflag)
            return false;

        if (boss.state != BossTouzoku.enemystate.EROWALK)
            return false;

        if (player.erodown == 0 || !player.m_Grounded)
            return false;

        EnsureEroReferences(boss);

        try
        {
            FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
            GameObject erodata = erodataField?.GetValue(boss) as GameObject;
            FieldInfo eroField = _eroComponentField ??= AccessTools.Field(typeof(BossTouzoku), "ero");
            FieldInfo erospineField = _erospineField ??= AccessTools.Field(typeof(BossTouzoku), "erospine");
            EroBOSSTouzoku ero = eroField?.GetValue(boss) as EroBOSSTouzoku;
            SkeletonAnimation erospine = erospineField?.GetValue(boss) as SkeletonAnimation;

            if (erodata == null || ero == null || erospine == null)
            {
                Plugin.Log?.LogWarning("[BossTouzokuCustom] Vanilla grab skipped — missing ero refs.");
                return false;
            }

            FieldInfo xWeaponCompField = _xWeaponCompField ??=
                AccessTools.Field(typeof(BossTouzoku), "xweapon_comp");
            if (xWeaponCompField?.GetValue(boss) is XWeaponTrail[] weapons
                && weapons.Length > 0
                && weapons[0] != null)
            {
                weapons[0].Deactivate();
            }

            player.eroflag = true;
            boss.eroflag = true;
            PlayerEnemyGrabStruggleSupport.PrepareForGrab(
                player,
                (_playerStatusField ??= AccessTools.Field(typeof(EnemyDate), "playerstatus"))?.GetValue(boss) as PlayerStatus
                ?? UnifiedPlayerCacheManager.GetPlayerStatus());
            ero.enabled = true;
            erospine.enabled = true;
            if (!erodata.activeSelf)
                erodata.SetActive(true);

            ero.count = 0;
            ero.se_count = 0;
            erospine.state.SetAnimation(0, "START", false);

            MasterAudio.StopBus("EroVoice");

            MethodInfo cameraGet = _cameraGetComponentMethod ??=
                AccessTools.Method(typeof(EnemyDate), "camera_GetComponent");
            cameraGet?.Invoke(boss, null);

            MethodInfo eroCamera = _eroCameraMethod ??=
                AccessTools.Method(typeof(EnemyDate), "ero_camera_1");
            eroCamera?.Invoke(boss, null);

            FieldInfo bgmField = _bgmField ??= AccessTools.Field(typeof(BossTouzoku), "BGM");
            FieldInfo bgmVolField = _bgmVolField ??= AccessTools.Field(typeof(BossTouzoku), "BgmVol");
            if (bgmField?.GetValue(boss) is AudioSource bgm
                && bgmVolField?.GetValue(boss) is float bgmVol)
            {
                bgm.volume = bgmVol / 2f;
            }

            HideCombatVisualsForEro(boss);

            Plugin.Log?.LogInfo(
                "[BossTouzokuCustom] Vanilla grab started (d="
                + boss.distance.ToString("0.##")
                + ").");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "[BossTouzokuCustom] Vanilla grab failed: "
                + FormatException(ex));
            return false;
        }
    }

    /// <summary>Vanilla Update L255–258 + field fix when StateMachine already picked ATK.</summary>
    internal static void ApplyVanillaDownedPlayerTransition(BossTouzoku boss)
    {
        if (boss == null || boss.eroflag)
            return;

        playercon player = boss.com_player;
        if (player == null || player.erodown == 0 || !player.m_Grounded)
            return;

        if (boss.state == BossTouzoku.enemystate.IDLE
            || boss.state == BossTouzoku.enemystate.WALK
            || boss.state == BossTouzoku.enemystate.FASTWALK)
        {
            boss.state = BossTouzoku.enemystate.EROWALK;
            return;
        }

        if (boss.state == BossTouzoku.enemystate.EROWALK
            || boss.state == BossTouzoku.enemystate.DEATH
            || IsIntroState(boss.state))
        {
            return;
        }

        float yReach = boss.Atkdistance > 0f ? boss.Atkdistance : 5f;
        if (Mathf.Abs(boss.distance) > 4f || Mathf.Abs(boss.distance_y) > yReach)
            return;

        MethodInfo stateMethod = _stateMethod ??= AccessTools.Method(typeof(BossTouzoku), "State");
        stateMethod?.Invoke(boss, new object[] { BossTouzoku.enemystate.EROWALK });
    }

    /// <summary>Field spawn: same as vanilla grab when trigger overlap fails.</summary>
    internal static void TryProximityGrab(BossTouzoku boss)
    {
        if (boss == null || boss.eroflag || boss.com_player == null)
            return;

        ApplyVanillaDownedPlayerTransition(boss);

        if (boss.state != BossTouzoku.enemystate.EROWALK)
            return;

        playercon player = boss.com_player;
        if (player.eroflag || player.erodown == 0 || !player.m_Grounded)
            return;

        if (Mathf.Abs(boss.distance) > 1.25f || Mathf.Abs(boss.distance_y) > 2.5f)
            return;

        ApplyVanillaGrabStart(boss);
    }

    /// <summary>GrabViaAttack knockdown → vanilla boss EROWALK + proximity grab.</summary>
    internal static void OnGrabViaAttackHit(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return;

        SyncPlayerDistance(boss);
        ApplyVanillaDownedPlayerTransition(boss);
        TryProximityGrab(boss);
    }

    /// <summary>Field-safe mirror of BossTouzoku.eroanime — vanilla assumes arena refs exist.</summary>
    internal static void RunSafeEroAnime(BossTouzoku boss)
    {
        if (boss == null || !boss.eroflag)
            return;

        playercon player = boss.com_player;
        if (player == null || player.erodown != 0)
            return;

        ForceAbortFieldHScene(boss);
    }

    /// <summary>Hard abort when struggle escape must tear down erodata even if player flags lag.</summary>
    internal static void ForceAbortFieldHScene(BossTouzoku boss)
    {
        if (boss == null || (!boss.eroflag && (boss.erodata == null || !boss.erodata.activeSelf)))
            return;

        EnsureBossUiRefs(boss);
        HideCombatVisualsForEro(boss);

        FieldInfo meshField = _meshField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder");
        if (meshField?.GetValue(boss) is MeshRenderer mesh)
            mesh.enabled = false;

        FieldInfo uiField = _uiField ??= AccessTools.Field(typeof(BossTouzoku), "UI");
        if (uiField?.GetValue(boss) is GameObject ui)
            ui.SetActive(false);

        playercon player = boss.com_player;

        try
        {
            MasterAudio.StopBus("EroVoice");
        }
        catch
        {
        }

        boss.ero_camerareset();
        boss.CancelInvoke("fun_DisableWhenOneTarget_reset");
        boss.Invoke("fun_DisableWhenOneTarget_reset", 2f);
        boss.eroflag = false;

        if (meshField?.GetValue(boss) is MeshRenderer meshRestore)
            meshRestore.enabled = true;

        if (uiField?.GetValue(boss) is GameObject uiRestore)
            uiRestore.SetActive(true);

        boss.enmTough -= 999f;
        boss.enmMAXfaltertime = 2.2f;
        boss.enmfaltertime = 1f;

        FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
        if (erodataField?.GetValue(boss) is GameObject erodata && erodata.activeSelf)
            erodata.SetActive(false);

        if (player != null)
        {
            player.eroflag = false;
            player._eroflag2 = false;
        }

        if (_xWeaponCompField?.GetValue(boss) is XWeaponTrail[] weapons
            && weapons.Length > 0
            && weapons[0] != null)
        {
            weapons[0].Activate();
        }

        if (_alleffectField?.GetValue(boss) is GameObject[] effects)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                    effects[i].SetActive(true);
            }
        }

        if (_bgmField?.GetValue(boss) is AudioSource bgm
            && _bgmVolField?.GetValue(boss) is float bgmVol)
        {
            bgm.volume = bgmVol;
        }

        OnVanillaEroExit(boss);
    }

    /// <summary>Postfix after vanilla eroanime exit — field mob poise/combat only.</summary>
    internal static void OnVanillaEroExit(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss) || boss.eroflag)
            return;

        if (boss.enmTough < 0f)
            boss.enmTough = boss.enmMAXtough;

        boss.enmATKnow = true;
        boss.Look = true;
        KeepPlayerControl(boss);
        HideExclamation(boss);

        if (boss.state == BossTouzoku.enemystate.EROWALK
            || boss.state == BossTouzoku.enemystate.BLANK)
        {
            TransitionToIdle(boss);
        }

        ForceSpineMeshRefresh(boss);
    }

    /// <summary>Mirror vanilla eroanime hide on frame 0 — combat mesh must not flash during H start.</summary>
    internal static void HideCombatVisualsForEro(BossTouzoku boss)
    {
        if (boss == null)
            return;

        EnsureBossUiRefs(boss);

        FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
        GameObject erodata = erodataField?.GetValue(boss) as GameObject;
        Transform eroRoot = erodata != null ? erodata.transform : null;

        FieldInfo meshField = _meshField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder");
        if (meshField?.GetValue(boss) is MeshRenderer primaryMesh)
            primaryMesh.enabled = false;

        FieldInfo meshAllField = _meshAllField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder_all");
        if (meshAllField?.GetValue(boss) is MeshRenderer[] meshAll)
        {
            for (int i = 0; i < meshAll.Length; i++)
            {
                MeshRenderer mesh = meshAll[i];
                if (mesh != null && !IsTransformUnderEro(mesh.transform, eroRoot))
                    mesh.enabled = false;
            }
        }

        MeshRenderer[] renderers = boss.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || IsTransformUnderEro(renderer.transform, eroRoot))
                continue;

            renderer.enabled = false;
        }

        SkeletonAnimation[] spines = boss.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null || IsTransformUnderEro(spine.transform, eroRoot))
                continue;

            spine.enabled = false;
        }

        FieldInfo alleffectField = _alleffectField ??= AccessTools.Field(typeof(BossTouzoku), "Alleffect");
        if (alleffectField?.GetValue(boss) is GameObject[] effects)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                    effects[i].SetActive(false);
            }
        }

        if (_xWeaponCompField?.GetValue(boss) is XWeaponTrail[] weapons)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                    weapons[i].Deactivate();
            }
        }

        FieldInfo uiField = _uiField ??= AccessTools.Field(typeof(BossTouzoku), "UI");
        if (uiField?.GetValue(boss) is GameObject ui && ui != null)
            ui.SetActive(false);
    }

    internal static void EnsureVisible(BossTouzoku boss)
    {
        if (boss == null || boss.Hp <= 0f || IsEroSceneActive(boss))
            return;

        if (!boss.gameObject.activeSelf)
        {
            Plugin.Log?.LogWarning(
                "[BossTouzokuCustom] Boss was inactive while alive (hp="
                + boss.Hp.ToString("0.##")
                + ") — reactivating.");
            boss.gameObject.SetActive(true);
        }

        FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
        GameObject erodata = erodataField?.GetValue(boss) as GameObject;
        if (erodata != null && erodata.activeSelf)
            erodata.SetActive(false);

        FieldInfo meshField = _meshField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder");
        MeshRenderer primaryMesh = meshField?.GetValue(boss) as MeshRenderer;
        if (primaryMesh == null)
        {
            primaryMesh = boss.GetComponent<MeshRenderer>();
            meshField?.SetValue(boss, primaryMesh);
        }

        FieldInfo meshAllField = _meshAllField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder_all");
        if (meshAllField?.GetValue(boss) is MeshRenderer[] meshAll)
        {
            Transform eroRoot = erodata != null ? erodata.transform : null;
            for (int i = 0; i < meshAll.Length; i++)
            {
                MeshRenderer mesh = meshAll[i];
                if (mesh != null && !IsTransformUnderEro(mesh.transform, eroRoot))
                    mesh.enabled = true;
            }
        }

        Transform eroRootForMeshes = erodata != null ? erodata.transform : null;
        MeshRenderer[] renderers = boss.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || IsTransformUnderEro(renderer.transform, eroRootForMeshes))
                continue;

            renderer.enabled = true;
        }

        if (primaryMesh != null)
            ApplyBodySortingFromWeaponTrail(boss, primaryMesh);

        SkeletonAnimation[] spines = boss.GetComponentsInChildren<SkeletonAnimation>(true);
        Transform eroRootForSpines = erodata != null ? erodata.transform : null;
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null || IsTransformUnderEro(spine.transform, eroRootForSpines))
                continue;

            if (!spine.gameObject.activeSelf)
                spine.gameObject.SetActive(true);

            spine.enabled = true;
            if (spine.skeleton == null)
                continue;

            spine.skeleton.SetColor(Color.white);
        }

        LogVisibilityDiagnosticOnce(boss, primaryMesh, spines);
    }

    internal static void ForceSpineMeshRefresh(BossTouzoku boss)
    {
        if (boss == null || boss.Hp <= 0f || IsEroSceneActive(boss))
            return;

        EnsureVisible(boss);

        FieldInfo spineField = _mySpineField ??= AccessTools.Field(typeof(BossTouzoku), "mySpine");
        SkeletonAnimation spine = spineField?.GetValue(boss) as SkeletonAnimation
            ?? boss.GetComponent<SkeletonAnimation>();
        if (spine == null)
            return;

        if (!spine.valid)
        {
            try
            {
                spine.Initialize(false);
            }
            catch
            {
                // Best-effort; LateUpdate below still tries to build mesh.
            }
        }

        MeshRenderer bodyMesh = spine.GetComponent<MeshRenderer>();
        if (bodyMesh != null)
        {
            bodyMesh.enabled = true;
            ApplyBodySortingFromWeaponTrail(boss, bodyMesh);
        }

        if (!spine.valid)
            return;

        try
        {
            _spineLateUpdateMethod ??= AccessTools.Method(typeof(SkeletonAnimation), "LateUpdate");
            _spineLateUpdateMethod?.Invoke(spine, null);
        }
        catch
        {
            // Ignore mesh rebuild failures; next frame retries.
        }
    }

    private static void ApplyBodySortingFromWeaponTrail(BossTouzoku boss, MeshRenderer bodyMesh)
    {
        if (boss == null || bodyMesh == null)
            return;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        if (marker != null && marker.SortingCaptured)
        {
            bodyMesh.sortingLayerName = marker.BodySortLayer;
            bodyMesh.sortingOrder = marker.BodySortOrder;
            return;
        }

        string layer = bodyMesh.sortingLayerName;
        int order = bodyMesh.sortingOrder;

        XWeaponTrail trail = boss.GetComponentInChildren<XWeaponTrail>(true);
        if (trail != null && !string.IsNullOrEmpty(trail.SortingLayerName))
        {
            layer = trail.SortingLayerName;
            order = trail.SortingOrder;
        }
        else
        {
            TouzokuNormal sample = UnityEngine.Object.FindObjectOfType<TouzokuNormal>();
            MeshRenderer sampleMesh = sample?.GetComponentInChildren<MeshRenderer>(true);
            if (sampleMesh != null)
            {
                layer = sampleMesh.sortingLayerName;
                order = sampleMesh.sortingOrder;
            }
        }

        bodyMesh.sortingLayerName = layer;
        bodyMesh.sortingOrder = order;

        if (marker != null)
        {
            marker.BodySortLayer = layer;
            marker.BodySortOrder = order;
            marker.SortingCaptured = true;
        }
    }

    internal static void EnsureBossUiRefs(BossTouzoku boss)
    {
        if (boss == null)
            return;

        FieldInfo hpBarField = _hpBarField ??= AccessTools.Field(typeof(BossTouzoku), "hpbar");
        if (hpBarField?.GetValue(boss) == null)
        {
            Transform canvas = boss.transform.Find("Canvas");
            Image bar = canvas?.GetComponentInChildren<Image>(true);
            if (bar != null)
                hpBarField?.SetValue(boss, bar);
        }

        FieldInfo hpUiField = _hpUiField ??= AccessTools.Field(typeof(BossTouzoku), "HPUI");
        if (hpUiField?.GetValue(boss) == null)
        {
            Canvas canvas = boss.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                hpUiField?.SetValue(boss, canvas);
        }

        FieldInfo exclamationField = _exclamationField ??=
            AccessTools.Field(typeof(BossTouzoku), "exclamation");
        if (exclamationField?.GetValue(boss) == null)
        {
            Transform canvas = boss.transform.Find("Canvas/exclamation");
            Image exclamation = canvas?.GetComponent<Image>();
            if (exclamation != null)
                exclamationField?.SetValue(boss, exclamation);
        }

        FieldInfo uiField = _uiField ??= AccessTools.Field(typeof(BossTouzoku), "UI");
        if (uiField?.GetValue(boss) == null)
        {
            Transform canvas = boss.transform.Find("Canvas");
            if (canvas != null)
            {
                uiField?.SetValue(boss, canvas.gameObject);
            }
            else
            {
                GameObject stub = new GameObject("BossUiStub");
                stub.transform.SetParent(boss.transform, false);
                stub.SetActive(false);
                uiField?.SetValue(boss, stub);
            }
        }

        FieldInfo meshField = _meshField ??= AccessTools.Field(typeof(BossTouzoku), "myspinerennder");
        if (meshField?.GetValue(boss) == null)
        {
            FieldInfo erodataField = _erodataField ??= AccessTools.Field(typeof(EnemyDate), "erodata");
            Transform eroRoot = erodataField?.GetValue(boss) is GameObject erodata
                ? erodata.transform
                : null;

            MeshRenderer mesh = boss.GetComponent<MeshRenderer>();
            if (mesh == null)
            {
                MeshRenderer[] meshes = boss.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] != null && !IsTransformUnderEro(meshes[i].transform, eroRoot))
                    {
                        mesh = meshes[i];
                        break;
                    }
                }
            }

            if (mesh != null)
                meshField?.SetValue(boss, mesh);
        }
    }

    private static bool IsTransformUnderEro(Transform child, Transform eroRoot)
    {
        if (child == null || eroRoot == null)
            return false;

        Transform walk = child;
        while (walk != null)
        {
            if (walk == eroRoot)
                return true;

            walk = walk.parent;
        }

        return false;
    }

    private static void LogVisibilityDiagnosticOnce(
        BossTouzoku boss,
        MeshRenderer primaryMesh,
        SkeletonAnimation[] spines)
    {
    }

    internal static void EnsureEnemyLayerWhenNotDodging(BossTouzoku boss)
    {
        if (boss == null || boss.Hp <= 0f)
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0 && boss.gameObject.layer != enemyLayer)
            boss.gameObject.layer = enemyLayer;
    }

    internal static void EnsureCombatReferences(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return;

        try
        {
            MethodInfo startFun = _startFunMethod ??= AccessTools.Method(typeof(EnemyDate), "start_fun");
            startFun?.Invoke(boss, null);

            FieldInfo playerStatusField =
                _playerStatusField ??= AccessTools.Field(typeof(EnemyDate), "playerstatus");
            GameObject controller = GameObject.FindGameObjectWithTag("GameController");
            if (controller != null)
            {
                PlayerStatus liveStatus = controller.GetComponent<PlayerStatus>();
                if (liveStatus != null)
                    playerStatusField?.SetValue(boss, liveStatus);
            }

            FieldInfo playerField = _playerField ??= AccessTools.Field(typeof(EnemyDate), "player");
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerField?.SetValue(boss, playerObject);
                playercon livePlayer = playerObject.GetComponent<playercon>();
                if (livePlayer != null)
                    boss.com_player = livePlayer;
            }
            else
            {
                playerObject = playerField?.GetValue(boss) as GameObject;
                if (boss.com_player == null && playerObject != null)
                    boss.com_player = playerObject.GetComponent<playercon>();
            }

            FieldInfo playerUiField = _playerUiField ??= AccessTools.Field(typeof(BossTouzoku), "playerUI");
            if (playerUiField?.GetValue(boss) == null)
            {
                FieldInfo objPlayerUiField = _objPlayerUiField ??= AccessTools.Field(typeof(BossTouzoku), "obj_playerUI");
                GameObject uiObject = objPlayerUiField?.GetValue(boss) as GameObject ?? GameObject.Find("UI");
                if (uiObject != null)
                {
                    objPlayerUiField?.SetValue(boss, uiObject);
                    playerUiField?.SetValue(boss, uiObject.GetComponent<UImng>());
                }
            }

            EnsureWeaponResistInitialized(boss);
            EnsureElementalResistInitialized(boss);
            EnsureEroReferences(boss);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] Reference bootstrap skipped: {FormatException(ex)}");
        }
    }

    private static void EnsureValidHp(BossTouzoku boss, HellGateBossTouzokuCustomMarker marker)
    {
        if (boss.MaxHp >= 100f && boss.Hp > 0f)
            return;

        MethodInfo setStatus = _setStatusMethod ??=
            AccessTools.Method(typeof(EnemyDate), "SetStatusGameDifficulty");
        setStatus?.Invoke(boss, new object[] { VanillaMaxHp, VanillaMaxTough });

        ApplyFieldMobHpScale(boss, fullHeal: true);
    }

    private static void KeepPlayerControl(BossTouzoku boss)
    {
        FieldInfo playerStatusField =
            _playerStatusField ??= AccessTools.Field(typeof(EnemyDate), "playerstatus");
        if (playerStatusField?.GetValue(boss) is not PlayerStatus status)
            return;

        // Field mobs run every frame; do not override vanilla TALK / EV dialog / EventCore modal locks.
        if (PlayerCombatControlRecovery.IsVanillaIntentionalControlLock(status))
            return;

        status._SOUSA = true;
        status._SOUSAMNG = true;
    }

    private static bool IsPlayerControlIntentionallyLocked(BossTouzoku boss)
    {
        if (EventCorePause.IsFrozen)
            return true;

        FieldInfo playerStatusField =
            _playerStatusField ??= AccessTools.Field(typeof(EnemyDate), "playerstatus");
        if (playerStatusField?.GetValue(boss) is PlayerStatus status)
            return PlayerCombatControlRecovery.IsVanillaIntentionalControlLock(status);

        return false;
    }

    private static void InvokeSousa(BossTouzoku boss)
    {
        try
        {
            MethodInfo sousa = _sousaMethod ??= AccessTools.Method(typeof(BossTouzoku), "sousa");
            sousa?.Invoke(boss, null);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] sousa() skipped: {FormatException(ex)}");
        }
    }

    internal static bool IsIntroState(BossTouzoku.enemystate state)
    {
        return state == BossTouzoku.enemystate.START
               || state == BossTouzoku.enemystate.START2
               || state == BossTouzoku.enemystate.START3
               || state == BossTouzoku.enemystate.START4
               || state == BossTouzoku.enemystate.START5
               || state == BossTouzoku.enemystate.START6
               || state == BossTouzoku.enemystate.EVENT
               || state == BossTouzoku.enemystate.EVENT2
               || state == BossTouzoku.enemystate.EVENT3;
    }

    internal static void StripSuperEliteTag(BossTouzoku boss)
    {
        FieldInfo jpField = _jpNameField ??= AccessTools.Field(typeof(EnemyDate), "JPname");
        if (jpField == null)
            return;

        string jpName = jpField.GetValue(boss) as string;

        // NoREroMod SuperBossEnemySpeed/SuperEnemySpeed do JPname.Contains(...) inside
        // fun_animekind; a null JPname throws NRE every frame and kills vanilla Update.
        if (string.IsNullOrEmpty(jpName))
        {
            jpField.SetValue(boss, "盗賊頭");
            return;
        }

        if (jpName.IndexOf(SuperTag, StringComparison.Ordinal) < 0)
            return;

        jpField.SetValue(boss, jpName.Replace(SuperTag, string.Empty));
    }

    internal static void CancelIntroInvokes(BossTouzoku boss)
    {
        if (boss == null)
            return;

        for (int i = 0; i < IntroInvokeNames.Length; i++)
            boss.CancelInvoke(IntroInvokeNames[i]);
    }

    internal static void ApplyFieldMobHitReaction(BossTouzoku boss)
    {
        if (boss == null)
            return;

        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        if (body != null && !boss.eroflag && !body.simulated)
            body.simulated = true;

        if (boss.Hp > 0f)
            ResetVanillaHitTimeScale();

        boss.CancelInvoke("REtimescale");
    }

    internal static bool IsWeaponHitReactionGuard(BossTouzoku boss)
    {
        if (boss == null)
            return false;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        return marker != null && marker.WeaponHitReactionGuard;
    }

    private static bool TryBeginWeaponHit(BossTouzoku boss)
    {
        if (boss?.com_player == null)
            return true;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        if (marker == null)
            return true;

        int frame = Time.frameCount;
        float atkId = boss.com_player.ATKID;
        if (marker.LastWeaponHitFrame == frame && Mathf.Approximately(marker.LastWeaponHitAtkId, atkId))
            return false;

        marker.LastWeaponHitFrame = frame;
        marker.LastWeaponHitAtkId = atkId;
        return true;
    }

    internal static bool ShouldSkipVanillaGetDame(BossTouzoku boss)
    {
        if (boss?.com_player == null)
            return false;

        FieldInfo field = _damegeIdField ??= AccessTools.Field(typeof(BossTouzoku), "damegeID");
        if (field?.GetValue(boss) is not float[] ids || ids.Length == 0)
            return false;

        float atkId = boss.com_player.ATKID;
        for (int i = 0; i < ids.Length; i++)
        {
            if (Mathf.Approximately(ids[i], atkId))
                return true;
        }

        return false;
    }

    internal static bool TryApplyCustomWeaponHit(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return false;

        if (IsFieldMobDead(boss))
            return true;

        if (!TryBeginWeaponHit(boss))
            return true;

        EnsureCombatReferences(boss);
        EnsureWeaponResistInitialized(boss);
        SyncPlayerAttackValues(boss);

        FieldInfo dameColField = _dameColField ??= AccessTools.Field(typeof(BossTouzoku), "DameCol");
        if (dameColField?.GetValue(boss) is BoxCollider2D dameCol)
            dameCol.enabled = true;

        float hpBeforeWeaponDamage = boss.Hp;
        try
        {
            MethodInfo weaponDamage = _weaponDamageMethod ??=
                AccessTools.Method(typeof(EnemyDate), "WeaponDamage");
            weaponDamage?.Invoke(boss, null);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] WeaponDamage failed: {FormatException(ex)}");
            return true;
        }

        float damageApplied = hpBeforeWeaponDamage - boss.Hp;
        if (damageApplied <= 0f)
        {
            Plugin.Log?.LogWarning(
                "[BossTouzokuCustom] WeaponDamage applied 0 - ATK="
                + (boss.com_player?.ATK.ToString("0.##") ?? "null")
                + " resist="
                + ReadWeaponResist(boss));
            return true;
        }

        ShiftDamageIds(boss);
        ApplyVanillaHitSideEffects(boss);
        MarkHitNow();

        if (boss.Hp <= 0f)
        {
            HandleFieldMobDeath(boss);
            return true;
        }

        ApplyFieldMobHitReaction(boss);
        ClearHitCombatFlash(boss);
        return true;
    }

    internal static bool TryApplyCustomMagicHit(
        BossTouzoku boss,
        float[] damage,
        float dir,
        int attribute,
        float cut,
        float falterDir)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return false;

        if (IsFieldMobDead(boss))
            return true;

        EnsureCombatReferences(boss);
        EnsureElementalResistInitialized(boss);

        float hpBefore = boss.Hp;
        try
        {
            MethodInfo getMgDame = _getMgDameFunMethod ??=
                AccessTools.Method(typeof(BossTouzoku), "getMGdame_fun");
            getMgDame?.Invoke(boss, new object[] { damage, dir, attribute, cut, falterDir });
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] getMGdame_fun failed: {FormatException(ex)}");
            return true;
        }

        float damageApplied = hpBefore - boss.Hp;
        if (damageApplied <= 0f)
        {
            Plugin.Log?.LogWarning(
                "[BossTouzokuCustom] MagicDamage applied 0 - attribute="
                + attribute
                + " cut="
                + cut.ToString("0.##"));
            return true;
        }

        MarkHitNow();

        if (boss.Hp <= 0f)
        {
            HandleFieldMobDeath(boss);
            return true;
        }

        ApplyFieldMobHitReaction(boss);
        ClearHitCombatFlash(boss);
        return true;
    }

    internal static void ClearHitCombatFlash(BossTouzoku boss)
    {
        if (boss == null)
            return;

        boss.enmMAXfaltertime = 0f;
        boss.enmfaltertime = 0f;
        boss.CancelInvoke("REtimescale");
        ResetVanillaHitTimeScale();

        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        if (body != null && boss.Hp > 0f)
            body.velocity = new Vector2(0f, body.velocity.y);

        boss.enmTough = boss.enmMAXtough;
        FieldInfo maxContinuityField = _continuityMaxToughField ??=
            AccessTools.Field(typeof(EnemyDate), "Continuity_MAXtough");
        boss.Continuity_tough = maxContinuityField?.GetValue(boss) is float maxContinuity
            ? maxContinuity
            : 10f;

        if (boss.state == BossTouzoku.enemystate.DAMAGE
            || boss.state == BossTouzoku.enemystate.DAMAGE2)
        {
            TransitionToIdle(boss);
        }

        ScheduleHitColorReset(boss);
    }

    internal static void ClearMagicHitFlash(BossTouzoku boss)
    {
        ClearHitCombatFlash(boss);
    }

    internal static void ScheduleHitColorReset(BossTouzoku boss)
    {
        if (boss == null)
            return;

        boss.CancelInvoke("reste");
        boss.Invoke("reste", 0.05f);
    }

    private static void ShiftDamageIds(BossTouzoku boss)
    {
        if (boss?.com_player == null)
            return;

        FieldInfo field = _damegeIdField ??= AccessTools.Field(typeof(BossTouzoku), "damegeID");
        if (field?.GetValue(boss) is not float[] ids || ids.Length < 6)
            return;

        ids[5] = ids[4];
        ids[4] = ids[3];
        ids[3] = ids[2];
        ids[2] = ids[1];
        ids[1] = ids[0];
        ids[0] = boss.com_player.ATKID;
    }

    private static void ApplyVanillaHitSideEffects(BossTouzoku boss)
    {
        if (boss?.com_player == null)
            return;

        FieldInfo avoidField = AccessTools.Field(typeof(EnemyDate), "Avoidcount");
        if (avoidField?.GetValue(boss) is int avoidCount)
        {
            if ((double)boss.Hp >= (double)boss.MaxHp * 0.75)
            {
                if (avoidCount <= 1)
                    avoidField.SetValue(boss, avoidCount + 1);
            }
            else if (avoidCount <= 0)
            {
                avoidField.SetValue(boss, avoidCount + 1);
            }
        }

        FieldInfo bloodField = _bloodField ??= AccessTools.Field(typeof(BossTouzoku), "blood");
        if (bloodField?.GetValue(boss) is GameObject blood)
        {
            blood.SetActive(false);
            blood.SetActive(true);
        }

        MasterAudio.StopAllOfSound("sword_hit");
        MasterAudio.PlaySound("sword_hit", 1f, null, 0f, null, false, false);

        if (boss.com_player.Atkcount == 3)
            boss.com_player.shake_fun("Gun");

        boss.enmTough -= boss.com_player.ToughCut;
        boss.Continuity_tough -= boss.com_player.ToughCut;

        MethodInfo continuityTough = AccessTools.Method(typeof(EnemyDate), "fun_eneContinuity_tough");
        continuityTough?.Invoke(boss, null);

        FieldInfo spineField = _mySpineField ??= AccessTools.Field(typeof(BossTouzoku), "mySpine");
        if (spineField?.GetValue(boss) is SkeletonAnimation primarySpine && primarySpine.skeleton != null)
            primarySpine.skeleton.SetColor(Color.red);

        SkeletonAnimation[] spines = boss.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            if (spines[i]?.skeleton != null)
                spines[i].skeleton.SetColor(Color.red);
        }

        FieldInfo playerStatusField =
            _playerStatusField ??= AccessTools.Field(typeof(EnemyDate), "playerstatus");
        if (playerStatusField?.GetValue(boss) is PlayerStatus playerStatus)
            playerStatus.AttackRegenerate();
    }

    private static void SyncPlayerAttackValues(BossTouzoku boss)
    {
        if (boss?.com_player == null)
            return;

        MethodInfo totalAtk = _totalAtkValMethod ??=
            AccessTools.Method(typeof(playercon), "TotalAtkVal_fun");
        totalAtk?.Invoke(boss.com_player, new object[] { 1f });
    }

    private static string ReadWeaponResist(BossTouzoku boss)
    {
        FieldInfo weaponResistField = AccessTools.Field(typeof(EnemyDate), "WeaponResist");
        if (weaponResistField?.GetValue(boss) is not float[] weaponResist || weaponResist.Length == 0)
            return "null";

        FieldInfo playerStatusField =
            _playerStatusField ??= AccessTools.Field(typeof(EnemyDate), "playerstatus");
        int kind = playerStatusField?.GetValue(boss) is PlayerStatus status
            ? status._NowSmashKind
            : 0;

        if (kind >= 0 && kind < weaponResist.Length)
            return weaponResist[kind].ToString("0.##");

        return weaponResist[0].ToString("0.##");
    }

    private static void ForceIdleAnimation(BossTouzoku boss)
    {
        if (boss == null)
            return;

        try
        {
            MethodInfo setAnimation = _setAnimationMethod ??=
                AccessTools.Method(typeof(BossTouzoku), "setanimation");
            setAnimation?.Invoke(boss, new object[] { "IDLE", true });
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] setanimation(IDLE) skipped: {FormatException(ex)}");
        }
    }

    private static FieldInfo _moveDistanceField;
    private static FieldInfo _continuityMaxToughField;

    private static void AlignFieldMobMoveDistance(BossTouzoku boss)
    {
        if (boss == null)
            return;

        boss.Atkdistance = BossTouzokuCustomStats.FieldMobAtkdistance;
        FieldInfo moveField = _moveDistanceField ??=
            AccessTools.Field(typeof(EnemyDate), "Movedistance");
        moveField?.SetValue(boss, BossTouzokuCustomStats.FieldMobMovedistance);
    }

    internal static void AlignFieldMobMoveDistanceForPatch(BossTouzoku boss)
    {
        AlignFieldMobMoveDistance(boss);
    }

    /// <summary>
    /// Field-spawn boss uses faction reputation like TouzokuNormal unless provoked or reputation is hostile.
    /// Arena/story BossTouzoku keeps legacy always-hostile bootstrap via EnsureHostileToPlayer when factions are off.
    /// </summary>
    internal static bool ShouldForcePlayerAggro(BossTouzoku boss)
    {
        if (boss == null)
            return false;

        if (!EnemyFactionsConfig.Enable)
            return true;

        if (EnemyFactionRuntime.IsHostileToPlayer(boss.gameObject))
            return true;

        if (FactionReputationBehavior.ShouldAutoProvoke(boss.gameObject))
            return true;

        if (FactionReputationBehavior.ShouldSuppressVanillaAggro(boss.gameObject)
            || FactionReputationBehavior.ShouldBanditsIgnorePlayer(boss.gameObject))
        {
            return false;
        }

        return true;
    }

    private static void ApplyPassivePlayerIgnore(BossTouzoku boss)
    {
        if (boss == null)
            return;

        Vector3 selfPos = boss.transform.position;
        boss.playerPos = selfPos + Vector3.right * 9999f;
        boss.distance = 9999f;
        boss.distance_y = 9999f;
        boss.Look = false;
        boss.enmATKnow = false;
        boss.Choose = 0;
    }

    private static void RefreshDistanceAndAggro(BossTouzoku boss)
    {
        if (boss == null)
            return;

        try
        {
            SyncPlayerDistance(boss);

            if (!ShouldForcePlayerAggro(boss) || IsPlayerOutsideFieldMobWakeRange(boss))
            {
                ApplyPassivePlayerIgnore(boss);
                return;
            }

            boss.Look = true;
            boss.enmATKnow = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] Distance refresh skipped: {FormatException(ex)}");
        }
    }

    private static bool IsPlayerOutsideFieldMobWakeRange(BossTouzoku boss)
    {
        if (boss == null)
            return true;

        float detectRange = BossTouzokuCustomStats.FieldMobDetectionRange;
        float yRange = BossTouzokuCustomStats.FieldMobMovedistance + 1f;
        float horiz = Mathf.Abs(boss.distance);
        float vert = Mathf.Abs(boss.distance_y);

        if (horiz > detectRange || vert > yRange)
            return true;

        if (!EnemyFactionsConfig.Enable || EnemyFactionsConfig.ActivationDistanceFromPlayer <= 0f)
            return false;

        float activation = EnemyFactionsConfig.ActivationDistanceFromPlayer;
        if (EnemyFactionsConfig.ActivationDistanceHorizontalOnly)
            return horiz > activation;

        return horiz * horiz + vert * vert > activation * activation;
    }

    /// <summary>
    /// After faction AI redirects distance toward a hostile, clamp field mob to TouzokuNormal sight range.
    /// </summary>
    internal static void CapFieldMobDetection(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss) || boss.Hp <= 0f || boss.eroflag)
            return;

        AlignFieldMobMoveDistance(boss);

        float detectRange = BossTouzokuCustomStats.FieldMobDetectionRange;
        float yRange = BossTouzokuCustomStats.FieldMobMovedistance + 1f;
        if (Mathf.Abs(boss.distance) > detectRange || Mathf.Abs(boss.distance_y) > yRange)
            ApplyPassivePlayerIgnore(boss);
    }

    private static int _updateExceptionLogged;
    private static float _lastHitRealtime = -999f;

    internal static void LogUpdateException(Exception ex)
    {
        if (_updateExceptionLogged >= 3 || ex == null)
            return;

        _updateExceptionLogged++;
        string detail = ex is TargetInvocationException tie && tie.InnerException != null
            ? tie.InnerException.GetType().FullName + ": " + tie.InnerException.Message + "\n" + tie.InnerException.StackTrace
            : ex.GetType().FullName + ": " + ex.Message + "\n" + ex.StackTrace;

        Plugin.Log?.LogWarning("[BossTouzokuCustom] vanilla Update threw -> " + detail);
    }

    internal static void MarkHitNow()
    {
        _lastHitRealtime = Time.time;
    }

    internal static void RunFieldMobUpkeep(BossTouzoku boss)
    {
        if (boss == null)
            return;

        if (IsEroSceneActive(boss))
            HideCombatVisualsForEro(boss);
        else
            ForceSpineMeshRefresh(boss);
        EnsureEnemyLayerWhenNotDodging(boss);

        if (boss.Hp <= 0f)
        {
            HandleFieldMobDeath(boss);
            return;
        }

        if (!IsEroSceneActive(boss) && Time.time - _lastHitRealtime > 0.12f)
            ResetSkeletonWhite(boss);

        ApplyVanillaDownedPlayerTransition(boss);

        if (boss.state == BossTouzoku.enemystate.EROWALK && !boss.eroflag)
            TryProximityGrab(boss);
    }

    internal static void SyncPlayerDistance(BossTouzoku boss)
    {
        if (boss == null)
            return;

        GameObject playerObject = boss.player;
        if (playerObject == null)
            playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            FieldInfo playerField = _playerField ??= AccessTools.Field(typeof(EnemyDate), "player");
            playerField?.SetValue(boss, playerObject);

            if (boss.com_player == null)
                boss.com_player = playerObject.GetComponent<playercon>();

            boss.playerPos = playerObject.transform.position;
            boss.distance = boss.playerPos.x - boss.transform.position.x;
            boss.distance_y = boss.playerPos.y - boss.transform.position.y;
            return;
        }

        MethodInfo distanceFun = _distanceFunMethod ??=
            AccessTools.Method(typeof(EnemyDate), "Distance_fun");
        distanceFun?.Invoke(boss, null);
    }

    private static void EnsureDeathSafeReferences(BossTouzoku boss)
    {
        if (boss == null)
            return;

        FieldInfo wallField = _wallMoveCameraField ??=
            AccessTools.Field(typeof(BossTouzoku), "WALLmovecamera");
        if (wallField?.GetValue(boss) is not movecameraWall[] walls || walls.Length == 0)
            return;

        bool changed = false;
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
                continue;

            GameObject stub = new GameObject("HellGateWallStub" + i);
            stub.transform.SetParent(boss.transform, false);
            walls[i] = stub.AddComponent<movecameraWall>();
            changed = true;
        }

        if (changed)
            wallField.SetValue(boss, walls);
    }

    internal static void HandleFieldMobDeath(BossTouzoku boss)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss) || boss.Hp > 0f)
            return;

        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();
        if (marker != null && marker.DeathHandled)
            return;

        if (marker != null)
            marker.DeathHandled = true;

        boss.CancelInvoke("REtimescale");
        ResetVanillaHitTimeScale();

        boss.Hp = 0f;
        EnsureDeathSafeReferences(boss);

        TransitionToDeath(boss);
        TryDeactivateWeapon(boss);

        FieldInfo dameColField = _dameColField ??= AccessTools.Field(typeof(BossTouzoku), "DameCol");
        if (dameColField?.GetValue(boss) is BoxCollider2D dameCol)
            dameCol.enabled = false;

        boss.enmTough = -3f;

        int stepLayer = LayerMask.NameToLayer("EnemyStepnow");
        if (stepLayer >= 0)
            boss.gameObject.layer = stepLayer;

        ForceDeathAnimation(boss);
    }

    private static void TransitionToDeath(BossTouzoku boss)
    {
        MethodInfo stateMethod = _stateMethod ??= AccessTools.Method(typeof(BossTouzoku), "State");
        if (stateMethod != null)
        {
            stateMethod.Invoke(boss, new object[] { BossTouzoku.enemystate.DEATH });
            return;
        }

        boss.state = BossTouzoku.enemystate.DEATH;
    }

    private static void ForceDeathAnimation(BossTouzoku boss)
    {
        if (boss == null)
            return;

        try
        {
            MethodInfo setAnimation = _setAnimationMethod ??=
                AccessTools.Method(typeof(BossTouzoku), "setanimation");
            setAnimation?.Invoke(boss, new object[] { "DEATH", false });
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] setanimation(DEATH) skipped: {FormatException(ex)}");
        }
    }

    private static void EnsureWeaponResistInitialized(BossTouzoku boss)
    {
        if (boss == null)
            return;

        FieldInfo weaponResistField = AccessTools.Field(typeof(EnemyDate), "WeaponResist");
        if (weaponResistField?.GetValue(boss) is not float[] weaponResist || weaponResist.Length == 0)
            return;

        if (weaponResist[0] > 0f)
            return;

        MethodInfo setResist = _atkResistArraySetMethod ??=
            AccessTools.Method(typeof(EnemyDate), "ATKResistArraySet");
        setResist?.Invoke(boss, new object[] { 0.8f, 0.9f, 1.1f });
    }

    private static void EnsureElementalResistInitialized(BossTouzoku boss)
    {
        if (boss == null)
            return;

        FieldInfo elementalField = AccessTools.Field(typeof(EnemyDate), "ElementalResist");
        if (elementalField?.GetValue(boss) is not float[] elemental || elemental.Length == 0)
            return;

        if (elemental[0] > 0f)
            return;

        MethodInfo setElemental = AccessTools.Method(typeof(EnemyDate), "ResistArraySet");
        setElemental?.Invoke(boss, new object[] { 1f, 1f, 1f, 1f, 1f });
    }

    internal static void ApplyFieldMobHpScale(BossTouzoku boss, bool fullHeal = false)
    {
        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
            return;

        float targetMax = BossTouzokuCustomStats.FieldMobMaxHp;
        HellGateBossTouzokuCustomMarker marker =
            boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>();

        if (!fullHeal && marker != null && marker.HpScaled
            && Mathf.Abs(boss.MaxHp - targetMax) < 1f)
        {
            return;
        }

        float ratio = boss.MaxHp > 0f ? boss.Hp / boss.MaxHp : 1f;
        boss.MaxHp = targetMax;
        boss.Hp = fullHeal ? targetMax : Mathf.Clamp(targetMax * ratio, 0f, targetMax);

        if (marker != null)
            marker.HpScaled = true;
    }

    internal static void ResetSkeletonWhite(BossTouzoku boss)
    {
        if (boss == null)
            return;

        SkeletonAnimation[] spines = boss.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine?.skeleton != null)
                spine.skeleton.SetColor(Color.white);
        }
    }

    internal static BossTouzoku.enemystate NormalizeFieldMobState(BossTouzoku.enemystate state)
    {
        if (IsIntroState(state))
            return BossTouzoku.enemystate.IDLE;

        return state;
    }

    internal static bool IsBossFlagSet(BossTouzoku boss)
    {
        if (boss == null)
            return false;

        FieldInfo field = _bossFlagField ??= AccessTools.Field(typeof(BossTouzoku), "BOSSflag");
        return field?.GetValue(boss) is bool flag && flag;
    }

    private static void SetBossFlag(BossTouzoku boss, bool value)
    {
        FieldInfo field = _bossFlagField ??= AccessTools.Field(typeof(BossTouzoku), "BOSSflag");
        field?.SetValue(boss, value);
    }

    private static void TransitionToIdle(BossTouzoku boss)
    {
        MethodInfo stateMethod = _stateMethod ??= AccessTools.Method(typeof(BossTouzoku), "State");
        stateMethod?.Invoke(boss, new object[] { BossTouzoku.enemystate.IDLE });
    }

    internal static void HideExclamation(BossTouzoku boss)
    {
        FieldInfo uiField = _uiField ??= AccessTools.Field(typeof(BossTouzoku), "UI");
        if (uiField?.GetValue(boss) is GameObject ui && ui != null && ui.activeSelf)
            ui.SetActive(false);

        FieldInfo field = _exclamationField ??=
            AccessTools.Field(typeof(BossTouzoku), "exclamation");
        if (field?.GetValue(boss) is Image exclamation)
        {
            exclamation.enabled = false;
            if (exclamation.gameObject.activeSelf)
                exclamation.gameObject.SetActive(false);
        }
    }

    private static void ResetPro2dOffset(BossTouzoku boss)
    {
        FieldInfo pro2dField = _pro2dField ??= AccessTools.Field(typeof(BossTouzoku), "pro2d");
        object pro2d = pro2dField?.GetValue(boss);
        if (pro2d == null)
            return;

        FieldInfo offsetField = pro2d.GetType().GetField("OverallOffset");
        if (offsetField?.GetValue(pro2d) is Vector3 offset)
        {
            offset.x = 0f;
            offsetField.SetValue(pro2d, offset);
        }
    }

    private static void EnsureHostileToPlayer(BossTouzoku boss)
    {
        if (boss == null || !ShouldForcePlayerAggro(boss))
            return;

        try
        {
            if (EnemyFactionsConfig.Enable)
                EnemyFactionRuntime.MarkPermanentlyHostileToPlayer(boss);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] Hostile mark skipped: {FormatException(ex)}");
        }
    }

    private static void TryActivateWeapon(BossTouzoku boss)
    {
        try
        {
            FieldInfo field = _xWeaponCompField ??=
                AccessTools.Field(typeof(BossTouzoku), "xweapon_comp");
            Array weaponsRaw = field?.GetValue(boss) as Array;
            if (weaponsRaw == null || weaponsRaw.Length == 0)
                return;

            if (weaponsRaw.GetValue(0) is XWeaponTrail weapon)
                weapon.Activate();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] Weapon activate skipped: {FormatException(ex)}");
        }
    }

    private static void TryDeactivateWeapon(BossTouzoku boss)
    {
        try
        {
            FieldInfo field = _xWeaponCompField ??=
                AccessTools.Field(typeof(BossTouzoku), "xweapon_comp");
            Array weaponsRaw = field?.GetValue(boss) as Array;
            if (weaponsRaw == null || weaponsRaw.Length == 0)
                return;

            if (weaponsRaw.GetValue(0) is XWeaponTrail weapon)
                weapon.Deactivate();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BossTouzokuCustom] Weapon deactivate skipped: {FormatException(ex)}");
        }
    }

    private static T GetField<T>(FieldInfo field, BossTouzoku boss) where T : class
    {
        return field?.GetValue(boss) as T;
    }

    private static void SetField(FieldInfo field, BossTouzoku boss, object value)
    {
        field?.SetValue(boss, value);
    }

    internal static string FormatException(Exception ex)
    {
        if (ex == null)
            return string.Empty;

        if (ex is TargetInvocationException tie && tie.InnerException != null)
            return tie.InnerException.GetType().Name + ": " + tie.InnerException.Message;

        if (ex.InnerException != null)
            return ex.Message + " -> " + ex.InnerException.Message;

        return ex.Message;
    }
}

[HarmonyPatch(typeof(BossTouzoku), "Start")]
internal static class BossTouzokuCustomStartPrefixPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        BossTouzokuCustomRuntime.CancelIntroInvokes(__instance);
        BossTouzokuCustomRuntime.HideExclamation(__instance);
    }
}

[HarmonyPatch(typeof(BossTouzoku), "Start")]
internal static class BossTouzokuCustomStartPatch
{
    [HarmonyFinalizer]
    private static Exception Finalizer(BossTouzoku __instance, Exception __exception)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return __exception;

        BossTouzokuCustomRuntime.EnsureCombatReferences(__instance);
        BossTouzokuCustomRuntime.ApplyFieldMobCombat(__instance);
        return null;
    }
}

[HarmonyPatch(typeof(BossTouzoku), "Start")]
internal static class BossTouzokuCustomStartHpScalePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        BossTouzokuCustomRuntime.ApplyFieldMobHpScale(__instance, fullHeal: true);
    }
}

[HarmonyPatch(typeof(BossTouzoku), "Update")]
internal static class BossTouzokuCustomUpdatePrefixPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return true;

        BossTouzokuCustomRuntime.EnsureFieldMobActive(__instance);
        BossTouzokuCustomRuntime.EnsureBossUiRefs(__instance);

        return true;
    }
}

[HarmonyPatch(typeof(BossTouzoku), "Update")]
internal static class BossTouzokuCustomUpdatePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        BossTouzokuCustomRuntime.RunFieldMobUpkeep(__instance);
        BossTouzokuCustomRuntime.CapFieldMobDetection(__instance);
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(BossTouzoku __instance, Exception __exception)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return __exception;

        if (__exception != null)
        {
            BossTouzokuCustomRuntime.LogUpdateException(__exception);
            BossTouzokuCustomRuntime.RunFieldMobUpkeep(__instance);
            BossTouzokuCustomRuntime.CapFieldMobDetection(__instance);
        }

        return null;
    }
}

[HarmonyPatch(typeof(BossTouzoku), "reste")]
internal static class BossTouzokuCustomRestePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        BossTouzokuCustomRuntime.ResetSkeletonWhite(__instance);
    }
}

[HarmonyPatch(typeof(BossTouzoku), "getdame_fun")]
internal static class BossTouzokuCustomDamagePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return true;

        if (BossTouzokuCustomRuntime.IsWeaponHitReactionGuard(__instance)
            || BossTouzokuCustomRuntime.ShouldSkipVanillaGetDame(__instance))
            return false;

        return !BossTouzokuCustomRuntime.TryApplyCustomWeaponHit(__instance);
    }
}

[HarmonyPatch(typeof(BossTouzoku), "getMGdame_fun")]
internal static class BossTouzokuCustomMagicDamagePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        if (__instance.Hp <= 0f)
        {
            BossTouzokuCustomRuntime.HandleFieldMobDeath(__instance);
            return;
        }

        BossTouzokuCustomRuntime.ApplyFieldMobHitReaction(__instance);
        BossTouzokuCustomRuntime.ClearMagicHitFlash(__instance);
    }
}

[HarmonyPatch(typeof(BossTouzoku), "Distance_fun")]
internal static class BossTouzokuCustomDistanceCapPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        BossTouzokuCustomRuntime.AlignFieldMobMoveDistanceForPatch(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        BossTouzokuCustomRuntime.CapFieldMobDetection(__instance);
    }
}

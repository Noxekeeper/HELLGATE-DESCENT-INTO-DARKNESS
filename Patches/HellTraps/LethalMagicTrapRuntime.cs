using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Template registration, lethal bullet setup, hit finalization, and forced player death for lethal_magictrap.
/// </summary>
internal static class LethalMagicTrapRuntime
{
    internal const float VanillaSetupFireballAtk = 70f;

    internal static readonly FieldInfo MagictrapBulletField =
        AccessTools.Field(typeof(Magictrap), "bullet");

    internal static readonly FieldInfo MagictrapEffectField =
        AccessTools.Field(typeof(Magictrap), "effect");

    private static readonly FieldInfo MagictrapActtimeField =
        AccessTools.Field(typeof(Magictrap), "acttime");

    private static readonly FieldInfo SetupFireballXspdField =
        AccessTools.Field(typeof(SetupFireball), "Xspd");

    private static readonly FieldInfo SetupFireballYspdField =
        AccessTools.Field(typeof(SetupFireball), "Yspd");

    private static readonly FieldInfo SetupFireballStartYspdField =
        AccessTools.Field(typeof(SetupFireball), "startYspd");

    private static readonly FieldInfo FireballXspdField =
        AccessTools.Field(typeof(Fireball), "Xspd");

    private static readonly FieldInfo FireballYspdField =
        AccessTools.Field(typeof(Fireball), "Yspd");

    private static readonly FieldInfo SetupFireballEnmAtkField =
        AccessTools.Field(typeof(SetupFireball), "enmATK");

    private static readonly FieldInfo FireballEnmAtkField =
        AccessTools.Field(typeof(Fireball), "enmATK");

    private static readonly FieldInfo SetupFireballComPlayerField =
        AccessTools.Field(typeof(SetupFireball), "com_player");

    private static readonly FieldInfo SetupFireballDirField =
        AccessTools.Field(typeof(SetupFireball), "DIR");

    private static readonly FieldInfo SetupFireballDamecountField =
        AccessTools.Field(typeof(SetupFireball), "damecount");

    private static readonly FieldInfo SetupFireballEffectField =
        AccessTools.Field(typeof(SetupFireball), "effect");

    private static readonly FieldInfo FireballComPlayerField =
        AccessTools.Field(typeof(Fireball), "com_player");

    private static readonly FieldInfo FireballDirField =
        AccessTools.Field(typeof(Fireball), "DIR");

    private static readonly FieldInfo FireballDamecountField =
        AccessTools.Field(typeof(Fireball), "damecount");

    private static readonly FieldInfo FireballEffectField =
        AccessTools.Field(typeof(Fireball), "effect");

    private static bool _registerAttempted;
    private static bool _loggedMissingBulletComponent;
    private static bool _loggedCreateObj;
    private static readonly HashSet<int> _lethalBulletInstanceIds = new HashSet<int>();
    private static int _liveLethalBulletCount;

    internal static bool HasLiveLethalBullets()
    {
        return _liveLethalBulletCount > 0;
    }

    internal static void TryEnsureTemplateRegistered()
    {
        if (!Plugin.enableLethalMagicTrap.Value)
            return;

        if (SpawnTemplateCatalog.HasTemplate(LethalMagicTrapPaths.TemplateKey))
        {
            if (_registerAttempted)
                return;
        }

        _registerAttempted = true;

        if (!SpawnTemplateCatalog.HasTemplate("magictrap"))
            SpawnTemplateCatalog.TryCacheFromResources("magictrap");

        if (!SpawnTemplateCatalog.TryGetTrapTemplate("magictrap", out GameObject baseTemplate) ||
            baseTemplate == null)
        {
            Plugin.Log?.LogWarning(
                "[LethalMagicTrap] Base template 'magictrap' is not cached yet; lethal variant will register later.");
            _registerAttempted = false;
            return;
        }

        GameObject lethalTemplate = Object.Instantiate(baseTemplate);
        if (lethalTemplate == null)
            return;

        lethalTemplate.name = "HellGateTrapTemplate_LethalMagictrap";
        lethalTemplate.SetActive(false);
        Object.DontDestroyOnLoad(lethalTemplate);
        ConfigureSpawnedTrap(lethalTemplate, logSpawn: false);

        Magictrap trap = lethalTemplate.GetComponent<Magictrap>();
        if (trap == null)
        {
            Object.Destroy(lethalTemplate);
            Plugin.Log?.LogWarning("[LethalMagicTrap] magictrap template has no Magictrap component.");
            return;
        }

        if (!SpawnTemplateCatalog.TryRegisterCustomTrapTemplate(
                LethalMagicTrapPaths.TemplateKey,
                lethalTemplate))
        {
            Object.Destroy(lethalTemplate);
            return;
        }

        SpawnTemplateCatalog.TryRegisterCustomTrapTemplate(
            LethalMagicTrapPaths.LegacyTemplateKeyAlias,
            lethalTemplate);

        float multiplier = GetDamageMultiplier();
        Plugin.Log?.LogInfo(
            "[LethalMagicTrap] Registered spawn template key '"
            + LethalMagicTrapPaths.TemplateKey
            + "' (vanilla bullet, damage x"
            + multiplier.ToString("0.##")
            + " ~= "
            + GetLethalShotAtk().ToString("0.##")
            + " per hit).");
    }

    internal static void ConfigureSpawnedTrap(GameObject spawnedTrap, bool logSpawn = false)
    {
        if (spawnedTrap == null || !Plugin.enableLethalMagicTrap.Value)
            return;

        if (spawnedTrap.GetComponent<Magictrap>() == null)
            return;

        if (spawnedTrap.GetComponent<HellGateLethalMagicTrapMarker>() == null)
            spawnedTrap.AddComponent<HellGateLethalMagicTrapMarker>();

        LethalTrapDangerThoughts.EnsureAnchor(spawnedTrap, "LethalMagicTrap");
        ApplyTrapInstanceTuning(spawnedTrap);

        if (logSpawn)
        {
            Plugin.Log?.LogInfo(
                "[LethalMagicTrap] Spawned trap instance '"
                + spawnedTrap.name
                + "' @ "
                + spawnedTrap.transform.position
                + " (marker attached).");
        }
    }

    internal static bool IsLethalTrap(Magictrap trap)
    {
        return trap != null && trap.GetComponent<HellGateLethalMagicTrapMarker>() != null;
    }

    internal static float GetDamageMultiplier()
    {
        return Mathf.Max(1f, Plugin.lethalMagicTrapDamageMultiplier.Value);
    }

    internal static float GetLethalShotAtk()
    {
        return VanillaSetupFireballAtk * GetDamageMultiplier();
    }

    internal static bool TryApplyLethalShotDamage(Component bulletDamageComponent)
    {
        if (bulletDamageComponent == null)
            return false;

        FieldInfo enmAtkField = ResolveEnmAtkField(bulletDamageComponent);
        if (enmAtkField == null)
            return false;

        enmAtkField.SetValue(bulletDamageComponent, GetLethalShotAtk());
        return true;
    }

    internal static bool IsLethalBullet(Component component)
    {
        if (component == null)
            return false;

        Transform node = component.transform;
        while (node != null)
        {
            if (_lethalBulletInstanceIds.Contains(node.gameObject.GetInstanceID()))
                return true;

            if (node.GetComponent<LethalMagicTrapBulletMarker>() != null)
                return true;

            node = node.parent;
        }

        return ReadBulletEnmAtk(component) >= GetLethalShotAtk() * 0.5f;
    }

    internal static bool IsLethalDamageAmount(float getatk)
    {
        return getatk >= GetLethalShotAtk() * 0.5f;
    }

    /// <summary>Marks pending custom death before vanilla OnTriggerEnter2D applies trap damage.</summary>
    internal static void TryMarkLethalHitPending(Component bulletComponent, Collider2D col)
    {
        if (!Plugin.enableLethalMagicTrap.Value || bulletComponent == null || col == null)
            return;

        if (col.gameObject == null || col.gameObject.tag != "playerDAMAGEcol")
            return;

        if (!IsLethalBullet(bulletComponent))
            return;

        LethalMagicTrapDeathContext.MarkPending();
        Plugin.Log?.LogInfo(
            "[LethalMagicTrap] Lethal trap hit (vanilla path), enmATK="
            + ReadBulletEnmAtk(bulletComponent).ToString("0.##")
            + "; finalize after damage.");
    }

    internal static void ConfigureSpawnedBullet(GameObject spawnedBullet)
    {
        if (spawnedBullet == null)
            return;

        Component damageComponent = ResolveBulletDamageComponent(spawnedBullet);
        if (damageComponent == null)
        {
            if (!_loggedMissingBulletComponent)
            {
                _loggedMissingBulletComponent = true;
                Plugin.Log?.LogWarning(
                    "[LethalMagicTrap] Spawned bullet has no SetupFireball/Fireball component; lethal damage will not apply.");
            }

            return;
        }

        if (!TryApplyLethalShotDamage(damageComponent))
            return;

        ApplyBulletSpeedTuning(damageComponent);

        if (spawnedBullet.GetComponent<LethalMagicTrapBulletMarker>() == null)
            spawnedBullet.AddComponent<LethalMagicTrapBulletMarker>();

        RegisterLethalBullet(spawnedBullet, damageComponent);

        if (!_loggedCreateObj)
        {
            _loggedCreateObj = true;
            Plugin.Log?.LogInfo(
                "[LethalMagicTrap] Lethal bullet configured ("
                + damageComponent.GetType().Name
                + ", enmATK="
                + GetLethalShotAtk().ToString("0.##")
                + "). Armed custom death on next player hit.");
        }
        else
        {
            Plugin.Log?.LogInfo("[LethalMagicTrap] Lethal bullet armed for custom death clip.");
        }
    }

    /// <summary>
    /// Returns true when the hit was fully handled and vanilla OnTriggerEnter2D should be skipped.
    /// </summary>
    internal static bool TryHandleLethalBulletHit(Component bulletComponent, Collider2D col)
    {
        if (!Plugin.enableLethalMagicTrap.Value || bulletComponent == null)
            return false;

        if (!IsLethalBullet(bulletComponent))
            return false;

        if (col == null || col.gameObject == null ||
            col.gameObject.tag != "playerDAMAGEcol")
        {
            return false;
        }

        playercon player = ResolveBulletPlayer(bulletComponent);
        if (player == null)
            return false;

        if (player.stepfrag)
            return true;

        GameObject effectPrefab = ResolveBulletEffect(bulletComponent);
        Vector2 hitPos = bulletComponent.transform.position;

        if (player._parrynow)
        {
            if (effectPrefab != null)
                Object.Instantiate(effectPrefab, hitPos, bulletComponent.transform.rotation);

            Object.Destroy(bulletComponent.gameObject);
            return true;
        }

        TryApplyLethalShotDamage(bulletComponent);
        LethalMagicTrapDeathContext.MarkPending();
        LethalMagicTrapDeathContext.MarkBulletHitDealtDamage();
        LethalMagicTrapDeathContext.IsLethalDamageInFlight = true;
        try
        {
            float atk = GetLethalShotAtk();
            int dir = ResolveBulletDir(bulletComponent);
            float damecount = ResolveBulletDamecount(bulletComponent);
            player.fun_damage(atk, 999f, 0, dir, damecount);
        }
        finally
        {
            LethalMagicTrapDeathContext.IsLethalDamageInFlight = false;
        }

        if (effectPrefab != null)
            LethalMagicTrapDeathContext.QueueHitEffect(effectPrefab, bulletComponent.transform.rotation);

        FinalizeLethalBulletHit(player);

        Object.Destroy(bulletComponent.gameObject);
        return true;
    }

    /// <summary>After lethal bullet damage: force 0 HP + death menu, then play custom clip.</summary>
    internal static void FinalizeLethalBulletHit(playercon player)
    {
        if (!Plugin.enableLethalMagicTrap.Value || player == null)
            return;

        if (!LethalMagicTrapDeathContext.HasPending &&
            !LethalMagicTrapDeathContext.BulletHitDealtDamage &&
            !LethalMagicTrapDeathContext.IsLethalTrapDamageArmed)
        {
            return;
        }

        LethalTrapDeathCommon.FinalizeLethalDeathWithClip(
            player,
            "LethalMagicTrap",
            ClearMagicHitStateForClip,
            () =>
            {
                LethalMagicTrapDeathDisplay.TryApply(player);
                LethalMagicTrapDeathDisplay.ScheduleDeferredApply(player);
            });
    }

    private static void ClearMagicHitStateForClip()
    {
        LethalMagicTrapDeathContext.ClearPending();
        LethalMagicTrapDeathContext.ClearBulletHitDealtDamage();
        LethalMagicTrapDeathContext.ClearLethalTrapDamageArmed();
    }

    private static void RegisterLethalBullet(GameObject spawnedBullet, Component damageComponent)
    {
        if (spawnedBullet != null)
            _lethalBulletInstanceIds.Add(spawnedBullet.GetInstanceID());

        if (damageComponent != null)
            _lethalBulletInstanceIds.Add(damageComponent.gameObject.GetInstanceID());

        _liveLethalBulletCount++;
        LethalMagicTrapDeathContext.ArmLethalTrapPlayerHit();
    }

    internal static void NotifyLethalBulletDestroyed(GameObject bulletRoot)
    {
        if (bulletRoot == null)
            return;

        bool removedTrackedId = RemoveTrackedIdsInHierarchy(bulletRoot.transform);
        if (!removedTrackedId)
        {
            LethalMagicTrapDeathContext.TryClearStaleArmState();
            return;
        }

        if (_liveLethalBulletCount > 0)
            _liveLethalBulletCount--;

        LethalMagicTrapDeathContext.TryClearStaleArmState();
    }

    private static bool RemoveTrackedIdsInHierarchy(Transform root)
    {
        if (root == null)
            return false;

        bool removed = false;
        Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (node == null)
                continue;

            if (_lethalBulletInstanceIds.Remove(node.gameObject.GetInstanceID()))
                removed = true;
        }

        return removed;
    }

    internal static void ApplyLethalTrapDeathSlowMo(playercon player) =>
        LethalTrapDeathCommon.ApplyDeathSlowMo(player);

    internal static void ClearLethalTrapDeathSlowMo(playercon player) =>
        LethalTrapDeathCommon.ClearDeathSlowMo(player);

    internal static bool TryFireLethalTrapShot(Magictrap trap)
    {
        if (!IsLethalTrap(trap))
            return false;

        _magictrapBulletField ??= MagictrapBulletField;
        _magictrapEffectField ??= MagictrapEffectField;
        if (_magictrapBulletField == null)
            return false;

        GameObject bulletPrefab = _magictrapBulletField.GetValue(trap) as GameObject;
        if (bulletPrefab == null)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrap] Magictrap.bullet prefab is null on lethal trap instance.");
            return false;
        }

        Vector3 pos = trap.transform.position;
        LethalMagicTrapDeathContext.SetTrapFloorWorld(pos);
        GameObject spawnedBullet = Object.Instantiate(
            bulletPrefab,
            new Vector2(pos.x, pos.y + 1f),
            trap.transform.rotation);

        GameObject effectPrefab = _magictrapEffectField?.GetValue(trap) as GameObject;
        if (effectPrefab != null)
        {
            Object.Instantiate(
                effectPrefab,
                new Vector2(pos.x, pos.y),
                trap.transform.rotation);
        }

        ConfigureSpawnedBullet(spawnedBullet);
        Object.Destroy(trap.gameObject);
        return true;
    }

    private static FieldInfo _magictrapBulletField;
    private static FieldInfo _magictrapEffectField;

    private static Component ResolveBulletDamageComponent(GameObject spawnedBullet)
    {
        SetupFireball setupFireball = spawnedBullet.GetComponent<SetupFireball>();
        if (setupFireball != null)
            return setupFireball;

        Fireball fireball = spawnedBullet.GetComponent<Fireball>();
        if (fireball != null)
            return fireball;

        setupFireball = spawnedBullet.GetComponentInChildren<SetupFireball>(true);
        if (setupFireball != null)
            return setupFireball;

        fireball = spawnedBullet.GetComponentInChildren<Fireball>(true);
        return fireball;
    }

    private static float ReadBulletEnmAtk(Component bulletDamageComponent)
    {
        FieldInfo enmAtkField = ResolveEnmAtkField(bulletDamageComponent);
        if (enmAtkField == null)
            return 0f;

        object value = enmAtkField.GetValue(bulletDamageComponent);
        return value is float f ? f : 0f;
    }

    private static FieldInfo ResolveEnmAtkField(Component bulletDamageComponent)
    {
        if (bulletDamageComponent is SetupFireball)
            return SetupFireballEnmAtkField;

        if (bulletDamageComponent is Fireball)
            return FireballEnmAtkField;

        return AccessTools.Field(bulletDamageComponent.GetType(), "enmATK");
    }

    internal static playercon ResolveBulletPlayerForPatch(Component bulletComponent)
    {
        return ResolveBulletPlayer(bulletComponent);
    }

    private static playercon ResolveBulletPlayer(Component bulletComponent)
    {
        if (bulletComponent is SetupFireball)
        {
            playercon player = SetupFireballComPlayerField?.GetValue(bulletComponent) as playercon;
            if (player != null)
                return player;
        }

        if (bulletComponent is Fireball)
        {
            playercon player = FireballComPlayerField?.GetValue(bulletComponent) as playercon;
            if (player != null)
                return player;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        return playerObj != null ? playerObj.GetComponent<playercon>() : null;
    }

    private static GameObject ResolveBulletEffect(Component bulletComponent)
    {
        if (bulletComponent is SetupFireball)
            return SetupFireballEffectField?.GetValue(bulletComponent) as GameObject;
        if (bulletComponent is Fireball)
            return FireballEffectField?.GetValue(bulletComponent) as GameObject;
        return null;
    }

    private static int ResolveBulletDir(Component bulletComponent)
    {
        object value = bulletComponent is SetupFireball
            ? SetupFireballDirField?.GetValue(bulletComponent)
            : FireballDirField?.GetValue(bulletComponent);
        return value is int i ? i : 1;
    }

    private static float ResolveBulletDamecount(Component bulletComponent)
    {
        object value = bulletComponent is SetupFireball
            ? SetupFireballDamecountField?.GetValue(bulletComponent)
            : FireballDamecountField?.GetValue(bulletComponent);
        return value is float f ? f : 0f;
    }

    private static void ApplyTrapInstanceTuning(GameObject spawnedTrap)
    {
        if (spawnedTrap == null)
            return;

        Magictrap trap = spawnedTrap.GetComponent<Magictrap>();
        if (trap == null)
            return;

        float actMult = Plugin.lethalMagicTrapActTimeMultiplier != null
            ? Mathf.Max(0.05f, Plugin.lethalMagicTrapActTimeMultiplier.Value)
            : 1f;
        if (Mathf.Abs(actMult - 1f) > 0.001f && MagictrapActtimeField != null)
        {
            object current = MagictrapActtimeField.GetValue(trap);
            if (current is float actTime)
                MagictrapActtimeField.SetValue(trap, actTime * actMult);
        }

        float scale = Plugin.lethalMagicTrapSpawnScale != null
            ? Mathf.Max(0.1f, Plugin.lethalMagicTrapSpawnScale.Value)
            : 1f;
        if (Mathf.Abs(scale - 1f) > 0.001f)
            spawnedTrap.transform.localScale = spawnedTrap.transform.localScale * scale;
    }

    private static void ApplyBulletSpeedTuning(Component bulletDamageComponent)
    {
        if (bulletDamageComponent == null)
            return;

        float mult = Plugin.lethalMagicTrapBulletSpeedMultiplier != null
            ? Mathf.Max(0.05f, Plugin.lethalMagicTrapBulletSpeedMultiplier.Value)
            : 1f;
        if (Mathf.Abs(mult - 1f) < 0.001f)
            return;

        if (bulletDamageComponent is SetupFireball)
        {
            MultiplyField(SetupFireballXspdField, bulletDamageComponent, mult);
            MultiplyField(SetupFireballYspdField, bulletDamageComponent, mult);
            MultiplyField(SetupFireballStartYspdField, bulletDamageComponent, mult);
            return;
        }

        if (bulletDamageComponent is Fireball)
        {
            MultiplyField(FireballXspdField, bulletDamageComponent, mult);
            MultiplyField(FireballYspdField, bulletDamageComponent, mult);
        }
    }

    private static void MultiplyField(FieldInfo field, object target, float multiplier)
    {
        if (field == null || target == null)
            return;

        object value = field.GetValue(target);
        if (value is float f)
            field.SetValue(target, f * multiplier);
    }
}

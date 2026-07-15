using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Template registration and lethal hit finalization for lethal_cocoontrap (Cocoontrap base).</summary>
internal static class LethalCocoonTrapRuntime
{
    private static readonly FieldInfo CocoonEnmAtkField =
        AccessTools.Field(typeof(Cocoontrap), "enmATK");

    private static readonly FieldInfo CocoonActtimeField =
        AccessTools.Field(typeof(Cocoontrap), "Acttime");

    private static readonly FieldInfo CocoonComPlayerField =
        AccessTools.Field(typeof(Cocoontrap), "com_player");

    private static readonly FieldInfo CocoonDirField =
        AccessTools.Field(typeof(Cocoontrap), "DIR");

    private static readonly FieldInfo CocoonDamecountField =
        AccessTools.Field(typeof(Cocoontrap), "damecount");

    private static readonly FieldInfo CocoonKickbackkindField =
        AccessTools.Field(typeof(Cocoontrap), "kickbackkind");

    private static readonly FieldInfo CocoonAtkTrapField =
        AccessTools.Field(typeof(Cocoontrap), "AtkTrap");

    private static readonly FieldInfo CocoonWarningIconField =
        AccessTools.Field(typeof(Cocoontrap), "warningIcon");

    private static bool _registerAttempted;
    private static bool _finalizeConsumedThisHit;

    internal static void ResetFinalizeGuard()
    {
        _finalizeConsumedThisHit = false;
    }

    internal static void TryEnsureTemplateRegistered()
    {
        if (!Plugin.enableLethalCocoonTrap.Value)
            return;

        if (SpawnTemplateCatalog.HasTemplate(LethalCocoonTrapPaths.TemplateKey) && _registerAttempted)
            return;

        _registerAttempted = true;

        if (!SpawnTemplateCatalog.HasTemplate("cocoontrap"))
            SpawnTemplateCatalog.TryCacheFromResources("cocoontrap");

        if (!SpawnTemplateCatalog.TryGetTrapTemplate("cocoontrap", out GameObject baseTemplate) ||
            baseTemplate == null)
        {
            Plugin.Log?.LogWarning(
                "[LethalCocoonTrap] Base template 'cocoontrap' is not cached yet; lethal variant will register later.");
            _registerAttempted = false;
            return;
        }

        GameObject lethalTemplate = Object.Instantiate(baseTemplate);
        if (lethalTemplate == null)
            return;

        lethalTemplate.name = "HellGateTrapTemplate_LethalCocoontrap";
        lethalTemplate.SetActive(false);
        Object.DontDestroyOnLoad(lethalTemplate);
        ConfigureSpawnedTrap(lethalTemplate, logSpawn: false);
        LethalCocoonTrapRegistry.Register(lethalTemplate);

        if (ResolveCocoontrapComponent(lethalTemplate) == null)
        {
            Object.Destroy(lethalTemplate);
            Plugin.Log?.LogWarning("[LethalCocoonTrap] cocoontrap template has no Cocoontrap component.");
            return;
        }

        if (!SpawnTemplateCatalog.TryRegisterCustomTrapTemplate(
                LethalCocoonTrapPaths.TemplateKey,
                lethalTemplate))
        {
            Object.Destroy(lethalTemplate);
            return;
        }

        SpawnTemplateCatalog.TryRegisterCustomTrapTemplate(
            LethalCocoonTrapPaths.LegacyTemplateKeyAlias,
            lethalTemplate);

        Plugin.Log?.LogInfo(
            "[LethalCocoonTrap] Registered spawn template key '"
            + LethalCocoonTrapPaths.TemplateKey
            + "' (vanilla cocoon, damage x"
            + Plugin.lethalMagicTrapDamageMultiplier.Value.ToString("0.##")
            + " ~= "
            + GetLethalAtk().ToString("0.##")
            + " per hit, same formula as lethal_magictrap).");
    }

    internal static void ConfigureSpawnedTrap(GameObject spawnedTrap, bool logSpawn = false)
    {
        if (spawnedTrap == null || !Plugin.enableLethalCocoonTrap.Value)
            return;

        Cocoontrap trapComponent = ResolveCocoontrapComponent(spawnedTrap);
        if (trapComponent == null)
            return;

        if (spawnedTrap.GetComponent<HellGateLethalCocoonTrapMarker>() == null)
            spawnedTrap.AddComponent<HellGateLethalCocoonTrapMarker>();

        if (spawnedTrap.GetComponent<HellGateLethalCocoonTrapTracker>() == null)
            spawnedTrap.AddComponent<HellGateLethalCocoonTrapTracker>();

        LethalTrapDangerThoughts.EnsureAnchor(spawnedTrap, "LethalCocoonTrap");
        LethalCocoonTrapRegistry.Register(spawnedTrap);

        ApplyTrapInstanceTuning(spawnedTrap, trapComponent);

        if (logSpawn)
        {
            Plugin.Log?.LogInfo(
                "[LethalCocoonTrap] Spawned trap instance '"
                + spawnedTrap.name
                + "' @ "
                + spawnedTrap.transform.position
                + " (marker attached).");
        }
    }

    internal static bool IsLethalTrap(Cocoontrap trap)
    {
        return LethalCocoonTrapRegistry.IsLethalCocoonTrap(trap);
    }

    /// <summary>Same lethal damage as lethal_magictrap (~7000 with default x100 multiplier).</summary>
    internal static float GetLethalAtk()
    {
        return LethalMagicTrapRuntime.GetLethalShotAtk();
    }

    internal static bool IsLethalDamageAmount(float getatk)
    {
        return LethalMagicTrapRuntime.IsLethalDamageAmount(getatk);
    }

    /// <summary>Intercept playerDamage before ExecuteEvents (parent may not be Cocoontrap).</summary>
    internal static bool TryHandleLethalPlayerDamage(playerDamage source, Collider2D col)
    {
        if (!Plugin.enableLethalCocoonTrap.Value || source == null || col == null)
            return false;

        if (!LethalCocoonTrapRegistry.IsLethalCocoonTrap(source))
            return false;

        Cocoontrap trap = source.GetComponentInParent<Cocoontrap>();
        Transform anchor = trap != null ? trap.transform : source.transform.root;

        playercon player = col.transform.root.GetComponent<playercon>();
        if (player == null)
            player = ResolveTrapPlayer(trap);

        return TryApplyLethalHit(trap, anchor, player);
    }

    /// <summary>Handles Cocoontrap.OndamageSend for lethal variant; returns true if vanilla should be skipped.</summary>
    internal static bool TryHandleLethalDamageSend(Cocoontrap trap, string tag)
    {
        if (!Plugin.enableLethalCocoonTrap.Value || trap == null)
            return false;

        if (!IsLethalTrap(trap))
            return false;

        if (tag != "playerDAMAGEcol")
            return true;

        playercon player = ResolveTrapPlayer(trap);
        return TryApplyLethalHit(trap, trap.transform, player);
    }

    private static bool TryApplyLethalHit(Cocoontrap trap, Transform anchor, playercon player)
    {
        if (anchor == null || player == null)
            return false;

        if (player.stepfrag)
            return true;

        LethalMagicTrapDeathContext.ClearMagicHitState();

        Vector3 trapPos = anchor.position;
        LethalCocoonTrapDeathContext.SetTrapAnchorWorld(trapPos);
        LethalMagicTrapDeathContext.SetTrapFloorWorld(trapPos);
        LethalCocoonTrapDeathContext.MarkPending();

        int dir = trap != null ? ResolveTrapDir(trap) : 1;
        float damecount = trap != null ? ResolveTrapDamecount(trap) : 1.2f;
        float atk = GetLethalAtk();

        Plugin.Log?.LogInfo(
            "[LethalCocoonTrap] Lethal cocoon hit @ "
            + trapPos
            + ", enmATK="
            + atk.ToString("0.##"));

        LethalCocoonTrapDeathContext.MarkHitDealtDamage();
        _finalizeConsumedThisHit = false;

        LethalMagicTrapEroSuppression.PinPlayerBody(player);
        LethalMagicTrapEroSuppression.SuppressEnemyEroApproach(forceImmediate: true);

        LethalCocoonTrapDeathContext.IsLethalDamageInFlight = true;
        try
        {
            player.fun_damage(atk, 999f, 0, dir, damecount);
        }
        finally
        {
            LethalCocoonTrapDeathContext.IsLethalDamageInFlight = false;
        }

        FinalizeLethalHit(player);
        return true;
    }

    internal static void FinalizeLethalHit(playercon player)
    {
        if (!Plugin.enableLethalCocoonTrap.Value || player == null)
            return;

        if (_finalizeConsumedThisHit)
            return;

        if (!LethalCocoonTrapDeathContext.HasPending &&
            !LethalCocoonTrapDeathContext.HitDealtDamage)
        {
            return;
        }

        _finalizeConsumedThisHit = true;

        LethalTrapDeathCommon.FinalizeLethalDeathWithClip(
            player,
            "LethalCocoonTrap",
            ClearCocoonHitStateForClip,
            () =>
            {
                LethalCocoonTrapDeathDisplay.TryApply(player);
                LethalCocoonTrapDeathDisplay.ScheduleDeferredApply(player);
            },
            applySlowMoImmediately: false);
    }

    private static void ClearCocoonHitStateForClip()
    {
        LethalCocoonTrapDeathContext.ClearPending();
        LethalCocoonTrapDeathContext.ClearHitDealtDamage();
    }

    private static playercon ResolveTrapPlayer(Cocoontrap trap)
    {
        playercon player = CocoonComPlayerField?.GetValue(trap) as playercon;
        if (player != null)
            return player;

        GameObject playerObj = GameObject.FindWithTag("Player");
        return playerObj != null ? playerObj.GetComponent<playercon>() : null;
    }

    private static int ResolveTrapDir(Cocoontrap trap)
    {
        object value = CocoonDirField?.GetValue(trap);
        return value is int i ? i : 1;
    }

    private static float ResolveTrapDamecount(Cocoontrap trap)
    {
        object value = CocoonDamecountField?.GetValue(trap);
        return value is float f ? f : 1.2f;
    }

    private static Cocoontrap ResolveCocoontrapComponent(GameObject spawnedTrap)
    {
        if (spawnedTrap == null)
            return null;

        Cocoontrap trap = spawnedTrap.GetComponent<Cocoontrap>();
        if (trap != null)
            return trap;

        return spawnedTrap.GetComponentInChildren<Cocoontrap>(true);
    }

    private static void ApplyTrapInstanceTuning(GameObject spawnedTrap, Cocoontrap trap)
    {
        if (spawnedTrap == null || trap == null)
            return;

        if (CocoonEnmAtkField != null)
            CocoonEnmAtkField.SetValue(trap, GetLethalAtk());

        if (CocoonKickbackkindField != null)
            CocoonKickbackkindField.SetValue(trap, 0);

        if (CocoonAtkTrapField != null)
            CocoonAtkTrapField.SetValue(trap, false);

        // Vanilla warningIcon at trap Y+1 looks like a tiny red "?" — not used for lethal variant.
        if (CocoonWarningIconField != null)
            CocoonWarningIconField.SetValue(trap, null);

        float actMult = Plugin.lethalMagicTrapActTimeMultiplier != null
            ? Mathf.Max(0.05f, Plugin.lethalMagicTrapActTimeMultiplier.Value)
            : 1f;
        if (Mathf.Abs(actMult - 1f) > 0.001f && CocoonActtimeField != null)
        {
            object current = CocoonActtimeField.GetValue(trap);
            if (current is float actTime)
                CocoonActtimeField.SetValue(trap, actTime * actMult);
        }

        float scale = Plugin.lethalMagicTrapSpawnScale != null
            ? Mathf.Max(0.1f, Plugin.lethalMagicTrapSpawnScale.Value)
            : 1f;
        if (Mathf.Abs(scale - 1f) > 0.001f)
            spawnedTrap.transform.localScale = spawnedTrap.transform.localScale * scale;
    }
}

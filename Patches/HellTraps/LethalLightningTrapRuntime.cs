using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Template registration and lethal strike finalization for lightningTrap_button.</summary>
internal static class LethalLightningTrapRuntime
{
    private static readonly FieldInfo TrapButtonShotsField =
        AccessTools.Field(typeof(Trap_button), "trap");

    private static bool _registerAttempted;
    private static bool _finalizeConsumedThisHit;

    internal static void ResetFinalizeGuard()
    {
        _finalizeConsumedThisHit = false;
    }

    internal static void TryEnsureTemplateRegistered()
    {
        if (!Plugin.IsLethalLightningTrapActive)
            return;

        if (SpawnTemplateCatalog.HasTemplate(LethalLightningTrapPaths.TemplateKey) && _registerAttempted)
            return;

        _registerAttempted = true;

        if (!SpawnTemplateCatalog.HasTemplate("trap_button"))
            SpawnTemplateCatalog.TryCacheFromResources("trap_button");

        if (!SpawnTemplateCatalog.TryGetTrapTemplate("trap_button", out GameObject baseTemplate) ||
            baseTemplate == null)
        {
            Plugin.Log?.LogWarning(
                "[LethalLightningTrap] Base template 'trap_button' is not cached yet; lethal variant will register later.");
            _registerAttempted = false;
            return;
        }

        GameObject lethalTemplate = Object.Instantiate(baseTemplate);
        if (lethalTemplate == null)
            return;

        lethalTemplate.name = "HellGateTrapTemplate_LethalLightningButton";
        lethalTemplate.SetActive(false);
        Object.DontDestroyOnLoad(lethalTemplate);
        ConfigureSpawnedTrap(lethalTemplate, logSpawn: false);

        if (ResolveTrapButton(lethalTemplate) == null)
        {
            Object.Destroy(lethalTemplate);
            Plugin.Log?.LogWarning("[LethalLightningTrap] trap_button template has no Trap_button component.");
            return;
        }

        if (!SpawnTemplateCatalog.TryRegisterCustomTrapTemplate(
                LethalLightningTrapPaths.TemplateKey,
                lethalTemplate))
        {
            Object.Destroy(lethalTemplate);
            return;
        }

        SpawnTemplateCatalog.TryRegisterCustomTrapTemplate(
            LethalLightningTrapPaths.LegacyTemplateKeyAlias,
            lethalTemplate);

        Plugin.Log?.LogInfo(
            "[LethalLightningTrap] Registered spawn template key '"
            + LethalLightningTrapPaths.TemplateKey
            + "' (trap_button base, lethal ATK ~= "
            + GetLethalAtk().ToString("0.##")
            + ", delay "
            + (Plugin.lethalLightningTrapWarningDelay != null
                ? Plugin.lethalLightningTrapWarningDelay.Value.ToString("0.##")
                : "1.2")
            + "s).");
    }

    internal static void ConfigureSpawnedTrap(GameObject spawnedTrap, bool logSpawn = false)
    {
        if (spawnedTrap == null || !Plugin.IsLethalLightningTrapActive)
            return;

        Trap_button button = ResolveTrapButton(spawnedTrap);
        if (button == null)
            return;

        if (spawnedTrap.GetComponent<HellGateLethalLightningTrapMarker>() == null)
            spawnedTrap.AddComponent<HellGateLethalLightningTrapMarker>();

        if (spawnedTrap.GetComponent<HellGateLethalLightningTrapTracker>() == null)
            spawnedTrap.AddComponent<HellGateLethalLightningTrapTracker>();

        HellGateLethalLightningTrapArmer armer =
            button.GetComponent<HellGateLethalLightningTrapArmer>();
        if (armer == null)
            armer = button.gameObject.AddComponent<HellGateLethalLightningTrapArmer>();

        ClearVanillaShots(button);
        LethalTrapDangerThoughts.EnsureAnchor(spawnedTrap, "LethalLightningTrap");
        LethalLightningTrapRegistry.Register(spawnedTrap);

        float scale = Plugin.lethalLightningTrapSpawnScale != null
            ? Mathf.Max(0.1f, Plugin.lethalLightningTrapSpawnScale.Value)
            : 1f;
        if (Mathf.Abs(scale - 1f) > 0.001f)
            spawnedTrap.transform.localScale = spawnedTrap.transform.localScale * scale;

        if (logSpawn)
        {
            Plugin.Log?.LogInfo(
                "[LethalLightningTrap] Spawned '"
                + spawnedTrap.name
                + "' @ "
                + spawnedTrap.transform.position);
        }
    }

    internal static float GetLethalAtk()
    {
        return LethalMagicTrapRuntime.GetLethalShotAtk();
    }

    internal static void ApplyLethalStrike(playercon player, Vector3 trapPos)
    {
        if (!Plugin.IsLethalLightningTrapActive || player == null)
            return;

        if (NoREroMod.Systems.EnemyFatality.EnemyFatalitySession.IsActive)
            return;

        LethalMagicTrapDeathContext.ClearMagicHitState();
        LethalLightningTrapDeathContext.SetTrapAnchorWorld(trapPos);
        LethalMagicTrapDeathContext.SetTrapFloorWorld(trapPos);
        LethalLightningTrapDeathContext.MarkPending();
        LethalLightningTrapDeathContext.MarkHitDealtDamage();
        _finalizeConsumedThisHit = false;

        float atk = GetLethalAtk();
        Plugin.Log?.LogInfo(
            "[LethalLightningTrap] Lethal strike @ "
            + trapPos
            + ", enmATK="
            + atk.ToString("0.##"));

        LethalMagicTrapEroSuppression.PinPlayerBody(player);
        LethalMagicTrapEroSuppression.SuppressEnemyEroApproach(forceImmediate: true);

        LethalLightningTrapDeathContext.IsLethalDamageInFlight = true;
        try
        {
            player.fun_damage(atk, 999f, 0, 1, 1.2f);
        }
        finally
        {
            LethalLightningTrapDeathContext.IsLethalDamageInFlight = false;
        }

        FinalizeLethalHit(player);
    }

    internal static void FinalizeLethalHit(playercon player)
    {
        if (!Plugin.IsLethalLightningTrapActive || player == null)
            return;

        if (_finalizeConsumedThisHit)
            return;

        if (!LethalLightningTrapDeathContext.HasPending &&
            !LethalLightningTrapDeathContext.HitDealtDamage)
        {
            return;
        }

        _finalizeConsumedThisHit = true;

        LethalTrapDeathCommon.FinalizeLethalDeathWithClip(
            player,
            "LethalLightningTrap",
            ClearHitStateForClip,
            () =>
            {
                LethalLightningTrapDeathDisplay.TryApply(player);
                LethalLightningTrapDeathDisplay.ScheduleDeferredApply(player);
            },
            applySlowMoImmediately: false);
    }

    private static void ClearHitStateForClip()
    {
        LethalLightningTrapDeathContext.ClearPending();
        LethalLightningTrapDeathContext.ClearHitDealtDamage();
    }

    private static void ClearVanillaShots(Trap_button button)
    {
        if (button == null || TrapButtonShotsField == null)
            return;

        TrapButtonShotsField.SetValue(button, new Trapshot[0]);
    }

    private static Trap_button ResolveTrapButton(GameObject spawnedTrap)
    {
        if (spawnedTrap == null)
            return null;

        Trap_button button = spawnedTrap.GetComponent<Trap_button>();
        if (button != null)
            return button;

        return spawnedTrap.GetComponentInChildren<Trap_button>(true);
    }
}

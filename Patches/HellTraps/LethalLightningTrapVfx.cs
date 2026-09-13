using System.Reflection;
using Com.LuisPedroFonseca.ProCamera2D;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Caches and spawns TargetIcon_4 warning + Lightningstrike bolt VFX.</summary>
internal static class LethalLightningTrapVfx
{
    private static readonly FieldInfo CocoonWarningIconField =
        AccessTools.Field(typeof(Cocoontrap), "warningIcon");

    private static readonly FieldInfo MagictrapWarningIconField =
        AccessTools.Field(typeof(Magictrap), "WarnigIcon");

    private static readonly FieldInfo LightningStrikePrefabField =
        AccessTools.Field(typeof(Lightningstrikecreate), "lightningStrike");

    private static GameObject _warningIconPrefab;
    private static GameObject _lightningBoltPrefab;
    private static bool _warningResolveAttempted;
    private static bool _boltResolveAttempted;

    internal static void SpawnWarningAbovePlayer(playercon player)
    {
        if (player == null)
            return;

        Vector3 pos = player.transform.position;
        pos.y += 1f;
        SpawnWarningAtWorld(pos);
    }

    /// <summary>Warning icon exactly at world position (used for lightningTrap_button spawn coords).</summary>
    internal static void SpawnWarningAtWorld(Vector3 worldPos)
    {
        if (Plugin.lethalLightningTrapUseWarningIcon != null &&
            !Plugin.lethalLightningTrapUseWarningIcon.Value)
            return;

        GameObject prefab = ResolveWarningIconPrefab();
        if (prefab == null)
        {
            Plugin.Log?.LogWarning("[LethalLightningTrap] Warning icon prefab not cached.");
            return;
        }

        Object.Instantiate(prefab, worldPos, Quaternion.identity);
    }

    internal static void SpawnLightningStrikeAtPlayer(playercon player, bool playShakeAndSe)
    {
        if (player == null)
            return;

        SpawnLightningStrikeAtWorld(player.transform.position, playShakeAndSe);
    }

    internal static void SpawnLightningStrikeAtWorld(Vector3 worldPos, bool playShakeAndSe)
    {
        if (Plugin.lethalLightningTrapUseLightningVfx != null &&
            !Plugin.lethalLightningTrapUseLightningVfx.Value)
            return;

        GameObject prefab = ResolveLightningBoltPrefab();
        if (prefab == null)
        {
            Plugin.Log?.LogWarning("[LethalLightningTrap] Lightningstrike bolt prefab not cached.");
            return;
        }

        Vector3 pos = worldPos;
        pos.y += LethalLightningTrapDeathTuning.StrikeOffsetY;
        Quaternion rot = Quaternion.Euler(90f, 90f, -90f);
        GameObject bolt = Object.Instantiate(prefab, pos, rot);
        DisableBoltDamage(bolt);
        Object.Destroy(bolt, 0.6f);

        if (!playShakeAndSe)
            return;

        try
        {
            GameObject cam = GameObject.FindWithTag("MainCamera");
            if (cam != null)
            {
                ProCamera2DShake shake = cam.GetComponent<ProCamera2DShake>();
                if (shake != null)
                    shake.Shake("Gun");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalLightningTrap] Camera shake failed: " + ex.Message);
        }

        try
        {
            MasterAudio.PlaySound("atk_soko", 1f, null, 0f, null, false, false);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalLightningTrap] Strike SE failed: " + ex.Message);
        }
    }

    private static void DisableBoltDamage(GameObject bolt)
    {
        if (bolt == null)
            return;

        Transform body = bolt.transform.Find("body");
        if (body == null)
            return;

        Collider2D[] cols = body.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }

        magic magicComp = body.GetComponent<magic>();
        if (magicComp != null)
            magicComp.enabled = false;
    }

    private static GameObject ResolveWarningIconPrefab()
    {
        if (_warningIconPrefab != null)
            return _warningIconPrefab;

        if (_warningResolveAttempted)
            return null;

        _warningResolveAttempted = true;

        GameObject fromCocoon = TryPullWarningFromTrap("cocoontrap", CocoonWarningIconField);
        if (fromCocoon != null)
        {
            _warningIconPrefab = fromCocoon;
            return _warningIconPrefab;
        }

        GameObject fromMagic = TryPullWarningFromTrap("magictrap", MagictrapWarningIconField);
        if (fromMagic != null)
        {
            _warningIconPrefab = fromMagic;
            return _warningIconPrefab;
        }

        return null;
    }

    private static GameObject TryPullWarningFromTrap(string templateKey, FieldInfo field)
    {
        if (field == null)
            return null;

        if (!SpawnTemplateCatalog.HasTemplate(templateKey))
            SpawnTemplateCatalog.TryCacheFromResources(templateKey);

        if (!SpawnTemplateCatalog.TryGetTrapTemplate(templateKey, out GameObject template) ||
            template == null)
        {
            return null;
        }

        Component host = template.GetComponentInChildren(field.DeclaringType, true);
        if (host == null)
            return null;

        return field.GetValue(host) as GameObject;
    }

    private static GameObject ResolveLightningBoltPrefab()
    {
        if (_lightningBoltPrefab != null)
            return _lightningBoltPrefab;

        if (_boltResolveAttempted)
            return null;

        _boltResolveAttempted = true;

        if (LightningStrikePrefabField == null)
        {
            Plugin.Log?.LogWarning("[LethalLightningTrap] Lightningstrikecreate.lightningStrike field missing.");
            return null;
        }

        string[] resourcePaths =
        {
            "Magic/mg_Lightningstrike",
            "magic/mg_Lightningstrike",
            "Magic/mg_LightningStrike",
        };

        for (int i = 0; i < resourcePaths.Length; i++)
        {
            GameObject device = Resources.Load<GameObject>(resourcePaths[i]);
            if (device == null)
                continue;

            Lightningstrikecreate creator = device.GetComponent<Lightningstrikecreate>();
            if (creator == null)
                creator = device.GetComponentInChildren<Lightningstrikecreate>(true);

            if (creator == null)
                continue;

            GameObject bolt = LightningStrikePrefabField.GetValue(creator) as GameObject;
            if (bolt == null)
                continue;

            _lightningBoltPrefab = bolt;
            Plugin.Log?.LogInfo(
                "[LethalLightningTrap] Cached Lightningstrike bolt from Resources '"
                + resourcePaths[i]
                + "'.");
            return _lightningBoltPrefab;
        }

        Plugin.Log?.LogWarning(
            "[LethalLightningTrap] Could not Resources.Load Magic/mg_Lightningstrike for bolt prefab.");
        return null;
    }
}

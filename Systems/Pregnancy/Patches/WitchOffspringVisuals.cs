using System;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Shared visual helpers for offspring (faction emblem, billboard behaviour).
/// </summary>
public static class WitchOffspringVisuals
{
    /// <summary>Hide vanilla enemy alert/HP UI (exclamation "!?" and health bar).</summary>
    public static void HideEnemyCombatUi(GameObject obj)
    {
        if (obj == null)
            return;

        try
        {
            Transform canvas = obj.transform.Find("Canvas");
            if (canvas == null)
                return;

            Transform exclamation = canvas.Find("exclamation");
            if (exclamation != null)
            {
                var image = exclamation.GetComponent<Image>();
                if (image != null)
                    image.enabled = false;
                if (exclamation.gameObject.activeSelf)
                    exclamation.gameObject.SetActive(false);
            }

            Transform hp = canvas.Find("Hp");
            if (hp != null && hp.gameObject.activeSelf)
                hp.gameObject.SetActive(false);
        }
        catch { }
    }

    /// <summary>MafiaMuscle uses EnemyDate.scale where X = facing sign; keep Y/Z uniform.</summary>
    public static void ApplyUniformOffspringScale(EnemyDate enemyDate, float uniformScale)
    {
        if (enemyDate == null)
            return;

        int dir = enemyDate.DIR != 0 ? enemyDate.DIR : 1;
        enemyDate.scale = new Vector3(dir * uniformScale, uniformScale, uniformScale);
        enemyDate.transform.localScale = enemyDate.scale;
    }

    /// <summary>Align collider feet with the nearest ground below the spawn point.</summary>
    public static void SnapFeetToGround(GameObject obj)
    {
        if (obj == null)
            return;

        try
        {
            Collider2D selfCol = obj.GetComponent<Collider2D>() ?? obj.GetComponentInChildren<Collider2D>();
            int mask = LayerMask.GetMask("Ground", "Floor", "Platform", "Map");
            if (mask == 0)
                mask = Physics2D.AllLayers;

            Vector2 origin = obj.transform.position + Vector3.up * 2f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 12f, mask);
            float groundY = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hitCol = hits[i].collider;
                if (hitCol == null || hitCol.gameObject == obj)
                    continue;
                if (selfCol != null && hitCol.transform.IsChildOf(obj.transform))
                    continue;

                if (hits[i].point.y > groundY)
                    groundY = hits[i].point.y;
            }

            if (float.IsNegativeInfinity(groundY))
                return;

            float feetY = selfCol != null ? selfCol.bounds.min.y : obj.transform.position.y;
            float delta = groundY - feetY;
            if (Mathf.Abs(delta) < 0.01f)
                return;

            obj.transform.position += new Vector3(0f, delta, 0f);
        }
        catch { }
    }

    /// <summary>Apply scale and deferred companion setup.</summary>
    public static void ConfigureSpawnedOffspring(GameObject childObj, float scale, bool hideoutCompanion)
    {
        if (childObj == null)
            return;

        var setup = childObj.GetComponent<WitchOffspringSpawnSetup>();
        if (setup == null)
            setup = childObj.AddComponent<WitchOffspringSpawnSetup>();
        setup.UniformScale = scale;
        setup.IsHideoutCompanion = hideoutCompanion;

        HideEnemyCombatUi(childObj);
    }

    internal static void AddFactionEmblem(GameObject childObj, int factionSource)
    {
        if (factionSource == FactionIds.Neutral)
            return;

        try
        {
            var emblemObj = new GameObject("FactionEmblem");
            emblemObj.transform.SetParent(childObj.transform);
            emblemObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            emblemObj.transform.localScale = Vector3.one * 0.5f;

            var renderer = emblemObj.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 100;

            if (FactionStyle.TryGetIconStyle(factionSource, out var style) && style.Icon != null)
            {
                renderer.sprite = style.Icon;
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                    Plugin.Log?.LogInfo($"[Pregnancy.Birth] Added faction emblem for faction {factionSource}");
            }
            else
            {
                Plugin.Log?.LogWarning($"[Pregnancy.Birth] Could not load faction emblem for faction {factionSource}");
            }

            emblemObj.AddComponent<EmblemBillboard>();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Birth] Error adding faction emblem: {ex.Message}");
        }
    }
}

/// <summary>Simple billboard component for the faction emblem.</summary>
public class EmblemBillboard : MonoBehaviour
{
    private void Update()
    {
        var mainCam = UnityEngine.Camera.main;
        if (mainCam != null)
        {
            transform.rotation = Quaternion.LookRotation(Vector3.forward, mainCam.transform.up);
        }
    }
}

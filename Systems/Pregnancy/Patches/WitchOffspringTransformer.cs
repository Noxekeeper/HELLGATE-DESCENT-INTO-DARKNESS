using System;
using System.Collections;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Pregnancy.OffspringArchetype;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Replaces a birth slime with a small MafiaMuscle offspring after a configurable delay.
/// The new offspring is registered as Witch faction, carries the source-faction emblem,
/// and is then handed to <see cref="WitchOffspringController"/> for the hideout/display lifecycle.
/// </summary>
public class WitchOffspringTransformer : MonoBehaviour
{
    public int FactionSource { get; private set; }
    public float BirthScale { get; private set; }
    public ChildData ChildData { get; private set; }

    private bool _initialized;
    private float _slimeDisplayScale = 0.5f;

    public void Initialize(ChildData childData, int factionSource, float scale)
    {
        ChildData = childData;
        FactionSource = factionSource;
        BirthScale = scale;
        _slimeDisplayScale = PregnancyConfig.BirthSlimeDisplayScale?.Value ?? 0.5f;
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;

        var enemyDate = GetComponent<EnemyDate>();
        if (enemyDate != null)
            WitchOffspringVisuals.ApplyUniformOffspringScale(enemyDate, _slimeDisplayScale);
    }

    private void Start()
    {
        if (!_initialized)
        {
            Destroy(this);
            return;
        }

        StartCoroutine(TransformSequence());
    }

    private IEnumerator TransformSequence()
    {
        // Freeze the slime so it stays in place during the birth display.
        var slime = GetComponent<suraimu>();
        if (slime != null)
        {
            slime.enabled = false;
            var rb = slime.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.simulated = false;
            slime.state = suraimu.enemystate.IDLE;
        }

        float delay = PregnancyConfig.BirthTransformDelaySeconds?.Value ?? 3f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (this == null || gameObject == null)
            yield break;

        GameObject prefab = null;
        string archetypeKey = OffspringArchetypeCatalog.FallbackArchetype;
        try
        {
            if (!OffspringPrefabResolver.TryResolvePrefab(ChildData, out prefab, out archetypeKey) || prefab == null)
            {
                Plugin.Log?.LogError("[Pregnancy.Birth] Transformer: failed to resolve offspring prefab");
                yield break;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Birth] Transformer: prefab lookup failed: {ex.Message}");
            yield break;
        }

        Vector3 pos = transform.position;
        GameObject childObj = (GameObject)Instantiate(prefab, pos, Quaternion.identity);
        childObj.name = OffspringPrefabResolver.BuildObjectName(archetypeKey);

        var controller = childObj.AddComponent<WitchOffspringController>();
        controller.Initialize(FactionSource, BirthScale);
        controller.ChildGuid = ChildData.Guid;
        controller.DisplayDelayBeforeHideout = PregnancyConfig.OffspringDisplaySeconds?.Value ?? 2f;

        WitchOffspringVisuals.ConfigureSpawnedOffspring(childObj, BirthScale, hideoutCompanion: false);

        var enemyDate = childObj.GetComponent<EnemyDate>();
        if (enemyDate != null)
        {
            try { EnemyFactionRuntime.RegisterEnemy(enemyDate); } catch { }
            try { EnemyFactionRuntime.SetFaction(childObj, FactionIds.Witch); } catch { }
        }

        WitchOffspringVisuals.AddFactionEmblem(childObj, FactionSource);
        controller.StartHideoutMove(ChildData);

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Birth] Slime transformed into offspring: archetype={archetypeKey}, child={ChildData.Guid}, scale={BirthScale:F2}");

        // Destroy the slime without triggering death rewards (state was set to IDLE).
        Destroy(gameObject);
    }
}

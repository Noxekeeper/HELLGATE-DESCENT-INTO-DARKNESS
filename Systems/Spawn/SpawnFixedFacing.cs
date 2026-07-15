using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Keeps a spawn-time horizontal mirror. Trapdata / EnemyDate use FixedDir; others use spine/sprite flip.
/// </summary>
[DefaultExecutionOrder(1000)]
internal sealed class SpawnFixedFacing : MonoBehaviour
{
    public int FixedDir = -1;

    private Trapdata trapdata;
    private EnemyDate enemyDate;

    private void Awake()
    {
        trapdata = GetComponentInChildren<Trapdata>(true);
        if (trapdata == null)
            SpawnFlipUtility.TryGetEnemyDate(gameObject, out enemyDate);
    }

    private void OnEnable()
    {
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (trapdata != null)
            SpawnFlipUtility.ApplyTrapdataFacing(trapdata.gameObject, trapdata, FixedDir);
        else if (enemyDate != null)
            SpawnFlipUtility.ApplyEnemyDateFacing(enemyDate.gameObject, enemyDate, FixedDir);
        else
            SpawnFlipUtility.ApplySpineAndSpriteFlipLeft(gameObject);
    }
}

using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Keeps a spawn-time horizontal mirror. Trapdata / EnemyDate use FixedDir; others use spine/sprite flip.
/// </summary>
[DefaultExecutionOrder(20000)]
internal sealed class SpawnFixedFacing : MonoBehaviour
{
    public int FixedDir = -1;

    private Trapdata trapdata;
    private EnemyDate enemyDate;
    private bool armed;
    private bool hasRestore;
    private Vector3 restoreRootScale;
    private bool hasTrapRestore;
    private Vector3 restoreTrapScale;
    private int restoreTrapDir;
    private bool hasEnemyRestore;
    private Vector3 restoreEnemyScale;
    private int restoreEnemyDir;
    private Transform[] restoreSpineTransforms;
    private Vector3[] restoreSpineScales;
    private bool[] restoreSpineFlipX;
    private SpriteRenderer[] restoreSprites;
    private bool[] restoreSpriteFlipX;

    private void Awake()
    {
        BindControllers();
    }

    private void BindControllers()
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

    internal void CaptureRestoreIfNeeded()
    {
        if (hasRestore)
            return;

        hasRestore = true;
        BindControllers();
        restoreRootScale = transform.localScale;

        if (trapdata != null)
        {
            hasTrapRestore = true;
            restoreTrapScale = trapdata.transform.localScale;
            restoreTrapDir = trapdata.DIR;
        }
        else if (enemyDate != null)
        {
            hasEnemyRestore = true;
            Vector3 scale = enemyDate.scale;
            if (Mathf.Abs(scale.x) < 0.001f && Mathf.Abs(scale.y) < 0.001f && Mathf.Abs(scale.z) < 0.001f)
                scale = enemyDate.transform.localScale;
            restoreEnemyScale = scale;
            restoreEnemyDir = enemyDate.DIR;
        }

        SkeletonAnimation[] spines = GetComponentsInChildren<SkeletonAnimation>(true);
        if (spines != null && spines.Length > 0)
        {
            restoreSpineTransforms = new Transform[spines.Length];
            restoreSpineScales = new Vector3[spines.Length];
            restoreSpineFlipX = new bool[spines.Length];
            for (int i = 0; i < spines.Length; i++)
            {
                if (spines[i] == null)
                    continue;
                restoreSpineTransforms[i] = spines[i].transform;
                restoreSpineScales[i] = spines[i].transform.localScale;
                restoreSpineFlipX[i] = spines[i].skeleton != null && spines[i].skeleton.FlipX;
            }
        }

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites != null && sprites.Length > 0)
        {
            restoreSprites = sprites;
            restoreSpriteFlipX = new bool[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    restoreSpriteFlipX[i] = sprites[i].flipX;
            }
        }
    }

    internal void RestoreOriginalFacing()
    {
        if (!hasRestore)
            return;

        armed = false;
        transform.localScale = restoreRootScale;

        if (hasTrapRestore && trapdata != null)
        {
            trapdata.transform.localScale = restoreTrapScale;
            trapdata.scale = restoreTrapScale;
            trapdata.DIR = restoreTrapDir;
        }

        if (hasEnemyRestore && enemyDate != null)
        {
            enemyDate.scale = restoreEnemyScale;
            enemyDate.DIR = restoreEnemyDir;
            enemyDate.transform.localScale = restoreEnemyScale;
        }

        if (restoreSpineTransforms != null && restoreSpineScales != null)
        {
            for (int i = 0; i < restoreSpineTransforms.Length; i++)
            {
                if (restoreSpineTransforms[i] != null)
                    restoreSpineTransforms[i].localScale = restoreSpineScales[i];

                if (restoreSpineFlipX != null && i < restoreSpineFlipX.Length && restoreSpineTransforms[i] != null)
                {
                    SkeletonAnimation spine = restoreSpineTransforms[i].GetComponent<SkeletonAnimation>();
                    if (spine != null && spine.skeleton != null)
                        spine.skeleton.FlipX = restoreSpineFlipX[i];
                }
            }
        }

        if (restoreSprites != null && restoreSpriteFlipX != null)
        {
            for (int i = 0; i < restoreSprites.Length; i++)
            {
                if (restoreSprites[i] != null)
                    restoreSprites[i].flipX = restoreSpriteFlipX[i];
            }
        }
    }

    internal void Arm(int fixedDir)
    {
        FixedDir = fixedDir >= 0 ? 1 : -1;
        armed = true;
        EnsureEnableRelay(gameObject);
        if (restoreSpineTransforms != null)
        {
            for (int i = 0; i < restoreSpineTransforms.Length; i++)
            {
                if (restoreSpineTransforms[i] != null)
                    EnsureEnableRelay(restoreSpineTransforms[i].gameObject);
            }
        }

        Apply();
    }

    internal void Reapply()
    {
        if (armed)
            Apply();
    }

    private static void EnsureEnableRelay(GameObject go)
    {
        if (go == null)
            return;
        if (go.GetComponent<SpawnFixedFacingEnableRelay>() == null)
            go.AddComponent<SpawnFixedFacingEnableRelay>();
    }

    private void Apply()
    {
        if (!armed)
            return;

        if (trapdata != null)
        {
            SpawnFlipUtility.ApplyTrapdataFacing(trapdata.gameObject, trapdata, FixedDir);
            return;
        }

        if (enemyDate != null)
            SpawnFlipUtility.ApplyEnemyDateFacing(enemyDate.gameObject, enemyDate, FixedDir);

        if (!ApplyCapturedSpineFacing())
            SpawnFlipUtility.ApplySpineAndSpriteFacing(gameObject, FixedDir);
    }

    private bool ApplyCapturedSpineFacing()
    {
        if (restoreSpineTransforms == null || restoreSpineScales == null)
            return false;

        bool any = false;
        for (int i = 0; i < restoreSpineTransforms.Length; i++)
        {
            Transform target = restoreSpineTransforms[i];
            if (target == null)
                continue;
            any = true;

            Vector3 origScale = restoreSpineScales[i];
            bool origFlip = restoreSpineFlipX != null &&
                            i < restoreSpineFlipX.Length &&
                            restoreSpineFlipX[i];
            bool needMirror = FixedDir < 0;
            float mag = Mathf.Abs(origScale.x) > 0.001f ? Mathf.Abs(origScale.x) : 1f;
            float sign = origScale.x < 0f ? -1f : 1f;
            if (needMirror)
                sign = -sign;
            target.localScale = new Vector3(sign * mag, origScale.y, origScale.z);

            SkeletonAnimation spine = target.GetComponent<SkeletonAnimation>();
            if (spine != null && spine.skeleton != null)
                spine.skeleton.FlipX = origFlip;
        }

        return any;
    }
}

/// <summary>
/// SpawnSlave toggles the visual child with SetActive. Spine Initialize then
/// rebuilds Skeleton (FlipX=false). Re-apply the lock on enable.
/// </summary>
internal sealed class SpawnFixedFacingEnableRelay : MonoBehaviour
{
    private void OnEnable()
    {
        SpawnFixedFacing facing = GetComponent<SpawnFixedFacing>()
            ?? GetComponentInParent<SpawnFixedFacing>();
        if (facing != null)
            facing.Reapply();
    }
}

/// <summary>Keeps pack Z rotation after vanilla Trapdata Start/DIR resets the transform.</summary>
[DefaultExecutionOrder(1001)]
internal sealed class SpawnFixedRotation : MonoBehaviour
{
    public float FixedZ;

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
        SpawnRotationUtility.ApplyAuthoringRotation(gameObject, FixedZ);
    }
}

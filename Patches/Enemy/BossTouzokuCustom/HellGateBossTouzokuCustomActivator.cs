using System.Collections;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>Runs field-mob bootstrap after BossTouzoku.Start on the same frame stack.</summary>
internal sealed class HellGateBossTouzokuCustomActivator : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(ActivateAfterBossStart());
    }

    private IEnumerator ActivateAfterBossStart()
    {
        yield return null;

        BossTouzoku boss = GetComponent<BossTouzoku>();
        if (boss != null)
        {
            BossTouzokuCustomRuntime.ApplyFieldMobCombat(boss);
            BossTouzokuCustomRuntime.BeginEroScriptWarmUp(boss);
        }

        Destroy(this);
    }
}

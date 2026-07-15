using System.Collections;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Runs boss combat bootstrap after vanilla Start() has initialized fields.</summary>
internal sealed class HellGateBossSpawnBootstrap : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(BootstrapAfterStart());
    }

    private IEnumerator BootstrapAfterStart()
    {
        yield return null;

        BossTouzoku boss = GetComponent<BossTouzoku>();
        if (boss != null)
            HellGateBossSpawnRuntime.TryActivateBossTouzoku(boss);

        Destroy(this);
    }
}

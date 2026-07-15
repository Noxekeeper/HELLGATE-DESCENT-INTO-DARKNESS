using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

internal static class ShelterAttackTracker
{
    private static readonly List<ShelterAttackEnemyMarker> _markers = new List<ShelterAttackEnemyMarker>();

    internal static void Register(ShelterAttackEnemyMarker marker)
    {
        if (marker == null || _markers.Contains(marker))
            return;
        _markers.Add(marker);
    }

    internal static void PruneDead()
    {
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            if (!IsAlive(_markers[i]))
                _markers.RemoveAt(i);
        }
    }

    internal static int AliveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _markers.Count; i++)
            {
                if (IsAlive(_markers[i]))
                    count++;
            }

            return count;
        }
    }

    internal static void DestroyAllRemaining()
    {
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            ShelterAttackEnemyMarker marker = _markers[i];
            if (marker != null && marker.gameObject != null)
                Object.Destroy(marker.gameObject);
        }

        _markers.Clear();
    }

    private static bool IsAlive(ShelterAttackEnemyMarker marker)
    {
        if (marker == null)
            return false;

        GameObject go = marker.gameObject;
        if (go == null || !go.activeInHierarchy)
            return false;

        EnemyDate enemy = go.GetComponent<EnemyDate>();
        if (enemy == null)
            return true;

        return enemy.Hp > 0f;
    }
}

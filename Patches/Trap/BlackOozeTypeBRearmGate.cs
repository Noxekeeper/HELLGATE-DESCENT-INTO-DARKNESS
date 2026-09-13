using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Trap;

/// <summary>
/// Realtime re-arm for <see cref="BlackOozeTrapTypeB"/> after escape.
/// Vanilla uses <c>Invoke("flagcount", 0.3)</c> (scaled time); after HellGate abort we need a
/// longer window that still clears <c>trapflag</c> reliably so the trap stays reusable.
/// </summary>
internal sealed class BlackOozeTypeBRearmGate : MonoBehaviour
{
    private float clearAtUnscaled = -1f;

    internal void Arm(float durationSeconds)
    {
        if (durationSeconds < 0.05f)
            durationSeconds = 0.05f;

        clearAtUnscaled = Time.unscaledTime + durationSeconds;
        enabled = true;
    }

    private void Update()
    {
        if (clearAtUnscaled < 0f || Time.unscaledTime < clearAtUnscaled)
            return;

        clearAtUnscaled = -1f;
        enabled = false;

        BlackOozeTrapTypeB trap = GetComponent<BlackOozeTrapTypeB>();
        if (trap == null)
            return;

        try
        {
            Traverse.Create(trap).Field("trapflag").SetValue(false);
        }
        catch
        {
        }
    }
}

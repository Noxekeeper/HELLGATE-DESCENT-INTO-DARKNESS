using UnityEngine;

namespace NoREroMod.Patches.Enemy.HeckGateEnemy;

/// <summary>
/// Visual-only profile for biscord.
/// Keeps enemy scale at a fixed multiplier from its original prefab size.
/// </summary>
internal sealed class BiscodVisualProfile : MonoBehaviour
{
    private const float ScaleMultiplier = 2f;

    private Vector3 _baseScale = Vector3.one;
    private bool _baseCaptured;

    private void Awake()
    {
        CaptureBaseScaleIfNeeded();
        ApplyScale();
    }

    private void OnEnable()
    {
        CaptureBaseScaleIfNeeded();
        ApplyScale();
    }

    private void Start()
    {
        CaptureBaseScaleIfNeeded();
        ApplyScale();
    }

    private void CaptureBaseScaleIfNeeded()
    {
        if (_baseCaptured) return;
        _baseScale = transform.localScale;
        _baseCaptured = true;
    }

    private void ApplyScale()
    {
        transform.localScale = _baseScale * ScaleMultiplier;
    }

}

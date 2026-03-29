using UnityEngine;
using UnityEngine.UI;
using NoREroMod.Systems.GrabSystem;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Runtime component that keeps the GrabChance label in sync with grab chance.
/// Lives on the Rage overlay canvas, next to Rage label.
/// </summary>
internal class GrabChanceRageUILabel : MonoBehaviour
{
    private Text? _label;
    private RectTransform? _rect;
    private static readonly Color GrabTextColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    internal void Initialise(Text label)
    {
        _label = label;
        _rect = GetComponent<RectTransform>();
        Refresh();
    }

    private void OnEnable()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();
        ApplyAnchors();
        Refresh();
    }

    private void LateUpdate()
    {
        ApplyAnchors();
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            if (_label == null)
            {
                _label = GetComponent<Text>();
                if (_label == null)
                    return;
            }

            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            bool shouldShow = RageUISystem.ShouldShowLabelForUI();

            float chance = GrabChanceCalculator.GetApproxMeleeGrabChanceForUI();
            int percent = Mathf.RoundToInt(chance * 100f);
            _label.text = $"Grab chance: {percent}%";
            _label.color = GrabTextColor;
            gameObject.SetActive(shouldShow);
        }
        catch
        {
        }
    }

    internal void ForceRefresh()
    {
        Refresh();
    }

    private void ApplyAnchors()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        if (_rect == null)
            return;

        _rect.anchorMin = new Vector2(0f, 0f);
        _rect.anchorMax = new Vector2(0f, 0f);
        _rect.pivot = new Vector2(0f, 0f);

        if (_rect.anchoredPosition != new Vector2(360f, 883f))
        {
            _rect.anchoredPosition = new Vector2(360f, 883f);
        }
    }
}


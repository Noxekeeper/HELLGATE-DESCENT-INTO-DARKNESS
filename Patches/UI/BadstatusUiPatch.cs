using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod;

internal static class BadstatusUiPatch
{
    private static readonly FieldInfo NumField = AccessTools.Field(typeof(Badstatus), "num");
    private static readonly FieldInfo BarField = AccessTools.Field(typeof(Badstatus), "Bar");
    private static readonly FieldInfo TextField = AccessTools.Field(typeof(Badstatus), "text");

    private static readonly Dictionary<int, float> BaseFontSizes = new();

    private const float BaseOffsetX = 0f;
    private const float BaseOffsetY = 0f;
    private const float RowSpacing = 36f;
    private const float BarWidth = 150f;
    private const float LabelOffset = 162f;

    [HarmonyPatch(typeof(Badstatus), "Update")]
    [HarmonyPrefix]
    private static bool PreventPleasureSpam(int ___num, Image ___Bar, PlayerStatus ___pl)
    {
        try
        {
            if (___num == 0 && ___Bar != null && ___pl != null)
            {
                if (___Bar.fillAmount >= 1f && ___Bar.color == Color.magenta)
                {
                    ___Bar.fillAmount = ___pl._BadstatusVal[0] / 100f;
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BADSTATUS] Update prefix error: {ex.Message}");
        }

        return true;
    }

    [HarmonyPatch(typeof(Badstatus), "badstatusSet")]
    [HarmonyPostfix]
    private static void RelocateStatusBars(Badstatus __instance)
    {
        try
        {
            int index = GetStatusIndex(__instance);
            RectTransform container = __instance.GetComponent<RectTransform>();
            if (container == null)
            {
                return;
            }

            container.anchorMin = new Vector2(0f, 0.5f);
            container.anchorMax = new Vector2(0f, 0.5f);
            container.pivot = new Vector2(0f, 0.5f);

            float rowHeight = Mathf.Max(container.rect.height, RowSpacing);
            float y = BaseOffsetY + (rowHeight + 4f) * Mathf.Clamp(index, 0, 3);
            container.anchoredPosition = new Vector2(BaseOffsetX, y);

            if (BarField?.GetValue(__instance) is Image bar && bar != null)
            {
                RectTransform barRect = bar.GetComponent<RectTransform>();
                if (barRect != null)
                {
                    barRect.anchorMin = new Vector2(0f, 0.5f);
                    barRect.anchorMax = new Vector2(1f, 0.5f); // тянем by шириnot родителя
                    barRect.pivot = new Vector2(0.5f, 0.5f);
                    // Паддинги внутрь рамки
                    const float paddingX = 5f;
                    var offsetMin = barRect.offsetMin;
                    var offsetMax = barRect.offsetMax;
                    offsetMin.x = paddingX;
                    offsetMax.x = -paddingX;
                    barRect.offsetMin = offsetMin;
                    barRect.offsetMax = offsetMax;
                    float height = barRect.rect.height > 0 ? barRect.rect.height : 20f;
                    barRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }
            }

            AdjustText(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BADSTATUS UI] Relocate error: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Badstatus), "textset")]
    [HarmonyPostfix]
    private static void AfterTextSet(Badstatus __instance)
    {
        AdjustText(__instance);
    }

    private static void AdjustText(Badstatus instance)
    {
        try
        {
            if (TextField?.GetValue(instance) is TextMeshProUGUI tmp && tmp != null)
            {
                int key = tmp.GetInstanceID();
                if (!BaseFontSizes.TryGetValue(key, out float baseSize))
                {
                    baseSize = tmp.fontSize > 0 ? tmp.fontSize : 17f;
                    BaseFontSizes[key] = baseSize;
                }

                // Уменьшаем шрифт on 1 for Climax, беременность и pleasure
                string text = tmp.text?.ToLowerInvariant() ?? "";
                if (text.Contains("climax") || text.Contains("беременность") || text.Contains("pregnancy") || text.Contains("impregnated") || text.Contains("pleasure"))
                {
                    tmp.fontSize = baseSize - 1f;
                }
                else
                {
                    tmp.fontSize = baseSize;
                }
                
                tmp.fontStyle |= FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Left;

                RectTransform textRect = tmp.GetComponent<RectTransform>();
                if (textRect != null)
                {
                    textRect.anchorMin = new Vector2(0f, 0.5f);
                    textRect.anchorMax = new Vector2(0f, 0.5f);
                    textRect.pivot = new Vector2(0f, 0.5f);
                    // Текст справа from шкал (already настроеbut через LabelOffset)
                    textRect.anchoredPosition = new Vector2(LabelOffset, textRect.anchoredPosition.y);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BADSTATUS UI] Text adjust error: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
    [HarmonyPostfix]
    private static void AdjustCanvasRoot(CanvasBadstatusinfo __instance)
    {
        try
        {
            if (__instance == null)
            {
                return;
            }

            var rect = __instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                // Изменено: используем левый центр (middle of слева между верхним и нижним углом)
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                
                // Прямо у левого края, by центру вертикально
                // Небольшой отступ from края (e.g., 10px) to avoid было вплотную
                rect.anchoredPosition = new Vector2(10f, 0f);
            }
            else
            {
                Vector3 oldPosition = __instance.transform.position;
                __instance.transform.position = new Vector3(10f, Screen.height * 0.5f, oldPosition.z);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BADSTATUS UI] Canvas adjust error: {ex.Message}");
        }
    }

    private static int GetStatusIndex(Badstatus instance)
    {
        if (NumField == null)
        {
            return 0;
        }

        object value = NumField.GetValue(instance);
        return value is int i ? i : 0;
    }
}


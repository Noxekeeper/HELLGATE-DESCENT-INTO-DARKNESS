using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace NoREroMod;

public static class StruggleVisualIndicators {
    
    public static GameObject struggleIndicatorCanvas;
    public static Image difficultyBar;
    public static TextMeshProUGUI difficultyText;
    public static Image struggleProgressBar;
    public static TextMeshProUGUI struggleProgressText;
    public static Image criticalChanceIndicator;
    public static TextMeshProUGUI criticalChanceText;
    
    private static bool isInitialized = false;
    private static float lastStruggleDifficulty = 0f;
    private static float lastStruggleProgress = 0f;
    private static float lastCriticalChance = 0f;
    
    public static void Initialize() {
        if (isInitialized) return;
        
        // Создаем холст/корневой объект for индикаторов
        GameObject canvasGO = new GameObject("StruggleIndicatorsCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Создаем панель for индикаторов
        GameObject panelGO = new GameObject("StrugglePanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 130f);
        panelRect.sizeDelta = new Vector2(400, 200);
        
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);
        
        // Индикатор сложности борьбы
        CreateDifficultyIndicator(panelGO);
        
        // Индикатор прогресса борьбы
        CreateStruggleProgressIndicator(panelGO);
        
        // Индикатор шанса критического удара
        CreateCriticalChanceIndicator(panelGO);
        
        struggleIndicatorCanvas = canvasGO;
        isInitialized = true;

        ApplyLayout();
        
        // Hidden by default
        canvasGO.SetActive(false);
    }
    
    private static void CreateDifficultyIndicator(GameObject parent) {
        // Фон for полосы сложности
        GameObject difficultyBG = new GameObject("DifficultyBackground");
        difficultyBG.transform.SetParent(parent.transform, false);
        
        RectTransform bgRect = difficultyBG.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.05f, 0.7f);
        bgRect.anchorMax = new Vector2(0.95f, 0.85f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bgImage = difficultyBG.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Полоса сложности
        GameObject difficultyBarGO = new GameObject("DifficultyBar");
        difficultyBarGO.transform.SetParent(difficultyBG.transform, false);
        
        RectTransform barRect = difficultyBarGO.AddComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        
        difficultyBar = difficultyBarGO.AddComponent<Image>();
        difficultyBar.color = Color.green;
        
        // Текст сложности
        GameObject difficultyTextGO = new GameObject("DifficultyText");
        difficultyTextGO.transform.SetParent(difficultyBG.transform, false);
        
        RectTransform textRect = difficultyTextGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        difficultyText = difficultyTextGO.AddComponent<TextMeshProUGUI>();
        difficultyText.text = "Difficulty: Easy";
        difficultyText.fontSize = 16;
        difficultyText.color = Color.white;
        difficultyText.alignment = TextAlignmentOptions.Center;
    }
    
    private static void CreateStruggleProgressIndicator(GameObject parent) {
        // Фон for полосы прогресса
        GameObject progressBG = new GameObject("ProgressBackground");
        progressBG.transform.SetParent(parent.transform, false);
        
        RectTransform bgRect = progressBG.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.05f, 0.5f);
        bgRect.anchorMax = new Vector2(0.95f, 0.65f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bgImage = progressBG.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Полоса прогресса
        GameObject progressBarGO = new GameObject("ProgressBar");
        progressBarGO.transform.SetParent(progressBG.transform, false);
        
        RectTransform barRect = progressBarGO.AddComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        
        struggleProgressBar = progressBarGO.AddComponent<Image>();
        struggleProgressBar.color = Color.blue;
        
        // Текст прогресса
        GameObject progressTextGO = new GameObject("ProgressText");
        progressTextGO.transform.SetParent(progressBG.transform, false);
        
        RectTransform textRect = progressTextGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        struggleProgressText = progressTextGO.AddComponent<TextMeshProUGUI>();
        struggleProgressText.text = "SP: 0%";
        struggleProgressText.fontSize = 16;
        struggleProgressText.color = Color.white;
        struggleProgressText.alignment = TextAlignmentOptions.Center;
    }
    
    private static void CreateCriticalChanceIndicator(GameObject parent) {
        // Фон for индикатора критического шанса
        GameObject criticalBG = new GameObject("CriticalBackground");
        criticalBG.transform.SetParent(parent.transform, false);
        
        RectTransform bgRect = criticalBG.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.05f, 0.3f);
        bgRect.anchorMax = new Vector2(0.95f, 0.45f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bgImage = criticalBG.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Полоса критического шанса
        GameObject criticalBarGO = new GameObject("CriticalBar");
        criticalBarGO.transform.SetParent(criticalBG.transform, false);
        
        RectTransform barRect = criticalBarGO.AddComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        
        criticalChanceIndicator = criticalBarGO.AddComponent<Image>();
        criticalChanceIndicator.color = Color.yellow;
        
        // Текст критического шанса
        GameObject criticalTextGO = new GameObject("CriticalText");
        criticalTextGO.transform.SetParent(criticalBG.transform, false);
        
        RectTransform textRect = criticalTextGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        criticalChanceText = criticalTextGO.AddComponent<TextMeshProUGUI>();
        criticalChanceText.text = "Critical: 0%";
        criticalChanceText.fontSize = 16;
        criticalChanceText.color = Color.white;
        criticalChanceText.alignment = TextAlignmentOptions.Center;
    }
    
    public static void ShowIndicators() {
        if (!isInitialized) Initialize();
        if (struggleIndicatorCanvas != null) {
            ApplyLayout();
            struggleIndicatorCanvas.SetActive(true);
        }
    }
    
    public static void HideIndicators() {
        if (struggleIndicatorCanvas != null) {
            struggleIndicatorCanvas.SetActive(false);
        }
    }
    
    public static void UpdateIndicators(float struggleDifficulty, float struggleProgress, float criticalChance, bool isStruggling) {
        if (!isInitialized) Initialize();
        if (struggleIndicatorCanvas == null || !struggleIndicatorCanvas.activeInHierarchy) return;

        ApplyLayout();
        
        // Update индикатор сложности
        UpdateDifficultyIndicator(struggleDifficulty);
        
        // Update индикатор прогресса
        UpdateStruggleProgressIndicator(struggleProgress);
        
        // Update индикатор критического шанса
        UpdateCriticalChanceIndicator(criticalChance);
        
        // Показываем/скрываем индикаторы depending on состояния борьбы
        if (isStruggling) {
            ShowIndicators();
        } else {
            HideIndicators();
        }
    }
    
    private static RectTransform cachedBadstatusRoot;

    private static void ApplyLayout()
    {
        if (struggleIndicatorCanvas == null)
        {
            return;
        }

        RectTransform rootRect = struggleIndicatorCanvas.GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = struggleIndicatorCanvas.AddComponent<RectTransform>();
        }

        RectTransform targetRoot = ResolveBadstatusRoot();
        if (targetRoot != null && rootRect.parent != targetRoot)
        {
            rootRect.SetParent(targetRoot, worldPositionStays: false);
        }

        if (targetRoot != null)
        {
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localScale = Vector3.one;
        }

        var panel = struggleIndicatorCanvas.transform.Find("StrugglePanel") as RectTransform;
        if (panel != null)
        {
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 100f);
            panel.localScale = Vector3.one;

            ApplyQteContainerLayout(panel);
        }
    }

    private static void ApplyQteContainerLayout(RectTransform strugglePanel)
    {
        Transform qteRoot = strugglePanel.Find("qteButtonsRoot");
        if (qteRoot is RectTransform qteRect)
        {
            qteRect.anchorMin = new Vector2(0.5f, 0f);
            qteRect.anchorMax = new Vector2(0.5f, 0f);
            qteRect.pivot = new Vector2(0.5f, 0f);
            qteRect.anchoredPosition = new Vector2(0f, 170f);
        }
    }

    private static RectTransform ResolveBadstatusRoot()
    {
        if (cachedBadstatusRoot != null && cachedBadstatusRoot)
        {
            return cachedBadstatusRoot;
        }

        var canvas = Object.FindObjectOfType<CanvasBadstatusinfo>();
        if (canvas == null)
        {
            var all = Resources.FindObjectsOfTypeAll(typeof(CanvasBadstatusinfo));
            if (all is { Length: > 0 })
            {
                canvas = all[0] as CanvasBadstatusinfo;
            }
        }

        if (canvas == null)
        {
            return null;
        }

        cachedBadstatusRoot = canvas.GetComponent<RectTransform>();
        return cachedBadstatusRoot;
    }

    private static void UpdateDifficultyIndicator(float difficulty) {
        if (difficultyBar == null || difficultyText == null) return;
        
        // Нормализуем сложность from 0 until 1
        float normalizedDifficulty = Mathf.Clamp01(difficulty);
        
        // Update полосу
        difficultyBar.fillAmount = normalizedDifficulty;
        
        // Update цвет depending on сложности
        if (normalizedDifficulty < 0.3f) {
            difficultyBar.color = Color.green;
            difficultyText.text = "Difficulty: Easy";
        } else if (normalizedDifficulty < 0.6f) {
            difficultyBar.color = Color.yellow;
            difficultyText.text = "Difficulty: Medium";
        } else if (normalizedDifficulty < 0.8f) {
            difficultyBar.color = new Color(1f, 0.5f, 0f); // Orange color
            difficultyText.text = "Difficulty: Hard";
        } else {
            difficultyBar.color = Color.red;
            difficultyText.text = "Difficulty: Extreme";
        }
        
        lastStruggleDifficulty = difficulty;
    }
    
    private static void UpdateStruggleProgressIndicator(float progress) {
        if (struggleProgressBar == null || struggleProgressText == null) return;
        
        // Нормализуем прогресwith from 0 until 1
        float normalizedProgress = Mathf.Clamp01(progress);
        
        // Update полосу
        struggleProgressBar.fillAmount = normalizedProgress;
        
        // Update text
        struggleProgressText.text = $"SP: {Mathf.RoundToInt(normalizedProgress * 100)}%";
        
        // Update цвет depending on прогресса
        if (normalizedProgress < 0.3f) {
            struggleProgressBar.color = Color.red;
        } else if (normalizedProgress < 0.6f) {
            struggleProgressBar.color = Color.yellow;
        } else if (normalizedProgress < 0.9f) {
            struggleProgressBar.color = Color.blue;
        } else {
            struggleProgressBar.color = Color.green;
        }
        
        lastStruggleProgress = progress;
    }
    
    private static void UpdateCriticalChanceIndicator(float criticalChance) {
        if (criticalChanceIndicator == null || criticalChanceText == null) return;
        
        // Нормализуем шанwith from 0 until 1
        float normalizedChance = Mathf.Clamp01(criticalChance);
        
        // Update полосу
        criticalChanceIndicator.fillAmount = normalizedChance;
        
        // Update text
        criticalChanceText.text = $"Critical: {Mathf.RoundToInt(normalizedChance * 100)}%";
        
        // Update цвет depending on шанса
        if (normalizedChance < 0.1f) {
            criticalChanceIndicator.color = Color.gray;
        } else if (normalizedChance < 0.3f) {
            criticalChanceIndicator.color = Color.yellow;
        } else if (normalizedChance < 0.6f) {
            criticalChanceIndicator.color = new Color(1f, 0.5f, 0f); // Orange color
        } else {
            criticalChanceIndicator.color = Color.red;
        }
        
        lastCriticalChance = criticalChance;
    }
    
    public static void Cleanup() {
        if (struggleIndicatorCanvas != null) {
            Object.Destroy(struggleIndicatorCanvas);
            struggleIndicatorCanvas = null;
        }
        isInitialized = false;
    }
}

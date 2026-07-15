using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Systems.UI;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Visual display of dialogues and onomatopoeia in manga style
/// </summary>
internal class DialogueDisplay
{
    private readonly DialoguePool _pool;
    private readonly MonoBehaviour _coroutineRunner;
    
    // Single active Aradia bubble per player/enemy instance.
    private readonly Dictionary<object, GameObject> _activeAradiaContainerByOwner = new();
    
    // Counter for alternating spectator text height (120px / 145px)
    private static int _spectatorHeightCounter = 0;

    internal DialogueDisplay(DialoguePool pool)
    {
        _pool = pool ?? throw new System.ArgumentNullException(nameof(pool));
        
        // Create object for coroutines
        GameObject runnerObj = new GameObject("DialogueCoroutineRunner_XUAIGNORE");
        Object.DontDestroyOnLoad(runnerObj);
        _coroutineRunner = runnerObj.AddComponent<DialogueCoroutineRunner>();
    }
    
    /// <summary>
    /// Centralized Aradia bone name (default for all H-scenes).
    /// </summary>
    internal static string GetDefaultAradiaBone()
    {
        return "bone14";
    }
    
    /// <summary>
    /// Centralized Aradia vertical offset (default: 100px above bone).
    /// </summary>
    internal static float GetDefaultAradiaVerticalOffset()
    {
        return 100f;
    }

    private static Vector3 GetBoneWorldPosition(SkeletonAnimation spine, Spine.Bone bone, BonePosition bonePos)
    {
        return spine.transform.TransformPoint(bone.WorldX, bone.WorldY + bonePos.WorldOffsetY, 0f);
    }

    private static readonly string[] PlayerDisplayBoneFallbacks =
    {
        "hair1", "hair_front", "SIDE_hair", "head", "kubi", "face", "bone14", "bone12", "bone11", "bone10"
    };

    /// <summary>
    /// Gameplay camera (not always UnityEngine.Camera.main during combat / scene transitions).
    /// </summary>
    private static UnityEngine.Camera TryGetDialogueWorldCamera()
    {
        try
        {
            GameObject camGo = NoREroMod.Systems.Cache.UnifiedCameraCacheManager.GetMainCamera();
            if (camGo != null)
            {
                UnityEngine.Camera c = camGo.GetComponent<UnityEngine.Camera>();
                if (c != null && c.enabled)
                {
                    return c;
                }
            }
        }
        catch
        {
        }

        return UnityEngine.Camera.main;
    }

    /// <summary>
    /// Vanilla <see cref="playercon"/> assigns <c>spineanime = GetComponent&lt;SkeletonAnimation&gt;()</c> on the same GameObject (playercon.Start in Assembly-CSharp).
    /// Use that rig before any child SkeletonAnimation (FX / props), or bone world positions will not match the visible heroine.
    /// </summary>
    private static SkeletonAnimation GetPrimarySkeletonForHost(MonoBehaviour mb)
    {
        if (mb == null)
        {
            return null;
        }

        if (mb.GetComponent<playercon>() != null)
        {
            SkeletonAnimation onPlayerRoot = mb.GetComponent<SkeletonAnimation>();
            if (onPlayerRoot != null)
            {
                return onPlayerRoot;
            }
        }

        return mb.GetComponentInChildren<SkeletonAnimation>(true);
    }

    private static SkeletonAnimation GetPrimarySkeletonOnRootObject(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        if (root.GetComponent<playercon>() != null)
        {
            SkeletonAnimation onPlayerRoot = root.GetComponent<SkeletonAnimation>();
            if (onPlayerRoot != null)
            {
                return onPlayerRoot;
            }
        }

        return root.GetComponentInChildren<SkeletonAnimation>(true);
    }

    /// <summary>
    /// Call sites pass either a host <see cref="MonoBehaviour"/> (H-scene enemies) or a <see cref="GameObject"/>
    /// (EventTrap passes the tagged player root). <c>instance as MonoBehaviour</c> is always null for <see cref="GameObject"/>,
    /// which broke bone lookup and left bubbles at canvas center with no per-frame follow.
    /// </summary>
    private static MonoBehaviour ResolveDialogueHost(object instance)
    {
        if (instance == null)
        {
            return null;
        }

        if (instance is MonoBehaviour direct)
        {
            return direct;
        }

        GameObject go = null;
        if (instance is GameObject gobj)
        {
            go = gobj;
        }
        else if (instance is Component comp)
        {
            go = comp.gameObject;
        }

        if (go == null)
        {
            return null;
        }

        playercon pc = go.GetComponent<playercon>();
        if (pc != null)
        {
            return pc;
        }

        SkeletonAnimation sa = go.GetComponent<SkeletonAnimation>();
        if (sa != null)
        {
            return sa;
        }

        return go.GetComponent<MonoBehaviour>();
    }

    /// <summary>
    /// After the hierarchy-first skeleton fails, search other Spine rigs on the player (FX/weapon layers can come first).
    /// </summary>
    private static bool TryResolvePlayerBoneOnOtherSkeletons(MonoBehaviour mb, SkeletonAnimation skipPrimary, string boneName, bool allowBoneFallbacks, out SkeletonAnimation resolvedSpine, out Spine.Bone resolvedBone)
    {
        resolvedSpine = null;
        resolvedBone = null;
        if (mb == null || mb.GetComponent<playercon>() == null || skipPrimary == null)
        {
            return false;
        }

        foreach (SkeletonAnimation sa in mb.GetComponentsInChildren<SkeletonAnimation>(true))
        {
            if (sa == null || sa.skeleton == null || sa == skipPrimary)
            {
                continue;
            }

            Spine.Bone b = null;
            if (boneName.Contains("/"))
            {
                string[] bonePath = boneName.Split('/');
                b = sa.skeleton.FindBone(bonePath[0]);
                for (int i = 1; i < bonePath.Length && b != null; i++)
                {
                    b = FindChildBone(b, bonePath[i]);
                }
            }
            else
            {
                b = sa.skeleton.FindBone(boneName);
            }

            if (allowBoneFallbacks && b == null && (boneName == "bone13" || boneName == "bone12" || boneName == "bone11" || boneName == "bone10"))
            {
                b = sa.skeleton.FindBone("bone12") ??
                    sa.skeleton.FindBone("bone11") ??
                    sa.skeleton.FindBone("bone10");
            }

            if (allowBoneFallbacks && b == null)
            {
                for (int i = 0; i < PlayerDisplayBoneFallbacks.Length && b == null; i++)
                {
                    b = sa.skeleton.FindBone(PlayerDisplayBoneFallbacks[i]);
                }
            }

            if (b != null)
            {
                resolvedSpine = sa;
                resolvedBone = b;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Exact bone name only (no display fallbacks), first hit across all child skeletons.
    /// </summary>
    private static bool TryFindBoneByNameOnAnySkeleton(GameObject root, string boneName, out SkeletonAnimation saOut, out Spine.Bone boneOut)
    {
        saOut = null;
        boneOut = null;
        if (root == null || string.IsNullOrEmpty(boneName))
        {
            return false;
        }

        SkeletonAnimation primary = GetPrimarySkeletonOnRootObject(root);
        if (TryFindNamedBoneOnSkeleton(primary, boneName, out Spine.Bone primaryBone))
        {
            saOut = primary;
            boneOut = primaryBone;
            return true;
        }

        foreach (SkeletonAnimation sa in root.GetComponentsInChildren<SkeletonAnimation>(true))
        {
            if (sa == null || sa.skeleton == null || sa == primary)
            {
                continue;
            }

            if (TryFindNamedBoneOnSkeleton(sa, boneName, out Spine.Bone b))
            {
                saOut = sa;
                boneOut = b;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindNamedBoneOnSkeleton(SkeletonAnimation sa, string boneName, out Spine.Bone boneOut)
    {
        boneOut = null;
        if (sa == null || sa.skeleton == null || string.IsNullOrEmpty(boneName))
        {
            return false;
        }

        Spine.Bone b;
        if (boneName.Contains("/"))
        {
            string[] bonePath = boneName.Split('/');
            b = sa.skeleton.FindBone(bonePath[0]);
            for (int i = 1; i < bonePath.Length && b != null; i++)
            {
                b = FindChildBone(b, bonePath[i]);
            }
        }
        else
        {
            b = sa.skeleton.FindBone(boneName);
        }

        if (b != null)
        {
            boneOut = b;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get appropriate bone for Aradia dialogue based on enemy type and animation.
    /// Similar to onomatopoeia bone selection logic.
    /// </summary>
    internal static string GetAradiaBoneForAnimation(object enemyInstance, string animationName)
    {
        if (enemyInstance == null || string.IsNullOrEmpty(animationName))
            return GetDefaultAradiaBone();

        string enemyName = enemyInstance.GetType().Name;
        string animUpper = animationName.ToUpperInvariant();

        // Debug logging

        // TouzokuNormal (EroTouzoku) - uses bone14 for all animations (player bone)
        if (enemyName == "EroTouzoku" || enemyName.Contains("EroTouzoku") && !enemyName.Contains("AXE"))
        {
            return "bone14";
        }

        // TouzokuAxe (EroTouzokuAXE, TouzokuAxe) - different bones for different animations
        if (enemyName == "EroTouzokuAXE" || enemyName == "TouzokuAxe" || enemyName.Contains("TouzokuAXE"))
        {
            // START animations: bone82 (player bone)
            if (animUpper == "START" || animUpper == "START1" || animUpper == "START2" ||
                animUpper == "START3" || animUpper == "START4" || animUpper == "START5")
            {
                return "bone82";
            }
            // Other animations: bone156 (player bone)
            return "bone156";
        }

        // Goblin - uses SIDE_hair (player bone)
        if (enemyName.ToLowerInvariant().Contains("goblin"))
        {
            return "SIDE_hair";
        }

        // Kakasi - different bones based on animation context
        if (enemyName.Contains("Kakasi"))
        {
            // Ground animations: face (player bone)
            if (animUpper.Contains("GROUND") || animUpper.Contains("LAND"))
            {
                return "face";
            }
            // Cross animations: hair_front (player bone)
            return "hair_front";
        }

        // InquisitionBlack (InquiBlackEro) - uses bone32 (player bone, same as onomatopoeia)
        if (enemyName == "InquiBlackEro" || enemyName.Contains("InquisitionBlack") || enemyName.Contains("BlackInquisitor"))
        {
            return "bone32";
        }

        // Default fallback
        return GetDefaultAradiaBone();
    }
    
    /// <summary>
    /// Centralized Aradia style for responses.
    /// Uses gold text (#FFD700) with black outline (same style as enemies).
    /// </summary>
    internal static DialogueStyle BuildAradiaResponseStyle(float verticalOffset, float horizontalOffset, bool followBone)
    {
        var fontStyle = Plugin.GetFontStyle(Plugin.aradiaResponseFontStyle.Value);

        // Gold text (#FFD700) with black outline (same style as enemies) for Aradia responses
        return new DialogueStyle
        {
            FontSize = Plugin.dialogueFontSize.Value,
            Color = Plugin.ParseColor("255,215,0,255"), // #FFD700 - gold
            OutlineColor = Plugin.ParseColor("0,0,0,255"), // Black outline (same as enemies)
            OutlineDistance = new Vector2(1f, -1f),
            UseOutline = true, // Always use outline
            IsBold = true, // Bold text (same as enemies)
            IsItalic = false, // No italic for responses
            VerticalOffset = verticalOffset,
            HorizontalOffset = horizontalOffset,
            FollowBone = followBone
        };
    }

    /// <summary>
    /// Centralized Aradia style for thoughts (italic).
    /// Defaults: cfg <c>DialogueFonts/AradiaThoughtColor</c> and <c>AradiaThoughtOutlineColor</c>.
    /// Optional <paramref name="textColor"/> / <paramref name="outlineColor"/> override (e.g. lethal trap thoughts).
    /// </summary>
    internal static DialogueStyle BuildAradiaThoughtStyle(
        float verticalOffset,
        float horizontalOffset,
        bool followBone,
        Color? textColor = null,
        Color? outlineColor = null)
    {
        return new DialogueStyle
        {
            FontSize = Plugin.dialogueFontSize.Value,
            Color = textColor ?? Plugin.ParseColor(Plugin.aradiaThoughtColor.Value),
            OutlineColor = outlineColor ?? Plugin.ParseColor(Plugin.aradiaThoughtOutlineColor.Value),
            OutlineDistance = new Vector2(1f, -1f),
            UseOutline = true,
            IsBold = false,
            IsItalic = true,
            VerticalOffset = verticalOffset,
            HorizontalOffset = horizontalOffset,
            FollowBone = followBone
        };
    }

    /// <summary>
    /// Legacy method - redirects to response style for backward compatibility.
    /// Now uses gold color (#FFD700) with black outline (same style as enemies).
    /// </summary>
    internal static DialogueStyle BuildAradiaUnifiedStyle(float verticalOffset, float horizontalOffset, bool followBone)
    {
        return BuildAradiaResponseStyle(verticalOffset, horizontalOffset, followBone);
    }
    
    private void ReplaceActiveAradiaContainer(object ownerKey, GameObject newContainer)
    {
        if (ownerKey == null)
        {
            return;
        }
        
        if (_activeAradiaContainerByOwner.TryGetValue(ownerKey, out GameObject existing) && existing != null)
        {
            Object.Destroy(existing);
        }
        
        _activeAradiaContainerByOwner[ownerKey] = newContainer;
    }

    /// <summary>
    /// Do not add a nested <see cref="Canvas"/> on Aradia containers: with <see cref="CanvasScaler"/> (Scale With Screen Size),
    /// a child canvas breaks the mapping between <see cref="RectTransform.anchoredPosition"/> and screen points from
    /// <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle"/>, so bone-follow appears stuck or offset.
    /// Grab threats use the dialogue root canvas only; match that here and rely on sibling order for draw priority.
    /// </summary>
    private static void LiftAradiaContainerDrawOrder(GameObject container)
    {
        if (container == null)
        {
            return;
        }

        Canvas nested = container.GetComponent<Canvas>();
        if (nested != null)
        {
            Object.Destroy(nested);
        }

        container.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Show onomatopoeia
    /// </summary>
    internal void ShowOnomatopoeia(object enemyInstance, string text, BonePosition bonePos, DialogueStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        GameObject textObj = _pool.GetTextObject();
        if (textObj == null)
        {
            return;
        }

        UnityEngine.UI.Text textComponent = textObj.GetComponent<UnityEngine.UI.Text>();
        if (textComponent == null)
        {
            _pool.ReturnTextObject(textObj);
            return;
        }

        SetupMangaStyle(textComponent, text, style);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        
        // Set sufficient size for long phrases (QTE reactions can be long)
        // Use larger size for QTE reactions
        if (bonePos.UseScreenCenter)
        {
            // QTE reactions - set wide RectTransform for long phrases
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 800f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
        }
        else
        {
            // Onomatopoeia - standard size
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
        }

        PositionRelativeToBone(enemyInstance, rect, bonePos, style.VerticalOffset, style.HorizontalOffset);
        textObj.SetActive(true);
        rect.localScale = Vector3.one;
        // Color already set in SetupMangaStyle from style.Color (pink for onomatopoeia)

        float disappearDuration = 1.0f; // 1 second
        _coroutineRunner.StartCoroutine(AnimateText(textObj, style, disappearDuration));
    }
    
    /// <summary>
    /// Show grab threat (special version for grab threats)
    /// COMMENTED: Old method with upward movement animation (jerky)
    /// Now redirects to new static method ShowStaticThreat
    /// </summary>
    internal void ShowThreatOnomatopoeia(object enemyInstance, string threat, BonePosition bonePos, DialogueStyle style, float duration)
    {
        ShowStaticThreat(enemyInstance, threat, bonePos, style, duration);
    }
    
    /// <summary>
    /// Shows static threat with background, anchored to enemy bone.
    /// </summary>
    internal void ShowStaticThreat(object enemyInstance, string threat, BonePosition bonePos, DialogueStyle style, float duration)
    {
        if (string.IsNullOrEmpty(threat))
        {
            return;
        }

        // Create container for text and background (as in ShowTouzokuHSceneComment)
        GameObject container = new GameObject("StaticThreatContainer_XUAIGNORE");
        container.transform.SetParent(_pool.CanvasRoot.transform, false);
        
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        
        // Create text first, so that get its size
        GameObject textObj = new GameObject("ThreatText_XUAIGNORE");
        textObj.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = threat; // Full text at once (no streaming)
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontSize = (int)style.FontSize; // Use size from style
        textComponent.color = style.Color; // Use color from style
        FontStyle fontStyle = FontStyle.Normal;
        if (style.IsBold)
        {
            fontStyle |= FontStyle.Bold;
        }
        if (style.IsItalic)
        {
            fontStyle |= FontStyle.Italic;
        }
        textComponent.fontStyle = fontStyle; // Apply style from DialogueStyle
        textComponent.raycastTarget = false;

        // Apply outline from style
        if (style.UseOutline)
        {
            var outline = textComponent.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null)
            {
                outline = textComponent.gameObject.AddComponent<UnityEngine.UI.Outline>();
            }
            outline.effectColor = style.OutlineColor;
            outline.effectDistance = style.OutlineDistance;
        }

        // Add line breaks for for long phrases
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        
        // Use font from pool
        var pool = _pool as DialoguePool;
        if (pool != null)
        {
            var cachedFont = pool.GetCachedFont();
            if (cachedFont != null)
            {
                textComponent.font = cachedFont;
            }
            else
            {
                textComponent.font = HellGateFontProvider.GetUiFont();
            }
        }
        
        // Get text size
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        
        // Fix width for wrapping long phrases (for 2 lines)
        float maxWidth = 350f; // width for two lines (as for enemy)
        textRect.sizeDelta = new Vector2(maxWidth, 0f); // Height will be calculated automatically
        
        // Update only this RectTransform (cheaper than Canvas.ForceUpdateCanvases)
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        
        // Get actual height after wrap
        float actualHeight = textComponent.preferredHeight;
        textRect.sizeDelta = new Vector2(maxWidth, actualHeight + 10f); // Add small padding
        
        // Set container size exactly to text size (adaptively)
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textRect.sizeDelta.x);
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textRect.sizeDelta.y);
        
        // Center text in container
        textRect.anchoredPosition = Vector2.zero;
        
        // Create background - fully transparent, to avoid would be visible
        GameObject background = new GameObject("ThreatBackground_XUAIGNORE");
        background.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Image bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0f); // Fully transparent background (alpha = 0)
        
        // Set render order: background should be behind text
        background.transform.SetAsFirstSibling();
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // Get bone position
        Vector2 bonePosition = GetBoneScreenPosition(enemyInstance, bonePos);

        // Use offset from style (for goblins) or alternate height (for spectators)
        float verticalOffset = style.VerticalOffset > 0 ? style.VerticalOffset :
                             (_spectatorHeightCounter % 2 == 0) ? 120f : 145f;
        float horizontalOffset = style.HorizontalOffset;

        if (style.VerticalOffset > 0)
        {
            // For goblins don't increment counter
        }
        else
        {
            _spectatorHeightCounter++; // Increment counter only for spectators
        }

        Vector2 startPosition = bonePosition + new Vector2(horizontalOffset, verticalOffset);
        
        containerRect.anchoredPosition = startPosition;
        
        // IMPORTANT: Set initial scale before activation, so that text is immediately compressed
        containerRect.localScale = new Vector3(0f, 1f, 1f);
        
        container.SetActive(true);
        
        // Start coroutine with unfold
        // For static dialogues (FollowBone = false) don't pass enemyInstance and bonePos
        Color textColorForAnim = style.Color;
        if (style.FollowBone)
        {
            _coroutineRunner.StartCoroutine(AnimateTouzokuHSceneComment(container, threat, duration, enemyInstance, bonePos, verticalOffset, textColorForAnim));
        }
        else
        {
            _coroutineRunner.StartCoroutine(AnimateTouzokuHSceneComment(container, threat, duration, null, null, verticalOffset, textColorForAnim));
        }
    }

    /// <summary>
    /// Manga style setup for text
    /// </summary>
    private void SetupMangaStyle(UnityEngine.UI.Text text, string content, DialogueStyle style)
    {
        text.text = content;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = (int)style.FontSize;
        text.color = style.Color; // Use color from style (for QTE reactions will be red)
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        
        // IMPORTANT: Allow overflow text, so that long phrases not clipped
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;

        if (style.UseOutline)
        {
            var outline = text.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            }
            outline.effectColor = style.OutlineColor;
            outline.effectDistance = style.OutlineDistance;
        }
        
        var pool = _pool as DialoguePool;
        if (pool != null)
        {
            var cachedFont = pool.GetCachedFont();
            if (cachedFont != null)
            {
                text.font = cachedFont;
            }
            else
            {
                text.font = HellGateFontProvider.GetUiFont();
            }
        }
    }

    private void ApplyOutline(GameObject textObj, DialogueStyle style, Color defaultColor, Vector2 defaultDistance)
    {
        UnityEngine.UI.Outline outline = textObj.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null)
        {
            outline = textObj.AddComponent<UnityEngine.UI.Outline>();
        }

        if (style.UseOutline)
        {
            outline.effectColor = style.OutlineColor;
            outline.effectDistance = style.OutlineDistance;
        }
        else
        {
            outline.effectColor = defaultColor;
            outline.effectDistance = defaultDistance;
        }
    }

    /// <summary>
    /// Find child bone by name
    /// </summary>
    private static Spine.Bone FindChildBone(Spine.Bone parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }
        
        Spine.ExposedList<Spine.Bone> children = parent.Children;
        if (children == null)
        {
            return null;
        }
        
        for (int i = 0; i < children.Count; i++)
        {
            Spine.Bone child = children.Items[i];
            if (child != null && child.Data != null && child.Data.Name == childName)
            {
                return child;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Text positioning relative to bone
    /// </summary>
    private void PositionRelativeToBone(object enemyInstance, RectTransform rect, BonePosition bonePos, float verticalOffset = 0f, float horizontalOffset = 0f)
    {
        try
        {
            // If UseScreenCenter = true, show in center screen + offset
            if (bonePos.UseScreenCenter)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(100f, 0f); // Screen center + 100px right
                return;
            }

            UnityEngine.Camera worldCam = TryGetDialogueWorldCamera();

            SkeletonAnimation spine = null;
            Spine.Bone bone = null;
            string boneName = string.IsNullOrEmpty(bonePos.BoneName) ? "bone13" : bonePos.BoneName;
            SkeletonAnimation targetSpine = null; // Add variable for storing correct spine

            // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] PositionRelativeToBone called: boneName='{boneName}', enemyInstance={enemyInstance?.GetType().Name ?? "null"}");

            MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
            // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] mb={mb != null}");
            if (mb != null)
            {
                spine = GetPrimarySkeletonForHost(mb);
                // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] spine={spine != null}, skeleton={spine?.skeleton != null}");

                if (spine != null && spine.skeleton != null)
                {
                    // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] Looking for bone '{boneName}' in skeleton");
                    var testBone = spine.skeleton.FindBone(boneName);
                    // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] Bone '{boneName}' found: {testBone != null}");

                    // Nested bone support via separator "/"
                    // E.g.: "bone37/E_face/E_face" or "bone5/jigo_face/jigo_face"
                    if (boneName.Contains("/"))
                    {
                        string[] bonePath = boneName.Split('/');
                        bone = spine.skeleton.FindBone(bonePath[0]);
                        
                        // Walk the path nested bones
                        for (int i = 1; i < bonePath.Length && bone != null; i++)
                        {
                            bone = FindChildBone(bone, bonePath[i]);
                        }
                    }
                    else
                    {
                        // Regular bone (not nested)
                        bone = spine.skeleton.FindBone(boneName);
                    }
                    
                    // If bone not found, try alternative bones
                    if (bone == null && !bonePos.DisableBoneFallbacks)
                    {
                        // UnityEngine.Debug.Log($"[ONOMATOPOEIA BONE] Bone '{boneName}' not found on {enemyInstance?.GetType().Name}, trying alternatives...");
                        // Try other bones head
                        bone = spine.skeleton.FindBone("bone12") ??
                               spine.skeleton.FindBone("bone11") ??
                               spine.skeleton.FindBone("bone10") ??
                               spine.skeleton.FindBone("bone13"); // Additional attempt bone13
                        if (bone != null)
                        {
                            // UnityEngine.Debug.Log($"[ONOMATOPOEIA BONE] Found alternative bone");
                        }
                        else
                        {
                            // UnityEngine.Debug.Log($"[ONOMATOPOEIA BONE] No bones found, will use transform fallback");
                        }
                    }
                }
            }

            if (bone == null && mb != null && mb.GetComponent<playercon>() != null && spine != null && spine.skeleton != null)
            {
                if (!bonePos.DisableBoneFallbacks)
                {
                    for (int i = 0; i < PlayerDisplayBoneFallbacks.Length && bone == null; i++)
                    {
                        bone = spine.skeleton.FindBone(PlayerDisplayBoneFallbacks[i]);
                    }
                }

                if (bone == null && TryResolvePlayerBoneOnOtherSkeletons(mb, spine, boneName, !bonePos.DisableBoneFallbacks, out SkeletonAnimation altSpine, out Spine.Bone altBone))
                {
                    spine = altSpine;
                    bone = altBone;
                }
            }

            // If bone not found in the enemy skeleton, and this is a GG bone (head, bone32, bone82, bone25, bone33/front_hair, hair_front, face, SIDE_hair, bone44 etc.),
            // try find in GG skeleton
            if (bone == null && !bonePos.DisableBoneFallbacks && (boneName == "head" || boneName == "hair1" || boneName == "kubi" || boneName == "bone32" || boneName == "bone82" || boneName == "bone25" || boneName == "face" || boneName == "hair_front" || boneName.Contains("bone33") || boneName.Contains("front_hair") || boneName == "SIDE_hair" || boneName == "bone44"))
            {
                // Optimization: use cached playercon
                GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObj != null && TryFindBoneByNameOnAnySkeleton(playerObj, boneName, out SkeletonAnimation playerSkel, out Spine.Bone ggBone))
                {
                    bone = ggBone;
                    targetSpine = playerSkel;
                }
            }

            if (bone == null || worldCam == null)
            {
                // Fallback for onomatopoeia: random positioning around screen center
                // Use same parameters as for normal onomatopoeia around bone
                float fallbackDistance = UnityEngine.Random.Range(100f, 150f); // Standard distance
                float fallbackAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector2 fallbackOffset = new Vector2(
                    Mathf.Cos(fallbackAngle) * fallbackDistance,
                    Mathf.Sin(fallbackAngle) * fallbackDistance
                );

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = fallbackOffset + new Vector2(horizontalOffset, verticalOffset);

                // UnityEngine.Debug.Log($"[ONOMATOPOEIA FALLBACK] Using screen center fallback: position={rect.anchoredPosition}");
                return;
            }

            // Use targetSpine if set (GG bone), otherwise enemy spine
            SkeletonAnimation finalSpine = targetSpine != null ? targetSpine : spine;
            Vector3 worldPos = finalSpine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }

            RectTransform canvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }
            
            Vector2 boneLocalPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out boneLocalPoint);

            // Offset for onomatopoeia: in circle + additional offset from style
            float distance = UnityEngine.Random.Range(100f, 150f);
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad; // Random angle around bone
            Vector2 spawnOffset = new Vector2(
                Mathf.Cos(angle) * distance + horizontalOffset,
                Mathf.Sin(angle) * distance + verticalOffset
            );

            Vector2 finalPosition = boneLocalPoint + spawnOffset;

            // Debug logging for onomatopoeia removed

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = finalPosition;
        }
        catch
        {
            rect.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Appear animation with sharp scale and disappear
    /// Appears in place (no flyout) and disappears in 1 second
    /// </summary>
    private IEnumerator AnimateText(GameObject textObj, DialogueStyle style, float disappearDuration)
    {
        RectTransform rect = textObj.GetComponent<RectTransform>();
        UnityEngine.UI.Text text = textObj.GetComponent<UnityEngine.UI.Text>();

        if (rect == null || text == null)
        {
            yield break;
        }

        if (!textObj.activeSelf)
        {
            textObj.SetActive(true);
        }

        rect.localRotation = Quaternion.identity;
        Vector2 startPosition = rect.anchoredPosition; // Position already set at distance 100-150px from bone
        Color startColor = style.Color; // Use color from style (pink for onomatopoeia)
        text.color = startColor;
        
        int startFontSize = 1;
        int endFontSize = text.fontSize;
        text.fontSize = startFontSize;

        float scaleDuration = 0.1f;
        float elapsed = 0f;
        float totalDuration = disappearDuration;
        
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            
            // Scale animation (quick appear)
            float scaleT = Mathf.Clamp01(elapsed / scaleDuration);
            if (scaleT < 1f)
            {
                scaleT = 1f - Mathf.Pow(1f - scaleT, 3f);
                text.fontSize = (int)Mathf.Lerp(startFontSize, endFontSize, scaleT);
            }
            else
            {
                text.fontSize = endFontSize;
            }
            
            // Position stays in place (without movement)
            rect.anchoredPosition = startPosition;
            
            // Smooth disappear at end (last 0.3 seconds)
            float fadeStart = totalDuration - 0.3f;
            float alpha = 1f;
            if (elapsed > fadeStart)
            {
                float fadeT = (elapsed - fadeStart) / 0.3f;
                alpha = 1f - fadeT;
            }
            
            text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            
            yield return null;
        }

        text.color = startColor;
        text.fontSize = endFontSize;
        rect.anchoredPosition = startPosition;
        _pool.ReturnTextObject(textObj);
    }
    
    /// <summary>
    /// Grab threat animation — smooth move up over 5 seconds
    /// </summary>
    private IEnumerator AnimateThreatText(GameObject textObj, object enemyInstance, BonePosition bonePos, DialogueStyle style, float duration)
    {
        RectTransform rect = textObj.GetComponent<RectTransform>();
        UnityEngine.UI.Text text = textObj.GetComponent<UnityEngine.UI.Text>();

        if (rect == null || text == null)
        {
            yield break;
        }

        if (!textObj.activeSelf)
        {
            textObj.SetActive(true);
        }

        rect.localRotation = Quaternion.identity;
        Color startColor = style.Color;
        text.color = startColor;
        
        // Quick appear (0.2 sec)
        int startFontSize = 1;
        int endFontSize = text.fontSize;
        text.fontSize = startFontSize;

        // Upward offset for flyaway animation (accumulates over time)
        // 2x slower than onomatopoeia: 50 px/sec instead of 100
        float verticalOffset = 0f;
        float verticalSpeed = 50f; // pixels per second upward (2x slower)
        
        // Random horizontal offset (computed once)
        float randomHorizontalOffset = UnityEngine.Random.Range(-20f, 20f);
        
        float scaleInDuration = 0.2f; // Quick appear
        float fadeOutStart = duration - 1.0f; // Disappear start 1 sec before end
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Appear phase (fast)
            float scaleT = Mathf.Clamp01(elapsed / scaleInDuration);
            if (scaleT < 1f)
            {
                scaleT = 1f - Mathf.Pow(1f - scaleT, 3f); // Ease out
                text.fontSize = (int)Mathf.Lerp(startFontSize, endFontSize, scaleT);
            }
            else
            {
                text.fontSize = endFontSize;
            }
            
            // Update enemy bone position every frame (text follows the enemy)
            UpdateBonePosition(enemyInstance, rect, bonePos, verticalOffset, randomHorizontalOffset);
            
            // Increase upward offset for the fly-away effect
            verticalOffset += verticalSpeed * Time.deltaTime;
            
            // Smooth disappear at end
            float alpha = 1f;
            if (elapsed > fadeOutStart)
            {
                float fadeT = (elapsed - fadeOutStart) / (duration - fadeOutStart);
                alpha = 1f - fadeT;
            }
            
            text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            
            yield return null;
        }

        // Return to pool
        text.color = startColor;
        text.fontSize = endFontSize;
        _pool.ReturnTextObject(textObj);
    }

    private IEnumerator AnimateAradiaFloatingText(GameObject container, object playerInstance, BonePosition bonePos, DialogueStyle style, float duration, float initialVerticalOffset)
    {
        RectTransform rect = container.GetComponent<RectTransform>();
        UnityEngine.UI.Text text = container.GetComponentInChildren<UnityEngine.UI.Text>();

        if (rect == null || text == null)
        {
            Object.Destroy(container);
            yield break;
        }

        float elapsed = 0f;
        float verticalOffset = initialVerticalOffset;
        float horizontalOffset = style.HorizontalOffset;
        float scaleDuration = 0.35f;
        float startScale = 1.4f;
        float fadeDuration = Mathf.Min(1.5f, duration * 0.4f);
        Color baseColor = style.Color;
        Vector2 currentPosition = rect.anchoredPosition;
        Vector2 smoothVelocity = Vector2.zero;
        float smoothTime = 0.25f;
        float maxSpeed = float.PositiveInfinity;
        bool shouldFollowBone = playerInstance != null && style.FollowBone && !string.IsNullOrEmpty(bonePos.BoneName);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float scaleT = Mathf.Clamp01(elapsed / scaleDuration);
            float scale = Mathf.Lerp(startScale, 1f, scaleT);
            rect.localScale = new Vector3(scale, scale, 1f);

            // Keep static offsets; stay anchored

            if (shouldFollowBone)
            {
                Vector2 targetBonePosition = GetBoneScreenPosition(playerInstance, bonePos);
                if (targetBonePosition != Vector2.zero)
                {
                    Vector2 targetPosition = targetBonePosition + new Vector2(horizontalOffset, verticalOffset);
                    currentPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref smoothVelocity, smoothTime, maxSpeed, Time.deltaTime);
                    rect.anchoredPosition = currentPosition;
                }
                else
                {
                    UpdateBonePosition(playerInstance, rect, bonePos, verticalOffset, horizontalOffset);
                }
            }
            else
            {
                UpdateBonePosition(playerInstance, rect, bonePos, verticalOffset, horizontalOffset);
            }

            float alpha = 1f;
            if (elapsed > duration - fadeDuration)
            {
                alpha = Mathf.Clamp01(1f - (elapsed - (duration - fadeDuration)) / fadeDuration);
            }

            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        Object.Destroy(container);
    }
    
    /// <summary>
    /// Grab threat animation — quickly pops 40px up, then slowly drifts up (50 px/sec)
    /// Same style as TouzokuNormal comments
    /// </summary>
    private IEnumerator AnimateStaticThreat(GameObject container, float duration)
    {
        UnityEngine.UI.Text text = container.GetComponentInChildren<UnityEngine.UI.Text>();
        UnityEngine.UI.Image background = container.GetComponentInChildren<UnityEngine.UI.Image>();
        RectTransform containerRect = container.GetComponent<RectTransform>();
        
        if (text == null || containerRect == null)
        {
            Object.Destroy(container);
            yield break;
        }
        
        Color startTextColor = text.color;
        Color startBgColor = background != null ? background.color : new Color(0f, 0f, 0f, 0.1f);
        
        // Store the initial position (from the bone)
        Vector2 startPosition = containerRect.anchoredPosition;
        
        // Phase 1: quickly pops 40px up (0.2 seconds)
        float quickFlyDuration = 0.2f;
        float quickFlyTarget = 40f;
        
        // Phase 2: slowly drifts up for the remaining time
        float slowFlySpeed = 50f; // pixels per second upward
        
        float elapsed = 0f;
        float currentVerticalOffset = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            if (elapsed < quickFlyDuration)
            {
                // Phase 1: quickly pops 40px up
                float t = elapsed / quickFlyDuration;
                t = 1f - Mathf.Pow(1f - t, 3f); // Ease out
                currentVerticalOffset = Mathf.Lerp(0f, quickFlyTarget, t);
            }
            else
            {
                // Phase 2: slowly drifts up
                float slowElapsed = elapsed - quickFlyDuration;
                currentVerticalOffset = quickFlyTarget + (slowFlySpeed * slowElapsed);
            }
            
            // Apply position (static; does not track the bone)
            containerRect.anchoredPosition = startPosition + new Vector2(0f, currentVerticalOffset);
            
            // Smooth fade-out at the end (last 1 second)
            float fadeOutStart = duration - 1.0f;
            float alpha = 1f;
            if (elapsed > fadeOutStart)
            {
                float fadeT = (elapsed - fadeOutStart) / (duration - fadeOutStart);
                alpha = 1f - fadeT;
            }
            
            // Apply alpha to text and background
            text.color = new Color(startTextColor.r, startTextColor.g, startTextColor.b, startTextColor.a * alpha);
            if (background != null)
            {
                background.color = new Color(startBgColor.r, startBgColor.g, startBgColor.b, startBgColor.a * alpha);
            }
            
            yield return null;
        }
        
        // Destroy the container
        Object.Destroy(container);
    }
    
    /// <summary>
    /// Get screen-space bone position (enemy or PC) in the same H-scene.
    /// IMPORTANT: Always look up the bone inside the single H-scene Spine skeleton (enemyInstance),
    /// because both enemy and PC are animated in the same animation.
    /// </summary>
    private Vector2 GetBoneScreenPosition(object enemyInstance, BonePosition bonePos)
    {

        try
        {
            UnityEngine.Camera worldCam = TryGetDialogueWorldCamera();
            if (bonePos.UseScreenCenter || worldCam == null)
            {
                return Vector2.zero;
            }

            MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
            if (mb == null)
            {
                return Vector2.zero;
            }

            SkeletonAnimation spine = GetPrimarySkeletonForHost(mb);
            if (spine == null || spine.skeleton == null)
            {
                return Vector2.zero;
            }
            
            SkeletonAnimation targetSpine = spine;

            string boneName = string.IsNullOrEmpty(bonePos.BoneName) ? "bone13" : bonePos.BoneName;
            Spine.Bone bone = null;

            // Nested bone support via separator "/"
            // E.g.: "bone37/E_face/E_face" or "bone5/jigo_face/jigo_face"
            if (boneName.Contains("/"))
            {
                string[] bonePath = boneName.Split('/');
                bone = spine.skeleton.FindBone(bonePath[0]);

                // Walk the path nested bones
                for (int i = 1; i < bonePath.Length && bone != null; i++)
                {
                    bone = FindChildBone(bone, bonePath[i]);
                }
            }
            else
            {
                // Regular bone (not nested)
                bone = spine.skeleton.FindBone(boneName);
            }

            // If bone not found, try standard fallback bones
            if (!bonePos.DisableBoneFallbacks && bone == null && (boneName == "bone13" || boneName == "bone12" || boneName == "bone11" || boneName == "bone10"))
            {
                bone = spine.skeleton.FindBone("bone12") ??
                       spine.skeleton.FindBone("bone11") ??
                       spine.skeleton.FindBone("bone10");
            }

            // Player combat: primary bone may be missing on this skin — try typical head/hair on the same skeleton.
            if (!bonePos.DisableBoneFallbacks && bone == null && spine != null && spine.skeleton != null && mb.GetComponent<playercon>() != null)
            {
                string[] playerFallbacks =
                {
                    "hair1", "hair_front", "SIDE_hair", "head", "kubi", "face", "bone14", "bone12", "bone11", "bone10"
                };
                for (int i = 0; i < playerFallbacks.Length && bone == null; i++)
                {
                    bone = spine.skeleton.FindBone(playerFallbacks[i]);
                }
            }

            // Player may have multiple Spine rigs; display bones often live on a rig that is not hierarchy-first.
            if (bone == null && TryResolvePlayerBoneOnOtherSkeletons(mb, spine, boneName, !bonePos.DisableBoneFallbacks, out SkeletonAnimation altSpine, out Spine.Bone altBone))
            {
                spine = altSpine;
                targetSpine = altSpine;
                bone = altBone;
            }
            
            // If bone not found in the enemy skeleton, and this is a GG bone (face, head, bone14, SIDE_hair, bone44, etc.),
            // try to find it in the PC skeleton.
            if (!bonePos.DisableBoneFallbacks && bone == null && (boneName == "head" || boneName == "hair1" || boneName == "kubi" || boneName == "bone32" || boneName == "bone82" || boneName == "bone25" ||
                                 boneName == "face" || boneName == "hair_front" || boneName.Contains("bone33") ||
                                 boneName.Contains("front_hair") || boneName == "bone14" || boneName == "bone23" || boneName == "SIDE_hair" || boneName == "bone44"))
            {
                // Optimization: use cached playercon
                GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObj != null && TryFindBoneByNameOnAnySkeleton(playerObj, boneName, out SkeletonAnimation playerSkel, out Spine.Bone ggBone))
                {
                    bone = ggBone;
                    targetSpine = playerSkel;
                }
            }

            // If bones still not found — fallback: enemy transform position (H-scene center)
            if (bone == null)
            {
                Vector3 enemyWorldPos = mb.transform.position;
                if (!Mathf.Approximately(bonePos.WorldOffsetY, 0f))
                    enemyWorldPos.y += bonePos.WorldOffsetY;

                Vector3 enemyScreenPos = worldCam.WorldToScreenPoint(enemyWorldPos);

                if (enemyScreenPos.z > 0)
                {
                    RectTransform fallbackCanvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
                    if (fallbackCanvasRect != null)
                    {
                        Vector2 localPoint;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(fallbackCanvasRect, enemyScreenPos, null, out localPoint);
                        return localPoint;
                    }
                }

                return Vector2.zero;
            }

            Vector3 worldPos = GetBoneWorldPosition(targetSpine, bone, bonePos);
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                return Vector2.zero;
            }

            RectTransform boneCanvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
            if (boneCanvasRect == null)
            {
                return Vector2.zero;
            }

            Vector2 boneLocalPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boneCanvasRect, screenPos, null, out boneLocalPoint);
            return boneLocalPoint;
        }
        catch
        {
            return Vector2.zero;
        }
    }
    
    /// <summary>
    /// Display custom TouzokuNormal H-scene comment with streaming
    /// Red color, italic, larger font, bound to bone33
    /// </summary>
    internal void ShowTouzokuHSceneComment(object enemyInstance, string comment, float duration, float fontSize, float verticalOffset, float horizontalOffset, Color? textColor = null, Color? outlineColor = null, BonePosition? bonePos = null)
    {
        if (string.IsNullOrEmpty(comment))
        {
            return;
        }

        // Create container for text and background (as in ShowStaticThreat)
        GameObject container = new GameObject("TouzokuHSceneCommentContainer_XUAIGNORE");
        container.transform.SetParent(_pool.CanvasRoot.transform, false);
        
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        // Container size is set after text creation so it matches the text size
        
        // Create text first, so that get its size
        GameObject textObj = new GameObject("CommentText_XUAIGNORE");
        textObj.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = comment; // Full text at once (no streaming)
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontSize = (int)fontSize; // Use the provided font size
        textComponent.color = textColor ?? Color.white; // Use the provided color or white by default
        textComponent.fontStyle = FontStyle.Bold; // Bold font
        textComponent.raycastTarget = false;

        UnityEngine.UI.Outline outline = textObj.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null)
        {
            outline = textObj.AddComponent<UnityEngine.UI.Outline>();
        }
        outline.effectColor = outlineColor ?? Color.black; // Use the provided outline color or black by default
        outline.effectDistance = new Vector2(1f, -1f); // Thin outline
        // Add line breaks for for long phrases enemy
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        
        // Use font from pool
        var pool = _pool as DialoguePool;
        if (pool != null)
        {
            var cachedFont = pool.GetCachedFont();
            if (cachedFont != null)
            {
                textComponent.font = cachedFont;
            }
            else
            {
                textComponent.font = HellGateFontProvider.GetUiFont();
            }
        }
        
        // Get text size
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        
        // Fix width for wrapping long phrases (for 2 lines)
        float maxWidth = 350f; // width for two lines for enemy
        textRect.sizeDelta = new Vector2(maxWidth, 0f); // Height will be calculated automatically
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        
        // Get actual height after wrap
        float actualHeight = textComponent.preferredHeight;
        textRect.sizeDelta = new Vector2(maxWidth, actualHeight + 10f); // Add small padding
        
        // Set container size exactly to text size (adaptively)
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textRect.sizeDelta.x);
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textRect.sizeDelta.y);
        
        // Center text in container
        textRect.anchoredPosition = Vector2.zero;
        
        // Create background (violet-black) — fully transparent so it stays invisible
        GameObject background = new GameObject("CommentBackground_XUAIGNORE");
        background.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Image bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.1f, 0.05f, 0.15f, 0f); // Fully transparent background (alpha = 0)
        
        // Set render order: background should be behind text
        background.transform.SetAsFirstSibling();
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // Determine bone based on enemy type
        BonePosition actualBonePos = bonePos.HasValue ? bonePos.Value : GetHSceneBonePosition(enemyInstance);

        // Get initial bone position and add offsets
        Vector2 bonePosition = GetBoneScreenPosition(enemyInstance, actualBonePos);
        Vector2 startPosition = bonePosition + new Vector2(horizontalOffset, verticalOffset);

        containerRect.anchoredPosition = startPosition;

        // IMPORTANT: Set initial scale before activation, so that text is immediately compressed
        containerRect.localScale = new Vector3(0f, 1f, 1f);

        container.SetActive(true);

        // Start unfold coroutine bound to the bone (zoom support)
        // Pass text color into the coroutine so it persists during the animation
        _coroutineRunner.StartCoroutine(AnimateTouzokuHSceneComment(container, comment, duration, enemyInstance, actualBonePos, verticalOffset, textColor ?? Color.white));
    }
    
    /// <summary>
    /// Get current animation from enemyInstance
    /// </summary>
    private string GetCurrentAnimationName(object enemyInstance)
    {
        try
        {
            MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
            if (mb != null)
            {
                SkeletonAnimation spine = GetPrimarySkeletonForHost(mb);
                if (spine != null)
                {
                    return spine.AnimationName ?? string.Empty;
                }
            }
        }
        catch { }
        return string.Empty;
    }
    
    /// <summary>
    /// Get enemy text color (always white for consistency)
    /// </summary>
    private Color GetEnemyTextColorByAnimation(string animationName)
    {
        // All enemies now use white text for consistency
        return Color.white;
    }
    
    /// <summary>
    /// Get bone for H-scene comments depending on enemy type
    /// </summary>
    private BonePosition GetHSceneBonePosition(object enemyInstance)
    {
        if (enemyInstance == null)
        {
            return new BonePosition { BoneName = "bone13", UseScreenCenter = false };
        }
        
        MonoBehaviour hostMb = ResolveDialogueHost(enemyInstance);
        string typeName = hostMb != null ? hostMb.GetType().Name : enemyInstance.GetType().Name;
        
        // InquisitionBlack (InquiBlackEro) - enemy bone: bone11 (or bone11/E_face if nested)
        if (typeName == "InquiBlackEro" || typeName.Contains("InquisitionBlack") || typeName.Contains("InquiBlack"))
        {
            return new BonePosition 
            { 
                BoneName = "bone11",  // Inquisitor bone for H-scene comments (can try "bone11/E_face" if not works)
                UseScreenCenter = false
            };
        }
        
        // TouzokuAxe (EroTouzokuAXE) - ENEMY bones for H-scene comments: E_face (Start-Start5), bone126 (others)
        if (typeName == "EroTouzokuAXE" || typeName.Contains("TouzokuAXE"))
        {
            // Get current animation from spine enemy
            string currentAnim = null;
            try
            {
                MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
                if (mb != null)
                {
                    SkeletonAnimation spine = GetPrimarySkeletonForHost(mb);
                    if (spine != null)
                    {
                        currentAnim = spine.AnimationName;
                    }
                }
            }
            catch
            {
                // If animation could not be obtained, use fallback
            }

            // Opening animations: Start–Start5 — bone E_face
            if (!string.IsNullOrEmpty(currentAnim))
            {
                string animUpper = currentAnim.ToUpperInvariant();
                if (animUpper == "START" || animUpper == "START1" || animUpper == "START2" ||
                    animUpper == "START3" || animUpper == "START4" || animUpper == "START5")
                {
                    return new BonePosition
                    {
                        BoneName = "E_face",  // Bone for START–START5
                        UseScreenCenter = false
                    };
                }
            }

            // All remaining animations — ENEMY bone bone126
            return new BonePosition
            {
                BoneName = "bone126",  // ENEMY bone for remaining animations
                UseScreenCenter = false
            };
        }

        // TouzokuNormal (EroTouzoku) - ENEMY bone: bone148 with 50px vertical offset
        if (typeName == "EroTouzoku" || typeName.Contains("EroTouzoku"))
        {
            return new BonePosition
            {
                BoneName = "bone148",  // Enemy bone for TouzokuNormal H-scene comments
                UseScreenCenter = false
            };
        }

        // Goblin (goblinero) - ENEMY bone: bone37
        if (typeName == "goblinero" || typeName.Contains("goblin"))
        {
            return new BonePosition
            {
                BoneName = "bone37",  // Goblin bone for H-scene comments
                UseScreenCenter = false
            };
        }

        // Kakasi (EroAnimation for cross, kakashi_ero2 for ground)
        if (typeName == "EroAnimation" || typeName == "kakashi_ero2" || typeName.Contains("Kakasi") || typeName.Contains("Kakash"))
        {
            // Detect cross vs ground from type
            if (typeName == "EroAnimation")
            {
                // Cross — enemy bone bone9
                return new BonePosition 
                { 
                    BoneName = "bone9",  // Kakasi enemy bone on cross
                    UseScreenCenter = false
                };
            }
            else
            {
                // Ground — enemy bone bone24
                return new BonePosition 
                { 
                    BoneName = "bone24",  // Kakasi enemy bone on ground
                    UseScreenCenter = false
                };
            }
        }
        
        // By default
        return new BonePosition 
        { 
            BoneName = "bone13",
            UseScreenCenter = false
        };
    }
    
    /// <summary>
    /// Detects whether the enemy faces left (via Spine skeleton scaleX)
    /// Priority: Spine skeleton scaleX > transform scale > position relative to player
    /// </summary>
    private bool IsEnemyFacingLeft(object enemyInstance)
    {
        try
        {
            MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
            if (mb != null && mb.transform != null)
            {
                // Priority 1: check via Spine skeleton (most reliable)
                SkeletonAnimation spine = GetPrimarySkeletonForHost(mb);
                if (spine != null && spine.skeleton != null)
                {
                    // Check root skeleton scaleX
                    Spine.Bone rootBone = spine.skeleton.RootBone;
                    if (rootBone != null)
                    {
                        return rootBone.ScaleX < 0f;
                    }
                    
                    // Fallback: check transform scale
                    if (spine.transform.localScale.x < 0f)
                    {
                        return true;
                    }
                }
                
                // Priority 2: check via position relative to player (may be inaccurate in H-scenes)
                // Optimization: use cached playercon
                GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObj != null)
                {
                    Vector3 enemyPos = mb.transform.position;
                    Vector3 playerPos = playerObj.transform.position;
                    // If enemy is to the player's right, they usually face left
                    // If enemy is to the player's left, they usually face right
                    return enemyPos.x > playerPos.x;
                }
            }
        }
        catch { }
        
        return false; // Looks right by default
    }
    
    /// <summary>
    /// Positioning with vertical and horizontal offset (Touzoku H-scene comments)
    /// </summary>
    private void PositionRelativeToBoneWithOffset(object enemyInstance, RectTransform rect, BonePosition bonePos, float verticalOffset, float horizontalOffset)
    {
        try
        {
            UnityEngine.Camera worldCam = TryGetDialogueWorldCamera();
            SkeletonAnimation spine = null;
            Spine.Bone bone = null;

            MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
            if (mb != null)
            {
                spine = GetPrimarySkeletonForHost(mb);
                if (spine != null && spine.skeleton != null)
                {
                    string boneName = string.IsNullOrEmpty(bonePos.BoneName) ? "bone45" : bonePos.BoneName;
                    bone = spine.skeleton.FindBone(boneName);
                }
            }

            if (bone == null || worldCam == null)
            {
                // Fallback: use enemy transform position
                if (mb != null && mb.transform != null && worldCam != null)
                {
                    Vector3 enemyWorldPos = mb.transform.position;
                    Vector3 enemyScreenPos = worldCam.WorldToScreenPoint(enemyWorldPos);
                    
                    if (enemyScreenPos.z > 0)
                    {
                        RectTransform fallbackCanvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
                        if (fallbackCanvasRect != null)
                        {
                            Vector2 localPoint;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(fallbackCanvasRect, enemyScreenPos, null, out localPoint);
                            
                            rect.anchorMin = new Vector2(0.5f, 0.5f);
                            rect.anchorMax = new Vector2(0.5f, 0.5f);
                            rect.anchoredPosition = localPoint + new Vector2(horizontalOffset, verticalOffset);
                            return;
                        }
                    }
                }
                
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(horizontalOffset, verticalOffset);
                return;
            }

            Vector3 worldPos = GetBoneWorldPosition(spine, bone, bonePos);
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }

            RectTransform touzokuCanvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
            if (touzokuCanvasRect == null)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }
            
            Vector2 boneLocalPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(touzokuCanvasRect, screenPos, null, out boneLocalPoint);

            Vector2 finalPosition = boneLocalPoint + new Vector2(horizontalOffset, verticalOffset);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = finalPosition;
        }
        catch
        {
            rect.anchoredPosition = Vector2.zero;
        }
    }
    
    /// <summary>
    /// Touzoku/Aradia H-scene comment anim — pops 50px up from bone, unfolds, stays in place
    /// </summary>
    private IEnumerator AnimateTouzokuHSceneComment(GameObject container, string fullText, float duration)
    {
        return AnimateTouzokuHSceneComment(container, fullText, duration, null, null, 0f, Color.white);
    }
    
    /// <summary>
    /// Touzoku/Aradia H-scene comment anim bound to bone (follows zoom)
    /// </summary>
    private IEnumerator AnimateTouzokuHSceneComment(GameObject container, string fullText, float duration, object enemyInstance, BonePosition? bonePos, float verticalOffset, Color textColor)
    {
        UnityEngine.UI.Text text = container.GetComponentInChildren<UnityEngine.UI.Text>();
        UnityEngine.UI.Image background = container.GetComponentInChildren<UnityEngine.UI.Image>();
        RectTransform containerRect = container.GetComponent<RectTransform>();
        
        if (text == null || containerRect == null)
        {
            Object.Destroy(container);
            yield break;
        }
        
        // Use provided color or default text color
        Color startTextColor = textColor != Color.clear ? textColor : text.color;
        // Set initial text color
        text.color = startTextColor;
        Color startBgColor = background != null ? background.color : new Color(0.1f, 0.05f, 0.15f, 0.2f);
        
        // Position already set (verticalOffset for enemy/PC)
        Vector2 startPosition = containerRect.anchoredPosition;
        Vector2 currentPosition = startPosition; // Current smoothed position
        
        // Decide whether to update position every frame (if enemyInstance and bonePos were passed)
        bool shouldFollowBone = enemyInstance != null && bonePos.HasValue;
        
        // Variables for position smoothing (remove jitter)
        Vector2 smoothVelocity = Vector2.zero;
        float smoothTime = 0.25f; // Smoothing time (0.25 sec for smoother follow; removes jitter)
        float maxSpeed = float.PositiveInfinity; // No speed limit
        
        // Initial scale already set before container activation; ensure it stays collapsed
        if (containerRect.localScale.x > 0.01f)
        {
            containerRect.localScale = new Vector3(0f, 1f, 1f);
        }
        
        // Unfold duration
        float unfoldDuration = 0.25f; // 0.25 seconds for unfold
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Update position every frame, if need to follow the bone (for zoom support)
            if (shouldFollowBone)
            {
                Vector2 targetBonePosition = GetBoneScreenPosition(enemyInstance, bonePos.Value);
                if (targetBonePosition != Vector2.zero)
                {
                    Vector2 targetPosition = targetBonePosition + new Vector2(0f, verticalOffset);
                    
                    // Smooth position with SmoothDamp to remove jitter
                    currentPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref smoothVelocity, smoothTime, maxSpeed, Time.deltaTime);
                    containerRect.anchoredPosition = currentPosition;
                }
                else
                {
                    UpdateBonePosition(enemyInstance, containerRect, bonePos.Value, verticalOffset, 0f);
                }
            }
            else
            {
                // Position stays in place (without movement)
                containerRect.anchoredPosition = startPosition;
            }
            
            // Unfold phase (first 0.25 seconds)
            if (elapsed < unfoldDuration)
            {
                float unfoldT = elapsed / unfoldDuration;
                unfoldT = 1f - Mathf.Pow(1f - unfoldT, 3f); // Ease-out for smooth unfold
                float scaleX = Mathf.Lerp(0f, 1f, unfoldT);
                containerRect.localScale = new Vector3(scaleX, 1f, 1f);
            }
            else
            {
                // After unfold — full size
                containerRect.localScale = Vector3.one;
            }
            
            // Smooth fade-out at the end (last 1 second)
            float fadeOutStart = duration - 1.0f;
            float alpha = 1f;
            if (elapsed > fadeOutStart)
            {
                float fadeT = (elapsed - fadeOutStart) / (duration - fadeOutStart);
                alpha = 1f - fadeT;
            }
            
            // Apply alpha to text and background
            text.color = new Color(startTextColor.r, startTextColor.g, startTextColor.b, startTextColor.a * alpha);
            if (background != null)
            {
                background.color = new Color(startBgColor.r, startBgColor.g, startBgColor.b, startBgColor.a * alpha);
            }
            
            yield return null;
        }
        
        // Destroy the container
        Object.Destroy(container);
    }
    
    /// <summary>
    /// Update text position relative to enemy bone
    /// Called every frame to follow the enemy
    /// </summary>
    private void UpdateBonePosition(object enemyInstance, RectTransform rect, BonePosition bonePos, float verticalOffset, float horizontalOffset)
    {
        try
        {
            if (bonePos.UseScreenCenter)
            {
                return;
            }

            UnityEngine.Camera worldCam = TryGetDialogueWorldCamera();
            if (worldCam == null)
            {
                return;
            }

            SkeletonAnimation spine = null;
            Spine.Bone bone = null;

            MonoBehaviour mb = ResolveDialogueHost(enemyInstance);
            if (mb != null)
            {
                spine = GetPrimarySkeletonForHost(mb);
                if (spine != null && spine.skeleton != null)
                {
                    string boneName = string.IsNullOrEmpty(bonePos.BoneName) ? "bone13" : bonePos.BoneName;
                    
                    // Nested bone support via separator "/"
                    // E.g.: "bone37/E_face/E_face" or "bone5/jigo_face/jigo_face"
                    if (boneName.Contains("/"))
                    {
                        string[] bonePath = boneName.Split('/');
                        bone = spine.skeleton.FindBone(bonePath[0]);
                        
                        // Walk the path nested bones
                        for (int i = 1; i < bonePath.Length && bone != null; i++)
                        {
                            bone = FindChildBone(bone, bonePath[i]);
                        }
                    }
                    else
                    {
                        // Regular bone (not nested)
                        bone = spine.skeleton.FindBone(boneName);
                    }
                    
                    // If bone not found, try alternative bones
                    if (!bonePos.DisableBoneFallbacks && bone == null)
                    {
                        bone = spine.skeleton.FindBone("bone12") ?? 
                               spine.skeleton.FindBone("bone11") ?? 
                               spine.skeleton.FindBone("bone10");
                    }

                    if (!bonePos.DisableBoneFallbacks && bone == null && mb.GetComponent<playercon>() != null)
                    {
                        string[] playerFallbacks =
                        {
                            "hair1", "hair_front", "SIDE_hair", "head", "kubi", "face", "bone14", "bone12", "bone11", "bone10"
                        };
                        for (int i = 0; i < playerFallbacks.Length && bone == null; i++)
                        {
                            bone = spine.skeleton.FindBone(playerFallbacks[i]);
                        }
                    }

                    if (bone == null && TryResolvePlayerBoneOnOtherSkeletons(mb, spine, boneName, !bonePos.DisableBoneFallbacks, out SkeletonAnimation altSpine, out Spine.Bone altBone))
                    {
                        spine = altSpine;
                        bone = altBone;
                    }
                }
            }

            if (bone == null)
            {
                // Fallback: use enemy transform position
                if (mb != null && mb.transform != null)
                {
                    Vector3 enemyWorldPos = mb.transform.position;
                    Vector3 enemyScreenPos = worldCam.WorldToScreenPoint(enemyWorldPos);
                    
                    if (enemyScreenPos.z > 0)
                    {
                        RectTransform canvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
                        if (canvasRect != null)
                        {
                            Vector2 localPoint;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, enemyScreenPos, null, out localPoint);
                            
                            rect.anchorMin = new Vector2(0.5f, 0.5f);
                            rect.anchorMax = new Vector2(0.5f, 0.5f);
                            rect.anchoredPosition = localPoint + new Vector2(0f, 100f + verticalOffset);
                            return;
                        }
                    }
                }
                return;
            }

            Vector3 worldPos = GetBoneWorldPosition(spine, bone, bonePos);
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                return;
            }

            RectTransform updateCanvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
            if (updateCanvasRect == null)
            {
                return;
            }
            
            Vector2 boneLocalPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(updateCanvasRect, screenPos, null, out boneLocalPoint);

            Vector2 spawnOffset = new Vector2(
                horizontalOffset,
                80f + verticalOffset
            );
            
            Vector2 finalPosition = boneLocalPoint + spawnOffset;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = finalPosition;
        }
        catch
        {
            // Ignore errors while updating position
        }
    }
    
    /// <summary>
    /// Display PC (Aradia) reply during H-scene — bound to PC bones (not enemy!)
    /// Same approach as ShowTouzokuHSceneComment, but for PC bones
    /// </summary>
    internal void ShowAradiaHSceneComment(object enemyInstance, string comment, float duration, float fontSize, float verticalOffset, float horizontalOffset, string boneName, Color textColor, Color outlineColor)
    {
        if (string.IsNullOrEmpty(comment))
        {
            return;
        }

        // Create container for text and background (as in ShowTouzokuHSceneComment)
        GameObject container = new GameObject("AradiaHSceneCommentContainer_XUAIGNORE");
        container.transform.SetParent(_pool.CanvasRoot.transform, false);
        
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        
        // Create text first, so that get its size
        GameObject textObj = new GameObject("AradiaCommentText_XUAIGNORE");
        textObj.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = comment; // Full text at once (no streaming)
        textComponent.alignment = TextAnchor.UpperCenter;
        textComponent.fontSize = (int)fontSize; // Use the provided font size
        textComponent.color = textColor; // Use the provided text color
        textComponent.fontStyle = FontStyle.Bold; // Bold font like all others
        textComponent.raycastTarget = false;
        
        // Apply outline only if color is not transparent
        if (outlineColor.a > 0f)
        {
            UnityEngine.UI.Outline outline = textObj.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null)
            {
                outline = textObj.AddComponent<UnityEngine.UI.Outline>();
            }
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }
        // Force 2 lines without changing JSON: enable word wrap and limit width
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        
        // Use font from pool
        var pool = _pool as DialoguePool;
        if (pool != null)
        {
            var cachedFont = pool.GetCachedFont();
            if (cachedFont != null)
            {
                textComponent.font = cachedFont;
            }
            else
            {
                textComponent.font = HellGateFontProvider.GetUiFont();
            }
        }
        
        // Get text size
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        // Fix width for two lines (long text wraps to the second line)
        float maxWidth = 320f; // width for two lines
        textRect.sizeDelta = new Vector2(maxWidth, 0f); // Height will be calculated automatically
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        
        // Get actual height after wrap
        float actualHeight = textComponent.preferredHeight;
        textRect.sizeDelta = new Vector2(maxWidth, actualHeight + 10f); // Add small padding
        
        // Set container size exactly to text size (adaptively)
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textRect.sizeDelta.x);
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textRect.sizeDelta.y);
        
        // Center text in container
        textRect.anchoredPosition = Vector2.zero;
        
        // Create background (lighter than enemy) — fully transparent so it stays invisible
        GameObject background = new GameObject("AradiaCommentBackground_XUAIGNORE");
        background.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Image bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.15f, 0.1f, 0.2f, 0f); // Fully transparent background (alpha = 0)
        
        // Set render order: background should be behind text
        background.transform.SetAsFirstSibling();
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // IMPORTANT: Bind to PC bones (not enemy!)
        // Get PC bone position directly via GetBoneScreenPosition
        // GetBoneScreenPosition auto-detects GG bones (face, head, etc.) and finds them in the PC skeleton
        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false
        };
        
        Vector2 bonePosition = GetBoneScreenPosition(enemyInstance, bonePos);
        
        // Position: GG bone + offsets
        // If bonePosition == Vector2.zero, GetBoneScreenPosition already returned fallback (player position)
        containerRect.anchoredPosition = bonePosition + new Vector2(horizontalOffset, verticalOffset);
        
        // IMPORTANT: Set initial scale before activation, so that text is immediately compressed
        containerRect.localScale = new Vector3(0f, 1f, 1f);
        
        container.SetActive(true);
        
        // Start unfold coroutine bound to the bone (zoom support)
        // PC color is always white
        _coroutineRunner.StartCoroutine(AnimateTouzokuHSceneComment(container, comment, duration, enemyInstance, bonePos, verticalOffset, Color.white));
    }

    /// <summary>
    /// Display Aradia reply (ARADIA_RESPONSE) — first phase
    /// </summary>
    internal void ShowAradiaResponse(object playerInstance, string response, string boneName, DialogueStyle style, float duration)
    {
        if (string.IsNullOrEmpty(response))
        {
            return;
        }
        
        // Single bubble policy: replace previous Aradia bubble for this owner.
        // Prevents stacking and removes the need for multi-offset "lanes".
        DialogueStyle unifiedStyle = BuildAradiaResponseStyle(style.VerticalOffset, style.HorizontalOffset, true);

        // Create container for text and background (as in ShowTouzokuHSceneComment)
        GameObject container = new GameObject("AradiaResponseContainer_XUAIGNORE");
        container.transform.SetParent(_pool.CanvasRoot.transform, false);
        ReplaceActiveAradiaContainer(playerInstance, container);

        LiftAradiaContainerDrawOrder(container);

        RectTransform containerRect = container.GetComponent<RectTransform>();
        if (containerRect == null)
        {
            containerRect = container.AddComponent<RectTransform>();
        }
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Create text first, so that get its size
        GameObject textObj = new GameObject("AradiaResponseText_XUAIGNORE");
        textObj.transform.SetParent(container.transform, false);

        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = response; // Full text at once
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontSize = (int)unifiedStyle.FontSize;
        textComponent.color = new Color(unifiedStyle.Color.r, unifiedStyle.Color.g, unifiedStyle.Color.b, 1.0f); // Guarantee alpha = 1.0
        FontStyle responseFontStyle = FontStyle.Normal;
        if (unifiedStyle.IsBold)
        {
            responseFontStyle |= FontStyle.Bold;
        }
        if (unifiedStyle.IsItalic)
        {
            responseFontStyle |= FontStyle.Italic;
        }
        textComponent.fontStyle = responseFontStyle;
        textComponent.raycastTarget = false;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;

        // Apply outline from style only if UseOutline = true
        if (unifiedStyle.UseOutline)
        {
            ApplyOutline(textObj, unifiedStyle, Color.white, new Vector2(1f, -1f));
        }

        // Use font from pool
        var pool = _pool as DialoguePool;
        if (pool != null)
        {
            var cachedFont = pool.GetCachedFont();
            if (cachedFont != null)
            {
                textComponent.font = cachedFont;
            }
            else
            {
                textComponent.font = HellGateFontProvider.GetUiFont();
            }
        }

        // Get text size
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Fix width for wrapping long phrases
        float maxWidth = 300f;
        textRect.sizeDelta = new Vector2(maxWidth, 0f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

        float actualHeight = textComponent.preferredHeight;
        textRect.sizeDelta = new Vector2(maxWidth, actualHeight + 10f);

        // Set container size
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textRect.sizeDelta.x);
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textRect.sizeDelta.y);

        // Center text in container
        textRect.anchoredPosition = Vector2.zero;

        // Create background - fully transparent
        GameObject background = new GameObject("AradiaResponseBackground_XUAIGNORE");
        background.transform.SetParent(container.transform, false);

        UnityEngine.UI.Image bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0f);

        background.transform.SetAsFirstSibling();

        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        // IMPORTANT: Bind to PC bones (not enemy!)
        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false
        };

        Vector2 bonePosition = GetBoneScreenPosition(playerInstance, bonePos);

        // Position: GG bone + downward offset (default 50px below bone)
        float initialVerticalOffset = unifiedStyle.VerticalOffset;
        containerRect.anchoredPosition = bonePosition + new Vector2(0f, initialVerticalOffset);

        containerRect.localScale = Vector3.one;
        container.SetActive(true);

        // Debug Aradia text visibility
        // Plugin.Log.LogInfo($"[AradiaResponse] Showing text: '{response}' at position {containerRect.anchoredPosition}, container active: {container.activeSelf}");

        _coroutineRunner.StartCoroutine(AnimateAradiaFloatingText(container, playerInstance, bonePos, unifiedStyle, duration, initialVerticalOffset));
    }

    /// <summary>
    /// Display Aradia thoughts (ARADIA_THOUGHT) — second phase
    /// </summary>
    internal void ShowAradiaThought(
        object playerInstance,
        string thought,
        string boneName,
        DialogueStyle style,
        float duration,
        bool disableBoneFallbacks = false,
        float boneWorldOffsetY = 0f,
        Color? textColor = null,
        Color? outlineColor = null)
    {
        if (string.IsNullOrEmpty(thought))
        {
            return;
        }

        DialogueStyle unifiedStyle = BuildAradiaThoughtStyle(
            style.VerticalOffset,
            style.HorizontalOffset,
            true,
            textColor,
            outlineColor);

        // Create container for text and background (as in ShowTouzokuHSceneComment)
        GameObject container = new GameObject("AradiaThoughtContainer_XUAIGNORE");
        container.transform.SetParent(_pool.CanvasRoot.transform, false);
        ReplaceActiveAradiaContainer(playerInstance, container);

        LiftAradiaContainerDrawOrder(container);

        RectTransform containerRect = container.GetComponent<RectTransform>();
        if (containerRect == null)
        {
            containerRect = container.AddComponent<RectTransform>();
        }
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Create text
        GameObject textObj = new GameObject("AradiaThoughtText_XUAIGNORE");
        textObj.transform.SetParent(container.transform, false);

        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = thought;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontSize = (int)unifiedStyle.FontSize;
        textComponent.color = new Color(unifiedStyle.Color.r, unifiedStyle.Color.g, unifiedStyle.Color.b, 1.0f); // Guarantee alpha = 1.0
        FontStyle thoughtFontStyle = FontStyle.Normal;
        if (unifiedStyle.IsBold)
        {
            thoughtFontStyle |= FontStyle.Bold;
        }
        if (unifiedStyle.IsItalic)
        {
            thoughtFontStyle |= FontStyle.Italic;
        }
        textComponent.fontStyle = thoughtFontStyle;
        textComponent.raycastTarget = false;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;

        // Use font from pool
        var pool = _pool as DialoguePool;
        if (pool != null)
        {
            var cachedFont = pool.GetCachedFont();
            if (cachedFont != null)
            {
                textComponent.font = cachedFont;
            }
            else
            {
                textComponent.font = HellGateFontProvider.GetUiFont();
            }
        }

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);

        float maxWidth = 280f; // Slightly narrower for thoughts
        textRect.sizeDelta = new Vector2(maxWidth, 0f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

        float actualHeight = textComponent.preferredHeight;
        textRect.sizeDelta = new Vector2(maxWidth, actualHeight + 8f);

        // Set container size
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textRect.sizeDelta.x);
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textRect.sizeDelta.y);

        textRect.anchoredPosition = Vector2.zero;

        // Apply outline from style only if UseOutline = true
        if (unifiedStyle.UseOutline)
        {
            ApplyOutline(textObj, unifiedStyle, Color.white, new Vector2(1f, -1f));
        }

        // Background disabled for unified Aradia style.
        GameObject background = new GameObject("AradiaThoughtBackground_XUAIGNORE");
        background.transform.SetParent(container.transform, false);

        UnityEngine.UI.Image bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0f);

        background.transform.SetAsFirstSibling();

        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        // Bind to PC bones
        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false,
            DisableBoneFallbacks = disableBoneFallbacks,
            WorldOffsetY = boneWorldOffsetY
        };

        Vector2 bonePosition = GetBoneScreenPosition(playerInstance, bonePos);
        float initialVerticalOffset = unifiedStyle.VerticalOffset;
        containerRect.anchoredPosition = bonePosition + new Vector2(0f, initialVerticalOffset);

        containerRect.localScale = Vector3.one;
        container.SetActive(true);

        _coroutineRunner.StartCoroutine(AnimateAradiaFloatingText(container, playerInstance, bonePos, unifiedStyle, duration, initialVerticalOffset));
    }

    /// <summary>
    /// Immediately hide all dialogue bubbles (H-scene comments, Aradia lines, onomatopoeia, threats).
    /// Stops running display coroutines so timed fade-outs do not leave text after escape.
    /// </summary>
    internal void DismissAllVisible()
    {
        if (_coroutineRunner != null)
            _coroutineRunner.StopAllCoroutines();

        foreach (KeyValuePair<object, GameObject> entry in _activeAradiaContainerByOwner)
        {
            if (entry.Value != null)
                Object.Destroy(entry.Value);
        }
        _activeAradiaContainerByOwner.Clear();

        GameObject canvasRoot = _pool?.CanvasRoot;
        if (canvasRoot == null)
            return;

        Transform root = canvasRoot.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            GameObject go = child.gameObject;
            if (go.name.StartsWith("DialogueText_"))
            {
                if (go.activeSelf)
                    _pool.ReturnTextObject(go);
                continue;
            }

            Object.Destroy(go);
        }

        // Explicit fallback: thought containers may survive under renamed parents after scene changes.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            string n = child.name;
            if (n.IndexOf("AradiaThought", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("AradiaFloating", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("AradiaResponse", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Object.Destroy(child.gameObject);
            }
        }
    }
}

internal class DialogueCoroutineRunner : MonoBehaviour
{
}


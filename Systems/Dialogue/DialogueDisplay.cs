using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;
using NoREroMod;

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
    /// Uses deep sky blue text (#00BFFF) with black outline (same as enemies).
    /// </summary>
    internal static DialogueStyle BuildAradiaThoughtStyle(float verticalOffset, float horizontalOffset, bool followBone)
    {
        var fontStyle = Plugin.GetFontStyle(Plugin.aradiaResponseFontStyle.Value);

        // Deep sky blue text (#00BFFF) with black outline (same as enemies) for Aradia thoughts
        return new DialogueStyle
        {
            FontSize = Plugin.dialogueFontSize.Value,
            Color = Plugin.ParseColor("0,191,255,255"), // #00BFFF - deep sky blue
            OutlineColor = Plugin.ParseColor("0,0,0,255"), // Black outline (same as enemies)
            OutlineDistance = new Vector2(1f, -1f),
            UseOutline = true, // Always use outline
            IsBold = false, // No bold for thoughts
            IsItalic = true, // Always italic for thoughts
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
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
    private Spine.Bone FindChildBone(Spine.Bone parent, string childName)
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

            SkeletonAnimation spine = null;
            Spine.Bone bone = null;
            string boneName = string.IsNullOrEmpty(bonePos.BoneName) ? "bone13" : bonePos.BoneName;
            SkeletonAnimation targetSpine = null; // Add variable for storing correct spine

            // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] PositionRelativeToBone called: boneName='{boneName}', enemyInstance={enemyInstance?.GetType().Name ?? "null"}");

            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            // UnityEngine.Debug.Log($"[ONOMATOPOEIA DEBUG] mb={mb != null}");
            if (mb != null)
            {
                spine = mb.GetComponentInChildren<SkeletonAnimation>();
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
                    if (bone == null)
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
                    else
                    {
                        // UnityEngine.Debug.Log($"[ONOMATOPOEIA BONE] Successfully found bone '{boneName}'");
                    }
                }
            }

            // If bone not found in скелете enemy, and this is GG bone (head, bone32, bone82, bone25, bone33/front_hair, hair_front, face, SIDE_hair, bone44 etc.),
            // try find in GG skeleton
            if (bone == null && (boneName == "head" || boneName == "bone32" || boneName == "bone82" || boneName == "bone25" || boneName == "face" || boneName == "hair_front" || boneName.Contains("bone33") || boneName.Contains("front_hair") || boneName == "SIDE_hair" || boneName == "bone44"))
            {
                // Optimization: use cached playercon
                GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObj != null)
                {
                    SkeletonAnimation playerSpine = playerObj.GetComponentInChildren<SkeletonAnimation>();
                    if (playerSpine != null && playerSpine.skeleton != null)
                    {
                        if (boneName.Contains("/"))
                        {
                            string[] bonePath = boneName.Split('/');
                            bone = playerSpine.skeleton.FindBone(bonePath[0]);
                            for (int i = 1; i < bonePath.Length && bone != null; i++)
                            {
                                bone = FindChildBone(bone, bonePath[i]);
                            }
                        }
                        else
                        {
                            bone = playerSpine.skeleton.FindBone(boneName);
                        }
                        
                        // IMPORTANT: Save reference on GG skeleton, if bone found there
                        if (bone != null)
                        {
                            targetSpine = playerSpine;
                        }
                    }
                }
            }

            if (bone == null || UnityEngine.Camera.main == null)
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

            // Use targetSpine if it is set (GG bone), иначе spine enemy
            SkeletonAnimation finalSpine = targetSpine != null ? targetSpine : spine;
            Vector3 worldPos = finalSpine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(worldPos);

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
    /// Appears in place (without flyout) и disappears in 1 second
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
    /// Grab threat animation - smooth move up over 5 секунд
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
        
        // Quick appear (0.2 сек)
        int startFontSize = 1;
        int endFontSize = text.fontSize;
        text.fontSize = startFontSize;

        // Upward offset for flyaway animation (accumulates over time)
        // 2x slower than onomatopoeia: 50 пикселей/сек instead of 100
        float verticalOffset = 0f;
        float verticalSpeed = 50f; // пикселей in second up (2x slower)
        
        // Random horizontal offset (computed once)
        float randomHorizontalOffset = UnityEngine.Random.Range(-20f, 20f);
        
        float scaleInDuration = 0.2f; // Quick appear
        float fadeOutStart = duration - 1.0f; // Disappear start 1 sec before end
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Appear phase (быстрая)
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
            
            // Update bone position enemy every frame (text следует за enemyом)
            UpdateBonePosition(enemyInstance, rect, bonePos, verticalOffset, randomHorizontalOffset);
            
            // Увеличиваем offset up for эффекта улетания
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

        // Возврат in пул
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
        bool shouldFollowBone = playerInstance != null && bonePos.BoneName != null;

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
                    Vector2 targetPosition = targetBonePosition + new Vector2(0f, verticalOffset);
                    currentPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref smoothVelocity, smoothTime, maxSpeed, Time.deltaTime);
                    rect.anchoredPosition = currentPosition;
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
    /// Grab threat animation - быстро вылетает on 40px up, затем медленbut уходит up (50px/сек)
    /// Такой же стиль as у TouzokuNormal comments
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
        
        // Сохраняем начальную позицию (from кости)
        Vector2 startPosition = containerRect.anchoredPosition;
        
        // Фаза 1: Быстро вылетает on 40px up (0.2 seconds)
        float quickFlyDuration = 0.2f;
        float quickFlyTarget = 40f;
        
        // Фаза 2: Медленbut уходит up over оставшегося времени
        float slowFlySpeed = 50f; // пикселей in second up
        
        float elapsed = 0f;
        float currentVerticalOffset = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            if (elapsed < quickFlyDuration)
            {
                // Фаза 1: Быстро вылетает on 40px up
                float t = elapsed / quickFlyDuration;
                t = 1f - Mathf.Pow(1f - t, 3f); // Ease out
                currentVerticalOffset = Mathf.Lerp(0f, quickFlyTarget, t);
            }
            else
            {
                // Фаза 2: Медленbut уходит up
                float slowElapsed = elapsed - quickFlyDuration;
                currentVerticalOffset = quickFlyTarget + (slowFlySpeed * slowElapsed);
            }
            
            // Применяем позицию (статичная, not отслеживает кость)
            containerRect.anchoredPosition = startPosition + new Vector2(0f, currentVerticalOffset);
            
            // Smooth disappear at end (последняя 1 second)
            float fadeOutStart = duration - 1.0f;
            float alpha = 1f;
            if (elapsed > fadeOutStart)
            {
                float fadeT = (elapsed - fadeOutStart) / (duration - fadeOutStart);
                alpha = 1f - fadeT;
            }
            
            // Применяем alpha к textу и фону
            text.color = new Color(startTextColor.r, startTextColor.g, startTextColor.b, startTextColor.a * alpha);
            if (background != null)
            {
                background.color = new Color(startBgColor.r, startBgColor.g, startBgColor.b, startBgColor.a * alpha);
            }
            
            yield return null;
        }
        
        // Удаляем контейнер
        Object.Destroy(container);
    }
    
    /// <summary>
    /// Get экранную bone position (enemy or ГГ) in одной и той же H-сцене.
    /// IMPORTANT: Всегда ищем кость inside одного Spine-скелета H-scene (enemyInstance),
    /// потому as enemy, и ГГ анимируются in одной и той же animation.
    /// </summary>
    private Vector2 GetBoneScreenPosition(object enemyInstance, BonePosition bonePos)
    {

        try
        {
            if (bonePos.UseScreenCenter || UnityEngine.Camera.main == null)
            {
                return Vector2.zero;
            }

            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            if (mb == null)
            {
                return Vector2.zero;
            }

            SkeletonAnimation spine = mb.GetComponentInChildren<SkeletonAnimation>();
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

            // If bone not found, пробуем стандартные fallback-кости
            if (bone == null && (boneName == "bone13" || boneName == "bone12" || boneName == "bone11" || boneName == "bone10"))
            {
                bone = spine.skeleton.FindBone("bone12") ??
                       spine.skeleton.FindBone("bone11") ??
                       spine.skeleton.FindBone("bone10");
            }
            
            // If bone not found in скелете enemy, and this is GG bone (face, head, bone14, SIDE_hair, bone44 etc.),
            // пробуем найти its in скелете ГГ.
            if (bone == null && (boneName == "head" || boneName == "bone32" || boneName == "bone82" || boneName == "bone25" ||
                                 boneName == "face" || boneName == "hair_front" || boneName.Contains("bone33") ||
                                 boneName.Contains("front_hair") || boneName == "bone14" || boneName == "bone23" || boneName == "SIDE_hair" || boneName == "bone44"))
            {
                // Optimization: use cached playercon
                GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObj != null)
                {
                    SkeletonAnimation playerSpine = playerObj.GetComponentInChildren<SkeletonAnimation>();
                    if (playerSpine != null && playerSpine.skeleton != null)
                    {
                        if (boneName.Contains("/"))
                        {
                            string[] bonePath = boneName.Split('/');
                            bone = playerSpine.skeleton.FindBone(bonePath[0]);
                            for (int i = 1; i < bonePath.Length && bone != null; i++)
                            {
                                bone = FindChildBone(bone, bonePath[i]);
                            }
                        }
                        else
                        {
                            bone = playerSpine.skeleton.FindBone(boneName);
                        }
                        
                        if (bone != null)
                        {
                            targetSpine = playerSpine;
                        }
                    }
                }
            }

            // If кости так и not found – fallback: позиция transform enemy (центр H-scene)
            if (bone == null)
            {
                Vector3 enemyWorldPos = mb.transform.position;
                Vector3 enemyScreenPos = UnityEngine.Camera.main.WorldToScreenPoint(enemyWorldPos);

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

            // Конвертируем мировую bone position in экранные координаты
            Vector3 worldPos = targetSpine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(worldPos);

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
    /// Display кастомный комментарий TouzokuNormal during H-scene with стримингом
    /// Красный цвет, курсив, больший шрифт, привязка к кости bone33
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
        // Размер контейнера will установлен after создания text, so that соответствовать sizeу text
        
        // Create text first, so that get its size
        GameObject textObj = new GameObject("CommentText_XUAIGNORE");
        textObj.transform.SetParent(container.transform, false);
        
        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = comment; // Full text at once (no streaming)
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontSize = (int)fontSize; // Use переданный size шрифта
        textComponent.color = textColor ?? Color.white; // Use переданный цвет or white by умолчанию
        textComponent.fontStyle = FontStyle.Bold; // Жирный шрифт
        textComponent.raycastTarget = false;

        UnityEngine.UI.Outline outline = textObj.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null)
        {
            outline = textObj.AddComponent<UnityEngine.UI.Outline>();
        }
        outline.effectColor = outlineColor ?? Color.black; // Use переданный цвет обводки or черный by умолчанию
        outline.effectDistance = new Vector2(1f, -1f); // Тонкая обводка
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
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
        
        // Create background (фиолетово-черный) - fully transparent, to avoid would be visible
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

        // Start coroutine with unfold и привязкой к кости (for zoom support)
        // Pass цвет text in корутину, so that он сохранялся во время animation
        _coroutineRunner.StartCoroutine(AnimateTouzokuHSceneComment(container, comment, duration, enemyInstance, actualBonePos, verticalOffset, textColor ?? Color.white));
    }
    
    /// <summary>
    /// Get current animation from enemyInstance
    /// </summary>
    private string GetCurrentAnimationName(object enemyInstance)
    {
        try
        {
            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            if (mb != null)
            {
                SkeletonAnimation spine = mb.GetComponentInChildren<SkeletonAnimation>();
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
    /// Get цвет text enemy (always white for консистентности)
    /// </summary>
    private Color GetEnemyTextColorByAnimation(string animationName)
    {
        // Все enemyи теперь используют white text for консистентности
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
        
        string typeName = enemyInstance.GetType().Name;
        
        // InquisitionBlack (InquiBlackEro) - enemy bone: bone11 (or bone11/E_face if nested)
        if (typeName == "InquiBlackEro" || typeName.Contains("InquisitionBlack") || typeName.Contains("InquiBlack"))
        {
            return new BonePosition 
            { 
                BoneName = "bone11",  // Inquisitor bone for H-scene comments (can try "bone11/E_face" if not works)
                UseScreenCenter = false
            };
        }
        
        // TouzokuAxe (EroTouzokuAXE) - кости ВРАГА for H-scene comments: E_face (Start-Start5), bone126 (остальные)
        if (typeName == "EroTouzokuAXE" || typeName.Contains("TouzokuAXE"))
        {
            // Get current animation from spine enemy
            string currentAnim = null;
            try
            {
                MonoBehaviour mb = enemyInstance as MonoBehaviour;
                if (mb != null)
                {
                    SkeletonAnimation spine = mb.GetComponentInChildren<SkeletonAnimation>();
                    if (spine != null)
                    {
                        currentAnim = spine.AnimationName;
                    }
                }
            }
            catch
            {
                // If not удалось получить animation, используем fallback
            }

            // Начальные animation: Start, Start1, Start2, Start3, Start4, Start5 - кость E_face
            if (!string.IsNullOrEmpty(currentAnim))
            {
                string animUpper = currentAnim.ToUpperInvariant();
                if (animUpper == "START" || animUpper == "START1" || animUpper == "START2" ||
                    animUpper == "START3" || animUpper == "START4" || animUpper == "START5")
                {
                    return new BonePosition
                    {
                        BoneName = "E_face",  // Кость for START-START5
                        UseScreenCenter = false
                    };
                }
            }

            // Все остальные animation - кость ВРАГА bone126
            return new BonePosition
            {
                BoneName = "bone126",  // Кость ВРАГА for остальных анимаций
                UseScreenCenter = false
            };
        }

        // TouzokuNormal (EroTouzoku) - кость ВРАГА: bone148 with вертикальным offsetм 50px
        if (typeName == "EroTouzoku" || typeName.Contains("EroTouzoku"))
        {
            return new BonePosition
            {
                BoneName = "bone148",  // Кость enemy for TouzokuNormal H-scene comments
                UseScreenCenter = false
            };
        }

        // Goblin (goblinero) - кость ВРАГА: bone37
        if (typeName == "goblinero" || typeName.Contains("goblin"))
        {
            return new BonePosition
            {
                BoneName = "bone37",  // Кость гоблиon for H-scene comments
                UseScreenCenter = false
            };
        }

        // Kakasi (EroAnimation for креста, kakashi_ero2 for земли)
        if (typeName == "EroAnimation" || typeName == "kakashi_ero2" || typeName.Contains("Kakasi") || typeName.Contains("Kakash"))
        {
            // Определяем, крест this or земля by типу
            if (typeName == "EroAnimation")
            {
                // Крест - кость enemy bone9
                return new BonePosition 
                { 
                    BoneName = "bone9",  // Кость enemy Kakasi on кресте
                    UseScreenCenter = false
                };
            }
            else
            {
                // Земля - кость enemy bone24
                return new BonePosition 
                { 
                    BoneName = "bone24",  // Кость enemy Kakasi on земле
                    UseScreenCenter = false
                };
            }
        }
        
        // By умолчанию
        return new BonePosition 
        { 
            BoneName = "bone13",
            UseScreenCenter = false
        };
    }
    
    /// <summary>
    /// Определяет, смотрит ли enemy влево (через scaleX скелета Spine)
    /// Приоритет: Spine skeleton scaleX > transform scale > позиция относительbut игрока
    /// </summary>
    private bool IsEnemyFacingLeft(object enemyInstance)
    {
        try
        {
            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            if (mb != null && mb.transform != null)
            {
                // Приоритет 1: проверяем через Spine skeleton (most reliable способ)
                SkeletonAnimation spine = mb.GetComponentInChildren<SkeletonAnimation>();
                if (spine != null && spine.skeleton != null)
                {
                    // Проверяем scaleX корневого костяка
                    Spine.Bone rootBone = spine.skeleton.RootBone;
                    if (rootBone != null)
                    {
                        return rootBone.ScaleX < 0f;
                    }
                    
                    // Fallback: проверяем scale transform
                    if (spine.transform.localScale.x < 0f)
                    {
                        return true;
                    }
                }
                
                // Приоритет 2: проверяем через позицию относительbut игрока (for H-scenes может быть неточным)
                // Optimization: use cached playercon
                GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObj != null)
                {
                    Vector3 enemyPos = mb.transform.position;
                    Vector3 playerPos = playerObj.transform.position;
                    // If enemy справа from игрока, он обычbut смотрит влево
                    // If enemy слева from игрока, он обычbut смотрит right
                    return enemyPos.x > playerPos.x;
                }
            }
        }
        catch { }
        
        return false; // By умолчанию смотрит right
    }
    
    /// <summary>
    /// Позиционирование with вертикальным и горизонтальным offsetм (for Touzoku H-scene comments)
    /// </summary>
    private void PositionRelativeToBoneWithOffset(object enemyInstance, RectTransform rect, BonePosition bonePos, float verticalOffset, float horizontalOffset)
    {
        try
        {
            SkeletonAnimation spine = null;
            Spine.Bone bone = null;

            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            if (mb != null)
            {
                spine = mb.GetComponentInChildren<SkeletonAnimation>();
                if (spine != null && spine.skeleton != null)
                {
                    string boneName = string.IsNullOrEmpty(bonePos.BoneName) ? "bone45" : bonePos.BoneName;
                    bone = spine.skeleton.FindBone(boneName);
                }
            }

            if (bone == null || UnityEngine.Camera.main == null)
            {
                // Fallback: используем позицию transform enemy
                if (mb != null && mb.transform != null)
                {
                    Vector3 enemyWorldPos = mb.transform.position;
                    Vector3 enemyScreenPos = UnityEngine.Camera.main.WorldToScreenPoint(enemyWorldPos);
                    
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

            Vector3 worldPos = spine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(worldPos);

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

            // Позиция над костью with вертикальным и горизонтальным offsetм
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
    /// Анимация комментария Touzoku/Aradia H-scene - вылетает on 50px up from bone, разворачивается и остается in place
    /// </summary>
    private IEnumerator AnimateTouzokuHSceneComment(GameObject container, string fullText, float duration)
    {
        return AnimateTouzokuHSceneComment(container, fullText, duration, null, null, 0f, Color.white);
    }
    
    /// <summary>
    /// Анимация комментария Touzoku/Aradia H-scene bound to bone (for следования on зуме)
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
        
        // Use переданный цвет or цвет text by умолчанию
        Color startTextColor = textColor != Color.clear ? textColor : text.color;
        // Set начальный цвет text
        text.color = startTextColor;
        Color startBgColor = background != null ? background.color : new Color(0.1f, 0.05f, 0.15f, 0.2f);
        
        // Position already set (verticalOffset for enemy/ГГ)
        Vector2 startPosition = containerRect.anchoredPosition;
        Vector2 currentPosition = startPosition; // Текущая сглаженная позиция
        
        // Определяем, need to ли обновлять позицию every frame (if переданы enemyInstance and bonePos)
        bool shouldFollowBone = enemyInstance != null && bonePos.HasValue;
        
        // Переменные for сглаживания позиции (чтобы убрать тряску)
        Vector2 smoothVelocity = Vector2.zero;
        float smoothTime = 0.25f; // Время сглаживания (0.25 сек for более плавного следования, убирает тряску)
        float maxSpeed = float.PositiveInfinity; // Without ограничения скорости
        
        // Начальный scale already установлен before activation контейнера, but убеждаемся that он сжат
        if (containerRect.localScale.x > 0.01f)
        {
            containerRect.localScale = new Vector3(0f, 1f, 1f);
        }
        
        // Длительность разворачивания
        float unfoldDuration = 0.25f; // 0.25 seconds on разворачивание
        
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
                    
                    // Сглаживаем позицию with помощью SmoothDamp for устранения тряски
                    currentPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref smoothVelocity, smoothTime, maxSpeed, Time.deltaTime);
                    containerRect.anchoredPosition = currentPosition;
                }
            }
            else
            {
                // Position stays in place (without movement)
                containerRect.anchoredPosition = startPosition;
            }
            
            // Фаза разворачивания (первые 0.25 seconds)
            if (elapsed < unfoldDuration)
            {
                float unfoldT = elapsed / unfoldDuration;
                unfoldT = 1f - Mathf.Pow(1f - unfoldT, 3f); // Ease out for плавного разворачивания
                float scaleX = Mathf.Lerp(0f, 1f, unfoldT);
                containerRect.localScale = new Vector3(scaleX, 1f, 1f);
            }
            else
            {
                // After разворачивания - полный size
                containerRect.localScale = Vector3.one;
            }
            
            // Smooth disappear at end (последняя 1 second)
            float fadeOutStart = duration - 1.0f;
            float alpha = 1f;
            if (elapsed > fadeOutStart)
            {
                float fadeT = (elapsed - fadeOutStart) / (duration - fadeOutStart);
                alpha = 1f - fadeT;
            }
            
            // Применяем alpha к textу и фону
            text.color = new Color(startTextColor.r, startTextColor.g, startTextColor.b, startTextColor.a * alpha);
            if (background != null)
            {
                background.color = new Color(startBgColor.r, startBgColor.g, startBgColor.b, startBgColor.a * alpha);
            }
            
            yield return null;
        }
        
        // Удаляем контейнер
        Object.Destroy(container);
    }
    
    /// <summary>
    /// Обновление позиции text relative to bone enemy
    /// Вызывается every frame for следования за enemyом
    /// </summary>
    private void UpdateBonePosition(object enemyInstance, RectTransform rect, BonePosition bonePos, float verticalOffset, float horizontalOffset)
    {
        try
        {
            if (bonePos.UseScreenCenter || UnityEngine.Camera.main == null)
            {
                return;
            }

            SkeletonAnimation spine = null;
            Spine.Bone bone = null;

            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            if (mb != null)
            {
                spine = mb.GetComponentInChildren<SkeletonAnimation>();
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
                    if (bone == null)
                    {
                        bone = spine.skeleton.FindBone("bone12") ?? 
                               spine.skeleton.FindBone("bone11") ?? 
                               spine.skeleton.FindBone("bone10");
                    }
                }
            }

            if (bone == null)
            {
                // Fallback: используем позицию transform enemy
                if (mb != null && mb.transform != null)
                {
                    Vector3 enemyWorldPos = mb.transform.position;
                    Vector3 enemyScreenPos = UnityEngine.Camera.main.WorldToScreenPoint(enemyWorldPos);
                    
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

            // Get мировую bone position
            Vector3 worldPos = spine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                return;
            }

            RectTransform updateCanvasRect = _pool.CanvasRoot.GetComponent<RectTransform>();
            if (updateCanvasRect == null)
            {
                return;
            }
            
            // Конвертируем экранную позицию in локальные координаты канваса
            Vector2 boneLocalPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(updateCanvasRect, screenPos, null, out boneLocalPoint);

            // Добавляем горизонтальное offset (один раз) и вертикальное offset for улетания
            Vector2 spawnOffset = new Vector2(
                horizontalOffset,
                80f + verticalOffset // Базовое offset + накапливающееся offset up
            );
            
            Vector2 finalPosition = boneLocalPoint + spawnOffset;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = finalPosition;
        }
        catch
        {
            // Игнорируем ошибки on обновлении позиции
        }
    }
    
    /// <summary>
    /// Display ответ ГГ (Aradia) during H-scene - привязка к костям ГГ (not enemy!)
    /// Uses the same же подход as ShowTouzokuHSceneComment, but for костей ГГ
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
        textComponent.fontSize = (int)fontSize; // Use переданный size шрифта
        textComponent.color = textColor; // Use переданный цвет text
        textComponent.fontStyle = FontStyle.Bold; // Жирный шрифт as у всех
        textComponent.raycastTarget = false;
        
        // Apply outline only if цвет not прозрачный
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
        // Make 2 строки without изменения JSON: включаем переноwith by словам и ограничиваем ширину
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
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }
        
        // Get text size
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        // Fix width под две строки (if text длинный – он переносится on вторую строку)
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
        
        // Create background (светлее чем у enemy) - fully transparent, to avoid would be visible
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
        
        // IMPORTANT: Привязываем к костям ГГ (not enemy!)
        // Get bone position ГГ directly через GetBoneScreenPosition
        // GetBoneScreenPosition automatically определит, that this GG bone (face, head etc.), и найдет its in скелете ГГ
        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false
        };
        
        Vector2 bonePosition = GetBoneScreenPosition(enemyInstance, bonePos);
        
        // Позиция: GG bone + смещения
        // If bonePosition == Vector2.zero, GetBoneScreenPosition already вернул fallback (позицию игрока)
        containerRect.anchoredPosition = bonePosition + new Vector2(horizontalOffset, verticalOffset);
        
        // IMPORTANT: Set initial scale before activation, so that text is immediately compressed
        containerRect.localScale = new Vector3(0f, 1f, 1f);
        
        container.SetActive(true);
        
        // Start coroutine with unfold и привязкой к кости (for zoom support)
        // Цвет for ГГ always white
        _coroutineRunner.StartCoroutine(AnimateTouzokuHSceneComment(container, comment, duration, enemyInstance, bonePos, verticalOffset, Color.white));
    }

    /// <summary>
    /// Display ответ Аради (ARADIA_RESPONSE) - for первой фазы
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

        // Set высокий sortingOrder for visibility поверх other UI
        Canvas containerCanvas = container.GetComponent<Canvas>();
        if (containerCanvas == null)
        {
            containerCanvas = container.AddComponent<Canvas>();
        }
        containerCanvas.overrideSorting = true;
        containerCanvas.sortingOrder = 15000; // Выше чем основной канваwith (10000)

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
        textComponent.color = new Color(unifiedStyle.Color.r, unifiedStyle.Color.g, unifiedStyle.Color.b, 1.0f); // Гарантируем alpha = 1.0
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
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        // IMPORTANT: Привязываем к костям ГГ (not enemy!)
        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false
        };

        Vector2 bonePosition = GetBoneScreenPosition(playerInstance, bonePos);

        // Позиция: GG bone + offset down (by умолчанию 50px ниже кости)
        float initialVerticalOffset = unifiedStyle.VerticalOffset;
        containerRect.anchoredPosition = bonePosition + new Vector2(0f, initialVerticalOffset);

        containerRect.localScale = Vector3.one;
        container.SetActive(true);

        // Отладка visibility text Арадии
        // Plugin.Log.LogInfo($"[AradiaResponse] Showing text: '{response}' at position {containerRect.anchoredPosition}, container active: {container.activeSelf}, canvas sortingOrder: {containerCanvas.sortingOrder}");

        _coroutineRunner.StartCoroutine(AnimateAradiaFloatingText(container, playerInstance, bonePos, unifiedStyle, duration, initialVerticalOffset));
    }

    /// <summary>
    /// Display мысли Аради (ARADIA_THOUGHT) - for второй фазы
    /// </summary>
    internal void ShowAradiaThought(object playerInstance, string thought, string boneName, DialogueStyle style, float duration)
    {
        if (string.IsNullOrEmpty(thought))
        {
            return;
        }
        
        // Use italic style for thoughts
        DialogueStyle unifiedStyle = BuildAradiaThoughtStyle(style.VerticalOffset, style.HorizontalOffset, true);

        // Create container for text and background (as in ShowTouzokuHSceneComment)
        GameObject container = new GameObject("AradiaThoughtContainer_XUAIGNORE");
        container.transform.SetParent(_pool.CanvasRoot.transform, false);
        ReplaceActiveAradiaContainer(playerInstance, container);

        // Set высокий sortingOrder for visibility поверх other UI
        Canvas containerCanvas = container.GetComponent<Canvas>();
        if (containerCanvas == null)
        {
            containerCanvas = container.AddComponent<Canvas>();
        }
        containerCanvas.overrideSorting = true;
        containerCanvas.sortingOrder = 15000; // Выше чем основной канваwith (10000)

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
        textComponent.color = new Color(unifiedStyle.Color.r, unifiedStyle.Color.g, unifiedStyle.Color.b, 1.0f); // Гарантируем alpha = 1.0
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
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);

        float maxWidth = 280f; // Немного already for мыслей
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

        // Привязываем к костям ГГ
        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false
        };

        Vector2 bonePosition = GetBoneScreenPosition(playerInstance, bonePos);
        float initialVerticalOffset = unifiedStyle.VerticalOffset;
        containerRect.anchoredPosition = bonePosition + new Vector2(0f, initialVerticalOffset);

        containerRect.localScale = Vector3.one;
        container.SetActive(true);

        // Отладка visibility text Арадии

        _coroutineRunner.StartCoroutine(AnimateAradiaFloatingText(container, playerInstance, bonePos, unifiedStyle, duration, initialVerticalOffset));
    }
}

internal class DialogueCoroutineRunner : MonoBehaviour
{
}


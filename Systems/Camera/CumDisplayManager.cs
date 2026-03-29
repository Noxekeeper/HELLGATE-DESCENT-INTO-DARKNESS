using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// X-ray and pregnancy clip display manager.
/// Loads PNG frames from clip folders and renders them during H-scenes.
/// Supports dual-slot rendering (pregnancy and VAG/X-ray simultaneously).
/// </summary>
internal class CumDisplayManager
{
    private const string DefaultClipFolder = "CUM_DEFAULT-0";
    private static readonly Dictionary<string, string[]> EnemyClipMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        // Inquisition Black -> oral
        { "inquiblackero", new[] { @"FIN\ORA\IN_MOUTH_NoX-ray", @"FIN\ORA\CumInMouthXray" } },
        { "inquisitionblack", new[] { @"FIN\ORA\IN_MOUTH_NoX-ray", @"FIN\ORA\CumInMouthXray" } },
        // Goblin -> специальный клип GoblinsCum2way
        { "goblinero", new[] { @"FIN\Goblins\GoblinsCum2way" } },
        { "goblin", new[] { @"FIN\Goblins\GoblinsCum2way" } },
        // Touzoku Normal / Axe -> vaginal variants
        { "touzokuero", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        { "erotouzoku", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        { "touzokuaxero", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        { "erotouzokuaxe", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        // Kakasi (Scarecrow) -> vaginal variants
        { "kakashi_ero2", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        { "kakasi", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        // Dorei (SinnerslaveCrossbow) -> vaginal variants
        { "sinnerslavecrossbowero", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        { "dorei", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        // Bigoni Brother -> vaginal
        { "bigoni", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        { "bigonibrother", new[] { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" } },
        // SlaveBigAxe -> специальный клип BigAxe
        { "slavebigaxeero", new[] { @"FIN\Goblins\BigAxe" } },
        { "slavebigaxe", new[] { @"FIN\Goblins\BigAxe" } },
    };

    private GameObject _canvas;
    private RawImage _image;
    private RectTransform _rect;
    private RawImage _image2;
    private RectTransform _rect2;
    private MonoBehaviour _runner;
    private Coroutine _playCoroutine;
    private Coroutine _playCoroutine2;
    private readonly object _lock = new();
    private readonly List<Texture2D> _frames = new();
    private readonly Dictionary<string, List<Texture2D>> _frameCache = new Dictionary<string, List<Texture2D>>(StringComparer.OrdinalIgnoreCase);
    private string _resolvedDirectory = string.Empty;
    private bool _initialized;
    private bool _isPlaying;  // for первого слота (беременность)
    private bool _isPlaying2; // for второго слота (VAG/X-ray)
    private string _lastEnemy = string.Empty;
    private float _lastPlayTime = -999f;

    internal void Initialize()
    {
        if (_initialized) return;
        try
        {
            // Canvas
            _canvas = new GameObject("CumDisplayCanvas");
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 33000;

            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _canvas.AddComponent<GraphicRaycaster>().enabled = false;
            _canvas.layer = LayerMask.NameToLayer("UI");

            // Runner
            _runner = _canvas.AddComponent<CumDisplayRunner>();
            UnityEngine.Object.DontDestroyOnLoad(_canvas);

            // Panel / Image
            var panel = new GameObject("CumDisplayPanel");
            panel.transform.SetParent(_canvas.transform, false);
            _rect = panel.AddComponent<RectTransform>();
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(400f, 400f);

            _image = panel.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.enabled = false;

            // Second slot (for goblin oral+vaginal)
            var panel2 = new GameObject("CumDisplayPanel2");
            panel2.transform.SetParent(_canvas.transform, false);
            _rect2 = panel2.AddComponent<RectTransform>();
            _rect2.anchorMin = new Vector2(0.5f, 0.5f);
            _rect2.anchorMax = new Vector2(0.5f, 0.5f);
            _rect2.pivot = new Vector2(0.5f, 0.5f);
            _rect2.sizeDelta = new Vector2(320f, 240f);

            _image2 = panel2.AddComponent<RawImage>();
            _image2.raycastTarget = false;
            _image2.enabled = false;

            // Scale for WorldSpace to approximate screen size
            panel.transform.localScale = Vector3.one * 0.0025f;
            panel2.transform.localScale = Vector3.one * 0.0025f;

            _canvas.SetActive(false);
            _initialized = true;
            var cam = GetCamera();
            canvas.worldCamera = cam;
            var offsets = GetOffsetsForClip(null);
            PositionInWorld(canvas, cam, offsets.x, offsets.y);

            // Preload frames (prevents freezes on first show)
            PreloadClip(DefaultClipFolder);
            PreloadClip(@"Pregnant\Pregnant_action\Pregnant_action");
            
            // Preload special goblin clip
            PreloadClip(@"FIN\Goblins\GoblinsCum2way");
            
            // Preload all VAG/ORA clips for enemies
            var uniqueClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var clipList in EnemyClipMap.Values)
            {
                if (clipList != null)
                {
                    foreach (var clip in clipList)
                    {
                        if (!string.IsNullOrEmpty(clip))
                        {
                            uniqueClips.Add(clip);
                        }
                    }
                }
            }
            foreach (var clip in uniqueClips)
            {
                PreloadClip(clip);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[CumDisplay] Init error: {ex}");
        }
    }

    private void PreloadClip(string clipFolder)
    {
        var cacheKey = (clipFolder ?? string.Empty).ToLowerInvariant();
        if (_frameCache.ContainsKey(cacheKey)) return;

        var loaded = LoadFramesFromDisk(clipFolder);
        if (loaded != null && loaded.Count > 0)
        {
            _frameCache[cacheKey] = loaded;
            // preload info suppressed
        }
        else
        {
            Plugin.Log?.LogWarning($"[CumDisplay] Preload failed for {clipFolder}");
        }
    }

    private static UnityEngine.Camera GetCamera()
    {
        return UnityEngine.Camera.main;
    }

    private void PositionInWorld(Canvas canvas, UnityEngine.Camera cam, float offsetX, float offsetY)
    {
        if (canvas == null || _rect == null || cam == null) return;

        // Position relative to camera: offset right/up from view center at specified depth
        float worldDepth = Plugin.cumDisplayWorldDepth?.Value ?? 3f;
        var vp = new Vector3(0.5f + offsetX, 0.5f + offsetY, worldDepth);
        var worldPos = cam.ViewportToWorldPoint(vp);

        _rect.position = worldPos;
        _rect.rotation = cam.transform.rotation;
    }

    private Vector2 GetOffsetsForClip(string clipFolderOverride)
    {
        if (!string.IsNullOrEmpty(clipFolderOverride) &&
            clipFolderOverride.IndexOf("pregnant", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            float pregOffsetX = Plugin.cumDisplayPregnantOffsetX?.Value ?? 0.25f;
            float pregOffsetY = Plugin.cumDisplayPregnantOffsetY?.Value ?? 0f;
            return new Vector2(pregOffsetX, pregOffsetY);
        }
        // Use anchored offsets for overlay
        float offsetX = Plugin.cumDisplayAnchoredOffsetX?.Value ?? 350f;
        float offsetY = Plugin.cumDisplayAnchoredOffsetY?.Value ?? 100f;
        return new Vector2(offsetX, offsetY);
    }

    /// <summary>
    /// Checks if clip is playing in slot 2 (VAG/X-ray).
    /// </summary>
    internal bool IsPlayingVAG()
    {
        // Check both flag and active coroutine for reliability
        return _isPlaying2 || (_playCoroutine2 != null && _runner != null);
    }

    internal void ShowClimax(string enemyName = null, string clipFolderOverride = null)
    {
        try
        {
            // Keep this as debug-level diagnostics to avoid false error noise in runtime logs.
            Plugin.Log?.LogDebug($"[CumDisplayManager] ShowClimax called with enemyName: '{enemyName}'");

            bool isPregnancy = !string.IsNullOrEmpty(clipFolderOverride) &&
                               clipFolderOverride.IndexOf("pregnant", StringComparison.OrdinalIgnoreCase) >= 0;
            string resolvedClip = clipFolderOverride;
            if (string.IsNullOrEmpty(resolvedClip))
            {
                // Select custom clip by enemy if available
                string key = enemyName?.ToLowerInvariant() ?? string.Empty;
                // Plugin.Log.LogInfo($"[CumDisplayManager] Looking for clips for enemy: '{enemyName}' -> key: '{key}'");

                if (!string.IsNullOrEmpty(key) && EnemyClipMap.TryGetValue(key, out var list) && list != null && list.Length > 0)
                {
                    // Plugin.Log.LogInfo($"[CumDisplayManager] Found {list.Length} clip options for '{key}': {string.Join(", ", list)}");

                    // Special handling for BigoniBrother removed - now uses standard logic like other enemies
                    // For goblins: try special clip first, fallback to standard if not exists
                    if (key.Contains("goblin"))
                    {
                        try
                        {
                            // Try first clip (special for goblins)
                            string specialClip = list[0];
                            if (!string.IsNullOrEmpty(specialClip))
                            {
                                // Check if special clip exists
                                var candidates = BuildCandidateDirectories(specialClip);
                                bool specialExists = false;
                                try
                                {
                                    specialExists = candidates.Any(dir =>
                                    {
                                        try
                                        {
                                            bool dirExists = Directory.Exists(dir);
                                            int pngCount = dirExists ? Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly).Length : 0;
                                            // Plugin.Log.LogInfo($"[CumDisplayManager] Checking path '{dir}': exists={dirExists}, png_files={pngCount}");

                                            return dirExists && pngCount > 0;
                                        }
                                        catch (Exception ex)
                                        {
                                            // Plugin.Log.LogInfo($"[CumDisplayManager] Error checking path '{dir}': {ex.Message}");
                                            return false;
                                        }
                                    });

                                    // Plugin.Log.LogInfo($"[CumDisplayManager] Special clip '{specialClip}' exists: {specialExists}");
                                }
                                catch
                                {
                                    // If check failed - assume special clip doesn't exist
                                    specialExists = false;
                                }

                                if (specialExists)
                                {
                                    resolvedClip = specialClip;
                                }
                                else
                                {
                                    // Special clip doesn't exist - use random from others
                                    if (list.Length > 1)
                                    {
                                        resolvedClip = list[UnityEngine.Random.Range(1, list.Length)];
                                    }
                                    else
                                    {
                                        resolvedClip = specialClip; // If only one option - use it
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // If something went wrong with goblin processing - fallback to random
                            resolvedClip = list[UnityEngine.Random.Range(0, list.Length)];
                        }
                    }
                    else
                    {
                        // Standard logic for all enemies: find all existing clips and randomly select from them
                        var existingClips = new List<string>();
                        foreach (var candidateClip in list)
                        {
                            var candidates = BuildCandidateDirectories(candidateClip);
                            bool clipExists = candidates.Any(dir =>
                            {
                                try
                                {
                                    bool dirExists = Directory.Exists(dir);
                                    int pngCount = dirExists ? Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly).Length : 0;
                                    // Plugin.Log.LogInfo($"[CumDisplayManager] Checking clip '{candidateClip}' path '{dir}': exists={dirExists}, png_files={pngCount}");

                                    return dirExists && pngCount > 0;
                                }
                                catch (Exception ex)
                                {
                                    // Plugin.Log.LogInfo($"[CumDisplayManager] Error checking clip '{candidateClip}' path '{dir}': {ex.Message}");
                                    return false;
                                }
                            });

                            if (clipExists)
                            {
                                existingClips.Add(candidateClip);
                                // Plugin.Log.LogInfo($"[CumDisplayManager] Found existing clip: '{candidateClip}'");
                            }
                        }

                        if (existingClips.Count > 0)
                        {
                            // Randomly select from existing clips (like Touzoku does)
                            int randomIndex = UnityEngine.Random.Range(0, existingClips.Count);
                            resolvedClip = existingClips[randomIndex];
                            // Plugin.Log.LogInfo($"[CumDisplayManager] Selected random existing clip: '{resolvedClip}' from {existingClips.Count} available");
                        }
                        else
                        {
                            // Fallback to random selection from full list if none exist
                            int randomIndex = UnityEngine.Random.Range(0, list.Length);
                            resolvedClip = list[randomIndex];
                            Plugin.Log.LogWarning($"[CumDisplayManager] No existing clips found for '{key}', using fallback: '{resolvedClip}'");
                        }
                    }
                }
            }
            float now = Time.realtimeSinceStartup;
            // Cooldown only for non-pregnancy clips
            if (!isPregnancy && !string.IsNullOrEmpty(enemyName) && _lastEnemy == enemyName && now - _lastPlayTime < 4f)
            {
                return;
            }
            if (!EnsureReady())
            {
                Plugin.Log?.LogWarning("[CumDisplay] Init failed, abort");
                return;
            }

            // Reinitialize if something destroyed at runtime
            if (_canvas == null || _image == null || _rect == null || 
                _image2 == null || _rect2 == null || _runner == null)
            {
                _initialized = false;
                if (!EnsureReady())
                {
                    Plugin.Log?.LogWarning("[CumDisplay] Re-init failed, abort");
                    return;
                }
            }

            // Update camera binding and position before playback
            var cam = GetCamera();
            var canvasComp = _canvas != null ? _canvas.GetComponent<Canvas>() : null;
            if (canvasComp != null)
            {
                // Show all clips in overlay for stable positioning from screen center
                canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasComp.worldCamera = null;
                canvasComp.overrideSorting = true;
                canvasComp.sortingOrder = 33000;
            }

            // Determine which slot clip goes to:
            // - Pregnancy → slot 1 (_image, _rect)
            // - VAG/X-ray → slot 2 (_image2, _rect2)
            bool useSlot1 = isPregnancy;
            RawImage targetImage = useSlot1 ? _image : _image2;
            RectTransform targetRect = useSlot1 ? _rect : _rect2;
            bool isCurrentlyPlaying = useSlot1 ? _isPlaying : _isPlaying2;
            
            // Interrupt if clip already playing in target slot
            if (isCurrentlyPlaying)
            {
                if (useSlot1)
                {
                    // Interrupt slot 1
                    if (_playCoroutine != null && _runner != null)
                    {
                        (_runner as CumDisplayRunner)?.StopCoroutine(_playCoroutine);
                        _playCoroutine = null;
                    }
                    if (_image != null)
                    {
                        _image.enabled = false;
                        _image.texture = null;
                    }
                    _isPlaying = false;
                }
                else
                {
                    // Interrupt slot 2
                    if (_playCoroutine2 != null && _runner != null)
                    {
                        (_runner as CumDisplayRunner)?.StopCoroutine(_playCoroutine2);
                        _playCoroutine2 = null;
                    }
                    if (_image2 != null)
                    {
                        _image2.enabled = false;
                        _image2.texture = null;
                    }
                    _isPlaying2 = false;
                }
            }

            if (!LoadFrames(resolvedClip))
            {
                Plugin.Log?.LogWarning("[CumDisplay] No frames loaded, abort");
                return;
            }

            // Copy frames for independent playback
            var framesCopy = new List<Texture2D>(_frames);

            // Positioning and sizing
            if (targetRect != null)
            {
                targetRect.localScale = Vector3.one;
                if (isPregnancy)
                {
                    // Pregnancy: fixed screen coordinates (400, 440) with anchor (0.5, 0.5)
                    float screenWidth = Screen.width;
                    float screenHeight = Screen.height;
                    const float targetX = 400f;
                    const float targetY = 440f;
                    float offsetX = targetX - screenWidth * 0.5f;
                    float offsetY = targetY - screenHeight * 0.5f;
                    targetRect.anchoredPosition = new Vector2(offsetX, offsetY);
                    // Size from frames (uses actual file dimensions)
                    if (framesCopy.Count > 0 && framesCopy[0] != null)
                    {
                        float w = framesCopy[0].width;
                        float h = framesCopy[0].height;
                        targetRect.sizeDelta = new Vector2(w, h);
                    }
                    else
                    {
                        // Fallback to default size if frames not loaded
                        targetRect.sizeDelta = new Vector2(600f, 338f);
                    }
                }
                else
                {
                    // VAG/X-ray: standard position
                    var offsets = GetOffsetsForClip(resolvedClip);
                    targetRect.anchoredPosition = new Vector2(offsets.x, offsets.y);
                    // Size from frames
                    if (framesCopy.Count > 0 && framesCopy[0] != null)
                    {
                        float w = framesCopy[0].width;
                        float h = framesCopy[0].height;
                        targetRect.sizeDelta = new Vector2(w, h);
                    }
                }
            }

            _canvas.SetActive(true);
            targetImage.enabled = true;

            // Update flags and metadata
            if (useSlot1)
            {
                _isPlaying = true;
            }
            else
            {
                _isPlaying2 = true;
                _lastEnemy = enemyName ?? string.Empty;
                _lastPlayTime = now;
            }

            // Standard playback speed for all enemies
            float frameDuration = Plugin.cumDisplayFrameDuration?.Value ?? (1f / 25f);
            
            // Start coroutine in appropriate slot
            if (useSlot1)
            {
                _playCoroutine = (_runner as CumDisplayRunner)?.StartCoroutine(PlayOnce(_image, framesCopy, 0, frameDuration));
            }
            else
            {
                _playCoroutine2 = (_runner as CumDisplayRunner)?.StartCoroutine(PlayOnce(_image2, framesCopy, 1, frameDuration));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[CumDisplay] ShowClimax error: {ex}");
        }
    }

    internal void ResetClimaxFlag()
    {
        StopPlayback();
    }

    internal void Hide()
    {
        StopPlayback();
    }

    private void StopPlayback()
    {
        try
        {
            // Stop slot 1
            if (_playCoroutine != null && _runner != null)
            {
                (_runner as CumDisplayRunner)?.StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            _isPlaying = false;
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }

            // Stop slot 2
            if (_playCoroutine2 != null && _runner != null)
            {
                (_runner as CumDisplayRunner)?.StopCoroutine(_playCoroutine2);
                _playCoroutine2 = null;
            }
            _isPlaying2 = false;
            if (_image2 != null)
            {
                _image2.enabled = false;
                _image2.texture = null;
            }

            // Deactivate canvas only if both slots stopped
            if (_canvas != null && !_isPlaying && !_isPlaying2)
            {
                _canvas.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[CumDisplay] StopPlayback error: {ex}");
        }
    }

    private bool LoadFrames(string clipFolderOverride = null)
    {
        try
        {
            string clipFolder = string.IsNullOrEmpty(clipFolderOverride) ? DefaultClipFolder : clipFolderOverride;

            var cacheKey = clipFolder.ToLowerInvariant();
            // Use cache if available
            if (_frameCache.TryGetValue(cacheKey, out var cached))
            {
                _frames.Clear();
                _frames.AddRange(cached);
                _resolvedDirectory = cacheKey;
                return _frames.Count > 0;
            }

            _frames.Clear();
            _resolvedDirectory = string.Empty;

            var loaded = LoadFramesFromDisk(clipFolder);
            if (loaded != null && loaded.Count > 0)
            {
                _frames.AddRange(loaded);
                _frameCache[cacheKey] = loaded;
                _resolvedDirectory = cacheKey;
                // preload info suppressed
                return true;
            }

            Plugin.Log?.LogWarning($"[CumDisplay] No frames found for clip {clipFolder}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[CumDisplay] LoadFrames error: {ex.Message}");
        }
        return false;
    }

    private List<Texture2D> LoadFramesFromDisk(string clipFolder)
    {
        // Plugin.Log.LogInfo($"[CumDisplayManager] Loading frames for clip: '{clipFolder}'");

        var result = new List<Texture2D>();

        // Check if clipFolder is an absolute path (starts with drive letter like C:\)
        bool isAbsolutePath = !string.IsNullOrEmpty(clipFolder) && clipFolder.Contains(@":\");

        IEnumerable<string> candidates;
        if (isAbsolutePath)
        {
            // Use the absolute path directly
            candidates = new List<string> { clipFolder };
            // Plugin.Log.LogInfo($"[CumDisplayManager] Using absolute path directly: '{clipFolder}'");
        }
        else
        {
            // Use standard candidate search
            candidates = BuildCandidateDirectories(clipFolder);
        }

        foreach (var dir in candidates)
        {
            try
            {
                // Plugin.Log.LogInfo($"[CumDisplayManager] Checking directory: '{dir}'");
                bool dirExists = Directory.Exists(dir);
                // Plugin.Log.LogInfo($"[CumDisplayManager] Directory exists: {dirExists}");

                if (!dirExists) continue;

                var files = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly)
                                     .OrderBy(f => NaturalSortKey(f), StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

                // Plugin.Log.LogInfo($"[CumDisplayManager] Found {files.Length} PNG files in '{dir}'");
                if (files.Length == 0) continue;

                foreach (var file in files)
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(file);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(data, true)) // markNonReadable: true - GPU-only, reduces memory/GC
                        {
                            result.Add(tex);
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(tex);
                        }
                    }
                    catch
                    {
                        // Skip corrupted frames
                    }
                }

                if (result.Count > 0)
                {
                    // load info suppressed
                    return result;
                }
            }
            catch
            {
                // Skip directories that couldn't be read
                continue;
            }
        }
        return result;
    }

    private static string NaturalSortKey(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        // Find trailing digits
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i])) i--;
        i++;
        if (i < name.Length && int.TryParse(name.Substring(i), out int num))
        {
            return name.Substring(0, i) + num.ToString("D4");
        }
        return name;
    }

    private IEnumerable<string> BuildCandidateDirectories(string clipFolder)
    {
        // Plugin.Log.LogInfo($"[CumDisplayManager] Building candidate directories for clip: '{clipFolder}'");

        var list = new List<string>();
        void add(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                list.Add(path);
                // Plugin.Log.LogInfo($"[CumDisplayManager] Added candidate path: '{path}'");
            }
        }

        // 1) Current sources next to mod
        try
        {
            string asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(asmDir))
            {
                add(Path.Combine(Path.Combine(Path.Combine(asmDir, "sources"), "HellGate_sources"), clipFolder));
                add(Path.Combine(Path.Combine(asmDir, "HellGate_sources"), clipFolder));
                add(Path.Combine(asmDir, clipFolder));
            }
        }
        catch { }

        // 2) Repository root path (if running in editor)
        try
        {
            add(Path.Combine(Path.Combine("sources", "HellGate_sources"), clipFolder));
            add(Path.Combine("HellGate_sources", clipFolder));
        }
        catch { }

        // 3) Game root: [GameFolder]/sources/HellGate_sources/[clipFolder] (portable, works on any machine)
        try
        {
            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            add(Path.Combine(Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources"), clipFolder));
        }
        catch { }

        return list;
    }

    private System.Collections.IEnumerator PlayOnce(RawImage img, List<Texture2D> frames, int slotIndex, float frameDuration)
    {
        if (frames == null || frames.Count == 0 || img == null)
        {
            // Reset flag for corresponding slot
            if (slotIndex == 0)
                _isPlaying = false;
            else
                _isPlaying2 = false;
            yield break;
        }

        int i = 0;
        while (i < frames.Count)
        {
            if (img == null)
            {
                Plugin.Log?.LogWarning("[CumDisplay] PlayOnce aborted: image destroyed");
                // Reset flag for corresponding slot
                if (slotIndex == 0)
                    _isPlaying = false;
                else
                    _isPlaying2 = false;
                yield break;
            }

            var frame = frames[i];
            if (frame != null)
            {
                img.texture = frame;
            }
            i++;
            yield return new WaitForSecondsRealtime(frameDuration);
        }

        // Clip finished - cleanup corresponding slot
        if (slotIndex == 0)
        {
            // Slot 1 (pregnancy)
            _isPlaying = false;
            if (_playCoroutine != null && _runner != null)
            {
                (_runner as CumDisplayRunner)?.StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }
        }
        else
        {
            // Slot 2 (VAG/X-ray)
            _isPlaying2 = false;
            if (_playCoroutine2 != null && _runner != null)
            {
                (_runner as CumDisplayRunner)?.StopCoroutine(_playCoroutine2);
                _playCoroutine2 = null;
            }
            if (_image2 != null)
            {
                _image2.enabled = false;
                _image2.texture = null;
            }
        }

        // Deactivate canvas only if both slots stopped
        if (_canvas != null && !_isPlaying && !_isPlaying2)
        {
            _canvas.SetActive(false);
        }
    }

    private bool EnsureReady()
    {
        lock (_lock)
        {
            if (!_initialized || _canvas == null || _image == null || _rect == null || 
                _image2 == null || _rect2 == null || _runner == null)
            {
                Plugin.Log?.LogWarning("[CumDisplay] Cached objects missing, reinitializing");
                _initialized = false;
                Initialize();
            }
            return _initialized && _canvas != null && _image != null && _rect != null && 
                   _image2 != null && _rect2 != null && _runner != null;
        }
    }

}

internal class CumDisplayRunner : MonoBehaviour { }

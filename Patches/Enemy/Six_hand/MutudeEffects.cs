using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NoREroMod;
using UnityEngine.UI;

namespace NoREroMod.Patches.Enemy.Six_hand
{
    internal static class MutudeEffects
    {
        private const float FrameDuration = 0.083f; // ~12 FPS (demon_cum default)

        private static GameObject videoCanvas;
        private static RawImage videoImage;
        private static RectTransform videoRect;
        private static MutudeVideoPositionTracker videoTracker;
        private static bool videoActive;
        private static bool playOnce = false; // Флаг for одноразового проигрывания
        private static string[] framePaths = new string[0];
        private static readonly List<Texture2D> frameTextures = new();
        private static Coroutine frameCoroutine;
        private static string sourcesDirectoryCache;

        internal static void PlayVideo(string videoDirectory, Vector2 anchoredPosition, Vector2 size, float rotationDegrees = 0f, bool loop = true)
        {
            try
            {
                EnsureVideoUI();

                if (videoCanvas == null || videoImage == null)
                {
                    return;
                }

                var candidateDirectories = BuildCandidateDirectories(videoDirectory);
                if (!TryResolveFrameDirectory(candidateDirectories, out var resolvedDirectory, out var resolvedFrames))
                {
                    return;
                }

                framePaths = resolvedFrames;

                ClearFrameTextures();
                LoadFrameTextures();

                if (frameTextures.Count == 0)
                {
                    return;
                }

                Vector2 finalSize = size;
                if ((finalSize.x <= 0f || finalSize.y <= 0f) && frameTextures.Count > 0 && frameTextures[0] != null)
                {
                    var tex = frameTextures[0];
                    finalSize = new Vector2(tex.width, tex.height);
                }
                
                // If одноразовое проигрывание (loop = false), уменьшаем size on 40% (умножаем on 0.6)
                if (!loop)
                {
                    finalSize = new Vector2(finalSize.x * 0.6f, finalSize.y * 0.6f);
                }

                ApplyVideoTransform(anchoredPosition, finalSize, rotationDegrees);
                
                playOnce = !loop; // Set flag одноразового проигрывания

                MonoBehaviour runner = null;
                GameObject runnerObj = GameObject.Find("MutudeCoroutineRunner");
                if (runnerObj == null)
                {
                    runnerObj = new GameObject("MutudeCoroutineRunner");
                    UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                }
                runner = runnerObj.GetComponent<MutudeCoroutineRunner>();
                if (runner == null)
                {
                    runner = runnerObj.AddComponent<MutudeCoroutineRunner>();
                }
                
                if (runner == null)
                {
                    return;
                }

                if (frameCoroutine != null)
                {
                    try
                    {
                        runner.StopCoroutine(frameCoroutine);
                    }
                    catch
                    {
                        // ignore
                    }
                    frameCoroutine = null;
                }

                videoActive = true;
                frameCoroutine = runner.StartCoroutine(FramePlayback());
            }
            catch (Exception ex)
            {
            }
        }

        internal static void StopVideo(bool force = false)
        {
            if (!videoActive && !force)
            {
                return;
            }

            videoActive = false;

            MonoBehaviour runner = null;
            GameObject runnerObj = GameObject.Find("MutudeCoroutineRunner");
            if (runnerObj != null)
            {
                runner = runnerObj.GetComponent<MutudeCoroutineRunner>();
            }
            
            if (frameCoroutine != null && runner != null)
            {
                try
                {
                    runner.StopCoroutine(frameCoroutine);
                }
                catch
                {
                    // ignore
                }
                frameCoroutine = null;
            }

            if (videoImage != null)
            {
                videoImage.texture = null;
                videoImage.enabled = false;
            }

            if (videoCanvas != null)
            {
                videoCanvas.SetActive(false);
            }

            ClearFrameTextures();
        }

        internal static void StopAll(bool force = false)
        {
            StopVideo(force);
        }

        internal static string GetFinVideoPath()
        {
            string baseDir = GetSourcesDirectory();
            string hellgateDir = Path.Combine(baseDir, "HellGate_sources");
            return Path.Combine(hellgateDir, "demon_cum_000");
        }

        internal static string GetFin1VideoPath()
        {
            string baseDir = GetSourcesDirectory();
            string hellgateDir = Path.Combine(baseDir, "HellGate_sources");
            return Path.Combine(hellgateDir, "CUM_inside_normal");
        }

        internal static Transform GetTrackingTarget(bool followPlayer)
        {
            if (followPlayer)
            {
                var player = GameObject.FindWithTag("Player");
                return player != null ? player.transform : null;
            }

            return null;
        }

        internal static RectTransform VideoRect => videoRect;

        private static void EnsureVideoUI()
        {
            if (videoCanvas != null && videoImage != null)
            {
                return;
            }

            videoCanvas = new GameObject("MutudeVideoCanvas");
            var canvas = videoCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 33000;

            var scaler = videoCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var panelObj = new GameObject("MutudeVideoPanel");
            panelObj.transform.SetParent(videoCanvas.transform, false);
            videoRect = panelObj.AddComponent<RectTransform>();
            videoRect.anchorMin = new Vector2(0f, 1f);
            videoRect.anchorMax = new Vector2(0f, 1f);
            videoRect.pivot = new Vector2(0f, 1f);
            videoRect.sizeDelta = new Vector2(240f, 270f);
            videoRect.anchoredPosition = Vector2.zero;

            videoImage = panelObj.AddComponent<RawImage>();
            videoImage.raycastTarget = false;
            videoImage.color = Color.white;

            videoTracker = videoCanvas.AddComponent<MutudeVideoPositionTracker>();
            videoTracker.Initialize(videoRect);
            videoCanvas.SetActive(false);
        }

        private static void ApplyVideoTransform(Vector2 anchoredPosition, Vector2 size, float rotationDegrees)
        {
            videoCanvas.SetActive(true);
            videoImage.enabled = true;

            if (videoRect != null)
            {
                videoRect.sizeDelta = size;
                videoRect.anchoredPosition = anchoredPosition;
                videoRect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            }

            if (videoTracker != null)
            {
                videoTracker.SetTrackingTarget(false);
                videoTracker.DisableTracking();
                videoTracker.SetOffsets(anchoredPosition);
            }
        }

        private static List<string> BuildCandidateDirectories(string videoDirectory)
        {
            var candidates = new List<string>();
            void AddCandidate(string path)
            {
                if (!IsNullOrWhiteSpace(path))
                {
                    candidates.Add(path);
                }
            }

            AddCandidate(videoDirectory);

            string sourcesRoot = GetSourcesDirectory();
            if (!string.IsNullOrEmpty(sourcesRoot))
            {
                // For FIN1 используется CUM_inside_normal, for FIN2 - demon_cum_000
                AddCandidate(CombinePaths(sourcesRoot, "HellGate_sources", "CUM_inside_normal"));
                AddCandidate(CombinePaths(sourcesRoot, "HellGate_sources", "demon_cum_000"));
                AddCandidate(CombinePaths(sourcesRoot, "CUM_inside_normal"));
                AddCandidate(CombinePaths(sourcesRoot, "demon_cum_000"));
                AddCandidate(CombinePaths(sourcesRoot, "lowquality"));
            }

            string assemblyDirectory = null;
            try
            {
                assemblyDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(assemblyDirectory))
            {
                // For FIN1 используется CUM_inside_normal, for FIN2 - demon_cum_000
                AddCandidate(CombinePaths(assemblyDirectory, "sources", "HellGate_sources", "CUM_inside_normal"));
                AddCandidate(CombinePaths(assemblyDirectory, "sources", "HellGate_sources", "demon_cum_000"));
                AddCandidate(CombinePaths(assemblyDirectory, "HellGate_sources", "CUM_inside_normal"));
                AddCandidate(CombinePaths(assemblyDirectory, "HellGate_sources", "demon_cum_000"));
                AddCandidate(CombinePaths(assemblyDirectory, "CUM_inside_normal"));
                AddCandidate(CombinePaths(assemblyDirectory, "demon_cum_000"));
                AddCandidate(CombinePaths(assemblyDirectory, "sources", "lowquality"));
                AddCandidate(CombinePaths(assemblyDirectory, "lowquality"));
            }

            return candidates;
        }

        private static bool TryResolveFrameDirectory(IEnumerable<string> candidates, out string resolvedDirectory, out string[] resolvedFrames)
        {
            resolvedDirectory = null;
            resolvedFrames = null;

            foreach (var candidate in candidates)
            {
                try
                {
                    if (!Directory.Exists(candidate))
                    {
                        continue;
                    }

                    var frames = Directory.GetFiles(candidate, "*.png");
                    Array.Sort(frames, StringComparer.OrdinalIgnoreCase);

                    if (frames.Length == 0)
                    {
                        continue;
                    }

                    resolvedDirectory = candidate;
                    resolvedFrames = frames;
                    return true;
                }
                catch (Exception ex)
                {
                }
            }

            return false;
        }


        private static void LoadFrameTextures()
        {
            foreach (string path in framePaths)
            {
                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(data))
                    {
                        UnityEngine.Object.Destroy(tex);
                        continue;
                    }

                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    frameTextures.Add(tex);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private static void ClearFrameTextures()
        {
            if (frameTextures.Count == 0)
            {
                return;
            }

            foreach (var tex in frameTextures)
            {
                if (tex != null)
                {
                    UnityEngine.Object.Destroy(tex);
                }
            }
            frameTextures.Clear();
        }

        private static IEnumerator FramePlayback()
        {
            int frameIndex = 0;
            int totalFrames = frameTextures.Count;

            while (videoActive && frameTextures.Count > 0)
            {
                Texture2D tex = frameTextures[frameIndex];
                if (tex != null && videoImage != null)
                {
                    videoImage.texture = tex;
                }

                frameIndex++;
                
                // If одноразовое проигрывание и все кадры показаны - останавливаем
                if (playOnce && frameIndex >= totalFrames)
                {
                    videoActive = false;
                    break;
                }
                
                // Зацикливание only if not одноразовое проигрывание
                if (!playOnce)
                {
                    frameIndex = frameIndex % frameTextures.Count;
                }
                
                yield return new WaitForSecondsRealtime(FrameDuration);
            }
        }

        private static string GetSourcesDirectory()
        {
            if (!string.IsNullOrEmpty(sourcesDirectoryCache))
            {
                return sourcesDirectoryCache;
            }

            try
            {
                var candidates = new List<string>();

                string assemblyDirectory = null;
                try
                {
                    assemblyDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                catch (Exception ex)
                {
                }

                void AddCandidate(string path)
                {
                    if (!IsNullOrWhiteSpace(path))
                    {
                        candidates.Add(path);
                    }
                }

                if (!string.IsNullOrEmpty(assemblyDirectory))
                {
                    AddCandidate(CombinePaths(assemblyDirectory, "sources"));
                    AddCandidate(CombinePaths(assemblyDirectory, "HellGate_sources"));
                    AddCandidate(assemblyDirectory);
                }

               string basePath = Path.GetDirectoryName(Application.dataPath);
                if (!string.IsNullOrEmpty(basePath))
                {
                    AddCandidate(CombinePaths(basePath, "REZERVNIE COPY", "NoRHellGate3", "sources"));
                    AddCandidate(CombinePaths(basePath, "NoRHellGate3", "sources"));
                    AddCandidate(CombinePaths(basePath, "sources"));
                    AddCandidate(CombinePaths(basePath, "HellGate_sources"));
                }

                foreach (var candidate in candidates)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(candidate))
                        {
                            continue;
                        }

                        if (!Directory.Exists(candidate))
                        {
                            continue;
                        }

                        if (string.Equals(Path.GetFileName(candidate), "HellGate_sources", StringComparison.OrdinalIgnoreCase))
                        {
                            var parent = Path.GetDirectoryName(candidate);
                            sourcesDirectoryCache = string.IsNullOrEmpty(parent) ? candidate : parent;
                            return sourcesDirectoryCache;
                        }

                        string hellgateDir = CombinePaths(candidate, "HellGate_sources");
                        if (Directory.Exists(hellgateDir))
                        {
                            sourcesDirectoryCache = candidate;
                            return sourcesDirectoryCache;
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
            }

            sourcesDirectoryCache = ".";
            return sourcesDirectoryCache;
        }

        private static bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        private static string CombinePaths(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return null;
            }

            string current = null;
            foreach (var part in parts)
            {
                if (IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                current = current == null ? part : Path.Combine(current, part);
            }

            return current;
        }
    }

    internal class MutudeCoroutineRunner : MonoBehaviour
    {
        // Простой класс-наследник MonoBehaviour for starting корутин
    }
}


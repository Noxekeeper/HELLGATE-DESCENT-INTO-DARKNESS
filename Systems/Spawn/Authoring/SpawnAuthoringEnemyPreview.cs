using System;
using System.Globalization;
using NoREroMod.Patches.Enemy.BossTouzokuCustom;
using NoREroMod.Patches.Enemy.ButcherModCustom;
using NoREroMod.Patches.Enemy.DemonGorotukiModCustom;
using NoREroMod.Patches.Enemy.HellishTouzokuModCustom;
using NoREroMod.Patches.Enemy.WolfModCustom;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Small F11 enemy thumbnail: offscreen RenderTexture snapshot of a stripped prefab clone.
/// </summary>
internal static class SpawnAuthoringEnemyPreview
{
    // Portrait RT matching the tall preview panel (avoids letterboxing + tiny subject).
    private const int TexW = 280;
    private const int TexH = 320;
    private const float PreviewX = 480f;
    private const float PreviewY = 480f;
    private const float MaxBodyExtent = 5.5f;
    private const float MaxBodyDist = 4.5f;

    private static string cachedKey = string.Empty;
    private static Texture2D cachedTex;
    private static bool cachedFailed;
    private static string failReason = string.Empty;
    private static RenderTexture rt;
    private static UnityEngine.Camera previewCam;
    private static GameObject camGo;

    internal static void Clear()
    {
        cachedKey = string.Empty;
        cachedFailed = false;
        failReason = string.Empty;
        if (cachedTex != null)
        {
            Object.Destroy(cachedTex);
            cachedTex = null;
        }
    }

    internal static void Draw(Rect box, string enemyKey, bool trapMode = false)
    {
        EnsurePipeline();

        if (string.IsNullOrEmpty(enemyKey))
        {
            DrawPlaceholder(box, SpawnAuthoringLoc.T(trapMode ? "preview.emptyTrap" : "preview.empty"));
            return;
        }

        if (!trapMode && SpawnAuthoringEnemyCatalog.IsUnavailable(enemyKey))
        {
            DrawPlaceholder(box, SpawnAuthoringLoc.T("preview.unavailable"));
            return;
        }

        string cacheId;
        if (trapMode)
        {
            bool bakeRot = SpawnAuthoringState.KeyAllowsRotation(enemyKey);
            cacheId = "T:" + enemyKey + ":r" +
                      (bakeRot
                          ? Mathf.RoundToInt(SpawnAuthoringState.PendingRotationZ).ToString(CultureInfo.InvariantCulture)
                          : "0") +
                      (SpawnAuthoringState.PendingFlipX ? ":f" : "");
        }
        else
        {
            cacheId = "E:" + enemyKey;
        }
        if (!string.Equals(cachedKey, cacheId, StringComparison.OrdinalIgnoreCase))
            Bake(enemyKey, trapMode, cacheId);

        Color prev = GUI.color;
        GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);

        if (cachedTex != null)
        {
            GUI.color = Color.white;
            // Stretch to panel — RT aspect already matches the tall preview box.
            GUI.DrawTexture(Inset(box, 2f), cachedTex, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.color = Color.white;
            string msg = cachedFailed
                ? (string.IsNullOrEmpty(failReason)
                    ? SpawnAuthoringLoc.T("preview.missing")
                    : failReason)
                : "…";
            DrawPlaceholder(box, msg);
        }

        GUI.color = new Color(1f, 0.55f, 0.1f, 0.85f);
        GUI.DrawTexture(new Rect(box.x, box.yMax - 2f, box.width, 2f), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void DrawPlaceholder(Rect box, string text)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            fontSize = 12
        };
        s.normal.textColor = new Color(0.75f, 0.73f, 0.7f, 1f);
        GUI.Label(Inset(box, 6f), text ?? string.Empty, s);
    }

    private static void Bake(string key, bool trapMode, string cacheId)
    {
        cachedKey = cacheId ?? string.Empty;
        cachedFailed = false;
        failReason = string.Empty;
        if (cachedTex != null)
        {
            Object.Destroy(cachedTex);
            cachedTex = null;
        }

        if (string.IsNullOrEmpty(key))
            return;

        GameObject prefab = null;
        if (trapMode)
        {
            if (!SpawnTemplateCatalog.TryGetTrapTemplate(key, out prefab) || prefab == null)
            {
                cachedFailed = true;
                failReason = SpawnAuthoringLoc.T("preview.missing");
                return;
            }
        }
        else
        {
            EnemyPrefabRegistry.Initialize();
            if (!EnemyPrefabRegistry.TryGetPrefab(key, out prefab) || prefab == null)
            {
                cachedFailed = true;
                failReason = SpawnAuthoringLoc.T("preview.missing");
                return;
            }
        }

        GameObject clone = null;
        try
        {
            EnsurePipeline();
            clone = Object.Instantiate(prefab);
            clone.name = "HG_AuthoringPreview_" + key;
            clone.SetActive(true);
            clone.transform.position = new Vector3(PreviewX, PreviewY, 0f);
            clone.transform.rotation = Quaternion.identity;
            if (clone.transform.localScale.sqrMagnitude < 0.01f)
                clone.transform.localScale = Vector3.one;

            if (trapMode)
            {
                if (SpawnAuthoringState.KeyAllowsRotation(key))
                    SpawnRotationUtility.ApplyAuthoringRotation(clone, SpawnAuthoringState.PendingRotationZ);
                if (SpawnAuthoringState.PendingFlipX)
                    SpawnFlipUtility.ApplyAuthoringFlip(clone, true);
            }

            if (!trapMode)
            {
                ApplyKeyVisuals(key, clone);
                SpawnConfigExecutor.PrepareSpawnedEnemyPresentation(clone);
                ForceSpinePose(clone);
            }

            StripGameplay(clone);
            ReenableVisuals(clone);
            if (!trapMode)
                ForceSpinePose(clone);

            FitCamera(clone);

            previewCam.targetTexture = rt;
            previewCam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var shot = new Texture2D(TexW, TexH, TextureFormat.RGB24, false);
            shot.filterMode = FilterMode.Bilinear;
            shot.ReadPixels(new Rect(0, 0, TexW, TexH), 0, 0);
            shot.Apply(false, false);
            RenderTexture.active = prev;
            previewCam.targetTexture = null;

            if (IsMostlyEmpty(shot))
            {
                Object.Destroy(shot);
                if (TryAtlasFallback(clone, out Texture2D atlasTex))
                {
                    cachedTex = atlasTex;
                }
                else
                {
                    cachedFailed = true;
                    failReason = SpawnAuthoringLoc.T("preview.blank");
                }
            }
            else
            {
                cachedTex = shot;
            }
        }
        catch (Exception ex)
        {
            cachedFailed = true;
            failReason = SpawnAuthoringLoc.T("preview.missing");
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Preview bake failed (" + key + "): " + ex.Message);
        }
        finally
        {
            if (clone != null)
                Object.Destroy(clone);
        }
    }

    private static void ApplyKeyVisuals(string key, GameObject spawned)
    {
        if (spawned == null || string.IsNullOrEmpty(key))
            return;

        try
        {
            if (string.Equals(key, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(key, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase))
                    BossTouzokuCustomRuntime.PrepareSpawnedInstance(spawned);
                else
                    HellishTouzokuSkeletonLoader.ApplySkeletons(spawned, HellishTouzokuVariant.Boss);
            }
            else if (string.Equals(key, "Butcher", StringComparison.OrdinalIgnoreCase))
            {
                spawned.name = "Butcher";
                ButcherFatalityLoader.ApplyButcherFatality(spawned);
            }
            else if (string.Equals(key, "Wolf", StringComparison.OrdinalIgnoreCase))
            {
                WolfSkeletonLoader.ApplyWolfSkeletons(spawned);
            }
            else if (string.Equals(key, DemonGorotukiSkeletonLoader.SpawnKey, StringComparison.OrdinalIgnoreCase))
            {
                DemonGorotukiSkeletonLoader.ApplySkeletons(spawned);
            }
            else if (string.Equals(key, "HellishTouzokuAxe", StringComparison.OrdinalIgnoreCase))
            {
                HellishTouzokuSkeletonLoader.ApplySkeletons(spawned, HellishTouzokuVariant.Axe);
            }
            else if (string.Equals(key, "HellishTouzokuSword", StringComparison.OrdinalIgnoreCase))
            {
                HellishTouzokuSkeletonLoader.ApplySkeletons(spawned, HellishTouzokuVariant.Sword);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Preview visuals (" + key + "): " + ex.Message);
        }
    }

    private static void StripGameplay(GameObject root)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null)
                continue;
            if (mb is SkeletonAnimation)
                continue;
            // Keep Spine mesh updater types if present as separate components.
            string tn = mb.GetType().Name;
            if (tn.IndexOf("Skeleton", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            mb.enabled = false;
        }

        Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null)
                continue;
            bodies[i].isKinematic = true;
            bodies[i].velocity = Vector2.zero;
            bodies[i].angularVelocity = 0f;
        }

        Collider2D[] cols = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }

        AudioSource[] audio = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audio.Length; i++)
        {
            if (audio[i] != null)
                audio[i].enabled = false;
        }
    }

    private static void ReenableVisuals(GameObject root)
    {
        MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] == null)
                continue;
            if (meshes[i].gameObject.name != null
                && meshes[i].gameObject.name.IndexOf("ero", StringComparison.OrdinalIgnoreCase) >= 0
                && meshes[i].gameObject != root)
                continue;
            meshes[i].enabled = true;
            if (!meshes[i].gameObject.activeSelf)
                meshes[i].gameObject.SetActive(true);
        }

        SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                sprites[i].enabled = true;
        }
    }

    private static void ForceSpinePose(GameObject root)
    {
        SkeletonAnimation[] spines = root.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null)
                continue;
            if (spine.gameObject != null
                && spine.gameObject.name != null
                && spine.gameObject.name.IndexOf("ero", StringComparison.OrdinalIgnoreCase) >= 0
                && spine.gameObject != root)
            {
                spine.gameObject.SetActive(false);
                continue;
            }

            try
            {
                if (!spine.gameObject.activeSelf)
                    spine.gameObject.SetActive(true);
                spine.enabled = true;
                spine.Initialize(true);
                if (spine.state != null
                    && spine.state.GetCurrent(0) == null
                    && !string.IsNullOrEmpty(spine.AnimationName))
                {
                    spine.state.SetAnimation(0, spine.AnimationName, true);
                }

                spine.Update(0f);
                spine.LateUpdate();
            }
            catch
            {
            }
        }
    }

    private static void FitCamera(GameObject root)
    {
        float aspect = TexW / (float)TexH;
        previewCam.aspect = aspect;

        Bounds bounds;
        if (!TryGetBodyBounds(root, out bounds) || bounds.size.sqrMagnitude < 0.0001f)
        {
            previewCam.orthographicSize = 1.35f;
            previewCam.transform.position = new Vector3(PreviewX, PreviewY, -10f);
            return;
        }

        // Ortho size = half-height of view. Also fit width via aspect.
        float pad = 1.06f;
        float halfH = Mathf.Max(0.01f, bounds.extents.y) * pad;
        float halfW = Mathf.Max(0.01f, bounds.extents.x) * pad;
        float size = Mathf.Max(halfH, halfW / aspect);

        // Pull in a bit — previous framing left characters tiny.
        size *= 0.88f;
        if (size < 0.55f)
            size = 0.55f;
        if (size > 6.5f)
            size = 6.5f;

        previewCam.orthographicSize = size;
        Vector3 c = bounds.center;
        previewCam.transform.position = new Vector3(c.x, c.y, -10f);
    }

    /// <summary>
    /// Bounds of the combat body only — ignore huge FX / distant / ero meshes that blow out framing.
    /// </summary>
    private static bool TryGetBodyBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(root.transform.position, Vector3.zero);
        Vector3 origin = root.transform.position;
        bool any = false;
        float bestArea = -1f;
        Bounds best = bounds;

        MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshes.Length; i++)
        {
            MeshRenderer mr = meshes[i];
            if (mr == null || !mr.enabled)
                continue;
            string n = mr.gameObject != null ? mr.gameObject.name : null;
            if (n != null && n.IndexOf("ero", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            Bounds b = mr.bounds;
            if (!IsPlausibleBodyBounds(b, origin))
                continue;

            float area = b.size.x * b.size.y;
            if (area > bestArea)
            {
                bestArea = area;
                best = b;
            }

            if (!any)
            {
                bounds = b;
                any = true;
            }
            else
                bounds.Encapsulate(b);
        }

        SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null || !sr.enabled || sr.sprite == null)
                continue;

            Bounds b = sr.bounds;
            if (!IsPlausibleBodyBounds(b, origin))
                continue;

            float area = b.size.x * b.size.y;
            if (area > bestArea)
            {
                bestArea = area;
                best = b;
            }

            if (!any)
            {
                bounds = b;
                any = true;
            }
            else
                bounds.Encapsulate(b);
        }

        // Prefer the single largest body mesh when the union is still huge (weapons/FX slipped in).
        if (any && bestArea > 0f)
        {
            float unionArea = bounds.size.x * bounds.size.y;
            if (unionArea > bestArea * 2.4f)
                bounds = best;
        }

        return any;
    }

    private static bool IsPlausibleBodyBounds(Bounds b, Vector3 origin)
    {
        if (b.extents.x > MaxBodyExtent || b.extents.y > MaxBodyExtent)
            return false;
        if (b.size.sqrMagnitude < 0.0004f)
            return false;
        Vector2 d = new Vector2(b.center.x - origin.x, b.center.y - origin.y);
        if (d.magnitude > MaxBodyDist)
            return false;
        return true;
    }

    private static bool IsMostlyEmpty(Texture2D tex)
    {
        if (tex == null)
            return true;

        Color32[] px = tex.GetPixels32();
        if (px == null || px.Length == 0)
            return true;

        int lit = 0;
        int step = Mathf.Max(1, px.Length / 400);
        for (int i = 0; i < px.Length; i += step)
        {
            Color32 c = px[i];
            if (c.r > 18 || c.g > 18 || c.b > 18)
                lit++;
        }

        int samples = (px.Length + step - 1) / step;
        return lit < Mathf.Max(3, samples / 40);
    }

    private static bool TryAtlasFallback(GameObject root, out Texture2D tex)
    {
        tex = null;
        SkeletonAnimation[] spines = root.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null || spine.SkeletonDataAsset == null)
                continue;
            if (spine.gameObject != null
                && spine.gameObject.name != null
                && spine.gameObject.name.IndexOf("ero", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            try
            {
                var atlases = spine.SkeletonDataAsset.atlasAssets;
                if (atlases == null || atlases.Length == 0 || atlases[0] == null)
                    continue;
                if (atlases[0].materials == null || atlases[0].materials.Length == 0 || atlases[0].materials[0] == null)
                    continue;
                Texture main = atlases[0].materials[0].mainTexture;
                if (main == null)
                    continue;

                tex = new Texture2D(TexW, TexH, TextureFormat.RGB24, false);
                // Can't reliably blit arbitrary Texture → copy via RenderTexture if possible.
                var tmp = RenderTexture.GetTemporary(TexW, TexH, 0);
                Graphics.Blit(main, tmp);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = tmp;
                tex.ReadPixels(new Rect(0, 0, TexW, TexH), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(tmp);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static void EnsurePipeline()
    {
        if (rt != null && previewCam != null && rt.width == TexW && rt.height == TexH)
            return;

        if (rt != null)
        {
            rt.Release();
            Object.Destroy(rt);
            rt = null;
        }

        rt = new RenderTexture(TexW, TexH, 16, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();

        if (camGo == null || previewCam == null)
        {
            camGo = new GameObject("HellGate_SpawnAuthoringPreviewCam");
            Object.DontDestroyOnLoad(camGo);
            camGo.hideFlags = HideFlags.HideAndDontSave;
            previewCam = camGo.AddComponent<UnityEngine.Camera>();
        }

        previewCam.orthographic = true;
        previewCam.orthographicSize = 1.35f;
        previewCam.aspect = TexW / (float)TexH;
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        previewCam.nearClipPlane = 0.1f;
        previewCam.farClipPlane = 50f;
        previewCam.cullingMask = ~0;
        previewCam.enabled = false;
        previewCam.allowHDR = false;
        previewCam.allowMSAA = false;
        camGo.transform.position = new Vector3(PreviewX, PreviewY, -10f);
    }

    private static Rect Inset(Rect r, float pad)
    {
        return new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f);
    }
}

using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Resolves Aradia bone, forces death, spawns fatality clip + flash + vanilla FatalityDeathIcon.</summary>
internal static class EnemyFatalityPlayback
{
    /// <summary>Same default as config: ~16 FPS.</summary>
    private const float DefaultFrameSeconds = 0.0625f;

    private static GameObject _vanillaFatalityIconPrefab;
    private static bool _vanillaIconLookupDone;
    private static GameObject _activeFatalityIcon;

    // Prefer High Inquisition — Slaughterer's FatalityIcon may be swapped to Rick skull by HellGate.
    private static readonly string[] VanillaIconPrefabKeys =
    {
        "HighInquisitionFemale",
        "RequiemKnight",
        "Candore",
        "Sheepheaddemon",
    };

    /// <summary>
    /// FATALITY logo sits on the same screen point as the QTE WASD row center
    /// (<see cref="Plugin.qteButtonPositionX"/> / <see cref="Plugin.qteButtonPositionY"/>).
    /// </summary>
    internal static void Trigger(
        playercon player,
        EnemyDate enemy,
        IEnemyFatalityProfile profile,
        string clipRelative = null)
    {
        if (profile == null || !profile.IsEnabled || player == null)
            return;

        if (EnemyFatalitySession.IsActive)
            return;

        // No mid-air fatalities — clip has no reliable ground settle; use normal death instead.
        if (IsPlayerAirborne(player))
        {
            EnemyFatalityConfig.LogDebug("[" + profile.Id + "] Trigger skipped — airborne");
            return;
        }

        string clipRel = clipRelative;
        if (string.IsNullOrEmpty(clipRel))
            clipRel = EnemyFatalityClipPicker.PickRelative(profile, 0f);

        if (string.IsNullOrEmpty(clipRel))
        {
            Plugin.Log?.LogWarning("[" + profile.Id + "] No eligible clip — fatality skipped.");
            return;
        }

        Sprite[] frames = EnemyFatalitySpriteCache.GetFramesForRelative(clipRel, profile.Id);
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning(
                "[" + profile.Id + "] No PNG frames — fatality skipped"
                + " (" + clipRel + ")");
            return;
        }

        // Capture facing + bone BEFORE ForceVanillaDeath (death may reset dir / pose).
        // Clip art is authored facing right: no flip when Aradia looks right; flipX when she looks left.
        bool flipForFacingLeft = ResolveFlipForPlayerFacing(player);
        Vector3 start = ResolvePlayerBoneWorld(player, profile);
        start.y += profile.ClipOffsetY;
        // Facing-relative X: + forward / − backward along look direction (mirrored when left).
        float facingSign = flipForFacingLeft ? -1f : 1f;
        start.x += profile.ClipOffsetAlongFacing * facingSign;
        start.z = player.transform.position.z;
        start = ClampClipSpawnAbovePlayerRoot(player, start);
        string layer;
        int order;
        ResolveSorting(player, profile, out layer, out order);

        bool muteVanillaDeath1 = EnemyFatalityAudio.IsHeavyCriticalClip(clipRel);
        if (muteVanillaDeath1)
            EnemyFatalityVanillaDeathMute.BeginHeavyCritical();

        ForceVanillaDeath(player, playVanillaDeath1: !muteVanillaDeath1);

        EnemyFatalitySession.EnsureSceneHook();
        EnemyFatalitySession.Begin(clipRel);
        EnemyFatalityEroSuppression.PinPlayerBody(player);
        EnemyFatalityEroSuppression.SettleEnemiesOnce();
        EnemyFatalityEroSuppression.HideKillerIfNeeded(enemy, profile);

        // FatalityDeathIcon — QTE row screen point (−70px), held until respawn.
        SpawnVanillaFatalityIcon(player);

        // Scarlet UI triple-blink at fatality start.
        if (profile.ScarletFlashEnable)
            EnemyFatalityFlash.PlayScarletTripleBlink();

        EnemyFatalityAudio.PlayHitThenDeath(profile, clipRel);

        float frameSec = profile.FrameSeconds > 0.01f ? profile.FrameSeconds : DefaultFrameSeconds;
        float fall = profile.FallDistance;
        float fallSpeed = profile.FallSpeedMultiplier;

        bool slowMo = profile.SlowMoEnable;
        float slowScale = profile.SlowMoTimeScale;
        float slowDur = profile.SlowMoDurationSeconds;
        int slowFrame = profile.SlowMoStartFrame;

        EnemyFatalityClipPlayer.Spawn(
            player,
            profile,
            start,
            frames,
            flipForFacingLeft,
            frameSec,
            fall,
            order,
            layer,
            fallSpeed,
            slowMo,
            slowScale,
            slowDur,
            slowFrame);

        Plugin.Log?.LogInfo(
            "[" + profile.Id + "] Fatality triggered — frames="
            + frames.Length
            + " clip="
            + (string.IsNullOrEmpty(clipRel) ? "?" : clipRel)
            + " enemy="
            + (enemy != null ? enemy.GetType().Name : "?")
            + " bone="
            + profile.BoneName
            + " facingLeft="
            + flipForFacingLeft
            + " facingX="
            + profile.ClipOffsetAlongFacing.ToString("0.###")
            + " frameSec="
            + frameSec.ToString("0.###")
            + " fall="
            + fall);
    }

    /// <summary>
    /// Airborne check used to block mid-air fatalities.
    /// Uses !m_Grounded, jumpfrag / dashjumpfrag, or player.state containing JUMP / FALL
    /// (covers JUMP, FALL, FALLSTART).
    /// </summary>
    internal static bool IsPlayerAirborne(playercon player)
    {
        if (player == null)
            return false;

        if (!player.m_Grounded)
            return true;

        if (player.jumpfrag)
            return true;

        try
        {
            if (Traverse.Create(player).Field("dashjumpfrag").GetValue<bool>())
                return true;
        }
        catch
        {
        }

        string state = player.state;
        if (!string.IsNullOrEmpty(state))
        {
            if (state.IndexOf("JUMP", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (state.IndexOf("FALL", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Spawns vanilla FatalityDeathIcon at the QTE button-row screen point
    /// (−70px Y) and holds it until Take Vengeance / Death_flag.
    /// ParticleSystemDestroyer is removed so it does not auto-despawn.
    /// </summary>
    private static void SpawnVanillaFatalityIcon(playercon player)
    {
        try
        {
            DestroyActiveFatalityIcon();

            GameObject iconPrefab = ResolveVanillaFatalityIconPrefab();
            if (iconPrefab == null)
            {
                Plugin.Log?.LogWarning(
                    "[EnemyFatality] Vanilla FatalityIcon not found (need HighInquisitionFemale prefab cached)");
                return;
            }

            // Skip if somehow pointing at Rick overlay template.
            if (iconPrefab.GetComponent<NoREroMod.Patches.Enemy.RickEnemyModShared.RickEnemyModFatalityLogoMarker>() != null
                || iconPrefab.GetComponent<NoREroMod.Patches.Enemy.RickEnemyModShared.RickEnemyModFatalityLogoPlayer>() != null)
            {
                Plugin.Log?.LogWarning(
                    "[EnemyFatality] FatalityIcon was Rick logo — refusing. Prefer HighInquisitionFemale prefab.");
                _vanillaFatalityIconPrefab = null;
                _vanillaIconLookupDone = false;
                return;
            }

            Vector3 pos = ResolveFatalityIconWorldPos(player);

            // Instantiate inactive so ParticleSystemDestroyer cannot start before we strip it.
            GameObject spawned = Object.Instantiate(iconPrefab, pos, Quaternion.identity);
            spawned.SetActive(false);
            spawned.name = "HellGate_VanillaFatalityDeathIcon";

            DisableAutoDestroy(spawned);

            SkeletonAnimation spine = spawned.GetComponent<SkeletonAnimation>();
            if (spine != null)
            {
                if (!spine.valid)
                    spine.Initialize(true);
                // Prefab default timeScale is 0.6; freeze before START alpha fade-out.
                spine.timeScale = 0.6f;
            }

            MeshRenderer mesh = spawned.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.enabled = true;

            ApplyFatalityIconDrawOrder(spawned, player);

            spawned.AddComponent<EnemyFatalityIconHold>().Bind(player);

            spawned.SetActive(true);
            if (spine != null && spine.state != null)
                spine.state.SetAnimation(0, "START", false);

            _activeFatalityIcon = spawned;

            Plugin.Log?.LogInfo(
                "[EnemyFatality] Vanilla FatalityDeathIcon spawned at "
                + pos.x.ToString("0.##")
                + ","
                + pos.y.ToString("0.##")
                + " (QTE row coords, hold until respawn)");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] Vanilla FatalityIcon spawn failed: " + ex.Message);
        }
    }

    internal static void DestroyActiveFatalityIcon()
    {
        if (_activeFatalityIcon != null)
        {
            Object.Destroy(_activeFatalityIcon);
            _activeFatalityIcon = null;
        }
    }

    /// <summary>
    /// Same point as the QTE WASD row. QTE is Screen Space UI — we map its
    /// RectTransform through the gameplay camera for the Spine MeshRenderer.
    /// </summary>
    internal static Vector3 ResolveFatalityIconWorldPos(playercon player)
    {
        return QTESystem.GetButtonRowCenterWorldPosition(player);
    }

    /// <summary>
    /// Keep FATALITY logo above the PNG clip / enemies / death FX.
    /// World Spine MeshRenderer — not Screen Space UI (taunt/flash use Canvas).
    /// </summary>
    private const int FatalityIconSortingOrder = 8000;

    private static readonly string[] FatalityIconSortingLayerCandidates =
    {
        "UI",
        "Foreground",
        "Front",
        "Default",
    };

    internal static void ApplyFatalityIconDrawOrder(GameObject icon, playercon player)
    {
        if (icon == null)
            return;

        // Pull slightly toward the camera so depth/Z sort does not bury the logo
        // behind the fatality clip or body mesh.
        try
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null && player != null)
                cam = player.GetComponentInChildren<UnityEngine.Camera>();
            if (cam != null)
            {
                Vector3 pos = icon.transform.position;
                Vector3 camPos = cam.transform.position;
                Vector3 toCam = camPos - pos;
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    toCam.Normalize();
                    pos += toCam * 1.5f;
                    icon.transform.position = pos;
                }
            }
        }
        catch
        {
        }

        string layer = ResolvePreferredSortingLayer(FatalityIconSortingLayerCandidates);

        Renderer[] renderers = icon.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!string.IsNullOrEmpty(layer))
                r.sortingLayerName = layer;
            if (r.sortingOrder < FatalityIconSortingOrder)
                r.sortingOrder = FatalityIconSortingOrder;
        }
    }

    private static string ResolvePreferredSortingLayer(string[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
            return "Default";

        SortingLayer[] layers = SortingLayer.layers;
        for (int c = 0; c < candidates.Length; c++)
        {
            string name = candidates[c];
            if (string.IsNullOrEmpty(name))
                continue;

            for (int i = 0; i < layers.Length; i++)
            {
                if (string.Equals(layers[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                    return layers[i].name;
            }
        }

        return layers != null && layers.Length > 0
            ? layers[layers.Length - 1].name
            : "Default";
    }

    private static void DisableAutoDestroy(GameObject spawned)
    {
        if (spawned == null)
            return;

        MonoBehaviour[] behaviours = spawned.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];
            if (b == null)
                continue;

            // Strip ParticleSystemDestroyer (and any Rick logo self-destruct helper if present).
            string typeName = b.GetType().Name;
            if (typeName.IndexOf("ParticleSystemDestroyer", System.StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("FatalityLogoPlayer", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                b.enabled = false;
                Object.DestroyImmediate(b);
            }
        }
    }

    private static GameObject ResolveVanillaFatalityIconPrefab()
    {
        if (_vanillaFatalityIconPrefab != null)
            return _vanillaFatalityIconPrefab;

        if (_vanillaIconLookupDone)
            return null;

        _vanillaIconLookupDone = true;

        try
        {
            EnemyPrefabRegistry.Initialize();
        }
        catch
        {
        }

        for (int i = 0; i < VanillaIconPrefabKeys.Length; i++)
        {
            GameObject enemyPrefab = EnemyPrefabRegistry.GetPrefab(VanillaIconPrefabKeys[i]);
            GameObject icon = ReadFatalityIconField(enemyPrefab);
            if (icon != null)
            {
                _vanillaFatalityIconPrefab = icon;
                Plugin.Log?.LogInfo(
                    "[EnemyFatality] Cached vanilla FatalityIcon from "
                    + VanillaIconPrefabKeys[i]
                    + " ("
                    + icon.name
                    + ")");
                return _vanillaFatalityIconPrefab;
            }
        }

        // Live scene fallback (prefab registry may not be warm yet).
        HighInquisition_famale hi = Object.FindObjectOfType<HighInquisition_famale>();
        if (hi != null)
        {
            GameObject icon = ReadFatalityIconField(hi.gameObject);
            if (icon == null)
            {
                FieldInfo field = AccessTools.Field(typeof(HighInquisition_famale), "FatalityIcon");
                if (field != null)
                    icon = field.GetValue(hi) as GameObject;
            }

            if (icon != null)
            {
                _vanillaFatalityIconPrefab = icon;
                Plugin.Log?.LogInfo(
                    "[EnemyFatality] Cached vanilla FatalityIcon from live HighInquisition_famale");
                return _vanillaFatalityIconPrefab;
            }
        }

        return null;
    }

    private static GameObject ReadFatalityIconField(GameObject enemyRoot)
    {
        if (enemyRoot == null)
            return null;

        Component[] comps = enemyRoot.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            Component c = comps[i];
            if (c == null)
                continue;

            FieldInfo field = AccessTools.Field(c.GetType(), "FatalityIcon");
            if (field == null)
                continue;

            GameObject icon = field.GetValue(c) as GameObject;
            if (icon != null)
                return icon;
        }

        return null;
    }

    private static void ForceVanillaDeath(playercon player, bool playVanillaDeath1 = true)
    {
        PlayerStatus status =
            Traverse.Create(player).Field("playerstatus").GetValue<PlayerStatus>();

        if (status != null)
            status.Hp = 0f;

        // Lethal-trap style death: Death + restart menu, no erodown (avoids EROWALK).
        player.erodown = 0;
        player.nowdamage = false;
        player.eroflag = false;

        Traverse.Create(player).Field("Death").SetValue(true);
        Traverse.Create(player).Field("tough").SetValue(-999f);
        player.state = "IDLE";

        if (status != null)
        {
            status.REstart_menu();
            status._SOUSA = false;
            status._SOUSAMNG = false;
        }

        GameObject magicCanvas =
            Traverse.Create(player).Field("MagicSpellCanvas").GetValue<GameObject>();
        if (magicCanvas != null)
            magicCanvas.SetActive(false);

        try
        {
            player.CancelInvoke("timescale");
            player.CancelInvoke("colarrcovery");
        }
        catch
        {
        }

        // Vanilla lethal hit leaves Time.timeScale at ~0.25 and schedules Invoke("timescale").
        // Cancel alone is not enough if scale is already low — clip uses Time.deltaTime.
        if (Time.timeScale < 0.99f)
            Time.timeScale = 1f;

        // Clear damage-red immediately (vanilla sets Color.red on the lethal hit).
        try
        {
            SkeletonAnimation spine = Traverse.Create(player)
                .Field("spineanime")
                .GetValue<SkeletonAnimation>();
            if (spine == null)
                spine = player.GetComponentInChildren<SkeletonAnimation>(true);
            if (spine != null && spine.skeleton != null)
                spine.skeleton.SetColor(Color.white);
        }
        catch
        {
        }

        // Vanilla lethal cue (same bus as fun_damage death path).
        // HeavyCritical uses Quick Death WAVs instead — skip / already muted.
        if (!playVanillaDeath1)
            return;

        try
        {
            DarkTonic.MasterAudio.MasterAudio.PlaySound(
                "death1", 1f, null, 1f, null, false, false);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Prone / recovery poses can put the spine body bone under the floor.
    /// Keep the PNG at least slightly above the player root.
    /// </summary>
    private static Vector3 ClampClipSpawnAbovePlayerRoot(playercon player, Vector3 start)
    {
        if (player == null)
            return start;

        const float minHeightAboveRoot = 0.75f;
        float minY = player.transform.position.y + minHeightAboveRoot;
        if (start.y < minY)
            start.y = minY;
        return start;
    }

    /// <summary>
    /// Clip PNGs are authored facing right.
    /// Returns true (= SpriteRenderer.flipX) when Aradia was facing left at death.
    /// Prefers <c>playercon.dir</c> (1 right / -1 left); also checks transform / spine scale.
    /// </summary>
    private static bool ResolveFlipForPlayerFacing(playercon player)
    {
        if (player == null)
            return false;

        float dir = player.dir;
        if (Mathf.Abs(dir) > 0.01f)
            return dir < 0f;

        float scaleX = player.transform.localScale.x;
        if (Mathf.Abs(scaleX) > 0.01f)
            return scaleX < 0f;

        try
        {
            SkeletonAnimation spine = player.GetComponentInChildren<SkeletonAnimation>();
            if (spine != null && spine.transform != null)
            {
                float sx = spine.transform.lossyScale.x;
                if (Mathf.Abs(sx) > 0.01f)
                    return sx < 0f;
            }
        }
        catch
        {
        }

        return false;
    }

    private static void ResolveSorting(playercon player, IEnemyFatalityProfile profile, out string layerName, out int order)
    {
        layerName = "Default";
        order = profile != null ? profile.SortingOrder : 80;

        try
        {
            MeshRenderer mesh = player.GetComponent<MeshRenderer>();
            if (mesh == null)
                mesh = player.GetComponentInChildren<MeshRenderer>();
            if (mesh != null)
            {
                if (!string.IsNullOrEmpty(mesh.sortingLayerName))
                    layerName = mesh.sortingLayerName;
                order = mesh.sortingOrder + 20;
            }
        }
        catch
        {
        }
    }

    private static Vector3 ResolvePlayerBoneWorld(playercon player, IEnemyFatalityProfile profile)
    {
        Vector3 fallback = player.transform.position;
        try
        {
            SkeletonAnimation spine = Traverse.Create(player)
                .Field("spineanime")
                .GetValue<SkeletonAnimation>();
            if (spine == null)
                spine = player.GetComponent<SkeletonAnimation>();
            if (spine == null)
                spine = player.GetComponentInChildren<SkeletonAnimation>(true);
            if (spine == null || spine.skeleton == null)
                return fallback;

            spine.skeleton.UpdateWorldTransform();

            string boneName = profile != null ? profile.BoneName : "body";
            if (string.IsNullOrEmpty(boneName))
                boneName = "body";

            Bone bone = spine.skeleton.FindBone(boneName);
            if (bone == null)
            {
                Plugin.Log?.LogWarning(
                    "[EnemyFatality] Bone not found: " + boneName + " — using player root");
                return fallback;
            }

            return spine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] Bone resolve failed: " + ex.Message);
            return fallback;
        }
    }
}

/// <summary>
/// FatalityDeath START fades slot alpha to 0 near the end of the clip.
/// Freeze just before that so the icon stays visible until respawn cleanup.
/// Also re-pins world position each frame (screen-pixel placement vs camera).
/// </summary>
internal sealed class EnemyFatalityIconHold : MonoBehaviour
{
    /// <summary>Animation time just before out-fade keys (logo still fully opaque).</summary>
    private const float FreezeTrackTime = 1.25f;

    private SkeletonAnimation _spine;
    private playercon _player;
    private bool _frozen;

    internal void Bind(playercon player)
    {
        _player = player;
    }

    private void Awake()
    {
        _spine = GetComponent<SkeletonAnimation>();
    }

    private void LateUpdate()
    {
        // Keep logo on the intended screen band even if the camera moves.
        transform.position = EnemyFatalityPlayback.ResolveFatalityIconWorldPos(_player);
        EnemyFatalityPlayback.ApplyFatalityIconDrawOrder(gameObject, _player);

        if (_frozen || _spine == null || _spine.state == null)
            return;

        Spine.TrackEntry entry = _spine.state.GetCurrent(0);
        if (entry == null)
            return;

        if (entry.Animation == null ||
            !string.Equals(entry.Animation.Name, "START", System.StringComparison.Ordinal))
            return;

        if (entry.Time < FreezeTrackTime)
            return;

        entry.Time = FreezeTrackTime;
        entry.TimeScale = 0f;
        _spine.Update(0f);
        _spine.LateUpdate();
        _frozen = true;
    }
}

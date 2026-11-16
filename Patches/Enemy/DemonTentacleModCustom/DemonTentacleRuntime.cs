using System;
using System.Reflection;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Patches;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.DemonTentacleModCustom;

/// <summary>Identifies a DemonTentacle clone (survives renames) and holds per-instance hit state.</summary>
internal sealed class DemonTentacleMarker : MonoBehaviour
{
    internal SkeletonAnimation BattleSpine;
    internal int LastHitColliderId;
    internal float LastHitTime = -1f;
    internal float FlashUntil;
    internal Color BaseColor = Color.white;

    private void Update()
    {
        if (FlashUntil <= 0f || Time.time < FlashUntil)
            return;

        FlashUntil = 0f;
        BattleSpine?.skeleton?.SetColor(BaseColor);
    }

    /// <summary>Tint shown between hit flashes (e.g. Combat AI enrage).</summary>
    internal void SetBaseColor(Color color)
    {
        if (BaseColor == color)
            return;

        BaseColor = color;
        if (FlashUntil <= 0f)
            BattleSpine?.skeleton?.SetColor(color);
    }
}

/// <summary>
/// DemonTentacle behaviour on top of vanilla AngelStatue: config HP and regular weapon/magic damage
/// instead of the vanilla "any hit kills" trigger. Lethal hits fall through to the vanilla death path.
/// </summary>
internal static class DemonTentacleRuntime
{
    private const float SameColliderHitCooldown = 0.1f;
    private const float HitFlashDuration = 0.05f;

    private static readonly MethodInfo DamagePopMethod = AccessTools.Method(typeof(EnemyDate), "DamagePOP_fun", new[] { typeof(float) });
    private static readonly FieldInfo DifficultyField = AccessTools.Field(typeof(EnemyDate), "GameDifficultyFlag");
    private static readonly FieldInfo PlayerStatusField = AccessTools.Field(typeof(EnemyDate), "playerstatus");
    private static readonly FieldInfo WeaponResistField = AccessTools.Field(typeof(EnemyDate), "WeaponResist");
    private static readonly FieldInfo ElementalResistField = AccessTools.Field(typeof(EnemyDate), "ElementalResist");

    private static readonly FieldInfo BulletField = AccessTools.Field(typeof(AngelStatue), "bullet2");
    private static readonly FieldInfo ParticleField = AccessTools.Field(typeof(AngelStatue), "particle");

    // bone21 is also where vanilla AngelStatue attaches the charge glow, so one bone drives glow and shot.
    private const string MuzzleBoneName = "bone21";
    private const string FallbackMuzzleBoneName = "bone26";

    private static bool _loggedStats;
    private static bool _loggedDamagePopFailure;
    private static bool _loggedMissingMuzzle;

    internal static void Register(GameObject spawned)
    {
        if (spawned == null)
            return;

        DemonTentacleMarker marker = spawned.GetComponent<DemonTentacleMarker>();
        if (marker == null)
            marker = spawned.AddComponent<DemonTentacleMarker>();
        marker.BattleSpine = DemonTentacleLoader.ResolveBattleSpine(spawned);
    }

    internal static DemonTentacleMarker GetMarker(Component enemy)
    {
        return enemy != null ? enemy.GetComponent<DemonTentacleMarker>() : null;
    }

    /// <summary>Runs from AngelStatue.Start postfix (before NoREroMod elite roll, so elite multipliers stack on top).</summary>
    internal static void ApplyStats(AngelStatue enemy)
    {
        if (GetMarker(enemy) == null)
            return;

        float vanillaHp = enemy.MaxHp;
        float maxHp = Mathf.Max(1f, Mathf.Round(DemonTentacleConfig.GetBaseMaxHp() * GetDifficultyHpFactor(enemy)));
        enemy.MaxHp = maxHp;
        enemy.Hp = maxHp;

        if (!_loggedStats)
        {
            _loggedStats = true;
            Plugin.Log?.LogInfo($"[DemonTentacle] HP {vanillaHp} -> {maxHp}");
        }
    }

    /// <summary>Same per-difficulty HP factors as EnemyDate.SetStatusGameDifficulty.</summary>
    private static float GetDifficultyHpFactor(EnemyDate enemy)
    {
        object raw = DifficultyField?.GetValue(enemy);
        int flag = raw is int i ? i : 1;
        switch (flag)
        {
            case 0: return 0.6f;
            case 3: return 1.1f;
            default: return 1f;
        }
    }

    /// <summary>
    /// AngelStatue.OnTriggerEnter2D prefix. Returns true to run vanilla (non-DemonTentacle, non-damage tags, lethal hit).
    /// </summary>
    internal static bool HandleHit(AngelStatue enemy, Collider2D col)
    {
        DemonTentacleMarker marker = GetMarker(enemy);
        if (marker == null || col == null)
            return true;

        bool weapon = col.CompareTag("ATKweapon");
        bool magicHit = !weapon && col.CompareTag("ATKmagic");
        if (!weapon && !magicHit)
            return true;

        if (enemy.Hp <= 0f)
            return true;

        // Root and child colliders can both report the same swing.
        int colliderId = col.GetInstanceID();
        if (colliderId == marker.LastHitColliderId && Time.time - marker.LastHitTime < SameColliderHitCooldown)
            return false;
        marker.LastHitColliderId = colliderId;
        marker.LastHitTime = Time.time;

        if (weapon)
            ApplyWeaponDamage(enemy);
        else
            ApplyMagicDamage(enemy, col.GetComponent<magic>());

        if (enemy.Hp <= 0f)
            return true;

        PlayHitFeedback(marker, weapon);
        return false;
    }

    /// <summary>
    /// Formula of EnemyDate.WeaponDamage. The vanilla helper is not called: it toggles ElementdamegeEffect[i],
    /// which is empty on the angel_Statue prefab (IndexOutOfRange).
    /// </summary>
    private static void ApplyWeaponDamage(AngelStatue enemy)
    {
        playercon player = enemy.com_player;
        PlayerStatus status = PlayerStatusField?.GetValue(enemy) as PlayerStatus;
        float[] weaponResist = WeaponResistField?.GetValue(enemy) as float[];
        float[] elementalResist = ElementalResistField?.GetValue(enemy) as float[];

        float atk = player != null ? player.ATK : 1f;
        int smashKind = status != null ? status._NowSmashKind : 0;
        float weaponMult = weaponResist != null && smashKind >= 0 && smashKind < weaponResist.Length ? weaponResist[smashKind] : 1f;

        float elemental = 0f;
        float[] atkElement = player?.ATKEmental();
        if (atkElement != null && elementalResist != null)
        {
            int count = Mathf.Min(4, Mathf.Min(atkElement.Length, elementalResist.Length));
            for (int i = 0; i < count; i++)
                elemental += elementalResist[i] * atkElement[i];
        }

        float num = Mathf.Floor((atk * weaponMult + elemental) * UnityEngine.Random.Range(0.98f, 1.02f));
        if (status != null && status._Criticalcheck)
            num = Mathf.Floor(num * 1.5f);

        DealDamage(enemy, num);
    }

    /// <summary>Formula of EnemyDate.MagicDamage, without the elemental hit effects (see <see cref="ApplyWeaponDamage"/>).</summary>
    private static void ApplyMagicDamage(AngelStatue enemy, magic source)
    {
        float[] damage = source?.damage;
        if (damage == null)
        {
            ApplyWeaponDamage(enemy);
            return;
        }

        float[] elementalResist = ElementalResistField?.GetValue(enemy) as float[];
        float sum = 0f;
        for (int i = 0; i < damage.Length; i++)
        {
            float resist = elementalResist != null && i < elementalResist.Length ? elementalResist[i] : 1f;
            sum += resist * damage[i];
        }

        float num = Mathf.Floor(sum * UnityEngine.Random.Range(0.98f, 1.02f));
        if (PlayerStatusField?.GetValue(enemy) is PlayerStatus status)
            status._Criticalcheck = false;

        DealDamage(enemy, num);
    }

    private static void DealDamage(AngelStatue enemy, float num)
    {
        enemy.Hp -= num;

        if (DamagePopMethod == null || enemy.damageUI == null)
            return;

        try
        {
            DamagePopMethod.Invoke(enemy, new object[] { num });
        }
        catch (Exception ex)
        {
            if (_loggedDamagePopFailure)
                return;
            _loggedDamagePopFailure = true;
            Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
            Plugin.Log?.LogWarning($"[DemonTentacle] Damage popup failed ({inner.GetType().Name}: {inner.Message}).");
        }
    }

    /// <summary>
    /// AngelStatue.OnEvent prefix. Vanilla CREATE spawns bullet2 at a fixed offset from the root (sized for the small statue);
    /// DemonTentacle fires from the muzzle bone instead. Returns true to run vanilla.
    /// </summary>
    internal static bool HandleSpineEvent(AngelStatue enemy, Spine.Event e)
    {
        if (e?.Data == null || e.Data.Name != "CREATE")
            return true;

        DemonTentacleMarker marker = GetMarker(enemy);
        if (marker == null || !TrySpawnProjectile(enemy, marker))
            return true;

        (ParticleField?.GetValue(enemy) as ParticleSystem)?.Stop();
        DemonTentacleCombatAi.OnShotFired(enemy, marker);
        return false;
    }

    /// <summary>Spawns the vanilla AngelStatue projectile at the muzzle bone. False = no bullet prefab or muzzle bone.</summary>
    internal static bool TrySpawnProjectile(AngelStatue enemy, DemonTentacleMarker marker)
    {
        if (enemy == null || marker == null || !TryGetMuzzlePosition(marker.BattleSpine, out Vector3 muzzle))
            return false;

        GameObject bullet = BulletField?.GetValue(enemy) as GameObject;
        if (bullet == null)
            return false;

        GameObject spawned = UnityEngine.Object.Instantiate(bullet, new Vector2(muzzle.x, muzzle.y), enemy.transform.rotation);
        DemonTentacleProjectile.Attach(spawned, enemy);
        return true;
    }

    /// <summary>
    /// World position of the end of the muzzle bone: bone21, else the tip of the shooting tentacle
    /// (bone26, slot "Tentacle 4": closed "Tentacle 4" / open "Tentacle 5").
    /// </summary>
    private static bool TryGetMuzzlePosition(SkeletonAnimation spine, out Vector3 world)
    {
        world = Vector3.zero;
        Spine.Skeleton skeleton = spine?.skeleton;
        Spine.Bone bone = skeleton?.FindBone(MuzzleBoneName) ?? skeleton?.FindBone(FallbackMuzzleBoneName);
        if (bone == null)
        {
            if (!_loggedMissingMuzzle)
            {
                _loggedMissingMuzzle = true;
                Plugin.Log?.LogWarning(
                    $"[DemonTentacle] Bones '{MuzzleBoneName}' / '{FallbackMuzzleBoneName}' not found; projectile uses the vanilla AngelStatue offset.");
            }
            return false;
        }

        float length = bone.Data.Length;
        float skeletonX = bone.A * length + bone.WorldX;
        float skeletonY = bone.C * length + bone.WorldY;
        world = spine.transform.TransformPoint(new Vector3(skeletonX, skeletonY, 0f));
        return true;
    }

    private static void PlayHitFeedback(DemonTentacleMarker marker, bool weapon)
    {
        if (marker.BattleSpine?.skeleton != null)
        {
            marker.BattleSpine.skeleton.SetColor(Color.red);
            marker.FlashUntil = Time.time + HitFlashDuration;
        }

        if (!weapon)
            return;

        try
        {
            MasterAudio.StopAllOfSound("sword_hit");
            MasterAudio.PlaySound("sword_hit", 1f, null, 0f, null, false, false);
        }
        catch
        {
        }
    }
}

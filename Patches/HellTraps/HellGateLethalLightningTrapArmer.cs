using System.Collections;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Arms lethal lightning flow on button press: warning → delay → bolt VFX → lethal finalize.
/// Strike only if the player is still on the button. After resolve, cooldown then re-armable.
/// </summary>
internal sealed class HellGateLethalLightningTrapArmer : MonoBehaviour
{
    private bool _armed;
    private bool _playerInside;
    private playercon _playerInsideRef;
    private Collider2D _buttonCollider;

    private void Awake()
    {
        _buttonCollider = GetComponent<Collider2D>();
        if (_buttonCollider == null)
            _buttonCollider = GetComponentInChildren<Collider2D>(true);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        TrackPlayerInside(col, entering: true);
        TryArmFromCollider(col);
    }

    private void OnTriggerStay2D(Collider2D col)
    {
        TrackPlayerInside(col, entering: true);
        // After cooldown, player may still be standing on the button — Enter will not fire again.
        TryArmFromCollider(col);
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        TrackPlayerInside(col, entering: false);
    }

    private void TrackPlayerInside(Collider2D col, bool entering)
    {
        if (col == null || col.gameObject.tag != "playerDAMAGEcol")
            return;

        playercon player = col.transform.root.GetComponent<playercon>();
        if (player == null)
            return;

        if (entering)
        {
            _playerInside = true;
            _playerInsideRef = player;
            return;
        }

        if (_playerInsideRef == null || player == _playerInsideRef)
        {
            _playerInside = false;
            _playerInsideRef = null;
        }
    }

    private void TryArmFromCollider(Collider2D col)
    {
        if (!Plugin.IsLethalLightningTrapActive)
            return;

        if (_armed || col == null || col.gameObject.tag != "playerDAMAGEcol")
            return;

        playercon player = col.transform.root.GetComponent<playercon>();
        if (player == null)
            return;

        if (player.stepfrag || player._Death)
            return;

        if (LethalLightningTrapDeathContext.IsLethalHitInProgress ||
            LethalLightningTrapDeathDisplay.HasActiveClip)
        {
            return;
        }

        _armed = true;
        StartCoroutine(ArmSequence(player));
    }

    private IEnumerator ArmSequence(playercon player)
    {
        Vector3 trapPos = transform.position;

        try
        {
            if (player == null)
                yield break;

            LethalLightningTrapDeathContext.SetTrapAnchorWorld(trapPos);
            LethalMagicTrapDeathContext.SetTrapFloorWorld(trapPos);

            // Exact spawn / button world coords (not player — avoids right shift when standing off-center).
            LethalLightningTrapVfx.SpawnWarningAtWorld(trapPos);

            float delay = Plugin.lethalLightningTrapWarningDelay != null
                ? Mathf.Max(0.05f, Plugin.lethalLightningTrapWarningDelay.Value)
                : LethalLightningTrapDeathTuning.DefaultWarningDelaySeconds;

            Plugin.Log?.LogInfo(
                "[LethalLightningTrap] Armed @ "
                + trapPos
                + ", delay="
                + delay.ToString("0.##")
                + "s (VFX always; kill only if still on button)");

            yield return new WaitForSeconds(delay);

            bool shake = Plugin.lethalLightningTrapUseStrikeShake == null ||
                         Plugin.lethalLightningTrapUseStrikeShake.Value;

            // Vanilla-style: strike VFX always fires at the trap after the warning delay.
            LethalLightningTrapVfx.SpawnLightningStrikeAtWorld(trapPos, shake);

            if (player == null || !IsPlayerStillOnButton(player))
            {
                Plugin.Log?.LogInfo(
                    "[LethalLightningTrap] VFX fired; kill skipped — player not in button radius.");
                yield break;
            }

            LethalLightningTrapRuntime.ApplyLethalStrike(player, trapPos);
        }
        finally
        {
            // IEnumerator finally cannot yield — cooldown runs in a follow-up routine.
            StartCoroutine(CooldownThenDisarm());
        }
    }

    private IEnumerator CooldownThenDisarm()
    {
        float cooldown = Plugin.lethalLightningTrapCooldownSeconds != null
            ? Mathf.Max(0f, Plugin.lethalLightningTrapCooldownSeconds.Value)
            : LethalLightningTrapDeathTuning.DefaultCooldownSeconds;

        if (cooldown > 0f)
            yield return new WaitForSeconds(cooldown);

        _armed = false;
        Plugin.Log?.LogInfo(
            "[LethalLightningTrap] Cooldown done ("
            + cooldown.ToString("0.##")
            + "s) — button active again @ "
            + transform.position);
    }

    private bool IsPlayerStillOnButton(playercon player)
    {
        if (player == null || player._Death)
            return false;

        if (_playerInside && _playerInsideRef == player)
            return true;

        if (_buttonCollider == null)
            return false;

        Collider2D playerCol = null;
        Transform damageCol = player.transform.Find("playerDAMAGEcol");
        if (damageCol != null)
            playerCol = damageCol.GetComponent<Collider2D>();
        if (playerCol == null)
        {
            Collider2D[] cols = player.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].CompareTag("playerDAMAGEcol"))
                {
                    playerCol = cols[i];
                    break;
                }
            }
        }

        if (playerCol == null)
            return false;

        return _buttonCollider.bounds.Intersects(playerCol.bounds);
    }

    internal void ResetArmed()
    {
        _armed = false;
    }
}

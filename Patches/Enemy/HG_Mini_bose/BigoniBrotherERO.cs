using System;
using System.Collections;
using System.Collections.Generic;
using DarkTonic.MasterAudio;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy
{
    // Token: 0x02000811 RID: 2065
    /// <summary>
    /// Отдельный ERO класс для BigoniBrother - полностью независимый от оригинального Bigoni
    /// </summary>
    public class BigoniBrotherERO : MonoBehaviour
{
    // Token: 0x04000C00 RID: 3072
    public Bigoni oya;

    // Token: 0x04000C01 RID: 3073
    public SkeletonAnimation myspine;

    // Token: 0x04000C02 RID: 3074
    public int count;

    // Token: 0x04000C03 RID: 3075
    public int se_count;

    // Token: 0x04000C04 RID: 3076
    private playercon player;

    // Token: 0x04000C05 RID: 3077
    private PlayerStatus pl;

    // Token: 0x04000C06 RID: 3078
    private Canvas thiscanvas;

    // Token: 0x04000C07 RID: 3079
    private AudioSource Audio;

    // Token: 0x04000C08 RID: 3080
    public AudioClip[] SE;

    // Token: 0x04000C09 RID: 3081
    private GameObject[] moza;

    // Token: 0x04000C0A RID: 3082
    private float timecount;

    // Token: 0x04000C0B RID: 3083
    private bool frag;

    // Token: 0x04000C0C RID: 3084
    private bool ENDflag;

    // Token: 0x04000C0D RID: 3085
    private GameObject endsubmit;

    // Token: 0x04000C0E RID: 3086
    private fadein_out fadeinout;

    // Token: 0x04000C0F RID: 3087
    private UInarration narration;

    // Token: 0x04000C10 RID: 3088
    private GameObject UIeffet;

    // Token: 0x04000C11 RID: 3089
    private fadein_out fadeimg;

    // Token: 0x04000C12 RID: 3090
    private Canvas MainUICanvas;

    // Token: 0x0600445D RID: 17501
    private void Start()
    {
        this.myspine.state.Event += this.OnEvent;
        this.MainUICanvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        this.MainUICanvas.enabled = false;
        this.thiscanvas.worldCamera = GameObject.FindWithTag("MainCamera").GetComponent<Camera>();
        this.thiscanvas.planeDistance = 1f;
        this.UIeffet = GameObject.Find("UIeffect");
        this.fadeimg = this.UIeffet.GetComponent<fadein_out>();
        this.pl = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetPlayerStatus();
        this.player = GameObject.FindWithTag("Player").GetComponent<playercon>();

        // ВАЖНО: Для BigoniBrother ВКЛЮЧАЕМ систему борьбы (в отличие от оригинального Bigoni)
        this.pl._SOUSA = true;
        this.pl._SOUSAMNG = true;

        this.player.rigi2d.simulated = false;
        this.player.transform.position = new Vector2(0f, 0f);
        this.fadeinout._fade_now = true;
        this.fadeinout._enable = true;
    }

    // Token: 0x0600445E RID: 17502
    private void Seset(string sename)
    {
        MasterAudio.StopAllOfSound(sename);
        MasterAudio.PlaySound(sename, 1f, null, 0f, null, false, false);
    }

    // Token: 0x0600445F RID: 17503
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            this.narration = this.UIeffet.GetComponent<UInarration>();
            this.narration.narration();
            this.fadeimg.on_slow();
            base.Invoke("REstrat_invoke", 8f);
            base.Invoke("REstrat", 7f);
            this.frag = true;
            this.endsubmit.SetActive(false);
        }
        if (this.timecount < 3f)
        {
            this.timecount += 1f * Time.deltaTime;
        }
        if (this.timecount <= 2f || this.frag)
        {
        }
        if (this.frag || this.timecount <= 2f || !this.ENDflag)
        {
        }
        if (this.ENDflag && !this.endsubmit.activeSelf && !this.frag)
        {
            this.endsubmit.SetActive(true);
        }
        if (this.ENDflag && (this.player._key_submit || Input.GetMouseButtonDown(0)))
        {
            this.narration = this.UIeffet.GetComponent<UInarration>();
            this.narration.narration();
            this.fadeimg.on_slow();
            base.Invoke("REstrat_invoke", 8f);
            base.Invoke("REstrat", 7f);
            this.frag = true;
            this.endsubmit.SetActive(false);
        }
    }

    // Token: 0x06004460 RID: 17504
    private void REstrat_invoke()
    {
        this.fadeimg.on_fade_in();
        Canvas component = GameObject.Find("Canvas").GetComponent<Canvas>();
        component.enabled = true;
        UnityEngine.Object.Destroy(base.gameObject);
    }

    // Token: 0x06004461 RID: 17505
    private void REstrat()
    {
    }

    // Token: 0x06004462 RID: 17506
    private void OnEvent(Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        string name = e.Data.name;
        switch (name)
        {
        case "SE":
        {
            this.se_count++;
            string animationName = this.myspine.AnimationName;
            switch (animationName)
            {
            case "ERO":
                if (this.se_count == 1)
                {
                    MasterAudio.PlaySound("ero_now12", 1f, null, 0f, null, false, false);
                    this.se_count = 0;
                }
                break;
            case "2ERO":
                if (this.se_count == 1)
                {
                    MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                    this.se_count = 0;
                }
                break;
            case "FIN":
                if (this.se_count == 1)
                {
                    MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                }
                else if (this.se_count == 2)
                {
                    this.Seset("ero_enemy_syasei1");
                    this.se_count = 0;
                }
                break;
            case "FIN2":
                if (this.se_count == 1)
                {
                    MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                }
                else if (this.se_count == 2)
                {
                    this.randomSE();
                    this.Seset("ero_enemy_syasei1");
                    this.se_count = 0;
                }
                break;
            case "JIGO":
                if (this.se_count == 1)
                {
                    MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                    this.se_count = 0;
                }
                break;
            case "JIGO2":
                if (this.se_count == 1)
                {
                    this.moza[1].SetActive(false);
                    this.Seset("dame_kuu");
                }
                else if (this.se_count == 2)
                {
                    this.Seset("snd_down");
                    this.moza[3].SetActive(true);
                    this.se_count = 0;
                }
                break;
            }
            break;
        }
        case "SE1":
            this.randomSE();
            break;
        case "SE2":
            this.GutyuSE();
            break;
        case "SE3":
            this.ChainSE();
            break;
        case "SE4":
            this.Seset("ero_enemy_syasei2");
            this.moza[2].SetActive(true);
            break;
        case "SE5":
            MasterAudio.StopAllOfSound("snd_step");
            MasterAudio.PlaySound("snd_step", 1f, null, 0f, null, false, false);
            break;
        case "SE6":
            this.Audio.PlayOneShot(this.SE[0]);
            break;
        case "SE7":
            this.Audio.PlayOneShot(this.SE[1]);
            break;
        case "ERO":
            this.count++;
            if (this.count >= 2)
            {
                this.myspine.AnimationState.SetAnimation(0, "2ERO", true);
            }
            break;
        case "2ERO":
            this.count++;
            if (this.count >= 15)
            {
                this.myspine.AnimationState.SetAnimation(0, "FIN", false);
            }
            break;
        case "FIN":
            this.myspine.AnimationState.AddAnimation(0, "JIGO", false, 0f);
            break;
        case "JIGO":
            this.myspine.AnimationState.AddAnimation(0, "JIGO2", false, 0f);
            break;
        case "JIGO2":
            this.ENDflag = true;
            break;
        }
    }

    // Token: 0x06004463 RID: 17507
    private void randomSE()
    {
        int num = UnityEngine.Random.Range(0, 2);
        if (num == 0)
        {
            this.Seset("ero_gutyugutyu1");
        }
        else
        {
            this.Seset("ero_gutyugutyu2");
        }
    }

    // Token: 0x06004464 RID: 17508
    private void GutyuSE()
    {
        this.Seset("ero_gutyugutyu2");
    }

    // Token: 0x06004465 RID: 17509
    private void ChainSE()
    {
        this.Seset("ero_enemy_chain");
    }
}
}
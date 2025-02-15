﻿using RoR2;
using UnityEngine;
using System;
using UnityEngine.Networking;
using R2API;

namespace EntityStates.Digger
{
    public class Gouge : BaseSkillState
    {
        public static float damageCoefficient = DiggerPlugin.DiggerPlugin.gougeDamage.Value;
        public float baseDuration = 1f;
        public static float attackRecoil = 0.5f;
        public static float hitHopVelocity = 5f;
        public static float styleCoefficient = 1.2f;
        public static float attackRadius = 1.2f;
        public static float baseEarlyExit = 0.4f;
        public int swingIndex;

        private bool firstSwing = true; //hacky fix for shuriken
        private bool isSlash;
        private float earlyExitDuration;
        private float duration;
        private bool hasFired;
        private bool hasFiredSound;
        private float hitPauseTimer;
        private OverlapAttack attack;
        private bool inHitPause;
        private bool hasHopped;
        private float stopwatch;
        private Animator animator;
        private BaseState.HitStopCachedState hitStopCachedState;
        //private StyleSystem.StyleComponent styleComponent;

        public override void OnEnter()
        {
            base.OnEnter();

            //Shuriken fix
            if (!firstSwing && base.skillLocator && base.skillLocator.primary)  //Hardcoded to be primary since activatorSkillSlot nullrefs
            {
                base.characterBody.OnSkillActivated(base.skillLocator.primary);
            }

            this.duration = this.baseDuration / this.attackSpeedStat;
            this.earlyExitDuration = Gouge.baseEarlyExit / this.attackSpeedStat;
            this.hasFired = false;
            this.animator = base.GetModelAnimator();
            //this.styleComponent = base.GetComponent<StyleSystem.StyleComponent>();
            base.StartAimMode(0.5f + this.duration, false);

            if (base.characterBody.skinIndex == DiggerPlugin.DiggerPlugin.blacksmithSkinIndex && this.swingIndex == 0) this.isSlash = true;
            else this.isSlash = false;

            HitBoxGroup hitBoxGroup = null;
            Transform modelTransform = base.GetModelTransform();

            if (modelTransform)
            {
                hitBoxGroup = Array.Find<HitBoxGroup>(modelTransform.GetComponents<HitBoxGroup>(), (HitBoxGroup element) => element.groupName == "Crush");
            }

            if (this.swingIndex == 0) base.PlayCrossfade("Pick, Override", "Swing1", "Swing.playbackRate", this.duration, 0.05f);
            else base.PlayCrossfade("Pick, Override", "Swing2", "Swing.playbackRate", this.duration, 0.05f);

            this.attack = new OverlapAttack();
            this.attack.damageType = DamageType.Generic;
            this.attack.AddModdedDamageType(DiggerPlugin.DiggerPlugin.CleaveDamage);
            this.attack.attacker = base.gameObject;
            this.attack.inflictor = base.gameObject;
            this.attack.teamIndex = base.GetTeam();
            this.attack.damage = Gouge.damageCoefficient * this.damageStat;
            this.attack.procCoefficient = 1;
            if (this.isSlash) this.attack.hitEffectPrefab = DiggerPlugin.Assets.slashFX;
            else this.attack.hitEffectPrefab = DiggerPlugin.Assets.hitFX;
            this.attack.forceVector = Vector3.zero;
            this.attack.pushAwayForce = 1f;
            this.attack.hitBoxGroup = hitBoxGroup;
            this.attack.isCrit = base.RollCrit();
            this.attack.impactSound = DiggerPlugin.Assets.pickHitEventDef.index;
            if (this.isSlash) this.attack.impactSound = LegacyResourcesAPI.Load<NetworkSoundEventDef>("NetworkSoundEventDefs/nseMercSwordImpact").index;
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public void FireSound() {
            Util.PlayAttackSpeedSound(DiggerPlugin.Sounds.Swing, base.gameObject, this.attackSpeedStat);
        }

        public void FireAttack()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                if (base.isAuthority)
                {
                    string muzzleString;
                    if (this.swingIndex == 0) muzzleString = "SwingRight";
                    else muzzleString = "SwingLeft";

                    GameObject effectPrefab = DiggerPlugin.Assets.swingFX;
                    if (base.characterBody.GetBuffCount(DiggerPlugin.Buffs.goldRushBuff) >= 0.8f * DiggerPlugin.DiggerPlugin.adrenalineCap) effectPrefab = DiggerPlugin.Assets.empoweredSwingFX;

                    EffectManager.SimpleMuzzleFlash(effectPrefab, base.gameObject, muzzleString, true);

                    base.AddRecoil(-1f * Gouge.attackRecoil, -2f * Gouge.attackRecoil, -0.5f * Gouge.attackRecoil, 0.5f * Gouge.attackRecoil);
                }
            }

            if (base.isAuthority)
            {
                if (this.attack.Fire())
                {
                    //if (this.styleComponent) this.styleComponent.AddStyle(Gouge.styleCoefficient);

                    if (!this.hasHopped)
                    {
                        if (base.characterMotor && !base.characterMotor.isGrounded)
                        {
                            base.SmallHop(base.characterMotor, (Gouge.hitHopVelocity / Mathf.Sqrt(this.attackSpeedStat)));
                        }

                        this.hasHopped = true;
                    }

                    if (!this.inHitPause)
                    {
                        this.hitStopCachedState = base.CreateHitStopCachedState(base.characterMotor, this.animator, "Swing.playbackRate");
                        this.hitPauseTimer = (0.6f * Merc.GroundLight.hitPauseDuration) / this.attackSpeedStat;
                        this.inHitPause = true;
                    } 
                }
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            this.hitPauseTimer -= Time.fixedDeltaTime;

            if (this.hitPauseTimer <= 0f && this.inHitPause)
            {
                base.ConsumeHitStopCachedState(this.hitStopCachedState, base.characterMotor, this.animator);
                this.inHitPause = false;
            }

            if (!this.inHitPause)
            {
                this.stopwatch += Time.fixedDeltaTime;
            }
            else
            {
                if (base.characterMotor) base.characterMotor.velocity = Vector3.zero;
                if (this.animator) this.animator.SetFloat("Swing.playbackRate", 0f);
            }


            if (this.stopwatch >= this.duration * 0.189f && !hasFiredSound) {
                FireSound();
                hasFiredSound = true;
            }

            if (this.stopwatch >= this.duration * 0.245f && (!this.hasFired || this.stopwatch <= this.duration * 0.469f))
            {
                this.FireAttack();
            }

            if (base.fixedAge >= (this.duration - this.earlyExitDuration) && base.isAuthority)
            {
                if (base.inputBank.skill1.down)
                {
                    int index = this.swingIndex;
                    if (index == 0) index = 1;
                    else index = 0;

                    this.outer.SetNextState(new Gouge
                    {
                        swingIndex = index,
                        firstSwing = false
                    });

                    if (!this.hasFired) this.FireAttack();

                    return;
                }
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(this.swingIndex);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            this.swingIndex = reader.ReadInt32();
        }
    }
}
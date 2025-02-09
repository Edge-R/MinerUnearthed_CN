﻿using RoR2;
using UnityEngine;
using System;
using static RoR2.CameraTargetParams;

namespace EntityStates.Digger
{
    public class BaseEmote : BaseState
    {
        public string soundString;
        public string animString;
        public float duration;
        public float animDuration;
        public bool explosive;

        private bool hasExploded;
        private uint activePlayID;
        private Animator animator;
        private ChildLocator childLocator;

        public LocalUser localUser;

        private CharacterCameraParamsData emoteCameraParams = new CharacterCameraParamsData() {
            maxPitch = 70,
            minPitch = -70,
            pivotVerticalOffset = 1f,
            idealLocalCameraPos = emoteCameraPosition,
            wallCushion = 0.1f,
        };

        public static Vector3 emoteCameraPosition = new Vector3(0, 0.0f, -7.9f);

        private CameraParamsOverrideHandle camOverrideHandle;

        public override void OnEnter()
        {
            base.OnEnter();
            this.animator = base.GetModelAnimator();
            this.childLocator = base.GetModelChildLocator();
            this.localUser = LocalUserManager.readOnlyLocalUsersList[0];

            base.characterBody.hideCrosshair = true;

            if (base.GetAimAnimator()) base.GetAimAnimator().enabled = false;
            this.animator.SetLayerWeight(animator.GetLayerIndex("AimPitch"), 0);
            this.animator.SetLayerWeight(animator.GetLayerIndex("AimYaw"), 0);

            if (this.animDuration == 0 && this.duration != 0) this.animDuration = this.duration;

            if (this.duration > 0) base.PlayAnimation("FullBody, Override", this.animString, "Emote.playbackRate", this.duration);
            else base.PlayAnimation("FullBody, Override", this.animString, "Emote.playbackRate", this.animDuration);

            this.activePlayID = Util.PlaySound(soundString, base.gameObject);

            CameraParamsOverrideRequest request = new CameraParamsOverrideRequest {
                cameraParamsData = emoteCameraParams,
                priority = 0,
            };

            camOverrideHandle = base.cameraTargetParams.AddParamsOverride(request, 0.5f);

        }

        public override void OnExit()
        {
            base.OnExit();

            base.characterBody.hideCrosshair = false;

            if (base.GetAimAnimator()) base.GetAimAnimator().enabled = true;
            this.animator.SetLayerWeight(animator.GetLayerIndex("AimPitch"), 1);
            this.animator.SetLayerWeight(animator.GetLayerIndex("AimYaw"), 1);

            base.PlayAnimation("FullBody, Override", "BufferEmpty");
            if (this.activePlayID != 0) AkSoundEngine.StopPlayingID(this.activePlayID);

            this.childLocator.FindChild("PickL").localScale = Vector3.one;
            this.childLocator.FindChild("PickR").localScale = Vector3.one;

            base.cameraTargetParams.RemoveParamsOverride(camOverrideHandle, 0.5f);
        }

        private void Explode()
        {
            if (!this.hasExploded)
            {
                this.hasExploded = true;

                BlastAttack blastAttack = new BlastAttack();
                blastAttack.radius = 8f;
                blastAttack.procCoefficient = 1f;
                blastAttack.position = base.characterBody.corePosition + (0.5f * base.characterDirection.forward) + (Vector3.up * - 0.25f);
                blastAttack.attacker = null;
                blastAttack.crit = base.RollCrit();
                blastAttack.baseDamage = base.characterBody.damage * 100f;
                blastAttack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
                blastAttack.baseForce = 8000f;
                blastAttack.teamIndex = TeamComponent.GetObjectTeam(blastAttack.attacker);
                blastAttack.damageType = DamageType.BypassOneShotProtection;
                blastAttack.attackerFiltering = AttackerFiltering.AlwaysHit;
                blastAttack.Fire();

                EffectData effectData = new EffectData();
                effectData.origin = base.characterBody.footPosition;
                effectData.scale = 32;

                EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX"), effectData, false);
                Util.PlaySound(DiggerPlugin.Sounds.ToTheStarsExplosion, base.gameObject);
                Util.PlaySound(DiggerPlugin.Sounds.Beep, base.gameObject);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            bool flag = false;

            if (base.characterMotor)
            {
                if (!base.characterMotor.isGrounded) flag = true;
                if (base.characterMotor.velocity != Vector3.zero) flag = true;
            }
             
            if (base.inputBank)
            {
                if (base.inputBank.skill1.down) flag = true;
                if (base.inputBank.skill2.down) flag = true;
                if (base.inputBank.skill3.down) flag = true;
                if (base.inputBank.skill4.down) flag = true;
                if (base.inputBank.jump.down) flag = true;

                if (base.inputBank.moveVector != Vector3.zero) flag = true;
            }

            //emote cancels
            if (base.isAuthority && base.characterMotor.isGrounded)
            {
                if (!this.localUser.isUIFocused)
                {
                    if (Input.GetKeyDown(DiggerPlugin.DiggerPlugin.restKeybind.Value))
                    {
                        this.outer.SetInterruptState(new Rest(), InterruptPriority.Any);
                        return;
                    }
                    else if (Input.GetKeyDown(DiggerPlugin.DiggerPlugin.tauntKeybind.Value))
                    {
                        this.outer.SetInterruptState(new Taunt(), InterruptPriority.Any);
                        return;
                    }
                    else if (Input.GetKeyDown(DiggerPlugin.DiggerPlugin.jokeKeybind.Value))
                    {
                        this.outer.SetInterruptState(new Joke(), InterruptPriority.Any);
                        return;
                    }
                }
            }

            if (this.duration > 0 && base.fixedAge >= this.duration) flag = true;


            if (this.explosive && base.fixedAge >= this.duration && base.isAuthority)
            {
                this.Explode();
            }

            if (flag)
            {
                this.outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Any;
        }
    }

    public class Rest : BaseEmote
    {
        public override void OnEnter()
        {
            this.animString = "Rest";
            this.animDuration = 2.5f;
            this.explosive = false;
            base.OnEnter();
        }
    }

    public class Taunt : BaseEmote
    {
        public override void OnEnter()
        {
            this.animString = "Taunt";
            this.animDuration = 2f;
            this.explosive = false;
            base.OnEnter();
        }
    }

    public class Joke : BaseEmote
    {
        public override void OnEnter()
        {
            this.animString = "Joke";
            this.animDuration = 3.5f;
            this.duration = 3.5f;
            this.explosive = true;
            base.OnEnter();
        }
    }
}

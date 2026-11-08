using System;
using System.Collections.Generic;
using EntityStates;
using GravitosNext.Components;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.SkillStates;

public class FireGravitonLance : BaseSkillState
{
	private float duration;

	private float tickInterval;

	private float tickTimer;

	private Vector3 beamDirection;

	private Transform muzzle;

	private GravitonBeamVisual visual;

	private uint loopSoundId;

	private GravitonBeamDamage beamDamage;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = GravitosConfig.LanceDuration.Value;
		tickInterval = GravitosConfig.LanceTickInterval.Value / attackSpeedStat;
		beamDirection = GetAimRay().direction;
		muzzle = GravitonBeamVisual.FindMuzzle(this);
		beamDamage = new GravitonBeamDamage(base.gameObject);
		PlayAnimation("Gesture Right Cannon, Additive", "FireGrenadeRight");
		Util.PlaySound("Play_voidRaid_snipe_shoot", base.gameObject);
		loopSoundId = Util.PlaySound("Play_voidman_m1_corrupted_loop", base.gameObject);
		if (base.isAuthority)
		{
			AddRecoil(-1f, -2f, -0.5f, 0.5f);
		}
		visual = GravitonBeamVisual.Create(GravitosConfig.LanceBeamRadius.Value);
		if ((bool)base.characterMotor)
		{
			base.characterMotor.walkSpeedPenaltyCoefficient = GravitosConfig.LanceFireMoveSpeed.Value;
		}
	}

	private Ray BeamRay()
	{
		return new Ray(GetAimRay().origin, beamDirection);
	}

	private float BeamLength(Ray ray)
	{
		float value = GravitosConfig.LanceRange.Value;
		if (!Physics.Raycast(ray, out var hitInfo, value, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			return value;
		}
		return hitInfo.distance;
	}

	public override void Update()
	{
		base.Update();
		if ((bool)visual)
		{
			Ray ray = BeamRay();
			Vector3 point = ray.GetPoint(BeamLength(ray));
			Vector3 start = (muzzle ? muzzle.position : ray.origin);
			visual.SetPoints(start, point);
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		StartAimMode();
		float maxRadiansDelta = GravitosConfig.LanceTurnRate.Value * (MathF.PI / 180f) * Time.fixedDeltaTime;
		beamDirection = Vector3.RotateTowards(beamDirection, GetAimRay().direction, maxRadiansDelta, 0f).normalized;
		if (NetworkServer.active)
		{
			tickTimer -= Time.fixedDeltaTime;
			while (tickTimer <= 0f)
			{
				tickTimer += tickInterval;
				Tick();
			}
		}
		if (base.isAuthority)
		{
			base.characterBody.isSprinting = false;
			if (base.fixedAge >= duration)
			{
				outer.SetNextStateToMain();
			}
		}
	}

	private void Tick()
	{
		Ray ray = BeamRay();
		float damage = damageStat * GravitosConfig.LanceDamagePerSecond.Value * GravitosConfig.LanceTickInterval.Value;
		beamDamage.FireBeam(ray, BeamLength(ray), GravitosConfig.LanceBeamRadius.Value, GravitosConfig.LanceSplashRadius.Value, damage, damageStat * GravitosConfig.LanceSlamDamage.Value, RollCrit(), base.teamComponent.teamIndex, base.fixedAge, new HashSet<HealthComponent>());
	}

	public override void OnExit()
	{
		if ((bool)visual)
		{
			visual.Stop();
		}
		if (loopSoundId != 0)
		{
			AkSoundEngine.StopPlayingID(loopSoundId);
		}
		Util.PlaySound("Play_voidman_m1_corrupted_end", base.gameObject);
		if ((bool)base.characterMotor)
		{
			base.characterMotor.walkSpeedPenaltyCoefficient = 1f;
		}
		base.OnExit();
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.PrioritySkill;
	}
}

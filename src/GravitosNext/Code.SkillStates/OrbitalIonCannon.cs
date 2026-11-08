using System.Collections.Generic;
using EntityStates;
using GravitosNext.Components;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.SkillStates;

public class OrbitalIonCannon : BaseSkillState
{
	private enum Phase
	{
		Ascend,
		Fire,
		Descend
	}

	private const float AscendTime = 1.5f;

	private const float MaxDescendTime = 5f;

	private const float DescendSpeed = 25f;

	private const int BeamCount = 4;

	private Phase phase;

	private float phaseAge;

	private float altitude;

	private Vector3 center;

	private float spin;

	private float tickTimer;

	private uint loopSoundId;

	private CharacterModel characterModel;

	private CameraTargetParams.CameraParamsOverrideHandle cameraHandle;

	private readonly GravitonBeamVisual[] visuals = new GravitonBeamVisual[4];

	private readonly Vector3[] beamTops = new Vector3[4];

	private readonly Vector3[] beamBottoms = new Vector3[4];

	private GravitonBeamDamage beamDamage;

	public override void OnEnter()
	{
		base.OnEnter();
		altitude = base.characterBody.footPosition.y + GravitosConfig.IonAltitude.Value;
		center = AimPoint();
		beamDamage = new GravitonBeamDamage(base.gameObject);
		Util.PlaySound("Play_voidRaid_snipe_chargeUp", base.gameObject);
		base.characterBody.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;
		if (NetworkServer.active)
		{
			base.characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
		}
		Transform modelTransform = GetModelTransform();
		characterModel = (modelTransform ? modelTransform.GetComponent<CharacterModel>() : null);
		if ((bool)characterModel)
		{
			CharacterModel obj = characterModel;
			obj.invisibilityCount++;
		}
		if ((bool)base.cameraTargetParams)
		{
			float value = GravitosConfig.IonCameraDistance.Value;
			CharacterCameraParamsData characterCameraParamsData = default(CharacterCameraParamsData);
			characterCameraParamsData.idealLocalCameraPos = new Vector3(0f, 0f, 0f - value);
			characterCameraParamsData.pivotVerticalOffset = 2f;
			characterCameraParamsData.maxPitch = 89f;
			characterCameraParamsData.minPitch = -30f;
			characterCameraParamsData.wallCushion = 0.1f;
			CharacterCameraParamsData cameraParamsData = characterCameraParamsData;
			cameraHandle = base.cameraTargetParams.AddParamsOverride(new CameraTargetParams.CameraParamsOverrideRequest
			{
				cameraParamsData = cameraParamsData,
				priority = 10f
			}, 1f);
		}
	}

	private Vector3 AimPoint()
	{
		Ray aimRay = GetAimRay();
		if (Physics.Raycast(aimRay, out var hitInfo, 1000f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			return hitInfo.point;
		}
		return GravityUtil.SnapToGround(aimRay.GetPoint(150f));
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		float fixedDeltaTime = Time.fixedDeltaTime;
		phaseAge += fixedDeltaTime;
		switch (phase)
		{
		case Phase.Ascend:
			Fly(fixedDeltaTime, holdAltitude: true);
			if (phaseAge >= 1.5f)
			{
				StartFiring();
			}
			break;
		case Phase.Fire:
			Fly(fixedDeltaTime, holdAltitude: true);
			UpdateBeams(fixedDeltaTime);
			if (NetworkServer.active)
			{
				ServerTicks(fixedDeltaTime);
			}
			if (phaseAge >= GravitosConfig.IonDuration.Value)
			{
				StartDescending();
			}
			break;
		case Phase.Descend:
		{
			Fly(fixedDeltaTime, holdAltitude: false);
			bool flag = (bool)base.characterMotor && base.characterMotor.isGrounded && phaseAge > 0.1f;
			if (base.isAuthority && (flag || phaseAge >= 5f))
			{
				outer.SetNextStateToMain();
			}
			break;
		}
		}
	}

	private void Fly(float dt, bool holdAltitude)
	{
		if (base.isAuthority && (bool)base.characterMotor)
		{
			base.characterBody.isSprinting = false;
			Vector3 velocity = (base.inputBank ? (base.inputBank.moveVector * GravitosConfig.IonFlySpeed.Value) : Vector3.zero);
			velocity.y = (holdAltitude ? Mathf.Clamp((altitude - base.characterBody.footPosition.y) * 3f, -20f, 35f) : (-25f));
			if ((bool)base.characterMotor.Motor)
			{
				base.characterMotor.Motor.ForceUnground();
			}
			base.characterMotor.velocity = velocity;
		}
	}

	private void StartFiring()
	{
		phase = Phase.Fire;
		phaseAge = 0f;
		Util.PlaySound("Play_voidRaid_snipe_shoot", base.gameObject);
		loopSoundId = Util.PlaySound("Play_voidman_m1_corrupted_loop", base.gameObject);
		for (int i = 0; i < 4; i++)
		{
			visuals[i] = GravitonBeamVisual.Create(GravitosConfig.IonBeamRadius.Value);
		}
		UpdateBeams(0f);
	}

	private void StartDescending()
	{
		phase = Phase.Descend;
		phaseAge = 0f;
		StopBeams();
	}

	private void UpdateBeams(float dt)
	{
		center = Vector3.MoveTowards(center, AimPoint(), GravitosConfig.IonBeamMoveSpeed.Value * dt);
		spin += GravitosConfig.IonSpinSpeed.Value * dt;
		float value = GravitosConfig.IonBeamSpread.Value;
		float value2 = GravitosConfig.IonBeamHeight.Value;
		for (int i = 0; i < 4; i++)
		{
			Vector3 vector = Quaternion.Euler(0f, spin + (float)i * 90f, 0f) * Vector3.forward * value;
			Vector3 vector2 = center + vector;
			Vector3 vector3 = vector2 + Vector3.up * value2;
			RaycastHit hitInfo;
			Vector3 vector4 = (Physics.Raycast(vector3, Vector3.down, out hitInfo, value2 * 2f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ? hitInfo.point : vector2);
			beamTops[i] = vector3;
			beamBottoms[i] = vector4;
		}
	}

	public override void Update()
	{
		base.Update();
		if (phase != Phase.Fire)
		{
			return;
		}
		for (int i = 0; i < 4; i++)
		{
			if ((bool)visuals[i])
			{
				visuals[i].SetPoints(beamTops[i], beamBottoms[i]);
			}
		}
	}

	private void ServerTicks(float dt)
	{
		tickTimer -= dt;
		if (!(tickTimer > 0f))
		{
			tickTimer += GravitosConfig.LanceTickInterval.Value / attackSpeedStat;
			float damage = damageStat * GravitosConfig.IonDamagePerSecond.Value * GravitosConfig.LanceTickInterval.Value;
			float slamDamage = damageStat * GravitosConfig.LanceSlamDamage.Value;
			bool crit = RollCrit();
			HashSet<HealthComponent> hitThisTick = new HashSet<HealthComponent>();
			for (int i = 0; i < 4; i++)
			{
				Vector3 vector = beamBottoms[i] - beamTops[i];
				Ray ray = new Ray(beamTops[i], vector.normalized);
				beamDamage.FireBeam(ray, vector.magnitude, GravitosConfig.IonBeamRadius.Value, GravitosConfig.LanceSplashRadius.Value, damage, slamDamage, crit, base.teamComponent.teamIndex, base.fixedAge, hitThisTick);
			}
		}
	}

	private void StopBeams()
	{
		for (int i = 0; i < 4; i++)
		{
			if ((bool)visuals[i])
			{
				visuals[i].Stop();
			}
			visuals[i] = null;
		}
		if (loopSoundId != 0)
		{
			AkSoundEngine.StopPlayingID(loopSoundId);
			loopSoundId = 0u;
			Util.PlaySound("Play_voidman_m1_corrupted_end", base.gameObject);
		}
	}

	public override void OnExit()
	{
		StopBeams();
		if ((bool)characterModel)
		{
			CharacterModel obj = characterModel;
			obj.invisibilityCount--;
		}
		if ((bool)base.cameraTargetParams && cameraHandle.isValid)
		{
			cameraHandle = base.cameraTargetParams.RemoveParamsOverride(cameraHandle, 1f);
		}
		if (NetworkServer.active)
		{
			base.characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
		}
		base.characterBody.bodyFlags &= ~CharacterBody.BodyFlags.IgnoreFallDamage;
		base.OnExit();
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Frozen;
	}
}

using EntityStates;
using NextMech.Components;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

// AC4 Overed Boost: spool up, then rocket where you look at a frightening speed. Burns EN and PA.
// Quick Boost cancels it (it runs on the same Boost machine). Jump = Boost Kick. Press V again to stop.
public class OveredBoost : BaseState
{
	private bool lit;
	private float speed;

	public override void OnEnter()
	{
		base.OnEnter();
		HPStatus st = HPStatus.Of(characterBody);
		if (st) st.Spend(NextConfig.ObStartCost.Value);
		speed = NextConfig.ObSpeed.Value * Garage.ObSpeed(characterBody);
		CustomAudio.PlayNear(CustomAudio.Clip.ArmorCharge, transform.position, 60f);
		if (characterBody) HPUtil.SpawnLocalEffect(HPAssets.Spark, characterBody.corePosition - transform.forward * 1.2f, 1.5f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		float charge = NextConfig.ObCharge.Value;
		if (!lit && fixedAge >= charge)
		{
			lit = true;
			Anim.Loop(GetModelAnimator(), "AssaultBoost");
			CustomAudio.PlayNear(CustomAudio.Clip.AssaultBoost, transform.position, 90f);
			if (characterBody) HPUtil.SpawnLocalEffect(HPAssets.Blink, characterBody.corePosition, 3.5f);
		}
		if (NetworkServer.active && lit)
		{
			PrimalArmor.Drain(characterBody, NextConfig.ObPaDrain.Value * Time.fixedDeltaTime);
		}
		if (!isAuthority || !characterMotor) return;

		HPStatus st = HPStatus.Of(characterBody);
		NextPilot pilot = NextPilot.Of(characterBody);
		Vector3 aim = GetAimRay().direction;
		if (!lit)
		{
			// spool-up: brake hard, hang in place
			characterMotor.velocity *= 0.85f;
		}
		else
		{
			aim.y = Mathf.Clamp(aim.y, -0.3f, 0.35f);
			aim.Normalize();
			if (characterMotor.Motor) characterMotor.Motor.ForceUnground();
			float ramp = Mathf.Clamp01((fixedAge - charge) / 0.35f);
			characterMotor.velocity = aim * Mathf.Lerp(speed * 0.5f, speed, ramp);
			Move.Face(characterDirection, Move.Flat(aim, transform.forward));
			if (st) st.Spend(NextConfig.ObCost.Value * Garage.ObCost(characterBody) * Time.fixedDeltaTime);
			if (inputBank && inputBank.jump.justPressed)
			{
				outer.SetNextState(new BoostKick { direction = Move.Flat(aim, transform.forward) });
				return;
			}
		}
		if ((st && st.redline) || (pilot && pilot.obStopRequested))
		{
			if (pilot) pilot.obStopRequested = false;
			outer.SetNextStateToMain();
		}
	}

	public override void OnExit()
	{
		if (!outer.destroying && lit) Anim.Stop(GetModelAnimator());
		if (isAuthority && characterMotor && lit) characterMotor.velocity *= 0.35f;
		base.OnExit();
	}

	// Quick Boost (Skill priority) may cut it off: the classic QB-cancel.
	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

// Hanger weapon: shotgun. Big stagger up close.
public class FireShotgun : BaseSkillState
{
	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = NextConfig.ShotgunInterval.Value / attackSpeedStat;
		characterBody.SetAimTimer(2f);
		Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 1f);
		CustomAudio.PlayNear(CustomAudio.Clip.Stomp, transform.position, 70f);
		CustomAudio.PlayNear(CustomAudio.Clip.Rifle, transform.position, 70f);
		if (HPAssets.MuzzleFlash) EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, gameObject, "MuzzleRifle", false);
		if (isAuthority)
		{
			Ray aim = GetAimRay();
			AddRecoil(-2.5f, -3.5f, -0.8f, 0.8f);
			new BulletAttack
			{
				owner = gameObject,
				weapon = gameObject,
				origin = aim.origin,
				aimVector = aim.direction,
				minSpread = 0f,
				maxSpread = 6f,
				bulletCount = (uint)Mathf.Max(1, NextConfig.ShotgunPellets.Value),
				damage = damageStat * NextConfig.ShotgunDamage.Value,
				procCoefficient = 0.7f,
				force = 300f,
				falloffModel = BulletAttack.FalloffModel.Buckshot,
				maxDistance = 70f,
				radius = 0.4f,
				isCrit = RollCrit(),
				muzzleName = "MuzzleRifle",
				tracerEffectPrefab = HPAssets.Tracer,
				hitEffectPrefab = HPAssets.HitSpark,
				smartCollision = true,
			}.Fire();
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

// Hanger weapon: linear rifle. One heavy piercing round.
public class FireLinear : BaseSkillState
{
	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = NextConfig.LinearInterval.Value / attackSpeedStat;
		characterBody.SetAimTimer(2f);
		Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 0.7f);
		CustomAudio.PlayNear(CustomAudio.Clip.Rifle, transform.position, 80f);
		CustomAudio.PlayNear(CustomAudio.Clip.MissileHit, transform.position, 40f);
		if (HPAssets.MuzzleFlash) EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, gameObject, "MuzzleRifle", false);
		if (isAuthority)
		{
			Ray aim = GetAimRay();
			AddRecoil(-1.5f, -2f, -0.4f, 0.4f);
			new BulletAttack
			{
				owner = gameObject,
				weapon = gameObject,
				origin = aim.origin,
				aimVector = aim.direction,
				minSpread = 0f,
				maxSpread = 0f,
				bulletCount = 1u,
				damage = damageStat * NextConfig.LinearDamage.Value,
				procCoefficient = 1f,
				force = 800f,
				falloffModel = BulletAttack.FalloffModel.None,
				maxDistance = 300f,
				radius = 0.6f,
				isCrit = RollCrit(),
				muzzleName = "MuzzleRifle",
				tracerEffectPrefab = HPAssets.Tracer,
				hitEffectPrefab = HPAssets.Spark ? HPAssets.Spark : HPAssets.HitSpark,
				stopperMask = LayerIndex.world.mask,
				smartCollision = true,
			}.Fire();
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

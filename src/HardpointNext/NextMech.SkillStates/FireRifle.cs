using EntityStates;
using NextMech.Components;
using RoR2;
using UnityEngine;

namespace NextMech.SkillStates;

public class FireRifle : BaseSkillState
{
	private float gap;

	private float duration;

	private int fired;

	private int burst;

	private bool reload;

	private HPStatus status;

	public override void OnEnter()
	{
		base.OnEnter();
		status = HPStatus.Of(base.characterBody);
		if ((bool)status && status.magazine <= 0)
		{
			reload = true;
			return;
		}
		burst = Mathf.Max(1, HPConfig.RifleBurst.Value);
		gap = HPConfig.RifleShotGap.Value / attackSpeedStat;
		duration = gap * (float)(burst - 1) + HPConfig.RifleBurstGap.Value / attackSpeedStat;
		base.characterBody.SetAimTimer(2f);
		Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 0.8f / Mathf.Max(0.3f, duration + 0.2f));
		Shoot();
	}

	private void Shoot()
	{
		fired++;
		if ((bool)status)
		{
			status.magazine--;
		}
		CustomAudio.PlayNear(CustomAudio.Clip.Rifle, base.transform.position, 70f);
		if ((bool)HPAssets.MuzzleFlash)
		{
			EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, base.gameObject, "MuzzleRifle", transmit: false);
		}
		if (base.isAuthority)
		{
			Ray aimRay = GetAimRay();
			AddRecoil(-0.5f, -0.9f, -0.3f, 0.3f);
			BulletAttack bulletAttack = new BulletAttack();
			bulletAttack.owner = base.gameObject;
			bulletAttack.weapon = base.gameObject;
			bulletAttack.origin = aimRay.origin;
			bulletAttack.aimVector = aimRay.direction;
			bulletAttack.minSpread = 0f;
			bulletAttack.maxSpread = HPConfig.RifleSpread.Value;
			bulletAttack.bulletCount = 1u;
			bulletAttack.damage = damageStat * HPConfig.RifleDamage.Value;
			bulletAttack.procCoefficient = 1f;
			bulletAttack.force = 120f;
			bulletAttack.falloffModel = BulletAttack.FalloffModel.DefaultBullet;
			bulletAttack.maxDistance = HPConfig.RifleRange.Value;
			bulletAttack.radius = 0.2f;
			bulletAttack.isCrit = RollCrit();
			bulletAttack.muzzleName = "MuzzleRifle";
			bulletAttack.tracerEffectPrefab = HPAssets.Tracer;
			bulletAttack.hitEffectPrefab = HPAssets.HitSpark;
			bulletAttack.smartCollision = true;
			bulletAttack.Fire();
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (reload)
		{
			if (base.isAuthority)
			{
				outer.SetNextState(new ReloadRifle());
			}
			return;
		}
		while (fired < burst && base.fixedAge >= (float)fired * gap && (!status || status.magazine > 0))
		{
			Shoot();
		}
		if (base.isAuthority && base.fixedAge >= duration)
		{
			outer.SetNextStateToMain();
		}
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Skill;
	}
}

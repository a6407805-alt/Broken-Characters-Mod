using EntityStates;
using GravitosNext.Components;
using RoR2;
using UnityEngine.Networking;

namespace GravitosNext.SkillStates;

public class SelectState : BaseSkillState
{
	private const float LiftDuration = 0.3f;

	private const string Muzzle = "MuzzleLeft";

	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = GravitosConfig.SelectDuration.Value / attackSpeedStat;
		StartAimMode();
		PlayAnimation("Gesture Left Cannon, Additive", "FireGrenadeLeft");
		Util.PlaySound("Play_mage_m1_shoot", base.gameObject);
		if ((bool)GravitosAssets.MuzzleFlash)
		{
			EffectManager.SimpleMuzzleFlash(GravitosAssets.MuzzleFlash, base.gameObject, "MuzzleLeft", transmit: false);
		}
		if (NetworkServer.active)
		{
			Grab();
		}
	}

	private void Grab()
	{
		GravitosTracker component = GetComponent<GravitosTracker>();
		HurtBox hurtBox = (component ? component.GetTrackingTarget() : null);
		if ((bool)hurtBox && (bool)hurtBox.healthComponent && hurtBox.healthComponent.alive)
		{
			CharacterBody body = hurtBox.healthComponent.body;
			GravityLiftSlam.Apply(liftDuration: GravityUtil.IsSmall(body) ? 0.3f : 0f, victim: body, attacker: base.gameObject, damage: damageStat * GravitosConfig.SelectDamage.Value, crit: RollCrit(), procCoefficient: 1f);
			GravityUtil.SpawnEffect(GravitosAssets.SelectTracer, hurtBox.transform.position, 1f, GetAimRay().origin);
			GravityUtil.SpawnEffect(GravitosAssets.LightningImpact, hurtBox.transform.position, 1f);
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
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

using EntityStates;
using GravitosNext.Components;
using RoR2;
using UnityEngine;

namespace GravitosNext.SkillStates;

public class ChargeGravitonLance : BaseSkillState
{
	private float chargeDuration;

	private GameObject chargeEffect;

	private Transform muzzle;

	public override void OnEnter()
	{
		base.OnEnter();
		chargeDuration = GravitosConfig.LanceChargeTime.Value;
		muzzle = GravitonBeamVisual.FindMuzzle(this);
		PlayAnimation("Gesture, Additive", "ChargeGrenades");
		Util.PlaySound("Play_voidRaid_snipe_chargeUp", base.gameObject);
		if ((bool)GravitosAssets.LanceChargeEffect && (bool)muzzle)
		{
			chargeEffect = Object.Instantiate(GravitosAssets.LanceChargeEffect, muzzle.position, muzzle.rotation, muzzle);
			chargeEffect.transform.localScale = Vector3.one * 0.5f;
		}
		if ((bool)base.characterMotor)
		{
			base.characterMotor.walkSpeedPenaltyCoefficient = GravitosConfig.LanceChargeMoveSpeed.Value;
		}
	}

	public override void Update()
	{
		base.Update();
		if ((bool)chargeEffect)
		{
			chargeEffect.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, Mathf.Clamp01(base.age / chargeDuration));
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		StartAimMode();
		if (base.isAuthority)
		{
			base.characterBody.isSprinting = false;
			if (base.fixedAge >= chargeDuration)
			{
				outer.SetNextState(new FireGravitonLance());
			}
		}
	}

	public override void OnExit()
	{
		if ((bool)chargeEffect)
		{
			EntityState.Destroy(chargeEffect);
		}
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

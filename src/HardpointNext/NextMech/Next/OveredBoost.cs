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

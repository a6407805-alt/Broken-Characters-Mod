using EntityStates;
using NextMech.Components;
using UnityEngine;

namespace NextMech.SkillStates;

public class AssaultBoost : BaseSkillState
{
	private bool released;

	public override void OnEnter()
	{
		base.OnEnter();
		HPStatus hPStatus = HPStatus.Of(base.characterBody);
		if ((bool)hPStatus)
		{
			hPStatus.Spend(10f);
		}
		Anim.Loop(GetModelAnimator(), "AssaultBoost");
		CustomAudio.PlayNear(CustomAudio.Clip.AssaultBoost, base.transform.position, 70f);
		HPUtil.SpawnLocalEffect(HPAssets.Blink, base.characterBody.corePosition, 2.5f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority && (bool)base.characterMotor)
		{
			HPStatus hPStatus = HPStatus.Of(base.characterBody);
			Vector3 direction = GetAimRay().direction;
			direction.y = Mathf.Clamp(direction.y, -0.35f, 0.55f);
			direction.Normalize();
			if ((bool)base.characterMotor.Motor)
			{
				base.characterMotor.Motor.ForceUnground();
			}
			base.characterMotor.velocity = direction * HPConfig.AssaultSpeed.Value;
			Move.Face(base.characterDirection, Move.Flat(direction, base.transform.forward));
			if ((bool)hPStatus)
			{
				hPStatus.Spend(HPConfig.AssaultCost.Value * Time.fixedDeltaTime);
			}
			if ((bool)base.inputBank && !base.inputBank.skill3.down)
			{
				released = true;
			}
			if ((bool)base.inputBank && base.inputBank.jump.justPressed && base.fixedAge > 0.15f)
			{
				outer.SetNextState(new BoostKick
				{
					direction = Move.Flat(direction, base.transform.forward)
				});
			}
			else if (base.fixedAge >= HPConfig.AssaultMaxTime.Value || ((bool)hPStatus && hPStatus.redline) || (released && (bool)base.inputBank && base.inputBank.skill3.down))
			{
				outer.SetNextStateToMain();
			}
		}
	}

	public override void OnExit()
	{
		if (!outer.destroying)
		{
			Anim.Stop(GetModelAnimator());
		}
		if (base.isAuthority && (bool)base.characterMotor)
		{
			base.characterMotor.velocity *= 0.4f;
		}
		base.OnExit();
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.PrioritySkill;
	}
}

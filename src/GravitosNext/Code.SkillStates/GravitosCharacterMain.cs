using System.Collections.Generic;
using EntityStates;
using UnityEngine;

namespace GravitosNext.SkillStates;

public class GravitosCharacterMain : GenericCharacterMain
{
	private static readonly string[] MovementFloats = new string[3] { "walkSpeed", "forwardSpeed", "rightSpeed" };

	private static readonly string[] MovementBools = new string[2] { "isMoving", "isSprinting" };

	private readonly HashSet<string> animatorParameters = new HashSet<string>();

	public override void OnEnter()
	{
		base.OnEnter();
		Animator animator = base.modelAnimator;
		if ((bool)animator)
		{
			AnimatorControllerParameter[] parameters = animator.parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				animatorParameters.Add(animatorControllerParameter.name);
			}
		}
	}

	public override void UpdateAnimationParameters()
	{
		base.UpdateAnimationParameters();
		Animator animator = base.modelAnimator;
		if (!animator || !GravitosConfig.HoverIdleLegs.Value)
		{
			return;
		}
		string[] movementFloats = MovementFloats;
		foreach (string text in movementFloats)
		{
			if (animatorParameters.Contains(text))
			{
				animator.SetFloat(text, 0f);
			}
		}
		movementFloats = MovementBools;
		foreach (string text2 in movementFloats)
		{
			if (animatorParameters.Contains(text2))
			{
				animator.SetBool(text2, value: false);
			}
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority && (bool)base.characterMotor && (bool)base.inputBank && !base.characterMotor.isGrounded)
		{
			float value = GravitosConfig.GlideFallSpeed.Value;
			if (base.inputBank.jump.down && base.characterMotor.velocity.y < 0f - value)
			{
				Vector3 velocity = base.characterMotor.velocity;
				velocity.y = Mathf.MoveTowards(velocity.y, 0f - value, GravitosConfig.GlideBraking.Value * Time.fixedDeltaTime);
				base.characterMotor.velocity = velocity;
			}
		}
	}
}

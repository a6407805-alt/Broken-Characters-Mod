using EntityStates;
using NextMech.Components;
using UnityEngine;

namespace NextMech.SkillStates;

public class HPMain : GenericCharacterMain
{
	private float airTime;

	public override void HandleMovements()
	{
		base.HandleMovements();
		FaceCamera();
		// Gatling fire slows the frame down.
		HPStatus st = HPStatus.Of(base.characterBody);
		if (st && Time.time < st.gatlingSlowUntil && base.characterMotor)
		{
			base.characterMotor.moveDirection *= 0.55f;
		}
	}

	public override void Update()
	{
		base.Update();
		FaceCamera();
	}

	private void FaceCamera()
	{
		if (HPConfig.AcMovement.Value && base.isAuthority && (bool)base.characterDirection && (bool)base.inputBank)
		{
			Vector3 vector = base.inputBank.aimDirection;
			vector.y = 0f;
			if (vector.sqrMagnitude > 0.001f)
			{
				base.characterDirection.moveVector = vector.normalized;
			}
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (!base.isAuthority || !base.characterMotor || !base.inputBank)
		{
			return;
		}
		if (base.characterMotor.isGrounded)
		{
			airTime = 0f;
			return;
		}
		airTime += Time.fixedDeltaTime;
		HPStatus hPStatus = HPStatus.Of(base.characterBody);
		if ((bool)hPStatus && base.inputBank.jump.down && !(airTime < 0.3f) && hPStatus.CanSpend(0.5f))
		{
			float fixedDeltaTime = Time.fixedDeltaTime;
			hPStatus.Spend(HPConfig.HoverCost.Value * Garage.HoverCost(base.characterBody) * fixedDeltaTime);
			Vector3 velocity = base.characterMotor.velocity;
			velocity.y = Mathf.MoveTowards(Mathf.Max(velocity.y, -2f), HPConfig.HoverRise.Value, 30f * fixedDeltaTime);
			base.characterMotor.velocity = velocity;
		}
	}
}

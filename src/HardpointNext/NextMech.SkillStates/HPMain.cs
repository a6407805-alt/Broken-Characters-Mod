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

	private float lastVy;

	private bool wasAirborne;

	// AC4 movement: a NEXT doesn't fly, it floats. Low gravity in the air, a vertical booster (hold Jump) that
	// pushes you up with momentum instead of a flat hover, a slow capped fall, and air drift: with no input you
	// keep sliding; hold Sprint (boost) in the air to glide and steer.
	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (!base.isAuthority || !base.characterMotor || !base.inputBank)
		{
			return;
		}
		float dt = Time.fixedDeltaTime;
		HPStatus st = HPStatus.Of(base.characterBody);
		if (base.characterMotor.isGrounded)
		{
			if (wasAirborne && lastVy < -NextConfig.HeavyLandingSpeed.Value)
			{
				CustomAudio.PlayNear(CustomAudio.Clip.Stomp, base.transform.position, 50f);
				if (HPAssets.Slam) HPUtil.SpawnLocalEffect(HPAssets.Slam, base.characterBody.footPosition, 0.6f);
			}
			wasAirborne = false;
			airTime = 0f;
			if (st) st.thrusting = false;
			return;
		}
		wasAirborne = true;
		airTime += dt;
		Vector3 v = base.characterMotor.velocity;
		bool thrust = false;

		// 1. floaty descent: once you're coming down, give back part of the gravity the motor applies
		//    (jumps keep normal gravity going up, so a hop is still a hop)
		if (v.y < 0.5f) v.y += -Physics.gravity.y * (1f - NextConfig.AirGravity.Value) * dt;

		// 2. vertical booster: accelerate up, keep the momentum when you let go
		if (st && base.inputBank.jump.down && airTime >= 0.18f && st.CanSpend(0.5f))
		{
			st.Spend(HPConfig.HoverCost.Value * Garage.HoverCost(base.characterBody) * dt);
			float rise = NextConfig.RiseSpeed.Value * Tuning.Mult(Tuning.Stat.MainBooster);
			if (v.y < rise) v.y = Mathf.Min(rise, v.y + NextConfig.VerticalThrust.Value * dt);
			thrust = true;
		}

		// 3. air boost: hold Sprint in the air to glide/steer; without it you drift on your momentum
		Vector3 move = base.inputBank.moveVector;
		move.y = 0f;
		if (st && base.inputBank.sprint.down && move.sqrMagnitude > 0.01f && st.CanSpend(0.5f))
		{
			Vector3 h = new Vector3(v.x, 0f, v.z);
			Vector3 target = move.normalized * base.characterBody.moveSpeed * NextConfig.AirBoostSpeed.Value;
			h = Vector3.MoveTowards(h, target, NextConfig.AirBoostAccel.Value * dt);
			v.x = h.x;
			v.z = h.z;
			st.Spend(NextConfig.AirBoostCost.Value * dt);
			thrust = true;
		}

		// 4. slow, capped fall
		v.y = Mathf.Max(v.y, -NextConfig.FallCap.Value);
		base.characterMotor.velocity = v;
		lastVy = v.y;
		if (st) st.thrusting = thrust;
	}
}

using EntityStates;
using NextMech.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

// AC4 quick boost: no charges, only EN. Chain them back to back; press again right after a boost for a
// second-stage quick boost (much further, costs more). Also cancels the Overed Boost.
public class QuickBoost : BaseSkillState
{
	private const float Dash = 0.18f;

	private const float Duration = 0.3f;

	private Vector3 direction;

	private bool secondStage;

	private float distance;

	private bool braked;

	public override void OnEnter()
	{
		base.OnEnter();
		HPStatus hPStatus = HPStatus.Of(base.characterBody);
		NextPilot pilot = NextPilot.Of(base.characterBody);
		if (base.isAuthority)
		{
			secondStage = pilot && !pilot.lastQbWasSecond && Time.time - pilot.lastQbTime <= NextConfig.SecondStageWindow.Value;
			if (pilot)
			{
				pilot.lastQbTime = Time.time;
				pilot.lastQbWasSecond = secondStage;
			}
			Vector3 v = (base.inputBank ? base.inputBank.moveVector : Vector3.zero);
			direction = Move.Flat(v, Move.Flat(GetAimRay().direction, base.transform.forward));
			distance = HPConfig.QuickBoostDistance.Value * Garage.QbDistance(base.characterBody) * (secondStage ? NextConfig.SecondStageDistance.Value : 1f);
			if ((bool)hPStatus)
			{
				hPStatus.Spend(NextConfig.QbCost.Value * Garage.QbCost(base.characterBody) * (secondStage ? NextConfig.SecondStageCost.Value : 1f));
			}
		}
		Vector3 vector = (base.characterDirection ? base.characterDirection.forward : base.transform.forward);
		float num = Vector3.Dot(vector, direction);
		float num2 = Vector3.Dot(Vector3.Cross(Vector3.up, vector), direction);
		string clip = ((!(Mathf.Abs(num) >= Mathf.Abs(num2))) ? ((num2 >= 0f) ? "QuickBoostR" : "QuickBoostL") : ((num >= 0f) ? "QuickBoostF" : "QuickBoostB"));
		Anim.Play(GetModelAnimator(), "FullBody, Override", clip);
		CustomAudio.PlayNear(CustomAudio.Clip.Boost, base.transform.position, 60f);
		HPUtil.SpawnLocalEffect(HPAssets.Blink, base.characterBody.corePosition, secondStage ? 3f : 1.5f);
		if (secondStage && HPAssets.Spark)
		{
			HPUtil.SpawnLocalEffect(HPAssets.Spark, base.characterBody.corePosition - direction * 1.5f, 2f);
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority && (bool)base.characterMotor)
		{
			if (base.fixedAge < Dash)
			{
				Vector3 vector = direction * (distance / Dash);
				base.characterMotor.velocity = new Vector3(vector.x, Mathf.Max(base.characterMotor.velocity.y, 0f), vector.z);
			}
			else if (!braked)
			{
				braked = true;
				// keep some momentum so chained boosts flow into each other
				base.characterMotor.velocity = new Vector3(base.characterMotor.velocity.x * 0.35f, base.characterMotor.velocity.y, base.characterMotor.velocity.z * 0.35f);
			}
			if (base.fixedAge >= Duration)
			{
				outer.SetNextStateToMain();
			}
		}
	}

	public override void OnSerialize(NetworkWriter writer)
	{
		base.OnSerialize(writer);
		writer.Write(direction);
		writer.Write(secondStage);
	}

	public override void OnDeserialize(NetworkReader reader)
	{
		base.OnDeserialize(reader);
		direction = reader.ReadVector3();
		secondStage = reader.ReadBoolean();
	}

	// During the dash nothing cuts in; once it's braking, the next quick boost can (chaining).
	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return base.fixedAge < Dash ? InterruptPriority.PrioritySkill : InterruptPriority.Skill;
	}
}

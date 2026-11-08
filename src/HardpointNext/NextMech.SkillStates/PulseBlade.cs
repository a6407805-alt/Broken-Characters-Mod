using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

public class PulseBlade : BaseSkillState
{
	public int step = 1;

	private const float LungeStart = 0.15f;

	private const float HitTime = 0.42f;

	private const float Duration = 0.85f;

	private Vector3 forward;

	private bool hit;

	private bool queued;

	private bool released;

	public override void OnEnter()
	{
		base.OnEnter();
		forward = Move.Flat(GetAimRay().direction, base.transform.forward);
		Move.Face(base.characterDirection, forward);
		Anim.Play(GetModelAnimator(), "FullBody, Override", (step == 1) ? "Slash1" : "Slash2");
		CustomAudio.PlayNear((step == 1) ? CustomAudio.Clip.BladeOn : CustomAudio.Clip.BladeSwing, base.transform.position, 60f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		Move.Face(base.characterDirection, forward);
		if (base.isAuthority && (bool)base.characterMotor)
		{
			Vector3 velocity = base.characterMotor.velocity;
			if (base.fixedAge >= 0.15f && base.fixedAge < 0.42f)
			{
				Vector3 vector = forward * (HPConfig.BladeLunge.Value / 0.26999998f);
				base.characterMotor.velocity = new Vector3(vector.x, Mathf.Max(velocity.y, 0f), vector.z);
			}
			else
			{
				base.characterMotor.velocity = new Vector3(velocity.x * 0.8f, velocity.y, velocity.z * 0.8f);
			}
			if ((bool)base.inputBank)
			{
				if (!Arsenal.Held(this))
				{
					released = true;
				}
				else if (released && step == 1 && base.fixedAge > 0.25f)
				{
					queued = true;
				}
			}
		}
		if (!hit && base.fixedAge >= 0.42f)
		{
			hit = true;
			CustomAudio.PlayNear(CustomAudio.Clip.BladeSwing, base.transform.position, 60f);
			if (NetworkServer.active)
			{
				Slash();
			}
		}
		if (base.isAuthority)
		{
			if (queued && base.fixedAge >= 0.6f)
			{
				outer.SetNextState(new PulseBlade
				{
					step = 2
				});
			}
			else if (base.fixedAge >= 0.85f)
			{
				outer.SetNextStateToMain();
			}
		}
	}

	private void Slash()
	{
		float value = HPConfig.BladeRadius.Value;
		Vector3 center = base.characterBody.corePosition + forward * value * 0.55f;
		bool crit = RollCrit();
		foreach (CharacterBody item in HPUtil.FindEnemies(base.teamComponent.teamIndex, center, value))
		{
			Vector3 to = item.corePosition - base.characterBody.corePosition;
			to.y = 0f;
			if (!(to.sqrMagnitude > 1f) || !(Vector3.Angle(forward, to) > 80f))
			{
				HPUtil.DealDamage(base.gameObject, item.healthComponent, damageStat * HPConfig.BladeDamage.Value, crit, item.corePosition, forward * 1800f + Vector3.up * 400f, 1f);
				HPUtil.SpawnEffect(HPAssets.SlashHit, item.corePosition, 2f);
			}
		}
	}

	public override void OnSerialize(NetworkWriter writer)
	{
		base.OnSerialize(writer);
		writer.Write(step);
	}

	public override void OnDeserialize(NetworkReader reader)
	{
		base.OnDeserialize(reader);
		step = reader.ReadInt32();
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.PrioritySkill;
	}
}

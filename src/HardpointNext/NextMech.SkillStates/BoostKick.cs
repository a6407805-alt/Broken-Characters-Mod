using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

public class BoostKick : BaseSkillState
{
	public Vector3 direction;

	private const float HitTime = 0.26f;

	private const float Duration = 0.6f;

	private bool hit;

	public override void OnEnter()
	{
		base.OnEnter();
		Anim.Play(GetModelAnimator(), "FullBody, Override", "BoostKick");
		CustomAudio.PlayNear(CustomAudio.Clip.Boost, base.transform.position, 60f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		Move.Face(base.characterDirection, direction);
		if (base.isAuthority && (bool)base.characterMotor)
		{
			base.characterMotor.velocity = ((base.fixedAge < 0.31f) ? (direction * 24f + Vector3.up * 2f) : (base.characterMotor.velocity * 0.9f));
		}
		if (!hit && base.fixedAge >= 0.26f)
		{
			hit = true;
			CustomAudio.PlayNear(CustomAudio.Clip.Kick, base.transform.position, 70f);
			if (NetworkServer.active)
			{
				bool crit = RollCrit();
				foreach (CharacterBody item in HPUtil.FindEnemies(base.teamComponent.teamIndex, base.characterBody.corePosition + direction * 2f, HPConfig.KickRadius.Value))
				{
					HPUtil.DealDamage(base.gameObject, item.healthComponent, damageStat * HPConfig.KickDamage.Value, crit, item.corePosition, direction * 4000f + Vector3.up * 1500f, 1f);
					HPUtil.SpawnEffect(HPAssets.Slam, item.corePosition, 2f);
				}
			}
		}
		if (base.isAuthority && base.fixedAge >= 0.6f)
		{
			outer.SetNextStateToMain();
		}
	}

	public override void OnSerialize(NetworkWriter writer)
	{
		base.OnSerialize(writer);
		writer.Write(direction);
	}

	public override void OnDeserialize(NetworkReader reader)
	{
		base.OnDeserialize(reader);
		direction = reader.ReadVector3();
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.PrioritySkill;
	}
}

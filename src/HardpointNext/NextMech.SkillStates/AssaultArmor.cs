using System;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

public class AssaultArmor : BaseSkillState
{
	private const float BlastTime = 1.15f;

	private const float Duration = 2f;

	private bool blasted;

	public override void OnEnter()
	{
		base.OnEnter();
		Anim.Play(GetModelAnimator(), "FullBody, Override", "AssaultArmor");
		CustomAudio.PlayNear(CustomAudio.Clip.ArmorVoice, base.transform.position, 40f);
		CustomAudio.PlayNear(CustomAudio.Clip.ArmorCharge, base.transform.position, 80f);
		if (NetworkServer.active && (bool)HPContent.ArmorBuff)
		{
			base.characterBody.AddBuff(HPContent.ArmorBuff);
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority && (bool)base.characterMotor)
		{
			base.characterMotor.velocity = new Vector3(0f, Mathf.Min(0f, base.characterMotor.velocity.y) * 0.5f, 0f);
		}
		if (!blasted && base.fixedAge >= 1.15f)
		{
			blasted = true;
			CustomAudio.PlayNear(CustomAudio.Clip.ArmorBlast, base.transform.position, 150f);
			if (NetworkServer.active)
			{
				if ((bool)HPContent.ArmorBuff)
				{
					base.characterBody.RemoveBuff(HPContent.ArmorBuff);
				}
				Blast();
			}
		}
		if (base.isAuthority && base.fixedAge >= 2f)
		{
			outer.SetNextStateToMain();
		}
	}

	private void Blast()
	{
		float value = HPConfig.ArmorRadius.Value;
		try
		{
			BlastAttack blastAttack = new BlastAttack();
			blastAttack.attacker = base.gameObject;
			blastAttack.inflictor = base.gameObject;
			blastAttack.teamIndex = base.teamComponent.teamIndex;
			// AC4: Assault Armor dumps the whole Primal Armor; a full field hits harder.
			blastAttack.baseDamage = damageStat * HPConfig.ArmorDamage.Value * (0.6f + 0.8f * PrimalArmor.Fraction(base.characterBody));
			PrimalArmor.Empty(base.characterBody);
			blastAttack.baseForce = 3500f;
			blastAttack.bonusForce = Vector3.up * 800f;
			blastAttack.position = base.characterBody.corePosition;
			blastAttack.radius = value;
			blastAttack.falloffModel = BlastAttack.FalloffModel.None;
			blastAttack.crit = RollCrit();
			blastAttack.procCoefficient = 1f;
			blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
			blastAttack.Fire();
		}
		catch (Exception ex)
		{
			HPPlugin.Log.LogWarning("Assault Armor blast threw: " + ex.Message);
		}
		HPUtil.SpawnEffect(HPAssets.BigExplosion, base.characterBody.corePosition, value);
		HPUtil.SpawnEffect(HPAssets.Explosion, base.characterBody.corePosition, value * 0.6f);
	}

	public override void OnExit()
	{
		if (NetworkServer.active && (bool)HPContent.ArmorBuff && base.characterBody.HasBuff(HPContent.ArmorBuff))
		{
			base.characterBody.RemoveBuff(HPContent.ArmorBuff);
		}
		base.OnExit();
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Stun;
	}
}

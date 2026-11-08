using System;
using System.Collections.Generic;
using System.Linq;
using EntityStates;
using NextMech.Components;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace NextMech.SkillStates;

public class MissileFire : BaseSkillState
{
	private const float First = 0.4f;

	private const float Gap = 0.06f;

	private const float Duration = 1.4f;

	private int fired;

	private List<HurtBox> targets;

	public override void OnEnter()
	{
		base.OnEnter();
		HPStatus hPStatus = HPStatus.Of(base.characterBody);
		targets = (hPStatus ? Enumerable.ToList<HurtBox>(Enumerable.Where<HurtBox>((IEnumerable<HurtBox>)hPStatus.locks, (Func<HurtBox, bool>)((HurtBox h) => h))) : new List<HurtBox>());
		Anim.Play(GetModelAnimator(), "Gesture, Override", "Missiles");
		CustomAudio.PlayNear(CustomAudio.Clip.MissilesVoice, base.transform.position, 40f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		int num = Mathf.Max(1, HPConfig.MissileCount.Value);
		while (fired < num && base.fixedAge >= 0.4f + (float)fired * 0.06f)
		{
			Fire(fired);
			fired++;
		}
		if (base.fixedAge >= 1.4f)
		{
			HPStatus hPStatus = HPStatus.Of(base.characterBody);
			if ((bool)hPStatus)
			{
				hPStatus.locks.Clear();
			}
			if (base.isAuthority)
			{
				outer.SetNextStateToMain();
			}
		}
	}

	private void Fire(int index)
	{
		CustomAudio.PlayNear(CustomAudio.Clip.Missile, base.transform.position, 60f);
		if (base.isAuthority && (bool)HPContent.MissileProjectile)
		{
			ChildLocator modelChildLocator = GetModelChildLocator();
			Transform transform = (modelChildLocator ? modelChildLocator.FindChild((index % 2 == 0) ? "PodL" : "PodR") : null);
			Vector3 position = (transform ? (transform.position + Vector3.up * 0.6f) : (base.characterBody.corePosition + Vector3.up * 2f));
			Vector3 normalized = (GetAimRay().direction + Vector3.up * 0.9f + UnityEngine.Random.insideUnitSphere * 0.35f).normalized;
			GameObject target = ((targets.Count > 0 && (bool)targets[index % targets.Count]) ? targets[index % targets.Count].gameObject : null);
			ProjectileManager.instance.FireProjectile(new FireProjectileInfo
			{
				projectilePrefab = HPContent.MissileProjectile,
				position = position,
				rotation = Quaternion.LookRotation(normalized),
				owner = base.gameObject,
				damage = damageStat * HPConfig.MissileDamage.Value,
				force = 600f,
				crit = RollCrit(),
				target = target
			});
		}
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Skill;
	}
}

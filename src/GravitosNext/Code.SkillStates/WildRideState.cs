using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace GravitosNext.SkillStates;

public class WildRideState : BaseSkillState
{
	private const float Duration = 0.5f;

	private const float HoverHeight = 4f;

	public override void OnEnter()
	{
		base.OnEnter();
		StartAimMode();
		PlayAnimation("Gesture, Additive", "FireMineRight");
		Util.PlaySound("Play_item_use_BFG_fire", base.gameObject);
		if (base.isAuthority && (bool)GravitosContent.BlackHolePrefab)
		{
			Ray aimRay = GetAimRay();
			float value = GravitosConfig.WildRideCastRange.Value;
			RaycastHit hitInfo;
			Vector3 position = (Physics.Raycast(aimRay, out hitInfo, value, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ? (hitInfo.point + hitInfo.normal * 4f) : aimRay.GetPoint(value * 0.5f));
			ProjectileManager.instance.FireProjectile(new FireProjectileInfo
			{
				projectilePrefab = GravitosContent.BlackHolePrefab,
				position = position,
				rotation = Quaternion.identity,
				owner = base.gameObject,
				damage = damageStat,
				crit = RollCrit(),
				force = 0f
			});
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority && base.fixedAge >= 0.5f)
		{
			outer.SetNextStateToMain();
		}
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.PrioritySkill;
	}
}

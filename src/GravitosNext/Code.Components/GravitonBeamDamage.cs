using System.Collections.Generic;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.Components;

public class GravitonBeamDamage
{
	private const float ChainCooldown = 0.5f;

	private const float SlamCooldown = 2f;

	private readonly GameObject owner;

	private readonly Dictionary<HealthComponent, float> nextChain = new Dictionary<HealthComponent, float>();

	private readonly Dictionary<HealthComponent, float> nextSlam = new Dictionary<HealthComponent, float>();

	public GravitonBeamDamage(GameObject owner)
	{
		this.owner = owner;
	}

	public void FireBeam(Ray ray, float length, float radius, float splashRadius, float damage, float slamDamage, bool crit, TeamIndex team, float now, HashSet<HealthComponent> hitThisTick)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		float value = GravitosConfig.LanceProcCoefficient.Value;
		BulletAttack bulletAttack = new BulletAttack
		{
			owner = owner,
			weapon = owner,
			origin = ray.origin,
			aimVector = ray.direction,
			minSpread = 0f,
			maxSpread = 0f,
			bulletCount = 1u,
			damage = damage,
			procCoefficient = value,
			force = 150f,
			falloffModel = BulletAttack.FalloffModel.None,
			maxDistance = length,
			radius = radius,
			isCrit = crit,
			smartCollision = true,
			stopperMask = LayerIndex.world.mask,
			hitMask = LayerIndex.CommonMasks.bullet
		};
		BulletAttack.HitCallback vanillaHit = bulletAttack.hitCallback ?? PrivateAccess.DefaultHitCallback;
		bulletAttack.hitCallback = delegate(BulletAttack attack, ref BulletAttack.BulletHit hit)
		{
			HealthComponent healthComponent = (hit.hitHurtBox ? hit.hitHurtBox.healthComponent : null);
			if (!healthComponent || hitThisTick.Add(healthComponent))
			{
				bool result = vanillaHit != null && vanillaHit(attack, ref hit);
				if ((bool)healthComponent && GravityUtil.IsEnemy(team, healthComponent.body))
				{
					OnHit(healthComponent, hit.point, damage, slamDamage, crit, team, now);
				}
				return result;
			}
			return false;
		};
		bulletAttack.Fire();
		Vector3 point = ray.GetPoint(length);
		foreach (CharacterBody item in GravityUtil.FindEnemies(team, point, splashRadius))
		{
			if (hitThisTick.Add(item.healthComponent))
			{
				GravityUtil.DealDamage(owner, item.healthComponent, damage, crit, item.corePosition, Vector3.zero, value);
				OnHit(item.healthComponent, item.corePosition, damage, slamDamage, crit, team, now);
			}
		}
	}

	private void OnHit(HealthComponent victim, Vector3 point, float tickDamage, float slamDamage, bool crit, TeamIndex team, float now)
	{
		int value = GravitosConfig.LanceChainBounces.Value;
		if (value > 0 && (!nextChain.TryGetValue(victim, out var value2) || now >= value2))
		{
			nextChain[victim] = now + 0.5f;
			LightningOrb lightningOrb = new LightningOrb
			{
				origin = point,
				damageValue = tickDamage * GravitosConfig.LanceChainFraction.Value,
				isCrit = crit,
				bouncesRemaining = value - 1,
				teamIndex = team,
				attacker = owner,
				inflictor = owner,
				procCoefficient = 0.3f,
				lightningType = LightningOrb.LightningType.Ukulele,
				damageColorIndex = DamageColorIndex.Item,
				range = GravitosConfig.LanceChainRange.Value,
				bouncedObjects = new List<HealthComponent> { victim }
			};
			HurtBox hurtBox = lightningOrb.PickNextTarget(point);
			if ((bool)hurtBox)
			{
				lightningOrb.target = hurtBox;
				OrbManager.instance.AddOrb(lightningOrb);
			}
		}
		if (victim.alive && GravityUtil.IsSmall(victim.body) && (!nextSlam.TryGetValue(victim, out var value3) || now >= value3))
		{
			nextSlam[victim] = now + 2f;
			GravityLiftSlam.Apply(victim.body, owner, slamDamage, crit, 0.5f, 0.35f);
		}
	}
}

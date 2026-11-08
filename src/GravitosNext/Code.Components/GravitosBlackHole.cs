using System.Collections.Generic;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.Components;

[RequireComponent(typeof(ProjectileController))]
public class GravitosBlackHole : MonoBehaviour
{
	private const float DamageTickInterval = 0.5f;

	private const float RemotePushInterval = 0.2f;

	private const float CoreRadius = 1.5f;

	private ProjectileController projectileController;

	private ProjectileDamage projectileDamage;

	private float age;

	private float damageTimer;

	private float pushTimer;

	private bool exploded;

	private GameObject Owner => projectileController.owner;

	private TeamIndex Team
	{
		get
		{
			if (!projectileController.teamFilter)
			{
				return TeamIndex.Player;
			}
			return projectileController.teamFilter.teamIndex;
		}
	}

	private float BaseDamage
	{
		get
		{
			if (!projectileDamage)
			{
				return 12f;
			}
			return projectileDamage.damage;
		}
	}

	private bool Crit
	{
		get
		{
			if ((bool)projectileDamage)
			{
				return projectileDamage.crit;
			}
			return false;
		}
	}

	private void Awake()
	{
		projectileController = GetComponent<ProjectileController>();
		projectileDamage = GetComponent<ProjectileDamage>();
	}

	private void FixedUpdate()
	{
		if (!NetworkServer.active || exploded)
		{
			return;
		}
		float fixedDeltaTime = Time.fixedDeltaTime;
		age += fixedDeltaTime;
		if (age >= GravitosConfig.WildRideDuration.Value)
		{
			Explode();
			return;
		}
		pushTimer -= fixedDeltaTime;
		bool flag = pushTimer <= 0f;
		if (flag)
		{
			pushTimer = 0.2f;
		}
		Pull(flag);
		damageTimer -= fixedDeltaTime;
		if (damageTimer <= 0f)
		{
			damageTimer += 0.5f;
			DamageTick();
		}
	}

	private List<CharacterBody> Targets(float radius)
	{
		List<CharacterBody> list = new List<CharacterBody>();
		float num = radius * radius;
		Vector3 position = base.transform.position;
		bool value = GravitosConfig.WildRideHitsTeammates.Value;
		bool value2 = GravitosConfig.WildRideAffectsSelf.Value;
		foreach (CharacterBody readOnlyInstances in CharacterBody.readOnlyInstancesList)
		{
			if (!readOnlyInstances || !readOnlyInstances.healthComponent || !readOnlyInstances.healthComponent.alive || (readOnlyInstances.corePosition - position).sqrMagnitude > num)
			{
				continue;
			}
			if (readOnlyInstances.gameObject == Owner)
			{
				if (!value2)
				{
					continue;
				}
			}
			else if (!value && !GravityUtil.IsEnemy(Team, readOnlyInstances))
			{
				continue;
			}
			list.Add(readOnlyInstances);
		}
		return list;
	}

	private void Pull(bool pushRemote)
	{
		float value = GravitosConfig.WildRidePullRadius.Value;
		float value2 = GravitosConfig.WildRidePullSpeed.Value;
		Vector3 position = base.transform.position;
		foreach (CharacterBody item in Targets(value))
		{
			Vector3 vector = position - item.corePosition;
			float magnitude = vector.magnitude;
			Vector3 vector2 = ((magnitude > 0.01f) ? (vector / magnitude) : Vector3.zero);
			float num = value2 * Mathf.Lerp(1.5f, 0.6f, magnitude / value);
			if (GravityUtil.IsBig(item))
			{
				num *= 0.5f;
			}
			if (item.hasEffectiveAuthority)
			{
				Vector3 velocity = ((magnitude < 1.5f) ? Vector3.zero : (vector2 * Mathf.Min(num, magnitude * 4f)));
				GravityUtil.SetVelocity(item, velocity);
			}
			else if (pushRemote && magnitude > 1.5f)
			{
				GravityUtil.AddVelocity(item, vector2 * num * 0.5f);
			}
		}
	}

	private void DamageTick()
	{
		float damage = BaseDamage * GravitosConfig.WildRideDamagePerSecond.Value * 0.5f;
		foreach (CharacterBody item in Targets(GravitosConfig.WildRideDamageRadius.Value))
		{
			Hit(item, damage, 0.25f, Vector3.zero);
		}
	}

	private void Explode()
	{
		exploded = true;
		float value = GravitosConfig.WildRideExplosionRadius.Value;
		float damage = BaseDamage * GravitosConfig.WildRideExplosionDamage.Value;
		Vector3 position = base.transform.position;
		foreach (CharacterBody item in Targets(value))
		{
			Vector3 normalized = (item.corePosition - position).normalized;
			Hit(item, damage, 1f, normalized * 4000f);
		}
		GravityUtil.SpawnEffect(GravitosAssets.ExplosionEffect, position, value);
		Object.Destroy(base.gameObject);
	}

	private void Hit(CharacterBody body, float damage, float procCoefficient, Vector3 force)
	{
		if (!GravityUtil.IsEnemy(Team, body))
		{
			damage *= GravitosConfig.WildRideTeammateDamageMultiplier.Value;
			procCoefficient = 0f;
		}
		GravityUtil.DealDamage(Owner, body.healthComponent, damage, Crit, body.corePosition, force, procCoefficient);
	}
}

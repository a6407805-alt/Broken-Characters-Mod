using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext;

internal static class GravityUtil
{
	public static bool IsSmall(CharacterBody body)
	{
		if ((bool)body && !body.isChampion && !body.isBoss)
		{
			return body.hullClassification == HullClassification.Human;
		}
		return false;
	}

	public static bool IsBig(CharacterBody body)
	{
		if ((bool)body)
		{
			if (!body.isChampion)
			{
				return body.isBoss;
			}
			return true;
		}
		return false;
	}

	public static float Falloff(float distance, float range, float min)
	{
		return Mathf.Lerp(1f, min, Mathf.Clamp01(distance / Mathf.Max(range, 0.01f)));
	}

	public static float ShieldPullMultiplier(CharacterBody body)
	{
		if (!body)
		{
			return 1f;
		}
		float num = Mathf.Max(1f, GravitosConfig.PullShieldPerDoubling.Value);
		float p = (body.maxShield - GravitosConfig.PullShieldBaseline.Value) / num;
		return Mathf.Min(GravitosConfig.PullMaxStrength.Value, Mathf.Pow(2f, p));
	}

	public static Vector3 GetVelocity(CharacterBody body)
	{
		if ((bool)body.characterMotor)
		{
			return body.characterMotor.velocity;
		}
		if ((bool)body.rigidbody)
		{
			return body.rigidbody.velocity;
		}
		return Vector3.zero;
	}

	public static void SetVelocity(CharacterBody body, Vector3 velocity)
	{
		if (!NetworkServer.active || !body)
		{
			return;
		}
		if (!body.hasEffectiveAuthority)
		{
			AddVelocity(body, velocity - GetVelocity(body));
			return;
		}
		CharacterMotor characterMotor = body.characterMotor;
		if ((bool)characterMotor)
		{
			if ((bool)characterMotor.Motor)
			{
				characterMotor.Motor.ForceUnground();
			}
			characterMotor.disableAirControlUntilCollision = true;
			characterMotor.velocity = velocity;
		}
		else if ((bool)body.rigidbody && !body.rigidbody.isKinematic)
		{
			body.rigidbody.velocity = velocity;
		}
	}

	public static void AddVelocity(CharacterBody body, Vector3 deltaVelocity)
	{
		if (NetworkServer.active && (bool)body && (bool)body.healthComponent)
		{
			float num = (body.characterMotor ? body.characterMotor.mass : (body.rigidbody ? body.rigidbody.mass : 1f));
			body.healthComponent.TakeDamageForce(deltaVelocity * num, alwaysApply: true);
		}
	}

	public static bool IsEnemy(TeamIndex attackerTeam, CharacterBody victim)
	{
		if ((bool)victim && (bool)victim.teamComponent)
		{
			return victim.teamComponent.teamIndex != attackerTeam;
		}
		return false;
	}

	public static void DealDamage(GameObject attacker, HealthComponent victim, float damage, bool crit, Vector3 position, Vector3 force, float procCoefficient, DamageType damageType = DamageType.Generic, DamageColorIndex color = DamageColorIndex.Default)
	{
		if (NetworkServer.active && (bool)victim && victim.alive && !(damage <= 0f))
		{
			DamageInfo damageInfo = new DamageInfo
			{
				attacker = attacker,
				inflictor = attacker,
				damage = damage,
				crit = crit,
				position = position,
				force = force,
				procCoefficient = procCoefficient,
				damageType = damageType,
				damageColorIndex = color,
				procChainMask = default(ProcChainMask)
			};
			victim.TakeDamage(damageInfo);
			CharacterBody characterBody = (attacker ? attacker.GetComponent<CharacterBody>() : null);
			if ((!characterBody || IsEnemy(characterBody.teamComponent.teamIndex, victim.body)) && procCoefficient > 0f)
			{
				GlobalEventManager.instance.OnHitEnemy(damageInfo, victim.gameObject);
			}
			GlobalEventManager.instance.OnHitAll(damageInfo, victim.gameObject);
		}
	}

	public static List<CharacterBody> FindEnemies(TeamIndex team, Vector3 center, float radius)
	{
		List<CharacterBody> list = new List<CharacterBody>();
		float num = radius * radius;
		foreach (CharacterBody readOnlyInstances in CharacterBody.readOnlyInstancesList)
		{
			if ((bool)readOnlyInstances && (bool)readOnlyInstances.healthComponent && readOnlyInstances.healthComponent.alive && IsEnemy(team, readOnlyInstances) && !((readOnlyInstances.corePosition - center).sqrMagnitude > num))
			{
				list.Add(readOnlyInstances);
			}
		}
		return list;
	}

	public static void SpawnEffect(GameObject prefab, Vector3 origin, float scale, Vector3? start = null)
	{
		if ((bool)prefab)
		{
			EffectData effectData = new EffectData
			{
				origin = origin,
				scale = scale
			};
			if (start.HasValue)
			{
				effectData.start = start.Value;
			}
			EffectManager.SpawnEffect(prefab, effectData, transmit: true);
		}
	}

	public static Vector3 SnapToGround(Vector3 point)
	{
		if (Physics.Raycast(point + Vector3.up * 6f, Vector3.down, out var hitInfo, 40f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			return hitInfo.point;
		}
		return point;
	}
}

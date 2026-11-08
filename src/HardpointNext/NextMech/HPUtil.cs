using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NextMech.Components;
using Rewired;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech;

internal static class HPUtil
{
	private const BindingFlags AnyInstance = (BindingFlags)52;

	private static BodyIndex myIndex = BodyIndex.None;

	private static readonly FieldInfo StopwatchField = typeof(GenericSkill).GetField("rechargeStopwatch", (BindingFlags)52);

	private static readonly Dictionary<ValueTuple<Type, string>, PropertyInfo> syncSetters = new Dictionary<ValueTuple<Type, string>, PropertyInfo>();

	private static bool damageErrorLogged;

	private static Material lineMaterial;

	public static bool SetField(object target, string field, object value)
	{
		Type type = target.GetType();
		while (type != null)
		{
			FieldInfo field2 = type.GetField(field, (BindingFlags)54);
			if (!(field2 == (FieldInfo)null))
			{
				try
				{
					field2.SetValue(target, value);
					return true;
				}
				catch (Exception ex)
				{
					HPPlugin.Log.LogWarning("Could not set " + ((MemberInfo)type).Name + "." + field + ": " + ex.Message);
					return false;
				}
			}
			type = type.BaseType;
		}
		HPPlugin.Log.LogWarning("Field " + ((MemberInfo)target.GetType()).Name + "." + field + " not found.");
		return false;
	}

	public static T GetField<T>(object target, string field) where T : class
	{
		Type type = target.GetType();
		while (type != null)
		{
			FieldInfo field2 = type.GetField(field, (BindingFlags)54);
			if (field2 != (FieldInfo)null)
			{
				return field2.GetValue(target) as T;
			}
			type = type.BaseType;
		}
		return null;
	}

	public static bool IsMine(CharacterBody body)
	{
		if (!body || body.bodyIndex == BodyIndex.None)
		{
			return false;
		}
		if (myIndex == BodyIndex.None && (bool)HPContent.BodyPrefab)
		{
			myIndex = BodyCatalog.FindBodyIndex(HPContent.BodyPrefab);
		}
		return body.bodyIndex == myIndex;
	}

	public static void AddCooldown(GenericSkill skill, float seconds)
	{
		if (!skill || StopwatchField == (FieldInfo)null)
		{
			return;
		}
		try
		{
			StopwatchField.SetValue((object)skill, (object)((float)StopwatchField.GetValue((object)skill) - seconds));
		}
		catch (Exception ex)
		{
			HPPlugin.Log.LogWarning("Could not lengthen the cooldown: " + ex.Message);
		}
	}

	public static Player LocalInput()
	{
		try
		{
			return LocalUserManager.GetFirstLocalUser()?.inputPlayer;
		}
		catch
		{
			return null;
		}
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

	public static void SetSynced(object target, string field, float value)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		Type type = target.GetType();
		if (!syncSetters.TryGetValue(new ValueTuple<Type, string>(type, field), out var value2))
		{
			value2 = (syncSetters[new ValueTuple<Type, string>(type, field)] = type.GetProperty("Network" + field, (BindingFlags)52));
		}
		try
		{
			if (value2 != (PropertyInfo)null && value2.CanWrite)
			{
				value2.SetValue(target, (object)value);
			}
			else
			{
				SetField(target, field, value);
			}
		}
		catch (Exception ex)
		{
			HPPlugin.Log.LogWarning("Could not set " + ((MemberInfo)type).Name + "." + field + ": " + ex.Message);
		}
	}

	public static void Teleport(CharacterBody body, Vector3 foot)
	{
		if (NetworkServer.active && (bool)body && body.hasEffectiveAuthority)
		{
			Vector3 position = foot + (body.transform.position - body.footPosition);
			CharacterMotor characterMotor = body.characterMotor;
			if ((bool)characterMotor && (bool)characterMotor.Motor)
			{
				characterMotor.Motor.SetPosition(position);
				characterMotor.velocity = Vector3.zero;
			}
			else if ((bool)body.rigidbody)
			{
				body.rigidbody.position = position;
				body.rigidbody.velocity = Vector3.zero;
				body.transform.position = position;
			}
			else
			{
				body.transform.position = position;
			}
		}
	}

	public static void MoveSelf(CharacterBody body, Vector3 foot)
	{
		if ((bool)body && body.hasEffectiveAuthority)
		{
			Vector3 position = foot + (body.transform.position - body.footPosition);
			CharacterMotor characterMotor = body.characterMotor;
			if ((bool)characterMotor && (bool)characterMotor.Motor)
			{
				characterMotor.Motor.SetPosition(position);
				characterMotor.velocity = Vector3.zero;
			}
			else
			{
				body.transform.position = position;
			}
		}
	}

	public static void Stun(GameObject attacker, CharacterBody victim)
	{
		if (!NetworkServer.active || !victim || !victim.healthComponent || !victim.healthComponent.alive)
		{
			return;
		}
		DamageInfo damageInfo = new DamageInfo
		{
			attacker = attacker,
			inflictor = attacker,
			damage = 0.01f,
			position = victim.corePosition,
			procCoefficient = 1f,
			damageType = (DamageType.NonLethal | DamageType.Stun1s | DamageType.Silent),
			damageColorIndex = DamageColorIndex.Default
		};
		try
		{
			victim.healthComponent.TakeDamage(damageInfo);
		}
		catch (Exception)
		{
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
		if (!NetworkServer.active || !victim || !victim.alive || damage <= 0f)
		{
			return;
		}
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
		try
		{
			victim.TakeDamage(damageInfo);
			CharacterBody characterBody = (attacker ? attacker.GetComponent<CharacterBody>() : null);
			if ((!characterBody || IsEnemy(characterBody.teamComponent.teamIndex, victim.body)) && procCoefficient > 0f)
			{
				GlobalEventManager.instance.OnHitEnemy(damageInfo, victim.gameObject);
			}
			GlobalEventManager.instance.OnHitAll(damageInfo, victim.gameObject);
		}
		catch (Exception ex)
		{
			if (!damageErrorLogged)
			{
				HPPlugin.Log.LogWarning("A damage call threw (logged once): " + ex);
			}
			damageErrorLogged = true;
		}
	}

	public static List<CharacterBody> FindBodies(Vector3 center, float radius, Func<CharacterBody, bool> filter)
	{
		List<CharacterBody> list = new List<CharacterBody>();
		float num = radius * radius;
		foreach (CharacterBody readOnlyInstances in CharacterBody.readOnlyInstancesList)
		{
			if ((bool)readOnlyInstances && (bool)readOnlyInstances.healthComponent && readOnlyInstances.healthComponent.alive && filter(readOnlyInstances) && !((readOnlyInstances.corePosition - center).sqrMagnitude > num))
			{
				list.Add(readOnlyInstances);
			}
		}
		Collider[] array = Physics.OverlapSphere(center, radius, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
		foreach (Collider collider in array)
		{
			HurtBox hurtBox = (collider ? collider.GetComponent<HurtBox>() : null);
			HealthComponent healthComponent = (hurtBox ? hurtBox.healthComponent : null);
			CharacterBody characterBody = (healthComponent ? healthComponent.body : null);
			if ((bool)characterBody && healthComponent.alive && !list.Contains(characterBody) && filter(characterBody))
			{
				list.Add(characterBody);
			}
		}
		return list;
	}

	public static List<CharacterBody> FindEnemies(TeamIndex team, Vector3 center, float radius)
	{
		return FindBodies(center, radius, (CharacterBody b) => IsEnemy(team, b));
	}

	public static void SpawnEffect(GameObject prefab, Vector3 origin, float scale, Quaternion rotation = default(Quaternion))
	{
		if ((bool)prefab)
		{
			EffectManager.SpawnEffect(prefab, new EffectData
			{
				origin = origin,
				scale = scale,
				rotation = rotation
			}, transmit: true);
		}
	}

	public static void SpawnLocalEffect(GameObject prefab, Vector3 origin, float scale)
	{
		if ((bool)prefab)
		{
			EffectManager.SpawnEffect(prefab, new EffectData
			{
				origin = origin,
				scale = scale
			}, transmit: false);
		}
	}

	public static Vector3 SnapToGround(Vector3 point, float above = 6f, float depth = 200f)
	{
		if (Physics.Raycast(point + Vector3.up * above, Vector3.down, out var hitInfo, depth, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			return hitInfo.point;
		}
		return point;
	}

	public static bool HasState(Animator animator, string layerName, string stateName)
	{
		if (!animator)
		{
			return false;
		}
		int layerIndex = animator.GetLayerIndex(layerName);
		if (layerIndex >= 0)
		{
			return animator.HasState(layerIndex, Animator.StringToHash(stateName));
		}
		return false;
	}

	public static EntityStateMachine FindMachine(GameObject prefab, string name)
	{
		return Enumerable.FirstOrDefault<EntityStateMachine>((IEnumerable<EntityStateMachine>)prefab.GetComponents<EntityStateMachine>(), (Func<EntityStateMachine, bool>)((EntityStateMachine m) => m.customName == name));
	}

	public static bool LocalPlayerIs(Func<CharacterBody, bool> test)
	{
		CharacterBody characterBody = LocalUserManager.GetFirstLocalUser()?.cachedBody;
		if ((bool)characterBody)
		{
			return test(characterBody);
		}
		return false;
	}

	public static Material LineMaterial()
	{
		if ((bool)lineMaterial)
		{
			return lineMaterial;
		}
		Material material = HPAssets.Load<Material>(RoR2_Base_Common_VFX.matTracerBright_mat);
		lineMaterial = (material ? new Material(material) : CustomModel.Glow(Color.white, Color.white, 2f, twoSided: true));
		return lineMaterial;
	}

	public static void TintModel(GameObject bodyPrefab, Color color)
	{
		ModelLocator component = bodyPrefab.GetComponent<ModelLocator>();
		if ((bool)component && (bool)component.modelTransform)
		{
			component.modelTransform.gameObject.AddComponent<ColorTint>().tint = color;
		}
	}
}

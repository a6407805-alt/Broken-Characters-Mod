using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.Components;

public class GravityLiftSlam : MonoBehaviour
{
	private const float LiftSpeed = 22f;

	private const float SlamSpeed = 75f;

	private const float MaxSlamTime = 1.25f;

	private GameObject attacker;

	private float damage;

	private bool crit;

	private float procCoefficient;

	private float liftDuration;

	private bool moveVictim;

	private CharacterBody body;

	private float age;

	private float slamAge;

	public static void Apply(CharacterBody victim, GameObject attacker, float damage, bool crit, float procCoefficient, float liftDuration)
	{
		if (NetworkServer.active && (bool)victim && (bool)victim.healthComponent && victim.healthComponent.alive)
		{
			if ((bool)victim.GetComponent<GravityLiftSlam>())
			{
				Impact(victim, attacker, damage, crit, procCoefficient);
				return;
			}
			GravityLiftSlam gravityLiftSlam = victim.gameObject.AddComponent<GravityLiftSlam>();
			gravityLiftSlam.body = victim;
			gravityLiftSlam.attacker = attacker;
			gravityLiftSlam.damage = damage;
			gravityLiftSlam.crit = crit;
			gravityLiftSlam.procCoefficient = procCoefficient;
			gravityLiftSlam.liftDuration = liftDuration;
			gravityLiftSlam.moveVictim = !GravityUtil.IsBig(victim);
		}
	}

	private void FixedUpdate()
	{
		if (!NetworkServer.active || !body || !body.healthComponent || !body.healthComponent.alive)
		{
			Object.Destroy(this);
			return;
		}
		if (!moveVictim)
		{
			Finish();
			return;
		}
		age += Time.fixedDeltaTime;
		if (age < liftDuration)
		{
			GravityUtil.SetVelocity(body, Vector3.up * 22f);
			return;
		}
		slamAge += Time.fixedDeltaTime;
		GravityUtil.SetVelocity(body, Vector3.down * 75f);
		if (slamAge > 0.05f && (IsGrounded() || slamAge >= 1.25f))
		{
			Finish();
		}
	}

	private bool IsGrounded()
	{
		if ((bool)body.characterMotor && body.characterMotor.isGrounded)
		{
			return true;
		}
		float maxDistance = 0.5f + 75f * Time.fixedDeltaTime;
		return Physics.Raycast(body.footPosition + Vector3.up * 0.25f, Vector3.down, maxDistance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
	}

	private void Finish()
	{
		if (moveVictim)
		{
			GravityUtil.SetVelocity(body, Vector3.zero);
		}
		Impact(body, attacker, damage, crit, procCoefficient);
		Object.Destroy(this);
	}

	private static void Impact(CharacterBody victim, GameObject attacker, float damage, bool crit, float procCoefficient)
	{
		GravityUtil.DealDamage(attacker, victim.healthComponent, damage, crit, victim.footPosition, Vector3.down * 500f, procCoefficient);
		GravityUtil.SpawnEffect(GravitosAssets.SlamEffect, victim.footPosition, Mathf.Max(2f, victim.radius * 2f));
	}
}

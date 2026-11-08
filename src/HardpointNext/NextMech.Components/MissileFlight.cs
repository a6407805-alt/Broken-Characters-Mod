using System;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.Components;

public class MissileFlight : MonoBehaviour
{
	private const float Lifetime = 4f;

	private const float StartSpeed = 26f;

	private const float TopSpeed = 80f;

	private static float lastCue = -99f;

	private ProjectileController controller;

	private ProjectileDamage damage;

	private ProjectileTargetComponent targeting;

	private TeamFilter team;

	private Vector3 velocity;

	private float age;

	private bool exploded;

	private void Awake()
	{
		controller = GetComponent<ProjectileController>();
		damage = GetComponent<ProjectileDamage>();
		targeting = GetComponent<ProjectileTargetComponent>();
		team = GetComponent<TeamFilter>();
	}

	private void Start()
	{
		velocity = base.transform.forward * 26f;
		if (NetworkServer.active)
		{
			UnityEngine.Object.Destroy(base.gameObject, 5f);
		}
	}

	private void FixedUpdate()
	{
		if (!NetworkServer.active || exploded)
		{
			return;
		}
		float fixedDeltaTime = Time.fixedDeltaTime;
		age += fixedDeltaTime;
		float num = Mathf.MoveTowards(velocity.magnitude, 80f, 140f * fixedDeltaTime);
		Vector3 vector = velocity.normalized;
		Transform transform = (targeting ? targeting.target : null);
		if ((bool)transform && age > 0.18f)
		{
			Vector3 normalized = (transform.position - base.transform.position).normalized;
			float num2 = Mathf.Lerp(180f, 720f, Mathf.Clamp01((age - 0.18f) / 0.8f));
			vector = Vector3.RotateTowards(vector, normalized, num2 * (MathF.PI / 180f) * fixedDeltaTime, 0f);
		}
		else if (age > 0.3f)
		{
			vector = Vector3.RotateTowards(vector, Vector3.down * 0.2f + vector, MathF.PI / 6f * fixedDeltaTime, 0f).normalized;
		}
		velocity = vector * num;
		Vector3 vector2 = velocity * fixedDeltaTime;
		if (age > 0.05f && Physics.SphereCast(base.transform.position, 0.3f, vector, out var hitInfo, vector2.magnitude + 0.2f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			Explode(hitInfo.point);
			return;
		}
		base.transform.position += vector2;
		base.transform.rotation = Quaternion.LookRotation(vector);
		TeamIndex teamIndex = ((!team) ? TeamIndex.Player : team.teamIndex);
		if (age > 0.1f && HPUtil.FindEnemies(teamIndex, base.transform.position, 1.5f).Count > 0)
		{
			Explode(base.transform.position);
		}
		else if (age >= 4f)
		{
			Explode(base.transform.position);
		}
	}

	private void Explode(Vector3 point)
	{
		if (exploded)
		{
			return;
		}
		exploded = true;
		float value = HPConfig.MissileRadius.Value;
		try
		{
			BlastAttack blastAttack = new BlastAttack();
			blastAttack.attacker = (controller ? controller.owner : null);
			blastAttack.inflictor = base.gameObject;
			blastAttack.teamIndex = ((!team) ? TeamIndex.Player : team.teamIndex);
			blastAttack.baseDamage = (damage ? damage.damage : 10f);
			blastAttack.baseForce = 600f;
			blastAttack.position = point;
			blastAttack.radius = value;
			blastAttack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
			blastAttack.crit = (bool)damage && damage.crit;
			blastAttack.procCoefficient = 0.6f;
			blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
			blastAttack.Fire();
		}
		catch (Exception)
		{
		}
		try
		{
			HPUtil.SpawnEffect(HPAssets.Explosion, point, value);
			if (Time.time - lastCue > 0.12f)
			{
				lastCue = Time.time;
				HPContent.Cue(CustomAudio.Clip.MissileHit, point, 90f);
			}
		}
		catch (Exception)
		{
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}
}

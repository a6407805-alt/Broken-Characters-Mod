using System.Collections.Generic;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace GravitosNext.Components;

public class GravitosController : MonoBehaviour
{
	private const float ProjectileScanInterval = 0.1f;

	private static readonly Collider[] overlapBuffer = new Collider[256];

	private CharacterBody body;

	private float slamTimer;

	private float projectileScanTimer;

	private GameObject bubble;

	private float Radius => GravitosConfig.PassiveRadius.Value;

	private void Awake()
	{
		body = GetComponent<CharacterBody>();
	}

	private void Start()
	{
		if (GravitosConfig.ShowBubble.Value && (bool)GravitosAssets.BubbleMaterial)
		{
			CreateBubble();
		}
	}

	private void OnDestroy()
	{
		if ((bool)bubble)
		{
			Object.Destroy(bubble);
		}
	}

	private void FixedUpdate()
	{
		if ((bool)bubble)
		{
			bubble.transform.position = body.corePosition;
		}
		if (NetworkServer.active && (bool)body.healthComponent && body.healthComponent.alive)
		{
			slamTimer -= Time.fixedDeltaTime;
			if (slamTimer <= 0f)
			{
				slamTimer = (TrySlam() ? GravitosConfig.PassiveCooldown.Value : 0.1f);
			}
			projectileScanTimer -= Time.fixedDeltaTime;
			if (projectileScanTimer <= 0f)
			{
				projectileScanTimer = 0.1f;
				SlowProjectiles();
			}
		}
	}

	private bool TrySlam()
	{
		List<CharacterBody> list = GravityUtil.FindEnemies(body.teamComponent.teamIndex, body.corePosition, Radius);
		if (list.Count == 0)
		{
			return false;
		}
		float damage = body.damage * GravitosConfig.PassiveSlamDamage.Value;
		foreach (CharacterBody item in list)
		{
			GravityLiftSlam.Apply(item, base.gameObject, damage, body.RollCrit(), 1f, GravitosConfig.PassiveLiftDuration.Value);
		}
		GravityUtil.SpawnEffect(GravitosAssets.PulseEffect, body.footPosition, Radius);
		return true;
	}

	private void SlowProjectiles()
	{
		int layerMask = (int)LayerIndex.projectile.mask | (int)LayerIndex.projectileWorldOnly.mask;
		int num = Physics.OverlapSphereNonAlloc(body.corePosition, Radius, overlapBuffer, layerMask, QueryTriggerInteraction.Collide);
		TeamIndex teamIndex = body.teamComponent.teamIndex;
		for (int i = 0; i < num; i++)
		{
			ProjectileController projectileController = (overlapBuffer[i] ? overlapBuffer[i].GetComponentInParent<ProjectileController>() : null);
			if ((bool)projectileController && !projectileController.GetComponent<SlowedProjectileMarker>() && (!projectileController.teamFilter || projectileController.teamFilter.teamIndex != teamIndex))
			{
				projectileController.gameObject.AddComponent<SlowedProjectileMarker>();
				float value = GravitosConfig.ProjectileSlowFactor.Value;
				ProjectileSimple component = projectileController.GetComponent<ProjectileSimple>();
				if ((bool)component)
				{
					component.desiredForwardSpeed *= value;
				}
				Rigidbody component2 = projectileController.GetComponent<Rigidbody>();
				if ((bool)component2 && !component2.isKinematic)
				{
					component2.velocity *= value;
				}
			}
		}
	}

	private void CreateBubble()
	{
		bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		bubble.name = "GravitosNextBubble";
		Object.Destroy(bubble.GetComponent<Collider>());
		bubble.transform.localScale = Vector3.one * Radius * 2f;
		Renderer component = bubble.GetComponent<Renderer>();
		component.sharedMaterial = GravitosAssets.BubbleMaterial;
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
	}
}

using System.Collections.Generic;
using EntityStates;
using GravitosNext.Networking;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.SkillStates;

public class PullState : BaseSkillState
{
	private const float Duration = 0.35f;

	private const float StopDistance = 3f;

	private float strength = 1f;

	private float Range => GravitosConfig.PullRange.Value;

	public override void OnEnter()
	{
		base.OnEnter();
		StartAimMode(1f);
		PlayAnimation("Gesture, Additive", "FireMineRight");
		Util.PlaySound("Play_gravekeeper_attack2_shoot", base.gameObject);
		if ((bool)GravitosAssets.MuzzleFlash)
		{
			EffectManager.SimpleMuzzleFlash(GravitosAssets.MuzzleFlash, base.gameObject, "MuzzleLeft", transmit: false);
		}
		if (NetworkServer.active)
		{
			strength = GravityUtil.ShieldPullMultiplier(base.characterBody);
			Ray aimRay = GetAimRay();
			PullEnemies(aimRay);
			PullObjects(aimRay);
		}
	}

	private float PullFraction(float distance, bool big)
	{
		float num = GravityUtil.Falloff(distance, Range, GravitosConfig.PullMinFalloff.Value) * strength;
		if (big)
		{
			num *= GravitosConfig.PullBossMultiplier.Value;
		}
		return Mathf.Clamp01(num);
	}

	private float TravelTime(float distance, float slow, float fast)
	{
		return Mathf.Max(0.2f, Mathf.Lerp(slow, fast, distance / Range) / Mathf.Sqrt(Mathf.Max(1f, strength)));
	}

	private Vector3 Anchor(Vector3 aimDirection)
	{
		Vector3 vector = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
		if (vector.sqrMagnitude < 0.01f)
		{
			vector = (base.characterDirection ? base.characterDirection.forward : base.transform.forward);
		}
		return base.characterBody.footPosition + vector.normalized * 3f;
	}

	private bool InCone(Ray aim, Vector3 position, out float distance)
	{
		Vector3 to = position - aim.origin;
		distance = to.magnitude;
		if (distance > Range)
		{
			return false;
		}
		if (!(distance < 1f))
		{
			return Vector3.Angle(aim.direction, to) <= GravitosConfig.PullConeAngle.Value;
		}
		return true;
	}

	private void PullEnemies(Ray aim)
	{
		BullseyeSearch bullseyeSearch = new BullseyeSearch();
		bullseyeSearch.teamMaskFilter = TeamMask.GetUnprotectedTeams(base.teamComponent.teamIndex);
		bullseyeSearch.filterByLoS = false;
		bullseyeSearch.searchOrigin = aim.origin;
		bullseyeSearch.searchDirection = aim.direction;
		bullseyeSearch.maxDistanceFilter = Range;
		bullseyeSearch.maxAngleFilter = GravitosConfig.PullConeAngle.Value;
		bullseyeSearch.sortMode = BullseyeSearch.SortMode.None;
		bullseyeSearch.RefreshCandidates();
		bullseyeSearch.FilterOutGameObject(base.gameObject);
		Vector3 vector = Anchor(aim.direction);
		float num = Mathf.Abs(Physics.gravity.y);
		HashSet<HealthComponent> hashSet = new HashSet<HealthComponent>();
		foreach (HurtBox result in bullseyeSearch.GetResults())
		{
			HealthComponent healthComponent = result.healthComponent;
			if ((bool)healthComponent && healthComponent.alive && hashSet.Add(healthComponent) && (bool)healthComponent.body)
			{
				CharacterBody body = healthComponent.body;
				float distance = Vector3.Distance(aim.origin, body.corePosition);
				float num2 = PullFraction(distance, GravityUtil.IsBig(body));
				Vector3 vector2 = (vector - body.footPosition) * num2;
				float num3 = TravelTime(distance, 0.45f, 0.9f);
				Vector3 velocity;
				if ((bool)body.characterMotor)
				{
					Vector3 vector3 = Vector3.ProjectOnPlane(vector2, Vector3.up) / num3;
					float num4 = vector2.y / num3 + 0.5f * num * num3;
					velocity = vector3 + Vector3.up * num4;
				}
				else
				{
					velocity = vector2 / num3;
				}
				GravityUtil.SetVelocity(body, velocity);
			}
		}
	}

	private void PullObjects(Ray aim)
	{
		Vector3 destination = Anchor(aim.direction);
		if (GravitosConfig.PullItems.Value)
		{
			Vector3 destination2 = base.characterBody.footPosition + Vector3.up * 0.5f;
			GenericPickupController[] array = Object.FindObjectsOfType<GenericPickupController>();
			foreach (GenericPickupController genericPickupController in array)
			{
				PullObject(aim, genericPickupController.gameObject, destination2, snapToGround: false);
			}
			PickupPickerController[] array2 = Object.FindObjectsOfType<PickupPickerController>();
			foreach (PickupPickerController pickupPickerController in array2)
			{
				PullObject(aim, pickupPickerController.gameObject, destination, snapToGround: true);
			}
		}
		if (GravitosConfig.PullChests.Value)
		{
			ChestBehavior[] array3 = Object.FindObjectsOfType<ChestBehavior>();
			foreach (ChestBehavior chestBehavior in array3)
			{
				PullObject(aim, chestBehavior.gameObject, destination, snapToGround: true);
			}
		}
		if (GravitosConfig.PullBarrels.Value)
		{
			BarrelInteraction[] array4 = Object.FindObjectsOfType<BarrelInteraction>();
			foreach (BarrelInteraction barrelInteraction in array4)
			{
				PullObject(aim, barrelInteraction.gameObject, destination, snapToGround: true);
			}
		}
	}

	private void PullObject(Ray aim, GameObject target, Vector3 destination, bool snapToGround)
	{
		if ((bool)target && InCone(aim, target.transform.position, out var distance))
		{
			float t = PullFraction(distance, big: false);
			Vector3 vector = Vector3.Lerp(target.transform.position, destination, t);
			if (snapToGround)
			{
				vector = GravityUtil.SnapToGround(vector);
			}
			GlideMessage.StartGlide(target, vector, TravelTime(distance, 0.35f, 0.8f));
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority && base.fixedAge >= 0.35f)
		{
			outer.SetNextStateToMain();
		}
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.PrioritySkill;
	}
}

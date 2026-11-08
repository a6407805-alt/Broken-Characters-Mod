using System;
using System.Collections.Generic;
using System.Linq;
using EntityStates;
using NextMech.Components;
using RoR2;
using UnityEngine;

namespace NextMech.SkillStates;

public class MissileLock : BaseSkillState
{
	private float timer;

	private HPStatus status;

	private bool left;

	private System.Collections.Generic.List<HurtBox> Locks => status.LocksFor(left);

	public override void OnEnter()
	{
		base.OnEnter();
		status = HPStatus.Of(base.characterBody);
		left = Arsenal.IsLeft(this);
		if ((bool)status)
		{
			Locks.Clear();
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.isAuthority)
		{
			timer += Time.fixedDeltaTime;
			if (timer >= HPConfig.MissileLockTime.Value)
			{
				timer = 0f;
				Lock();
			}
			if (!base.inputBank || !Arsenal.Held(this) || base.fixedAge >= HPConfig.MissileMaxHold.Value)
			{
				outer.SetNextState(new MissileFire());
			}
		}
	}

	private void Lock()
	{
		if (!status || Locks.Count >= HPConfig.MissileMaxLocks.Value)
		{
			return;
		}
		Ray aimRay = GetAimRay();
		BullseyeSearch bullseyeSearch = new BullseyeSearch();
		bullseyeSearch.teamMaskFilter = TeamMask.GetEnemyTeams(base.teamComponent.teamIndex);
		bullseyeSearch.filterByLoS = true;
		bullseyeSearch.searchOrigin = aimRay.origin;
		bullseyeSearch.searchDirection = aimRay.direction;
		bullseyeSearch.maxDistanceFilter = HPConfig.MissileLockRange.Value;
		bullseyeSearch.maxAngleFilter = HPConfig.MissileLockAngle.Value;
		bullseyeSearch.sortMode = BullseyeSearch.SortMode.Angle;
		bullseyeSearch.RefreshCandidates();
		List<HurtBox> list = Enumerable.ToList<HurtBox>(Enumerable.Where<HurtBox>(bullseyeSearch.GetResults(), (Func<HurtBox, bool>)((HurtBox h) => (bool)h && (bool)h.healthComponent && h.healthComponent.alive)));
		if (list.Count == 0)
		{
			return;
		}
		HurtBox item = Enumerable.FirstOrDefault<HurtBox>((IEnumerable<HurtBox>)list, (Func<HurtBox, bool>)((HurtBox h) => !Enumerable.Any<HurtBox>((IEnumerable<HurtBox>)Locks, (Func<HurtBox, bool>)((HurtBox l) => (bool)l && l.healthComponent == h.healthComponent)))) ?? list[0];
		Locks.Add(item);
		CustomAudio.Play(CustomAudio.Clip.Lock);
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Skill;
	}
}

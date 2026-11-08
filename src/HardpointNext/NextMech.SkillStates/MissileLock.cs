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

	public override void OnEnter()
	{
		base.OnEnter();
		status = HPStatus.Of(base.characterBody);
		if ((bool)status)
		{
			status.locks.Clear();
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
			if (!base.inputBank || !base.inputBank.skill4.down || base.fixedAge >= HPConfig.MissileMaxHold.Value)
			{
				outer.SetNextState(new MissileFire());
			}
		}
	}

	private void Lock()
	{
		if (!status || status.locks.Count >= HPConfig.MissileMaxLocks.Value)
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
		HurtBox item = Enumerable.FirstOrDefault<HurtBox>((IEnumerable<HurtBox>)list, (Func<HurtBox, bool>)((HurtBox h) => !Enumerable.Any<HurtBox>((IEnumerable<HurtBox>)status.locks, (Func<HurtBox, bool>)((HurtBox l) => (bool)l && l.healthComponent == h.healthComponent)))) ?? list[0];
		status.locks.Add(item);
		CustomAudio.Play(CustomAudio.Clip.Lock);
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Skill;
	}
}

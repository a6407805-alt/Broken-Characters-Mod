using EntityStates;
using NextMech.Components;

namespace NextMech.SkillStates;

public class ReloadRifle : BaseSkillState
{
	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = HPConfig.RifleReload.Value / attackSpeedStat;
		CustomAudio.PlayNear(CustomAudio.Clip.Reload, base.transform.position, 40f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (!(base.fixedAge < duration))
		{
			HPStatus hPStatus = HPStatus.Of(base.characterBody);
			if ((bool)hPStatus)
			{
				hPStatus.magazine = HPConfig.RifleMagazine.Value;
			}
			if (base.isAuthority)
			{
				outer.SetNextStateToMain();
			}
		}
	}

	public override InterruptPriority GetMinimumInterruptPriority()
	{
		return InterruptPriority.Skill;
	}
}

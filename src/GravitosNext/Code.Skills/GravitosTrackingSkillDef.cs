using GravitosNext.Components;
using JetBrains.Annotations;
using RoR2;
using RoR2.Skills;

namespace GravitosNext.Skills;

public class GravitosTrackingSkillDef : SkillDef
{
	protected class InstanceData : BaseSkillInstanceData
	{
		public GravitosTracker tracker;
	}

	public override BaseSkillInstanceData OnAssigned([NotNull] GenericSkill skillSlot)
	{
		return new InstanceData
		{
			tracker = skillSlot.GetComponent<GravitosTracker>()
		};
	}

	private static bool HasTarget(GenericSkill skillSlot)
	{
		GravitosTracker gravitosTracker = (skillSlot.skillInstanceData as InstanceData)?.tracker;
		if ((bool)gravitosTracker)
		{
			return gravitosTracker.GetTrackingTarget();
		}
		return false;
	}

	public override bool CanExecute([NotNull] GenericSkill skillSlot)
	{
		if (HasTarget(skillSlot))
		{
			return base.CanExecute(skillSlot);
		}
		return false;
	}

	public override bool IsReady([NotNull] GenericSkill skillSlot)
	{
		if (base.IsReady(skillSlot))
		{
			return HasTarget(skillSlot);
		}
		return false;
	}
}

using NextMech.Components;
using RoR2;
using RoR2.Skills;

namespace NextMech;

public class ENSkillDef : SkillDef
{
	public float enCost;

	private bool HasEN(GenericSkill skillSlot)
	{
		HPStatus hPStatus = (skillSlot ? skillSlot.GetComponent<HPStatus>() : null);
		if ((bool)hPStatus)
		{
			return hPStatus.CanSpend(enCost);
		}
		return true;
	}

	public override bool CanExecute(GenericSkill skillSlot)
	{
		if (HasEN(skillSlot))
		{
			return base.CanExecute(skillSlot);
		}
		return false;
	}

	public override bool IsReady(GenericSkill skillSlot)
	{
		if (base.IsReady(skillSlot))
		{
			return HasEN(skillSlot);
		}
		return false;
	}
}

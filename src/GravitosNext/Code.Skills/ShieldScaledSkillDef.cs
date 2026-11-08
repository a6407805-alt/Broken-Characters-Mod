using JetBrains.Annotations;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace GravitosNext.Skills;

public class ShieldScaledSkillDef : SkillDef
{
	public override float GetRechargeInterval([NotNull] GenericSkill skillSlot)
	{
		float rechargeInterval = base.GetRechargeInterval(skillSlot);
		CharacterBody component = skillSlot.GetComponent<CharacterBody>();
		if (!component)
		{
			return rechargeInterval;
		}
		float num = Mathf.Max(1f, GravitosConfig.PullShieldReference.Value);
		float num2 = Mathf.Max(num, component.maxShield);
		float b = rechargeInterval * Mathf.Sqrt(num / num2);
		return Mathf.Max(Mathf.Min(GravitosConfig.PullMinCooldown.Value, rechargeInterval), b);
	}
}

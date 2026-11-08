using BepInEx.Configuration;
using EntityStates;
using NextMech.Components;
using NextMech.SkillStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace NextMech;

// The pilot's extra keys on their own NEXT: Overed Boost (V) and Hanger swap (F).
public class NextPilot : MonoBehaviour
{
	public bool hangerOut;
	public bool obStopRequested;
	public float lastQbTime = -99f;
	public bool lastQbWasSecond;

	private CharacterBody body;
	private EntityStateMachine boostMachine;

	public static NextPilot Of(CharacterBody b) => b ? b.GetComponent<NextPilot>() : null;

	public static bool KeyDown(KeyboardShortcut k)
	{
		if (k.MainKey == KeyCode.None) return false;
		if (!Input.GetKeyDown(k.MainKey)) return false;
		foreach (KeyCode m in k.Modifiers)
		{
			if (!Input.GetKey(m)) return false;
		}
		return true;
	}

	private void Awake()
	{
		body = GetComponent<CharacterBody>();
	}

	private bool IsLocalPilot()
	{
		return body && body.hasEffectiveAuthority && LocalUserManager.GetFirstLocalUser()?.cachedBody == body
			&& body.healthComponent && body.healthComponent.alive && !TuningScreen.Open;
	}

	private void Update()
	{
		if (!IsLocalPilot()) return;
		if (KeyDown(NextConfig.ObKey.Value)) ToggleOveredBoost();
		if (KeyDown(NextConfig.HangerKey.Value)) SwapHanger();
	}

	private void ToggleOveredBoost()
	{
		if (!boostMachine) boostMachine = EntityStateMachine.FindByCustomName(gameObject, "Boost");
		if (!boostMachine) return;
		if (boostMachine.state is OveredBoost)
		{
			obStopRequested = true;
			return;
		}
		HPStatus st = HPStatus.Of(body);
		if (st && !st.CanSpend(NextConfig.ObStartCost.Value)) return;
		obStopRequested = false;
		boostMachine.SetInterruptState(new OveredBoost(), InterruptPriority.Skill);
	}

	private void SwapHanger()
	{
		GenericSkill primary = body.skillLocator ? body.skillLocator.primary : null;
		if (!primary) return;
		HangerPart part = Garage.Hanger(body);
		SkillDef def = part == HangerPart.None ? null : Garage.Hangers[(int)part];
		if (hangerOut)
		{
			foreach (SkillDef d in Garage.Hangers) if (d) primary.UnsetSkillOverride(this, d, GenericSkill.SkillOverridePriority.Replacement);
			hangerOut = false;
		}
		else if (def)
		{
			primary.SetSkillOverride(this, def, GenericSkill.SkillOverridePriority.Replacement);
			hangerOut = true;
		}
		else
		{
			return;
		}
		CustomAudio.PlayNear(CustomAudio.Clip.Reload, transform.position, 40f);
	}
}

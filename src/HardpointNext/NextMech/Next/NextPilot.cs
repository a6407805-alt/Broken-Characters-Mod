using BepInEx.Configuration;
using EntityStates;
using NextMech.Components;
using NextMech.SkillStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace NextMech;

// The pilot's extra keys on their own NEXT: Overed Boost (V) and the left back unit (G).
public class NextPilot : MonoBehaviour
{
	public bool obStopRequested;
	public float lastQbTime = -99f;
	public bool lastQbWasSecond;
	public bool leftBackHeld;
	private GenericSkill backLeft;

	// The left back unit's skill slot (not one of the four vanilla slots; fired with G).
	public static GenericSkill BackLeftSlot(CharacterBody b)
	{
		NextPilot p = Of(b);
		if (!p) return null;
		if (!p.backLeft)
		{
			foreach (GenericSkill gs in p.GetComponents<GenericSkill>())
			{
				if (gs && gs.skillName == "BackLeft") { p.backLeft = gs; break; }
			}
		}
		return p.backLeft;
	}

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
		if (!IsLocalPilot())
		{
			leftBackHeld = false;
			return;
		}
		KeyCode lb = NextConfig.LeftBackKey.Value.MainKey;
		leftBackHeld = lb != KeyCode.None && Input.GetKey(lb);
		if (KeyDown(NextConfig.LeftBackKey.Value))
		{
			GenericSkill slot = BackLeftSlot(body);
			if (slot) slot.ExecuteIfReady();
		}
		if (KeyDown(NextConfig.ObKey.Value)) ToggleOveredBoost();
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
}

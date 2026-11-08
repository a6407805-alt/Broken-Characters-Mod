using System;
using System.Collections.Generic;
using EntityStates;
using NextMech.SkillStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace NextMech;

public enum Mount { RightArm, LeftArm, RightBack, LeftBack }

// Every arm weapon fits either arm (M1 = right, M2 = left, each on its own state machine so they fire
// independently). Two back units: R = right back, G = left back. Each weapon has a weight; the Frame sets
// the max load and going over it is the overweight penalty (Garage.Stats / Garage.Overweight).
public static class Arsenal
{
	public class Weapon
	{
		public string id;
		public int weight;
		public Type state;
		public bool back;
		public float cooldown;
		public bool hold;          // fires again while held (mustKeyPress = false)
		public string machine;     // null = the mount's own machine
		public InterruptPriority priority = InterruptPriority.Any;
		public Sprite icon;
		public Color color;
	}

	public static readonly List<Weapon> Arms = new List<Weapon>();
	public static readonly List<Weapon> Backs = new List<Weapon>();
	private static readonly Dictionary<SkillDef, Weapon> byDef = new Dictionary<SkillDef, Weapon>();
	public static readonly Dictionary<string, SkillDef> RightArmDefs = new Dictionary<string, SkillDef>();

	public static void Define()
	{
		Arms.Clear();
		Backs.Clear();
		// Arm list (same for M1 and M2), in the owner's order.
		Arm("BURSTRIFLE", 12, typeof(FireRifle), hold: true, icon: HPAssets.PrimaryIcon, c: new Color(0.9f, 0.85f, 0.75f));
		Arm("ASSAULTRIFLE", 14, typeof(FireAssaultRifle), hold: true, c: new Color(0.95f, 0.75f, 0.35f));
		Arm("SMG", 8, typeof(FireSmg), hold: true, c: new Color(0.5f, 0.95f, 0.6f));
		Arm("SHOTGUN", 16, typeof(FireShotgun), hold: true, c: new Color(1f, 0.6f, 0.3f));
		Arm("LINEAR", 20, typeof(FireLinear), hold: true, c: new Color(0.6f, 0.45f, 1f));
		Arm("PULSEGUN", 10, typeof(FirePulseGun), hold: true, c: new Color(0.4f, 0.95f, 1f));
		Arm("BAZOOKA", 30, typeof(FireBazooka), hold: true, c: new Color(1f, 0.85f, 0.3f));
		Arm("GATLING", 34, typeof(FireGatling), hold: true, c: new Color(1f, 0.4f, 0.3f));

		Back("MISSILES", 22, typeof(MissileLock), HPConfig.MissileCooldown.Value, null, InterruptPriority.Skill, HPAssets.SpecialIcon, new Color(1f, 0.45f, 0.2f));
		Back("GRENADE", 40, typeof(FireGrenadeCannon), 5f, null, InterruptPriority.Skill, null, new Color(1f, 0.7f, 0.2f));
		Back("LASERCANNON", 32, typeof(ChargeLaserCannon), 8f, null, InterruptPriority.Skill, null, new Color(0.35f, 0.6f, 1f));
		Back("ASSAULTARMOR", 18, typeof(AssaultArmor), HPConfig.ArmorCooldown.Value, "Body", InterruptPriority.PrioritySkill, HPAssets.Special2Icon, new Color(0.45f, 1f, 1f));
	}

	private static void Arm(string id, int w, Type state, bool hold, float cd = 0f, string machine = null, InterruptPriority prio = InterruptPriority.Any, Sprite icon = null, Color c = default)
	{
		Arms.Add(new Weapon { id = id, weight = w, state = state, hold = hold, cooldown = cd, machine = machine, priority = prio, icon = icon, color = c });
	}

	private static void Back(string id, int w, Type state, float cd, string machine, InterruptPriority prio, Sprite icon, Color c)
	{
		Backs.Add(new Weapon { id = id, weight = w, state = state, back = true, cooldown = cd, machine = machine, priority = prio, icon = icon, color = c });
	}

	public static string MachineFor(Mount m)
	{
		switch (m)
		{
			case Mount.LeftArm: return "LeftArm";
			case Mount.RightBack: return "Pods";
			case Mount.LeftBack: return "Pods2";
			default: return "Weapon";
		}
	}

	// Skill defs for one mount, with `firstId` as the default (first) variant.
	public static SkillDef[] DefsFor(Mount mount, string firstId)
	{
		List<Weapon> list = (mount == Mount.RightBack || mount == Mount.LeftBack) ? Backs : Arms;
		var ordered = new List<Weapon>(list);
		int i0 = ordered.FindIndex(w => w.id == firstId);
		if (i0 > 0)
		{
			Weapon w0 = ordered[i0];
			ordered.RemoveAt(i0);
			ordered.Insert(0, w0);
		}
		var defs = new SkillDef[ordered.Count];
		for (int i = 0; i < ordered.Count; i++)
		{
			Weapon w = ordered[i];
			SkillDef def = HPContent.MakeSkill<SkillDef>(mount + w.id, "W_" + w.id, w.icon ? w.icon : WeaponIcon(w), w.state, w.cooldown, w.machine ?? MachineFor(mount), w.priority);
			def.mustKeyPress = !w.hold;
			def.cancelSprintingOnActivation = false;
			def.isCombatSkill = true;
			byDef[def] = w;
			if (mount == Mount.RightArm) RightArmDefs[w.id] = def;
			defs[i] = def;
		}
		return defs;
	}

	public static Weapon Of(SkillDef def) => def && byDef.TryGetValue(def, out Weapon w) ? w : null;

	public static int WeightOf(GenericSkill gs)
	{
		if (!gs) return 0;
		Weapon w = Of(gs.baseSkill ? gs.baseSkill : gs.skillDef);
		return w != null ? w.weight : 0;
	}

	private static Sprite WeaponIcon(Weapon w)
	{
		const int S = 128;
		var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
		var px = new Color[S * S];
		Color bg = new Color(0.07f, 0.08f, 0.1f);
		int hash = Math.Abs(w.id.GetHashCode());
		for (int y = 0; y < S; y++)
		{
			for (int x = 0; x < S; x++)
			{
				Color p = bg;
				if (x < 5 || y < 5 || x >= S - 5 || y >= S - 5) p = w.color * 0.8f;
				// a gun silhouette whose barrel length / thickness follows the weight
				int barrel = 30 + Mathf.Clamp(w.weight, 8, 50) * 1;
				int thick = 8 + w.weight / 6;
				if (w.back)
				{
					if (y > 40 && y < 40 + thick && x > 20 && x < 20 + barrel + 20) p = w.color;
					if (y > 30 && y < 70 && x > 16 && x < 50) p = w.color * 0.85f;
				}
				else
				{
					if (y > 64 && y < 64 + thick / 2 + 4 && x > 24 && x < 24 + barrel) p = w.color;
					if (y > 44 && y < 70 && x > 24 && x < 52) p = w.color * 0.9f;
					if (y > 30 && y < 50 && x > 30 && x < 42) p = w.color * 0.7f;
				}
				if (((x + y + hash) / 9) % 7 == 0 && x + y < 36) p = w.color;
				p.a = 1f;
				px[y * S + x] = p;
			}
		}
		tex.SetPixels(px);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
	}

	// ---- helpers for the weapon states ----

	public static Mount MountOf(EntityState s)
	{
		if (s is BaseSkillState bs && bs.activatorSkillSlot && s.skillLocator)
		{
			GenericSkill slot = bs.activatorSkillSlot;
			if (slot == s.skillLocator.secondary) return Mount.LeftArm;
			if (slot == s.skillLocator.special) return Mount.RightBack;
			if (slot == NextPilot.BackLeftSlot(s.characterBody)) return Mount.LeftBack;
			if (slot == s.skillLocator.primary) return Mount.RightArm;
		}
		switch (s.outer ? s.outer.customName : "")
		{
			case "LeftArm": return Mount.LeftArm;
			case "Pods": return Mount.RightBack;
			case "Pods2": return Mount.LeftBack;
			default: return Mount.RightArm;
		}
	}

	public static bool Held(EntityState s)
	{
		InputBankTest ib = s.inputBank;
		if (!ib) return false;
		switch (MountOf(s))
		{
			case Mount.LeftArm: return ib.skill2.down;
			case Mount.RightBack: return ib.skill4.down;
			case Mount.LeftBack:
				NextPilot p = NextPilot.Of(s.characterBody);
				return p && p.leftBackHeld;
			default: return ib.skill1.down;
		}
	}

	public static string Muzzle(EntityState s)
	{
		switch (MountOf(s))
		{
			case Mount.LeftArm: return "MuzzleLeft";
			case Mount.RightBack: return "PodR";
			case Mount.LeftBack: return "PodL";
			default: return "MuzzleRifle";
		}
	}

	public static bool IsLeft(EntityState s)
	{
		Mount m = MountOf(s);
		return m == Mount.LeftArm || m == Mount.LeftBack;
	}
}

using System;
using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace NextMech;

public enum FramePart { Middle, Light, Heavy }
public enum BoosterPart { Standard, QuickType, OverType }
public enum GeneratorPart { Standard, HighOutput, HighCapacity }

// The garage. FRAME is a loadout row (networked: it changes AP, armor and max load). BOOSTER and GENERATOR are
// picked on the in-game ACSIS screen (T) and saved in the config: they only change things the pilot's own
// machine runs (boosts, EN), so the character select screen stays short.
public static class Garage
{
	private static readonly Dictionary<SkillDef, int> partIndex = new Dictionary<SkillDef, int>();

	public static void CreateSlots(GameObject body)
	{
		Row(body, "Frame", "FRAME", new[] { "MIDDLE", "LIGHT", "HEAVY" },
			new[] { new Color(0.55f, 0.6f, 0.65f), new Color(0.35f, 0.85f, 1f), new Color(0.85f, 0.55f, 0.2f) });
	}

	private static void Row(GameObject body, string slot, string token, string[] variants, Color[] colors)
	{
		var defs = new SkillDef[variants.Length];
		for (int i = 0; i < variants.Length; i++)
		{
			SkillDef def = ScriptableObject.CreateInstance<SkillDef>();
			def.skillName = "HardpointNext" + slot + variants[i];
			((UnityEngine.Object)def).name = def.skillName;
			def.skillNameToken = "BCM_NEXT_" + token + "_" + variants[i] + "_NAME";
			def.skillDescriptionToken = "BCM_NEXT_" + token + "_" + variants[i] + "_DESCRIPTION";
			def.icon = PartIcon(colors[i], i, variants.Length);
			def.activationState = new SerializableEntityStateType(typeof(Idle));
			def.activationStateMachineName = "Body";
			def.baseRechargeInterval = 0f;
			def.baseMaxStock = 0;
			def.rechargeStock = 0;
			def.requiredStock = 1;
			def.isCombatSkill = false;
			ContentAddition.AddSkillDef(def);
			defs[i] = def;
			partIndex[def] = i;
		}
		GenericSkill gs = HPContent.AddSkillSlot(body, slot, defs);
		gs.loadoutTitleToken = "BCM_NEXT_GARAGE_" + token;
		gs.hideInCharacterSelect = false;
	}

	// Plain procedural icon: coloured plate with N bars (which variant it is).
	private static Sprite PartIcon(Color c, int index, int count)
	{
		const int S = 128;
		var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
		var px = new Color[S * S];
		Color bg = new Color(0.07f, 0.08f, 0.1f);
		for (int y = 0; y < S; y++)
		{
			for (int x = 0; x < S; x++)
			{
				Color p = bg;
				bool border = x < 6 || y < 6 || x >= S - 6 || y >= S - 6;
				if (border) p = c * 0.8f;
				// diagonal hazard stripe in the corner
				if (x + y < 40 && ((x + y) / 6) % 2 == 0) p = c;
				// bars
				int bars = index + 1;
				for (int b = 0; b < bars; b++)
				{
					int bx = 34 + b * 22;
					if (x >= bx && x < bx + 14 && y >= 34 && y < 94) p = c;
				}
				p.a = 1f;
				px[y * S + x] = p;
			}
		}
		tex.SetPixels(px);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
	}

	public static int Get(CharacterBody body, string slot)
	{
		NextParts parts = body ? body.GetComponent<NextParts>() : null;
		return parts ? parts.Get(slot) : 0;
	}

	internal static int IndexOf(SkillDef def)
	{
		return def && partIndex.TryGetValue(def, out int i) ? i : 0;
	}

	public static FramePart Frame(CharacterBody b) => (FramePart)Get(b, "Frame");
	public static BoosterPart Booster(CharacterBody b) => IsLocal(b) ? (BoosterPart)Mathf.Clamp(NextConfig.BoosterChoice.Value, 0, 2) : BoosterPart.Standard;
	public static GeneratorPart Generator(CharacterBody b) => IsLocal(b) ? (GeneratorPart)Mathf.Clamp(NextConfig.GeneratorChoice.Value, 0, 2) : GeneratorPart.Standard;

	private static bool IsLocal(CharacterBody b) => b && b.hasEffectiveAuthority;

	// ---- weapon load (AC weight limits) ----

	public static int MaxLoad(CharacterBody b)
	{
		switch (Frame(b))
		{
			case FramePart.Light: return NextConfig.LoadLight.Value;
			case FramePart.Heavy: return NextConfig.LoadHeavy.Value;
			default: return NextConfig.LoadMiddle.Value;
		}
	}

	public static int Load(CharacterBody b)
	{
		NextParts parts = b ? b.GetComponent<NextParts>() : null;
		if (!parts) return 0;
		int load = Arsenal.WeightOf(parts.Slot("Primary")) + Arsenal.WeightOf(parts.Slot("Secondary"))
			+ Arsenal.WeightOf(parts.Slot("Special")) + Arsenal.WeightOf(parts.Slot("BackLeft"));
		return load;
	}

	public static int Overweight(CharacterBody b) => Mathf.Max(0, Load(b) - MaxLoad(b));

	// 1 = no penalty; drops per point over the load, down to 0.4.
	public static float WeightFactor(CharacterBody b) => 1f - Mathf.Min(0.5f, Overweight(b) * NextConfig.OverweightPenalty.Value);

	// ---- what the parts do (multipliers; 1 = Hardpoint's numbers) ----

	public static float QbDistance(CharacterBody b)
	{
		float m = 1f;
		switch (Frame(b)) { case FramePart.Light: m *= 1.15f; break; case FramePart.Heavy: m *= 0.85f; break; }
		switch (Booster(b)) { case BoosterPart.QuickType: m *= 1.25f; break; case BoosterPart.OverType: m *= 0.85f; break; }
		return m * Tuning.Mult(Tuning.Stat.QuickBoost) * WeightFactor(b);
	}

	public static float QbCost(CharacterBody b)
	{
		float m = 1f;
		switch (Frame(b)) { case FramePart.Light: m *= 0.85f; break; case FramePart.Heavy: m *= 1.3f; break; }
		if (Booster(b) == BoosterPart.QuickType) m *= 1.15f;
		return m;
	}

	public static float ObSpeed(CharacterBody b)
	{
		float m = 1f;
		switch (Booster(b)) { case BoosterPart.QuickType: m *= 0.85f; break; case BoosterPart.OverType: m *= 1.35f; break; }
		if (Frame(b) == FramePart.Light) m *= 1.1f;
		if (Frame(b) == FramePart.Heavy) m *= 0.9f;
		return m * Tuning.Mult(Tuning.Stat.OveredBoost);
	}

	public static float ObCost(CharacterBody b)
	{
		return Booster(b) == BoosterPart.OverType ? 0.8f : 1f;
	}

	public static float EnCapacity(CharacterBody b)
	{
		float m = 1f;
		switch (Generator(b)) { case GeneratorPart.HighOutput: m *= 0.8f; break; case GeneratorPart.HighCapacity: m *= 1.5f; break; }
		return m * Tuning.Mult(Tuning.Stat.Capacity);
	}

	public static float EnRegen(CharacterBody b)
	{
		float m = 1f;
		switch (Generator(b)) { case GeneratorPart.HighOutput: m *= 1.4f; break; case GeneratorPart.HighCapacity: m *= 0.85f; break; }
		if (Frame(b) == FramePart.Heavy) m *= 0.9f;
		return m * Tuning.Mult(Tuning.Stat.Output) * WeightFactor(b);
	}

	public static float HoverCost(CharacterBody b)
	{
		return 1f / Tuning.Mult(Tuning.Stat.MainBooster);
	}

	// PA parts are read on the server (no tuning: tuning is per player and only known on their own machine).
	public static float PaMax(CharacterBody b)
	{
		float m = 1f;
		switch (Frame(b)) { case FramePart.Light: m *= 0.6f; break; case FramePart.Heavy: m *= 1.5f; break; }
		return m;
	}

	public static float PaRegen(CharacterBody b)
	{
		return 1f;
	}

	internal static void Stats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
	{
		if (!body || !HPUtil.IsMine(body)) return;
		args.moveSpeedMultAdd -= 1f - WeightFactor(body);
		switch (Frame(body))
		{
			case FramePart.Light:
				args.healthMultAdd -= 0.3f;
				args.moveSpeedMultAdd += 0.2f;
				break;
			case FramePart.Heavy:
				args.healthMultAdd += 0.35f;
				args.armorAdd += 25f;
				args.moveSpeedMultAdd -= 0.15f;
				break;
		}
	}
}

// Caches the body's chosen parts (read from its loadout rows).
public class NextParts : MonoBehaviour
{
	private readonly Dictionary<string, GenericSkill> slots = new Dictionary<string, GenericSkill>();
	private readonly Dictionary<string, int> last = new Dictionary<string, int>();
	private CharacterBody body;

	private void Awake()
	{
		body = GetComponent<CharacterBody>();
		foreach (GenericSkill gs in GetComponents<GenericSkill>())
		{
			if (gs && !string.IsNullOrEmpty(gs.skillName)) slots[gs.skillName] = gs;
		}
	}

	public GenericSkill Slot(string slot) => slots.TryGetValue(slot, out GenericSkill gs) ? gs : null;

	private int lastLoad = -1;

	private void FixedUpdate()
	{
		// weapon swaps between stages change the load: refresh the speed penalty
		int load = Garage.Load(body);
		if (load != lastLoad)
		{
			lastLoad = load;
			if (body) body.MarkAllStatsDirty();
		}
	}

	public int Get(string slot)
	{
		if (!slots.TryGetValue(slot, out GenericSkill gs) || !gs) return 0;
		int i = Garage.IndexOf(gs.baseSkill ? gs.baseSkill : gs.skillDef);
		if (!last.TryGetValue(slot, out int old) || old != i)
		{
			last[slot] = i;
			if (body) body.MarkAllStatsDirty();
		}
		return i;
	}

	private void Start()
	{
		if (body) body.MarkAllStatsDirty();
	}
}

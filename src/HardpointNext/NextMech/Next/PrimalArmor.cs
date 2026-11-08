using System;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech;

// Primal Armor (AC4): a regenerating Kojima-particle field. While it's up, every hit is weakened
// (up to 45% at full PA) and drains it. Runs on the server; the PA percent is shown to everyone
// through a hidden buff's stack count (0-100).
public static class PrimalArmor
{
	public static BuffDef PaBuff;
	private static bool warned;

	public static void Init()
	{
		PaBuff = ScriptableObject.CreateInstance<BuffDef>();
		((UnityEngine.Object)PaBuff).name = "NextPrimalArmor";
		PaBuff.canStack = true;
		PaBuff.isHidden = true;
		PaBuff.isDebuff = false;
		PaBuff.buffColor = new Color(0.4f, 0.95f, 1f);
		PaBuff.iconSprite = HPAssets.ArmorIcon;
		ContentAddition.AddBuffDef(PaBuff);

		On.RoR2.HealthComponent.TakeDamage += (orig, self, info) =>
		{
			try { Absorb(self, info); }
			catch (Exception e)
			{
				if (!warned) HPPlugin.Log.LogWarning("Primal Armor (logged once): " + e);
				warned = true;
			}
			orig(self, info);
		};
	}

	private static void Absorb(HealthComponent self, DamageInfo info)
	{
		if (!NetworkServer.active || !self || info == null || info.rejected || info.damage <= 0f) return;
		CharacterBody body = self.body;
		if (!body || !HPUtil.IsMine(body)) return;
		PaField field = body.GetComponent<PaField>();
		if (!field || field.pa <= 0f) return;
		float frac = field.Fraction;
		float before = info.damage;
		info.damage *= 1f - NextConfig.PaReduction.Value * frac;
		float drain = NextConfig.PaHitDrain.Value + NextConfig.PaDamageDrain.Value * before / Mathf.Max(1f, self.fullCombinedHealth);
		field.Drain(drain);
	}

	// Server: current PA fraction (0-1). Clients: read it from the buff.
	public static float Fraction(CharacterBody body)
	{
		if (!body) return 0f;
		if (NetworkServer.active)
		{
			PaField f = body.GetComponent<PaField>();
			if (f) return f.Fraction;
		}
		return PaBuff ? body.GetBuffCount(PaBuff) / 100f : 0f;
	}

	public static void Drain(CharacterBody body, float amount)
	{
		if (!NetworkServer.active || !body) return;
		PaField f = body.GetComponent<PaField>();
		if (f) f.Drain(amount);
	}

	public static void Empty(CharacterBody body)
	{
		if (!NetworkServer.active || !body) return;
		PaField f = body.GetComponent<PaField>();
		if (f) f.Drain(f.pa + 1f);
	}
}

public class PaField : MonoBehaviour
{
	public float pa = -1f;
	private float lastHit = -99f;
	private int shown = -1;
	private CharacterBody body;

	public float Max => NextConfig.PaMax.Value * Garage.PaMax(body);
	public float Fraction => Max > 0f ? Mathf.Clamp01(pa / Max) : 0f;

	private void Awake() => body = GetComponent<CharacterBody>();

	public void Drain(float amount)
	{
		pa = Mathf.Max(0f, pa - amount);
		lastHit = Time.time;
		Show();
	}

	private void FixedUpdate()
	{
		if (!NetworkServer.active || !body) return;
		if (pa < 0f) pa = Max;
		if (Time.time - lastHit > NextConfig.PaRegenDelay.Value)
		{
			pa = Mathf.Min(Max, pa + NextConfig.PaRegen.Value * Garage.PaRegen(body) * Time.fixedDeltaTime);
		}
		pa = Mathf.Min(pa, Max);
		Show();
	}

	private void Show()
	{
		if (!PrimalArmor.PaBuff || !body) return;
		int n = Mathf.Clamp(Mathf.RoundToInt(Fraction * 100f), 0, 100);
		if (n == shown) return;
		shown = n;
		body.SetBuffCount(PrimalArmor.PaBuff.buffIndex, n);
	}
}

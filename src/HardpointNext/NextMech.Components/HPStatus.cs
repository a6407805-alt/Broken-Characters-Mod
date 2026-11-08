using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace NextMech.Components;

public class HPStatus : MonoBehaviour
{
	public float en;

	public bool redline;

	public int magazine;

	public int magazineL;

	public float gatlingSpinR, gatlingSpinL, gatlingLastR = -99f, gatlingLastL = -99f;

	public readonly List<HurtBox> locksL = new List<HurtBox>();

	public List<HurtBox> LocksFor(bool left) => left ? locksL : locks;

	public readonly List<HurtBox> locks = new List<HurtBox>();

	private float lastSpend = -99f;

	private bool saidLow;

	private CharacterBody body;

	public float Capacity => HPConfig.EnCapacity.Value * (body ? Garage.EnCapacity(body) : 1f);

	public float Fraction => en / Mathf.Max(1f, Capacity);

	public static HPStatus Of(CharacterBody body)
	{
		if (!body)
		{
			return null;
		}
		return body.GetComponent<HPStatus>();
	}

	private void Awake()
	{
		body = GetComponent<CharacterBody>();
		en = HPConfig.EnCapacity.Value;
		magazine = HPConfig.RifleMagazine.Value;
		magazineL = HPConfig.RifleMagazine.Value;
	}

	public bool CanSpend(float amount)
	{
		if (!redline)
		{
			return en >= Mathf.Min(amount, Capacity);
		}
		return false;
	}

	public void Spend(float amount)
	{
		en -= amount;
		lastSpend = Time.time;
		if (en > 0f)
		{
			return;
		}
		en = 0f;
		if (!redline)
		{
			redline = true;
			if (IsLocal())
			{
				CustomAudio.Play(CustomAudio.Clip.Redline);
			}
		}
	}

	private bool IsLocal()
	{
		if ((bool)body)
		{
			return HPUtil.LocalPlayerIs((CharacterBody b) => b == body);
		}
		return false;
	}

	private void FixedUpdate()
	{
		float value = Capacity;
		en = Mathf.Min(en, value);
		if (Time.time - lastSpend > HPConfig.EnRegenDelay.Value)
		{
			en = Mathf.Min(value, en + HPConfig.EnRegen.Value * Garage.EnRegen(body) * Time.fixedDeltaTime);
		}
		if (redline && en >= value * HPConfig.RedlineRecover.Value)
		{
			redline = false;
		}
		for (int n = locksL.Count - 1; n >= 0; n--)
		{
			if (!locksL[n] || !locksL[n].healthComponent || !locksL[n].healthComponent.alive) locksL.RemoveAt(n);
		}
		for (int num = locks.Count - 1; num >= 0; num--)
		{
			if (!locks[num] || !locks[num].healthComponent || !locks[num].healthComponent.alive)
			{
				locks.RemoveAt(num);
			}
		}
		HealthComponent healthComponent = (body ? body.healthComponent : null);
		if (!healthComponent || !healthComponent.alive)
		{
			return;
		}
		float combinedHealthFraction = healthComponent.combinedHealthFraction;
		if (combinedHealthFraction < 0.3f && !saidLow)
		{
			saidLow = true;
			if (IsLocal())
			{
				CustomAudio.Play(CustomAudio.Clip.LowAP);
			}
		}
		else if (combinedHealthFraction > 0.5f)
		{
			saidLow = false;
		}
	}
}

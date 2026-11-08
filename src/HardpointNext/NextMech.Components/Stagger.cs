using System;
using System.Collections.Generic;
using On.RoR2;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.Components;

public static class Stagger
{
	private class Entry
	{
		public RoR2.CharacterBody victim;

		public float value;

		public float lastHit;
	}

	private static readonly Dictionary<RoR2.HealthComponent, Entry> table = new Dictionary<RoR2.HealthComponent, Entry>();

	private static bool warned;

	public static void Init()
	{
		On.RoR2.HealthComponent.TakeDamage += delegate(On.RoR2.HealthComponent.orig_TakeDamage orig, RoR2.HealthComponent self, RoR2.DamageInfo info)
		{
			try
			{
				Before(self, info);
			}
			catch (Exception e)
			{
				Warn(e);
			}
			orig(self, info);
			try
			{
				After(self, info);
			}
			catch (Exception e2)
			{
				Warn(e2);
			}
		};
		GameObject gameObject = new GameObject("HardpointNextStaggerTicker");
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		gameObject.AddComponent<StaggerTicker>();
	}

	private static void Warn(Exception e)
	{
		if (!warned)
		{
			HPPlugin.Log.LogWarning("Stagger (logged once): " + e);
		}
		warned = true;
	}

	private static void Before(RoR2.HealthComponent self, RoR2.DamageInfo info)
	{
		if (NetworkServer.active && (bool)self && info != null && !info.rejected)
		{
			RoR2.CharacterBody body = self.body;
			if ((bool)body && (bool)HPContent.OverloadBuff && body.HasBuff(HPContent.OverloadBuff))
			{
				info.damage *= 1f + HPConfig.OverloadDamageBonus.Value;
			}
		}
	}

	private static void After(RoR2.HealthComponent self, RoR2.DamageInfo info)
	{
		if (!NetworkServer.active || !self || !self.alive || info == null || info.rejected || !info.attacker || !HPContent.StaggerBuff)
		{
			return;
		}
		RoR2.CharacterBody component = info.attacker.GetComponent<RoR2.CharacterBody>();
		RoR2.CharacterBody body = self.body;
		if (!component || !body || !HPUtil.IsMine(component) || !HPUtil.IsEnemy(component.teamComponent.teamIndex, body) || ((bool)HPContent.OverloadBuff && body.HasBuff(HPContent.OverloadBuff)))
		{
			return;
		}
		float num = info.damage / Mathf.Max(1f, component.damage) / (info.crit ? 2f : 1f);
		if (!table.TryGetValue(self, out var value))
		{
			Dictionary<RoR2.HealthComponent, Entry> dictionary = table;
			Entry obj = new Entry
			{
				victim = body
			};
			value = obj;
			dictionary[self] = obj;
		}
		value.value += num * HPConfig.StaggerPerDamage.Value;
		value.lastHit = Time.time;
		if (value.value >= HPConfig.StaggerThreshold.Value)
		{
			value.value = 0f;
			body.AddTimedBuff(HPContent.OverloadBuff, HPConfig.OverloadDuration.Value);
			if (!body.isChampion)
			{
				RoR2.SetStateOnHurt.SetStunOnObject(body.gameObject, HPConfig.StunTime.Value);
			}
			HPUtil.SpawnEffect(HPAssets.Spark, body.corePosition, 3f);
			HPContent.Cue(CustomAudio.Clip.Overload, component.corePosition, 50f);
		}
		Show(value);
	}

	private static void Show(Entry e)
	{
		if ((bool)e.victim)
		{
			e.victim.SetBuffCount(HPContent.StaggerBuff.buffIndex, Mathf.Clamp(Mathf.RoundToInt(e.value), 0, 100));
		}
	}

	public static void Tick(float dt)
	{
		if (!NetworkServer.active || table.Count == 0)
		{
			return;
		}
		List<RoR2.HealthComponent> list = null;
		foreach (KeyValuePair<RoR2.HealthComponent, Entry> item in table)
		{
			Entry value = item.Value;
			if (!item.Key || !item.Key.alive || !value.victim)
			{
				List<RoR2.HealthComponent> obj = list ?? new List<RoR2.HealthComponent>();
				list = obj;
				obj.Add(item.Key);
			}
			else if (!(Time.time - value.lastHit < HPConfig.StaggerDecayDelay.Value))
			{
				value.value = Mathf.Max(0f, value.value - HPConfig.StaggerDecay.Value * dt);
				Show(value);
				if (value.value <= 0f)
				{
					List<RoR2.HealthComponent> obj2 = list ?? new List<RoR2.HealthComponent>();
					list = obj2;
					obj2.Add(item.Key);
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (RoR2.HealthComponent item2 in list)
		{
			table.Remove(item2);
		}
	}
}

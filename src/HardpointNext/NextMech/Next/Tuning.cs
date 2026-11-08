using System;
using RoR2;
using UnityEngine;

namespace NextMech;

// FRS tuning: points earned per level, spent on the local player's NEXT (press T).
// Kept for the whole run (reset when a new run starts). Only affects things the player's own
// machine runs (boost speeds and EN), so it works the same as host or client.
public static class Tuning
{
	public enum Stat { QuickBoost, OveredBoost, MainBooster, Output, Capacity }

	public static readonly string[] Names = { "QUICK BOOST THRUST", "OVERED BOOST THRUST", "MAIN BOOSTER (HOVER)", "GENERATOR OUTPUT", "GENERATOR CAPACITY" };
	public static readonly string[] Info = { "Quick boost distance", "Overed boost top speed", "Hover EN cost", "EN recovery speed", "Max EN" };

	public static readonly int[] Points = new int[5];

	public static void Init()
	{
		Run.onRunStartGlobal += _ => Array.Clear(Points, 0, Points.Length);
		GameObject go = new GameObject("NextTuningScreen");
		UnityEngine.Object.DontDestroyOnLoad(go);
		go.AddComponent<TuningScreen>();
	}

	public static float Mult(Stat s)
	{
		return 1f + Points[(int)s] * NextConfig.TunePerPoint.Value;
	}

	public static int Earned(CharacterBody body)
	{
		if (!body) return 0;
		return Mathf.Max(0, Mathf.FloorToInt((Mathf.Floor(body.level) - 1f) * NextConfig.TunePointsPerLevel.Value));
	}

	public static int Spent()
	{
		int n = 0;
		foreach (int p in Points) n += p;
		return n;
	}

	public static int Free(CharacterBody body) => Earned(body) - Spent();
}

public class TuningScreen : MonoBehaviour
{
	public static bool Open;
	private static object eventSystem;
	private bool cursorOpened;
	private GUIStyle label, button, title;
	private Texture2D white;

	private static CharacterBody LocalNext()
	{
		CharacterBody b = LocalUserManager.GetFirstLocalUser()?.cachedBody;
		return b && HPUtil.IsMine(b) && b.healthComponent && b.healthComponent.alive ? b : null;
	}

	private void Update()
	{
		CharacterBody body = LocalNext();
		if (!body)
		{
			SetOpen(false);
			return;
		}
		if (NextPilot.KeyDown(NextConfig.TuneKey.Value)) SetOpen(!Open);
		if (Open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
	}

	private void SetOpen(bool on)
	{
		Open = on;
		if (on == cursorOpened) return;
		try
		{
			var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
			if (eventSystem == null)
			{
				LocalUser lu = LocalUserManager.GetFirstLocalUser();
				if (lu != null) eventSystem = lu.GetType().GetProperty("eventSystem", bf)?.GetValue(lu) ?? lu.GetType().GetField("eventSystem", bf)?.GetValue(lu);
			}
			if (eventSystem == null) return;
			Type t = eventSystem.GetType();
			var p = t.GetProperty("cursorOpenerCount", bf);
			var f = p == null ? t.GetField("cursorOpenerCount", bf) : null;
			int d = on ? 1 : -1;
			if (p != null && p.CanWrite) p.SetValue(eventSystem, Mathf.Max(0, (int)p.GetValue(eventSystem) + d));
			else if (f != null) f.SetValue(eventSystem, Mathf.Max(0, (int)f.GetValue(eventSystem) + d));
			else return;
			cursorOpened = on;
		}
		catch (Exception e)
		{
			HPPlugin.Log.LogWarning("Tuning cursor: " + e.Message);
		}
	}

	private void OnDisable() => SetOpen(false);

	private void OnGUI()
	{
		CharacterBody body = LocalNext();
		if (!body) return;
		float k = Screen.height / 1080f;
		Setup(k);
		int free = Tuning.Free(body);
		if (!Open)
		{
			if (free > 0)
			{
				GUI.color = new Color(0.4f, 0.95f, 1f);
				GUI.Label(new Rect(60f * k, Screen.height - 330f * k, 600f * k, 30f * k), $"FRS POINTS: {free}  ·  [{NextConfig.TuneKey.Value}] TUNE", label);
				GUI.color = Color.white;
			}
			return;
		}
		float w = 760f * k, h = 470f * k;
		float x = (Screen.width - w) / 2f, y = (Screen.height - h) / 2f;
		GUI.color = new Color(0.03f, 0.05f, 0.07f, 0.92f);
		GUI.DrawTexture(new Rect(x, y, w, h), white);
		GUI.color = new Color(0.4f, 0.95f, 1f, 0.9f);
		GUI.DrawTexture(new Rect(x, y, w, 2f * k), white);
		GUI.DrawTexture(new Rect(x, y + h - 2f * k, w, 2f * k), white);
		GUI.color = Color.white;
		GUI.Label(new Rect(x + 30f * k, y + 20f * k, w, 40f * k), "ACSIS // TUNE", title);
		GUI.Label(new Rect(x + w - 330f * k, y + 28f * k, 300f * k, 30f * k), $"FRS MEMORY  {free} / {Tuning.Earned(body)}", label);
		float rowY = y + 90f * k;
		int max = NextConfig.TuneMaxPerStat.Value;
		for (int i = 0; i < Tuning.Points.Length; i++)
		{
			float ry = rowY + i * 66f * k;
			GUI.Label(new Rect(x + 30f * k, ry, 360f * k, 30f * k), Tuning.Names[i], label);
			GUI.color = new Color(0.6f, 0.65f, 0.7f);
			GUI.Label(new Rect(x + 30f * k, ry + 26f * k, 360f * k, 24f * k), $"{Tuning.Info[i]}  +{Tuning.Points[i] * NextConfig.TunePerPoint.Value * 100f:0}%", label);
			GUI.color = Color.white;
			for (int s = 0; s < max; s++)
			{
				GUI.color = s < Tuning.Points[i] ? new Color(0.4f, 0.95f, 1f) : new Color(0.2f, 0.25f, 0.3f);
				GUI.DrawTexture(new Rect(x + 400f * k + s * 20f * k, ry + 8f * k, 14f * k, 22f * k), white);
			}
			GUI.color = Color.white;
			if (GUI.Button(new Rect(x + 620f * k, ry + 2f * k, 50f * k, 36f * k), "-", button) && Tuning.Points[i] > 0) Tuning.Points[i]--;
			if (GUI.Button(new Rect(x + 680f * k, ry + 2f * k, 50f * k, 36f * k), "+", button) && free > 0 && Tuning.Points[i] < max) Tuning.Points[i]++;
		}
		GUI.color = new Color(0.6f, 0.65f, 0.7f);
		GUI.Label(new Rect(x + 30f * k, y + h - 44f * k, w - 60f * k, 30f * k), $"1 point per level  ·  [{NextConfig.TuneKey.Value}] or ESC to close", label);
		GUI.color = Color.white;
	}

	private void Setup(float k)
	{
		if (!white)
		{
			white = new Texture2D(1, 1);
			white.SetPixel(0, 0, Color.white);
			white.Apply();
		}
		Font f = CustomModel.GetFont("Rajdhani");
		label ??= new GUIStyle { normal = { textColor = Color.white } };
		title ??= new GUIStyle { normal = { textColor = new Color(0.4f, 0.95f, 1f) } };
		button ??= new GUIStyle(GUI.skin.button);
		label.font = f; title.font = f; button.font = f;
		label.fontSize = Mathf.RoundToInt(22f * k);
		title.fontSize = Mathf.RoundToInt(32f * k);
		button.fontSize = Mathf.RoundToInt(26f * k);
	}
}

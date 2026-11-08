using System;
using System.Collections.Generic;
using NextMech.Components;
using RoR2;
using UnityEngine;

namespace NextMech;

public class ACHud : MonoBehaviour
{
	private static readonly string[] HiddenParts = new string[4] { "BottomLeftCluster", "BottomRightCluster", "TopCenterCluster", "UpperLeftCluster" };

	private static readonly Color Line = new Color(0.89f, 0.93f, 0.95f, 0.92f);

	private static readonly Color Dim = new Color(0.78f, 0.84f, 0.87f, 0.5f);

	private static readonly Color Cyan = new Color(0.47f, 0.93f, 1f, 1f);

	private static readonly Color Amber = new Color(1f, 0.75f, 0.27f, 1f);

	private static readonly Color Red = new Color(1f, 0.33f, 0.24f, 1f);

	private static bool loggedError;

	private static bool loggedHide;

	private readonly List<ValueTuple<Texture, Rect, Color, int>> items = new List<ValueTuple<Texture, Rect, Color, int>>();

	private Texture2D white;

	private Font font;

	private Font mono;

	private GUIStyle style;

	private CharacterBody body;

	private CharacterBody target;

	private float targetTimer;

	private float itemTimer;

	private bool active;

	private float k;

	public static void Init()
	{
		GameObject obj = new GameObject("HardpointNextHUD");
		UnityEngine.Object.DontDestroyOnLoad(obj);
		obj.AddComponent<ACHud>();
	}

	private void Update()
	{
		try
		{
			CharacterBody characterBody = LocalUserManager.GetFirstLocalUser()?.cachedBody;
			bool flag = HPConfig.HudEnabled.Value && (bool)characterBody && HPUtil.IsMine(characterBody) && (bool)characterBody.healthComponent && characterBody.healthComponent.alive;
			if (flag != active)
			{
				active = flag;
				if (active && HPConfig.HudHideVanilla.Value)
				{
					HideVanilla();
				}
				else
				{
					ShowVanilla();
				}
			}
			body = (active ? characterBody : null);
			if (active)
			{
				if (HPConfig.HudHideVanilla.Value && VanillaHud.Stale())
				{
					HideVanilla();
				}
				VanillaHud.Tick();
				targetTimer -= Time.deltaTime;
				if (targetTimer <= 0f)
				{
					targetTimer = 0.1f;
					target = FindTarget();
				}
				itemTimer -= Time.deltaTime;
				if (itemTimer <= 0f)
				{
					itemTimer = 0.5f;
					RefreshItems();
				}
			}
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				HPPlugin.Log.LogWarning("AC HUD (logged once): " + ex);
			}
			loggedError = true;
		}
	}

	private void HideVanilla()
	{
		int num = VanillaHud.Hide(HiddenParts);
		if (!loggedHide)
		{
			HPPlugin.Log.LogInfo($"AC HUD up: hid {num} vanilla HUD parts (objective panel kept).");
		}
		loggedHide = true;
	}

	private void ShowVanilla()
	{
		VanillaHud.Show();
	}

	private static Transform Find(Transform root, string name)
	{
		if (root.name == name)
		{
			return root;
		}
		foreach (Transform item in root)
		{
			Transform transform = Find(item, name);
			if ((bool)transform)
			{
				return transform;
			}
		}
		return null;
	}

	private Camera Cam()
	{
		CameraRigController cameraRigController = LocalUserManager.GetFirstLocalUser()?.cameraRigController;
		if (!cameraRigController || !cameraRigController.sceneCam)
		{
			return Camera.main;
		}
		return cameraRigController.sceneCam;
	}

	private CharacterBody FindTarget()
	{
		Camera camera = Cam();
		if (!camera || !body)
		{
			return null;
		}
		RaycastHit[] array = Physics.SphereCastAll(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), 1.2f, 250f, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			HurtBox hurtBox = (raycastHit.collider ? raycastHit.collider.GetComponent<HurtBox>() : null);
			CharacterBody characterBody = (((bool)hurtBox && (bool)hurtBox.healthComponent) ? hurtBox.healthComponent.body : null);
			if ((bool)characterBody && characterBody != body && hurtBox.healthComponent.alive && HPUtil.IsEnemy(body.teamComponent.teamIndex, characterBody))
			{
				return characterBody;
			}
		}
		if (!target || !target.healthComponent || !target.healthComponent.alive || !Visible(target))
		{
			return null;
		}
		return target;
	}

	private bool Visible(CharacterBody b)
	{
		Camera camera = Cam();
		if (!camera)
		{
			return false;
		}
		Vector3 vector = camera.WorldToViewportPoint(b.corePosition);
		if (vector.z > 0f && vector.x > 0.3f && vector.x < 0.7f && vector.y > 0.25f)
		{
			return vector.y < 0.75f;
		}
		return false;
	}

	private void RefreshItems()
	{
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		items.Clear();
		Inventory inventory = (((bool)body && (bool)body.master) ? body.master.inventory : null);
		if (!inventory)
		{
			return;
		}
		foreach (ItemDef allItemDef in ItemCatalog.allItemDefs)
		{
			if (!allItemDef || allItemDef.hidden || allItemDef.tier == ItemTier.NoTier)
			{
				continue;
			}
			int itemCount = inventory.GetItemCount(allItemDef.itemIndex);
			if (itemCount > 0)
			{
				Sprite pickupIconSprite = allItemDef.pickupIconSprite;
				if ((bool)pickupIconSprite && (bool)pickupIconSprite.texture)
				{
					Rect textureRect = pickupIconSprite.textureRect;
					Rect rect = new Rect(textureRect.x / (float)pickupIconSprite.texture.width, textureRect.y / (float)pickupIconSprite.texture.height, textureRect.width / (float)pickupIconSprite.texture.width, textureRect.height / (float)pickupIconSprite.texture.height);
					items.Add(new ValueTuple<Texture, Rect, Color, int>((Texture)pickupIconSprite.texture, rect, TierColor(allItemDef.tier), itemCount));
				}
			}
		}
	}

	public static Color TierColor(ItemTier tier)
	{
		switch (tier)
		{
		case ItemTier.Tier2:
			return new Color(0.47f, 0.78f, 0.35f);
		case ItemTier.Tier3:
			return new Color(0.9f, 0.27f, 0.24f);
		case ItemTier.Boss:
			return new Color(0.98f, 0.78f, 0.24f);
		case ItemTier.Lunar:
			return new Color(0.35f, 0.55f, 1f);
		case ItemTier.VoidTier1:
		case ItemTier.VoidTier2:
		case ItemTier.VoidTier3:
		case ItemTier.VoidBoss:
			return new Color(0.75f, 0.35f, 0.9f);
		default:
			return new Color(0.93f, 0.93f, 0.93f);
		}
	}

	private void OnGUI()
	{
		if (!active || !body || Event.current.type != EventType.Repaint)
		{
			return;
		}
		try
		{
			Draw();
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				HPPlugin.Log.LogWarning("AC HUD draw (logged once): " + ex);
			}
			loggedError = true;
		}
	}

	private void Setup()
	{
		if (!white)
		{
			white = new Texture2D(1, 1);
			white.SetPixel(0, 0, Color.white);
			white.Apply();
		}
		if (!font)
		{
			font = CustomModel.GetFont("Rajdhani") ?? Font.CreateDynamicFontFromOSFont(new string[3] { "Bahnschrift", "Segoe UI", "Arial" }, 32);
		}
		if (!mono)
		{
			mono = CustomModel.GetFont("ShareTechMono") ?? Font.CreateDynamicFontFromOSFont(new string[3] { "Consolas", "Lucida Console", "Arial" }, 32);
		}
		if (style == null)
		{
			style = new GUIStyle
			{
				richText = false,
				clipping = TextClipping.Overflow
			};
		}
	}

	private void Draw()
	{
		//IL_0b1e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b23: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b4e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b84: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b8b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b97: Unknown result type (might be due to invalid IL or missing references)
		Setup();
		this.k = (float)Screen.height / 1080f * HPConfig.HudScale.Value;
		float num = (float)Screen.width / this.k;
		float num2 = (float)Screen.height / this.k;
		float num3 = num / 2f;
		float num4 = num2 / 2f - 10f;
		HPStatus hPStatus = HPStatus.Of(body);
		SceneDef sceneDefForCurrentScene = SceneCatalog.GetSceneDefForCurrentScene();
		string arg = (sceneDefForCurrentScene ? Language.GetString(sceneDefForCurrentScene.nameToken).ToUpperInvariant() : "");
		int num5 = ((!Run.instance) ? 1 : (Run.instance.stageClearCount + 1));
		Text(60f, 92f, "COMBAT OPERATION", 25, Line, TextAnchor.UpperLeft);
		Rect(60f, 124f, 360f, 1f, Dim);
		Text(60f, 130f, $"STAGE {num5}  ·  {arg}", 20, Dim, TextAnchor.UpperLeft);
		float num6 = (Run.instance ? Run.instance.GetRunStopwatch() : 0f);
		float num7 = VanillaHud.RightEdge(this.k, num, 260f, 88f, 184f);
		Text(num7 - 60f, 88f, $"{(int)(num6 / 60f):00}:{(int)(num6 % 60f):00}", 34, Line, TextAnchor.UpperRight, mono);
		uint num8 = (body.master ? body.master.money : 0u);
		Text(num7 - 60f, 130f, $"COAM  $ {num8:N0}", 24, Amber, TextAnchor.UpperRight);
		Text(num7 - 60f, 160f, $"LUNAR  {VanillaHud.LunarCoins():N0}", 18, Dim, TextAnchor.UpperRight, mono);
		float num9 = 118f;
		Ring(num3, num4, num9, 2f, Line, 72);
		for (int i = 0; i < 360; i += 15)
		{
			float num10 = num9 - ((i % 90 == 0) ? 14f : 7f);
			float num11 = Mathf.Cos((float)i * (MathF.PI / 180f));
			float num12 = Mathf.Sin((float)i * (MathF.PI / 180f));
			LineSeg(num3 + num10 * num11, num4 + num10 * num12, num3 + num9 * num11, num4 + num9 * num12, 2f, Line);
		}
		LineSeg(num3 - 18f, num4, num3 - 6f, num4, 2f, Line);
		LineSeg(num3 + 6f, num4, num3 + 18f, num4, 2f, Line);
		LineSeg(num3, num4 - 18f, num3, num4 - 6f, 2f, Line);
		LineSeg(num3, num4 + 6f, num3, num4 + 18f, 2f, Line);
		Rect(num3 - 2f, num4 - 2f, 4f, 4f, Cyan);
		float r = num9 + 22f;
		Arc(num3, num4, r, 120f, 240f, 2f, Dim);
		Arc(num3, num4, r, -60f, 60f, 2f, Dim);
		if ((bool)target)
		{
			bool flag = (bool)HPContent.OverloadBuff && target.HasBuff(HPContent.OverloadBuff);
			float num13 = (flag ? 1f : (HPContent.StaggerBuff ? ((float)target.GetBuffCount(HPContent.StaggerBuff) / 100f) : 0f));
			if (num13 > 0f)
			{
				Arc(num3, num4, r, 240f - 120f * num13, 240f, 6f, flag ? Red : Amber);
				Arc(num3, num4, r, -60f, -60f + 120f * num13, 6f, flag ? Red : Amber);
			}
			if (flag)
			{
				Text(num3, num4 + num9 + 46f, "ACS OVERLOAD", 22, Red, TextAnchor.UpperCenter);
			}
			else
			{
				Text(num3, num4 + num9 + 46f, $"ACS {num13 * 100f:0}%", 22, Amber, TextAnchor.UpperCenter);
			}
			HealthComponent healthComponent = target.healthComponent;
			string text = Util.GetBestBodyName(target.gameObject).ToUpperInvariant();
			Text(num3, 40f, "TARGET: " + text, 22, Line, TextAnchor.UpperCenter);
			Bar(num3 - 260f, 70f, 520f, 6f, healthComponent ? healthComponent.combinedHealthFraction : 0f, Line);
			LineSeg(num3 - 270f, 64f, num3 - 270f, 82f, 1f, Line);
			LineSeg(num3 + 270f, 64f, num3 + 270f, 82f, 1f, Line);
			Vector2? vector = ToScreen(target.corePosition);
			if (vector.HasValue)
			{
				float num14 = 46f;
				Bracket(vector.Value.x, vector.Value.y, num14, Cyan);
				float num15 = Vector3.Distance(body.corePosition, target.corePosition);
				Text(vector.Value.x, vector.Value.y + num14 + 8f, $"LOCK  ·  {num15:0} m", 19, Cyan, TextAnchor.UpperCenter);
			}
		}
		if ((bool)hPStatus)
		{
			foreach (HurtBox @lock in hPStatus.locks)
			{
				if ((bool)@lock)
				{
					Vector2? vector2 = ToScreen(@lock.transform.position);
					if (vector2.HasValue)
					{
						Diamond(vector2.Value.x, vector2.Value.y, 13f, Red);
					}
				}
			}
			if (hPStatus.locks.Count > 0)
			{
				Text(num3, num4 - num9 - 40f, $"MISSILE LOCK  {hPStatus.locks.Count}/{HPConfig.MissileMaxLocks.Value}", 20, Red, TextAnchor.UpperCenter);
			}
		}
		for (int j = 0; j < 2; j++)
		{
			float num16 = ((j == 0) ? 300f : (num - 300f));
			Rect(num16, 300f, 1f, 480f, Dim);
			for (int k = 0; k < 13; k++)
			{
				Rect((j == 0) ? num16 : (num16 - (float)((k % 3 == 0) ? 16 : 8)), 300 + k * 40, (k % 3 == 0) ? 16 : 8, 1f, Dim);
			}
		}
		float num17 = 0f;
		if (Physics.Raycast(body.footPosition + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 200f, LayerIndex.world.mask))
		{
			num17 = hitInfo.distance - 0.5f;
		}
		Text(292f, 512f, $"ALT {num17:00}", 16, Dim, TextAnchor.UpperRight);
		float num18 = (body.characterMotor ? body.characterMotor.velocity.magnitude : 0f);
		// AC4 speedometer (km/h)
		float kmh = num18 * 3.6f * NextConfig.SpeedometerScale.Value;
		bool ob = IsOveredBoosting();
		Text(num - 292f, 528f, $"{kmh:0000}", 34, ob ? Amber : Line, TextAnchor.UpperLeft, mono);
		Text(num - 292f, 566f, "km/h", 16, Dim, TextAnchor.UpperLeft);
		HealthComponent healthComponent2 = body.healthComponent;
		float num19 = 60f;
		float num20 = num2 - 250f;
		// Primal Armor bar above the AP readout
		float pa = PrimalArmor.Fraction(body);
		Color paColor = new Color(0.45f, 1f, 0.75f);
		Text(num19, num20 - 46f, "PA", 20, paColor, TextAnchor.UpperLeft);
		Frame(num19 + 36f, num20 - 38f, 384f, 8f, 1f, Dim);
		Rect(num19 + 37f, num20 - 37f, 382f * pa, 6f, pa < 0.25f ? Red : paColor);
		Text(num19 + 430f, num20 - 46f, pa <= 0.01f ? "PA DOWN" : $"{pa * 100f:0}%", 18, pa <= 0.01f ? Red : paColor, TextAnchor.UpperLeft);
		Text(num19, num20, "AP", 26, Line, TextAnchor.UpperLeft);
		Text(num19 + 300f, num20 - 14f, $"{Mathf.CeilToInt(healthComponent2.combinedHealth):00000}", 64, Line, TextAnchor.UpperRight, mono);
		Text(num19 + 312f, num20 + 22f, $"/ {Mathf.CeilToInt(healthComponent2.fullCombinedHealth)}", 24, Dim, TextAnchor.UpperLeft);
		float num21 = ((healthComponent2.fullCombinedHealth > 0f) ? ((healthComponent2.health + healthComponent2.shield) / healthComponent2.fullCombinedHealth) : 0f);
		float num22 = ((healthComponent2.fullCombinedHealth > 0f) ? (healthComponent2.barrier / healthComponent2.fullCombinedHealth) : 0f);
		Bar(num19, num20 + 58f, 420f, 6f, num21, (num21 < 0.3f) ? Red : Line);
		if (num22 > 0f)
		{
			Rect(num19 + 420f * num21, num20 + 59f, 420f * Mathf.Min(num22, 1f - num21), 4f, new Color(0.7f, 0.82f, 1f, 0.85f));
		}
		Text(num19, num20 + 74f, $"LV {Mathf.FloorToInt(body.level):00}", 22, Line, TextAnchor.UpperLeft);
		Bar(num19 + 70f, num20 + 84f, 150f, 4f, ExpFraction(), Cyan);
		if (healthComponent2.barrier > 0f)
		{
			Text(num19 + 236f, num20 + 74f, $"BARRIER {Mathf.CeilToInt(healthComponent2.barrier)}", 20, new Color(0.7f, 0.82f, 1f), TextAnchor.UpperLeft);
		}
		Text(num19, num20 + 112f, "ITEMS", 18, Dim, TextAnchor.UpperLeft);
		int num23 = Mathf.Min(items.Count, 8);
		for (int l = 0; l < num23; l++)
		{
			ValueTuple<Texture, Rect, Color, int> val = items[l];
			float num24 = num19 + (float)(l * 54);
			float num25 = num20 + 138f;
			Frame(num24, num25, 46f, 46f, 2f, val.Item3);
			GUI.color = Color.white;
			GUI.DrawTextureWithTexCoords(Px(num24 + 4f, num25 + 4f, 38f, 38f), val.Item1, val.Item2);
			if (val.Item4 > 1)
			{
				Text(num24 + 44f, num25 + 28f, "x" + val.Item4, 15, Line, TextAnchor.UpperRight);
			}
		}
		if (items.Count > num23)
		{
			Text(num19 + (float)(num23 * 54) + 6f, num20 + 152f, "+" + (items.Count - num23), 20, Dim, TextAnchor.UpperLeft);
		}
		float num26 = num3 - 300f;
		float num27 = num2 - 92f;
		Text(num26 - 12f, num27 - 13f, "EN", 22, Line, TextAnchor.UpperRight);
		float num28 = (hPStatus ? hPStatus.Fraction : 1f);
		bool flag2 = (bool)hPStatus && hPStatus.redline;
		Frame(num26, num27 - 5f, 600f, 10f, 1f, Dim);
		Rect(num26 + 1f, num27 - 4f, 598f * num28, 8f, flag2 ? Red : Cyan);
		float num29 = num26 + 600f * HPConfig.RedlineRecover.Value;
		Rect(num29 - 1f, num27 - 12f, 2f, 24f, Red);
		Text(num29, num27 + 14f, flag2 ? "REDLINE" : "", 14, Red, TextAnchor.UpperCenter);
		if (ob)
		{
			Text(num3, num27 - 70f, "OVERED BOOST", 26, Amber, TextAnchor.UpperCenter);
		}
		NextPilot pilot = NextPilot.Of(body);
		if (pilot && Time.time - pilot.lastQbTime < 0.35f && pilot.lastQbWasSecond)
		{
			Text(num3, num27 - 44f, "2ND STAGE QB", 18, Cyan, TextAnchor.UpperCenter);
		}
		GenericSkill genericSkill = (body.skillLocator ? body.skillLocator.utility : null);
		if ((bool)genericSkill && genericSkill.maxStock > 1)
		{
			for (int m = 0; m < genericSkill.maxStock; m++)
			{
				float x = num3 - 25f * (float)(genericSkill.maxStock - 1) + (float)m * 50f;
				bool filled = m < genericSkill.stock;
				Parallelogram(x, num27 - 34f, filled, Cyan);
			}
			Text(num3 + 25f * (float)genericSkill.maxStock + 10f, num27 - 41f, "QB", 16, Cyan, TextAnchor.UpperLeft);
		}
		float x2 = num - 560f;
		float num30 = num2 - 310f;
		SkillLocator skillLocator = body.skillLocator;
		bool hanger = (bool)pilot && pilot.hangerOut;
		Slot(x2, num30, hanger ? "R-ARM · HANGER" : "R-ARM", skillLocator ? skillLocator.primary : null, "M1", hanger ? "" : (hPStatus ? hPStatus.magazine.ToString() : ""), hanger ? null : $"/ {HPConfig.RifleMagazine.Value}");
		Slot(x2, num30 + 64f, "L-ARM", skillLocator ? skillLocator.secondary : null, "M2", "", null);
		Slot(x2, num30 + 128f, "BOOSTER", skillLocator ? skillLocator.utility : null, "SHIFT", "", null);
		Slot(x2, num30 + 192f, "BACK / CORE", skillLocator ? skillLocator.special : null, "R", "", null);
		float num31 = 28f;
		Corner(num31, num31, 1, 1);
		Corner(num - num31, num31, -1, 1);
		Corner(num31, num2 - num31, 1, -1);
		Corner(num - num31, num2 - num31, -1, -1);
	}

	private bool IsOveredBoosting()
	{
		EntityStateMachine m = EntityStateMachine.FindByCustomName(body.gameObject, "Boost");
		return (bool)m && m.state is NextMech.SkillStates.OveredBoost;
	}

	private float ExpFraction()
	{
		TeamManager instance = TeamManager.instance;
		if (!instance || !body.teamComponent)
		{
			return 0f;
		}
		TeamIndex teamIndex = body.teamComponent.teamIndex;
		ulong teamExperience = instance.GetTeamExperience(teamIndex);
		ulong teamCurrentLevelExperience = instance.GetTeamCurrentLevelExperience(teamIndex);
		ulong teamNextLevelExperience = instance.GetTeamNextLevelExperience(teamIndex);
		if (teamNextLevelExperience <= teamCurrentLevelExperience)
		{
			return 0f;
		}
		return Mathf.Clamp01((float)(teamExperience - teamCurrentLevelExperience) / (float)(teamNextLevelExperience - teamCurrentLevelExperience));
	}

	private void Slot(float x, float y, string slot, GenericSkill skill, string key, string big, string small)
	{
		string text = (((bool)skill && (bool)skill.skillDef) ? Language.GetString(skill.skillDef.skillNameToken).ToUpperInvariant() : "-");
		bool flag = (bool)skill && skill.stock > 0;
		float num = (skill ? skill.cooldownRemaining : 0f);
		float b = (((bool)skill && (bool)skill.skillDef) ? skill.skillDef.baseRechargeInterval : 1f);
		float value = ((!skill) ? 0f : (flag ? 1f : (1f - num / Mathf.Max(0.01f, b))));
		Text(x, y, slot, 16, Dim, TextAnchor.UpperLeft);
		Text(x, y + 16f, text, 24, flag ? Line : Dim, TextAnchor.UpperLeft);
		Frame(x + 250f, y + 8f, 30 + ((key.Length > 2) ? 26 : 0), 26f, 1f, Line);
		Text(x + 265f + (float)((key.Length > 2) ? 13 : 0), y + 10f, key, 18, Line, TextAnchor.UpperCenter);
		if (!string.IsNullOrEmpty(big))
		{
			Text(x + 400f, y + 2f, big, 34, Line, TextAnchor.UpperRight, mono);
		}
		Text(x + 410f, y + 16f, small ?? ((!flag) ? $"{num:0.0}" : ((skill.maxStock > 1) ? $"x{skill.stock}" : "READY")), 18, flag ? Cyan : Amber, TextAnchor.UpperLeft);
		Rect(x, y + 48f, 410f, 1f, Dim);
		Rect(x, y + 47f, 410f * Mathf.Clamp01(value), 3f, flag ? Cyan : Amber);
	}

	private Rect Px(float x, float y, float w, float h)
	{
		return new Rect(x * k, y * k, w * k, h * k);
	}

	private void Rect(float x, float y, float w, float h, Color c)
	{
		GUI.color = c;
		GUI.DrawTexture(Px(x, y, w, h), white);
	}

	private void Frame(float x, float y, float w, float h, float t, Color c)
	{
		Rect(x, y, w, t, c);
		Rect(x, y + h - t, w, t, c);
		Rect(x, y, t, h, c);
		Rect(x + w - t, y, t, h, c);
	}

	private void Bar(float x, float y, float w, float h, float frac, Color c)
	{
		Frame(x, y, w, h, 1f, Dim);
		Rect(x + 1f, y + 1f, (w - 2f) * Mathf.Clamp01(frac), h - 2f, c);
	}

	private void LineSeg(float x0, float y0, float x1, float y1, float w, Color c)
	{
		Vector2 vector = new Vector2(x0 * k, y0 * k);
		Vector2 b = new Vector2(x1 * k, y1 * k);
		float num = Vector2.Distance(vector, b);
		if (!(num < 0.01f))
		{
			float angle = Mathf.Atan2(b.y - vector.y, b.x - vector.x) * 57.29578f;
			Matrix4x4 matrix = GUI.matrix;
			GUIUtility.RotateAroundPivot(angle, vector);
			GUI.color = c;
			GUI.DrawTexture(new Rect(vector.x, vector.y - w * k / 2f, num, Mathf.Max(1f, w * k)), white);
			GUI.matrix = matrix;
		}
	}

	private void Arc(float cx, float cy, float r, float a0, float a1, float w, Color c, int segments = 24)
	{
		for (int i = 0; i < segments; i++)
		{
			float f = MathF.PI / 180f * Mathf.Lerp(a0, a1, (float)i / (float)segments);
			float f2 = MathF.PI / 180f * Mathf.Lerp(a0, a1, (float)(i + 1) / (float)segments);
			LineSeg(cx + r * Mathf.Cos(f), cy + r * Mathf.Sin(f), cx + r * Mathf.Cos(f2), cy + r * Mathf.Sin(f2), w, c);
		}
	}

	private void Ring(float cx, float cy, float r, float w, Color c, int segments)
	{
		Arc(cx, cy, r, 0f, 360f, w, c, segments);
	}

	private void Bracket(float x, float y, float s, Color c)
	{
		int[] array = new int[2] { -1, 1 };
		foreach (int num in array)
		{
			int[] array2 = new int[2] { -1, 1 };
			foreach (int num2 in array2)
			{
				LineSeg(x + (float)num * s, y + (float)num2 * s, x + (float)num * (s - 20f), y + (float)num2 * s, 3f, c);
				LineSeg(x + (float)num * s, y + (float)num2 * s, x + (float)num * s, y + (float)num2 * (s - 20f), 3f, c);
			}
		}
	}

	private void Diamond(float x, float y, float s, Color c)
	{
		LineSeg(x, y - s, x + s, y, 2f, c);
		LineSeg(x + s, y, x, y + s, 2f, c);
		LineSeg(x, y + s, x - s, y, 2f, c);
		LineSeg(x - s, y, x, y - s, 2f, c);
	}

	private void Parallelogram(float x, float y, bool filled, Color c)
	{
		if (filled)
		{
			for (int i = 0; i < 12; i++)
			{
				LineSeg(x - 14f - (float)i * 0.5f, y + (float)i, x + 14f - (float)i * 0.5f, y + (float)i, 1.2f, c);
			}
		}
		LineSeg(x - 14f, y, x + 14f, y, 1.5f, c);
		LineSeg(x + 14f, y, x + 8f, y + 12f, 1.5f, c);
		LineSeg(x + 8f, y + 12f, x - 20f, y + 12f, 1.5f, c);
		LineSeg(x - 20f, y + 12f, x - 14f, y, 1.5f, c);
	}

	private void Corner(float x, float y, int sx, int sy)
	{
		Rect((sx > 0) ? x : (x - 60f), y - 1f, 60f, 2f, Dim);
		Rect(x - 1f, (sy > 0) ? y : (y - 60f), 2f, 60f, Dim);
	}

	private void Text(float x, float y, string text, int size, Color c, TextAnchor anchor, Font f = null)
	{
		style.font = f ?? font;
		style.fontSize = Mathf.Max(6, Mathf.RoundToInt((float)size * k));
		style.normal.textColor = c;
		style.alignment = anchor;
		float num = 900f;
		float h = (float)size * 1.6f;
		float x2 = anchor switch
		{
			TextAnchor.UpperCenter => x - num / 2f, 
			TextAnchor.UpperRight => x - num, 
			_ => x, 
		};
		GUI.color = Color.white;
		GUI.Label(Px(x2, y, num, h), text, style);
	}

	private Vector2? ToScreen(Vector3 world)
	{
		Camera camera = Cam();
		if (!camera)
		{
			return null;
		}
		Vector3 vector = camera.WorldToScreenPoint(world);
		if (vector.z <= 0f)
		{
			return null;
		}
		return new Vector2(vector.x / k, ((float)Screen.height - vector.y) / k);
	}
}

using System;
using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace NextMech;

internal static class VanillaHud
{
	private static readonly List<GameObject> off = new List<GameObject>();

	private static readonly List<CanvasGroup> faded = new List<CanvasGroup>();

	private static readonly HashSet<HUD> applied = new HashSet<HUD>();

	private static readonly Vector3[] corners = new Vector3[4];

	private static float nextRect;

	public static Rect? ObjectiveRect { get; private set; }

	public static bool Stale()
	{
		foreach (HUD readOnlyInstance in HUD.readOnlyInstanceList)
		{
			if ((bool)readOnlyInstance && !applied.Contains(readOnlyInstance))
			{
				return true;
			}
		}
		return false;
	}

	public static int Hide(string[] parts)
	{
		off.RemoveAll((GameObject g) => !g);
		faded.RemoveAll((CanvasGroup c) => !c);
		applied.RemoveWhere((HUD h) => !h);
		foreach (HUD readOnlyInstance in HUD.readOnlyInstanceList)
		{
			if (!readOnlyInstance || applied.Contains(readOnlyInstance))
			{
				continue;
			}
			applied.Add(readOnlyInstance);
			foreach (string name in parts)
			{
				Transform transform = Find(readOnlyInstance.transform, name);
				if ((bool)transform && transform.gameObject.activeSelf)
				{
					transform.gameObject.SetActive(value: false);
					off.Add(transform.gameObject);
				}
			}
			Transform transform2 = Find(readOnlyInstance.transform, "UpperRightCluster");
			if ((bool)transform2)
			{
				FadeAllBut(transform2, readOnlyInstance.GetComponentInChildren<ObjectivePanelController>(includeInactive: true));
			}
		}
		return off.Count + faded.Count;
	}

	public static void Show()
	{
		foreach (GameObject item in off)
		{
			if ((bool)item)
			{
				item.SetActive(value: true);
			}
		}
		foreach (CanvasGroup item2 in faded)
		{
			if ((bool)item2)
			{
				item2.alpha = 1f;
				item2.blocksRaycasts = true;
				item2.interactable = true;
			}
		}
		off.Clear();
		faded.Clear();
		applied.Clear();
		ObjectiveRect = null;
	}

	private static void FadeAllBut(Transform root, ObjectivePanelController keep)
	{
		if (!keep || !keep.transform.IsChildOf(root))
		{
			Fade(root);
			return;
		}
		Transform transform = keep.transform;
		while ((bool)transform && transform != root)
		{
			Transform parent = transform.parent;
			if (!parent)
			{
				break;
			}
			foreach (Transform item in parent)
			{
				if (item != transform)
				{
					Fade(item);
				}
			}
			transform = parent;
		}
	}

	private static void Fade(Transform t)
	{
		CanvasGroup canvasGroup = t.GetComponent<CanvasGroup>();
		if (!canvasGroup)
		{
			canvasGroup = t.gameObject.AddComponent<CanvasGroup>();
		}
		if ((bool)canvasGroup && !faded.Contains(canvasGroup))
		{
			canvasGroup.alpha = 0f;
			canvasGroup.blocksRaycasts = false;
			canvasGroup.interactable = false;
			faded.Add(canvasGroup);
		}
	}

	public static void Tick()
	{
		if (Time.unscaledTime < nextRect)
		{
			return;
		}
		nextRect = Time.unscaledTime + 0.5f;
		foreach (CanvasGroup item in faded)
		{
			if ((bool)item && item.alpha > 0f)
			{
				item.alpha = 0f;
			}
		}
		ObjectiveRect = null;
		foreach (HUD readOnlyInstance in HUD.readOnlyInstanceList)
		{
			if (!readOnlyInstance)
			{
				continue;
			}
			ObjectivePanelController componentInChildren = readOnlyInstance.GetComponentInChildren<ObjectivePanelController>();
			RectTransform rectTransform = (componentInChildren ? (componentInChildren.transform as RectTransform) : null);
			if ((bool)rectTransform && componentInChildren.gameObject.activeInHierarchy)
			{
				Canvas componentInParent = rectTransform.GetComponentInParent<Canvas>();
				Camera cam = (((bool)componentInParent && componentInParent.renderMode != 0) ? componentInParent.worldCamera : null);
				rectTransform.GetWorldCorners(corners);
				Vector2 vector = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
				Vector2 vector2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
				float num = Mathf.Min(vector.x, vector2.x);
				float num2 = Mathf.Max(vector.x, vector2.x);
				float num3 = Mathf.Min(vector.y, vector2.y);
				float num4 = Mathf.Max(vector.y, vector2.y);
				if (!(num2 - num < 4f) && !(num4 - num3 < 4f))
				{
					ObjectiveRect = new Rect(num, (float)Screen.height - num4, num2 - num, num4 - num3);
					break;
				}
			}
		}
	}

	public static float RightEdge(float k, float W, float width, float top, float bottom)
	{
		Rect? objectiveRect = ObjectiveRect;
		if (!objectiveRect.HasValue || k <= 0f)
		{
			return W;
		}
		Rect value = objectiveRect.Value;
		if (!new Rect((W - width) * k, top * k, width * k, (bottom - top) * k).Overlaps(value))
		{
			return W;
		}
		return Mathf.Max(width + 10f, (value.xMin - 12f) / k);
	}

	public static uint LunarCoins()
	{
		try
		{
			NetworkUser networkUser = LocalUserManager.GetFirstLocalUser()?.currentNetworkUser;
			return networkUser ? networkUser.lunarCoins : 0u;
		}
		catch (Exception)
		{
			return 0u;
		}
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
}

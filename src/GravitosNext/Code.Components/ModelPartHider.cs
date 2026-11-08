using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GravitosNext.Components;

public class ModelPartHider : MonoBehaviour
{
	private static bool dumped;

	private readonly List<Transform> hiddenBones = new List<Transform>();

	private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

	private static string Normalize(string s)
	{
		return new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
	}

	private void Awake()
	{
		string[] keywords = (from k in GravitosConfig.HiddenPartKeywords.Value.Split(',').Select(Normalize)
			where k.Length > 0
			select k).ToArray();
		Transform[] componentsInChildren = GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform != base.transform && Matches(transform.name) && !hiddenBones.Any(transform.IsChildOf))
			{
				hiddenBones.Add(transform);
			}
		}
		Renderer[] componentsInChildren2 = GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren2)
		{
			if (Matches(renderer.name))
			{
				hiddenRenderers.Add(renderer);
			}
		}
		if (GravitosConfig.DumpModelHierarchy.Value && !dumped)
		{
			dumped = true;
			Dump();
		}
		if (hiddenBones.Count == 0 && hiddenRenderers.Count == 0 && keywords.Length != 0)
		{
			GravitosPlugin.Log.LogWarning("ModelPartHider on " + base.name + ": no bone or renderer matched [" + string.Join(", ", keywords) + "]. Check the model dump in this log and update 'Hidden Part Keywords' in the config.");
			return;
		}
		GravitosPlugin.Log.LogInfo("ModelPartHider on " + base.name + ": hiding bones [" + string.Join(", ", hiddenBones.Select((Transform b) => b.name)) + "] and renderers [" + string.Join(", ", hiddenRenderers.Select((Renderer r) => r.name)) + "].");
		bool Matches(string name)
		{
			string j = Normalize(name);
			return keywords.Any((string k) => j.Contains(k));
		}
	}

	private void LateUpdate()
	{
		foreach (Transform hiddenBone in hiddenBones)
		{
			if ((bool)hiddenBone)
			{
				hiddenBone.localScale = Vector3.zero;
			}
		}
		foreach (Renderer hiddenRenderer in hiddenRenderers)
		{
			if ((bool)hiddenRenderer)
			{
				hiddenRenderer.forceRenderingOff = true;
			}
		}
	}

	private void Dump()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Gravitos model hierarchy for '" + base.name + "' (use these names for 'Hidden Part Keywords'):");
		AppendTree(stringBuilder, base.transform, 0);
		stringBuilder.AppendLine("Renderers:");
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			stringBuilder.AppendLine("  " + renderer.name + " (" + renderer.GetType().Name + ")");
		}
		GravitosPlugin.Log.LogInfo(stringBuilder.ToString());
	}

	private static void AppendTree(StringBuilder sb, Transform t, int depth)
	{
		sb.Append(' ', depth * 2).AppendLine(t.name);
		foreach (Transform item in t)
		{
			AppendTree(sb, item, depth + 1);
		}
	}
}

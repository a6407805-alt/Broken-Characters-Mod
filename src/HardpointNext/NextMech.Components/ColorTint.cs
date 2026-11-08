using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace NextMech.Components;

public class ColorTint : MonoBehaviour
{
	private static readonly Dictionary<ValueTuple<Material, Color>, Material> copies = new Dictionary<ValueTuple<Material, Color>, Material>();

	private static readonly HashSet<Material> tinted = new HashSet<Material>();

	public Color tint = Color.white;

	private CharacterModel characterModel;

	private Renderer[] renderers;

	private void Awake()
	{
		characterModel = GetComponent<CharacterModel>();
		if (!characterModel)
		{
			renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
		}
	}

	private void LateUpdate()
	{
		if ((bool)characterModel && characterModel.baseRendererInfos != null)
		{
			for (int i = 0; i < characterModel.baseRendererInfos.Length; i++)
			{
				Material defaultMaterial = characterModel.baseRendererInfos[i].defaultMaterial;
				Material material = Tint(defaultMaterial);
				if (material != defaultMaterial)
				{
					characterModel.baseRendererInfos[i].defaultMaterial = material;
				}
			}
		}
		else
		{
			if (renderers == null)
			{
				return;
			}
			Renderer[] array = renderers;
			foreach (Renderer renderer in array)
			{
				if ((bool)renderer && !(renderer is ParticleSystemRenderer))
				{
					Material material2 = Tint(renderer.sharedMaterial);
					if (material2 != renderer.sharedMaterial)
					{
						renderer.sharedMaterial = material2;
					}
				}
			}
		}
	}

	private Material Tint(Material original)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (!original || tinted.Contains(original))
		{
			return original;
		}
		if (copies.TryGetValue(new ValueTuple<Material, Color>(original, tint), out var value))
		{
			return value;
		}
		value = new Material(original);
		if (value.HasProperty("_Color"))
		{
			value.color *= tint;
		}
		copies[new ValueTuple<Material, Color>(original, tint)] = value;
		tinted.Add(value);
		return value;
	}
}

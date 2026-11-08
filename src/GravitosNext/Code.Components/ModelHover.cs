using System;
using UnityEngine;

namespace GravitosNext.Components;

public class ModelHover : MonoBehaviour
{
	private const float BobAmount = 0.08f;

	private const float BobSpeed = 2.2f;

	private Transform armature;

	private Vector3 restLocalPosition;

	private float phase;

	private void Awake()
	{
		foreach (Transform item in base.transform)
		{
			if (item.name.ToLowerInvariant().Contains("armature"))
			{
				armature = item;
				break;
			}
		}
		if ((bool)armature)
		{
			restLocalPosition = armature.localPosition;
		}
		else
		{
			GravitosPlugin.Log.LogWarning("ModelHover on " + base.name + ": no armature found; Gravitos will not float.");
		}
		phase = UnityEngine.Random.value * MathF.PI * 2f;
	}

	private void LateUpdate()
	{
		if ((bool)armature && (bool)armature.parent)
		{
			float num = GravitosConfig.HoverHeight.Value + Mathf.Sin(Time.time * 2.2f + phase) * 0.08f;
			armature.localPosition = restLocalPosition + armature.parent.InverseTransformVector(Vector3.up * num);
		}
	}
}

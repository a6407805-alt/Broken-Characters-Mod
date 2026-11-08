using RoR2;
using UnityEngine;

namespace NextMech.Components;

public class SpawnReport : MonoBehaviour
{
	private static bool reported;

	private void Start()
	{
		if (reported)
		{
			return;
		}
		reported = true;
		CharacterBody component = GetComponent<CharacterBody>();
		Transform transform = (((bool)component && (bool)component.modelLocator) ? component.modelLocator.modelTransform : null);
		int num = 0;
		int num2 = 0;
		if ((bool)transform)
		{
			Renderer[] componentsInChildren = transform.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				num2++;
				if (renderer.enabled && renderer.gameObject.activeInHierarchy)
				{
					num++;
				}
			}
		}
		Animator animator = (transform ? transform.GetComponent<Animator>() : null);
		HPPlugin.Log.LogInfo(string.Format("HardpointNext spawn report: custom model {0}, model '{1}' ", CustomModel.Active, transform ? transform.name : "none") + $"(scale {(transform ? transform.lossyScale.x : 0f):0.##}), renderers enabled {num}/{num2}, " + "animator " + (((bool)animator && (bool)animator.runtimeAnimatorController) ? animator.runtimeAnimatorController.name : "none") + ", skills " + ((!component || !component.skillLocator || !component.skillLocator.primary) ? "none" : component.skillLocator.primary.skillDef?.skillName) + ".");
	}
}

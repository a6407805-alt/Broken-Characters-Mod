using UnityEngine;
using UnityEngine.Rendering;

namespace NextMech.Components;

public class MissileTrail : MonoBehaviour
{
	private TrailRenderer trail;

	private void Awake()
	{
		trail = GetComponent<TrailRenderer>();
		if (!trail)
		{
			trail = base.gameObject.AddComponent<TrailRenderer>();
		}
		if ((bool)trail)
		{
			trail.time = 0.45f;
			trail.startWidth = 0.35f;
			trail.endWidth = 0.02f;
			trail.minVertexDistance = 0.3f;
			trail.material = HPUtil.LineMaterial();
			trail.startColor = new Color(1f, 0.85f, 0.6f, 0.9f);
			trail.endColor = new Color(0.6f, 0.6f, 0.65f, 0f);
			trail.shadowCastingMode = ShadowCastingMode.Off;
		}
	}

	private void OnEnable()
	{
		if ((bool)trail)
		{
			trail.Clear();
		}
	}
}

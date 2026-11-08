using NextMech.Components;
using RoR2;
using UnityEngine;

namespace NextMech;

// Booster exhaust on the back: a glow, a flame cone and a trail that flare up whenever the NEXT is boosting
// (vertical boost, air glide, quick boost, overed boost, fast ground boost). Visual only; runs on every machine
// (remote players are judged by how fast they move).
public class BoosterFx : MonoBehaviour
{
	private static Material flameMat;

	private CharacterBody body;
	private Transform anchor;
	private Light glow;
	private Transform flame;
	private TrailRenderer trail;
	private Vector3 lastPos;
	private Vector3 vel;
	private float intensity;
	private float findTimer;

	private void Awake()
	{
		body = GetComponent<CharacterBody>();
		lastPos = transform.position;
	}

	private static Material FlameMaterial()
	{
		if (flameMat) return flameMat;
		try
		{
			// Shader.Find("Sprites/Default") throws in RoR2; borrow the sprite shader from a SpriteRenderer.
			GameObject tmp = new GameObject("NextShaderProbe");
			Shader sh = tmp.AddComponent<SpriteRenderer>().sharedMaterial.shader;
			Object.Destroy(tmp);
			flameMat = new Material(sh) { color = new Color(0.55f, 0.9f, 1f, 0.85f) };
		}
		catch (System.Exception e)
		{
			HPPlugin.Log.LogWarning("Booster flame material: " + e.Message);
		}
		return flameMat;
	}

	private void Build()
	{
		Transform model = body && body.modelLocator ? body.modelLocator.modelTransform : null;
		ChildLocator cl = model ? model.GetComponent<ChildLocator>() : null;
		Transform booster = cl ? cl.FindChild("Booster") : null;
		GameObject root = new GameObject("NextBoosterFx");
		if (booster)
		{
			root.transform.SetParent(booster, false);
		}
		else
		{
			root.transform.SetParent(transform, false);
			root.transform.localPosition = new Vector3(0f, 2.2f, -0.9f);
		}
		anchor = root.transform;

		glow = root.AddComponent<Light>();
		glow.type = LightType.Point;
		glow.color = new Color(0.45f, 0.85f, 1f);
		glow.range = 7f;
		glow.intensity = 0f;

		Material mat = FlameMaterial();
		if (mat)
		{
			GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			cone.name = "Flame";
			Object.Destroy(cone.GetComponent<Collider>());
			cone.GetComponent<Renderer>().sharedMaterial = mat;
			cone.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			cone.transform.SetParent(root.transform, false);
			cone.transform.localScale = Vector3.zero;
			flame = cone.transform;

			trail = root.AddComponent<TrailRenderer>();
			trail.sharedMaterial = mat;
			trail.time = 0.22f;
			trail.startWidth = 0.55f;
			trail.endWidth = 0f;
			trail.minVertexDistance = 0.15f;
			trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			trail.emitting = false;
		}
	}

	private void LateUpdate()
	{
		if (!body) return;
		if (!anchor)
		{
			findTimer -= Time.deltaTime;
			if (findTimer > 0f) return;
			findTimer = 0.5f;
			if (!body.modelLocator || !body.modelLocator.modelTransform) return;
			Build();
		}
		float dt = Mathf.Max(Time.deltaTime, 0.0001f);
		Vector3 pos = transform.position;
		vel = Vector3.Lerp(vel, (pos - lastPos) / dt, 0.25f);
		lastPos = pos;

		float target = 0f;
		float flat = new Vector3(vel.x, 0f, vel.z).magnitude;
		float baseSpeed = Mathf.Max(1f, body.baseMoveSpeed);
		if (flat > baseSpeed * 1.3f) target = Mathf.Clamp01((flat - baseSpeed) / (baseSpeed * 3f)) * 0.8f + 0.2f;
		if (vel.y > 2f) target = Mathf.Max(target, 0.7f);
		HPStatus st = HPStatus.Of(body);
		if (body.hasEffectiveAuthority && st && st.thrusting) target = 1f;
		if (flat > 30f) target = 1.3f;   // overed boost
		intensity = Mathf.MoveTowards(intensity, target, dt * (target > intensity ? 10f : 4f));

		if (glow) glow.intensity = intensity * 5f;
		if (flame)
		{
			float flick = 1f + Mathf.Sin(Time.time * 47f) * 0.12f;
			float len = intensity * 1.6f * flick;
			flame.localScale = new Vector3(0.45f * intensity, 0.45f * intensity, len);
			Vector3 back = body.characterDirection ? -body.characterDirection.forward : -transform.forward;
			Vector3 exhaust = (back * 0.8f + Vector3.down * 0.6f).normalized;
			if (vel.y > 2f) exhaust = (Vector3.down + back * 0.3f).normalized;
			flame.rotation = Quaternion.LookRotation(exhaust);
			flame.position = anchor.position + exhaust * len * 0.5f;
		}
		if (trail) trail.emitting = intensity > 0.35f;
	}
}

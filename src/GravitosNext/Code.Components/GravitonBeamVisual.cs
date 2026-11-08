using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace GravitosNext.Components;

public class GravitonBeamVisual : MonoBehaviour
{
	private static readonly Color Purple = new Color(0.62f, 0.3f, 1f, 1f);

	private static readonly Color Core = new Color(0.92f, 0.85f, 1f, 1f);

	private const float FadeTime = 0.35f;

	private const float ImpactInterval = 0.12f;

	private LineRenderer outer;

	private LineRenderer inner;

	private Light endLight;

	private float radius;

	private float impactTimer;

	private float fade = -1f;

	private Vector3 end;

	public static GravitonBeamVisual Create(float radius)
	{
		GameObject gameObject = new GameObject("GravitonBeam");
		GravitonBeamVisual gravitonBeamVisual = gameObject.AddComponent<GravitonBeamVisual>();
		gravitonBeamVisual.radius = radius;
		gravitonBeamVisual.outer = MakeLine(gameObject, "Glow", GravitosAssets.BeamOuterMaterial, Purple, radius * 2f);
		gravitonBeamVisual.inner = MakeLine(gameObject, "Core", GravitosAssets.BeamCoreMaterial, Core, radius * 0.7f);
		GameObject gameObject2 = new GameObject("BeamLight");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gravitonBeamVisual.endLight = gameObject2.AddComponent<Light>();
		gravitonBeamVisual.endLight.type = LightType.Point;
		gravitonBeamVisual.endLight.color = Purple;
		gravitonBeamVisual.endLight.range = radius * 6f;
		gravitonBeamVisual.endLight.intensity = 6f;
		return gravitonBeamVisual;
	}

	public static Transform FindMuzzle(EntityState state)
	{
		CharacterBody characterBody = (state.outer ? state.outer.GetComponent<CharacterBody>() : null);
		Transform transform = (((bool)characterBody && (bool)characterBody.modelLocator) ? characterBody.modelLocator.modelTransform : null);
		if (!transform)
		{
			return null;
		}
		return FindDeep(transform, "hand.r") ?? FindDeep(transform, "MuzzleRight") ?? FindDeep(transform, "MuzzleLeft");
	}

	private static Transform FindDeep(Transform root, string name)
	{
		if (root.name == name)
		{
			return root;
		}
		foreach (Transform item in root)
		{
			Transform transform = FindDeep(item, name);
			if ((bool)transform)
			{
				return transform;
			}
		}
		return null;
	}

	private static LineRenderer MakeLine(GameObject parent, string name, Material material, Color color, float width)
	{
		GameObject obj = new GameObject(name);
		obj.transform.SetParent(parent.transform, worldPositionStays: false);
		LineRenderer lineRenderer = obj.AddComponent<LineRenderer>();
		lineRenderer.useWorldSpace = true;
		lineRenderer.positionCount = 2;
		lineRenderer.numCapVertices = 6;
		lineRenderer.textureMode = LineTextureMode.Stretch;
		lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
		lineRenderer.receiveShadows = false;
		float startWidth = (lineRenderer.endWidth = width);
		lineRenderer.startWidth = startWidth;
		Color startColor = (lineRenderer.endColor = color);
		lineRenderer.startColor = startColor;
		Material material2 = (material ? new Material(material) : new Material(Shader.Find("Sprites/Default")));
		if (material2.HasProperty("_TintColor"))
		{
			material2.SetColor("_TintColor", color);
		}
		if (material2.HasProperty("_Color"))
		{
			material2.SetColor("_Color", color);
		}
		lineRenderer.material = material2;
		return lineRenderer;
	}

	public void SetPoints(Vector3 start, Vector3 newEnd)
	{
		end = newEnd;
		outer.SetPosition(0, start);
		outer.SetPosition(1, end);
		inner.SetPosition(0, start);
		inner.SetPosition(1, end);
		endLight.transform.position = end - (end - start).normalized * 0.5f;
	}

	public void Stop()
	{
		if (fade < 0f)
		{
			fade = 0f;
		}
	}

	private void Update()
	{
		float num = 1f + 0.12f * Mathf.Sin(Time.time * 25f);
		float num2 = 1f;
		if (fade >= 0f)
		{
			fade += Time.deltaTime;
			num2 = Mathf.Clamp01(1f - fade / 0.35f);
			if (num2 <= 0f)
			{
				Object.Destroy(base.gameObject);
				return;
			}
		}
		LineRenderer lineRenderer = outer;
		float startWidth = (outer.endWidth = radius * 2f * num * num2);
		lineRenderer.startWidth = startWidth;
		LineRenderer lineRenderer2 = inner;
		startWidth = (inner.endWidth = radius * 0.7f * num * num2);
		lineRenderer2.startWidth = startWidth;
		endLight.intensity = 6f * num2;
		impactTimer -= Time.deltaTime;
		if (fade < 0f && impactTimer <= 0f && (bool)GravitosAssets.LanceImpactEffect)
		{
			impactTimer = 0.12f;
			EffectManager.SpawnEffect(GravitosAssets.LanceImpactEffect, new EffectData
			{
				origin = end,
				scale = radius
			}, transmit: false);
		}
	}
}

using System;
using UnityEngine;

namespace GravitosNext.Components;

public class ObjectGlide : MonoBehaviour
{
	private Vector3 start;

	private Vector3 end;

	private float duration;

	private float age;

	private float arcHeight;

	private Rigidbody rb;

	private bool restoreKinematic;

	public static void Begin(GameObject target, Vector3 end, float duration)
	{
		if ((bool)target)
		{
			ObjectGlide objectGlide = target.GetComponent<ObjectGlide>();
			if ((bool)objectGlide)
			{
				objectGlide.Restore();
			}
			else
			{
				objectGlide = target.AddComponent<ObjectGlide>();
			}
			objectGlide.start = target.transform.position;
			objectGlide.end = end;
			objectGlide.duration = Mathf.Max(0.05f, duration);
			objectGlide.age = 0f;
			objectGlide.arcHeight = Mathf.Min(4f, Vector3.Distance(objectGlide.start, end) * 0.15f);
			objectGlide.rb = target.GetComponent<Rigidbody>();
			if ((bool)objectGlide.rb)
			{
				objectGlide.restoreKinematic = !objectGlide.rb.isKinematic;
				objectGlide.rb.isKinematic = true;
			}
		}
	}

	private void FixedUpdate()
	{
		age += Time.fixedDeltaTime;
		float num = Mathf.Clamp01(age / duration);
		float t = num * num * (3f - 2f * num);
		Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(num * MathF.PI) * arcHeight;
		if ((bool)rb)
		{
			rb.MovePosition(position);
		}
		else
		{
			base.transform.position = position;
		}
		if (num >= 1f)
		{
			Restore();
			UnityEngine.Object.Destroy(this);
		}
	}

	private void Restore()
	{
		if ((bool)rb && restoreKinematic)
		{
			rb.isKinematic = false;
			rb.velocity = Vector3.zero;
		}
		restoreKinematic = false;
	}
}

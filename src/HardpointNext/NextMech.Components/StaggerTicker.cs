using System;
using UnityEngine;

namespace NextMech.Components;

public class StaggerTicker : MonoBehaviour
{
	private float timer;

	private void FixedUpdate()
	{
		timer += Time.fixedDeltaTime;
		if (!(timer < 0.25f))
		{
			try
			{
				Stagger.Tick(timer);
			}
			catch (Exception)
			{
			}
			timer = 0f;
		}
	}
}

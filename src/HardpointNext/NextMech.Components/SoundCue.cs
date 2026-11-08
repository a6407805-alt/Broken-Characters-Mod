using RoR2;
using UnityEngine;

namespace NextMech.Components;

public class SoundCue : MonoBehaviour
{
	private EffectComponent effect;

	private bool played;

	private void Awake()
	{
		effect = GetComponent<EffectComponent>();
	}

	private void OnEnable()
	{
		played = false;
	}

	private void Update()
	{
		if (!played)
		{
			played = true;
			EffectData effectData = (effect ? effect.effectData : null);
			if (effectData != null)
			{
				CustomAudio.PlayNear((CustomAudio.Clip)effectData.genericUInt, base.transform.position, Mathf.Max(10f, effectData.genericFloat));
			}
		}
	}
}

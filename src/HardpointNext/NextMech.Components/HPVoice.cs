using RoR2;
using UnityEngine;

namespace NextMech.Components;

public class HPVoice : MonoBehaviour
{
	private CharacterBody body;

	private bool deathSaid;

	private void Awake()
	{
		body = GetComponent<CharacterBody>();
	}

	private void Start()
	{
		if ((bool)body)
		{
			CustomAudio.PlayNear(CustomAudio.Clip.Spawn, body.corePosition, 60f);
		}
	}

	private void Update()
	{
		if (!deathSaid && (bool)body && (bool)body.healthComponent && !body.healthComponent.alive)
		{
			deathSaid = true;
			CustomAudio.PlayNear(CustomAudio.Clip.Death, body.corePosition, 80f);
		}
	}
}

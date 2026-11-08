using UnityEngine;

namespace NextMech.Components;

public class SelectVoice : MonoBehaviour
{
	private void OnEnable()
	{
		CustomAudio.Play(CustomAudio.Clip.Select);
	}
}

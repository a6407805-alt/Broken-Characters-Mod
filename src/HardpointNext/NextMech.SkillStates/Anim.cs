using UnityEngine;

namespace NextMech.SkillStates;

internal static class Anim
{
	public const string Full = "FullBody, Override";

	public const string Gesture = "Gesture, Override";

	public const float MaxRate = 1.3f;

	public static void Play(Animator animator, string layer, string clip, float rate = 1f, float fade = 0.08f)
	{
		if ((bool)animator && HPUtil.HasState(animator, layer, clip))
		{
			animator.speed = 1f;
			animator.SetFloat("Skill.playbackRate", Mathf.Min(rate, 1.3f));
			animator.CrossFadeInFixedTime(clip, fade, animator.GetLayerIndex(layer));
		}
	}

	public static void Loop(Animator animator, string clip)
	{
		if ((bool)animator && HPUtil.HasState(animator, "FullBody, Override", clip))
		{
			animator.speed = 1f;
			animator.SetFloat("Skill.playbackRate", 1f);
			animator.CrossFadeInFixedTime(clip, 0.12f, animator.GetLayerIndex("FullBody, Override"));
		}
	}

	public static void Stop(Animator animator, string layer = "FullBody, Override")
	{
		if ((bool)animator)
		{
			int layerIndex = animator.GetLayerIndex(layer);
			if (layerIndex >= 0 && animator.HasState(layerIndex, Animator.StringToHash("BufferEmpty")))
			{
				animator.CrossFadeInFixedTime("BufferEmpty", 0.15f, layerIndex);
			}
		}
	}
}

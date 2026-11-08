using RoR2;
using UnityEngine;

namespace NextMech.Components;

public class DeathAnimation : MonoBehaviour
{
	private const float ClipTime = 2.2f;

	private CharacterModel characterModel;

	private Animator animator;

	private bool seenBody;

	private bool dead;

	private float deadTime;

	private void Awake()
	{
		characterModel = GetComponent<CharacterModel>();
		animator = GetComponent<Animator>();
	}

	private void Update()
	{
		if (dead)
		{
			deadTime += Time.deltaTime;
			if ((bool)animator && deadTime < 2.2f)
			{
				animator.enabled = true;
			}
			return;
		}
		CharacterBody characterBody = (characterModel ? characterModel.body : null);
		if ((bool)characterBody)
		{
			seenBody = true;
		}
		bool num;
		if (!characterBody)
		{
			num = seenBody;
		}
		else
		{
			if (!characterBody.healthComponent)
			{
				return;
			}
			num = !characterBody.healthComponent.alive;
		}
		if (!num)
		{
			return;
		}
		dead = true;
		if ((bool)animator)
		{
			animator.enabled = true;
			animator.speed = 1f;
			for (int i = 1; i < animator.layerCount; i++)
			{
				animator.SetLayerWeight(i, 0f);
			}
			int num2 = Mathf.Max(0, animator.GetLayerIndex("Body"));
			if (animator.HasState(num2, Animator.StringToHash("Death")))
			{
				animator.CrossFadeInFixedTime("Death", 0.15f, num2);
			}
		}
	}
}

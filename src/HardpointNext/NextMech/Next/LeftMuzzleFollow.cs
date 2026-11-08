using UnityEngine;

namespace NextMech;

// Keeps the left-arm muzzle just in front of the left hand, facing where the mech faces.
public class LeftMuzzleFollow : MonoBehaviour
{
	public Transform hand;
	public Transform root;

	private void LateUpdate()
	{
		if (!hand || !root) return;
		float size = Mathf.Clamp(root.lossyScale.y, 0.5f, 2f);
		transform.position = hand.position + root.forward * 0.45f * size;
		transform.rotation = root.rotation;
	}
}

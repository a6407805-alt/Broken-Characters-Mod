using RoR2;
using UnityEngine;

namespace NextMech.SkillStates;

internal static class Move
{
	public static Vector3 Flat(Vector3 v, Vector3 fallback)
	{
		v.y = 0f;
		if (!(v.sqrMagnitude > 0.001f))
		{
			return fallback;
		}
		return v.normalized;
	}

	public static void Face(CharacterDirection dir, Vector3 facing)
	{
		if ((bool)dir && facing.sqrMagnitude > 0.001f)
		{
			dir.forward = facing;
			dir.moveVector = Vector3.zero;
		}
	}
}

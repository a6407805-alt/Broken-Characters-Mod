using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace NextMech;

// Puts the Blender weapon models on the mech: arm guns in the hands, cannons on the shoulders, all pointing where
// you aim. Hides the built-in rifle / blade / missile pod when a different weapon replaces them, and moves the
// muzzle points (MuzzleRight/Left, MuzzleBackR/L) to each weapon's barrel tip so flashes and tracers come out of
// the gun. Visual only; runs on every machine (the loadout is networked).
public class GunMount : MonoBehaviour
{
	private class Slot
	{
		public string id;
		public GameObject go;
		public WeaponModels.Model model;
	}

	private CharacterBody body;
	private Transform model;
	private ChildLocator cl;
	private CharacterModel cm;
	private readonly Slot[] slots = { new Slot(), new Slot(), new Slot(), new Slot() };
	private readonly Dictionary<Transform, Vector3> baseScale = new Dictionary<Transform, Vector3>();

	private static readonly string[] MuzzleNames = { "MuzzleRight", "MuzzleLeft", "MuzzleBackR", "MuzzleBackL" };
	private static readonly string[] AnchorNames = { "HandR", "HandL", "PodR", "PodL" };

	private void Awake() => body = GetComponent<CharacterBody>();

	private string WeaponId(int mount)
	{
		SkillLocator sl = body.skillLocator;
		GenericSkill gs = mount switch
		{
			0 => sl ? sl.primary : null,
			1 => sl ? sl.secondary : null,
			2 => sl ? sl.special : null,
			_ => NextPilot.BackLeftSlot(body),
		};
		return gs ? Arsenal.Of(gs.skillDef)?.id : null;
	}

	private void LateUpdate()
	{
		if (!body) return;
		Transform m = body.modelLocator ? body.modelLocator.modelTransform : null;
		if (m != model)
		{
			model = m;
			cl = m ? m.GetComponent<ChildLocator>() : null;
			cm = m ? m.GetComponent<CharacterModel>() : null;
			foreach (Slot s in slots) { if (s.go) Destroy(s.go); s.go = null; s.id = null; }
			baseScale.Clear();
		}
		if (!model || !cl) return;
		bool hidden = cm && cm.invisibilityCount > 0;

		Vector3 aim = body.inputBank ? body.inputBank.aimDirection : Vector3.zero;
		if (aim.sqrMagnitude < 0.01f) aim = model.forward;
		aim.Normalize();
		float scale = NextConfig.GunScale.Value * Mathf.Max(0.2f, HPConfig.ModelScale.Value);

		for (int i = 0; i < 4; i++)
		{
			string id = WeaponId(i);
			Slot s = slots[i];
			// The right arm's Burst Rifle and the shoulder missiles are already part of the mech.
			bool builtIn = (i == 0 && id == "BURSTRIFLE") || (i >= 2 && (id == "MISSILES" || id == "ASSAULTARMOR"));
			string want = builtIn ? null : id;
			if (want != s.id)
			{
				if (s.go) Destroy(s.go);
				s.go = null;
				s.id = want;
				s.model = want != null ? WeaponModels.Get(want) : null;
				if (s.model != null) s.go = Build(s.model);
			}
			Transform anchor = cl.FindChild(AnchorNames[i]);
			Transform muzzle = cl.FindChild(MuzzleNames[i]);
			if (s.go && anchor)
			{
				s.go.SetActive(!hidden);
				Transform t = s.go.transform;
				Vector3 pos = anchor.position;
				if (i >= 2) pos += Vector3.up * 0.25f * scale;
				t.SetPositionAndRotation(pos, Quaternion.LookRotation(aim, Vector3.up));
				Vector3 ls = model.lossyScale;
				t.localScale = new Vector3(scale / Mathf.Max(0.001f, ls.x), scale / Mathf.Max(0.001f, ls.y), scale / Mathf.Max(0.001f, ls.z));
				if (muzzle) muzzle.SetPositionAndRotation(t.TransformPoint(s.model.Muzzle), t.rotation);
			}
			else if (muzzle)
			{
				// built-in weapon (or no model): fire from the rifle's muzzle / the pod / the hand
				Transform src = i == 0 ? cl.FindChild("MuzzleRifle") : anchor;
				if (src) muzzle.SetPositionAndRotation(src.position, Quaternion.LookRotation(aim, Vector3.up));
			}
		}

		// Hide the built-in parts that a model replaced (scaling a bone to 0 collapses the mesh skinned to it).
		Hide("Rifle", slots[0].go != null);
		Hide("Blade", true);                     // no blade weapon in the arm list: the left hand holds a gun
		Hide("PodR", slots[2].go != null);
		Hide("PodL", slots[3].go != null);
	}

	private void Hide(string child, bool hide)
	{
		Transform t = cl.FindChild(child);
		if (!t) return;
		if (!baseScale.TryGetValue(t, out Vector3 s))
		{
			s = t.localScale;
			if (s.sqrMagnitude < 1e-6f) s = Vector3.one;
			baseScale[t] = s;
		}
		t.localScale = hide ? Vector3.zero : s;
	}

	private GameObject Build(WeaponModels.Model m)
	{
		Material mat = WeaponModels.Material();
		if (!mat || !m.Mesh) return null;
		GameObject go = new GameObject("NextWeapon_" + m.Name) { layer = model.gameObject.layer };
		go.transform.SetParent(model, false);
		go.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
		MeshRenderer mr = go.AddComponent<MeshRenderer>();
		mr.sharedMaterial = mat;
		mr.shadowCastingMode = ShadowCastingMode.On;
		return go;
	}

	private void OnDestroy()
	{
		foreach (Slot s in slots) if (s.go) Destroy(s.go);
	}

}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace NextMech;

// Loads next_weapons.bin.gz (made by Blender/build_weapons.py): one mesh per weapon with its muzzle point.
public static class WeaponModels
{
	public class Model
	{
		public string Name;
		public Mesh Mesh;
		public Vector3 Muzzle;
	}

	public const string FileName = "next_weapons.bin.gz";

	private static readonly Dictionary<string, Model> models = new Dictionary<string, Model>();
	private static string directory;
	private static bool loaded;
	private static Color[] palColor = new Color[0];
	private static float[] palEmit = new float[0];
	private static Material material;

	public static void Init(string pluginDirectory)
	{
		directory = pluginDirectory;
	}

	public static Model Get(string id)
	{
		Load();
		return id != null && models.TryGetValue(id, out Model m) ? m : null;
	}

	private static void Load()
	{
		if (loaded) return;
		loaded = true;
		string path = System.IO.Path.Combine(directory ?? "", FileName);
		if (!File.Exists(path))
		{
			HPPlugin.Log.LogWarning("No " + FileName + " next to the dll: weapons have no models.");
			return;
		}
		try
		{
			using (FileStream fs = File.OpenRead(path))
			using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
			using (BinaryReader br = new BinaryReader(new BufferedStream(gz, 1 << 16)))
			{
				if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "NXGW") throw new Exception("not a weapon file");
				br.ReadInt32();
				int np = br.ReadInt32();
				palColor = new Color[np];
				palEmit = new float[np];
				for (int i = 0; i < np; i++)
				{
					palColor[i] = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), 1f);
					palEmit[i] = br.ReadSingle();
				}
				int nw = br.ReadInt32();
				for (int w = 0; w < nw; w++)
				{
					string name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
					Vector3 muzzle = V3(br);
					int n = br.ReadInt32();
					var v = new Vector3[n];
					var nr = new Vector3[n];
					var uv = new Vector2[n];
					var tris = new int[n];
					for (int i = 0; i < n; i++)
					{
						v[i] = V3(br);
						nr[i] = V3(br);
						int pal = br.ReadInt32();
						uv[i] = new Vector2((pal + 0.5f) / Mathf.Max(1, np), 0.5f);
						tris[i] = i;
					}
					var mesh = new Mesh { name = "NextWeapon_" + name };
					if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
					mesh.vertices = v;
					mesh.normals = nr;
					mesh.uv = uv;
					mesh.triangles = tris;
					mesh.RecalculateBounds();
					models[name] = new Model { Name = name, Mesh = mesh, Muzzle = muzzle };
				}
			}
			HPPlugin.Log.LogInfo($"Loaded {models.Count} weapon models.");
		}
		catch (Exception e)
		{
			HPPlugin.Log.LogError("Couldn't read " + path + ": " + e);
		}
	}

	private static Vector3 V3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

	// Hopoo's shader painted from the palette (colour + glow), cel-shaded like the mech.
	public static Material Material()
	{
		if (material) return material;
		Shader sh = CustomModel.GameShader;
		if (!sh)
		{
			try { sh = Shader.Find("Hopoo Games/Deferred/Standard"); } catch { sh = null; }
		}
		if (!sh) return null;
		int n = Mathf.Max(1, palColor.Length);
		var tex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "NextWeaponPalette" };
		var em = new Texture2D(n, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "NextWeaponGlow" };
		for (int i = 0; i < palColor.Length; i++)
		{
			tex.SetPixel(i, 0, palColor[i]);
			em.SetPixel(i, 0, palEmit[i] > 0f ? palColor[i] * Mathf.Min(1f, palEmit[i]) : Color.black);
		}
		tex.Apply(false, true);
		em.Apply(false, true);
		material = new Material(sh) { name = "NextWeaponMat" };
		Set(material, "_MainTex", tex);
		Set(material, "_EmTex", em);
		SetC(material, "_Color", Color.white);
		SetC(material, "_EmColor", Color.white);
		SetF(material, "_EmPower", 1.5f);
		SetF(material, "_RampInfo", 0f);
		SetF(material, "_NormalStrength", 0f);
		SetF(material, "_SpecularStrength", 0.15f);
		SetF(material, "_SpecularExponent", 6f);
		SetF(material, "_DitherOn", 0f);
		SetF(material, "_Cull", (float)CullMode.Off);
		return material;
	}

	private static void Set(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }
	private static void SetC(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
	private static void SetF(Material m, string p, float f) { if (m.HasProperty(p)) m.SetFloat(p, f); }
}

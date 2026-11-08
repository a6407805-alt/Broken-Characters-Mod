using System;
using UnityEngine;

namespace GravitosNext;

internal static class IconFactory
{
	private const int Size = 128;

	private static readonly Color Background = new Color(0.06f, 0.05f, 0.12f);

	private static readonly Color Violet = new Color(0.62f, 0.38f, 1f);

	private static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);

	private static readonly Color White = new Color(0.95f, 0.95f, 1f);

	public static Sprite Passive()
	{
		return ToSprite(Draw((float x, float y, float r) => Ring(r, 0.62f, 0.07f) + 0.25f * Mathf.Clamp01(1f - r / 0.62f) + Dot(x, y + 0.05f, 0.14f), Cyan));
	}

	public static Sprite Primary()
	{
		return ToSprite(Draw((float x, float y, float r) => Dot(x, y - 0.42f, 0.16f) + Triangle(x, y + 0.05f, 0.32f) + Bar(y, 0.62f, 0.05f) * Mathf.Clamp01(1f - Mathf.Abs(x) / 0.6f), Violet));
	}

	public static Sprite Secondary()
	{
		return ToSprite(Draw(delegate(float x, float y, float r)
		{
			float num = Mathf.Abs(y - (x * 0.9f + 0.12f * Mathf.Sin(x * 22f)));
			return Mathf.Clamp01(1f - num / 0.07f) * Mathf.Clamp01(1f - r / 0.85f) + 0.5f * Dot(x - 0.55f, y - 0.5f, 0.18f);
		}, Cyan));
	}

	public static Sprite IonCannon()
	{
		return ToSprite(Draw(delegate(float x, float y, float r)
		{
			float num = 0f;
			float[] array = new float[4] { -0.45f, -0.15f, 0.15f, 0.45f };
			foreach (float num2 in array)
			{
				num += Mathf.Clamp01(1f - Mathf.Abs(x - num2) / 0.06f);
			}
			return Mathf.Clamp01(num) * Mathf.Clamp01((0.6f - y) / 0.05f) * Mathf.Clamp01(1f - r / 0.95f) + 0.6f * Bar(y, 0.62f, 0.05f) * Mathf.Clamp01(1f - Mathf.Abs(x) / 0.7f);
		}, Violet));
	}

	public static Sprite Utility()
	{
		return ToSprite(Draw((float x, float y, float r) => Chevron(x + 0.35f, y) + Chevron(x - 0.05f, y) * 0.75f + Chevron(x - 0.45f, y) * 0.5f, Violet));
	}

	public static Sprite Special()
	{
		return ToSprite(Draw(Spiral, Violet, blackCore: true));
	}

	public static Texture2D PortraitTexture()
	{
		return Draw(Spiral, Cyan, blackCore: true);
	}

	private static float Spiral(float x, float y, float r)
	{
		float num = Mathf.Atan2(y, x);
		return Mathf.Pow(Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(num * 2f + r * 14f)), 3f) * Mathf.Clamp01(1f - r / 0.9f) * 1.6f;
	}

	private static float Ring(float r, float radius, float width)
	{
		return Mathf.Clamp01(1f - Mathf.Abs(r - radius) / width);
	}

	private static float Dot(float x, float y, float radius)
	{
		return Mathf.Clamp01((radius - Mathf.Sqrt(x * x + y * y)) / 0.03f);
	}

	private static float Bar(float y, float at, float width)
	{
		return Mathf.Clamp01(1f - Mathf.Abs(y - at) / width);
	}

	private static float Triangle(float x, float y, float size)
	{
		float num = (y + size) / (2f * size);
		if (num < 0f || num > 1f)
		{
			return 0f;
		}
		return Mathf.Clamp01(((1f - num) * size - Mathf.Abs(x)) / 0.03f);
	}

	private static float Chevron(float x, float y)
	{
		float num = Mathf.Abs(x - Mathf.Abs(y) * 0.8f);
		return Mathf.Clamp01(1f - num / 0.07f) * Mathf.Clamp01(1f - Mathf.Abs(y) / 0.5f);
	}

	private static Texture2D Draw(Func<float, float, float, float> glyph, Color tint, bool blackCore = false)
	{
		Texture2D texture2D = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		Color[] array = new Color[16384];
		for (int i = 0; i < 128; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				float num = ((float)j + 0.5f) / 128f * 2f - 1f;
				float num2 = ((float)i + 0.5f) / 128f * 2f - 1f;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				Color a = Color.Lerp(Background, Background * 1.8f, Mathf.Clamp01(1f - num3));
				a = Color.Lerp(a, tint, Mathf.Clamp01(glyph(num, 0f - num2, num3)));
				a = Color.Lerp(a, White, Ring(num3, 0.93f, 0.035f) * 0.8f);
				if (blackCore)
				{
					a = Color.Lerp(a, Color.black, Mathf.Clamp01((0.2f - num3) / 0.04f));
				}
				float num4 = Mathf.Max(Mathf.Abs(num), Mathf.Abs(num2));
				a.a = Mathf.Clamp01((0.995f - num4) / 0.01f);
				array[i * 128 + j] = a;
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return texture2D;
	}

	private static Sprite ToSprite(Texture2D tex)
	{
		return Sprite.Create(tex, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
	}
}

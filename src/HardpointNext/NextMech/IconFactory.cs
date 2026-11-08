using UnityEngine;

namespace NextMech;

internal static class IconFactory
{
	private const int Size = 128;

	public static Texture2D FallbackTexture(Color color)
	{
		Texture2D texture2D = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false);
		Color[] array = new Color[16384];
		for (int i = 0; i < 128; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				float num = ((float)j + 0.5f) / 128f * 2f - 1f;
				float num2 = ((float)i + 0.5f) / 128f * 2f - 1f;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				Color color2 = ((num3 < 0.6f) ? color : new Color(0.12f, 0.05f, 0.03f));
				color2.a = ((num3 < 0.98f) ? 1f : 0f);
				array[i * 128 + j] = color2;
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return texture2D;
	}

	public static Sprite Fallback(Color color)
	{
		return Sprite.Create(FallbackTexture(color), new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
	}

	public static Sprite SkinIcon(Color a, Color b)
	{
		Texture2D texture2D = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false);
		Color[] array = new Color[16384];
		for (int i = 0; i < 128; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				array[i * 128 + j] = ((j + i < 128) ? a : b);
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
	}
}

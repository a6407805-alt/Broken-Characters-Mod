using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RoR2;
using UnityEngine;

namespace NextMech;

internal static class CustomAudio
{
	public enum Clip
	{
		Select,
		Spawn,
		Death,
		Redline,
		Overload,
		MissilesVoice,
		ArmorVoice,
		LowAP,
		Rifle,
		BladeOn,
		BladeSwing,
		Missile,
		MissileHit,
		Boost,
		AssaultBoost,
		Kick,
		ArmorCharge,
		ArmorBlast,
		Lock,
		Reload,
		Stomp
	}

	private class Sound
	{
		public string file;

		public int voices;

		public float minInterval;

		public bool voice;

		public readonly List<string> aliases = new List<string>();

		public int next;

		public float lastPlayed = -999f;
	}

	private const float VoiceGap = 0.9f;

	private static float lastVoice = -999f;

	private static readonly Dictionary<Clip, Sound> sounds = new Dictionary<Clip, Sound>
	{
		{
			Clip.Select,
			new Sound
			{
				file = "hp_select",
				voices = 1,
				minInterval = 2f,
				voice = true
			}
		},
		{
			Clip.Spawn,
			new Sound
			{
				file = "hp_spawn",
				voices = 1,
				minInterval = 5f,
				voice = true
			}
		},
		{
			Clip.Death,
			new Sound
			{
				file = "hp_death",
				voices = 1,
				minInterval = 2f,
				voice = true
			}
		},
		{
			Clip.Redline,
			new Sound
			{
				file = "hp_redline",
				voices = 1,
				minInterval = 4f,
				voice = true
			}
		},
		{
			Clip.Overload,
			new Sound
			{
				file = "hp_overload",
				voices = 1,
				minInterval = 3f,
				voice = true
			}
		},
		{
			Clip.MissilesVoice,
			new Sound
			{
				file = "hp_missiles",
				voices = 1,
				minInterval = 8f,
				voice = true
			}
		},
		{
			Clip.ArmorVoice,
			new Sound
			{
				file = "hp_armor",
				voices = 1,
				minInterval = 4f,
				voice = true
			}
		},
		{
			Clip.LowAP,
			new Sound
			{
				file = "hp_lowap",
				voices = 1,
				minInterval = 20f,
				voice = true
			}
		},
		{
			Clip.Rifle,
			new Sound
			{
				file = "rifle",
				voices = 6,
				minInterval = 0.03f
			}
		},
		{
			Clip.BladeOn,
			new Sound
			{
				file = "blade_on",
				voices = 2,
				minInterval = 0.2f
			}
		},
		{
			Clip.BladeSwing,
			new Sound
			{
				file = "blade_swing",
				voices = 3,
				minInterval = 0.1f
			}
		},
		{
			Clip.Missile,
			new Sound
			{
				file = "missile",
				voices = 6,
				minInterval = 0.04f
			}
		},
		{
			Clip.MissileHit,
			new Sound
			{
				file = "missile_hit",
				voices = 6,
				minInterval = 0.05f
			}
		},
		{
			Clip.Boost,
			new Sound
			{
				file = "boost",
				voices = 3,
				minInterval = 0.1f
			}
		},
		{
			Clip.AssaultBoost,
			new Sound
			{
				file = "assault_boost",
				voices = 1,
				minInterval = 0.5f
			}
		},
		{
			Clip.Kick,
			new Sound
			{
				file = "kick",
				voices = 2,
				minInterval = 0.2f
			}
		},
		{
			Clip.ArmorCharge,
			new Sound
			{
				file = "armor_charge",
				voices = 1,
				minInterval = 1f
			}
		},
		{
			Clip.ArmorBlast,
			new Sound
			{
				file = "armor_blast",
				voices = 1,
				minInterval = 0.5f
			}
		},
		{
			Clip.Lock,
			new Sound
			{
				file = "lock",
				voices = 3,
				minInterval = 0.05f
			}
		},
		{
			Clip.Reload,
			new Sound
			{
				file = "reload",
				voices = 1,
				minInterval = 0.5f
			}
		},
		{
			Clip.Stomp,
			new Sound
			{
				file = "stomp",
				voices = 3,
				minInterval = 0.15f
			}
		}
	};

	private static bool enabled;

	[DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
	private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

	public static void Init(string pluginDirectory)
	{
		if (!HPConfig.CustomSounds.Value)
		{
			return;
		}
		if (Application.platform != RuntimePlatform.WindowsPlayer)
		{
			HPPlugin.Log.LogInfo("Custom sounds need Windows; Hardpoint is silent.");
			return;
		}
		string text = System.IO.Path.Combine(pluginDirectory, "Sounds");
		string text2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HardpointNextSounds");
		try
		{
			Directory.CreateDirectory(text2);
			int num = 0;
			foreach (KeyValuePair<Clip, Sound> sound in sounds)
			{
				Sound value = sound.Value;
				string text3 = System.IO.Path.Combine(text, value.file + ".wav");
				if (!File.Exists(text3))
				{
					HPPlugin.Log.LogWarning("Missing sound file " + text3);
					continue;
				}
				byte[] array = WithVolume(File.ReadAllBytes(text3), HPConfig.Volume.Value / 100f * 0.7f);
				for (int i = 0; i < value.voices; i++)
				{
					string text4 = $"next_{sound.Key}_{i}".ToLowerInvariant();
					string text5 = System.IO.Path.Combine(text2, text4 + ".wav");
					File.WriteAllBytes(text5, array);
					int num2 = mciSendString("open \"" + text5 + "\" type waveaudio alias " + text4, null, 0, IntPtr.Zero);
					if (num2 == 0)
					{
						value.aliases.Add(text4);
					}
					else
					{
						HPPlugin.Log.LogWarning($"Could not open {text5} (MCI error {num2}).");
					}
				}
				if (value.aliases.Count > 0)
				{
					num++;
				}
			}
			enabled = num > 0;
			HPPlugin.Log.LogInfo($"Custom sounds: {num}/{sounds.Count} ready.");
			Application.quitting += delegate
			{
				mciSendString("close all", null, 0, IntPtr.Zero);
			};
		}
		catch (Exception ex)
		{
			HPPlugin.Log.LogWarning("Custom sounds disabled: " + ex.Message);
			enabled = false;
		}
	}

	public static bool Has(Clip clip)
	{
		if (enabled && sounds[clip].aliases.Count > 0)
		{
			if (sounds[clip].voice)
			{
				return HPConfig.VoiceLines.Value;
			}
			return true;
		}
		return false;
	}

	public static bool Play(Clip clip)
	{
		if (!Has(clip))
		{
			return false;
		}
		Sound sound = sounds[clip];
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime - sound.lastPlayed < sound.minInterval)
		{
			return true;
		}
		if (sound.voice)
		{
			if (unscaledTime - lastVoice < 0.9f)
			{
				return true;
			}
			lastVoice = unscaledTime;
		}
		sound.lastPlayed = unscaledTime;
		string text = sound.aliases[sound.next];
		sound.next = (sound.next + 1) % sound.aliases.Count;
		mciSendString("play " + text + " from 0", null, 0, IntPtr.Zero);
		return true;
	}

	public static bool PlayNear(Clip clip, Vector3 position, float range)
	{
		if (!Has(clip))
		{
			return false;
		}
		CharacterBody characterBody = LocalUserManager.GetFirstLocalUser()?.cachedBody;
		if ((bool)characterBody && (characterBody.corePosition - position).sqrMagnitude > range * range)
		{
			return true;
		}
		return Play(clip);
	}

	public static void Stop(Clip clip)
	{
		if (!Has(clip))
		{
			return;
		}
		Sound sound = sounds[clip];
		foreach (string alias in sound.aliases)
		{
			mciSendString("stop " + alias, null, 0, IntPtr.Zero);
		}
		sound.lastPlayed = -999f;
	}

	private static byte[] WithVolume(byte[] wav, float volume)
	{
		float num = Mathf.Clamp01(volume);
		num *= num;
		if (wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF")
		{
			return wav;
		}
		byte[] array = (byte[])wav.Clone();
		int num2 = 0;
		int num3 = 0;
		int num4 = 12;
		while (num4 + 8 <= array.Length)
		{
			string @string = Encoding.ASCII.GetString(array, num4, 4);
			int num5 = BitConverter.ToInt32(array, num4 + 4);
			int num6 = num4 + 8;
			if (@string == "fmt ")
			{
				num3 = BitConverter.ToInt16(array, num6);
				num2 = BitConverter.ToInt16(array, num6 + 14);
			}
			else if (@string == "data" && num3 == 1 && num2 == 16)
			{
				int num7 = Math.Min(array.Length, num6 + num5);
				for (int i = num6; i + 1 < num7; i += 2)
				{
					int num8 = (short)(array[i] | (array[i + 1] << 8));
					num8 = Mathf.Clamp(Mathf.RoundToInt((float)num8 * num), -32768, 32767);
					array[i] = (byte)((uint)num8 & 0xFFu);
					array[i + 1] = (byte)((uint)(num8 >> 8) & 0xFFu);
				}
				return array;
			}
			num4 = num6 + num5 + (num5 & 1);
		}
		return wav;
	}
}

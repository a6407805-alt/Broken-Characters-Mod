using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RoR2;
using UnityEngine;
using Path = System.IO.Path;

namespace SpecialForces
{
    // The mod's sounds (Sounds/*.wav next to the DLL, made by Sounds/make_sounds.py; the radio voice is Kokoro TTS
    // through a radio filter). Played through Windows' winmm MCI because RoR2 turns Unity audio off (the survivors'
    // CustomAudio approach). Windows only, not positional; volumes are in the config and baked into temp copies.
    internal static class SFAudio
    {
        public enum Clip
        {
            Radio, RadioAlt, Thunder1, Thunder2, Thunder3, Rain, StormWind, BossRoar, BossGrowl, BossLightning, BossSlam,
            BossCyclone, LanderEngine, LanderLand, TowerAlarm, Build, NestDeath,
        }

        private class Sound
        {
            public string file;
            public int voices = 1;
            public float minInterval = 0.2f;
            public Func<float> volume;
            public readonly List<string> aliases = new List<string>();
            public int next;
            public float lastPlayed = -999f;
        }

        private static readonly Dictionary<Clip, Sound> sounds = new Dictionary<Clip, Sound>();
        private static bool enabled;

        [DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        private static void Def(Clip clip, string file, int voices, float interval, Func<float> volume) =>
            sounds[clip] = new Sound { file = file, voices = voices, minInterval = interval, volume = volume };

        public static void Init(string pluginDirectory)
        {
            Func<float> voice = () => SFConfig.VoiceVolume.Value, storm = () => SFConfig.StormVolume.Value, boss = () => SFConfig.BossVolume.Value;
            Func<float> base_ = () => 70f;
            Def(Clip.Radio, "radio", 1, 2f, voice);
            Def(Clip.RadioAlt, "radio_alt", 1, 2f, voice);
            Def(Clip.Thunder1, "thunder1", 2, 0.5f, storm);
            Def(Clip.Thunder2, "thunder2", 2, 0.5f, storm);
            Def(Clip.Thunder3, "thunder3", 2, 0.5f, storm);
            Def(Clip.Rain, "rain", 2, 0.5f, () => SFConfig.StormVolume.Value * 0.8f);
            Def(Clip.StormWind, "storm_wind", 2, 2f, storm);
            Def(Clip.BossRoar, "boss_roar", 2, 1.5f, boss);
            Def(Clip.BossGrowl, "boss_growl", 2, 1f, boss);
            Def(Clip.BossLightning, "boss_lightning", 4, 0.1f, boss);
            Def(Clip.BossSlam, "boss_slam", 2, 0.5f, boss);
            Def(Clip.BossCyclone, "boss_cyclone", 2, 1f, boss);
            Def(Clip.LanderEngine, "lander_engine", 1, 2f, base_);
            Def(Clip.LanderLand, "lander_land", 1, 2f, base_);
            Def(Clip.TowerAlarm, "tower_alarm", 1, 6f, base_);
            Def(Clip.Build, "build", 3, 0.1f, base_);
            Def(Clip.NestDeath, "nest_death", 2, 0.5f, base_);

            if (!SFConfig.CustomSounds.Value) return;
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                SpecialForcesPlugin.Log.LogInfo("Custom sounds need Windows; they are off.");
                return;
            }
            string soundDir = Path.Combine(pluginDirectory, "Sounds");
            string tempDir = Path.Combine(Path.GetTempPath(), "SpecialForcesSounds");
            try
            {
                Directory.CreateDirectory(tempDir);
                int loaded = 0;
                foreach (var pair in sounds)
                {
                    Sound sound = pair.Value;
                    string source = Path.Combine(soundDir, sound.file + ".wav");
                    if (!File.Exists(source)) continue;
                    byte[] data = WithVolume(File.ReadAllBytes(source), sound.volume() / 100f * SFConfig.MasterVolume.Value / 100f);
                    for (int v = 0; v < sound.voices; v++)
                    {
                        string alias = $"sf_{pair.Key}_{v}".ToLowerInvariant();
                        string copy = Path.Combine(tempDir, alias + ".wav");
                        File.WriteAllBytes(copy, data);
                        int error = mciSendString($"open \"{copy}\" type waveaudio alias {alias}", null, 0, IntPtr.Zero);
                        if (error == 0) sound.aliases.Add(alias);
                        else SpecialForcesPlugin.Log.LogWarning($"Could not open {copy} (MCI error {error}).");
                    }
                    if (sound.aliases.Count > 0) loaded++;
                }
                enabled = loaded > 0;
                SpecialForcesPlugin.Log.LogInfo($"Custom sounds: {loaded}/{sounds.Count} ready.");
                Application.quitting += () => mciSendString("close all", null, 0, IntPtr.Zero);
            }
            catch (Exception e)
            {
                SpecialForcesPlugin.Log.LogWarning("Custom sounds disabled: " + e.Message);
                enabled = false;
            }
        }

        public static bool Has(Clip clip) => enabled && sounds[clip].aliases.Count > 0;

        public static bool Play(Clip clip)
        {
            if (!Has(clip)) return false;
            Sound sound = sounds[clip];
            float now = Time.unscaledTime;
            if (now - sound.lastPlayed < sound.minInterval) return true;
            sound.lastPlayed = now;
            string alias = sound.aliases[sound.next];
            sound.next = (sound.next + 1) % sound.aliases.Count;
            mciSendString($"play {alias} from 0", null, 0, IntPtr.Zero);
            return true;
        }

        public static void Stop(Clip clip)
        {
            if (!Has(clip)) return;
            foreach (string alias in sounds[clip].aliases) mciSendString($"stop {alias}", null, 0, IntPtr.Zero);
        }

        // Plays only for players within range of where it happens (MCI sounds aren't positional).
        public static bool PlayNear(Clip clip, Vector3 position, float range)
        {
            if (!Has(clip)) return false;
            CharacterBody listener = LocalUserManager.GetFirstLocalUser()?.cachedBody;
            Vector3 ear = listener ? listener.corePosition : (Camera.main ? Camera.main.transform.position : position);
            if ((ear - position).sqrMagnitude > range * range) return true;
            return Play(clip);
        }

        // ---- the rain loop: two copies of an 11 s clip, restarted just before each one ends.
        private static bool raining;
        private static float nextRain, nextWind;

        public static void SetRain(bool on)
        {
            if (raining == on) return;
            raining = on;
            nextRain = 0f;
            nextWind = Time.unscaledTime + 8f;
            if (!on)
            {
                Stop(Clip.Rain);
                Stop(Clip.StormWind);
            }
        }

        public static void Tick()
        {
            if (!raining || !enabled) return;
            float now = Time.unscaledTime;
            if (now >= nextRain)
            {
                Play(Clip.Rain);
                nextRain = now + 10.6f;
            }
            if (now >= nextWind)
            {
                Play(Clip.StormWind);
                nextWind = now + UnityEngine.Random.Range(12f, 25f);
            }
        }

        private static byte[] WithVolume(byte[] wav, float volume)
        {
            float gain = Mathf.Clamp01(volume);
            gain *= gain;
            if (wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF") return wav;
            byte[] result = (byte[])wav.Clone();
            int bits = 0, format = 0;
            int pos = 12;
            while (pos + 8 <= result.Length)
            {
                string id = Encoding.ASCII.GetString(result, pos, 4);
                int size = BitConverter.ToInt32(result, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    format = BitConverter.ToInt16(result, body);
                    bits = BitConverter.ToInt16(result, body + 14);
                }
                else if (id == "data" && format == 1 && bits == 16)
                {
                    int end = Math.Min(result.Length, body + size);
                    for (int i = body; i + 1 < end; i += 2)
                    {
                        int sample = (short)(result[i] | (result[i + 1] << 8));
                        sample = Mathf.Clamp(Mathf.RoundToInt(sample * gain), short.MinValue, short.MaxValue);
                        result[i] = (byte)(sample & 0xFF);
                        result[i + 1] = (byte)((sample >> 8) & 0xFF);
                    }
                    return result;
                }
                pos = body + size + (size & 1);
            }
            return wav;
        }
    }
}

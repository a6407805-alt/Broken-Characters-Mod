using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Jukebox
{
    /// <summary>
    /// Plays one song at a time through Windows winmm (Unity audio is off in RoR2). "mpegvideo" goes through
    /// DirectShow, so it plays .mp3 straight from Suno (no converting) and .wav, and it supports a real volume.
    /// Windows only; ignores the game's volume sliders (the mod has its own volume setting).
    /// </summary>
    public static class MciPlayer
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int mciSendString(string command, StringBuilder ret, int len, IntPtr hwnd);

        private const string Alias = "bcmjukebox";
        private static bool _open, _broken;
        private static readonly StringBuilder Ret = new StringBuilder(256);

        public static bool IsOpen => _open;
        public static int LengthMs { get; private set; }

        private static int Send(string cmd)
        {
            if (_broken) return -1;
            try { Ret.Clear(); return mciSendString(cmd, Ret, Ret.Capacity, IntPtr.Zero); }
            catch (Exception e) { _broken = true; JukeboxPlugin.Log?.LogWarning("Jukebox disabled (winmm unavailable): " + e.Message); return -1; }
        }

        private static string Query(string cmd) => Send(cmd) == 0 ? Ret.ToString().Trim() : "";

        public static bool Play(string path, int volume)
        {
            Stop();
            if (!System.IO.File.Exists(path)) return false;
            // Suno MP3s carry cover art, which Windows' player sometimes treats as a video stream and then fails to
            // open (MCI error 277). So play a copy without the tag first, then the original, each twice.
            string clean = CleanCopy(path);
            int err = -1;
            foreach (string file in clean != null ? new[] { clean, clean, path } : new[] { path, path })
            {
                err = Send("open \"" + file + "\" type mpegvideo alias " + Alias);
                if (err == 0) break;
                Send("close " + Alias);
            }
            if (err != 0) { JukeboxPlugin.Log?.LogWarning("Jukebox couldn't open " + path + " (MCI error " + err + ")"); return false; }
            _open = true;
            Send("set " + Alias + " time format milliseconds");
            int.TryParse(Query("status " + Alias + " length"), out int len);
            LengthMs = len;
            SetVolume(volume);
            return Send("play " + Alias) == 0;
        }

        /// <summary>
        /// Copy of the MP3 in %TEMP%\BCMJukebox without the ID3v2 tag (title, cover art) at the start and the ID3v1
        /// tag at the end: just the audio frames. Reused while the original doesn't change. Null if there's no tag.
        /// </summary>
        private static string CleanCopy(string path)
        {
            try
            {
                if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return null;
                byte[] d = System.IO.File.ReadAllBytes(path);
                int start = 0, end = d.Length;
                while (start + 10 <= end && d[start] == 'I' && d[start + 1] == 'D' && d[start + 2] == '3')
                {
                    int size = (d[start + 6] & 0x7F) << 21 | (d[start + 7] & 0x7F) << 14 | (d[start + 8] & 0x7F) << 7 | (d[start + 9] & 0x7F);
                    start += 10 + size + ((d[start + 5] & 0x10) != 0 ? 10 : 0);
                }
                if (end - 128 > start && d[end - 128] == 'T' && d[end - 127] == 'A' && d[end - 126] == 'G') end -= 128;
                if (start == 0 && end == d.Length) return null;
                if (start >= end) return null;
                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BCMJukebox");
                System.IO.Directory.CreateDirectory(dir);
                string dest = System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(path) + "_" + d.Length + ".mp3");
                if (!System.IO.File.Exists(dest))
                {
                    using (var f = System.IO.File.Create(dest)) f.Write(d, start, end - start);
                }
                return dest;
            }
            catch (Exception e)
            {
                JukeboxPlugin.Log?.LogWarning("Jukebox couldn't make a clean copy of " + path + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 0-150 volume scale with a boosted master gain ceiling.
        /// DirectShow MCI's 0-1000 scale is already logarithmic, so squaring it made mid volumes way too quiet.
        /// Here 1-100 maps with a high-punch root curve up to 1000 (0 dB full scale), and 100-150 ("Garage-Rock Boost")
        /// holds MCI at 1000 ceiling while GameMusic ducks heavy combat SFX so the track cuts right through gunfire.
        /// </summary>
        public static void SetVolume(int volume)
        {
            if (!_open) return;
            int clamped = Math.Max(0, Math.Min(150, volume));
            if (clamped == 0)
            {
                Send("setaudio " + Alias + " volume to 0");
                return;
            }
            float norm = Math.Min(100, clamped) / 100f;
            int mciVol = (int)Math.Round(Math.Pow(norm, 0.48) * 1000.0);
            if (clamped >= 95) mciVol = 1000;
            Send("setaudio " + Alias + " volume to " + Math.Max(0, Math.Min(1000, mciVol)));
        }

        public static int PositionMs
        {
            get { if (!_open) return 0; int.TryParse(Query("status " + Alias + " position"), out int p); return p; }
        }

        /// <summary>True while a song is open and still playing (or paused).</summary>
        public static bool Busy
        {
            get
            {
                if (!_open) return false;
                string mode = Query("status " + Alias + " mode");
                return mode == "playing" || mode == "paused" || mode == "seeking";
            }
        }

        public static void Stop()
        {
            if (!_open) return;
            Send("stop " + Alias);
            Send("close " + Alias);
            _open = false;
            LengthMs = 0;
        }
    }
}

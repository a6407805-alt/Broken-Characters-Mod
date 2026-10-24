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
            int err = Send("open \"" + path + "\" type mpegvideo alias " + Alias);
            if (err != 0) { JukeboxPlugin.Log?.LogWarning("Jukebox couldn't open " + path + " (MCI error " + err + ")"); return false; }
            _open = true;
            Send("set " + Alias + " time format milliseconds");
            int.TryParse(Query("status " + Alias + " length"), out int len);
            LengthMs = len;
            SetVolume(volume);
            return Send("play " + Alias) == 0;
        }

        /// <summary>0-100 on a perceptual curve, mapped to MCI's 0-1000.</summary>
        public static void SetVolume(int volume)
        {
            if (!_open) return;
            float v = Math.Max(0, Math.Min(100, volume)) / 100f;
            Send("setaudio " + Alias + " volume to " + (int)(v * v * 1000f));
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

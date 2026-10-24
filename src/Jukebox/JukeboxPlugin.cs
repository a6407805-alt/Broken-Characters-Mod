using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

namespace Jukebox
{
    /// <summary>
    /// The group's Suno songs in game. The launcher (JUKEBOX window) downloads the songs to
    /// %APPDATA%\BrokenCharacters\Jukebox\ and writes playlist.json; this mod reads that folder.
    ///
    /// Vote flow: the host picks up to 3 songs and opens a vote (stage start, song end, or someone pressing J).
    /// Everyone votes with 7/8/9, the host counts, then tells every player which song to play. Each PC plays its
    /// own copy (winmm isn't networked), so a player who hasn't downloaded a song just sees its name.
    /// </summary>
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class JukeboxPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.brokencharacters.jukebox";
        public const string PluginName = "Jukebox";
        public const string PluginVersion = "1.0.0";

        public static ManualLogSource Log;
        public static JukeboxPlugin Instance;

        public static ConfigEntry<int> Volume;
        public static ConfigEntry<bool> MuteGameMusic, VoteOnStageStart, VoteWhenSongEnds, ShowHud;
        public static ConfigEntry<float> VoteSeconds;
        public static ConfigEntry<KeyCode> Vote1Key, Vote2Key, Vote3Key, StartVoteKey, StopKey, VolumeUpKey, VolumeDownKey;

        // ---- shared state (every machine) ----
        public static string[] VoteIds = new string[0], VoteTitles = new string[0];
        public static int[] VoteCounts = new int[0];
        public static float VoteEndsAt;
        public static bool VoteOpen;
        public static int MyVote = -1;
        public static string NowPlayingId = "", NowPlayingTitle = "";
        public static bool NowPlayingMissing, NowPlayingBroken;
        private static float _nowPlayingShownAt = -100f;
        private static string _toast = "";
        private static float _toastUntil;

        // ---- host only ----
        private static readonly Dictionary<uint, int> Ballots = new Dictionary<uint, int>();
        private static bool _hostWasPlaying;
        private static float _nextVoteAllowed;

        private int _volumeLast;
        private float _volumeShownUntil;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            Volume = Config.Bind("Sound", "Volume", 60, new ConfigDescription("Jukebox volume 0-100 (the game's sliders don't affect it).", new AcceptableValueRange<int>(0, 100)));
            MuteGameMusic = Config.Bind("Sound", "MuteGameMusic", true, "Silence the game's own music while a jukebox song plays.");
            VoteOnStageStart = Config.Bind("Vote", "VoteOnStageStart", true, "Open a song vote when a stage starts (if nothing is playing).");
            VoteWhenSongEnds = Config.Bind("Vote", "VoteWhenSongEnds", true, "Open the next vote when a song ends.");
            VoteSeconds = Config.Bind("Vote", "VoteSeconds", 15f, "How long a vote stays open.");
            ShowHud = Config.Bind("Display", "ShowHud", true, "Show the vote panel and the now-playing line.");
            Vote1Key = Config.Bind("Keys", "Vote1", KeyCode.Alpha7, "Vote for the first song.");
            Vote2Key = Config.Bind("Keys", "Vote2", KeyCode.Alpha8, "Vote for the second song.");
            Vote3Key = Config.Bind("Keys", "Vote3", KeyCode.Alpha9, "Vote for the third song.");
            StartVoteKey = Config.Bind("Keys", "StartVote", KeyCode.J, "Ask for a song vote now.");
            StopKey = Config.Bind("Keys", "Stop", KeyCode.K, "Host: stop the music for everyone.");
            VolumeUpKey = Config.Bind("Keys", "VolumeUp", KeyCode.Equals, "Jukebox volume up (hold Shift).");
            VolumeDownKey = Config.Bind("Keys", "VolumeDown", KeyCode.Minus, "Jukebox volume down (hold Shift).");
            _volumeLast = Volume.Value;

            NetworkingAPI.RegisterMessageType<JbVoteStartMessage>();
            NetworkingAPI.RegisterMessageType<JbBallotMessage>();
            NetworkingAPI.RegisterMessageType<JbTallyMessage>();
            NetworkingAPI.RegisterMessageType<JbPlayMessage>();
            NetworkingAPI.RegisterMessageType<JbRequestMessage>();

            Stage.onServerStageBegin += OnServerStageBegin;
            Run.onRunDestroyGlobal += _ => ResetAll();

            SongLibrary.Reload();
            Log.LogInfo("Jukebox ready: " + SongLibrary.Songs.Count + " song(s) in " + SongLibrary.Folder);
        }

        private void OnDestroy()
        {
            Stage.onServerStageBegin -= OnServerStageBegin;
            ResetAll();
        }

        private static void ResetAll()
        {
            MciPlayer.Stop();
            GameMusic.Restore();
            VoteOpen = false;
            NowPlayingId = NowPlayingTitle = "";
            Ballots.Clear();
            _hostWasPlaying = false;
        }

        private void OnServerStageBegin(Stage stage)
        {
            if (!VoteOnStageStart.Value || MciPlayer.Busy || VoteOpen) return;
            HostStartVote();
        }

        // ================================================================== every frame

        private void Update()
        {
            bool inRun = Run.instance != null;

            if (inRun && !ChatOpen())
            {
                if (VoteOpen)
                {
                    if (Input.GetKeyDown(Vote1Key.Value)) CastVote(0);
                    else if (Input.GetKeyDown(Vote2Key.Value)) CastVote(1);
                    else if (Input.GetKeyDown(Vote3Key.Value)) CastVote(2);
                }
                if (Input.GetKeyDown(StartVoteKey.Value)) RequestVote();
                if (Input.GetKeyDown(StopKey.Value))
                {
                    if (NetworkServer.active) HostPlay("", "");
                    else Toast("Only the host can stop the music.");
                }
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift && Input.GetKeyDown(VolumeUpKey.Value)) Volume.Value = Mathf.Min(100, Volume.Value + 10);
                if (shift && Input.GetKeyDown(VolumeDownKey.Value)) Volume.Value = Mathf.Max(0, Volume.Value - 10);
            }

            if (_volumeLast != Volume.Value)
            {
                _volumeLast = Volume.Value;
                MciPlayer.SetVolume(Volume.Value);
                _volumeShownUntil = Time.unscaledTime + 2f;
            }

            if (NetworkServer.active && inRun) HostTick();

            // Song finished on this machine: give the game its music back.
            if (!MciPlayer.Busy && MciPlayer.IsOpen) { MciPlayer.Stop(); GameMusic.Restore(); }
        }

        private static Type _chatType;

        private static bool ChatOpen()
        {
            // ChatBox.showInput is read by reflection so a game update can't break the build.
            try
            {
                foreach (Component box in FindObjectsOfType(_chatType ??= typeof(RoR2.UI.HUD).Assembly.GetType("RoR2.UI.ChatBox")))
                {
                    PropertyInfo p = box.GetType().GetProperty("showInput", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    object v = p != null ? p.GetValue(box) : box.GetType().GetField("_showInput", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(box);
                    if (v is bool b && b) return true;
                }
            }
            catch { }
            return false;
        }

        private static void HostTick()
        {
            if (VoteOpen && Time.unscaledTime >= VoteEndsAt) HostCloseVote();

            bool playing = MciPlayer.Busy;
            if (_hostWasPlaying && !playing && !VoteOpen && NowPlayingId.Length > 0 && VoteWhenSongEnds.Value)
            {
                NowPlayingId = NowPlayingTitle = "";
                HostStartVote();
            }
            _hostWasPlaying = playing;
        }

        // ================================================================== host side

        public static void HostStartVote()
        {
            if (!NetworkServer.active || VoteOpen) return;
            if (Time.unscaledTime < _nextVoteAllowed) return;
            _nextVoteAllowed = Time.unscaledTime + 2f;
            SongLibrary.Reload();
            List<Song> pool = SongLibrary.Songs.Where(s => s.Downloaded && s.id != NowPlayingId).ToList();
            if (pool.Count == 0) pool = SongLibrary.Songs.Where(s => s.Downloaded).ToList();
            if (pool.Count == 0)
            {
                Toast("Jukebox: the host has no songs yet. Add some in the launcher's JUKEBOX window.");
                return;
            }
            for (int i = pool.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            List<Song> pick = pool.Take(3).ToList();

            var msg = new JbVoteStartMessage
            {
                Ids = pick.Select(s => s.id).ToArray(),
                Titles = pick.Select(s => s.Label).ToArray(),
                Seconds = Mathf.Clamp(VoteSeconds.Value, 3f, 120f)
            };
            Ballots.Clear();
            msg.Handle();
            try { msg.Send(NetworkDestination.Clients); } catch (Exception e) { Log.LogWarning("Jukebox vote send failed: " + e.Message); }

            // Only one choice: no point waiting.
            if (pick.Count == 1) VoteEndsAt = Time.unscaledTime + 1.5f;
        }

        public static void HostReceiveBallot(uint voter, int choice)
        {
            if (!NetworkServer.active || !VoteOpen || choice < 0 || choice >= VoteIds.Length) return;
            Ballots[voter] = choice;
            var tally = new JbTallyMessage { Counts = new int[VoteIds.Length] };
            foreach (int c in Ballots.Values) tally.Counts[c]++;
            tally.Handle();
            try { tally.Send(NetworkDestination.Clients); } catch { }

            // Everyone has voted: close early.
            int players = NetworkUser.readOnlyInstancesList.Count;
            if (players > 0 && Ballots.Count >= players) VoteEndsAt = Mathf.Min(VoteEndsAt, Time.unscaledTime + 1f);
        }

        private static void HostCloseVote()
        {
            if (VoteIds.Length == 0) { VoteOpen = false; return; }
            int[] counts = new int[VoteIds.Length];
            foreach (int c in Ballots.Values) counts[c]++;
            int best = counts.Max();
            List<int> tied = Enumerable.Range(0, counts.Length).Where(i => counts[i] == best).ToList();
            int winner = tied[Random.Range(0, tied.Count)];
            HostPlay(VoteIds[winner], VoteTitles[winner]);
        }

        public static void HostPlay(string id, string title)
        {
            if (!NetworkServer.active) return;
            var msg = new JbPlayMessage { Id = id ?? "", Title = title ?? "" };
            msg.Handle();
            try { msg.Send(NetworkDestination.Clients); } catch { }
            _hostWasPlaying = MciPlayer.Busy;
        }

        // ================================================================== client side

        private static void CastVote(int choice)
        {
            if (!VoteOpen || choice >= VoteIds.Length) return;
            MyVote = choice;
            uint me = LocalVoterId();
            if (NetworkServer.active) HostReceiveBallot(me, choice);
            else try { new JbBallotMessage { Voter = me, Choice = choice }.Send(NetworkDestination.Server); } catch { }
        }

        private static void RequestVote()
        {
            if (VoteOpen) return;
            if (NetworkServer.active) HostStartVote();
            else
            {
                try { new JbRequestMessage().Send(NetworkDestination.Server); } catch { }
                Toast("Asked the host for a song vote.");
            }
        }

        private static uint LocalVoterId()
        {
            try
            {
                NetworkUser user = LocalUserManager.GetFirstLocalUser()?.currentNetworkUser;
                if (user) return user.netId.Value;
            }
            catch { }
            return 0;
        }

        public static void ApplyVoteStart(string[] ids, string[] titles, float seconds)
        {
            VoteIds = ids ?? new string[0];
            VoteTitles = titles ?? new string[0];
            VoteCounts = new int[VoteIds.Length];
            VoteEndsAt = Time.unscaledTime + seconds;
            VoteOpen = VoteIds.Length > 0;
            MyVote = -1;
        }

        public static void ApplyPlay(string id, string title)
        {
            VoteOpen = false;
            MyVote = -1;
            MciPlayer.Stop();
            NowPlayingId = id ?? "";
            NowPlayingTitle = title ?? "";
            NowPlayingMissing = false;
            NowPlayingBroken = false;
            if (NowPlayingId.Length == 0) { GameMusic.Restore(); Toast("Jukebox stopped."); return; }

            SongLibrary.Reload();
            Song song = SongLibrary.Find(NowPlayingId);
            if (song != null && song.Downloaded && MciPlayer.Play(song.Path, Volume.Value))
            {
                if (MuteGameMusic.Value) GameMusic.Mute();
            }
            else
            {
                NowPlayingMissing = true;
                NowPlayingBroken = song != null && song.Downloaded; // the file is there, Windows wouldn't play it
                GameMusic.Restore();
            }
            _nowPlayingShownAt = Time.unscaledTime;
        }

        public static void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 4f;
            Log?.LogInfo(text);
        }

        // ================================================================== HUD

        private GUIStyle _title, _line, _small;

        private void OnGUI()
        {
            if (!ShowHud.Value || Run.instance == null) return;
            float s = Screen.height / 1080f;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                _line = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip };
                _small = new GUIStyle(GUI.skin.label);
            }
            _title.fontSize = Mathf.RoundToInt(20 * s);
            _line.fontSize = Mathf.RoundToInt(17 * s);
            _small.fontSize = Mathf.RoundToInt(14 * s);

            float w = 420 * s, x = Screen.width - w - 24 * s, y = 150 * s;

            if (VoteOpen)
            {
                float h = (52 + 30 * VoteIds.Length + 26) * s;
                GUI.color = new Color(0.06f, 0.08f, 0.13f, 0.88f);
                GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.79f, 0.16f);
                GUI.DrawTexture(new Rect(x, y, w, 3 * s), Texture2D.whiteTexture);
                GUI.color = Color.white;

                int left = Mathf.Max(0, Mathf.CeilToInt(VoteEndsAt - Time.unscaledTime));
                _title.normal.textColor = new Color(1f, 0.79f, 0.16f);
                GUI.Label(new Rect(x + 14 * s, y + 10 * s, w, 30 * s), "SONG VOTE  " + left + "s", _title);

                KeyCode[] keys = { Vote1Key.Value, Vote2Key.Value, Vote3Key.Value };
                for (int i = 0; i < VoteIds.Length; i++)
                {
                    float ly = y + (46 + 30 * i) * s;
                    bool mine = MyVote == i;
                    _line.normal.textColor = mine ? new Color(1f, 0.79f, 0.16f) : Color.white;
                    int votes = i < VoteCounts.Length ? VoteCounts[i] : 0;
                    bool have = SongLibrary.Find(VoteIds[i])?.Downloaded == true;
                    string label = "[" + KeyName(keys[i]) + "]  " + VoteTitles[i] + (have ? "" : "  (not downloaded)");
                    GUI.Label(new Rect(x + 14 * s, ly, w - 70 * s, 28 * s), label, _line);
                    GUI.Label(new Rect(x + w - 54 * s, ly, 50 * s, 28 * s), votes > 0 ? new string('|', Mathf.Min(votes, 8)) + " " + votes : "", _line);
                }
                _small.normal.textColor = new Color(0.62f, 0.7f, 0.82f);
                GUI.Label(new Rect(x + 14 * s, y + h - 24 * s, w, 22 * s),
                    MyVote < 0 ? "Press " + KeyName(keys[0]) + " / " + KeyName(keys[1]) + " / " + KeyName(keys[2]) + " to vote" : "Vote counted. You can still change it.", _small);
                y += h + 8 * s;
            }

            if (NowPlayingId.Length > 0)
            {
                bool big = Time.unscaledTime - _nowPlayingShownAt < 6f;
                string text = (NowPlayingBroken ? "Couldn't play: " : NowPlayingMissing ? "Not downloaded: " : "Now playing: ") + NowPlayingTitle;
                if (!NowPlayingMissing && MciPlayer.LengthMs > 0)
                {
                    int pos = MciPlayer.PositionMs / 1000, len = MciPlayer.LengthMs / 1000;
                    text += "   " + pos / 60 + ":" + (pos % 60).ToString("00") + " / " + len / 60 + ":" + (len % 60).ToString("00");
                }
                GUIStyle st = big ? _line : _small;
                st.normal.textColor = NowPlayingMissing ? new Color(1f, 0.6f, 0.55f) : new Color(1f, 0.79f, 0.16f);
                GUI.Label(new Rect(x, y, w, 26 * s), "♫ " + text, st);
                y += 26 * s;
            }

            if (Time.unscaledTime < _volumeShownUntil)
            {
                _small.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, w, 22 * s), "Jukebox volume " + Volume.Value, _small);
                y += 22 * s;
            }

            if (Time.unscaledTime < _toastUntil)
            {
                _small.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, w, 44 * s), _toast, new GUIStyle(_small) { wordWrap = true });
            }
        }

        private static string KeyName(KeyCode k)
        {
            string n = k.ToString();
            return n.StartsWith("Alpha") ? n.Substring(5) : n.StartsWith("Keypad") ? "Num" + n.Substring(6) : n;
        }
    }

    /// <summary>Turns the game's Wwise music down while a jukebox song plays, then back to the player's setting.</summary>
    public static class GameMusic
    {
        private static bool _muted;
        private static MethodInfo _setRtpc;
        private static bool _looked;

        private static MethodInfo SetRtpc()
        {
            if (_looked) return _setRtpc;
            _looked = true;
            try
            {
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = a.GetType("AkSoundEngine", false);
                    if (t == null) continue;
                    _setRtpc = t.GetMethod("SetRTPCValue", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(float) }, null);
                    if (_setRtpc != null) break;
                }
            }
            catch (Exception e) { JukeboxPlugin.Log?.LogWarning("Jukebox: couldn't find the game's music volume: " + e.Message); }
            return _setRtpc;
        }

        private static void SetMusicVolume(float value)
        {
            try { SetRtpc()?.Invoke(null, new object[] { "Volume_MSX", value }); }
            catch (Exception e) { JukeboxPlugin.Log?.LogWarning("Jukebox: music volume change failed: " + e.Message); }
        }

        public static void Mute()
        {
            SetMusicVolume(0f);
            _muted = true;
        }

        public static void Restore()
        {
            if (!_muted) return;
            _muted = false;
            SetMusicVolume(PlayerMusicVolume());
        }

        /// <summary>The player's own music slider (convar volume_music, 0-100).</summary>
        private static float PlayerMusicVolume()
        {
            try
            {
                object console = RoR2.Console.instance;
                MethodInfo find = console?.GetType().GetMethod("FindConVar", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object conVar = find?.Invoke(console, new object[] { "volume_music" });
                string text = conVar?.GetType().GetMethod("GetString")?.Invoke(conVar, null) as string;
                if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            }
            catch { }
            return 50f;
        }
    }
}

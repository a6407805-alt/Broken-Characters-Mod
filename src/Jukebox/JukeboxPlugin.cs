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
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Jukebox
{
    /// <summary>
    /// The group's Suno songs in game. The launcher (JUKEBOX window) downloads the songs to
    /// %APPDATA%\BrokenCharacters\Jukebox\ and writes playlist.json; this mod reads that folder.
    ///
    /// Vote flow: the host picks up to 3 songs and opens a vote (stage start, song end, or someone pressing J).
    /// Everyone votes with 7/8/9 (or clicks when the cursor is free), the host counts, then tells every player
    /// which song to play. Each PC plays its own copy (winmm isn't networked), so a player who hasn't downloaded
    /// a song just sees its name.
    /// </summary>
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class JukeboxPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.brokencharacters.jukebox";
        public const string PluginName = "Jukebox";
        public const string PluginVersion = "1.1.0";

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
        private bool _draggingVolume;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            Volume = Config.Bind("Sound", "Volume", 60, new ConfigDescription("Jukebox volume 0-100 (the game's sliders don't affect it).", new AcceptableValueRange<int>(0, 100)));
            MuteGameMusic = Config.Bind("Sound", "MuteGameMusic", true, "Silence the game's own music while a jukebox song plays.");
            VoteOnStageStart = Config.Bind("Vote", "VoteOnStageStart", true, "Open a song vote when a stage starts (if nothing is playing).");
            VoteWhenSongEnds = Config.Bind("Vote", "VoteWhenSongEnds", true, "Open the next vote when a song ends.");
            VoteSeconds = Config.Bind("Vote", "VoteSeconds", 15f, "How long a vote stays open.");
            ShowHud = Config.Bind("Display", "ShowHud", true, "Show the jukebox panel under the difficulty bar.");
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
                if (Input.GetKeyDown(StopKey.Value)) StopPressed();
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift && Input.GetKeyDown(VolumeUpKey.Value)) Volume.Value = Mathf.Min(100, Volume.Value + 10);
                if (shift && Input.GetKeyDown(VolumeDownKey.Value)) Volume.Value = Mathf.Max(0, Volume.Value - 10);
            }

            if (_volumeLast != Volume.Value)
            {
                _volumeLast = Volume.Value;
                MciPlayer.SetVolume(Volume.Value);
            }

            if (NetworkServer.active && inRun) HostTick();

            // Song finished on this machine: give the game its music back.
            if (!MciPlayer.Busy && MciPlayer.IsOpen) { MciPlayer.Stop(); GameMusic.Restore(); }
        }

        private static void StopPressed()
        {
            if (NetworkServer.active) HostPlay("", "");
            else Toast("Only the host can stop the music.");
        }

        private static Type _chatType;

        private static bool ChatOpen()
        {
            // ChatBox.showInput is read by reflection so a game update can't break the build.
            try
            {
                foreach (Component box in FindObjectsOfType(_chatType ??= typeof(HUD).Assembly.GetType("RoR2.UI.ChatBox")))
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
            if (!VoteOpen || choice < 0 || choice >= VoteIds.Length) return;
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

        // ================================================================== HUD (under difficulty + objectives)

        private static HUD _hud;
        private static Canvas _hudCanvas;
        private static RectTransform _diffRt;
        private static ObjectivePanelController _objPanel;
        private static float _nextHudScan;
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Finds the active HUD's difficulty bar and objective panel so the jukebox card sits right under them
        /// without overlapping either one, at the exact width of the difficulty panel.
        /// </summary>
        private static void PlaceBelowDifficulty(float s, out float x, out float y, out float w)
        {
            w = 340f * s;
            x = Screen.width - w - 22f * s;
            y = 215f * s;
            try
            {
                if ((!_hud || !_hud.gameObject.activeInHierarchy || !_diffRt) && Time.unscaledTime >= _nextHudScan)
                {
                    _nextHudScan = Time.unscaledTime + 1f;
                    _hud = null; _hudCanvas = null; _diffRt = null; _objPanel = null;
                    foreach (HUD h in HUD.readOnlyInstanceList)
                    {
                        if (!h || !h.gameObject.activeInHierarchy) continue;
                        _hud = h;
                        _hudCanvas = h.mainContainer ? h.mainContainer.GetComponentInParent<Canvas>() : h.GetComponentInParent<Canvas>();
                        DifficultyBarController diff = h.GetComponentInChildren<DifficultyBarController>(true);
                        if (diff) _diffRt = (diff.viewPort ? diff.viewPort.parent as RectTransform : null) ?? diff.gameObject.transform as RectTransform;
                        _objPanel = h.GetComponentInChildren<ObjectivePanelController>(true);
                        break;
                    }
                }

                float bottom = 0f;
                if (ScreenRect(_diffRt, out Rect dr) && dr.width > 80f)
                {
                    w = Mathf.Clamp(dr.width, 280f * s, 440f * s);
                    x = Mathf.Clamp(dr.xMax - w, 8f * s, Screen.width - w - 8f * s);
                    bottom = Mathf.Max(bottom, dr.yMax);
                }

                // Step below the objective panel and all active objective rows so we never overlap them.
                if (_objPanel && _objPanel.gameObject.activeInHierarchy)
                {
                    if (ScreenRect(_objPanel.gameObject.transform as RectTransform, out Rect or) && or.height > 4f && or.yMax < Screen.height * 0.75f)
                        bottom = Mathf.Max(bottom, or.yMax);
                    RectTransform box = _objPanel.objectiveTrackerContainer;
                    if (box && box.gameObject.activeInHierarchy)
                    {
                        if (ScreenRect(box, out Rect br) && br.height > 2f && br.yMax < Screen.height * 0.75f)
                            bottom = Mathf.Max(bottom, br.yMax);
                        for (int i = 0; i < box.childCount; i++)
                        {
                            RectTransform child = box.GetChild(i) as RectTransform;
                            if (child && child.gameObject.activeInHierarchy && ScreenRect(child, out Rect cr) && cr.yMax < Screen.height * 0.75f)
                                bottom = Mathf.Max(bottom, cr.yMax);
                        }
                    }
                }

                if (bottom > 20f) y = bottom + 8f * s;
            }
            catch { }
        }

        private static bool ScreenRect(RectTransform rt, out Rect r)
        {
            r = default;
            if (!rt || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(Corners);
            Camera cam = _hudCanvas && _hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _hudCanvas.worldCamera : null;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
                float gx = p.x, gy = Screen.height - p.y;
                if (gx < min.x) min.x = gx;
                if (gy < min.y) min.y = gy;
                if (gx > max.x) max.x = gx;
                if (gy > max.y) max.y = gy;
            }
            r = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            return r.width > 4f && r.height > 2f;
        }

        private GUIStyle _hdrStyle, _subStyle, _rowStyle, _rightStyle, _chipStyle;

        // RoR2 HUD palette: dark slate backdrop, subtle 1px frame, warm header band, gold/cyan accents.
        private static readonly Color BgDark = new Color(0.05f, 0.06f, 0.08f, 0.84f);
        private static readonly Color HeaderBand = new Color(0.13f, 0.16f, 0.21f, 0.92f);
        private static readonly Color StripBg = new Color(0.10f, 0.12f, 0.16f, 0.78f);
        private static readonly Color StripHover = new Color(0.17f, 0.20f, 0.27f, 0.90f);
        private static readonly Color StripMine = new Color(0.24f, 0.21f, 0.11f, 0.92f);
        private static readonly Color BorderCol = new Color(0.38f, 0.43f, 0.50f, 0.52f);
        private static readonly Color Gold = new Color(0.95f, 0.78f, 0.26f, 1f);
        private static readonly Color Cyan = new Color(0.46f, 0.80f, 0.96f, 1f);
        private static readonly Color TextBright = new Color(0.93f, 0.95f, 0.98f, 1f);
        private static readonly Color TextMuted = new Color(0.62f, 0.68f, 0.76f, 1f);
        private static readonly Color TextWarn = new Color(1f, 0.55f, 0.50f, 1f);

        private void InitStyles(float s)
        {
            if (_hdrStyle == null)
            {
                _hdrStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                _subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
                _rowStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
                _rightStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false };
                _chipStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }
            _hdrStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(13f * s));
            _subStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(12f * s));
            _rowStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(13f * s));
            _rightStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(12f * s));
            _chipStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * s));
        }

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void Border(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static void ShadowLabel(Rect r, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f * color.a);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        private bool ChipButton(Rect r, string label, Color accent, bool canClick, Event ev)
        {
            bool hov = canClick && r.Contains(ev.mousePosition);
            Fill(r, hov ? new Color(accent.r * 0.32f, accent.g * 0.32f, accent.b * 0.32f, 0.95f) : new Color(0.08f, 0.10f, 0.14f, 0.88f));
            Border(r, hov ? accent : new Color(accent.r, accent.g, accent.b, 0.45f), 1f);
            ShadowLabel(r, label, _chipStyle, hov ? Color.white : accent);
            if (hov && ev.type == EventType.MouseDown && ev.button == 0)
            {
                ev.Use();
                return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!ShowHud.Value || Run.instance == null) return;
            // Hide while the scoreboard (Tab) is held unless the pause menu is open so it never clashes with the inventory overlay.
            if (!PauseScreenController.paused && _hud && _hud.scoreboardPanel && _hud.scoreboardPanel.activeInHierarchy) return;

            float s = Mathf.Max(0.65f, Screen.height / 1080f);
            InitStyles(s);
            PlaceBelowDifficulty(s, out float x, out float y, out float w);

            Event ev = Event.current;
            bool canClick = Cursor.visible || PauseScreenController.paused;
            if (!canClick || ev.type == EventType.MouseUp) _draggingVolume = false;

            float pad = 8f * s;
            float hdrH = 22f * s;
            float songH = 32f * s;
            float voteHdrH = 20f * s;
            float voteRowH = 24f * s;
            float volH = 24f * s;
            float toastH = Time.unscaledTime < _toastUntil ? 20f * s : 0f;

            float voteH = VoteOpen ? (voteHdrH + VoteIds.Length * (voteRowH + 3f * s) + 4f * s) : 0f;
            float totalH = hdrH + pad + songH + 6f * s + voteH + volH + toastH + pad;

            Rect panel = new Rect(x, y, w, totalH);
            Fill(panel, BgDark);
            Border(panel, BorderCol, 1f);

            // Corner ticks like RoR2's HUD frames
            float tick = 5f * s;
            Fill(new Rect(panel.x, panel.y, tick, 2f), Gold);
            Fill(new Rect(panel.x, panel.y, 2f, tick), Gold);
            Fill(new Rect(panel.xMax - tick, panel.y, tick, 2f), Gold);
            Fill(new Rect(panel.xMax - 2f, panel.y, 2f, tick), Gold);

            // ---- 1. Header band (matches OBJECTIVES header style) ----
            Rect hdr = new Rect(x + 1f, y + 1f, w - 2f, hdrH);
            Fill(hdr, HeaderBand);
            Fill(new Rect(hdr.x, hdr.y, 3f * s, hdr.height), Gold);
            Fill(new Rect(hdr.x, hdr.yMax - 1f, hdr.width, 1f), new Color(1f, 1f, 1f, 0.08f));
            ShadowLabel(new Rect(hdr.x + 9f * s, hdr.y, hdr.width * 0.5f, hdr.height), "♫  JUKEBOX", _hdrStyle, Gold);

            // Action chips on the right of the header ([J] VOTE, [K] STOP)
            float chipH = hdrH - 4f * s;
            float rightX = hdr.xMax - 5f * s;
            if (NowPlayingId.Length > 0 && NetworkServer.active)
            {
                float stopW = 58f * s;
                rightX -= stopW;
                if (ChipButton(new Rect(rightX, hdr.y + 2f * s, stopW, chipH), "[" + KeyName(StopKey.Value) + "] STOP", TextWarn, canClick, ev))
                    StopPressed();
                rightX -= 4f * s;
            }
            if (!VoteOpen)
            {
                float voteW = 62f * s;
                rightX -= voteW;
                if (ChipButton(new Rect(rightX, hdr.y + 2f * s, voteW, chipH), "[" + KeyName(StartVoteKey.Value) + "] VOTE", Cyan, canClick, ev))
                    RequestVote();
            }

            float cy = y + hdrH + pad * 0.75f;
            float innerX = x + pad;
            float innerW = w - pad * 2f;

            // ---- 2. Now-playing strip (with progress bar) ----
            Rect songBox = new Rect(innerX, cy, innerW, songH);
            Fill(songBox, StripBg);
            bool playingOk = NowPlayingId.Length > 0 && !NowPlayingMissing && !NowPlayingBroken;
            Color stripAccent = NowPlayingId.Length == 0 ? BorderCol : playingOk ? Cyan : TextWarn;
            Fill(new Rect(songBox.x, songBox.y, 3f * s, songBox.height), stripAccent);

            if (NowPlayingId.Length > 0)
            {
                string prefix = NowPlayingBroken ? "Couldn't play: " : NowPlayingMissing ? "Not downloaded: " : "";
                Color titleCol = playingOk ? TextBright : TextWarn;
                string timeStr = "";
                float pct = 0f;
                if (playingOk && MciPlayer.LengthMs > 0)
                {
                    int pos = Mathf.Clamp(MciPlayer.PositionMs, 0, MciPlayer.LengthMs);
                    int len = MciPlayer.LengthMs;
                    pct = Mathf.Clamp01((float)pos / len);
                    int ps = pos / 1000, ls = len / 1000;
                    timeStr = (ps / 60) + ":" + (ps % 60).ToString("00") + " / " + (ls / 60) + ":" + (ls % 60).ToString("00");
                }
                float timeW = timeStr.Length > 0 ? 78f * s : 0f;
                ShadowLabel(new Rect(songBox.x + 8f * s, songBox.y + 2f * s, songBox.width - 14f * s - timeW, 20f * s),
                    prefix + NowPlayingTitle, _rowStyle, titleCol);
                if (timeW > 0f)
                    ShadowLabel(new Rect(songBox.xMax - timeW - 6f * s, songBox.y + 2f * s, timeW, 20f * s), timeStr, _rightStyle, TextMuted);

                // Progress bar along bottom of the strip
                Rect progBg = new Rect(songBox.x + 8f * s, songBox.yMax - 6f * s, songBox.width - 14f * s, 3f * s);
                Fill(progBg, new Color(0f, 0f, 0f, 0.55f));
                if (pct > 0f) Fill(new Rect(progBg.x, progBg.y, progBg.width * pct, progBg.height), Cyan);
            }
            else
            {
                string idle = VoteOpen ? "Vote below to pick the next track"
                    : SongLibrary.Songs.Count == 0 ? "No songs in folder — add some in Launcher > JUKEBOX"
                    : "Idle — press [" + KeyName(StartVoteKey.Value) + "] to start a song vote";
                ShadowLabel(new Rect(songBox.x + 8f * s, songBox.y, songBox.width - 12f * s, songBox.height), idle, _subStyle, TextMuted);
            }
            cy += songH + 6f * s;

            // ---- 3. Song vote strips ----
            if (VoteOpen)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(VoteEndsAt - Time.unscaledTime));
                float totalDuration = Mathf.Max(1f, VoteSeconds.Value);
                float remainPct = Mathf.Clamp01((VoteEndsAt - Time.unscaledTime) / totalDuration);

                ShadowLabel(new Rect(innerX + 2f * s, cy, innerW * 0.65f, voteHdrH),
                    MyVote < 0 ? "SONG VOTE  ·  pick a track" : "SONG VOTE  ·  vote counted", _subStyle, Gold);
                ShadowLabel(new Rect(innerX, cy, innerW - 2f * s, voteHdrH), left + "s", _rightStyle, Gold);
                cy += voteHdrH;

                KeyCode[] keys = { Vote1Key.Value, Vote2Key.Value, Vote3Key.Value };
                int totalVotes = 0;
                for (int i = 0; i < VoteCounts.Length; i++) totalVotes += VoteCounts[i];

                for (int i = 0; i < VoteIds.Length; i++)
                {
                    Rect r = new Rect(innerX, cy, innerW, voteRowH);
                    bool mine = MyVote == i;
                    bool hov = canClick && r.Contains(ev.mousePosition);
                    int votes = i < VoteCounts.Length ? VoteCounts[i] : 0;
                    bool have = SongLibrary.Find(VoteIds[i])?.Downloaded == true;

                    Fill(r, mine ? StripMine : hov ? StripHover : StripBg);
                    if (votes > 0 && totalVotes > 0)
                    {
                        float frac = Mathf.Clamp01((float)votes / totalVotes);
                        Fill(new Rect(r.x, r.y, r.width * frac, r.height),
                            mine ? new Color(0.95f, 0.78f, 0.26f, 0.18f) : new Color(0.46f, 0.80f, 0.96f, 0.14f));
                    }
                    Fill(new Rect(r.x, r.y, 3f * s, r.height), mine ? Gold : hov ? Cyan : new Color(1f, 1f, 1f, 0.22f));

                    // Key badge
                    Rect badge = new Rect(r.x + 7f * s, r.y + 3f * s, 22f * s, r.height - 6f * s);
                    Fill(badge, mine ? new Color(0.95f, 0.78f, 0.26f, 0.28f) : new Color(0f, 0f, 0f, 0.45f));
                    Border(badge, mine ? Gold : BorderCol, 1f);
                    ShadowLabel(badge, KeyName(keys[i]), _chipStyle, mine ? Gold : TextBright);

                    string title = VoteTitles[i] + (have ? "" : " (missing)");
                    float countW = votes > 0 ? 36f * s : 6f * s;
                    ShadowLabel(new Rect(badge.xMax + 6f * s, r.y, r.width - (badge.xMax - r.x) - countW - 8f * s, r.height),
                        title, _rowStyle, !have ? TextWarn : mine ? Gold : TextBright);

                    if (votes > 0)
                        ShadowLabel(new Rect(r.xMax - countW - 6f * s, r.y, countW, r.height),
                            votes + (votes == 1 ? "v" : "v"), _rightStyle, mine ? Gold : Cyan);

                    if (hov && ev.type == EventType.MouseDown && ev.button == 0)
                    {
                        CastVote(i);
                        ev.Use();
                    }
                    cy += voteRowH + 3f * s;
                }

                // Thin countdown bar under the choices
                Rect timerBar = new Rect(innerX, cy, innerW, 2f * s);
                Fill(timerBar, new Color(1f, 1f, 1f, 0.10f));
                Fill(new Rect(timerBar.x, timerBar.y, timerBar.width * remainPct, timerBar.height), Gold);
                cy += 4f * s;
            }

            // ---- 4. In-game Volume Slider + [-]/[+] buttons ----
            Rect volRow = new Rect(innerX, cy, innerW, volH);
            float lblW = 36f * s;
            float btnW = 20f * s;
            float valW = 34f * s;
            ShadowLabel(new Rect(volRow.x + 2f * s, volRow.y, lblW, volRow.height), "VOL", _chipStyle, TextMuted);

            Rect minusRect = new Rect(volRow.x + lblW + 2f * s, volRow.y + 3f * s, btnW, volRow.height - 6f * s);
            if (ChipButton(minusRect, "−", TextBright, canClick, ev))
                Volume.Value = Mathf.Max(0, Volume.Value - 5);

            Rect trackHit = new Rect(minusRect.xMax + 6f * s, volRow.y + 2f * s,
                volRow.width - lblW - btnW * 2f - valW - 20f * s, volRow.height - 4f * s);
            Rect trackBar = new Rect(trackHit.x, volRow.y + (volRow.height - 6f * s) * 0.5f, trackHit.width, 6f * s);

            if (canClick && ev.type == EventType.MouseDown && ev.button == 0 && trackHit.Contains(ev.mousePosition))
            {
                _draggingVolume = true;
                Volume.Value = Mathf.RoundToInt(Mathf.Clamp01((ev.mousePosition.x - trackBar.x) / trackBar.width) * 100f);
                ev.Use();
            }
            else if (_draggingVolume && ev.type == EventType.MouseDrag)
            {
                Volume.Value = Mathf.RoundToInt(Mathf.Clamp01((ev.mousePosition.x - trackBar.x) / trackBar.width) * 100f);
                ev.Use();
            }
            if (canClick && ev.type == EventType.ScrollWheel && volRow.Contains(ev.mousePosition))
            {
                int step = ev.delta.y < 0f ? 5 : -5;
                Volume.Value = Mathf.Clamp(Volume.Value + step, 0, 100);
                ev.Use();
            }

            float volFrac = Mathf.Clamp01(Volume.Value / 100f);
            bool trackHov = canClick && (trackHit.Contains(ev.mousePosition) || _draggingVolume);
            Fill(trackBar, new Color(0.02f, 0.03f, 0.05f, 0.85f));
            Border(trackBar, trackHov ? Gold : BorderCol, 1f);
            if (volFrac > 0f)
                Fill(new Rect(trackBar.x + 1f, trackBar.y + 1f, (trackBar.width - 2f) * volFrac, trackBar.height - 2f),
                    trackHov ? Gold : Cyan);

            // Slider thumb
            float thumbW = 6f * s, thumbH = 12f * s;
            float thumbX = trackBar.x + trackBar.width * volFrac - thumbW * 0.5f;
            Rect thumb = new Rect(thumbX, volRow.y + (volRow.height - thumbH) * 0.5f, thumbW, thumbH);
            Fill(thumb, trackHov ? Gold : TextBright);
            Border(thumb, new Color(0f, 0f, 0f, 0.7f), 1f);

            Rect plusRect = new Rect(trackHit.xMax + 6f * s, volRow.y + 3f * s, btnW, volRow.height - 6f * s);
            if (ChipButton(plusRect, "+", TextBright, canClick, ev))
                Volume.Value = Mathf.Min(100, Volume.Value + 5);

            ShadowLabel(new Rect(plusRect.xMax + 2f * s, volRow.y, valW, volRow.height), Volume.Value + "%", _rightStyle,
                Volume.Value == 0 ? TextWarn : TextBright);
            cy += volH;

            // ---- 5. Toast notification line (if active) ----
            if (Time.unscaledTime < _toastUntil)
            {
                ShadowLabel(new Rect(innerX + 2f * s, cy, innerW - 4f * s, toastH), _toast, _subStyle, Gold);
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

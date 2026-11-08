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
        public const string PluginVersion = "1.1.3";

        public static ManualLogSource Log;
        public static JukeboxPlugin Instance;

        public static ConfigEntry<int> Volume;
        public static ConfigEntry<bool> MuteGameMusic, VoteOnStageStart, VoteWhenSongEnds, ShowHud;
        public static ConfigEntry<float> VoteSeconds;
        public static ConfigEntry<KeyCode> Vote1Key, Vote2Key, Vote3Key, StartVoteKey, SkipKey, StopKey, VolumeUpKey, VolumeDownKey;

        // ---- shared state (every machine) ----
        public static string[] VoteIds = new string[0], VoteTitles = new string[0];
        public static int[] VoteCounts = new int[0];
        public static float VoteEndsAt;
        public static bool VoteOpen;
        public static int MyVote = -1;
        public static string NowPlayingId = "", NowPlayingTitle = "";
        public static bool NowPlayingMissing, NowPlayingBroken;
        public static int SkipCount, SkipNeeded;
        public static bool MySkipVote;
        private static float _nowPlayingShownAt = -100f;
        private static string _toast = "";
        private static float _toastUntil;

        // ---- host only ----
        private static readonly Dictionary<uint, int> Ballots = new Dictionary<uint, int>();
        private static readonly HashSet<uint> SkipBallots = new HashSet<uint>();
        private static bool _hostWasPlaying;
        private static float _nextVoteAllowed;

        private int _volumeLast;
        private bool _draggingVolume;
        private float _volHoldTimer;
        private float _volRampNext;
        private float _lastTapDownTime = -10f, _lastTapUpTime = -10f;
        private float _volPillUntil = -10f;
        private string _volPillTag = "";
        public const int MaxVolume = 150;
        public const int PresetGarageRock = 135;
        public const int PresetTacticalLow = 30;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            Volume = Config.Bind("Sound", "Volume", 85, new ConfigDescription("Jukebox volume 0-150 (100-150 = Garage-Rock Boost ceiling that punches through heavy game SFX).", new AcceptableValueRange<int>(0, MaxVolume)));
            MuteGameMusic = Config.Bind("Sound", "MuteGameMusic", true, "Silence the game's own music while a jukebox song plays.");
            VoteOnStageStart = Config.Bind("Vote", "VoteOnStageStart", true, "Open a song vote when a stage starts (if nothing is playing).");
            VoteWhenSongEnds = Config.Bind("Vote", "VoteWhenSongEnds", true, "Open the next vote when a song ends.");
            VoteSeconds = Config.Bind("Vote", "VoteSeconds", 15f, "How long a vote stays open.");
            ShowHud = Config.Bind("Display", "ShowHud", true, "Show the jukebox panel under the difficulty bar.");
            Vote1Key = Config.Bind("Keys", "Vote1", KeyCode.Alpha7, "Vote for the first song.");
            Vote2Key = Config.Bind("Keys", "Vote2", KeyCode.Alpha8, "Vote for the second song.");
            Vote3Key = Config.Bind("Keys", "Vote3", KeyCode.Alpha9, "Vote for the third song.");
            StartVoteKey = Config.Bind("Keys", "StartVote", KeyCode.J, "Ask for a song vote now.");
            SkipKey = Config.Bind("Keys", "Skip", KeyCode.N, "Vote to skip the current song.");
            StopKey = Config.Bind("Keys", "Stop", KeyCode.K, "Host: stop the music for everyone.");
            VolumeDownKey = Config.Bind("Keys", "VolumeDown", KeyCode.Alpha1, "Jukebox volume down (tap = step, hold = ramp, double-tap = low tactical preset).");
            VolumeUpKey = Config.Bind("Keys", "VolumeUp", KeyCode.Alpha2, "Jukebox volume up (tap = step, hold = ramp, double-tap = max garage-rock preset).");
            _volumeLast = Volume.Value;

            NetworkingAPI.RegisterMessageType<JbVoteStartMessage>();
            NetworkingAPI.RegisterMessageType<JbBallotMessage>();
            NetworkingAPI.RegisterMessageType<JbTallyMessage>();
            NetworkingAPI.RegisterMessageType<JbPlayMessage>();
            NetworkingAPI.RegisterMessageType<JbRequestMessage>();
            NetworkingAPI.RegisterMessageType<JbSkipBallotMessage>();
            NetworkingAPI.RegisterMessageType<JbSkipTallyMessage>();

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
            SkipCount = SkipNeeded = 0;
            MySkipVote = false;
            Ballots.Clear();
            SkipBallots.Clear();
            _hostWasPlaying = false;
        }

        private void OnServerStageBegin(Stage stage)
        {
            if (!VoteOnStageStart.Value || MciPlayer.Busy || VoteOpen) return;
            HostStartVote();
        }

        // ================================================================== every frame

        private void PlayKnobClick()
        {
            try
            {
                CharacterBody b = LocalUserManager.GetFirstLocalUser()?.cachedBody;
                if (b && b.gameObject) Util.PlaySound("Play_UI_menuClick", b.gameObject);
            }
            catch { }
        }

        private void StepVolume(int delta, string tag = "")
        {
            int next = Mathf.Clamp(Volume.Value + delta, 0, MaxVolume);
            if (next != Volume.Value || !string.IsNullOrEmpty(tag))
            {
                Volume.Value = next;
                PlayKnobClick();
            }
            _volPillTag = !string.IsNullOrEmpty(tag)
                ? tag
                : (Volume.Value > 100 ? "GARAGE-ROCK BOOST" : (Volume.Value <= 35 ? "TACTICAL LOW" : "STANDARD GAIN"));
            _volPillUntil = Time.unscaledTime + 2.2f;
        }

        private void ToggleVolumePreset()
        {
            bool goHigh = Volume.Value < 90;
            Volume.Value = goHigh ? PresetGarageRock : PresetTacticalLow;
            PlayKnobClick();
            _volPillTag = goHigh ? "PRESET: MAX GARAGE-ROCK" : "PRESET: TACTICAL COMBAT LOW";
            _volPillUntil = Time.unscaledTime + 2.5f;
        }

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
                if (Input.GetKeyDown(SkipKey.Value)) CastSkipVote();
                if (Input.GetKeyDown(StopKey.Value)) StopPressed();

                KeyCode downK = VolumeDownKey.Value;
                KeyCode upK = VolumeUpKey.Value;
                bool downPressed = Input.GetKeyDown(downK);
                bool upPressed = Input.GetKeyDown(upK);
                bool downHeld = Input.GetKey(downK);
                bool upHeld = Input.GetKey(upK);

                // Simultaneous 1 + 2 press toggles between Max Garage-Rock and Low Tactical Combat preset
                if ((downPressed && upHeld) || (upPressed && downHeld))
                {
                    ToggleVolumePreset();
                    _volHoldTimer = -10f;
                }
                else if (downPressed)
                {
                    if (Time.unscaledTime - _lastTapDownTime < 0.26f)
                    {
                        Volume.Value = PresetTacticalLow;
                        PlayKnobClick();
                        _volPillTag = "PRESET: TACTICAL COMBAT LOW";
                        _volPillUntil = Time.unscaledTime + 2.5f;
                    }
                    else
                    {
                        StepVolume(-5);
                    }
                    _lastTapDownTime = Time.unscaledTime;
                    _volHoldTimer = 0f;
                    _volRampNext = Time.unscaledTime + 0.28f;
                }
                else if (upPressed)
                {
                    if (Time.unscaledTime - _lastTapUpTime < 0.26f)
                    {
                        Volume.Value = PresetGarageRock;
                        PlayKnobClick();
                        _volPillTag = "PRESET: MAX GARAGE-ROCK";
                        _volPillUntil = Time.unscaledTime + 2.5f;
                    }
                    else
                    {
                        StepVolume(5);
                    }
                    _lastTapUpTime = Time.unscaledTime;
                    _volHoldTimer = 0f;
                    _volRampNext = Time.unscaledTime + 0.28f;
                }
                else if ((downHeld ^ upHeld) && _volHoldTimer >= 0f)
                {
                    _volHoldTimer += Time.unscaledDeltaTime;
                    if (Time.unscaledTime >= _volRampNext)
                    {
                        _volRampNext = Time.unscaledTime + 0.065f;
                        StepVolume(upHeld ? 3 : -3);
                    }
                }
                else
                {
                    _volHoldTimer = 0f;
                }

                // Legacy Shift + -/= support
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift && Input.GetKeyDown(KeyCode.Equals)) StepVolume(10);
                if (shift && Input.GetKeyDown(KeyCode.Minus)) StepVolume(-10);
            }

            if (_volumeLast != Volume.Value)
            {
                _volumeLast = Volume.Value;
                MciPlayer.SetVolume(Volume.Value);
                if (MciPlayer.Busy) GameMusic.ApplyBoostDuck(Volume.Value);
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
            SkipBallots.Clear();
            var msg = new JbPlayMessage { Id = id ?? "", Title = title ?? "" };
            msg.Handle();
            try { msg.Send(NetworkDestination.Clients); } catch { }
            _hostWasPlaying = MciPlayer.Busy;
        }

        public static void HostReceiveSkip(uint voter)
        {
            if (!NetworkServer.active || NowPlayingId.Length == 0) return;
            SkipBallots.Add(voter);
            int players = Mathf.Max(1, NetworkUser.readOnlyInstancesList.Count);
            int needed = Mathf.Max(1, (players + 1) / 2);
            var tally = new JbSkipTallyMessage { Count = SkipBallots.Count, Needed = needed };
            tally.Handle();
            try { tally.Send(NetworkDestination.Clients); } catch { }

            if (SkipBallots.Count >= needed)
            {
                SkipBallots.Clear();
                if (!VoteOpen)
                {
                    _nextVoteAllowed = 0f;
                    HostStartVote();
                }
                else
                {
                    HostPlay("", "");
                }
            }
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

        private static void CastSkipVote()
        {
            if (NowPlayingId.Length == 0)
            {
                RequestVote();
                return;
            }
            MySkipVote = true;
            uint me = LocalVoterId();
            if (NetworkServer.active) HostReceiveSkip(me);
            else
            {
                try { new JbSkipBallotMessage { Voter = me }.Send(NetworkDestination.Server); } catch { }
                Toast("Voted to skip the current song.");
            }
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
            MySkipVote = false;
            SkipCount = SkipNeeded = 0;
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
                GameMusic.ApplyBoostDuck(Volume.Value);
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
            w = 270f * s;
            x = Screen.width - w - 22f * s;
            y = 225f * s;
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
                bool matchedWidth = false;

                // Match the Objective panel's exact left & right edges first (and step below all objective rows).
                if (_objPanel && _objPanel.gameObject.activeInHierarchy)
                {
                    if (ScreenRect(_objPanel.gameObject.transform as RectTransform, out Rect or) && or.width > 120f)
                    {
                        w = Mathf.Clamp(or.width, 220f * s, 420f * s);
                        x = Mathf.Clamp(or.xMax - w, 8f * s, Screen.width - w - 8f * s);
                        matchedWidth = true;
                        if (or.yMax < Screen.height * 0.75f) bottom = Mathf.Max(bottom, or.yMax);
                    }
                    RectTransform box = _objPanel.objectiveTrackerContainer;
                    if (box && box.gameObject.activeInHierarchy)
                    {
                        if (ScreenRect(box, out Rect br) && br.height > 2f && br.yMax < Screen.height * 0.75f)
                        {
                            if (!matchedWidth && br.width > 120f)
                            {
                                w = Mathf.Clamp(br.width, 220f * s, 420f * s);
                                x = Mathf.Clamp(br.xMax - w, 8f * s, Screen.width - w - 8f * s);
                                matchedWidth = true;
                            }
                            bottom = Mathf.Max(bottom, br.yMax);
                        }
                        for (int i = 0; i < box.childCount; i++)
                        {
                            RectTransform child = box.GetChild(i) as RectTransform;
                            if (child && child.gameObject.activeInHierarchy && ScreenRect(child, out Rect cr) && cr.yMax < Screen.height * 0.75f)
                                bottom = Mathf.Max(bottom, cr.yMax);
                        }
                    }
                }

                if (ScreenRect(_diffRt, out Rect dr) && dr.width > 80f)
                {
                    if (!matchedWidth)
                    {
                        w = Mathf.Clamp(dr.width, 220f * s, 420f * s);
                        x = Mathf.Clamp(dr.xMax - w, 8f * s, Screen.width - w - 8f * s);
                    }
                    bottom = Mathf.Max(bottom, dr.yMax);
                }

                if (bottom > 20f) y = bottom + 6f * s;
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

        // RoR2 HUD palette: icy steel-blue backdrop matching the Difficulty & Objective panel.
        private static readonly Color BgDark = new Color(0.28f, 0.36f, 0.44f, 0.86f);
        private static readonly Color HeaderBand = new Color(0.20f, 0.27f, 0.34f, 0.94f);
        private static readonly Color StripBg = new Color(0.14f, 0.19f, 0.25f, 0.85f);
        private static readonly Color StripHover = new Color(0.22f, 0.30f, 0.38f, 0.94f);
        private static readonly Color StripMine = new Color(0.28f, 0.27f, 0.16f, 0.94f);
        private static readonly Color BorderCol = new Color(0.68f, 0.80f, 0.90f, 0.58f);
        private static readonly Color Gold = new Color(1.00f, 0.84f, 0.32f, 1f);
        private static readonly Color Cyan = new Color(0.56f, 0.86f, 1.00f, 1f);
        private static readonly Color TextBright = new Color(0.96f, 0.98f, 1.00f, 1f);
        private static readonly Color TextMuted = new Color(0.78f, 0.86f, 0.93f, 1f);
        private static readonly Color TextWarn = new Color(1f, 0.58f, 0.52f, 1f);

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
            bool paused = false;
            try { paused = PauseScreenController.pauseScreenUp || PauseScreenController.instancesList.Count > 0; } catch { }
            // Hide while the scoreboard (Tab) is held unless the pause menu is open so it never clashes with the inventory overlay.
            if (!paused && _hud && _hud.scoreboardPanel && _hud.scoreboardPanel.activeInHierarchy) return;

            float s = Mathf.Max(0.65f, Screen.height / 1080f);
            InitStyles(s);
            PlaceBelowDifficulty(s, out float x, out float y, out float w);

            Event ev = Event.current;
            bool canClick = Cursor.visible || paused;
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
            ShadowLabel(new Rect(hdr.x + 9f * s, hdr.y, hdr.width * 0.45f, hdr.height), "♫ JUKEBOX", _hdrStyle, Gold);

            // Action chips on the right of the header ([N] SKIP, [J] VOTE, [K] STOP)
            float chipH = hdrH - 4f * s;
            float rightX = hdr.xMax - 4f * s;
            if (NowPlayingId.Length > 0 && NetworkServer.active)
            {
                float stopW = 54f * s;
                rightX -= stopW;
                if (ChipButton(new Rect(rightX, hdr.y + 2f * s, stopW, chipH), "[" + KeyName(StopKey.Value) + "] STOP", TextWarn, canClick, ev))
                    StopPressed();
                rightX -= 3f * s;
            }
            if (NowPlayingId.Length > 0)
            {
                string skipLabel = SkipCount > 0 && SkipNeeded > 1
                    ? "[" + KeyName(SkipKey.Value) + "] SKIP " + SkipCount + "/" + SkipNeeded
                    : "[" + KeyName(SkipKey.Value) + "] SKIP";
                float skipW = (SkipCount > 0 && SkipNeeded > 1 ? 74f : 56f) * s;
                rightX -= skipW;
                if (ChipButton(new Rect(rightX, hdr.y + 2f * s, skipW, chipH), skipLabel, MySkipVote ? Gold : Cyan, canClick, ev))
                    CastSkipVote();
                rightX -= 3f * s;
            }
            else if (!VoteOpen)
            {
                float voteW = 58f * s;
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

            // ---- 4. In-game Volume Slider + [1]/[2] stepper & Preset Toggle ----
            Rect volRow = new Rect(innerX, cy, innerW, volH);
            float lblW = 48f * s;
            float btnW = 19f * s;
            float presetW = 36f * s;
            float valW = 36f * s;
            string kDown = KeyName(VolumeDownKey.Value);
            string kUp = KeyName(VolumeUpKey.Value);
            ShadowLabel(new Rect(volRow.x + 1f * s, volRow.y, lblW, volRow.height), "VOL " + kDown + "/" + kUp, _chipStyle, TextMuted);

            Rect minusRect = new Rect(volRow.x + lblW + 2f * s, volRow.y + 3f * s, btnW, volRow.height - 6f * s);
            if (ChipButton(minusRect, "−", TextBright, canClick, ev))
                StepVolume(-5);

            Rect trackHit = new Rect(minusRect.xMax + 5f * s, volRow.y + 2f * s,
                volRow.width - lblW - btnW * 2f - presetW - valW - 22f * s, volRow.height - 4f * s);
            Rect trackBar = new Rect(trackHit.x, volRow.y + (volRow.height - 6f * s) * 0.5f, trackHit.width, 6f * s);

            if (canClick && ev.type == EventType.MouseDown && ev.button == 0 && trackHit.Contains(ev.mousePosition))
            {
                _draggingVolume = true;
                Volume.Value = Mathf.RoundToInt(Mathf.Clamp01((ev.mousePosition.x - trackBar.x) / trackBar.width) * MaxVolume);
                ev.Use();
            }
            else if (_draggingVolume && ev.type == EventType.MouseDrag)
            {
                Volume.Value = Mathf.RoundToInt(Mathf.Clamp01((ev.mousePosition.x - trackBar.x) / trackBar.width) * MaxVolume);
                ev.Use();
            }
            if (canClick && ev.type == EventType.ScrollWheel && volRow.Contains(ev.mousePosition))
            {
                int step = ev.delta.y < 0f ? 5 : -5;
                StepVolume(step);
                ev.Use();
            }

            float volFrac = Mathf.Clamp01(Volume.Value / (float)MaxVolume);
            float stdMark = 100f / MaxVolume;
            bool isBoosted = Volume.Value > 100;
            bool trackHov = canClick && (trackHit.Contains(ev.mousePosition) || _draggingVolume);
            Fill(trackBar, new Color(0.02f, 0.03f, 0.05f, 0.85f));
            Border(trackBar, trackHov ? Gold : BorderCol, 1f);
            if (volFrac > 0f)
                Fill(new Rect(trackBar.x + 1f, trackBar.y + 1f, (trackBar.width - 2f) * volFrac, trackBar.height - 2f),
                    isBoosted ? Gold : (trackHov ? Gold : Cyan));
            // 100% nominal gain tick mark on the 0..150% bar
            Fill(new Rect(trackBar.x + trackBar.width * stdMark, trackBar.y - 1f, 1.5f * s, trackBar.height + 2f), new Color(1f, 1f, 1f, 0.45f));

            // Slider thumb
            float thumbW = 6f * s, thumbH = 12f * s;
            float thumbX = trackBar.x + trackBar.width * volFrac - thumbW * 0.5f;
            Rect thumb = new Rect(thumbX, volRow.y + (volRow.height - thumbH) * 0.5f, thumbW, thumbH);
            Fill(thumb, isBoosted || trackHov ? Gold : TextBright);
            Border(thumb, new Color(0f, 0f, 0f, 0.7f), 1f);

            Rect plusRect = new Rect(trackHit.xMax + 5f * s, volRow.y + 3f * s, btnW, volRow.height - 6f * s);
            if (ChipButton(plusRect, "+", TextBright, canClick, ev))
                StepVolume(5);

            Rect presetRect = new Rect(plusRect.xMax + 4f * s, volRow.y + 3f * s, presetW, volRow.height - 6f * s);
            if (ChipButton(presetRect, Volume.Value >= 90 ? "MAX" : "LOW", Volume.Value >= 90 ? Gold : Cyan, canClick, ev))
                ToggleVolumePreset();

            ShadowLabel(new Rect(presetRect.xMax + 2f * s, volRow.y, valW, volRow.height), Volume.Value + "%", _rightStyle,
                Volume.Value == 0 ? TextWarn : (isBoosted ? Gold : TextBright));
            cy += volH;

            // ---- 5. Toast notification line (if active) ----
            if (Time.unscaledTime < _toastUntil)
            {
                ShadowLabel(new Rect(innerX + 2f * s, cy, innerW - 4f * s, toastH), _toast, _subStyle, Gold);
            }

            // ---- 6. Temporary On-Screen HUD Volume Pill (when tapping/holding 1 or 2) ----
            if (Time.unscaledTime < _volPillUntil)
            {
                float pillAlpha = Mathf.Clamp01((_volPillUntil - Time.unscaledTime) * 2.2f);
                float pw = 290f * s, ph = 42f * s;
                float px = Screen.width * 0.5f - pw * 0.5f, py = 46f * s;
                Rect pill = new Rect(px, py, pw, ph);
                Fill(pill, new Color(0.10f, 0.14f, 0.19f, 0.92f * pillAlpha));
                Border(pill, isBoosted ? new Color(Gold.r, Gold.g, Gold.b, pillAlpha) : new Color(Cyan.r, Cyan.g, Cyan.b, pillAlpha), 1.5f);
                Fill(new Rect(pill.x, pill.y, 4f * s, pill.height), isBoosted ? Gold : Cyan);

                string pillHdr = "♫ JUKEBOX VOL: " + Volume.Value + "%  ·  " + _volPillTag;
                ShadowLabel(new Rect(pill.x + 10f * s, pill.y + 3f * s, pill.width - 16f * s, 18f * s), pillHdr, _hdrStyle, isBoosted ? Gold : TextBright);

                // Segmented LED gain bar inside the pill
                int segs = 15;
                float segAreaW = pill.width - 20f * s;
                float segW = (segAreaW - (segs - 1) * 2f * s) / segs;
                int litSegs = Mathf.RoundToInt((Volume.Value / (float)MaxVolume) * segs);
                for (int k = 0; k < segs; k++)
                {
                    Rect sr = new Rect(pill.x + 10f * s + k * (segW + 2f * s), pill.y + 24f * s, segW, 10f * s);
                    bool lit = k < litSegs;
                    Color sc = k >= 10 ? Gold : Cyan;
                    Fill(sr, lit ? new Color(sc.r, sc.g, sc.b, 0.95f * pillAlpha) : new Color(0.03f, 0.05f, 0.08f, 0.7f * pillAlpha));
                }
            }
        }

        private static string KeyName(KeyCode k)
        {
            string n = k.ToString();
            return n.StartsWith("Alpha") ? n.Substring(5) : n.StartsWith("Keypad") ? "Num" + n.Substring(6) : n;
        }
    }

    /// <summary>Turns the game's Wwise music down while a jukebox song plays, and gently ducks heavy combat SFX in Garage-Rock Boost mode (100-150%).</summary>
    public static class GameMusic
    {
        private static bool _muted;
        private static bool _sfxDucked;
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

        private static void SetSfxVolume(float value)
        {
            try { SetRtpc()?.Invoke(null, new object[] { "Volume_SFX", value }); }
            catch { }
        }

        public static void Mute()
        {
            SetMusicVolume(0f);
            _muted = true;
        }

        public static void ApplyBoostDuck(int jukeboxVolume)
        {
            if (jukeboxVolume > 100)
            {
                float userSfx = PlayerConVarVolume("volume_sfx", 80f);
                float boostT = Mathf.Clamp01((jukeboxVolume - 100f) / 50f);
                // Duck game SFX down to 42% of player setting at 150% Garage-Rock Boost so the song punches right through gunfire
                SetSfxVolume(userSfx * Mathf.Lerp(0.85f, 0.42f, boostT));
                _sfxDucked = true;
            }
            else if (_sfxDucked)
            {
                _sfxDucked = false;
                SetSfxVolume(PlayerConVarVolume("volume_sfx", 80f));
            }
        }

        public static void Restore()
        {
            if (_muted)
            {
                _muted = false;
                SetMusicVolume(PlayerConVarVolume("volume_music", 50f));
            }
            if (_sfxDucked)
            {
                _sfxDucked = false;
                SetSfxVolume(PlayerConVarVolume("volume_sfx", 80f));
            }
        }

        private static float PlayerConVarVolume(string conVarName, float fallback)
        {
            try
            {
                object console = RoR2.Console.instance;
                MethodInfo find = console?.GetType().GetMethod("FindConVar", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object conVar = find?.Invoke(console, new object[] { conVarName });
                string text = conVar?.GetType().GetMethod("GetString")?.Invoke(conVar, null) as string;
                if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            }
            catch { }
            return fallback;
        }
    }
}

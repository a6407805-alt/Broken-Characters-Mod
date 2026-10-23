// BCM Dev Tools: a test bench for making mods. Ships with the pack for everyone, but SINGLEPLAYER ONLY:
// in any multiplayer lobby or run (host or client) every tool is off and the panel is hidden.
//
//  - QUICK TEST: the launcher's TEST LEVEL button (or RELAUNCH below) writes %APPDATA%\BrokenCharacters\quicktest.flag.
//    At startup we read + delete it, skip the intro cutscene, start a singleplayer Simulacrum (or Classic) run with the
//    last-used survivor (or the one saved with "Use this survivor") and turn enemy waves off: a quiet arena.
//  - PANEL (F2): live stats, skills + cooldowns, entity states, buffs, damage log + DPS, and test buttons.
//    Buttons are clickable while the pause menu (Esc) is open; every button also has a hotkey.
//  - Safe with ScriptEngine hot reload (F6): everything is undone in OnDestroy.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Path = System.IO.Path;

namespace BCMDevTools
{
    [BepInPlugin(Guid, "BCM Dev Tools", "1.0.0")]
    public partial class DevToolsPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.a6407805.bcmdevtools";

        static readonly string SharedDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters");
        static readonly string MarkerFile = Path.Combine(SharedDir, "devtools-installed.txt");
        static readonly string FlagFile = Path.Combine(SharedDir, "quicktest.flag");

        // ---------------------------------------------------------------- config
        ConfigEntry<string> _mode;        // Simulacrum or Classic
        ConfigEntry<string> _survivor;    // body name, empty = last used
        ConfigEntry<KeyCode> _panelKey;
        ConfigEntry<bool> _flatWorld;     // quick test teleports you onto a big flat grid floor

        // ---------------------------------------------------------------- state
        enum Quick { Off, WaitTitle, WaitLobby }
        Quick _quick = Quick.Off;
        float _quickTimer;

        static bool _spawnsOff;           // read by the Harmony patch
        bool _godMode, _panelOpen = true, _dummyAi, _mouseMode, _flatPending;
        GameObject _flat;                 // the flat test floor (destroyed with the stage)
        object _cursorSystem;             // the MPEventSystem we opened the cursor on
        int _spawnPick = 2;
        Harmony _harmony;
        Rect _win = new Rect(Screen.width - 490, 60, 470, 10);

        static readonly string[] SpawnNames = { "Beetle", "Lemurian", "Stone Golem", "Elder Lemurian", "Stone Titan" };
        static readonly string[] SpawnMasters = { "BeetleMaster", "LemurianMaster", "GolemMaster", "LemurianBruiserMaster", "TitanMaster" };

        struct Hit { public float time, amount; public bool crit; public string victim; public bool taken; }
        readonly List<Hit> _hits = new List<Hit>();

        // ================================================================ lifecycle
        void Awake()
        {
            _mode = Config.Bind("Quick Test", "Game mode", "Classic", "Classic (normal run, waves off) or Simulacrum (needs Survivors of the Void).");
            _flatWorld = Config.Bind("Quick Test", "Flat world", true, "Quick test puts you on a big flat grid floor high above the stage.");
            _survivor = Config.Bind("Quick Test", "Survivor", "", "Body name to test with, e.g. CommandoBody. Empty = last used.");
            _panelKey = Config.Bind("Panel", "Toggle key", KeyCode.F2, "Shows/hides the dev panel.");

            try
            {
                Directory.CreateDirectory(SharedDir);
                File.WriteAllText(MarkerFile, "BCM Dev Tools " + DateTime.Now.ToString("o"));  // shows TEST LEVEL in the launcher
                if (File.Exists(FlagFile)) { File.Delete(FlagFile); _quick = Quick.WaitTitle; Logger.LogInfo("Quick test requested"); }
            }
            catch (Exception e) { Logger.LogWarning("Shared files: " + e.Message); }

            SceneManager.activeSceneChanged += OnSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            GlobalEventManager.onServerDamageDealt += OnDamage;
            Stage.onServerStageBegin += OnStageBegin;

            _harmony = new Harmony(Guid);
            MethodInfo wave = AccessTools.Method(typeof(InfiniteTowerRun), "BeginNextWave");
            if (wave != null) _harmony.Patch(wave, prefix: new HarmonyMethod(typeof(DevToolsPlugin), nameof(BlockWave)));
            else Logger.LogWarning("InfiniteTowerRun.BeginNextWave not found: Simulacrum waves can't be paused");
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GlobalEventManager.onServerDamageDealt -= OnDamage;
            Stage.onServerStageBegin -= OnStageBegin;
            _harmony?.UnpatchSelf();
            SetMouseMode(false);
            if (_flat) Destroy(_flat);
        }

        /// <summary>
        /// The dev tools only work alone: in the menus, or in a singleplayer run. Hosting or joining a multiplayer
        /// lobby/run turns everything off (no god mode, no spawning, waves normal, panel hidden).
        /// </summary>
        static bool Allowed
        {
            get
            {
                if (!NetworkServer.active && !NetworkClient.active) return true;   // main menu, nothing online
                return RoR2Application.isInSinglePlayer && NetworkUser.readOnlyInstancesList.Count <= 1;
            }
        }

        static bool BlockWave() => !(_spawnsOff && Allowed);   // false = skip the original (no new wave)

        // ================================================================ quick test
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Skip the intro cutscene the moment it finishes loading, before its first frame plays.
            if (_quick != Quick.Off) Logger.LogInfo("Scene loaded: " + scene.name);
            if (_quick == Quick.WaitTitle && scene.name == "intro") { Logger.LogInfo("Skipping intro"); _introTries = 0; SceneManager.LoadScene("title"); }
        }

        void OnSceneChanged(Scene from, Scene to)
        {
            if (_quick == Quick.Off) return;
            if (to.name == "intro") { SceneManager.LoadScene("title"); return; }
            _quickTimer = 0f;
        }

        int _introTries;
        readonly HashSet<string> _introLogged = new HashSet<string>();

        /// <summary>
        /// Still in the intro ship cutscene: do what pressing Space does. Calls any skip method the intro's own scripts
        /// have, jumps videos to their end, and logs what it found (so it can be pinned down if a patch renames it).
        /// </summary>
        void SkipIntroNow()
        {
            if (_quick != Quick.WaitTitle || SceneManager.GetActiveScene().name != "intro" || _introTries++ > 300) return;
            foreach (MonoBehaviour mb in FindObjectsOfType<MonoBehaviour>())
            {
                if (!mb) continue;
                Type t = mb.GetType();
                if (_introLogged.Add(t.FullName)) Logger.LogInfo("Intro has: " + t.FullName);
                if (t.Assembly != typeof(RoR2Application).Assembly) continue;
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.GetParameters().Length != 0 || m.ReturnType != typeof(void)) continue;
                    if (m.Name.IndexOf("skip", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try { Logger.LogInfo("Intro skip: " + t.Name + "." + m.Name); m.Invoke(mb, null); }
                    catch (Exception e) { Logger.LogWarning("Intro skip " + m.Name + ": " + (e.InnerException ?? e).Message); }
                }
            }
            foreach (UnityEngine.Video.VideoPlayer vp in FindObjectsOfType<UnityEngine.Video.VideoPlayer>())
                if (vp && vp.length > 1) vp.time = vp.length - 0.1;
        }

        /// <summary>Quick test only: run the logo screens at 30x speed until the title menu is up.</summary>
        void FastBoot()
        {
            string scene = SceneManager.GetActiveScene().name;
            bool booting = _quick == Quick.WaitTitle && (scene == "splash" || scene == "intro");   // never in a run
            if (booting) { Time.timeScale = 30f; _fastBoot = true; }
            else if (_fastBoot) { Time.timeScale = 1f; _fastBoot = false; }
        }
        bool _fastBoot;

        void QuickTestUpdate()
        {
            if (_quick == Quick.Off) return;
            _quickTimer += Time.unscaledDeltaTime;
            try
            {
                if (_quick == Quick.WaitTitle && SceneManager.GetActiveScene().name == "title" && RoR2.Console.instance != null && _quickTimer > 0.3f)
                {
                    string mode = _mode.Value.Trim().ToLowerInvariant() == "classic" ? "ClassicRun" : "InfiniteTowerRun";
                    Logger.LogInfo("Quick test: hosting " + mode);
                    RoR2.Console.instance.SubmitCmd(null, "transition_command \"gamemode " + mode + "; host 0;\"");
                    _quick = Quick.WaitLobby; _quickTimer = 0f;
                }
                else if (_quick == Quick.WaitLobby && PreGameController.instance != null && _quickTimer > 0.5f)
                {
                    NetworkUser user = LocalUserManager.GetFirstLocalUser()?.currentNetworkUser;
                    if (user == null) return;
                    if (!string.IsNullOrWhiteSpace(_survivor.Value))
                    {
                        BodyIndex body = BodyCatalog.FindBodyIndex(_survivor.Value.Trim());
                        if (body != BodyIndex.None) user.CmdSetBodyPreference(body);
                        else Logger.LogWarning("Survivor not found: " + _survivor.Value);
                    }
                    _quick = Quick.Off;
                    _spawnsOff = true;   // start in a quiet arena
                    _godMode = true;
                    _flatPending = _flatWorld.Value;   // the run starts when you press Ready
                    Logger.LogInfo("Quick test: at character select");
                }
            }
            catch (Exception e) { Logger.LogError("Quick test failed: " + e); _quick = Quick.Off; }
        }

        void OnStageBegin(Stage stage)
        {
            ApplySpawns();
            if (_flatPending) { _flatPending = false; StartCoroutine(GoFlatWhenSpawned()); }
        }

        // ================================================================ flat world
        System.Collections.IEnumerator GoFlatWhenSpawned()
        {
            for (float t = 0; t < 30f && !Me; t += 0.1f) yield return new WaitForSeconds(0.1f);
            yield return new WaitForSeconds(0.2f);
            CharacterBody me = Me;
            if (me && me.currentVehicle) { try { me.currentVehicle.EjectPassenger(me.gameObject); } catch (Exception e) { Logger.LogWarning("Pod: " + e.Message); } }
            yield return new WaitForSeconds(0.2f);
            GoFlat();
        }

        /// <summary>Builds a 400 m flat grid floor (10 m squares) high above the stage and puts you on it.</summary>
        void GoFlat()
        {
            CharacterBody me = Me;
            if (!me) return;
            try
            {
                if (!_flat)
                {
                    _flat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _flat.name = "BCMDevTools_FlatWorld";
                    _flat.layer = LayerIndex.world.intVal;
                    _flat.transform.position = me.footPosition + Vector3.up * 600f;
                    _flat.transform.localScale = new Vector3(400f, 2f, 400f);
                    Renderer r = _flat.GetComponent<Renderer>();
                    Shader sh = Shader.Find("Hopoo Games/Deferred/Standard");
                    if (!sh) sh = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard");
                    if (sh)
                    {
                        var mat = new Material(sh) { mainTexture = GridTexture() };
                        mat.mainTextureScale = new Vector2(40f, 40f);   // 400 m / 40 = 10 m squares
                        mat.color = Color.white;
                        r.sharedMaterial = mat;
                    }
                }
                Vector3 top = _flat.transform.position + Vector3.up * 1.5f;
                TeleportHelper.TeleportBody(me, top);
                _flatTop = top.y;
                Logger.LogInfo("Flat world ready at " + top);
            }
            catch (Exception e) { Logger.LogError("Flat world failed: " + e); }
        }

        float _flatTop = float.NaN;

        static Texture2D GridTexture()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool line = x < 3 || y < 3;
                    bool half = x == n / 2 || y == n / 2;      // 5 m lines
                    px[y * n + x] = line ? new Color(0.15f, 0.15f, 0.18f) : half ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.62f, 0.62f, 0.66f);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        void KeepOnFlat()
        {
            CharacterBody me = Me;
            if (!_flat || !me || float.IsNaN(_flatTop)) return;
            if (me.footPosition.y < _flatTop - 40f) TeleportHelper.TeleportBody(me, _flat.transform.position + Vector3.up * 1.5f);
        }

        // ================================================================ mouse mode
        /// <summary>
        /// Frees the mouse so the panel can be clicked. Does what the game's own menus do (bump the event system's
        /// cursor-opener count), which shows the cursor and stops the camera and skills from taking the mouse.
        /// </summary>
        void SetMouseMode(bool on)
        {
            if (on == _mouseMode) return;
            try
            {
                object es = on ? (object)LocalUserManager.GetFirstLocalUser()?.eventSystem : _cursorSystem;
                if (es != null)
                {
                    Traverse tr = Traverse.Create(es);
                    Traverse count = tr.Property("cursorOpenerCount");
                    if (!count.PropertyExists()) count = tr.Field("cursorOpenerCount");
                    if (count.FieldExists() || count.PropertyExists())
                        count.SetValue(Math.Max(0, count.GetValue<int>() + (on ? 1 : -1)));
                    else Logger.LogWarning("cursorOpenerCount not found; using the plain Unity cursor");
                }
                _cursorSystem = on ? es : null;
            }
            catch (Exception e) { Logger.LogWarning("Mouse mode: " + e.Message); }
            _mouseMode = on;
        }

        void ApplySpawns()
        {
            if (!NetworkServer.active || !Allowed) return;
            foreach (CombatDirector d in FindObjectsOfType<CombatDirector>()) d.enabled = !_spawnsOff;
        }

        // ================================================================ actions
        static CharacterBody Me => LocalUserManager.GetFirstLocalUser()?.cachedBody;

        void Update()
        {
            FastBoot();
            SkipIntroNow();
            QuickTestUpdate();

            if (!Allowed)
            {
                // Multiplayer: drop every cheat and ignore the hotkeys.
                _godMode = false; _spawnsOff = false; _flatPending = false;
                SetMouseMode(false);
                if (_flat) { Destroy(_flat); _flatTop = float.NaN; }
                return;
            }

            if (Input.GetKeyDown(_panelKey.Value)) { _panelOpen = !_panelOpen; if (!_panelOpen) SetMouseMode(false); }
            if (Input.GetKeyDown(KeyCode.F1)) { if (!_panelOpen) _panelOpen = true; SetMouseMode(!_mouseMode); }
            if (Input.GetKeyDown(KeyCode.F10)) GoFlat();
            KeepOnFlat();
            if (Input.GetKeyDown(KeyCode.F3)) _godMode = !_godMode;
            if (Input.GetKeyDown(KeyCode.F4)) ToggleSpawns();
            if (Input.GetKeyDown(KeyCode.F5)) SpawnEnemy();
            if (Input.GetKeyDown(KeyCode.F7)) KillAll();
            if (Input.GetKeyDown(KeyCode.F8)) ResetCooldowns();
            if (Input.GetKeyDown(KeyCode.F9)) Relaunch();

            CharacterBody me = Me;
            if (me && me.healthComponent && NetworkServer.active) me.healthComponent.godMode = _godMode;

            _hits.RemoveAll(h => Time.time - h.time > 30f);
        }

        void LateUpdate()
        {
            if (!_mouseMode) return;
            Cursor.lockState = CursorLockMode.None;   // backup in case the game re-locks it
            Cursor.visible = true;
        }

        void ToggleSpawns() { _spawnsOff = !_spawnsOff; ApplySpawns(); }

        void SpawnEnemy()
        {
            CharacterBody me = Me;
            if (!me || !NetworkServer.active) return;
            try
            {
                GameObject prefab = MasterCatalog.FindMasterPrefab(SpawnMasters[_spawnPick]);
                if (!prefab) { Logger.LogWarning("No master " + SpawnMasters[_spawnPick]); return; }
                Vector3 fwd = me.inputBank ? me.inputBank.aimDirection : me.gameObject.transform.forward;
                fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
                CharacterMaster m = new MasterSummon
                {
                    masterPrefab = prefab,
                    position = me.footPosition + fwd * 15f + Vector3.up,
                    rotation = Quaternion.LookRotation(-fwd),
                    summonerBodyObject = null,
                    teamIndexOverride = TeamIndex.Monster,
                    ignoreTeamMemberLimit = true
                }.Perform();
                if (m && !_dummyAi) foreach (BaseAI ai in m.GetComponents<BaseAI>()) ai.enabled = false;  // a standing target
            }
            catch (Exception e) { Logger.LogError("Spawn failed: " + e); }
        }

        void KillAll()
        {
            if (!NetworkServer.active) return;
            foreach (TeamComponent t in TeamComponent.GetTeamMembers(TeamIndex.Monster).ToList())
                if (t && t.body && t.body.healthComponent) t.body.healthComponent.Suicide();
        }

        void ResetCooldowns() { CharacterBody me = Me; if (me && me.skillLocator) me.skillLocator.ResetSkills(); }

        void FullHeal()
        {
            CharacterBody me = Me;
            if (me && me.healthComponent && NetworkServer.active) me.healthComponent.HealFraction(1f, default(ProcChainMask));
        }

        void NewTestRun()
        {
            _quick = Quick.WaitTitle; _quickTimer = 0f;
            RoR2.Console.instance?.SubmitCmd(null, "disconnect");   // back to the title, then the quick test starts again
        }

        /// <summary>Restart the whole game (picks up new dlls in plugins\) and come back into the test arena.</summary>
        void Relaunch()
        {
            try
            {
                Directory.CreateDirectory(SharedDir);
                File.WriteAllText(FlagFile, DateTime.Now.ToString("o"));
                // Wait for this game process to close, then ask Steam to start it again.
                Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 5 /nobreak >nul & start \"\" steam://rungameid/632360")
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
                Application.Quit();
            }
            catch (Exception e) { Logger.LogError("Relaunch failed: " + e); }
        }

        int _swapPick = -1;
        static List<SurvivorDef> Survivors() =>
            SurvivorCatalog.orderedSurvivorDefs.Where(d => d && d.bodyPrefab && !d.hidden).ToList();

        /// <summary>Respawns you on the spot as another survivor (singleplayer host only).</summary>
        void SwapTo(SurvivorDef def)
        {
            CharacterBody me = Me;
            if (!me || !me.master || !def || !NetworkServer.active) return;
            try
            {
                Vector3 pos = me.footPosition;
                Quaternion rot = me.gameObject.transform.rotation;
                CharacterMaster master = me.master;
                master.bodyPrefab = def.bodyPrefab;
                master.Respawn(pos, rot);
                master.GetComponent<PlayerCharacterMasterController>()?.networkUser?.CmdSetBodyPreference(BodyCatalog.FindBodyIndex(def.bodyPrefab));
                Logger.LogInfo("Swapped to " + def.bodyPrefab.name);
            }
            catch (Exception e) { Logger.LogError("Swap failed: " + e); }
        }

        void UseThisSurvivor()
        {
            CharacterBody me = Me;
            if (!me) return;
            _survivor.Value = BodyCatalog.GetBodyName(me.bodyIndex);
            Config.Save();
        }

        // ================================================================ damage log
        void OnDamage(DamageReport r)
        {
            try
            {
                CharacterBody me = Me;
                if (!me || r == null) return;
                bool mine = r.attackerBody == me ||
                            (r.attackerMaster && r.attackerMaster.minionOwnership && r.attackerMaster.minionOwnership.ownerMaster == me.master);
                bool taken = r.victimBody == me;
                if (!mine && !taken) return;
                _hits.Add(new Hit
                {
                    time = Time.time, amount = r.damageDealt, crit = r.damageInfo != null && r.damageInfo.crit, taken = taken,
                    victim = taken ? (r.attackerBody ? Language.GetString(r.attackerBody.baseNameToken) : "?")
                                   : (r.victimBody ? Language.GetString(r.victimBody.baseNameToken) : "?")
                });
            }
            catch (Exception e) { Logger.LogWarning("Damage log: " + e.Message); }
        }

        // ================================================================ panel
        GUIStyle _label;

        void OnGUI()
        {
            if (!_panelOpen || !Allowed) return;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, wordWrap = true };
            _win = GUILayout.Window(0x8CD7, _win, DrawPanel, _mouseMode ? "BCM DEV TOOLS   ·   F1 = back to game" : "BCM DEV TOOLS   ·   F1 = mouse   F2 = hide", GUILayout.Width(470));
        }

        int _tab;
        static readonly string[] Tabs = { "STATS", "ITEMS", "SPAWN", "WORLD" };

        void DrawPanel(int id)
        {
            CharacterBody me = Me;
            _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(28));
            GUILayout.Space(4);
            switch (_tab)
            {
                case 1: DrawItems(me); break;
                case 2: DrawSpawn(me); break;
                case 3: DrawWorld(me); break;
                default: DrawStats(me); break;
            }
            GUI.DragWindow();
        }

        void DrawStats(CharacterBody me)
        {
            var sb = new StringBuilder();

            if (!me) sb.Append("<color=#aaaaaa>No character yet. Press TEST LEVEL in the launcher,\nor</color> <b>NEW TEST RUN</b> below.");
            else
            {
                HealthComponent hc = me.healthComponent;
                sb.Append($"<b><size=15>{Language.GetString(me.baseNameToken)}</size></b>  <color=#aaaaaa>{BodyCatalog.GetBodyName(me.bodyIndex)} · Lv {me.level:0}</color>\n");
                if (hc) sb.Append($"<color=#7CFC9A>HP {hc.health:0}/{hc.fullHealth:0}</color>  <color=#7fb2ff>Shield {hc.shield:0}</color>  Regen {me.regen:0.0}/s\n");
                sb.Append($"Damage <b>{me.damage:0.0}</b>   Atk speed <b>{me.attackSpeed:0.00}</b>   Crit <b>{me.crit:0}%</b>\n");
                sb.Append($"Armor <b>{me.armor:0}</b>   Move speed <b>{me.moveSpeed:0.0}</b>\n\n");

                sb.Append("<b>SKILLS</b>\n");
                if (me.skillLocator)
                {
                    Skill(sb, "M1", me.skillLocator.primary); Skill(sb, "M2", me.skillLocator.secondary);
                    Skill(sb, "Shift", me.skillLocator.utility); Skill(sb, "R", me.skillLocator.special);
                }

                sb.Append("\n<b>STATES</b>\n");
                foreach (EntityStateMachine esm in me.GetComponents<EntityStateMachine>())
                    sb.Append($"  {esm.customName}: <color=#FFC928>{(esm.state != null ? esm.state.GetType().Name : "-")}</color>\n");

                var buffs = new List<string>();
                for (int i = 0; i < BuffCatalog.buffCount; i++)
                {
                    int n = me.GetBuffCount((BuffIndex)i);
                    if (n > 0) { BuffDef d = BuffCatalog.GetBuffDef((BuffIndex)i); buffs.Add((d ? d.name.Replace("bd", "") : "#" + i) + (n > 1 ? " x" + n : "")); }
                }
                sb.Append("\n<b>BUFFS</b>  " + (buffs.Count > 0 ? string.Join(", ", buffs) : "<color=#aaaaaa>none</color>") + "\n");

                float dealt5 = _hits.Where(h => !h.taken && Time.time - h.time <= 5f).Sum(h => h.amount);
                sb.Append($"\n<b>DAMAGE</b>  DPS (5s): <b><color=#FFC928>{dealt5 / 5f:0}</color></b>\n");
                foreach (Hit h in Enumerable.Reverse(_hits).Take(8))
                    sb.Append(h.taken
                        ? $"  <color=#ff7070>-{h.amount:0} from {h.victim}</color>\n"
                        : $"  {(h.crit ? "<color=#FFC928>" : "<color=#ffffff>")}{h.amount:0}{(h.crit ? " CRIT" : "")}</color> → {h.victim}\n");
            }
            GUILayout.Label(sb.ToString(), _label);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_godMode ? "God: ON" : "God: off") + " [F3]")) _godMode = !_godMode;
            if (GUILayout.Button((_spawnsOff ? "Waves: OFF" : "Waves: on") + " [F4]")) ToggleSpawns();
            if (GUILayout.Button("Full heal")) FullHeal();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset cooldowns [F8]")) ResetCooldowns();
            if (GUILayout.Button("Clear damage log")) _hits.Clear();
            GUILayout.EndHorizontal();

            List<SurvivorDef> list = Survivors();
            if (list.Count > 0)
            {
                if (_swapPick < 0 || _swapPick >= list.Count)
                    _swapPick = me ? Math.Max(0, list.FindIndex(d => BodyCatalog.FindBodyIndex(d.bodyPrefab) == me.bodyIndex)) : 0;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(26))) _swapPick = (_swapPick + list.Count - 1) % list.Count;
                GUILayout.Label("<b>" + Language.GetString(list[_swapPick].displayNameToken) + "</b>", _label, GUILayout.Width(170));
                if (GUILayout.Button(">", GUILayout.Width(26))) _swapPick = (_swapPick + 1) % list.Count;
                if (GUILayout.Button("SWAP CHARACTER")) SwapTo(list[_swapPick]);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use this survivor")) UseThisSurvivor();
            if (GUILayout.Button("NEW TEST RUN")) NewTestRun();
            if (GUILayout.Button("RELAUNCH GAME [F9]")) Relaunch();
            GUILayout.EndHorizontal();

            GUILayout.Label($"<color=#aaaaaa>Test survivor: {(string.IsNullOrWhiteSpace(_survivor.Value) ? "last used" : _survivor.Value)} · Mode: {_mode.Value} · F6 = hot reload (scripts\\)</color>", _label);
        }

        static void Skill(StringBuilder sb, string key, GenericSkill s)
        {
            if (!s || s.skillDef == null) return;
            string name = Language.GetString(s.skillDef.skillNameToken);
            string cd = s.cooldownRemaining > 0.05f && s.stock < s.maxStock ? $"<color=#ff9d5c>{s.cooldownRemaining:0.0}s</color>" : "<color=#7CFC9A>ready</color>";
            sb.Append($"  {key}: {name}  {s.stock}/{s.maxStock}  {cd}\n");
        }
    }
}

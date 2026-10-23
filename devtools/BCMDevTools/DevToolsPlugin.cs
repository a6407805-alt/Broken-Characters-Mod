// BCM Dev Tools: a test bench for making mods. Dev only (never shipped in the friends' pack).
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
    public class DevToolsPlugin : BaseUnityPlugin
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

        // ---------------------------------------------------------------- state
        enum Quick { Off, WaitTitle, WaitLobby }
        Quick _quick = Quick.Off;
        float _quickTimer;

        static bool _spawnsOff;           // read by the Harmony patch
        bool _godMode, _panelOpen = true, _dummyAi;
        int _spawnPick = 2;
        Harmony _harmony;
        Rect _win = new Rect(Screen.width - 440, 60, 420, 10);

        static readonly string[] SpawnNames = { "Beetle", "Lemurian", "Stone Golem", "Elder Lemurian", "Stone Titan" };
        static readonly string[] SpawnMasters = { "BeetleMaster", "LemurianMaster", "GolemMaster", "LemurianBruiserMaster", "TitanMaster" };

        struct Hit { public float time, amount; public bool crit; public string victim; public bool taken; }
        readonly List<Hit> _hits = new List<Hit>();

        // ================================================================ lifecycle
        void Awake()
        {
            _mode = Config.Bind("Quick Test", "Mode", "Simulacrum", "Simulacrum (arena, needs Survivors of the Void) or Classic.");
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
            GlobalEventManager.onServerDamageDealt -= OnDamage;
            Stage.onServerStageBegin -= OnStageBegin;
            _harmony?.UnpatchSelf();
        }

        static bool BlockWave() => !_spawnsOff;   // false = skip the original (no new wave)

        // ================================================================ quick test
        void OnSceneChanged(Scene from, Scene to)
        {
            if (_quick == Quick.Off) return;
            if (to.name == "intro") { Logger.LogInfo("Skipping intro"); SceneManager.LoadScene("title"); return; }
            _quickTimer = 0f;
        }

        void QuickTestUpdate()
        {
            if (_quick == Quick.Off) return;
            _quickTimer += Time.unscaledDeltaTime;
            try
            {
                if (_quick == Quick.WaitTitle && SceneManager.GetActiveScene().name == "title" && RoR2.Console.instance != null && _quickTimer > 1f)
                {
                    string mode = _mode.Value.Trim().ToLowerInvariant() == "classic" ? "ClassicRun" : "InfiniteTowerRun";
                    Logger.LogInfo("Quick test: hosting " + mode);
                    RoR2.Console.instance.SubmitCmd(null, "transition_command \"gamemode " + mode + "; host 0;\"");
                    _quick = Quick.WaitLobby; _quickTimer = 0f;
                }
                else if (_quick == Quick.WaitLobby && PreGameController.instance != null && _quickTimer > 1.5f)
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
                    StartRunNow();
                }
            }
            catch (Exception e) { Logger.LogError("Quick test failed: " + e); _quick = Quick.Off; }
        }

        void StartRunNow()
        {
            foreach (string name in new[] { "StartLaunch", "StartRun" })
            {
                MethodInfo m = AccessTools.Method(typeof(PreGameController), name, Type.EmptyTypes);
                if (m == null) continue;
                Logger.LogInfo("Quick test: PreGameController." + name);
                m.Invoke(PreGameController.instance, null);
                return;
            }
            Logger.LogInfo("Quick test: pregame_start_run");
            RoR2.Console.instance.SubmitCmd(LocalUserManager.GetFirstLocalUser()?.currentNetworkUser, "pregame_start_run");
        }

        void OnStageBegin(Stage stage) => ApplySpawns();

        void ApplySpawns()
        {
            if (!NetworkServer.active) return;
            foreach (CombatDirector d in FindObjectsOfType<CombatDirector>()) d.enabled = !_spawnsOff;
        }

        // ================================================================ actions
        static CharacterBody Me => LocalUserManager.GetFirstLocalUser()?.cachedBody;

        void Update()
        {
            QuickTestUpdate();

            if (Input.GetKeyDown(_panelKey.Value)) _panelOpen = !_panelOpen;
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
            if (!_panelOpen) return;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, wordWrap = true };
            _win = GUILayout.Window(0x8CD7, _win, DrawPanel, "BCM DEV TOOLS   (F2 hide · Esc to click)", GUILayout.Width(420));
        }

        void DrawPanel(int id)
        {
            CharacterBody me = Me;
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
            if (GUILayout.Button("<", GUILayout.Width(26))) _spawnPick = (_spawnPick + SpawnNames.Length - 1) % SpawnNames.Length;
            if (GUILayout.Button("Spawn " + SpawnNames[_spawnPick] + " [F5]")) SpawnEnemy();
            if (GUILayout.Button(">", GUILayout.Width(26))) _spawnPick = (_spawnPick + 1) % SpawnNames.Length;
            if (GUILayout.Button(_dummyAi ? "AI: on" : "AI: off (dummy)")) _dummyAi = !_dummyAi;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kill all [F7]")) KillAll();
            if (GUILayout.Button("Reset cooldowns [F8]")) ResetCooldowns();
            if (GUILayout.Button("Clear log")) _hits.Clear();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use this survivor")) UseThisSurvivor();
            if (GUILayout.Button("NEW TEST RUN")) NewTestRun();
            if (GUILayout.Button("RELAUNCH GAME [F9]")) Relaunch();
            GUILayout.EndHorizontal();

            GUILayout.Label($"<color=#aaaaaa>Test survivor: {(string.IsNullOrWhiteSpace(_survivor.Value) ? "last used" : _survivor.Value)} · Mode: {_mode.Value} · F6 = hot reload (scripts\\)</color>", _label);
            GUI.DragWindow();
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

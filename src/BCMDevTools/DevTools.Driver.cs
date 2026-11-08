// Remote test driver (TEST LEVEL + singleplayer only, same guard as every dev tool).
// Lets a tester (or Claude through the linked PC) drive the game from a text file when mouse input can't reach it.
// Write commands, one per line, to BepInEx\bcm_devcmd.txt; the file is read and deleted every 0.25 s.
//   body <BodyName>          lobby: pick that survivor; in a run: respawn as it
//   start                    lobby: start the run (pregame_start_run)
//   skill <1-4> <secs>       hold primary/secondary/utility/special
//   move <f|b|l|r> <secs>    walk relative to aim; sprint <secs>; jump
//   turn <degrees>           rotate the aim (and the camera) left/right
//   god | spawn | kill | cd  same as F3 / F5 / F7 / F8
//   cmd <console command>    run any console command
//   dump                     log the player's model renderers (name, enabled, material, bounds)
//   wait <secs> | waitlobby | waitbody   pause the queue (until time passes / character select / you have a body)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BCMDevTools
{
    public partial class DevToolsPlugin
    {
        static string DriverFile => Path.Combine(Paths.BepInExRootPath, "bcm_devcmd.txt");
        float _driverPoll;

        static readonly float[] _skillUntil = new float[4];
        static float _moveUntil, _sprintUntil, _jumpUntil;
        static Vector2 _moveDir;
        static float _yaw;            // extra aim yaw in degrees
        static bool _driverUsed;

        void DriverAwake()
        {
            try
            {
                foreach (string name in new[] { "Update", "FixedUpdate" })
                {
                    MethodInfo m = AccessTools.Method(typeof(PlayerCharacterMasterController), name);
                    if (m != null) _harmony.Patch(m, postfix: new HarmonyMethod(typeof(DevToolsPlugin), nameof(DriverInputs)));
                }
            }
            catch (Exception e) { Logger.LogWarning("Driver patch: " + e.Message); }
        }

        readonly Queue<string> _driverQueue = new Queue<string>();
        float _driverWaitUntil;
        string _driverWaitFor;

        void DriverUpdate()
        {
            _driverPoll += Time.unscaledDeltaTime;
            if (_driverPoll >= 0.25f)
            {
                _driverPoll = 0f;
                try
                {
                    if (File.Exists(DriverFile))
                    {
                        foreach (string raw in File.ReadAllLines(DriverFile))
                        {
                            string l = raw.Trim();
                            if (l.Length > 0 && !l.StartsWith("#")) _driverQueue.Enqueue(l);
                        }
                        File.Delete(DriverFile);
                    }
                }
                catch { }
            }

            while (_driverQueue.Count > 0)
            {
                if (Time.unscaledTime < _driverWaitUntil) return;
                if (_driverWaitFor == "lobby" && !PreGameController.instance) return;
                if (_driverWaitFor == "body" && !Me) return;
                _driverWaitFor = null;

                string line = _driverQueue.Dequeue();
                string low = line.ToLowerInvariant();
                if (low.StartsWith("wait "))
                {
                    float.TryParse(line.Substring(5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w);
                    _driverWaitUntil = Time.unscaledTime + w;
                    continue;
                }
                if (low == "waitlobby") { _driverWaitFor = "lobby"; continue; }
                if (low == "waitbody") { _driverWaitFor = "body"; continue; }
                try { RunDriver(line); }
                catch (Exception e) { Logger.LogWarning("Driver '" + line + "': " + e.Message); }
            }
        }

        void RunDriver(string line)
        {
            string[] p = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = p[0].ToLowerInvariant();
            string arg = p.Length > 1 ? p[1].Trim() : "";
            float now = Time.time;
            float Secs(string s, float def) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : def;
            _driverUsed = true;
            Logger.LogInfo("Driver: " + line);

            switch (cmd)
            {
                case "body":
                {
                    BodyIndex idx = BodyCatalog.FindBodyIndex(arg);
                    if (idx == BodyIndex.None) { Logger.LogWarning("Driver: no body " + arg); return; }
                    if (Me)
                    {
                        SurvivorDef def = SurvivorCatalog.GetSurvivorDef(SurvivorCatalog.GetSurvivorIndexFromBodyIndex(idx));
                        if (def) SwapTo(def);
                    }
                    else LocalUserManager.GetFirstLocalUser()?.currentNetworkUser?.CmdSetBodyPreference(idx);
                    _survivor.Value = arg; Config.Save();
                    break;
                }
                case "start": RoR2.Console.instance.SubmitCmd(null, "pregame_start_run"); break;
                case "skill":
                {
                    string[] a = arg.Split(' ');
                    int s = Mathf.Clamp((int)Secs(a[0], 1) - 1, 0, 3);
                    _skillUntil[s] = now + (a.Length > 1 ? Secs(a[1], 0.5f) : 0.5f);
                    break;
                }
                case "move":
                {
                    string[] a = arg.Split(' ');
                    string d = a[0].ToLowerInvariant();
                    _moveDir = d == "b" ? Vector2.down : d == "l" ? Vector2.left : d == "r" ? Vector2.right : Vector2.up;
                    _moveUntil = now + (a.Length > 1 ? Secs(a[1], 1f) : 1f);
                    break;
                }
                case "sprint": _sprintUntil = now + Secs(arg, 1f); break;
                case "jump": _jumpUntil = now + 0.15f; break;
                case "turn": _yaw += Secs(arg, 90f); TurnCamera(Secs(arg, 90f)); break;
                case "god": _godMode = !_godMode; break;
                case "spawn": SpawnEnemy(); break;
                case "kill": KillAll(); break;
                case "cd": ResetCooldowns(); break;
                case "cmd": RoR2.Console.instance.SubmitCmd(null, arg); break;
                case "dump": DumpModel(); break;
                default: Logger.LogWarning("Driver: unknown command " + cmd); break;
            }
        }

        static void TurnCamera(float deg)
        {
            // Rotate the camera rig's yaw (field name differs between versions, so look it up by name).
            foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
            {
                if (!rig || !rig.localUserViewer) continue;
                Traverse t = Traverse.Create(rig);
                foreach (string f in new[] { "yaw", "currentCameraState" })
                {
                    if (t.Field(f).FieldExists() && t.Field(f).GetValue() is float y) { t.Field(f).SetValue(y + deg); return; }
                }
                // Newer versions keep yaw on the camera mode instance data
                object modeData = t.Field("cameraModeInstanceData").GetValue();
                if (modeData != null)
                {
                    Traverse md = Traverse.Create(modeData);
                    foreach (string f in new[] { "yaw" })
                        if (md.Field(f).FieldExists()) { md.Field(f).SetValue(md.Field(f).GetValue<float>() + deg); return; }
                    Traverse pitchYaw = md.Field("pitchYaw");
                    if (pitchYaw.FieldExists())
                    {
                        object py = pitchYaw.GetValue();
                        Traverse pyt = Traverse.Create(py);
                        pyt.Field("yaw").SetValue(pyt.Field("yaw").GetValue<float>() + deg);
                        pitchYaw.SetValue(py);
                        return;
                    }
                }
            }
        }

        // Runs after the game wrote the player's inputs: our held inputs win.
        static void DriverInputs(PlayerCharacterMasterController __instance)
        {
            if (!_driverUsed || !Allowed || !__instance) return;
            CharacterBody body = __instance.master ? __instance.master.GetBody() : null;
            if (!body || !body.inputBank) return;
            InputBankTest ib = body.inputBank;
            float now = Time.time;
            if (now < _skillUntil[0]) ib.skill1.PushState(true);
            if (now < _skillUntil[1]) ib.skill2.PushState(true);
            if (now < _skillUntil[2]) ib.skill3.PushState(true);
            if (now < _skillUntil[3]) ib.skill4.PushState(true);
            if (now < _jumpUntil) ib.jump.PushState(true);
            if (now < _sprintUntil) ib.sprint.PushState(true);
            if (now < _moveUntil)
            {
                Vector3 fwd = ib.aimDirection; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = body.gameObject.transform.forward;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                ib.moveVector = (fwd * _moveDir.y + right * _moveDir.x).normalized;
            }
        }

        void DumpModel()
        {
            CharacterBody me = Me;
            if (!me) { Logger.LogInfo("Dump: no body"); return; }
            Transform model = me.modelLocator ? me.modelLocator.modelTransform : null;
            Logger.LogInfo($"Dump: body={me.name} pos={me.footPosition} model={(model ? model.name : "NONE")} modelScale={(model ? model.lossyScale.ToString() : "-")}");
            if (!model) return;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Logger.LogInfo($"Dump:  {r.name} type={r.GetType().Name} active={r.gameObject.activeInHierarchy} enabled={r.enabled} forceOff={r.forceRenderingOff} " +
                               $"mat={(r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "NONE")} bounds={r.bounds.center}/{r.bounds.size}");
            }
            CharacterModel cm = model.GetComponent<CharacterModel>();
            if (cm && cm.baseRendererInfos != null)
                foreach (var info in cm.baseRendererInfos)
                    Logger.LogInfo($"Dump:  info {(info.renderer ? info.renderer.name : "null")} mat={(info.defaultMaterial ? info.defaultMaterial.name : "null")}");
        }
    }
}

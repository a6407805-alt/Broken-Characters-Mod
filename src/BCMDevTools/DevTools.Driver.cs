// Remote test driver (TEST LEVEL + singleplayer only, same guard as every dev tool).
// Lets a tester (or Claude through the linked PC) drive the game from a text file when mouse input can't reach it.
// Write commands, one per line, to BepInEx\bcm_devcmd.txt; the file is read and deleted every 0.25 s.
//   body <BodyName>          lobby: pick that survivor; in a run: respawn as it
//   start                    lobby: start the run (pregame_start_run)
//   skill <1-4> <secs>       hold primary/secondary/utility/special
//   move <f|b|l|r> <secs>    walk relative to aim; sprint <secs>; jump
//   turn <degrees>           rotate the camera left/right; pitch <deg> = set camera pitch (+ = down)
//   cursor 1|0               free the mouse cursor (so moving the real mouse doesn't swing the camera)
//   god | spawn | kill | cd  same as F3 / F5 / F7 / F8
//   cmd <console command>    run any console command
//   dump                     log the player's model renderers (name, enabled, material, bounds)
//   wait <secs> | waitlobby | waitbody   pause the queue (until time passes / character select / you have a body; 180 s max)
//   clear                    drop everything still queued (put it first in the file)
//   shot <name>              save a screenshot to BepInEx\bcm_shots\<name>.png
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
        static string DriverFile => System.IO.Path.Combine(Paths.BepInExRootPath, "bcm_devcmd.txt");
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
        float _driverWaitStarted;

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
                            if (raw.Trim().ToLowerInvariant() == "clear")
                            {
                                _driverQueue.Clear(); _driverWaitFor = null; _driverWaitUntil = 0f;
                                continue;
                            }
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
                bool timedOut = _driverWaitFor != null && Time.unscaledTime > _driverWaitStarted + 180f;
                if (!timedOut && _driverWaitFor == "lobby" && !PreGameController.instance) return;
                if (!timedOut && _driverWaitFor == "body" && !Me) return;
                _driverWaitFor = null;

                string line = _driverQueue.Dequeue();
                string low = line.ToLowerInvariant();
                if (low.StartsWith("wait "))
                {
                    float.TryParse(line.Substring(5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w);
                    _driverWaitUntil = Time.unscaledTime + w;
                    continue;
                }
                if (low == "waitlobby") { _driverWaitFor = "lobby"; _driverWaitStarted = Time.unscaledTime; continue; }
                if (low == "waitbody") { _driverWaitFor = "body"; _driverWaitStarted = Time.unscaledTime; continue; }
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
                case "turn": _yaw += Secs(arg, 90f); SetPitchYaw(null, Secs(arg, 90f)); break;
                case "pitch": SetPitchYaw(Secs(arg, 10f), null); break;
                case "cursor": SetMouseMode(arg != "0"); break;
                case "god": _godMode = !_godMode; break;
                case "spawn": SpawnEnemy(); break;
                case "kill": KillAll(); break;
                case "cd": ResetCooldowns(); break;
                case "cmd": RoR2.Console.instance.SubmitCmd(null, arg); break;
                case "dump": DumpModel(); break;
                case "shot":
                {
                    string dir = System.IO.Path.Combine(Paths.BepInExRootPath, "bcm_shots");
                    Directory.CreateDirectory(dir);
                    string name = string.IsNullOrEmpty(arg) ? DateTime.Now.ToString("HHmmss") : arg;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
                    break;
                }
                case "hud": RoR2.Console.instance.SubmitCmd(null, "hud_enable " + (arg == "0" ? "0" : "1")); break;
                default: Logger.LogWarning("Driver: unknown command " + cmd); break;
            }
        }

        /// <summary>Sets the camera pitch (absolute, + = look down) and/or adds yaw, by finding the PitchYawPair the camera mode keeps.</summary>
        void SetPitchYaw(float? pitch, float? addYaw)
        {
            foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
            {
                if (!rig || rig.localUserViewer == null) continue;
                // breadth-first through the rig's objects (camera mode, its per-rig instance data dictionaries...)
                var seen = new HashSet<object>();
                var queue = new Queue<KeyValuePair<object, int>>();
                queue.Enqueue(new KeyValuePair<object, int>(rig, 0));
                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    object o = item.Key;
                    if (o == null || !seen.Add(o)) continue;
                    Type t = o.GetType();
                    FieldInfo py = t.GetField("pitchYaw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (py != null && !t.IsValueType)
                    {
                        object v = py.GetValue(o);
                        Type vt = v.GetType();
                        float p0 = (float)vt.GetField("pitch").GetValue(v), y0 = (float)vt.GetField("yaw").GetValue(v);
                        float p1 = pitch ?? p0, y1 = y0 + (addYaw ?? 0f);
                        py.SetValue(o, Activator.CreateInstance(vt, p1, y1));
                        Logger.LogInfo($"Driver: camera {t.Name}.pitchYaw {p0:F0}/{y0:F0} -> {p1:F0}/{y1:F0}");
                        return;
                    }
                    if (item.Value >= 4) continue;
                    if (o is System.Collections.IDictionary dict)
                    {
                        foreach (System.Collections.DictionaryEntry e in dict) queue.Enqueue(new KeyValuePair<object, int>(e.Value, item.Value + 1));
                        continue;
                    }
                    if (o is UnityEngine.Object && !(o is CameraRigController) && !(o is ScriptableObject)) continue;
                    for (Type tt = t; tt != null && tt != typeof(object) && tt != typeof(MonoBehaviour); tt = tt.BaseType)
                        foreach (FieldInfo f in tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string) || f.FieldType.IsValueType) continue;
                            object v;
                            try { v = f.GetValue(o); } catch { continue; }
                            if (v != null) queue.Enqueue(new KeyValuePair<object, int>(v, item.Value + 1));
                        }
                }
                Logger.LogWarning("Driver: no pitchYaw found under the camera rig");
                return;
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
            try
            {
                CameraTargetParams ctp = me.GetComponent<CameraTargetParams>();
                if (ctp)
                {
                    Logger.LogInfo($"Dump: cam params={(ctp.cameraParams ? ctp.cameraParams.name : "null")} ideal={(ctp.cameraParams ? ctp.cameraParams.data.idealLocalCameraPos.value.ToString() : "-")} pivotOff={(ctp.cameraParams ? ctp.cameraParams.data.pivotVerticalOffset.value.ToString() : "-")} pivot={(ctp.cameraPivotTransform ? ctp.cameraPivotTransform.position.ToString() : "null")}");
                }
                foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
                    if (rig && rig.sceneCam) Logger.LogInfo($"Dump: camera pos={rig.sceneCam.transform.position} fwd={rig.sceneCam.transform.forward} dist={(rig.sceneCam.transform.position - me.corePosition).magnitude:F1}");
            }
            catch (Exception e) { Logger.LogInfo("Dump cam: " + e.Message); }
            CharacterModel cm = model.GetComponent<CharacterModel>();
            if (cm && cm.baseRendererInfos != null)
                foreach (var info in cm.baseRendererInfos)
                    Logger.LogInfo($"Dump:  info {(info.renderer ? info.renderer.name : "null")} mat={(info.defaultMaterial ? info.defaultMaterial.name : "null")}");
        }
    }
}

using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpecialForces
{
    // The opening (every machine, local): the squad's dropship comes down through the storm and lands beside the
    // control tower while Command briefs them over the radio. Three shots: the lander falling through the rain;
    // inside the bay with the squad (each player's chosen survivor) in their seats; the landing and the ramp dropping.
    // Players stay hidden and can't act until the ramp is down; they're then standing at the ramp. Skipped if the run
    // is already under way (someone joining late) or turned off in the config.
    internal static class SFCutscene
    {
        public const float Length = 13.5f;
        public static bool Playing { get; set; }

        public static void Begin()
        {
            Playing = false;
            if (!SFConfig.Cutscene.Value || !SFAssets.Lander) return;
            if (Run.instance && Run.instance.GetRunStopwatch() > 10f) return;
            new GameObject("SFCutscene").AddComponent<LanderCinematic>();
        }
    }

    public class LanderCinematic : MonoBehaviour, ICameraStateProvider
    {
        private const float InteriorAt = 4.5f, LandingShotAt = 8.5f, TouchdownAt = 11f, RampAt = 11.2f, RevealAt = 12.6f;
        private static readonly (float at, float until, string token)[] Lines =
        {
            (0.9f, 4.6f, "RADIO_1"), (4.6f, 8.3f, "RADIO_2"), (8.3f, 13.0f, "RADIO_3"),
        };

        private float age;
        private Vector3 pad, start, hover, travelDir;
        private GameObject lander;
        private Transform ramp, interiorCam;
        private Quaternion rampClosed, rampOpen;
        private readonly List<CameraRigController> rigs = new List<CameraRigController>();
        private readonly HashSet<CharacterModel> hidden = new HashSet<CharacterModel>();
        private Vector3 camPos;
        private Quaternion camRot;
        private bool released, landed, radioStarted;
        private Canvas canvas;
        private TextMeshProUGUI subtitle;

        private void Start()
        {
            SFCutscene.Playing = true;
            pad = SFUtil.Ground(SFScene.LanderPad, 20f);
            Vector3 away = pad - SFScene.TowerPosition;
            away.y = 0;
            if (away.sqrMagnitude < 1f) away = Vector3.back;
            away.Normalize();
            start = pad + away * 160f + Vector3.up * 190f;
            hover = pad + Vector3.up * 26f;
            travelDir = (hover - start);
            travelDir.y = 0;
            travelDir.Normalize();

            lander = MakeLander();
            lander.transform.SetPositionAndRotation(start, Quaternion.LookRotation(travelDir));
            ramp = SFUtil.FindDeep(lander.transform, "LanderRamp");
            interiorCam = SFUtil.FindDeep(lander.transform, "CamInterior");
            if (ramp)
            {
                rampClosed = ramp.localRotation;
                rampOpen = PickRampOpen();
            }
            SeatSquad();
            Lights();
            SFAudio.Play(SFAudio.Clip.LanderEngine);
            BuildSubtitles();
        }

        // 1.0.1 flew the lander on its side: setting its rotation wiped the FBX root's Blender-to-Unity rotation.
        // Now an upright holder carries it, and the model inside is turned so its nose is the holder's forward
        // (the ramp hinge at the back tells which way the model faces, whatever the import axes were).
        private static GameObject MakeLander()
        {
            var holder = new GameObject("SFLander");
            GameObject model = SFAssets.Spawn(SFAssets.Lander, Vector3.zero, Quaternion.identity, 1f, holder.transform);
            Transform hinge = SFUtil.FindDeep(model.transform, "LanderRamp");
            Vector3 rear = hinge ? hinge.position - model.transform.position : Vector3.back;
            rear.y = 0;
            if (rear.sqrMagnitude > 0.01f)
            {
                Quaternion turn = Quaternion.FromToRotation(rear.normalized, Vector3.back);
                model.transform.localRotation = turn * model.transform.localRotation;
                model.transform.localPosition = turn * model.transform.localPosition;
            }
            return holder;
        }

        // The ramp opens outwards (away from the bay): try both directions about the hinge and keep the one whose
        // tip ends further from the lander's middle (import axes differ between Blender and Unity).
        private Quaternion PickRampOpen()
        {
            Renderer r = ramp.GetComponentInChildren<Renderer>();
            Vector3 middle = lander.transform.position + Vector3.up * 3f;
            Quaternion best = rampClosed;
            float bestDistance = -1f;
            foreach (float sign in new[] { 1f, -1f })
            {
                Quaternion q = rampClosed * Quaternion.AngleAxis(sign * 100f, Vector3.right);
                ramp.localRotation = q;
                float d = r ? (r.bounds.center - middle).magnitude : 0f;
                if (d > bestDistance) { bestDistance = d; best = q; }
            }
            ramp.localRotation = rampClosed;
            return best;
        }

        private void SeatSquad()
        {
            int seat = 1, seated = 0, players = 0, bodyModels = 0;
            foreach (var pc in PlayerCharacterMasterController.instances)
            {
                if (seat > 12) break;
                players++;
                var master = pc.master;
                bool fromBody = false;
                GameObject display = master ? DisplayFor(master.bodyPrefab, out fromBody) : null;
                if (display && fromBody) bodyModels++;
                Transform anchor = SFUtil.FindDeep(lander.transform, $"Seat_{seat:00}");
                seat++;
                if (!display || !anchor) continue;
                try
                {
                    // 1.0.2 showed an empty bay: the display was made a child of the seat with local scale 0.95, but the
                    // seat sits under the FBX's scaled transforms, so the character came out the wrong size. Made in
                    // world space at its normal size, then attached keeping that size.
                    var d = Instantiate(display, anchor.position, Quaternion.identity);
                    if (fromBody) Pose(d);
                    d.transform.localScale = Vector3.one * 0.95f;
                    Vector3 inward = Vector3.Project(lander.transform.position - anchor.position, lander.transform.right);
                    if (inward.sqrMagnitude > 0.01f) d.transform.rotation = Quaternion.LookRotation(inward, lander.transform.up);
                    d.transform.SetParent(anchor, true);
                    foreach (var r in d.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                    seated++;
                }
                catch (System.Exception e) { SpecialForcesPlugin.Log.LogWarning("Squad display failed: " + e.Message); }
            }
            Transform first = SFUtil.FindDeep(lander.transform, "Seat_01");
            SpecialForcesPlugin.Log.LogInfo($"Lander: seated {seated} of {players} players, {bodyModels} from the body model (seat scale {(first ? first.lossyScale.x : 0f):0.###}).");
        }

        // The survivor's lobby display; in game 1.5 it may only load on demand (SurvivorDef.PreloadAssets), so that is
        // asked for, and failing that the body's own model is used.
        private static GameObject DisplayFor(GameObject bodyPrefab, out bool fromBody)
        {
            fromBody = false;
            if (!bodyPrefab) return null;
            var survivor = SurvivorCatalog.FindSurvivorDefFromBody(bodyPrefab);
            if (survivor)
            {
                if (survivor.displayPrefab) return survivor.displayPrefab;
                try { SFUtil.Call(survivor, "PreloadAssets"); } catch { }
                if (survivor.displayPrefab) return survivor.displayPrefab;
            }
            var locator = bodyPrefab.GetComponent<ModelLocator>();
            if (!locator || !locator.modelTransform) return null;
            fromBody = true;
            return locator.modelTransform.gameObject;
        }

        // A copy of a body's model, made inert: no hitboxes, no scripts that expect a living body; the Animator stays.
        private static void Pose(GameObject copy)
        {
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        }

        private void Lights()
        {
            var bay = new GameObject("BayLight").AddComponent<Light>();
            bay.transform.SetParent(lander.transform, false);
            bay.transform.localPosition = new Vector3(0, 5f, 0);
            bay.type = LightType.Point;
            bay.color = new Color(1f, 0.85f, 0.7f);    // 1.0.1's dim red light left the bay black
            bay.range = 16f;
            bay.intensity = 3.5f;
            bay.shadows = LightShadows.None;
            var alarm = new GameObject("BayAlarm").AddComponent<Light>();
            alarm.transform.SetParent(lander.transform, false);
            alarm.transform.localPosition = new Vector3(0, 6f, -4f);
            alarm.type = LightType.Point;
            alarm.color = new Color(1f, 0.2f, 0.15f);
            alarm.range = 12f;
            alarm.intensity = 2.5f;
            foreach (string engine in new[] { "EngineL", "EngineR" })
            {
                Transform e = SFUtil.FindDeep(lander.transform, engine);
                if (!e) continue;
                var l = new GameObject("EngineGlow").AddComponent<Light>();
                l.transform.SetParent(e, false);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.2f);
                l.range = 25f;
                l.intensity = 5f;
            }
        }

        private void BuildSubtitles()
        {
            var go = new GameObject("SFSubtitles");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var t = new GameObject("Line", typeof(RectTransform));
            t.transform.SetParent(go.transform, false);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = new Vector2(0.15f, 0.06f);
            rt.anchorMax = new Vector2(0.85f, 0.2f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            subtitle = t.AddComponent<HGTextMeshProUGUI>();
            subtitle.fontSize = 30;
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.color = new Color(0.85f, 0.95f, 1f);
            subtitle.enableWordWrapping = true;
            subtitle.text = "";
        }

        private Vector3 LanderPosition(float t)
        {
            if (t < TouchdownAt * 0.77f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / (TouchdownAt * 0.77f));
                Vector3 a = Vector3.Lerp(start, hover + Vector3.up * 40f, k);
                return Vector3.Lerp(a, hover, k * k);
            }
            float f = Mathf.Clamp01((t - TouchdownAt * 0.77f) / (TouchdownAt * 0.23f));
            return Vector3.Lerp(hover, pad + Vector3.up * 0.6f, Mathf.SmoothStep(0f, 1f, f));
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (lander)
            {
                lander.transform.position = LanderPosition(Mathf.Min(age, TouchdownAt));
                float bank = age < TouchdownAt ? Mathf.Sin(age * 0.9f) * 4f * (1f - age / TouchdownAt) : 0f;
                lander.transform.rotation = Quaternion.LookRotation(travelDir) * Quaternion.Euler(age < TouchdownAt * 0.77f ? -8f : 0f, 0f, bank);
            }
            if (!landed && age >= TouchdownAt)
            {
                landed = true;
                SFAudio.Play(SFAudio.Clip.LanderLand);
                GameObject boom = SFUtil.Load<GameObject>(Keys.OmniExplosion, "OmniExplosionVFX");
                if (boom) EffectManager.SpawnEffect(boom, new EffectData { origin = pad, scale = 12f }, false);
            }
            if (ramp && age >= RampAt)
                ramp.localRotation = Quaternion.Slerp(rampClosed, rampOpen, Mathf.SmoothStep(0f, 1f, (age - RampAt) / 1.2f));
            if (!radioStarted && age >= Lines[0].at)
            {
                radioStarted = true;
                SFAudio.Play(SFConfig.RadioAltVoice.Value ? SFAudio.Clip.RadioAlt : SFAudio.Clip.Radio);
            }
            if (subtitle)
            {
                string text = "";
                foreach (var line in Lines) if (age >= line.at && age < line.until) text = Tokens.Get(line.token);
                subtitle.text = text;
            }
            HidePlayers();
            Shots();
            if (!released && age >= RevealAt) Release();
            if (age > SFCutscene.Length + 2f && released) Destroy(gameObject, 0f);
        }

        private void Shots()
        {
            Vector3 lp = lander ? lander.transform.position : pad;
            if (age < InteriorAt)
            {
                // Wide: from below and to the side, the lander dropping through the rain towards the outpost lights.
                Vector3 side = Vector3.Cross(Vector3.up, travelDir);
                camPos = lp + side * 55f - travelDir * 20f + Vector3.down * 12f;
                camRot = Quaternion.LookRotation(lp + Vector3.up * 4f - camPos);
            }
            else if (age < LandingShotAt && interiorCam)
            {
                // Inside the bay at the back, looking forward along the two rows of seats.
                camPos = interiorCam.position;
                Vector3 seats = lander.transform.position + lander.transform.up * 1.6f;
                camRot = Quaternion.LookRotation(seats - camPos, lander.transform.up);
            }
            else
            {
                // Landing: low beside the pad, looking at the rear ramp with the tower behind.
                Vector3 toTower = SFScene.TowerPosition - pad;
                toTower.y = 0;
                toTower.Normalize();
                camPos = pad + toTower * 28f + Vector3.Cross(Vector3.up, toTower) * 18f + Vector3.up * 6f;
                camRot = Quaternion.LookRotation(lp + Vector3.up * 4f - camPos);
            }
            foreach (var rig in CameraRigController.readOnlyInstancesList)
            {
                if (!rig || rigs.Contains(rig) || released) continue;
                rigs.Add(rig);
                rig.SetOverrideCam(this, 0f);
            }
        }

        private void HidePlayers()
        {
            bool hide = age < RevealAt;
            foreach (var pc in PlayerCharacterMasterController.instances)
            {
                var body = pc.master ? pc.master.GetBody() : null;
                if (!body) continue;
                var model = body.modelLocator && body.modelLocator.modelTransform ? body.modelLocator.modelTransform.GetComponent<CharacterModel>() : null;
                if (hide && model && hidden.Add(model)) model.invisibilityCount++;
                if (hide && body.hasEffectiveAuthority && body.characterMotor) body.characterMotor.velocity = Vector3.zero;
            }
        }

        private void Release()
        {
            released = true;
            SFCutscene.Playing = false;
            foreach (var model in hidden) if (model) model.invisibilityCount--;
            hidden.Clear();
            foreach (var rig in rigs) if (rig && rig.IsOverrideCam(this)) rig.SetOverrideCam(null, 1.2f);
            SFAudio.Stop(SFAudio.Clip.LanderEngine);
        }

        private void OnDestroy()
        {
            if (!released) Release();
        }

        public void GetCameraState(CameraRigController cameraRigController, ref CameraState cameraState)
        {
            cameraState.position = camPos;
            cameraState.rotation = camRot;
            cameraState.fov = age >= InteriorAt && age < LandingShotAt ? 75f : 55f;
        }

        public bool IsUserLookAllowed(CameraRigController cameraRigController) => false;
        public bool IsUserControlAllowed(CameraRigController cameraRigController) => false;
        public bool IsHudAllowed(CameraRigController cameraRigController) => false;
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarMaxSpecialist
{
    // ================================================================ HUD: timecard, banners, speech bubbles

    public class CmxHud : MonoBehaviour
    {
        private class Bub
        {
            public Transform Anchor;
            public Vector3 Pos;
            public string Text;
            public Style Style;
            public float Born, Life;
        }

        private static readonly List<Bub> Bubs = new List<Bub>();
        private static string _banner;
        private static float _bannerUntil, _bannerBorn;
        private static Texture2D _white;
        private static GUIStyle _text, _center;
        private static readonly Regex SizePct = new Regex("<size=(\\d+)%>");

        public static void Bubble(Transform anchor, Vector3 pos, string text, Style style)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (CmxPlugin.Bubbles != null && !CmxPlugin.Bubbles.Value && style != Style.Small) return;
            if (Bubs.Count > 40) Bubs.RemoveAt(0);
            Bubs.Add(new Bub { Anchor = anchor, Pos = pos, Text = text, Style = style, Born = Time.unscaledTime, Life = style == Style.Small ? 1.8f : 3.2f });
        }

        public static void Banner(string text, float seconds)
        {
            _banner = text;
            _bannerBorn = Time.unscaledTime;
            _bannerUntil = Time.unscaledTime + seconds;
        }

        private static string Rich(string s, int size)
        {
            return SizePct.Replace(s ?? "", m => "<size=" + Mathf.RoundToInt(size * int.Parse(m.Groups[1].Value) / 100f) + ">");
        }

        private static Camera Cam()
        {
            foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
                if (rig && rig.sceneCam && rig.localUserViewer != null) return rig.sceneCam;
            return Camera.main;
        }

        private static void Styles()
        {
            if (_white) return;
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, alignment = TextAnchor.UpperLeft };
            _center = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        }

        private static void Rect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private static void Shadowed(Rect r, string s, GUIStyle st)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f * old.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), StripColor(s), st);
            GUI.color = old;
            GUI.Label(r, s, st);
        }

        private static readonly Regex ColorTag = new Regex("</?color[^>]*>");
        private static string StripColor(string s) => ColorTag.Replace(s, "");

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Styles();
            float sc = Mathf.Max(0.6f, Screen.height / 1080f);

            DrawBubbles(sc);

            CharacterBody me = null;
            try { me = LocalUserManager.GetFirstLocalUser()?.cachedBody; } catch { }
            CmxShift shift = me ? me.GetComponent<CmxShift>() : null;
            if (shift && me.healthComponent && me.healthComponent.alive) { DrawTimecard(shift, sc); DrawStressMeter(shift, sc); }
            if (shift && _banner != null && Time.unscaledTime < _bannerUntil) DrawBanner(sc);
        }

        private void DrawBubbles(float sc)
        {
            Camera cam = Cam();
            if (!cam) return;
            float now = Time.unscaledTime;
            Bubs.RemoveAll(b => now - b.Born > b.Life);
            Dictionary<Transform, int> stack = new Dictionary<Transform, int>();
            for (int i = Bubs.Count - 1; i >= 0; i--)
            {
                Bub b = Bubs[i];
                Vector3 world = b.Pos;
                float lift = 0f;
                if (b.Anchor)
                {
                    CharacterBody cb = b.Anchor.GetComponent<CharacterBody>();
                    float h = cb ? Mathf.Max(1.6f, cb.radius * 2.2f) : 2f;
                    world = b.Anchor.position + Vector3.up * (h + 0.4f);
                    stack.TryGetValue(b.Anchor, out int n);
                    stack[b.Anchor] = n + 1;
                    lift = n * 34f * sc;
                }
                float age = now - b.Born;
                world += Vector3.up * age * (b.Style == Style.Small ? 1.2f : 0.25f);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z < 0.5f || sp.z > 140f) continue;
                float alpha = Mathf.Clamp01((b.Life - age) / 0.5f) * Mathf.Clamp01(age / 0.12f);
                int size = Mathf.RoundToInt((b.Style == Style.Small ? 20f : 18f) * sc * Mathf.Lerp(1.15f, 0.8f, Mathf.Clamp01(sp.z / 60f)));
                string txt = Rich(b.Text, size);
                _center.fontSize = size;
                _center.wordWrap = false;
                Vector2 dim = _center.CalcSize(new GUIContent(txt));
                float w = Mathf.Min(dim.x + 20f * sc, 420f * sc);
                _center.wordWrap = true;
                float hgt = _center.CalcHeight(new GUIContent(txt), w - 14f * sc) + 10f * sc;
                Rect r = new Rect(sp.x - w / 2f, Screen.height - sp.y - hgt - lift, w, hgt);

                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                if (b.Style == Style.Bubble)
                {
                    Rect(r, new Color(0.96f, 0.96f, 0.93f, 0.92f * alpha));
                    Rect(new Rect(r.x, r.yMax - 3f * sc, r.width, 3f * sc), new Color(0f, 0.23f, 0.46f, alpha));
                    _center.normal.textColor = new Color(0.08f, 0.1f, 0.16f);
                    GUI.Label(new Rect(r.x + 7f * sc, r.y, r.width - 14f * sc, r.height), txt, _center);
                }
                else if (b.Style == Style.Customer)
                {
                    Rect(r, new Color(0.55f, 0.06f, 0.06f, 0.9f * alpha));
                    _center.normal.textColor = Color.white;
                    GUI.Label(new Rect(r.x + 7f * sc, r.y, r.width - 14f * sc, r.height), txt, _center);
                }
                else
                {
                    _center.normal.textColor = Color.white;
                    Shadowed(new Rect(r.x + 7f * sc, r.y, r.width - 14f * sc, r.height), txt, _center);
                }
                GUI.color = old;
            }
            _center.wordWrap = true;
        }

        private void DrawBanner(float sc)
        {
            float age = Time.unscaledTime - _bannerBorn;
            float alpha = Mathf.Clamp01((_bannerUntil - Time.unscaledTime) / 0.5f) * Mathf.Clamp01(age / 0.15f);
            int size = Mathf.RoundToInt(40f * sc);
            string txt = Rich(_banner, size);
            _center.fontSize = size;
            float w = 900f * sc;
            float h = _center.CalcHeight(new GUIContent(txt), w) + 26f * sc;
            float pop = 1f + Mathf.Max(0f, 0.15f - age) * 1.5f;
            Rect r = new Rect(Screen.width / 2f - w / 2f * pop, Screen.height * 0.16f, w * pop, h);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Rect(r, new Color(0f, 0.12f, 0.26f, 0.82f * alpha));
            Rect(new Rect(r.x, r.y, r.width, 5f * sc), new Color(1f, 0.79f, 0.15f, alpha));
            _center.normal.textColor = Color.white;
            Shadowed(r, txt, _center);
            GUI.color = old;
        }

        // ---- stress meter right under (or over) the game's health bar

        private static Component _healthBar;
        private static Canvas _hudCanvas;
        private static float _nextHudLook;

        private static bool HealthBarRect(out Rect r)
        {
            r = default(Rect);
            try
            {
                if ((!_healthBar || !_healthBar.gameObject.activeInHierarchy) && Time.unscaledTime >= _nextHudLook)
                {
                    _nextHudLook = Time.unscaledTime + 1f;
                    _healthBar = null;
                    foreach (RoR2.UI.HealthBar hb in Object.FindObjectsOfType<RoR2.UI.HealthBar>())
                    {
                        if (!hb || !hb.gameObject.activeInHierarchy) continue;
                        if (hb.GetComponentInParent<RoR2.UI.HUD>() == null) continue;
                        _healthBar = hb;
                        _hudCanvas = hb.GetComponentInParent<Canvas>();
                        break;
                    }
                }
                if (!_healthBar) return false;
                RectTransform rt = _healthBar.transform as RectTransform;
                if (!rt) return false;
                Vector3[] c = new Vector3[4];
                rt.GetWorldCorners(c);
                Camera cam = _hudCanvas && _hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _hudCanvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]); // bottom left
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]); // top right
                r = new Rect(a.x, Screen.height - b.y, b.x - a.x, b.y - a.y);
                return r.width > 20f && r.height > 2f;
            }
            catch { return false; }
        }

        private void DrawStressMeter(CmxShift s, float sc)
        {
            float h = 18f * sc;
            Rect bar;
            if (HealthBarRect(out Rect hp))
            {
                float gap = 4f * sc;
                bar = hp.yMax + gap + h < Screen.height ? new Rect(hp.x, hp.yMax + gap, hp.width, h) : new Rect(hp.x, hp.y - gap - h, hp.width, h);
            }
            else bar = new Rect(Screen.width * 0.02f, Screen.height * 0.93f, 380f * sc, h);

            float k = Mathf.Clamp01(s.Stress / 100f);
            Rect(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), new Color(0f, 0f, 0f, 0.6f));
            Rect(bar, new Color(0.12f, 0.14f, 0.2f, 0.85f));
            Color c = k < 0.5f ? Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(1f, 0.8f, 0.2f), k * 2f) : Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.15f, 0.12f), (k - 0.5f) * 2f);
            if (k > 0.85f) c = Color.Lerp(c, Color.white, Mathf.PingPong(Time.unscaledTime * 5f, 0.6f));
            Rect(new Rect(bar.x, bar.y, bar.width * k, bar.height), c);
            for (int i = 1; i < 4; i++) Rect(new Rect(bar.x + bar.width * i / 4f, bar.y, 1f, bar.height), new Color(0f, 0f, 0f, 0.35f));

            _text.fontSize = Mathf.RoundToInt(14f * sc);
            _text.normal.textColor = Color.white;
            string label = k >= 0.999f ? "<b>BURNOUT!</b>" : "<b>STRESS</b>  " + Mathf.RoundToInt(s.Stress) + "%";
            if (s.Overtime) label += "   <color=#FFC928>OVERTIME</color>";
            Shadowed(new Rect(bar.x + 6f * sc, bar.y - 1f * sc, bar.width, bar.height + 4f), label, _text);
        }

        private void DrawTimecard(CmxShift s, float sc)
        {
            float x = Screen.width * CmxPlugin.HudX.Value, y = Screen.height * CmxPlugin.HudY.Value;
            float w = 360f * sc, pad = 10f * sc, line = 24f * sc;
            bool deadline = s.Kind != Deadline.None;
            float h = pad * 2f + line * (deadline ? 4.2f : 3.1f) + (s.Coworkers > 0 ? line : 0f);
            Rect(new Rect(x, y, w, h), new Color(0.02f, 0.07f, 0.16f, 0.82f));
            Rect(new Rect(x, y, w, 4f * sc), new Color(1f, 0.79f, 0.15f, 0.95f));

            _text.fontSize = Mathf.RoundToInt(17f * sc);
            _text.normal.textColor = Color.white;
            float cy = y + pad;
            Shadowed(new Rect(x + pad, cy, w, line), "<b><color=#FFC928>TIMECARD</color></b>  <color=#9FB3D1>Auto Repair Specialist</color>", _text);
            cy += line;

            float ot = CmxPlugin.OvertimeAfter.Value;
            string hours = "Hours  <b>" + s.Hours.ToString("0.0") + "</b>" + (s.Overtime
                ? "   <color=#FFC928><b>OVERTIME x" + CmxPlugin.OvertimePay.Value.ToString("0.#") + "</b></color>"
                : " <color=#9FB3D1>/ " + ot.ToString("0") + "</color>");
            Shadowed(new Rect(x + pad, cy, w, line), hours, _text);
            cy += line;

            float bh = 14f * sc;
            if (deadline)
            {
                _text.fontSize = Mathf.RoundToInt(15f * sc);
                string desc = CmxShift.Describe(s.Kind, s.Target);
                string prog = s.Kind == Deadline.NoHit ? s.Progress.ToString("0") + "/" + s.Target.ToString("0") + "s"
                    : s.Kind == Deadline.Damage ? Mathf.RoundToInt(s.Progress) + "/" + s.Target.ToString("0") : s.Progress.ToString("0") + "/" + s.Target.ToString("0");
                Shadowed(new Rect(x + pad, cy, w - pad * 2f, line), "<color=#FFC928><b>DUE:</b></color> " + desc, _text);
                cy += line;
                float tk = Mathf.Clamp01(s.Progress / Mathf.Max(1f, s.Target));
                Rect(new Rect(x + pad, cy + 4f * sc, w - pad * 2f - 110f * sc, bh), new Color(1f, 1f, 1f, 0.12f));
                Rect(new Rect(x + pad, cy + 4f * sc, (w - pad * 2f - 110f * sc) * tk, bh), new Color(0.35f, 0.7f, 1f));
                string time = s.TimeLeft < 10f ? "<color=#FF6060><b>" + Mathf.CeilToInt(Mathf.Max(0f, s.TimeLeft)) + "s</b></color>" : Mathf.CeilToInt(s.TimeLeft) + "s";
                Shadowed(new Rect(x + w - pad - 102f * sc, cy, 110f * sc, line), prog + "  " + time, _text);
                cy += line;
            }
            else if (CmxPlugin.CombatDeadlines.Value)
            {
                _text.fontSize = Mathf.RoundToInt(15f * sc);
                Shadowed(new Rect(x + pad, cy, w, line), "<color=#9FB3D1>Next deadline in " + Mathf.CeilToInt(Mathf.Max(0f, s.NextIn)) + "s</color>", _text);
                cy += line;
            }
            else
            {
                _text.fontSize = Mathf.RoundToInt(15f * sc);
                int open = CmxTasks.Open;
                Shadowed(new Rect(x + pad, cy, w, line), open == 0 ? "<color=#9FB3D1>Inbox: all caught up</color>" : "<color=#FF8080><b>Open tasks: " + open + "</b></color>  <color=#9FB3D1>(finish them!)</color>", _text);
                cy += line;
            }

            if (s.Coworkers > 0)
            {
                Shadowed(new Rect(x + pad, cy, w, line), "Coworkers on the clock: <b>" + s.Coworkers + "</b>" + (s.OnBreak > 0 ? "  <color=#FF8080>(" + s.OnBreak + " on break)</color>" : ""), _text);
            }
        }
    }

    // ================================================================ his look: blue work shirt, tie, lanyard, clipboard

    public class CmxLook : MonoBehaviour
    {
        private static readonly Dictionary<Material, Material> Tinted = new Dictionary<Material, Material>();
        private static readonly HashSet<Material> Mine = new HashSet<Material>();
        private static Material _navy, _yellow, _white, _board;
        private static readonly Color Shirt = new Color(0.62f, 0.76f, 1.05f);

        private CharacterModel _cm;
        private float _next;
        private bool _props;

        private class Prop
        {
            public Transform Bone, Obj;
            public Vector3 Offset;
            public Quaternion Rel;
            public bool Ready;
        }
        private readonly List<Prop> _list = new List<Prop>();

        private void Start() { _cm = GetComponent<CharacterModel>(); }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.5f;
                Recolor();
                if (!_props && CmxPlugin.Props.Value) { _props = true; BuildProps(); }
            }
            foreach (Prop p in _list)
            {
                if (!p.Bone || !p.Obj) continue;
                if (!p.Ready) { p.Rel = Quaternion.Inverse(p.Bone.rotation) * transform.rotation; p.Ready = true; }
                Quaternion frame = p.Bone.rotation * p.Rel;
                p.Obj.position = p.Bone.position + frame * p.Offset;
                p.Obj.rotation = frame;
            }
        }

        private void Recolor()
        {
            if (!_cm || _cm.baseRendererInfos == null) return;
            for (int i = 0; i < _cm.baseRendererInfos.Length; i++)
            {
                CharacterModel.RendererInfo ri = _cm.baseRendererInfos[i];
                if (!ri.renderer) continue;
                if (!CmxPlugin.AudiModel && ri.renderer.name.IndexOf("gun", StringComparison.OrdinalIgnoreCase) >= 0) { ri.renderer.forceRenderingOff = true; continue; }
                Material src = ri.defaultMaterial;
                if (!src || Mine.Contains(src)) continue;
                if (!Tinted.TryGetValue(src, out Material t) || !t)
                {
                    t = new Material(src) { name = src.name + "_CarMax" };
                    if (CmxPlugin.AudiModel) Blue(src, t);
                    else if (t.HasProperty("_Color")) t.SetColor("_Color", src.GetColor("_Color") * Shirt);
                    Tinted[src] = t;
                    Mine.Add(t);
                }
                ri.defaultMaterial = t;
                _cm.baseRendererInfos[i] = ri;
            }
        }

        // ---- red -> CarMax blue

        private static readonly Dictionary<Texture, Texture2D> BlueTex = new Dictionary<Texture, Texture2D>();

        private static bool IsRed(Color c, out float s, out float v)
        {
            Color.RGBToHSV(c, out float h, out s, out v);
            return (h < 0.06f || h > 0.92f) && s > 0.35f && v > 0.12f;
        }

        private static Color ToBlue(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            Color o = Color.HSVToRGB(0.6f, Mathf.Min(1f, s * 1.05f), v);
            o.a = c.a;
            return o;
        }

        private static void Blue(Material src, Material dst)
        {
            try
            {
                if (dst.HasProperty("_Color"))
                {
                    Color c = src.GetColor("_Color");
                    if (IsRed(c, out _, out _)) dst.SetColor("_Color", ToBlue(c));
                }
                if (dst.HasProperty("_MainTex"))
                {
                    Texture tex = src.GetTexture("_MainTex");
                    if (tex)
                    {
                        if (!BlueTex.TryGetValue(tex, out Texture2D blue)) { blue = BlueCopy(tex); BlueTex[tex] = blue; }
                        if (blue) dst.SetTexture("_MainTex", blue);
                    }
                }
            }
            catch (Exception e) { CmxPlugin.Log.LogWarning("Recolor of " + src.name + " skipped: " + e.Message); }
        }

        // Textures from asset bundles aren't readable, so the copy is made through the GPU.
        private static Texture2D BlueCopy(Texture tex)
        {
            int w = Mathf.Min(tex.width, 2048), h = Mathf.Min(tex.height, 2048);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                Texture2D copy = new Texture2D(w, h, TextureFormat.RGBA32, true);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                Color[] px = copy.GetPixels();
                int changed = 0;
                for (int i = 0; i < px.Length; i++)
                    if (IsRed(px[i], out _, out _)) { px[i] = ToBlue(px[i]); changed++; }
                if (changed == 0) { Object.Destroy(copy); return null; }
                copy.SetPixels(px);
                copy.Apply(true);
                copy.name = tex.name + "_CarMaxBlue";
                return copy;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private Transform Bone(params string[] names)
        {
            ChildLocator cl = GetComponent<ChildLocator>();
            foreach (string n in names)
            {
                Transform t = cl ? cl.FindChild(n) : null;
                if (t) return t;
            }
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                foreach (string n in names)
                    if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        private void Add(Transform bone, Vector3 offset, Action<Transform> build)
        {
            if (!bone) return;
            GameObject root = new GameObject("CmxProp");
            root.transform.SetParent(transform, false);
            build(root.transform);
            _list.Add(new Prop { Bone = bone, Obj = root.transform, Offset = offset });
        }

        private void BuildProps()
        {
            if (!_navy)
            {
                _navy = CmxFx.Lit(new Color(0.02f, 0.14f, 0.32f));
                _yellow = CmxFx.Lit(new Color(1f, 0.79f, 0.15f));
                _white = CmxFx.Lit(new Color(0.95f, 0.95f, 0.92f));
                _board = CmxFx.Lit(new Color(0.45f, 0.3f, 0.15f));
            }
            try
            {
                Transform chest = Bone("Chest", "chest", "Spine3", "spine.003");
                Add(chest, new Vector3(0f, 0.08f, 0.2f), t =>
                {
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.07f, 0.06f, 0.04f), _navy);
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.01f), new Vector3(0.08f, 0.3f, 0.02f), _navy, new Vector3(-6f, 0f, 0f));
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(-0.09f, 0.04f, 0.0f), new Vector3(0.025f, 0.3f, 0.01f), _yellow, new Vector3(0f, 0f, -22f));
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0.09f, 0.04f, 0.0f), new Vector3(0.025f, 0.3f, 0.01f), _yellow, new Vector3(0f, 0f, 22f));
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0.11f, -0.14f, 0.02f), new Vector3(0.09f, 0.12f, 0.01f), _white);
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0.11f, -0.11f, 0.027f), new Vector3(0.07f, 0.025f, 0.004f), _navy);
                });
                Transform hand = Bone("HandL", "hand.l", "LeftHand", "Hand.L");
                Add(hand, new Vector3(0f, -0.05f, 0.12f), t =>
                {
                    CmxFx.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.24f, 0.32f, 0.02f), _board, new Vector3(20f, 0f, 0f));
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.01f, 0.012f), new Vector3(0.2f, 0.26f, 0.005f), _white, new Vector3(20f, 0f, 0f));
                    CmxFx.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.14f, 0.02f), new Vector3(0.08f, 0.03f, 0.02f), _yellow, new Vector3(20f, 0f, 0f));
                });
                if (!chest) CmxPlugin.Log.LogInfo("No chest bone found; tie and lanyard skipped.");
            }
            catch (Exception e) { CmxPlugin.Log.LogWarning("Props skipped: " + e.Message); }
        }
    }

    // ================================================================ sounds (Windows winmm, like Wilbur's)

    public static class CmxAudio
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int mciSendString(string command, StringBuilder ret, int len, IntPtr hwnd);

        private static readonly string[] Keys = { "printer", "typing", "phone", "pizza", "stamp", "clock", "email", "cash", "slam", "sigh", "memo", "beep", "fail" };
        private const int Slots = 2;
        private static readonly Dictionary<string, List<string[]>> Sounds = new Dictionary<string, List<string[]>>();
        private static readonly Dictionary<string, int> NextSlot = new Dictionary<string, int>();
        private static readonly List<string> Opened = new List<string>();
        private static bool _broken;
        private static int _loads;

        private static int Send(string cmd)
        {
            if (_broken) return -1;
            try { return mciSendString(cmd, null, 0, IntPtr.Zero); }
            catch (Exception e) { _broken = true; CmxPlugin.Log?.LogWarning("CarMax sounds disabled (winmm unavailable): " + e.Message); return -1; }
        }

        public static void Unload()
        {
            foreach (string a in Opened) Send("close " + a);
            Opened.Clear();
            Sounds.Clear();
            NextSlot.Clear();
        }

        public static void Load(string folder, int volume)
        {
            Unload();
            if (_broken || !System.IO.Directory.Exists(folder)) return;
            _loads++;
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CarMaxSounds");
            try
            {
                System.IO.Directory.CreateDirectory(temp);
                foreach (string old in System.IO.Directory.GetFiles(temp, "*.wav")) { try { System.IO.File.Delete(old); } catch { } }
            }
            catch { return; }
            float gain = Mathf.Pow(Mathf.Clamp(volume, 0, 100) / 100f, 2f);
            int loaded = 0;
            foreach (string file in System.IO.Directory.GetFiles(folder, "*.wav"))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string key = null;
                foreach (string k in Keys) if (name.StartsWith(k)) { key = k; break; }
                if (key == null) continue;
                byte[] data = Scale(file, gain);
                if (data == null) continue;
                if (!Sounds.TryGetValue(key, out List<string[]> variants)) Sounds[key] = variants = new List<string[]>();
                string[] pool = new string[Slots];
                int ok = 0;
                for (int s = 0; s < Slots; s++)
                {
                    string alias = "cmx" + _loads + "_" + key + "_" + variants.Count + "_" + s;
                    string dest = System.IO.Path.Combine(temp, alias + ".wav");
                    try { System.IO.File.WriteAllBytes(dest, data); } catch { continue; }
                    if (Send("open \"" + dest + "\" type waveaudio alias " + alias) == 0) { pool[ok++] = alias; Opened.Add(alias); }
                }
                if (ok == 0) continue;
                if (ok < Slots) Array.Resize(ref pool, ok);
                variants.Add(pool);
                loaded++;
            }
            CmxPlugin.Log?.LogInfo("CarMax loaded " + loaded + " sounds.");
        }

        private static byte[] Scale(string path, float gain)
        {
            byte[] d;
            try { d = System.IO.File.ReadAllBytes(path); } catch { return null; }
            if (d.Length < 44 || Encoding.ASCII.GetString(d, 0, 4) != "RIFF") return null;
            int pos = 12; short fmt = 0, bits = 0;
            while (pos + 8 <= d.Length)
            {
                string id = Encoding.ASCII.GetString(d, pos, 4);
                int size = BitConverter.ToInt32(d, pos + 4), body = pos + 8;
                if (size < 0) break;
                if (id == "fmt " && body + 16 <= d.Length) { fmt = BitConverter.ToInt16(d, body); bits = BitConverter.ToInt16(d, body + 14); }
                else if (id == "data")
                {
                    if (fmt == 1 && bits == 16)
                    {
                        int end = Math.Min(body + size, d.Length);
                        for (int i = body; i + 1 < end; i += 2)
                        {
                            int v = Mathf.Clamp((int)Math.Round(BitConverter.ToInt16(d, i) * gain), short.MinValue, short.MaxValue);
                            d[i] = (byte)(v & 0xFF);
                            d[i + 1] = (byte)((v >> 8) & 0xFF);
                        }
                    }
                    break;
                }
                pos = body + size + (size & 1);
            }
            return d;
        }

        public static void Play(string key)
        {
            if (_broken || string.IsNullOrEmpty(key) || !Sounds.TryGetValue(key, out List<string[]> variants) || variants.Count == 0) return;
            string[] pool = variants[UnityEngine.Random.Range(0, variants.Count)];
            NextSlot.TryGetValue(key, out int slot);
            NextSlot[key] = slot + 1;
            Send("play " + pool[slot % pool.Length] + " from 0");
        }
    }

    // ================================================================ icons from the Icons folder

    public static class CmxIcons
    {
        public static string Folder;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Texture2D Texture(string name)
        {
            if (Textures.TryGetValue(name, out Texture2D tex)) return tex;
            tex = null;
            try
            {
                string path = Folder != null ? System.IO.Path.Combine(Folder, name + ".png") : null;
                if (path != null && System.IO.File.Exists(path))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path))) { tex.name = "CarMax_" + name; tex.wrapMode = TextureWrapMode.Clamp; }
                    else tex = null;
                }
            }
            catch (Exception e) { CmxPlugin.Log?.LogWarning("Icon " + name + ": " + e.Message); tex = null; }
            Textures[name] = tex;
            return tex;
        }

        public static Sprite Get(string name, Sprite fallback)
        {
            if (Sprites.TryGetValue(name, out Sprite s) && s) return s;
            Texture2D tex = Texture(name);
            if (!tex) return fallback;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "CarMax_" + name;
            Sprites[name] = s;
            return s;
        }
    }
}

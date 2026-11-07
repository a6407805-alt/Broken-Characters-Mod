using System.Reflection;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Surveyor
{
    /// <summary>
    /// v0.7.2: the F (visor / LiDAR) key gets its own button on the skill bar, next to M1, built from a copy of the game's
    /// own skill icon. It lights cyan while LiDAR is on and green while the visor is open.
    /// </summary>
    public class SvrFSlot : MonoBehaviour
    {
        private class Slot { public GameObject Go; public Image Icon; public Component Key; }
        private Slot _f, _q;
        private float _nextTry;
        private SvrLidar _lidar;

        private void Awake() { _lidar = GetComponent<SvrLidar>(); }

        private void Update()
        {
            if (!_lidar) return;
            if (_f == null || !_f.Go)
            {
                if (Time.time < _nextTry) return;
                _nextTry = Time.time + 1f;
                try { Make(); } catch (System.Exception e) { SvrPlugin.Log.LogWarning("Q/F skill buttons: " + e.Message); _nextTry = Time.time + 10f; }
                return;
            }
            bool peek = _lidar.Peeking;
            if (_f.Icon) _f.Icon.color = peek ? new Color(0.55f, 0.6f, 0.65f, 1f) : new Color(0.55f, 0.95f, 1f, 1f);
            if (_q != null && _q.Icon) _q.Icon.color = Color.Lerp(Color.white, SvrLidar.ModeColorOf(SvrLidar.OdMode), 0.6f);
            // the copied key label can't draw our letters (it is a glyph box): blank it, OnGUI writes Q and F
            SetText(_f.Key, "");
            if (_q != null) SetText(_q.Key, "");
        }

        private void Make()
        {
            HUD hud = null;
            foreach (HUD h in HUD.readOnlyInstanceList)
                if (h && h.targetBodyObject == gameObject) { hud = h; break; }
            if (!hud || hud.skillIcons == null || hud.skillIcons.Length < 2 || !hud.skillIcons[0] || !hud.skillIcons[1]) return;
            SkillIcon m1 = hud.skillIcons[0], m2 = hud.skillIcons[1];
            _f = Clone(m1, m2, 1, "peek", "F");
            _q = Clone(m1, m2, 2, "special", "Q");
        }

        /// <summary>A copy of the M1 button, `steps` slots further out than M1 (away from M2), showing our own icon and key.</summary>
        private static Slot Clone(SkillIcon m1, SkillIcon m2, int steps, string icon, string key)
        {
            GameObject clone = Instantiate(m1.gameObject, m1.transform.parent);
            clone.name = "SurveyorSlot_" + key;
            Slot s = new Slot { Go = clone };
            SkillIcon si = clone.GetComponent<SkillIcon>();
            s.Icon = si ? si.iconImage : null;
            if (si) DestroyImmediate(si);
            // the key label is rewritten every frame by the game's input / language helpers: remove them from the copy
            foreach (Component c in clone.GetComponentsInChildren<Component>(true))
            {
                if (!c) continue;
                string tn = c.GetType().Name;
                if (tn.Contains("InputBindingDisplay") || tn.Contains("LanguageTextMesh") || tn.Contains("TooltipProvider")) DestroyImmediate(c);
            }
            Component best = null;
            int bestLen = int.MaxValue;
            foreach (Component c in clone.GetComponentsInChildren<Component>(true))
            {
                if (!c || !c.GetType().Name.Contains("TextMeshPro")) continue;
                string t = GetText(c);
                if (!string.IsNullOrEmpty(t) && t.Length < bestLen) { bestLen = t.Length; best = c; }
            }
            foreach (Component c in clone.GetComponentsInChildren<Component>(true))
                if (c && c != best && c.GetType().Name.Contains("TextMeshPro")) SetText(c, "");
            s.Key = best;
            SetText(best, key);
            foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (t != clone.transform && (n.Contains("cooldown") || n.Contains("stock") || n.Contains("flash") || n.Contains("remap") || n.Contains("isready")))
                    t.gameObject.SetActive(false);
            }
            if (s.Icon)
            {
                Sprite sp = SvrIcons.Get(icon, null);
                if (sp) s.Icon.sprite = sp;
                s.Icon.enabled = true;
            }
            RectTransform r1 = m1.transform as RectTransform, r2 = m2.transform as RectTransform, rc = clone.transform as RectTransform;
            if (r1 && r2 && rc)
            {
                rc.anchorMin = r1.anchorMin; rc.anchorMax = r1.anchorMax; rc.pivot = r1.pivot;
                rc.anchoredPosition = r1.anchoredPosition - (r2.anchoredPosition - r1.anchoredPosition) * steps;
            }
            LayoutGroup lg = clone.transform.parent ? clone.transform.parent.GetComponent<LayoutGroup>() : null;
            if (lg) clone.transform.SetSiblingIndex(Mathf.Max(0, m1.transform.GetSiblingIndex()));
            clone.SetActive(true);
            return s;
        }

        private GUIStyle _keyStyle;
        private readonly Vector3[] _corners = new Vector3[4];

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawKey(_f, "F");
            DrawKey(_q, "Q");
        }

        private void DrawKey(Slot s, string key)
        {
            if (s == null || !s.Go || !s.Go.activeInHierarchy) return;
            RectTransform rt = s.Go.transform as RectTransform;
            if (!rt) return;
            Canvas c = rt.GetComponentInParent<Canvas>();
            Camera cam = c && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            rt.GetWorldCorners(_corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);   // bottom-left
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);   // top-right
            float w = b.x - a.x, h = b.y - a.y;
            if (w <= 2f) return;
            if (_keyStyle == null) _keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _keyStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(h * 0.36f));
            Rect r = new Rect(a.x - 10f, Screen.height - a.y + h * 0.04f, w + 20f, h * 0.5f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), key, _keyStyle);
            GUI.color = Color.white;
            GUI.Label(r, key, _keyStyle);
            GUI.color = old;
        }

        private static string GetText(Component c)
        {
            if (!c) return null;
            PropertyInfo p = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            return p != null ? p.GetValue(c, null) as string : null;
        }

        private static void SetText(Component c, string v)
        {
            if (!c) return;
            PropertyInfo p = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (p != null && p.CanWrite && (p.GetValue(c, null) as string) != v) p.SetValue(c, v, null);
        }

        private void OnDestroy()
        {
            if (_f != null && _f.Go) Destroy(_f.Go);
            if (_q != null && _q.Go) Destroy(_q.Go);
        }
    }
}

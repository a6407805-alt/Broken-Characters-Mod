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
        private GameObject _slot;
        private Image _icon;
        private Component _keyText;
        private HUD _hud;
        private float _nextTry;
        private SvrLidar _lidar;

        private void Awake() { _lidar = GetComponent<SvrLidar>(); }

        private void Update()
        {
            if (!_lidar) return;
            if (!_slot)
            {
                if (Time.time < _nextTry) return;
                _nextTry = Time.time + 1f;
                try { Make(); } catch (System.Exception e) { SvrPlugin.Log.LogWarning("F skill button: " + e.Message); _nextTry = Time.time + 10f; }
                return;
            }
            bool peek = _lidar.Peeking;
            if (_icon) _icon.color = peek ? new Color(0.45f, 1f, 0.6f, 1f) : new Color(0.55f, 0.95f, 1f, 1f);
            SetText(_keyText, "F");
        }

        private void Make()
        {
            _hud = null;
            foreach (HUD h in HUD.readOnlyInstanceList)
                if (h && h.targetBodyObject == gameObject) { _hud = h; break; }
            if (!_hud || _hud.skillIcons == null || _hud.skillIcons.Length < 2 || !_hud.skillIcons[0] || !_hud.skillIcons[1]) return;

            SkillIcon m1 = _hud.skillIcons[0], m2 = _hud.skillIcons[1];
            GameObject clone = Instantiate(m1.gameObject, m1.transform.parent);
            clone.name = "SurveyorFSlot";
            SkillIcon si = clone.GetComponent<SkillIcon>();
            _icon = si ? si.iconImage : null;
            if (si) DestroyImmediate(si);
            // find the key label: the shortest text on the copied icon (usually "M1")
            Component best = null;
            int bestLen = int.MaxValue;
            foreach (Component c in clone.GetComponentsInChildren<Component>(true))
            {
                if (!c || !c.GetType().Name.Contains("TextMeshPro")) continue;
                string t = GetText(c);
                if (t == null) continue;
                if (t.Length > 0 && t.Length < bestLen) { bestLen = t.Length; best = c; }
                else if (t.Length == 0) continue;
            }
            foreach (Component c in clone.GetComponentsInChildren<Component>(true))
                if (c && c != best && c.GetType().Name.Contains("TextMeshPro")) SetText(c, "");
            _keyText = best;
            // hide the cooldown / stock / flash overlays we can't drive
            foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (t != clone.transform && (n.Contains("cooldown") || n.Contains("stock") || n.Contains("flash") || n.Contains("remap") || n.Contains("isready")))
                    t.gameObject.SetActive(false);
            }
            if (_icon)
            {
                Sprite s = SvrIcons.Get("peek", null);
                if (s) _icon.sprite = s;
                _icon.enabled = true;
            }
            // one slot further out than M1, on the side away from M2
            RectTransform r1 = m1.transform as RectTransform, r2 = m2.transform as RectTransform, rc = clone.transform as RectTransform;
            if (r1 && r2 && rc)
            {
                rc.anchoredPosition = r1.anchoredPosition - (r2.anchoredPosition - r1.anchoredPosition);
                rc.anchorMin = r1.anchorMin; rc.anchorMax = r1.anchorMax; rc.pivot = r1.pivot;
            }
            LayoutGroup lg = clone.transform.parent ? clone.transform.parent.GetComponent<LayoutGroup>() : null;
            if (lg) clone.transform.SetSiblingIndex(m1.transform.GetSiblingIndex());
            clone.SetActive(true);
            _slot = clone;
        }

        private static string GetText(Component c)
        {
            PropertyInfo p = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            return p != null ? p.GetValue(c, null) as string : null;
        }

        private static void SetText(Component c, string v)
        {
            if (!c) return;
            PropertyInfo p = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (p != null && p.CanWrite && (p.GetValue(c, null) as string) != v) p.SetValue(c, v, null);
        }

        private void OnDestroy() { if (_slot) Destroy(_slot); }
    }
}

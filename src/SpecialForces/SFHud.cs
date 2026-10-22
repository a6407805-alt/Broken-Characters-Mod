using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpecialForces
{
    // Special Forces HUD (every machine): the control tower's health at the top of the screen, the wave and the time
    // to the next one, Storm Nests left, the build panel (build mode) and short notifications. The tower bar flashes
    // red and an alarm sounds when the tower is hit.
    internal static class SFHud
    {
        public static int Wave, NestsAlive, NestsTotal;
        public static float NextWave, StateTime;
        private static string note = "";
        private static bool noteOk;
        private static float noteUntil;

        public static void Begin()
        {
            Wave = 0;
            NextWave = 0;
            NestsAlive = NestsTotal = 0;
            if (!Object.FindObjectOfType<SFHudView>()) new GameObject("SFHud").AddComponent<SFHudView>();
        }

        public static void Notify(string text, bool ok)
        {
            note = text;
            noteOk = ok;
            noteUntil = Time.unscaledTime + 2.5f;
        }

        public static string Note => Time.unscaledTime < noteUntil ? note : "";
        public static bool NoteOk => noteOk;
    }

    public class SFHudView : MonoBehaviour
    {
        private HUD hud;
        private GameObject root;
        private RectTransform fill;
        private Image fillImage;
        private TextMeshProUGUI towerText, waveText, buildText, noteText;
        private float lastTowerHealth = -1f, flashUntil;

        private void Update()
        {
            if (!SFScene.Active) { Destroy(gameObject); return; }
            if (!hud)
            {
                foreach (var h in HUD.readOnlyInstanceList) { hud = h; break; }
                if (!hud || !hud.mainContainer) return;
                Build();
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (root) Destroy(root);
        }

        private static TextMeshProUGUI Text(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<HGTextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }

        private void Build()
        {
            root = new GameObject("SpecialForcesHud", typeof(RectTransform));
            root.transform.SetParent(hud.mainContainer.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Tower bar: top centre.
            var barBack = new GameObject("TowerBar", typeof(RectTransform));
            barBack.transform.SetParent(root.transform, false);
            var back = (RectTransform)barBack.transform;
            back.anchorMin = new Vector2(0.35f, 0.935f);
            back.anchorMax = new Vector2(0.65f, 0.955f);
            back.offsetMin = back.offsetMax = Vector2.zero;
            var backImage = barBack.AddComponent<Image>();
            backImage.color = new Color(0f, 0f, 0f, 0.55f);
            backImage.raycastTarget = false;
            var fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(barBack.transform, false);
            fill = (RectTransform)fillObject.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(-2, -2);
            fill.pivot = new Vector2(0f, 0.5f);
            fillImage = fillObject.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            fillImage.raycastTarget = false;

            towerText = Text(root.transform, "TowerText", new Vector2(0.3f, 0.955f), new Vector2(0.7f, 0.985f), 20, TextAlignmentOptions.Center);
            waveText = Text(root.transform, "WaveText", new Vector2(0.3f, 0.905f), new Vector2(0.7f, 0.935f), 18, TextAlignmentOptions.Center);
            buildText = Text(root.transform, "BuildText", new Vector2(0.25f, 0.13f), new Vector2(0.75f, 0.3f), 18, TextAlignmentOptions.Bottom);
            buildText.enableWordWrapping = true;
            noteText = Text(root.transform, "Note", new Vector2(0.25f, 0.32f), new Vector2(0.75f, 0.36f), 22, TextAlignmentOptions.Center);
        }

        private void Refresh()
        {
            if (!root) return;
            root.SetActive(!SFCutscene.Playing);
            var tower = SFStructure.Tower;
            float health = 0f, max = 1f;
            if (tower && tower.Health)
            {
                health = tower.Health.health;
                max = Mathf.Max(1f, tower.Health.fullHealth);
            }
            if (lastTowerHealth >= 0f && health < lastTowerHealth - 1f)
            {
                flashUntil = Time.unscaledTime + 0.4f;
                SFAudio.Play(SFAudio.Clip.TowerAlarm);
            }
            lastTowerHealth = health;
            fill.anchorMax = new Vector2(Mathf.Clamp01(health / max), 1f);
            fillImage.color = Time.unscaledTime < flashUntil ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(0.3f, 0.85f, 1f, 0.9f);
            towerText.text = tower ? $"{Tokens.Get("HUD_TOWER")}  {Mathf.CeilToInt(health):N0} / {Mathf.CeilToInt(max):N0}" : Tokens.Get("HUD_TOWER_DOWN");

            float next = Mathf.Max(0f, SFHud.NextWave - (Time.unscaledTime - SFHud.StateTime));
            string waveLine = SFHud.Wave == 0 ? string.Format(Tokens.Get("HUD_FIRST_WAVE"), Clock(next)) : string.Format(Tokens.Get("HUD_WAVE"), SFHud.Wave, Clock(next));
            if (SFHud.NestsTotal > 0) waveLine += "   " + string.Format(Tokens.Get("HUD_NESTS"), SFHud.NestsAlive, SFHud.NestsTotal);
            waveText.text = waveLine;

            if (SFBuilding.Active)
            {
                var sb = new StringBuilder();
                sb.Append(Tokens.Get("HUD_BUILD_TITLE")).Append('\n');
                foreach (var k in SFBuilding.Kinds)
                {
                    bool sel = k == SFBuilding.Selected;
                    sb.Append(sel ? "<color=#7fe0ff>> " : "<color=#c8c8c8>  ")
                      .Append((int)k + 1).Append("  ").Append(Tokens.Get("BUILD_" + k.ToString().ToUpperInvariant()))
                      .Append("  $").Append(SFBuilding.Cost(k))
                      .Append("  (").Append(SFBuilding.MyCounts[(int)k]).Append('/').Append(SFBuilding.Limit(k)).Append(")</color>  ");
                    if ((int)k == 2) sb.Append('\n');
                }
                sb.Append('\n').Append(string.Format(Tokens.Get("HUD_BUILD_KEYS"), SFConfig.PlaceKey.Value, SFConfig.RotateKey.Value, SFConfig.BuildKey.Value));
                if (!SFBuilding.Valid && !string.IsNullOrEmpty(SFBuilding.Problem)) sb.Append("\n<color=#ff8070>").Append(SFBuilding.Problem).Append("</color>");
                buildText.text = sb.ToString();
            }
            else buildText.text = string.Format(Tokens.Get("HUD_BUILD_HINT"), SFConfig.BuildKey.Value);

            string n = SFHud.Note;
            noteText.text = n;
            noteText.color = SFHud.NoteOk ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.6f, 0.5f);
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}

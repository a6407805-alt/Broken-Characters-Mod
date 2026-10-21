using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace DuelistMod
{
    // ======================================================================== card-game numbers

    /// <summary>
    /// Converts Risk of Rain numbers to the card game's scale. 1000 ATK is a monster card as strong as
    /// your own MonsterAtk coefficient (about a Gunner Drone); 8000 LP is a full health bar.
    /// Calibrated once at the start of each duel from your stats, so items and levels still matter.
    /// </summary>
    public static class DuelStats
    {
        public const int StartLP = 8000;
        public static float RefDamage = 30f;
        public static float RefHp = 33f;

        public static void Calibrate(CharacterBody p)
        {
            if (!p) return;
            RefDamage = Mathf.Max(1f, p.damage * DuelConfig.MonsterAtkCoeff.Value);
            RefHp = Mathf.Max(1f, (p.healthComponent ? p.healthComponent.fullHealth : 110f) * DuelConfig.MonsterHpFraction.Value);
        }

        public static int Round50(float v) => v <= 0f ? 0 : Mathf.RoundToInt(v / 50f) * 50;

        public static int EnemyAtk(float realDamage) => Mathf.Clamp(Round50(1000f * realDamage * DuelConfig.EnemyAtkScale.Value / RefDamage), 100, 9950);

        public static int EnemyDef(float realHealth) => Mathf.Clamp(Round50(1000f * Mathf.Sqrt(Mathf.Max(0f, realHealth) / RefHp) * DuelConfig.EnemyDefScale.Value), 100, 9950);

        public static int AllyAtk(float summedDamage) => Mathf.Clamp(Round50(1000f * summedDamage * DuelConfig.AllyAtkCoeff.Value / RefDamage), 0, 9950);

        public static int AllyDef(float summedHealth) => Mathf.Clamp(Round50(1000f * Mathf.Sqrt(Mathf.Max(0f, summedHealth) / RefHp) * DuelConfig.EnemyDefScale.Value), 0, 9950);

        /// <summary>Life points shown for a health bar: 8000 when full.</summary>
        public static int LifePoints(float current, float full) => full <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(StartLP * current / full), 0, 99999);

        /// <summary>Real damage for an LP amount against a health bar.</summary>
        public static float RealFromLP(int lp, float full) => lp / (float)StartLP * full;
    }

    // ======================================================================== sounds

    public enum DuelSound { Draw, Summon, SpecialSummon, Set, Flip, Position, Phase, Attack, Impact, Destroy, DirectHit, LifePoints, Activate, Chain, Negate, Victory, Defeat }

    /// <summary>
    /// One sound per duel event. A .wav in a "sounds" folder next to the mod (draw.wav, summon.wav …)
    /// plays instead of the game sound named in the config.
    /// </summary>
    public static class DuelSfx
    {
        private static bool _loaded;
        private static readonly Dictionary<DuelSound, AudioClip> _clips = new Dictionary<DuelSound, AudioClip>();
        private static AudioSource _source;
        private static float _lastTick;

        public static string DefaultEvent(DuelSound s)
        {
            switch (s)
            {
                case DuelSound.Draw: return "Play_UI_menuClick";
                case DuelSound.Summon: return "Play_UI_shrineActivate";
                case DuelSound.SpecialSummon: return "Play_UI_teleporter_activate";
                case DuelSound.Set: return "Play_UI_menuClick";
                case DuelSound.Flip: return "Play_UI_menuClick";
                case DuelSound.Position: return "Play_UI_menuClick";
                case DuelSound.Phase: return "Play_UI_menuClick";
                case DuelSound.Attack: return "Play_merc_sword_swing";
                case DuelSound.Impact: return "Play_merc_sword_impact";
                case DuelSound.Destroy: return "Play_commando_M2_grenade_explo";
                case DuelSound.DirectHit: return "Play_commando_M2_grenade_explo";
                case DuelSound.LifePoints: return "Play_UI_menuHover";
                case DuelSound.Activate: return "Play_UI_shrineActivate";
                case DuelSound.Chain: return "Play_UI_menuClick";
                case DuelSound.Negate: return "Play_UI_chest_unlock";
                case DuelSound.Victory: return "Play_UI_achievementUnlock";
                case DuelSound.Defeat: return "Play_UI_menuClick";
            }
            return "";
        }

        public static void Play(DuelSound s, GameObject at)
        {
            try
            {
                Load();
                if (_clips.TryGetValue(s, out AudioClip clip) && clip && _source)
                {
                    _source.PlayOneShot(clip, Mathf.Clamp01(DuelConfig.SoundVolume.Value));
                    return;
                }
                string ev = DuelConfig.GameSounds.TryGetValue(s, out var entry) ? entry.Value : DefaultEvent(s);
                if (!string.IsNullOrEmpty(ev) && at) Util.PlaySound(ev, at);
            }
            catch { }
        }

        /// <summary>The LP counter's tick, at most every 60 ms.</summary>
        public static void Tick(GameObject at)
        {
            if (Time.unscaledTime - _lastTick < 0.06f) return;
            _lastTick = Time.unscaledTime;
            Play(DuelSound.LifePoints, at);
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            string dir = SoundsFolder();
            if (dir == null) return;
            foreach (DuelSound s in (DuelSound[])Enum.GetValues(typeof(DuelSound)))
            {
                string f = Path.Combine(dir, s.ToString().ToLowerInvariant() + ".wav");
                if (!File.Exists(f)) continue;
                AudioClip c = LoadWav(f);
                if (c) _clips[s] = c;
                else DuelistPlugin.Log.LogWarning("Couldn't read " + f + " (needs a PCM or float .wav).");
            }
            if (_clips.Count > 0)
            {
                GameObject go = new GameObject("DuelistSfx");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                _source.ignoreListenerPause = true;
                DuelistPlugin.Log.LogInfo($"Duel sounds: {_clips.Count} custom .wav file(s) from {dir}.");
            }
        }

        private static string SoundsFolder()
        {
            string dir = Path.GetDirectoryName(typeof(DuelSfx).Assembly.Location);
            for (int up = 0; up < 5 && !string.IsNullOrEmpty(dir); up++)
            {
                string p = Path.Combine(dir, "sounds");
                if (Directory.Exists(p)) return p;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        /// <summary>Reads 8/16/24/32-bit PCM or 32-bit float WAV into an AudioClip.</summary>
        private static AudioClip LoadWav(string path)
        {
            try
            {
                byte[] b = File.ReadAllBytes(path);
                if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE") return null;
                int pos = 12, channels = 0, rate = 0, bits = 0, format = 0, dataPos = -1, dataLen = 0;
                while (pos + 8 <= b.Length)
                {
                    string id = Encoding.ASCII.GetString(b, pos, 4);
                    int len = BitConverter.ToInt32(b, pos + 4);
                    int body = pos + 8;
                    if (id == "fmt ")
                    {
                        format = BitConverter.ToUInt16(b, body);
                        channels = BitConverter.ToUInt16(b, body + 2);
                        rate = BitConverter.ToInt32(b, body + 4);
                        bits = BitConverter.ToUInt16(b, body + 14);
                        if (format == 0xFFFE && len >= 26) format = BitConverter.ToUInt16(b, body + 24);
                    }
                    else if (id == "data")
                    {
                        dataPos = body;
                        dataLen = Math.Min(len, b.Length - body);
                        break;
                    }
                    if (len < 0) return null;
                    pos = body + len + (len & 1);
                }
                if (dataPos < 0 || channels <= 0 || rate <= 0) return null;
                int bytesPer = bits / 8;
                if (bytesPer <= 0) return null;
                int count = dataLen / bytesPer;
                float[] data = new float[count];
                for (int i = 0; i < count; i++)
                {
                    int o = dataPos + i * bytesPer;
                    if (format == 3 && bits == 32) data[i] = BitConverter.ToSingle(b, o);
                    else if (bits == 8) data[i] = (b[o] - 128) / 128f;
                    else if (bits == 16) data[i] = BitConverter.ToInt16(b, o) / 32768f;
                    else if (bits == 24) data[i] = ((b[o] << 8 | b[o + 1] << 16 | b[o + 2] << 24) >> 8) / 8388608f;
                    else if (bits == 32) data[i] = BitConverter.ToInt32(b, o) / 2147483648f;
                }
                int frames = count / channels;
                if (frames <= 0) return null;
                AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), frames, channels, rate, false);
                clip.SetData(data, 0);
                return clip;
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("WAV load failed for " + path + ": " + e.Message);
                return null;
            }
        }
    }

    // ======================================================================== animation

    /// <summary>Moves / turns / scales a board object from where it starts to where it was put.</summary>
    public class DuelTween : MonoBehaviour
    {
        public Vector3 FromPos, ToPos, FromScale, ToScale;
        public Quaternion FromRot, ToRot;
        public float Delay, Duration = 0.3f;
        public bool Overshoot;
        private float _t;

        /// <summary>Animates go from (its current local transform + the given offsets) to its current local transform.</summary>
        public static DuelTween From(GameObject go, Vector3 posOffset, float scaleFrom, Vector3 eulerFrom, float duration, float delay = 0f, bool overshoot = false)
        {
            if (!go) return null;
            foreach (DuelTween old in go.GetComponents<DuelTween>()) { old.Finish(); Destroy(old); }
            Transform t = go.transform;
            DuelTween tw = go.AddComponent<DuelTween>();
            tw.ToPos = t.localPosition;
            tw.ToRot = t.localRotation;
            tw.ToScale = t.localScale;
            tw.FromPos = t.localPosition + posOffset;
            tw.FromRot = t.localRotation * Quaternion.Euler(eulerFrom);
            tw.FromScale = t.localScale * scaleFrom;
            tw.Duration = Mathf.Max(0.01f, duration);
            tw.Delay = delay;
            tw.Overshoot = overshoot;
            tw.Apply(0f);
            return tw;
        }

        public void Finish() => Apply(1f);

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01((_t - Delay) / Duration);
            Apply(k);
            if (k >= 1f) Destroy(this);
        }

        private void Apply(float k)
        {
            float e = Overshoot ? EaseBack(k) : 1f - (1f - k) * (1f - k) * (1f - k);
            Transform t = transform;
            t.localPosition = Vector3.LerpUnclamped(FromPos, ToPos, e);
            t.localRotation = Quaternion.SlerpUnclamped(FromRot, ToRot, Mathf.Clamp01(e));
            t.localScale = Vector3.LerpUnclamped(FromScale, ToScale, e);
        }

        private static float EaseBack(float k)
        {
            const float c = 1.9f;
            float x = k - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }

    /// <summary>Something that grows, holds and shrinks away (light pillars, shockwave rings).</summary>
    public class DuelPulse : MonoBehaviour
    {
        public Vector3 S0, Peak, S2;
        public float Life = 0.8f;
        public float Spin;
        private float _t;

        private void Update()
        {
            _t += Time.deltaTime;
            float k = _t / Life;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.localScale = k < 0.3f ? Vector3.Lerp(S0, Peak, k / 0.3f) : Vector3.Lerp(Peak, S2, (k - 0.3f) / 0.7f);
            if (Spin != 0f) transform.Rotate(Vector3.up, Spin * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>A destroyed card/monster: shrinks and spins away while shards fly out.</summary>
    public class DuelShatter : MonoBehaviour
    {
        private readonly List<Transform> _shards = new List<Transform>();
        private readonly List<Vector3> _vel = new List<Vector3>();
        private readonly List<Vector3> _spin = new List<Vector3>();
        private Vector3 _scale0;
        private float _t;
        private const float Life = 0.9f;

        public void Begin(Color color, int count, float size)
        {
            _scale0 = transform.localScale;
            Material m = DuelistVisuals.MakeMaterial(color, color * 1.6f);
            Vector3 c = transform.position + Vector3.up * 0.6f;
            for (int i = 0; i < count; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "Shard";
                Collider col = s.GetComponent<Collider>();
                if (col) Object.DestroyImmediate(col);
                s.GetComponent<Renderer>().sharedMaterial = m;
                s.transform.position = c + UnityEngine.Random.insideUnitSphere * size * 0.4f;
                s.transform.rotation = UnityEngine.Random.rotation;
                s.transform.localScale = new Vector3(UnityEngine.Random.Range(0.12f, 0.32f), 0.04f, UnityEngine.Random.Range(0.12f, 0.4f)) * size;
                _shards.Add(s.transform);
                Vector3 v = UnityEngine.Random.insideUnitSphere * 5f * size;
                v.y = Mathf.Abs(v.y) + 3.5f * size;
                _vel.Add(v);
                _spin.Add(UnityEngine.Random.insideUnitSphere * 720f);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            float k = Mathf.Clamp01(_t / Life);
            transform.localScale = _scale0 * Mathf.Max(0f, 1f - k * 2.2f);
            transform.Rotate(Vector3.up, 540f * dt, Space.World);
            for (int i = 0; i < _shards.Count; i++)
            {
                Transform s = _shards[i];
                if (!s) continue;
                _vel[i] += Vector3.down * 14f * dt;
                s.position += _vel[i] * dt;
                s.Rotate(_spin[i] * dt);
                s.localScale *= 1f - dt * 1.4f;
            }
            if (k >= 1f)
            {
                foreach (Transform s in _shards) if (s) Destroy(s.gameObject);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Board effects shared by the duel: summon pillars, shockwaves, destruction.</summary>
    public static class DuelFx
    {
        private static readonly Dictionary<int, Material> _glowMats = new Dictionary<int, Material>();

        private static Material Glow(Color c)
        {
            int key = ((int)(c.r * 31) << 10) | ((int)(c.g * 31) << 5) | (int)(c.b * 31);
            if (!_glowMats.TryGetValue(key, out Material m) || !m)
                _glowMats[key] = m = DuelistVisuals.MakeMaterial(c, c * 3f);
            return m;
        }

        /// <summary>A pillar of light rising from a zone (summons, activations).</summary>
        public static void Pillar(Transform board, Vector3 world, Color color, float height = 7f, float radius = 1.2f)
        {
            if (!board) return;
            GameObject p = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "SummonPillar", board, board.InverseTransformPoint(world) + Vector3.up * height * 0.5f, Vector3.one, Glow(color));
            DuelPulse pu = p.AddComponent<DuelPulse>();
            pu.S0 = new Vector3(radius * 0.15f, height * 0.5f, radius * 0.15f);
            pu.Peak = new Vector3(radius, height * 0.5f, radius);
            pu.S2 = new Vector3(0f, height * 0.6f, 0f);
            pu.Life = 0.75f;
            p.transform.localScale = pu.S0;
        }

        /// <summary>A flat ring that rushes outward across the mat.</summary>
        public static void Shockwave(Transform board, Vector3 world, Color color, float radius = 4f)
        {
            if (!board) return;
            GameObject r = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Shockwave", board, board.InverseTransformPoint(world) + Vector3.up * 0.12f, Vector3.one, Glow(color));
            DuelPulse pu = r.AddComponent<DuelPulse>();
            pu.S0 = new Vector3(0.3f, 0.01f, 0.3f);
            pu.Peak = new Vector3(radius * 0.6f, 0.012f, radius * 0.6f);
            pu.S2 = new Vector3(radius, 0.002f, radius);
            pu.Life = 0.5f;
            r.transform.localScale = pu.S0;
        }

        /// <summary>Breaks an object apart: it detaches, shrinks and spins away while shards fly out.</summary>
        public static void Shatter(GameObject obj, Color color, float size = 1.6f)
        {
            if (!obj) return;
            foreach (DuelTween tw in obj.GetComponentsInChildren<DuelTween>()) Object.Destroy(tw);
            obj.transform.SetParent(null, true);
            DuelShatter s = obj.AddComponent<DuelShatter>();
            s.Begin(color, 14, size);
        }

        // ---- the classic moves

        /// <summary>A card drops onto its zone with a half spin, then the monster grows out of it.</summary>
        public static void SummonIn(GameObject card, GameObject statue, Transform board, Color color, bool special)
        {
            DuelTween.From(card, Vector3.up * 3.2f, 0.35f, new Vector3(0f, 180f, 0f), 0.32f);
            if (statue) DuelTween.From(statue, Vector3.zero, 0.01f, Vector3.zero, 0.45f, 0.28f, true);   // Tick keeps statues facing forward, so only scale
            if (card && board) Pillar(board, card.transform.position, color, special ? 10f : 7f, special ? 1.6f : 1.15f);
        }

        /// <summary>A card slides in from your side of the table, face-down.</summary>
        public static void SetIn(GameObject card)
        {
            DuelTween.From(card, new Vector3(0f, 0.6f, -4.5f), 0.8f, new Vector3(0f, 25f, 0f), 0.3f);
        }

        /// <summary>The flip: the new face grows out of the card's edge.</summary>
        public static void FlipIn(GameObject card)
        {
            if (!card) return;
            DuelTween tw = DuelTween.From(card, Vector3.up * 0.8f, 1f, Vector3.zero, 0.28f);
            if (tw) tw.FromScale = new Vector3(card.transform.localScale.x * 0.02f, card.transform.localScale.y, card.transform.localScale.z);
        }

        /// <summary>Turns a card between attack (upright) and defense (sideways).</summary>
        public static void Turn(GameObject card, float fromYaw)
        {
            if (!card) return;
            DuelTween tw = DuelTween.From(card, Vector3.up * 0.3f, 1f, Vector3.zero, 0.25f);
            if (tw) tw.FromRot = Quaternion.Euler(0f, fromYaw, 0f);
        }

        /// <summary>An enemy monster rises out of the mat onto its zone.</summary>
        public static void RiseUp(GameObject stand, Transform board, Color color)
        {
            DuelTween.From(stand, Vector3.down * 2.6f, 0.5f, new Vector3(0f, -120f, 0f), 0.5f, 0f, true);
            if (stand && board) Pillar(board, stand.transform.position, color, 8f, 1.3f);
        }

        /// <summary>A set card stands up and shows its face before it resolves.</summary>
        public static void Activate(GameObject card, Transform board, Color color)
        {
            if (!card) return;
            DuelTween.From(card, Vector3.zero, 1f, new Vector3(-70f, 0f, 0f), 0.25f);
            if (board) { Pillar(board, card.transform.position, color, 5f, 1f); Shockwave(board, card.transform.position, color, 3f); }
        }
    }

    // ======================================================================== the mat

    /// <summary>Lays the printed playmat (duel_mat.jpg + duel_mat_glow.png) over the board.</summary>
    public static class DuelMat
    {
        // must match Blender/make_mat.py
        public const float X0 = -15.4f, X1 = 15.4f, Z0 = -2.8f, Z1 = 26.4f;

        private static bool _tried;
        private static Texture2D _tex, _glow;

        public static bool Lay(Transform board)
        {
            if (!DuelConfig.UsePlaymat.Value || !board) return false;
            if (!Load()) return false;
            try
            {
                Material m = DuelistModel.PaletteMaterial(null, _tex, _glow);
                if (m.HasProperty("_EmPower")) m.SetFloat("_EmPower", DuelConfig.PlaymatGlow.Value);
                if (m.HasProperty("_SpecularStrength")) m.SetFloat("_SpecularStrength", 0.04f);
                m.name = "DuelistPlaymat";

                Vector3 centre = new Vector3((X0 + X1) * 0.5f, 0f, (Z0 + Z1) * 0.5f);
                Material rubber = DuelistVisuals.MakeMaterial(new Color(0.03f, 0.025f, 0.05f), Color.black);
                DuelistVisuals.Primitive(PrimitiveType.Cube, "MatBase", board, centre + new Vector3(0f, 0.012f, 0f), new Vector3(X1 - X0 + 0.12f, 0.024f, Z1 - Z0 + 0.12f), rubber);

                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Playmat";
                Collider c = q.GetComponent<Collider>();
                if (c) Object.DestroyImmediate(c);
                q.transform.SetParent(board, false);
                q.transform.localPosition = centre + new Vector3(0f, 0.026f, 0f);
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = new Vector3(X1 - X0, Z1 - Z0, 1f);
                q.GetComponent<Renderer>().sharedMaterial = m;
                return true;
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("Playmat failed, using the plain board: " + e.Message);
                return false;
            }
        }

        private static bool Load()
        {
            if (_tried) return _tex;
            _tried = true;
            string path = DuelistModel.FindFile("duel_mat.jpg") ?? DuelistModel.FindFile("duel_mat.png");
            if (path == null || path.EndsWith(".gz"))
            {
                DuelistPlugin.Log.LogInfo("No duel_mat.jpg next to the mod; using the plain board.");
                return false;
            }
            _tex = LoadTex(path, false);
            string gp = DuelistModel.FindFile("duel_mat_glow.png");
            _glow = gp != null && !gp.EndsWith(".gz") ? LoadTex(gp, false) : null;
            if (_tex) DuelistPlugin.Log.LogInfo($"Playmat loaded ({_tex.width}x{_tex.height}).");
            return _tex;
        }

        private static Texture2D LoadTex(string path, bool linear)
        {
            try
            {
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
                if (!t.LoadImage(File.ReadAllBytes(path))) return null;
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                t.anisoLevel = 8;
                t.name = Path.GetFileNameWithoutExtension(path);
                return t;
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("Couldn't load " + path + ": " + e.Message);
                return null;
            }
        }
    }
}

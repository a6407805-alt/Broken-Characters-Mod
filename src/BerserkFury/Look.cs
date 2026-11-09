using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace BerserkFury
{
    /// <summary>Colour schemes picked in the Loadout tab (one SkinDef per palette; BerserkFuryLook tints the model).</summary>
    public static class BerserkFuryPalettes
    {
        public struct Palette { public string Name; public Color Tint, Accent; }

        public static readonly Palette[] All =
        {
            new Palette { Name = "Berserk Fury",  Tint = new Color(1f, 1f, 1f),        Accent = new Color(1f, 0.25f, 0.85f) },
            new Palette { Name = "Jagd Crimson",  Tint = new Color(0.95f, 0.36f, 0.4f), Accent = new Color(1f, 0.2f, 0.1f) },
            new Palette { Name = "Abyss Navy",    Tint = new Color(0.5f, 0.62f, 1f),   Accent = new Color(0.2f, 0.85f, 1f) },
            new Palette { Name = "Solar Gold",    Tint = new Color(1f, 0.83f, 0.45f),  Accent = new Color(1f, 0.55f, 0.1f) },
            new Palette { Name = "Phantom Black", Tint = new Color(0.32f, 0.32f, 0.38f), Accent = new Color(0.95f, 0.1f, 0.25f) },
        };

        /// <summary>Adds one skin per palette (copies of the model's own default skin, so the game still has a bind pose).</summary>
        public static void AddSkins(Transform model, string tokenPrefix)
        {
            try
            {
                ModelSkinController msc = model ? model.GetComponent<ModelSkinController>() : null;
                FieldInfo sf = typeof(ModelSkinController).GetField("skins", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!msc || sf == null) return;
                SkinDef[] old = sf.GetValue(msc) as SkinDef[];
                if (old == null || old.Length == 0 || !old[0]) return;
                var skins = new SkinDef[All.Length];
                for (int i = 0; i < All.Length; i++)
                {
                    SkinDef s = UnityEngine.Object.Instantiate(old[0]);
                    ((ScriptableObject)s).name = "skinBerserkFury" + i;
                    Set(s, "nameToken", tokenPrefix + "SKIN_" + i);
                    Set(s, "icon", SwatchIcon(All[i]));
                    Set(s, "unlockableDef", null);
                    skins[i] = s;
                }
                sf.SetValue(msc, skins);
            }
            catch (Exception e) { BerserkFuryPlugin.Log.LogWarning("Skins skipped: " + e.Message); }
        }

        static void Set(object o, string name, object v)
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo f = o.GetType().GetField(name, any);
            if (f != null && (v == null || f.FieldType.IsInstanceOfType(v))) { f.SetValue(o, v); return; }
            PropertyInfo p = o.GetType().GetProperty(name, any);
            if (p != null && p.CanWrite && (v == null || p.PropertyType.IsInstanceOfType(v))) p.SetValue(o, v, null);
        }

        /// <summary>Round skin button: body colour with an accent claw stripe.</summary>
        static Sprite SwatchIcon(Palette p)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            Color body = new Color(0.78f, 0.74f, 0.86f) * p.Tint; body.a = 1f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - S / 2f + 0.5f, dy = y - S / 2f + 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = new Color(0, 0, 0, 0);
                    if (r < 60f)
                    {
                        c = Color.Lerp(body * 0.55f, body, (y / (float)S));
                        // three claw prongs
                        for (int k = -1; k <= 1; k++)
                            if (Mathf.Abs(dx - k * 22f - (dy * 0.25f)) < 5f && dy > -40f && dy < 30f) c = p.Accent;
                        c.a = 1f;
                        if (r > 56f) c = new Color(0.08f, 0.04f, 0.1f, 1f);
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>
    /// Per-model look: palette tint from the selected skin, accent glow on the Buster Claws (Overdrive, slashes,
    /// CPC charge), blade trails on the claw tips, muzzle/charge lights.
    /// </summary>
    public class BerserkFuryLook : MonoBehaviour
    {
        CharacterModel _model;
        ModelSkinController _msc;
        Transform _visual;
        CharacterBody _body;
        BerserkFuryPassiveController _passive;
        EntityStateMachine _weapon;
        Material _bodyMat, _accentMat;
        int _skin = -1;
        readonly List<Renderer> _accentR = new List<Renderer>(), _bodyR = new List<Renderer>();
        TrailRenderer _trailL, _trailR;
        Light _chest, _muzzleL, _muzzleR, _mouth;
        float _flash, _lastShot, _mouthFlashT;
        bool _built;
        static Material _trailMat;
        static readonly FieldInfo SkinIndexField = typeof(ModelSkinController).GetField("currentSkinIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        void Build()
        {
            _model = GetComponent<CharacterModel>();
            _msc = GetComponent<ModelSkinController>();
            _visual = transform.Find("BerserkFuryVisual");
            if (!_visual) return;
            _body = GetComponentInParent<CharacterBody>();
            if (_body) _passive = _body.GetComponent<BerserkFuryPassiveController>();

            foreach (Renderer r in _visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer)) continue;
                if (r.name.Contains("prong") || r.name.StartsWith("BF_claw")) _accentR.Add(r); else _bodyR.Add(r);
            }

            Transform prongL = Deep(_visual, "BF_prongTopL"), prongR = Deep(_visual, "BF_prongTopR");
            Transform head = Deep(_visual, "BF_head"), body = Deep(_visual, "BF_body");
            _trailL = Trail(prongL); _trailR = Trail(prongR);
            _muzzleL = MakeLight(Deep(_visual, "BF_clawL"), new Vector3(0f, 0f, 2.0f), 9f);    // between the three prong tips
            _muzzleR = MakeLight(Deep(_visual, "BF_clawR"), new Vector3(0f, 0f, 2.0f), 9f);
            _mouth = MakeLight(head, new Vector3(0f, 0.05f, 0.85f), 10f);
            foreach (var ml in new[] { _muzzleL, _muzzleR }) if (ml) ml.color = BerserkFuryBeam.Blue;
            _chest = MakeLight(body, new Vector3(0f, 0.3f, 0.3f), 7f);
            _built = true;
        }

        /// <summary>World position of a Berserk Fury muzzle (0 left claw tip, 1 right claw tip, 2 mouth), or fallback.</summary>
        public static Vector3 Muzzle(GameObject bodyObject, int index, Vector3 fallback)
        {
            try
            {
                CharacterBody b = bodyObject ? bodyObject.GetComponent<CharacterBody>() : null;
                Transform model = b && b.modelLocator ? b.modelLocator.modelTransform : null;
                BerserkFuryLook look = model ? model.GetComponent<BerserkFuryLook>() : null;
                if (look && look._built)
                {
                    Light l = index == 0 ? look._muzzleL : index == 1 ? look._muzzleR : look._mouth;
                    if (l) return l.transform.position;
                }
            }
            catch { }
            return fallback;
        }

        static Transform Deep(Transform t, string n)
        {
            if (!t) return null;
            foreach (Transform c in t) { if (c.name == n) return c; Transform r = Deep(c, n); if (r) return r; }
            return null;
        }

        static Light MakeLight(Transform parent, Vector3 local, float range)
        {
            if (!parent) return null;
            GameObject g = new GameObject("BF_Light");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = 0f; l.shadows = LightShadows.None;
            return l;
        }

        public static Material TrailMaterial()
        {
            if (_trailMat) return _trailMat;
            try
            {
                // Shader.Find("Sprites/Default") throws in RoR2; borrow the sprite shader from a SpriteRenderer.
                GameObject tmp = new GameObject("BFShaderProbe");
                Shader sh = tmp.AddComponent<SpriteRenderer>().sharedMaterial.shader;
                Destroy(tmp);
                _trailMat = new Material(sh);
            }
            catch (Exception e) { BerserkFuryPlugin.Log.LogWarning("Trail material: " + e.Message); }
            return _trailMat;
        }

        static TrailRenderer Trail(Transform prong)
        {
            if (!prong || !TrailMaterial()) return null;
            GameObject g = new GameObject("BF_Trail");
            g.transform.SetParent(prong, false);
            g.transform.localPosition = new Vector3(0f, 0f, 1.75f);   // prong tip
            TrailRenderer t = g.AddComponent<TrailRenderer>();
            t.sharedMaterial = _trailMat;
            t.time = 0.18f; t.startWidth = 0.5f; t.endWidth = 0f; t.minVertexDistance = 0.08f;
            t.shadowCastingMode = ShadowCastingMode.Off; t.receiveShadows = false;
            t.emitting = false;
            return t;
        }

        int CurrentSkin()
        {
            if (_body) return (int)_body.skinIndex;
            try { if (_msc && SkinIndexField != null) return (int)SkinIndexField.GetValue(_msc); } catch { }
            return 0;
        }

        void LateUpdate()
        {
            if (!_built) { Build(); if (!_built) return; }
            int skin = Mathf.Clamp(CurrentSkin(), 0, BerserkFuryPalettes.All.Length - 1);
            var pal = BerserkFuryPalettes.All[skin];
            if (skin != _skin)
            {
                _skin = skin;
                Texture tex = _bodyR.Count > 0 && _bodyR[0].sharedMaterial ? _bodyR[0].sharedMaterial.mainTexture : null;
                _bodyMat = BerserkFuryModel.SafeMaterial(tex, pal.Tint, Color.black, 0f);
                _accentMat = BerserkFuryModel.SafeMaterial(tex, Color.Lerp(pal.Tint, Color.white, 0.25f), pal.Accent, 0f);
                foreach (Renderer r in _bodyR) if (r) r.sharedMaterial = _bodyMat;
                foreach (Renderer r in _accentR) if (r) r.sharedMaterial = _accentMat;
                if (_trailMat) { foreach (var tr in new[] { _trailL, _trailR }) if (tr) { tr.startColor = new Color(pal.Accent.r, pal.Accent.g, pal.Accent.b, 0.85f); tr.endColor = new Color(1f, 1f, 1f, 0f); } }
                foreach (var l in new[] { _chest, _mouth }) if (l) l.color = pal.Accent;
            }
            // keep CharacterModel pointing at our materials (it re-applies defaultMaterial on overlay changes)
            if (_model && _model.baseRendererInfos != null)
            {
                var infos = _model.baseRendererInfos;
                for (int i = 0; i < infos.Length; i++)
                {
                    Renderer r = infos[i].renderer;
                    if (!r || !r.transform.IsChildOf(_visual)) continue;
                    Material want = _accentR.Contains(r) ? _accentMat : _bodyMat;
                    if (infos[i].defaultMaterial != want) { infos[i].defaultMaterial = want; r.sharedMaterial = want; }
                }
            }

            // ---- state-driven effects ----
            float t = Time.time, dt = Time.deltaTime;
            if (!_weapon && _body) _weapon = EntityStateMachine.FindByCustomName(_body.gameObject, "Weapon");
            object st = _weapon ? (object)_weapon.state : null;
            var slash = st as BusterClawSlashState;
            var cpc = st as ChargedParticleCannonState;
            bool od = _passive && _passive.IsOverdrive;

            // blade trails only while a slash swings
            bool swingL = slash != null && (slash.Strike == 0 || slash.Strike == 2) && slash.Progress > 0.3f && slash.Progress < 0.68f;
            bool swingR = slash != null && (slash.Strike == 1 || slash.Strike == 2) && slash.Progress > 0.3f && slash.Progress < 0.68f;
            if (_trailL) _trailL.emitting = swingL;
            if (_trailR) _trailR.emitting = swingR;

            // muzzle flash on each Twin Buster Blaster shot (alternating sides)
            if (BusterBlasterState.LastShotTime > _lastShot && _body && BusterBlasterState.LastShooter == _body.gameObject)
            {
                _lastShot = BusterBlasterState.LastShotTime;
                int mz = BusterBlasterState.LastMuzzle;
                Light l = mz == 0 ? _muzzleL : mz == 1 ? _muzzleR : _mouth;
                if (l) { l.color = BerserkFuryBeam.Blue; l.intensity = 7f; _mouthFlashT = mz == 2 ? 0.15f : _mouthFlashT; }
            }
            foreach (var l in new[] { _muzzleL, _muzzleR }) if (l) l.intensity = Mathf.MoveTowards(l.intensity, 0f, dt * 40f);

            // CPC: mouth light charges up, then blazes while firing
            float cpcGlow = 0f;
            if (cpc != null) cpcGlow = cpc.Firing ? 9f + Mathf.Sin(t * 40f) * 1.5f : Mathf.Clamp01(cpc.ChargeProgress) * 7f;
            _mouthFlashT -= dt;
            if (_mouth && _mouthFlashT <= 0f)
            {
                _mouth.color = BerserkFuryPalettes.All[Mathf.Max(0, _skin)].Accent;
                _mouth.intensity = Mathf.MoveTowards(_mouth.intensity, cpcGlow, dt * 25f);
            }

            // claw glow: Overdrive pulse, slash flash, CPC charge
            if (slash != null && (swingL || swingR)) _flash = 1f;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 3f);
            float glow = (od ? 0.45f + 0.25f * Mathf.Sin(t * 6f) : 0f) + _flash * 0.6f + (cpc != null ? 0.5f + cpcGlow * 0.05f : 0f);
            if (_accentMat && _accentMat.HasProperty("_EmPower")) _accentMat.SetFloat("_EmPower", Mathf.Clamp(glow, 0f, 1.2f));
            if (_chest) _chest.intensity = Mathf.MoveTowards(_chest.intensity, od ? 2.5f + Mathf.Sin(t * 6f) : 0f, dt * 6f);
        }
    }
}

namespace BerserkFury
{
    /// <summary>Short-lived blue laser beam: white core + blue glow, light at both ends, fades out.</summary>
    public class BerserkFuryBeam : MonoBehaviour
    {
        public static readonly Color Blue = new Color(0.25f, 0.6f, 1f);
        LineRenderer _core, _glow;
        Light _endLight;
        float _life, _t, _width;

        public static void Spawn(Vector3 from, Vector3 to, float width, float life)
        {
            Material mat = BerserkFuryLook.TrailMaterial();
            if (!mat) return;
            GameObject g = new GameObject("BF_Beam");
            var b = g.AddComponent<BerserkFuryBeam>();
            b._life = life; b._width = width;
            b._glow = Line(g, mat, from, to, new Color(Blue.r, Blue.g, Blue.b, 0.55f));
            GameObject c = new GameObject("core"); c.transform.SetParent(g.transform, false);
            b._core = Line(c, mat, from, to, new Color(0.85f, 0.95f, 1f, 1f));
            GameObject lg = new GameObject("light"); lg.transform.SetParent(g.transform, false); lg.transform.position = to;
            b._endLight = lg.AddComponent<Light>();
            b._endLight.type = LightType.Point; b._endLight.color = Blue; b._endLight.range = 6f; b._endLight.intensity = 4f; b._endLight.shadows = LightShadows.None;
            b.Apply(1f);
        }

        static LineRenderer Line(GameObject g, Material mat, Vector3 a, Vector3 b, Color c)
        {
            LineRenderer lr = g.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startColor = c; lr.endColor = c;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.numCapVertices = 4;
            return lr;
        }

        void Apply(float k)
        {
            if (_glow) { _glow.startWidth = _width * k; _glow.endWidth = _width * 0.8f * k; var c = _glow.startColor; c.a = 0.55f * k; _glow.startColor = _glow.endColor = c; }
            if (_core) { _core.startWidth = _width * 0.35f * k; _core.endWidth = _width * 0.25f * k; var c = _core.startColor; c.a = k; _core.startColor = _core.endColor = c; }
            if (_endLight) _endLight.intensity = 4f * k;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_t / _life);
            Apply(k * k);
            if (_t >= _life) Destroy(gameObject);
        }
    }
}

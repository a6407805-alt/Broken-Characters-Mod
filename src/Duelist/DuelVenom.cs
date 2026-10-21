using RoR2;
using R2API;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelistMod
{
    /// <summary>
    /// "Duel Venom": everything hurt by a duel (enemies on the map, allies, the Duelist himself) is marked
    /// with a purple poison-like aura, so friends watching the real world can see what the duel is doing.
    /// It's a real (replicated) debuff, so every player sees it; the mist, drips and glow are drawn locally.
    /// Damage from the duel also shows as purple numbers.
    /// </summary>
    public static class DuelVenom
    {
        public static BuffDef Def;
        public static readonly Color Purple = new Color(0.62f, 0.2f, 1f);

        public static void Register()
        {
            Def = ScriptableObject.CreateInstance<BuffDef>();
            Def.name = "bdDuelVenom";
            Def.buffColor = Purple;
            Def.canStack = false;
            Def.isDebuff = true;
            Def.iconSprite = MakeIcon();
            ContentAddition.AddBuffDef(Def);

            GameObject watcher = new GameObject("DuelVenomWatcher");
            Object.DontDestroyOnLoad(watcher);
            watcher.AddComponent<DuelVenomWatcher>();
        }

        /// <summary>Host only: mark a body hit by the duel (refreshes the timer).</summary>
        public static void Mark(HealthComponent h)
        {
            if (!NetworkServer.active || !Def || !h || !h.body || !DuelConfig.VenomAura.Value) return;
            h.body.AddTimedBuff(Def, Mathf.Max(1f, DuelConfig.DuelVenomSeconds.Value));
        }

        public static DamageColorIndex NumberColor => DuelConfig.VenomAura.Value ? DamageColorIndex.Void : DamageColorIndex.Default;

        private static Sprite MakeIcon()
        {
            // a purple venom drop with a skull-ish highlight, drawn in code
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x - n * 0.5f) / (n * 0.5f), v = (y - n * 0.42f) / (n * 0.5f);
                    // drop: circle at the bottom, point at the top
                    float circle = u * u + v * v;
                    float point = v > 0f ? Mathf.Abs(u) - (1f - v / 1.15f) * 0.75f : 1f;
                    bool inside = circle < 0.55f || (v > 0f && point < 0f && v < 1.1f);
                    Color c = Color.clear;
                    if (inside)
                    {
                        float shade = Mathf.Clamp01(0.55f + 0.45f * (-u * 0.6f + v * 0.4f));
                        c = Color.Lerp(new Color(0.25f, 0.02f, 0.45f), new Color(0.85f, 0.5f, 1f), shade);
                        if ((u + 0.28f) * (u + 0.28f) + (v + 0.05f) * (v + 0.05f) < 0.025f) c = new Color(1f, 0.92f, 1f);
                        c.a = 1f;
                    }
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static Texture2D _dot;
        public static Texture2D SoftDot
        {
            get
            {
                if (_dot) return _dot;
                const int n = 32;
                _dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x - n * 0.5f + 0.5f, y - n * 0.5f + 0.5f).magnitude / (n * 0.5f);
                        float a = Mathf.Clamp01(1f - d);
                        _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                _dot.Apply();
                return _dot;
            }
        }
    }

    /// <summary>Puts the aura on every body carrying Duel Venom (on every client).</summary>
    public class DuelVenomWatcher : MonoBehaviour
    {
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next || !DuelVenom.Def) return;
            _next = Time.unscaledTime + 0.2f;
            foreach (CharacterBody b in CharacterBody.readOnlyInstancesList)
            {
                if (!b || !b.HasBuff(DuelVenom.Def)) continue;
                if (!b.GetComponent<DuelVenomAura>()) b.gameObject.AddComponent<DuelVenomAura>();
            }
        }
    }

    /// <summary>The look: purple mist rising off the body, venom dripping down, a sickly purple glow.</summary>
    public class DuelVenomAura : MonoBehaviour
    {
        private CharacterBody _body;
        private GameObject _root;
        private ParticleSystem _mist, _drips;
        private Light _light;
        private float _fade = 0f;
        private bool _ending;
        private static Material _mat;

        private void Start()
        {
            _body = GetComponent<CharacterBody>();
            if (!_body) { Destroy(this); return; }
            float r = Mathf.Clamp(_body.radius, 0.4f, 6f);
            Transform anchor = _body.coreTransform ? _body.coreTransform : transform;

            _root = new GameObject("DuelVenomAura");
            _root.transform.SetParent(anchor, false);

            if (!_mat)
            {
                Shader s = DuelistVisuals.UnlitShader;
                _mat = s ? new Material(s) : DuelistVisuals.MakeMaterial(Color.white, DuelVenom.Purple);
                _mat.mainTexture = DuelVenom.SoftDot;
            }

            _mist = MakeSystem("Mist", r, rate: 22f * Mathf.Sqrt(r), life: 1.4f, size: 0.55f * r, speed: 0.5f, gravity: -0.12f,
                               from: new Color(0.7f, 0.25f, 1f, 0.55f), to: new Color(0.3f, 0f, 0.55f, 0f));
            _drips = MakeSystem("Drips", r * 0.8f, rate: 7f * Mathf.Sqrt(r), life: 0.9f, size: 0.13f * Mathf.Sqrt(r), speed: 0.2f, gravity: 1.2f,
                               from: new Color(0.85f, 0.45f, 1f, 0.95f), to: new Color(0.45f, 0.05f, 0.75f, 0.2f));

            _light = _root.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = DuelVenom.Purple;
            _light.range = 3f + r * 2.5f;
            _light.intensity = 0f;
        }

        private ParticleSystem MakeSystem(string name, float radius, float rate, float life, float size, float speed, float gravity, Color from, Color to)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed * 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startColor = from;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = rate;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = radius;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(from.a, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = _mat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            return ps;
        }

        private void Update()
        {
            if (!_root) return;
            bool on = _body && _body.healthComponent && _body.healthComponent.alive && DuelVenom.Def && _body.HasBuff(DuelVenom.Def);
            if (!on && !_ending)
            {
                _ending = true;
                if (_mist) _mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (_drips) _drips.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (on && _ending)
            {
                _ending = false;
                if (_mist) _mist.Play();
                if (_drips) _drips.Play();
            }
            _fade = Mathf.MoveTowards(_fade, _ending ? 0f : 1f, Time.deltaTime * (_ending ? 0.8f : 3f));
            if (_light) _light.intensity = _fade * (1.6f + Mathf.Sin(Time.time * 7f) * 0.35f + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.4f);
            if (_ending && _fade <= 0f)
            {
                Destroy(_root);
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (_root) Destroy(_root);
        }
    }
}

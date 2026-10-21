using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DuelistMod
{
    public static class DuelistIcons
    {
        public enum Symbol
        {
            Card, Fan, Arrow, Portal, Deck,
            // Golden relics (original designs from public-domain Egyptian symbols)
            RelicEye, RelicRing, RelicScale, RelicKey, RelicRod, RelicNecklace
        }

        public static Sprite MakeSprite(Color bg, Color fg, Symbol symbol)
        {
            Texture2D tex = MakeTexture(bg, fg, symbol, 128);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        public static bool IsRelic(Symbol s) => s >= Symbol.RelicEye;

        /// <summary>
        /// A golden relic icon: dark gradient background in the partner's color, sandstone band border,
        /// shaded gold relic with a drop shadow, and 1/2/3 marker dots (M1 / M2 / R; 0 = none).
        /// </summary>
        public static Sprite MakeRelicSprite(Color bg, Symbol relic, int marker, int size = 128)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            Color goldDark = new Color(0.5f, 0.32f, 0.06f);
            Color goldLight = new Color(1f, 0.88f, 0.45f);
            Color band = new Color(0.78f, 0.62f, 0.32f);
            float aa = 2.5f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;

                    // Background: radial gradient, a little sandstone noise.
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = Color.Lerp(bg * 1.25f, bg * 0.35f, Mathf.Clamp01(r * 0.9f));
                    float noise = (Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
                    c *= 0.95f + 0.08f * Mathf.Abs(noise);
                    c.a = 1f;

                    // Shadow, then the gold relic shaded top-to-bottom with a soft highlight.
                    float shadow = RelicInk(relic, u - 0.05f, v + 0.05f, aa);
                    c = Color.Lerp(c, Color.black, shadow * 0.55f);
                    float ink = RelicInk(relic, u, v, aa);
                    if (ink > 0f)
                    {
                        Color g = Color.Lerp(goldDark, goldLight, Mathf.Clamp01((v + 1f) * 0.5f + 0.15f));
                        float hi = Mathf.Clamp01(1f - Mathf.Abs(u + v * 0.4f + 0.2f) * 3f) * 0.35f;
                        g = Color.Lerp(g, Color.white, hi);
                        c = Color.Lerp(c, g, ink);
                    }

                    // Marker dots (bottom right).
                    for (int i = 0; i < marker; i++)
                    {
                        float d = Dist(u, v, 0.72f - i * 0.2f, -0.78f) - 0.075f;
                        c = Color.Lerp(c, goldLight, Mathf.Clamp01(-d / aa));
                    }

                    // Border: sandstone band with little hieroglyph ticks.
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    if (edge < 6)
                    {
                        c = Color.Lerp(band, Color.black, edge < 2 ? 0.55f : 0.1f);
                        if (edge >= 2 && edge <= 4 && ((x + y) / 4) % 3 == 0) c = Color.Lerp(c, goldDark, 0.6f);
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static Texture2D RelicTexture(Color bg, Symbol relic) => MakeRelicSprite(bg, relic, 0).texture;

        // ---- signed-distance helpers in [-1,1] space
        private static float Dist(float u, float v, float x, float y) => Mathf.Sqrt((u - x) * (u - x) + (v - y) * (v - y));

        private static float SegDist(float u, float v, float ax, float ay, float bx, float by)
        {
            float px = u - ax, py = v - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            float qx = px - dx * t, qy = py - dy * t;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        private static float Cov(float d, float aa) => Mathf.Clamp01(-d / aa + 0.5f);

        /// <summary>Coverage 0..1 of a relic at (u,v).</summary>
        private static float RelicInk(Symbol s, float u, float v, float aa)
        {
            float d = 10f;
            switch (s)
            {
                case Symbol.RelicEye: // Eye of Horus: almond outline, iris, brow, the two marks below
                {
                    float w = 0.8f;
                    float half = u > -w && u < w ? 0.36f * (1f - (u / w) * (u / w)) : -1f;
                    float outline = Mathf.Abs(Mathf.Abs(v - 0.05f) - Mathf.Max(0f, half));
                    if (half >= 0f) d = Mathf.Min(d, outline - 0.05f);
                    float iris = Dist(u, v, 0f, 0.05f);
                    d = Mathf.Min(d, iris - 0.2f);
                    if (iris < 0.08f) d = 0.05f; // pupil cut-out
                    d = Mathf.Min(d, SegDist(u, v, -0.7f, 0.55f, 0.7f, 0.5f) - 0.055f);       // brow
                    d = Mathf.Min(d, SegDist(u, v, 0.05f, -0.3f, 0.05f, -0.72f) - 0.05f);      // drop
                    d = Mathf.Min(d, SegDist(u, v, -0.15f, -0.28f, -0.5f, -0.6f) - 0.045f);    // curl
                    d = Mathf.Min(d, Dist(u, v, -0.56f, -0.52f) - 0.08f);
                    break;
                }
                case Symbol.RelicRing: // sun ring: ring, rays, disc
                {
                    float r = Dist(u, v, 0f, 0f);
                    d = Mathf.Min(d, Mathf.Abs(r - 0.62f) - 0.07f);
                    d = Mathf.Min(d, r - 0.17f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        d = Mathf.Min(d, SegDist(u, v, Mathf.Cos(a) * 0.26f, Mathf.Sin(a) * 0.26f, Mathf.Cos(a) * 0.46f, Mathf.Sin(a) * 0.46f) - 0.04f);
                    }
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6f;
                        d = Mathf.Min(d, Dist(u, v, Mathf.Cos(a) * 0.8f, Mathf.Sin(a) * 0.8f) - 0.045f);
                    }
                    break;
                }
                case Symbol.RelicScale: // balance of Ma'at
                {
                    d = Mathf.Min(d, SegDist(u, v, 0f, -0.7f, 0f, 0.55f) - 0.05f);
                    d = Mathf.Min(d, SegDist(u, v, -0.66f, 0.42f, 0.66f, 0.42f) - 0.045f);
                    d = Mathf.Min(d, Dist(u, v, 0f, 0.62f) - 0.09f);
                    d = Mathf.Min(d, SegDist(u, v, -0.4f, -0.76f, 0.4f, -0.76f) - 0.05f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float cx = 0.62f * side;
                        d = Mathf.Min(d, SegDist(u, v, cx, 0.42f, cx - 0.17f, -0.05f) - 0.022f);
                        d = Mathf.Min(d, SegDist(u, v, cx, 0.42f, cx + 0.17f, -0.05f) - 0.022f);
                        // pan: lower half of an ellipse
                        float ex = (u - cx) / 0.28f, ey = (v + 0.05f) / 0.14f;
                        if (v <= -0.05f) d = Mathf.Min(d, (Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 0.14f);
                        d = Mathf.Min(d, SegDist(u, v, cx - 0.28f, -0.05f, cx + 0.28f, -0.05f) - 0.02f);
                    }
                    break;
                }
                case Symbol.RelicKey: // ankh-key
                {
                    float ex = u / 0.26f, ey = (v - 0.4f) / 0.32f;
                    d = Mathf.Min(d, Mathf.Abs(Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 0.26f - 0.06f);
                    d = Mathf.Min(d, SegDist(u, v, -0.48f, 0.02f, 0.48f, 0.02f) - 0.08f);
                    d = Mathf.Min(d, SegDist(u, v, 0f, 0.05f, 0f, -0.72f) - 0.085f);
                    d = Mathf.Min(d, SegDist(u, v, 0f, -0.48f, 0.24f, -0.48f) - 0.055f);
                    d = Mathf.Min(d, SegDist(u, v, 0f, -0.66f, 0.3f, -0.66f) - 0.055f);
                    break;
                }
                case Symbol.RelicRod: // sceptre with a winged head
                {
                    d = Mathf.Min(d, SegDist(u, v, -0.55f, -0.72f, 0.3f, 0.4f) - 0.06f);
                    d = Mathf.Min(d, Dist(u, v, 0.38f, 0.5f) - 0.16f);
                    if (Dist(u, v, 0.38f, 0.5f) < 0.06f) d = 0.05f;
                    d = Mathf.Min(d, SegDist(u, v, 0.48f, 0.55f, 0.82f, 0.35f) - 0.05f);
                    d = Mathf.Min(d, SegDist(u, v, 0.36f, 0.64f, 0.18f, 0.9f) - 0.05f);
                    d = Mathf.Min(d, SegDist(u, v, -0.55f, -0.72f, -0.72f, -0.82f) - 0.04f);
                    d = Mathf.Min(d, SegDist(u, v, -0.55f, -0.72f, -0.48f, -0.9f) - 0.04f);
                    d = Mathf.Min(d, SegDist(u, v, -0.22f, -0.28f, -0.02f, -0.42f) - 0.035f); // grip bands
                    d = Mathf.Min(d, SegDist(u, v, -0.12f, -0.14f, 0.08f, -0.28f) - 0.035f);
                    break;
                }
                case Symbol.RelicNecklace: // broad collar with a pendant
                {
                    float r = Dist(u, v, 0f, 0.25f);
                    if (v < 0.3f)
                    {
                        d = Mathf.Min(d, Mathf.Abs(r - 0.55f) - 0.12f);
                        float ang = Mathf.Atan2(v - 0.25f, u);
                        float stripe = Mathf.Abs(Mathf.Sin(ang * 9f));
                        if (r > 0.45f && r < 0.65f && stripe < 0.18f) d = Mathf.Max(d, 0.02f); // bead gaps
                    }
                    d = Mathf.Min(d, Dist(u, v, -0.55f, 0.32f) - 0.08f);
                    d = Mathf.Min(d, Dist(u, v, 0.55f, 0.32f) - 0.08f);
                    float px = Mathf.Abs(u) / 0.16f, py = Mathf.Abs(v + 0.62f) / 0.2f;   // diamond pendant
                    d = Mathf.Min(d, (px + py - 1f) * 0.14f);
                    break;
                }
            }
            return Cov(d, aa);
        }

        public static Texture2D MakeTexture(Color bg, Color fg, Symbol symbol, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float vignette = 1f - 0.35f * Mathf.Clamp01(u * u + v * v);
                    Color c = bg * vignette;
                    c.a = 1f;

                    float ink = Ink(symbol, u, v);
                    if (ink > 0f) c = Color.Lerp(c, fg, ink);

                    if (x < 4 || y < 4 || x >= size - 4 || y >= size - 4) c = Color.Lerp(fg, Color.black, 0.35f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Returns 0..1 coverage of the symbol at (u,v) in [-1,1].
        private static float Ink(Symbol s, float u, float v)
        {
            switch (s)
            {
                case Symbol.Card:
                    return CardShape(u, v, 0f, 0f, 0.38f, 0.55f, 0.25f);
                case Symbol.Fan:
                {
                    float best = 0f;
                    for (int i = -2; i <= 2; i++)
                        best = Mathf.Max(best, CardShape(u, v + 0.3f, i * 0.28f, 0.35f, 0.24f, 0.4f, i * 0.32f) * (i == 0 ? 1f : 0.85f));
                    return best;
                }
                case Symbol.Arrow:
                {
                    float card = CardShape(u, v, 0.2f, 0f, 0.3f, 0.45f, -0.35f);
                    float trail = (u < -0.05f && u > -0.75f && Mathf.Abs(v - (u + 0.4f) * 0.2f) < 0.06f) ? 0.7f : 0f;
                    float trail2 = (u < -0.15f && u > -0.65f && Mathf.Abs(v + 0.25f - (u + 0.4f) * 0.2f) < 0.04f) ? 0.5f : 0f;
                    return Mathf.Max(card, Mathf.Max(trail, trail2));
                }
                case Symbol.Portal:
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.62f) / 0.1f);
                    float inner = CardShape(u, v, 0f, 0f, 0.22f, 0.32f, 0f);
                    return Mathf.Max(ring, inner);
                }
                default: // Deck
                {
                    float best = 0f;
                    for (int i = 0; i < 3; i++)
                        best = Mathf.Max(best, CardShape(u, v, -0.12f + i * 0.12f, 0.12f - i * 0.12f, 0.32f, 0.48f, 0f) * (0.6f + i * 0.2f));
                    return best;
                }
            }
        }

        // Rotated rectangle outline with a diamond pip; angle in radians.
        private static float CardShape(float u, float v, float cx, float cy, float hw, float hh, float angle)
        {
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            float lx = (u - cx) * cs + (v - cy) * sn;
            float ly = -(u - cx) * sn + (v - cy) * cs;
            float ax = Mathf.Abs(lx), ay = Mathf.Abs(ly);
            if (ax > hw || ay > hh) return 0f;
            float edge = Mathf.Min(hw - ax, hh - ay);
            if (edge < 0.05f) return 1f;                       // border
            if (ax / (hw * 0.45f) + ay / (hh * 0.35f) < 1f) return 1f; // pip
            return 0.25f;                                       // face
        }
    }

    public static class DuelistVisuals
    {
        private static Shader _shader;
        private static Shader _unlitShader;
        private static bool _unlitResolved;

        /// <summary>Shader.Find that returns null instead of throwing (RoR2 patches it to throw).</summary>
        public static Shader SafeFindShader(string name)
        {
            try { return Shader.Find(name); }
            catch { return null; }
        }

        /// <summary>
        /// An unlit, alpha-blended shader for icon art, taken from objects Unity sets up itself
        /// (sprite renderer default material, then the UI default material), never by name.
        /// </summary>
        public static Shader UnlitShader
        {
            get
            {
                if (_unlitResolved) return _unlitShader;
                _unlitResolved = true;
                try
                {
                    GameObject tmp = new GameObject("DuelistShaderProbe");
                    SpriteRenderer sr = tmp.AddComponent<SpriteRenderer>();
                    if (sr.sharedMaterial) _unlitShader = sr.sharedMaterial.shader;
                    Object.Destroy(tmp);
                }
                catch { }
                if (!_unlitShader)
                {
                    try { Material ui = Canvas.GetDefaultCanvasMaterial(); if (ui) _unlitShader = ui.shader; }
                    catch { }
                }
                DuelistPlugin.Log.LogInfo("Card art shader: " + (_unlitShader ? _unlitShader.name : "none (lit fallback)"));
                return _unlitShader;
            }
        }

        public static Material MakeMaterial(Color color, Color emission)
        {
            if (!_shader)
            {
                // RoR2 replaces Shader.Find with its own lookup, which THROWS for unknown names.
                _shader = SafeFindShader("Hopoo Games/Deferred/Standard") ?? SafeFindShader("Standard");
            }
            Material m = new Material(_shader);
            m.color = color;
            if (m.HasProperty("_EmColor"))
            {
                m.SetColor("_EmColor", emission);
                if (m.HasProperty("_EmPower")) m.SetFloat("_EmPower", 0.35f); // v0.2 glowed so hard it washed out
            }
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c) Object.DestroyImmediate(c);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static Transform FindModel(GameObject root)
        {
            ModelLocator locator = root.GetComponent<ModelLocator>();
            if (locator && locator.modelTransform) return locator.modelTransform;
            CharacterModel cm = root.GetComponentInChildren<CharacterModel>(true);
            return cm ? cm.transform : null;
        }

        /// <summary>A small glowing deck of cards held in the left hand.</summary>
        public static void AttachDeckProp(GameObject root)
        {
            try
            {
                Transform model = FindModel(root);
                if (!model) return;
                ChildLocator cl = model.GetComponent<ChildLocator>();
                Transform hand = cl ? (cl.FindChild("HandL") ?? cl.FindChild("LeftHand")) : null;
                if (!hand) return;

                GameObject deck = new GameObject("DuelistDeckProp");
                deck.transform.SetParent(hand, false);
                deck.transform.localPosition = new Vector3(0f, 0.08f, 0.05f);
                deck.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                Material face = MakeMaterial(new Color(0.95f, 0.92f, 0.85f), new Color(0.9f, 0.7f, 0.2f) * 0.6f);
                Material back = MakeMaterial(new Color(0.4f, 0.1f, 0.5f), new Color(0.5f, 0.15f, 0.7f));
                for (int i = 0; i < 4; i++)
                {
                    Primitive(PrimitiveType.Cube, "Card" + i, deck.transform,
                        new Vector3(0f, i * 0.012f, 0f), new Vector3(0.07f, 0.01f, 0.1f), i == 3 ? back : face);
                }
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("Deck prop skipped: " + e.Message);
            }
        }

        public static void AddTinter(GameObject root)
        {
            Transform model = FindModel(root);
            if (!model) return;
            CharacterModel cm = model.GetComponent<CharacterModel>();
            if (cm && !cm.GetComponent<DuelistTinter>()) cm.gameObject.AddComponent<DuelistTinter>();
        }
    }

    /// <summary>
    /// Keeps tinted copies of the model's materials in place. Skins swap materials a few frames after
    /// spawn, so this checks every LateUpdate instead of once.
    /// </summary>
    public class DuelistTinter : MonoBehaviour
    {
        private static readonly Color Main = new Color(0.32f, 0.2f, 0.42f);
        private static readonly Color Glow = new Color(0.95f, 0.75f, 0.25f);

        private CharacterModel _model;
        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private readonly HashSet<Material> _ours = new HashSet<Material>();

        private void Awake() => _model = GetComponent<CharacterModel>();

        private void LateUpdate()
        {
            if (!_model || _model.baseRendererInfos == null) return;
            for (int i = 0; i < _model.baseRendererInfos.Length; i++)
            {
                Material orig = _model.baseRendererInfos[i].defaultMaterial;
                if (!orig || _ours.Contains(orig)) continue;

                if (!_copies.TryGetValue(orig, out Material copy))
                {
                    copy = new Material(orig);
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", Main);
                    if (copy.HasProperty("_EmColor")) copy.SetColor("_EmColor", Glow);
                    if (copy.HasProperty("_EmPower")) copy.SetFloat("_EmPower", 1.2f);
                    if (copy.HasProperty("_RimColor")) copy.SetColor("_RimColor", Glow);
                    _copies[orig] = copy;
                    _ours.Add(copy);
                }
                _model.baseRendererInfos[i].defaultMaterial = copy;
            }
        }
    }

    public enum ClickKind
    {
        Rep, MonsterZone, BackZone, ExtraDeck, Deck, Graveyard,
        EnemyZone, EnemyBack, EnemyDeck, EnemyGrave, EnemyExtra, Commander, Banished
    }

    /// <summary>Marks a board object the mouse can click (found by raycast from the duel camera).</summary>
    public class DuelClickable : MonoBehaviour
    {
        public ClickKind Kind;
        public int Index;
    }

    /// <summary>
    /// The floating duel board. Local space: +Z points from you toward the enemy.
    /// Your side:   monster zones z=7, spell/trap z=3.2, you stand at z=0.
    /// Enemy side:  monster zones z=15, spell/trap z=19, commander z=23.6 — a mirror of yours.
    /// Laid out like a real playmat: Field Zone and Graveyard beside the monster zones, Extra Deck and
    /// Deck beside the spell/trap zones (the enemy's mirrored).
    /// </summary>
    public class DuelBoard : MonoBehaviour
    {
        public const float SlotSpacing = 3.6f;

        public static Vector3 MonsterLocal(int i) => new Vector3((i - 2) * SlotSpacing, 0f, 7f);
        public static Vector3 BackLocal(int i) => new Vector3((i - 2) * SlotSpacing, 0f, 3.2f);
        public static readonly Vector3 FieldLocal = new Vector3(-12.4f, 0f, 7f);
        public static readonly Vector3 ExtraLocal = new Vector3(-12.4f, 0f, 3.2f);
        public static readonly Vector3 DeckLocal = new Vector3(12.4f, 0f, 3.2f);
        public static readonly Vector3 GraveLocal = new Vector3(12.4f, 0f, 7f);
        public static readonly Vector3 BanishLocal = new Vector3(12.4f, 0f, -0.75f);

        public static Vector3 EnemyMonsterLocal(int i) => new Vector3((i - 2) * SlotSpacing, 0f, 15f);
        public static Vector3 EnemyBackLocal(int i) => new Vector3((i - 2) * SlotSpacing, 0f, 19f);
        public static readonly Vector3 CommanderLocal = new Vector3(0f, 0f, 23.6f);
        public static readonly Vector3 EnemyFieldLocal = new Vector3(12.4f, 0f, 15f);   // mirrored: their left is our right
        public static readonly Vector3 EnemyExtraLocal = new Vector3(12.4f, 0f, 19f);
        public static readonly Vector3 EnemyDeckLocal = new Vector3(-12.4f, 0f, 19f);
        public static readonly Vector3 EnemyGraveLocal = new Vector3(-12.4f, 0f, 15f);

        public Transform TargetMarker;
        public Transform HoverMarker;

        private readonly GameObject[] _backCards = new GameObject[DuelSession.Zones];
        private readonly GameObject[] _enemyBackCards = new GameObject[DuelSession.Zones];
        private Transform _deckPile, _extraPile, _gravePile, _eDeckPile, _eExtraPile, _eGravePile, _banishPile;
        private GameObject _commander;
        private Material _backMat, _enemyBackMat, _gold;
        private readonly List<Renderer> _zoneTiles = new List<Renderer>();
        public bool HasPlaymat;

        public Vector3 World(Vector3 local) => transform.TransformPoint(local);
        public Vector3 MonsterPos(int i) => World(MonsterLocal(i));
        public Vector3 BackPos(int i) => World(BackLocal(i));
        public Vector3 EnemyMonsterPos(int i) => World(EnemyMonsterLocal(i));
        public Vector3 EnemyBackPos(int i) => World(EnemyBackLocal(i));
        public Vector3 CommanderPos => World(CommanderLocal);

        // ------------------------------------------------------------ build

        public static DuelBoard Build(Vector3 spot, Vector3 forward)
        {
            GameObject root = new GameObject("DuelistArena");
            root.transform.position = spot;
            root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            DuelBoard board = root.AddComponent<DuelBoard>();
            Transform t = root.transform;

            Material floor = DuelistVisuals.MakeMaterial(new Color(0.07f, 0.045f, 0.035f), new Color(0.12f, 0.06f, 0.03f) * 0.2f);   // dark wooden table
            Material enemyFloor = floor;
            board._gold = DuelistVisuals.MakeMaterial(new Color(0.95f, 0.75f, 0.25f), new Color(0.9f, 0.65f, 0.15f));
            Material monsterZone = DuelistVisuals.MakeMaterial(new Color(0.5f, 0.32f, 0.14f), new Color(0.8f, 0.5f, 0.2f) * 0.45f);
            Material stZone = DuelistVisuals.MakeMaterial(new Color(0.13f, 0.38f, 0.36f), new Color(0.2f, 0.6f, 0.55f) * 0.45f);
            Material extraZone = DuelistVisuals.MakeMaterial(new Color(0.35f, 0.2f, 0.55f), new Color(0.5f, 0.3f, 0.8f) * 0.5f);
            Material deckZone = DuelistVisuals.MakeMaterial(new Color(0.3f, 0.16f, 0.08f), new Color(0.45f, 0.25f, 0.1f) * 0.4f);
            Material graveZone = DuelistVisuals.MakeMaterial(new Color(0.22f, 0.22f, 0.25f), new Color(0.3f, 0.3f, 0.35f) * 0.3f);
            Material enemyMonsterZone = DuelistVisuals.MakeMaterial(new Color(0.45f, 0.14f, 0.12f), new Color(0.8f, 0.25f, 0.2f) * 0.45f);
            Material enemyStZone = DuelistVisuals.MakeMaterial(new Color(0.3f, 0.12f, 0.3f), new Color(0.55f, 0.2f, 0.5f) * 0.45f);
            board._backMat = DuelistVisuals.MakeMaterial(new Color(0.5f, 0.16f, 0.4f), new Color(0.6f, 0.2f, 0.5f) * 0.6f);
            board._enemyBackMat = DuelistVisuals.MakeMaterial(new Color(0.35f, 0.08f, 0.1f), new Color(0.6f, 0.1f, 0.15f) * 0.6f);

            // Walkable board (world layer) — your half and the enemy half in different tints.
            GameObject slab = DuelistVisuals.Primitive(PrimitiveType.Cube, "Board", t, new Vector3(0f, -0.5f, 2.5f), new Vector3(32f, 1f, 17f), floor, true);
            slab.layer = LayerIndex.world.intVal;
            GameObject slab2 = DuelistVisuals.Primitive(PrimitiveType.Cube, "EnemyBoard", t, new Vector3(0f, -0.5f, 19f), new Vector3(32f, 1f, 16f), enemyFloor, true);
            slab2.layer = LayerIndex.world.intVal;

            Material gold = board._gold;
            DuelistVisuals.Primitive(PrimitiveType.Cube, "TrimL", t, new Vector3(-16f, 0.05f, 10.5f), new Vector3(0.4f, 0.2f, 33f), gold);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "TrimR", t, new Vector3(16f, 0.05f, 10.5f), new Vector3(0.4f, 0.2f, 33f), gold);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "TrimF", t, new Vector3(0f, 0.05f, 27f), new Vector3(32f, 0.2f, 0.4f), gold);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "TrimB", t, new Vector3(0f, 0.05f, -6f), new Vector3(32f, 0.2f, 0.4f), gold);
            board.HasPlaymat = DuelMat.Lay(t);
            if (!board.HasPlaymat) DuelistVisuals.Primitive(PrimitiveType.Cube, "Midline", t, new Vector3(0f, 0.02f, 11f), new Vector3(31f, 0.05f, 0.3f), gold);

            for (int i = 0; i < DuelSession.Zones; i++)
            {
                board.Zone(t, "MonsterZone" + i, MonsterLocal(i), new Vector3(2.8f, 0.06f, 3.5f), monsterZone, ClickKind.MonsterZone, i);
                board.Zone(t, "SpellTrapZone" + i, BackLocal(i), new Vector3(2.8f, 0.06f, 3.4f), stZone, ClickKind.BackZone, i);
                board.Zone(t, "EnemyZone" + i, EnemyMonsterLocal(i), new Vector3(2.8f, 0.06f, 3.5f), enemyMonsterZone, ClickKind.EnemyZone, i);
                board.Zone(t, "EnemyBack" + i, EnemyBackLocal(i), new Vector3(2.8f, 0.06f, 3.4f), enemyStZone, ClickKind.EnemyBack, i);
            }
            board.Zone(t, "ExtraDeckZone", ExtraLocal, new Vector3(2.8f, 0.06f, 3.4f), extraZone, ClickKind.ExtraDeck, 0);
            board.Zone(t, "DeckZone", DeckLocal, new Vector3(2.8f, 0.06f, 3.4f), deckZone, ClickKind.Deck, 0);
            board.Zone(t, "GraveyardZone", GraveLocal, new Vector3(2.8f, 0.06f, 3.5f), graveZone, ClickKind.Graveyard, 0);
            Material banishZone = DuelistVisuals.MakeMaterial(new Color(0.12f, 0.12f, 0.2f), new Color(0.4f, 0.4f, 0.7f) * 0.35f);
            board.Zone(t, "BanishedZone", BanishLocal, new Vector3(2.8f, 0.06f, 2.4f), banishZone, ClickKind.Banished, 0);
            board.Zone(t, "EnemyExtraZone", EnemyExtraLocal, new Vector3(2.8f, 0.06f, 3.4f), extraZone, ClickKind.EnemyExtra, 0);
            board.Zone(t, "EnemyDeckZone", EnemyDeckLocal, new Vector3(2.8f, 0.06f, 3.4f), deckZone, ClickKind.EnemyDeck, 0);
            board.Zone(t, "EnemyGraveZone", EnemyGraveLocal, new Vector3(2.8f, 0.06f, 3.5f), graveZone, ClickKind.EnemyGrave, 0);
            if (board.HasPlaymat) foreach (Renderer r in board._zoneTiles) if (r) r.enabled = false;   // the mat has the zones printed on it

            board._extraPile = Pile(t, "ExtraPile", ExtraLocal, 3f, extraZone);
            board._deckPile = Pile(t, "DeckPile", DeckLocal, 3f, board._backMat);
            board._gravePile = Pile(t, "GravePile", GraveLocal, 3f, graveZone);
            board._banishPile = Pile(t, "BanishPile", BanishLocal, 2f, banishZone);
            board._eExtraPile = Pile(t, "EnemyExtraPile", EnemyExtraLocal, 3f, extraZone);
            board._eDeckPile = Pile(t, "EnemyDeckPile", EnemyDeckLocal, 3f, board._enemyBackMat);
            board._eGravePile = Pile(t, "EnemyGravePile", EnemyGraveLocal, 3f, graveZone);

            // Commander dais behind the enemy field (mirrors where you stand). On the mat it's a printed circle.
            if (!board.HasPlaymat)
                DuelistVisuals.Primitive(PrimitiveType.Cylinder, "CommanderDais", t, CommanderLocal + new Vector3(0f, 0.15f, 0f), new Vector3(4.2f, 0.15f, 4.2f), gold);

            Material ringGold = DuelistVisuals.MakeMaterial(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.8f, 0.2f) * 2f);
            Material ringWhite = DuelistVisuals.MakeMaterial(new Color(1f, 1f, 1f), new Color(0.8f, 0.8f, 1f));
            board.TargetMarker = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "TargetRing", t, Vector3.down * 10f, new Vector3(3.9f, 0.03f, 3.9f), ringGold).transform;
            board.HoverMarker = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "HoverRing", t, Vector3.down * 10f, new Vector3(3.4f, 0.025f, 3.4f), ringWhite).transform;

            return board;
        }

        private static Transform Pile(Transform parent, string name, Vector3 local, float depth, Material mat)
        {
            return DuelistVisuals.Primitive(PrimitiveType.Cube, name, parent, local, new Vector3(2.1f, 0.1f, depth), mat).transform;
        }

        private void Zone(Transform parent, string name, Vector3 local, Vector3 size, Material mat, ClickKind kind, int index)
        {
            GameObject z = DuelistVisuals.Primitive(PrimitiveType.Cube, name, parent, local + new Vector3(0f, 0.03f, 0f), size, mat);
            AddClick(z, kind, index, Vector3.zero, new Vector3(1f, 30f, 1f)); // tall in local units (the cube is 0.06 thick)
            _zoneTiles.Add(z.GetComponent<Renderer>());
        }

        private static void AddClick(GameObject go, ClickKind kind, int index, Vector3 center, Vector3 size)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = center;
            bc.size = size;
            DuelClickable c = go.AddComponent<DuelClickable>();
            c.Kind = kind;
            c.Index = index;
        }

        /// <summary>Unlit, transparent quad showing a texture (icons have alpha).</summary>
        private static GameObject Art(Transform parent, Texture tex, Vector3 localPos, Quaternion localRot, float size)
        {
            Shader unlit = DuelistVisuals.UnlitShader;
            Material m = unlit ? new Material(unlit) : DuelistVisuals.MakeMaterial(Color.white, Color.white * 0.4f);
            m.mainTexture = tex;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Art";
            Collider c = q.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localRotation = localRot;
            q.transform.localScale = new Vector3(size, size, 1f);
            q.GetComponent<Renderer>().sharedMaterial = m;
            return q;
        }

        // ------------------------------------------------------------ enemy monsters & commander

        /// <summary>
        /// Puts a species on an enemy monster zone: card on the zone, portrait standing behind it,
        /// and a live clone of the monster on top facing you. Returns the stand (rep.Hologram).
        /// </summary>
        public GameObject PlaceRep(Representative rep, int repIndex, int zone)
        {
            Vector3 local = EnemyMonsterLocal(zone);
            rep.BoardPosition = World(local);

            GameObject stand = new GameObject("Stand_" + rep.Name);
            stand.transform.SetParent(transform, false);
            stand.transform.localPosition = local;

            Color tint = rep.EliteName != null ? rep.EliteColor : rep.Color;
            Material frame = DuelistVisuals.MakeMaterial(tint * 0.5f, tint * 0.45f);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "CardOnZone", stand.transform, new Vector3(0f, 0.09f, 0f), new Vector3(2.3f, 0.06f, 3.1f), frame);
            if (rep.EliteName != null)
            {
                // elite: a glowing ring in its element's colour under the monster
                Material ring = DuelistVisuals.MakeMaterial(rep.EliteColor, rep.EliteColor * 2.5f);
                DuelistVisuals.Primitive(PrimitiveType.Cylinder, "EliteAura", stand.transform, new Vector3(0f, 0.14f, -0.2f), new Vector3(2.1f, 0.015f, 2.1f), ring);
                if (rep.EliteIcon) Art(stand.transform, rep.EliteIcon, new Vector3(0.75f, 0.14f, 1.15f), Quaternion.Euler(90f, 180f, 0f), 0.6f);
            }
            if (rep.Icon) Art(stand.transform, rep.Icon, new Vector3(0f, 0.13f, 0.95f), Quaternion.Euler(90f, 180f, 0f), 1.1f);

            GameObject holo = new GameObject("Model");
            holo.transform.SetParent(stand.transform, false);
            holo.transform.localPosition = new Vector3(0f, 0.12f, -0.2f);
            holo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (!AddRepModel(rep, holo.transform, rep.IsBoss ? 2.8f : 2.2f, 2.0f))
                DuelistVisuals.Primitive(PrimitiveType.Sphere, "Fallback", holo.transform, new Vector3(0f, 1.1f, 0f), Vector3.one * 1.3f, frame);

            AddClick(stand, ClickKind.Rep, repIndex, new Vector3(0f, 1.6f, 0.3f), new Vector3(3f, 3.4f, 3.8f));
            return stand;
        }

        /// <summary>The enemy commander: the boss as a big live model, or The Director if there's no boss.</summary>
        public void PlaceCommander(Representative boss, int repIndex)
        {
            if (_commander) Object.Destroy(_commander);
            _commander = new GameObject("Commander");
            _commander.transform.SetParent(transform, false);
            _commander.transform.localPosition = CommanderLocal + new Vector3(0f, 0.3f, 0f);

            if (boss != null)
            {
                boss.BoardPosition = CommanderPos;
                GameObject holo = new GameObject("Model");
                holo.transform.SetParent(_commander.transform, false);
                holo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                if (!AddRepModel(boss, holo.transform, 4.6f, 4.2f))
                    DuelistVisuals.Primitive(PrimitiveType.Sphere, "Fallback", holo.transform, new Vector3(0f, 2f, 0f), Vector3.one * 3f, DuelistVisuals.MakeMaterial(boss.Color, boss.Color * 0.5f));
                boss.Hologram = _commander;
                AddClick(_commander, ClickKind.Commander, repIndex, new Vector3(0f, 2.5f, 0f), new Vector3(4.2f, 5.5f, 4.2f));
            }
            else
            {
                // The Director: a dark floating crystal with a gold halo.
                Material dark = DuelistVisuals.MakeMaterial(new Color(0.12f, 0.08f, 0.18f), new Color(0.5f, 0.15f, 0.6f) * 0.6f);
                GameObject crystal = DuelistVisuals.Primitive(PrimitiveType.Cube, "DirectorCrystal", _commander.transform, new Vector3(0f, 2.8f, 0f), new Vector3(1.4f, 2.6f, 1.4f), dark);
                crystal.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                crystal.AddComponent<DuelSpin>().Speed = 30f;
                DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Halo", _commander.transform, new Vector3(0f, 4.9f, 0f), new Vector3(2.4f, 0.04f, 2.4f), _gold);
                AddClick(_commander, ClickKind.Commander, -1, new Vector3(0f, 2.5f, 0f), new Vector3(4f, 5f, 4f));
            }
        }

        public void RemoveStand(GameObject stand)
        {
            if (stand) Object.Destroy(stand);
        }

        /// <summary>Face-up enemy card on their spell/trap row (elite auras). Null art clears it.</summary>
        public void SetEnemyBack(int i, Texture art, Color color, bool present)
        {
            if (_enemyBackCards[i]) { Object.Destroy(_enemyBackCards[i]); _enemyBackCards[i] = null; }
            if (!present) return;
            GameObject c = new GameObject("EnemyAura" + i);
            c.transform.SetParent(transform, false);
            c.transform.localPosition = EnemyBackLocal(i) + new Vector3(0f, 0.09f, 0f);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Frame", c.transform, Vector3.zero, new Vector3(2.3f, 0.06f, 3.1f), DuelistVisuals.MakeMaterial(color * 0.6f, color * 0.5f));
            if (art) Art(c.transform, art, new Vector3(0f, 0.04f, 0f), Quaternion.Euler(90f, 180f, 0f), 1.7f);
            _enemyBackCards[i] = c;
        }

        /// <summary>The enemy's face-up elite card in that spell/trap zone (for its activation animation).</summary>
        public GameObject EnemyBackCard(int i) => i >= 0 && i < _enemyBackCards.Length ? _enemyBackCards[i] : null;

        /// <summary>Turns an enemy monster's card sideways for defense position (the model keeps facing you).</summary>
        public void SetRepPosition(Representative rep, bool animate)
        {
            if (rep == null || !rep.Hologram) return;
            Transform card = rep.Hologram.transform.Find("CardOnZone");
            if (!card) return;
            float from = card.localEulerAngles.y;
            card.localRotation = Quaternion.Euler(0f, rep.DefensePos ? 90f : 0f, 0f);
            if (animate) DuelFx.Turn(card.gameObject, from);
            Transform shield = rep.Hologram.transform.Find("DefShield");
            if (rep.DefensePos && !shield)
            {
                GameObject s = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "DefShield", rep.Hologram.transform, new Vector3(0f, 0.16f, -0.2f), new Vector3(2.5f, 0.012f, 2.5f), DuelistVisuals.MakeMaterial(new Color(0.35f, 0.55f, 1f), new Color(0.3f, 0.5f, 1f) * 2f));
                if (animate) DuelTween.From(s, Vector3.zero, 0.05f, Vector3.zero, 0.3f, 0f, true);
            }
            else if (!rep.DefensePos && shield) Object.Destroy(shield.gameObject);
        }

        // ------------------------------------------------------------ your side

        /// <summary>A card lying on a zone, with optional art. Returns the card object.</summary>
        public GameObject LayCard(Vector3 local, Color frame, Texture art, string name, float w, float h)
        {
            GameObject card = new GameObject(name);
            card.transform.SetParent(transform, false);
            card.transform.localPosition = local + new Vector3(0f, 0.09f, 0f);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Frame", card.transform, Vector3.zero, new Vector3(w, 0.06f, h), DuelistVisuals.MakeMaterial(frame, frame * 0.6f));
            if (art) Art(card.transform, art, new Vector3(0f, 0.04f, h * 0.12f), Quaternion.Euler(90f, 0f, 0f), w * 0.72f);
            return card;
        }

        /// <summary>Card + spinning model on your monster zone (the item's pickup model if it has one).</summary>
        public GameObject SummonOnZone(int slot, Card card, out GameObject statue)
        {
            GameObject cardObj = LayCard(MonsterLocal(slot), card.Frame, card.Art, "MyCard" + slot, 2.3f, 3.1f);
            statue = new GameObject("Statue");
            statue.transform.SetParent(cardObj.transform, false);
            statue.transform.localPosition = new Vector3(0f, 0.05f, -0.2f);

            if (card.Partner != PartnerKind.None)
            {
                // one of your 5 partners: the same model that follows you around, life-size on the zone
                GameObject pv = new GameObject("PartnerStatue");
                pv.transform.SetParent(statue.transform, false);
                pv.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                pv.transform.localScale = Vector3.one * (2.2f / Mathf.Max(0.2f, DuelConfig.PartnerSize.Value));
                PartnerVisual look = pv.AddComponent<PartnerVisual>();
                look.Kind = card.Partner;
                look.OnBoard = true;
                return cardObj;
            }

            // Extra Deck toolkit monsters have their own models (Scrap Chimera, Tuned Colossus, Overlay Sentinel, Link Arbiter)
            if (ExtraModels.TryBuild(card, statue.transform)) return cardObj;

            bool ok = false;
            if (card.Body != BodyIndex.None)
                ok = AddEnemyModel(card.Body, statue.transform, card.Type == CardType.Extra ? 2.6f : 2f, 2.0f); // the robot itself
            ItemDef def = !ok && card.Item != ItemIndex.None ? ItemCatalog.GetItemDef(card.Item) : null;
            if (def && def.pickupModelPrefab)
            {
                float hgt = card.Type == CardType.Extra ? 2.6f : 1.7f;
                ok = CloneVisual(def.pickupModelPrefab, statue.transform, hgt, def.name, 2.0f) || BakeStatue(def.pickupModelPrefab, statue.transform, hgt, def.name, 2.0f);
            }
            if (!ok)
                DuelistVisuals.Primitive(PrimitiveType.Cube, "Token", statue.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 1.4f, 0.1f), DuelistVisuals.MakeMaterial(card.Frame, card.Frame));
            return cardObj;
        }

        /// <summary>A set monster: card back, lying sideways (defense position).</summary>
        public GameObject LayFaceDown(int slot)
        {
            GameObject c = new GameObject("SetMonster" + slot);
            c.transform.SetParent(transform, false);
            c.transform.localPosition = MonsterLocal(slot) + new Vector3(0f, 0.09f, 0f);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Back", c.transform, Vector3.zero, new Vector3(2.3f, 0.06f, 3.1f), _backMat);
            DuelistVisuals.Primitive(PrimitiveType.Cube, "Emblem", c.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.02f, 0.9f), _gold);
            return c;
        }

        public void SetBackCard(int i, bool present)
        {
            if (present && !_backCards[i])
            {
                GameObject c = new GameObject("SetCard" + i);
                c.transform.SetParent(transform, false);
                c.transform.localPosition = BackLocal(i) + new Vector3(0f, 0.09f, 0f);
                DuelistVisuals.Primitive(PrimitiveType.Cube, "Back", c.transform, Vector3.zero, new Vector3(2.3f, 0.06f, 3.1f), _backMat);
                DuelistVisuals.Primitive(PrimitiveType.Cube, "Emblem", c.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.02f, 0.9f), _gold);
                _backCards[i] = c;
            }
            else if (!present && _backCards[i])
            {
                Object.Destroy(_backCards[i]);
                _backCards[i] = null;
            }
        }

        public GameObject BackCardObject(int i) => i >= 0 && i < _backCards.Length ? _backCards[i] : null;

        /// <summary>Hands over a set card's object (it's being activated) and frees the zone.</summary>
        public GameObject TakeBackCard(int i)
        {
            if (i < 0 || i >= _backCards.Length) return null;
            GameObject c = _backCards[i];
            _backCards[i] = null;
            return c;
        }

        /// <summary>A spell activated from the hand, shown face-up on a free spell/trap zone while it resolves.</summary>
        public GameObject ShowSpellOnZone(Card card)
        {
            int slot = 2;
            for (int i = 0; i < _backCards.Length; i++) if (!_backCards[i]) { slot = i; break; }
            return LayCard(BackLocal(slot) + new Vector3(0f, 0.05f, 0f), card.Frame, card.Art, "ActiveSpell", 2.3f, 3.1f);
        }

        public void SetPiles(int deck, int extra, int grave, int eDeck, int eExtra, int eGrave, int banished)
        {
            SetPile(_banishPile, BanishLocal, banished);
            SetPile(_deckPile, DeckLocal, deck);
            SetPile(_extraPile, ExtraLocal, extra);
            SetPile(_gravePile, GraveLocal, grave);
            SetPile(_eDeckPile, EnemyDeckLocal, eDeck);
            SetPile(_eExtraPile, EnemyExtraLocal, eExtra);
            SetPile(_eGravePile, EnemyGraveLocal, eGrave);
        }

        private static void SetPile(Transform pile, Vector3 local, int count)
        {
            if (!pile) return;
            pile.gameObject.SetActive(count > 0);
            float h = 0.06f + 0.035f * Mathf.Min(count, 30);
            Vector3 s = pile.localScale;
            pile.localScale = new Vector3(s.x, h, s.z);
            pile.localPosition = local + new Vector3(0f, h * 0.5f + 0.06f, 0f);
        }

        public void PlaceMarker(Transform marker, Vector3 worldPos, bool visible)
        {
            if (!marker) return;
            marker.gameObject.SetActive(visible);
            if (visible) marker.position = worldPos + transform.up * 0.08f;
        }

        // ------------------------------------------------------------ models

        /// <summary>The real monster from the map (already loaded and skinned); the prefab only as a fallback.</summary>
        private static bool AddRepModel(Representative rep, Transform parent, float height, float width)
        {
            foreach (DuelMember m in rep.Members)
            {
                if (!m.Alive || !m.Body) continue;
                ModelLocator ml = m.Body.modelLocator;
                Vector3 facing = m.Body.characterDirection ? m.Body.characterDirection.forward : m.Body.transform.forward;
                if (ml && ml.modelTransform && CloneLive(ml.modelTransform, parent, height, width, rep.Name, facing)) return true;
            }
            return AddEnemyModel(rep.Index, parent, height, width);
        }

        /// <summary>
        /// Copies a monster that's alive on the map right now. Its materials and skin are already loaded,
        /// so the copy looks right (prefab copies can come out pink in game 1.5). Sized from the real
        /// monster's body so it fits the zone; particles shrink with it. Keeps its Animator so it idles.
        /// </summary>
        public static bool CloneLive(Transform live, Transform parent, float height, float width, string label, Vector3 bodyForward = default(Vector3))
        {
            GameObject staging = null;
            try
            {
                if (!live) return false;
                bool any = false;
                Bounds b = default(Bounds);
                foreach (Renderer r in live.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || ZoneFit.IsEffect(r)) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any)
                    foreach (Renderer r in live.GetComponentsInChildren<Renderer>())
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy || r is TrailRenderer || r is LineRenderer) continue;
                        if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    }
                if (!any || b.size.y < 0.01f) return false;

                Vector3 centreLocal = live.InverseTransformPoint(b.center);
                Vector3 bottomLocal = live.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
                float foot = Mathf.Max(b.size.x, b.size.z);
                float f = Mathf.Min(width / Mathf.Max(foot, 0.01f), height / b.size.y);

                staging = new GameObject("DuelistLiveStaging");
                staging.SetActive(false);
                GameObject clone = Object.Instantiate(live.gameObject, staging.transform, false);
                clone.name = "Live_" + label;
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) if (mb) Object.DestroyImmediate(mb);
                    foreach (Joint j in clone.GetComponentsInChildren<Joint>(true)) if (j) Object.DestroyImmediate(j);
                    foreach (Collider c in clone.GetComponentsInChildren<Collider>(true)) if (c) Object.DestroyImmediate(c);
                    foreach (Rigidbody rb in clone.GetComponentsInChildren<Rigidbody>(true)) if (rb) Object.DestroyImmediate(rb);
                }
                foreach (ParticleSystem ps in clone.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // flames shrink with the model
                }

                Transform ct = clone.transform;
                ct.SetParent(parent, false);
                ct.localPosition = Vector3.zero;
                // Face the way the zone faces (toward your monsters), whichever way it was looking on the map:
                // take the model's rotation relative to its body's facing and re-apply it to the zone's facing.
                // That keeps any built-in tilt (some models are authored lying down) but fixes the heading.
                Vector3 bf = bodyForward;
                bf.y = 0f;
                if (bf.sqrMagnitude < 1e-4f) { bf = live.parent ? live.parent.forward : Vector3.forward; bf.y = 0f; }
                if (bf.sqrMagnitude < 1e-4f) bf = Vector3.forward;
                Quaternion relative = Quaternion.Inverse(Quaternion.LookRotation(bf.normalized, Vector3.up)) * live.rotation;
                ct.rotation = Quaternion.LookRotation(parent.forward, parent.up) * relative;
                Vector3 pl = parent.lossyScale, ll = live.lossyScale;
                ct.localScale = new Vector3(ll.x * f / Safe(pl.x), ll.y * f / Safe(pl.y), ll.z * f / Safe(pl.z));
                Object.Destroy(staging);
                staging = null;

                Vector3 pc = parent.InverseTransformPoint(ct.TransformPoint(centreLocal));
                Vector3 pb = parent.InverseTransformPoint(ct.TransformPoint(bottomLocal));
                ct.localPosition = -new Vector3(pc.x, pb.y, pc.z);

                foreach (SkinnedMeshRenderer smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
                foreach (Animator a in clone.GetComponentsInChildren<Animator>(true))
                {
                    a.applyRootMotion = false;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (AnimatorControllerParameter prm in a.parameters)
                        if (prm.type == AnimatorControllerParameterType.Bool && prm.name == "isGrounded") a.SetBool(prm.name, true);
                }
                DuelistPlugin.Log.LogInfo($"Live copy of {label}: size {b.size}, scale x{f:0.00}.");
                return true;
            }
            catch (Exception e)
            {
                if (staging) Object.Destroy(staging);
                DuelistPlugin.Log.LogWarning($"Live copy of {label} failed: {e.Message}");
                return false;
            }
        }

        private static float Safe(float v) => Mathf.Abs(v) < 1e-4f ? 1f : v;

        private static bool AddEnemyModel(BodyIndex index, Transform parent, float targetHeight, float maxWidth = 0f)
        {
            GameObject prefab = BodyCatalog.GetBodyPrefab(index);
            if (!prefab) { DuelistPlugin.Log.LogWarning($"No body prefab for {index}"); return false; }
            ModelLocator locator = prefab.GetComponent<ModelLocator>();
            if (!locator || !locator.modelTransform) { DuelistPlugin.Log.LogWarning($"{prefab.name} has no model"); return false; }
            GameObject src = locator.modelTransform.gameObject;
            if (CloneVisual(src, parent, targetHeight, prefab.name, maxWidth)) return true;
            return BakeStatue(src, parent, targetHeight, prefab.name, maxWidth);
        }

        // Components a model needs to look right: CharacterModel + ModelSkinController apply the skin's
        // materials at runtime (game 1.5 loads them async), ChildLocator is used by both. Everything
        // else (hurtboxes, aim/footstep scripts, colliders, physics) is gameplay and gets stripped.
        private static readonly HashSet<string> KeepTypes = new HashSet<string>
        {
            "CharacterModel", "ModelSkinController", "ChildLocator"
        };

        /// <summary>
        /// A living copy of a model: instantiated under an inactive parent, gameplay scripts, colliders
        /// and physics stripped, then switched on so its skin applies and its Animator plays idle.
        /// </summary>
        public static bool CloneVisual(GameObject source, Transform parent, float targetHeight, string label, float maxWidth = 0f)
        {
            GameObject staging = null;
            try
            {
                if (!source) return false;
                staging = new GameObject("DuelistCloneStaging");
                staging.SetActive(false);
                GameObject clone = Object.Instantiate(source, staging.transform, false);
                clone.name = "Clone_" + label;

                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
                        if (mb && !KeepTypes.Contains(mb.GetType().Name)) Object.DestroyImmediate(mb);
                    foreach (Joint j in clone.GetComponentsInChildren<Joint>(true)) if (j) Object.DestroyImmediate(j);
                    foreach (Collider c in clone.GetComponentsInChildren<Collider>(true)) if (c) Object.DestroyImmediate(c);
                    foreach (Rigidbody rb in clone.GetComponentsInChildren<Rigidbody>(true)) if (rb) Object.DestroyImmediate(rb);
                }

                // Renderers the skin will fill in: make sure they're on and have the model's default materials.
                CharacterModel cm = clone.GetComponent<CharacterModel>();
                if (cm && cm.baseRendererInfos != null)
                {
                    foreach (CharacterModel.RendererInfo ri in cm.baseRendererInfos)
                    {
                        if (!ri.renderer) continue;
                        ri.renderer.enabled = true;
                        if (ri.defaultMaterial) ri.renderer.sharedMaterial = ri.defaultMaterial;
                    }
                }

                Transform ct = clone.transform;
                ct.SetParent(parent, false);
                ct.localPosition = Vector3.zero;
                ct.localRotation = source.transform.localRotation;
                ct.localScale = Vector3.one;
                Object.Destroy(staging);
                staging = null;

                int count = 0;
                Bounds b = default(Bounds);
                foreach (Renderer r in clone.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!r.gameObject.activeInHierarchy) continue;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                    if (count == 0) b = r.bounds; else b.Encapsulate(r.bounds);
                    count++;
                }
                if (count == 0)
                {
                    DuelistPlugin.Log.LogWarning($"Clone of {label}: no renderers, falling back.");
                    Object.Destroy(clone);
                    return false;
                }

                foreach (Animator a in clone.GetComponentsInChildren<Animator>(true))
                {
                    a.applyRootMotion = false;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (AnimatorControllerParameter prm in a.parameters)
                        if (prm.type == AnimatorControllerParameterType.Bool && prm.name == "isGrounded") a.SetBool(prm.name, true);
                }

                // Fit it into its zone now, and keep re-fitting for a few seconds: skins and meshes load
                // asynchronously, so the first measurement is often wrong.
                ZoneFit fit = clone.AddComponent<ZoneFit>();
                fit.Height = targetHeight;
                fit.Width = maxWidth > 0f ? maxWidth : targetHeight;
                fit.Fit();

                DuelistPlugin.Log.LogInfo($"Clone of {label}: {count} renderers, skin={(clone.GetComponent<ModelSkinController>() ? "yes" : "no")}, scale {ct.localScale.x:0.00}.");
                return true;
            }
            catch (Exception e)
            {
                if (staging) Object.Destroy(staging);
                DuelistPlugin.Log.LogWarning($"Clone of {label} failed: {e.Message}");
                return false;
            }
        }

        public static bool BakeStatue(GameObject source, Transform parent, float targetHeight, string label, float maxWidth = 0f)
        {
            GameObject staging = null;
            try
            {
                if (!source) return false;
                staging = new GameObject("DuelistStaging");
                staging.SetActive(false);
                GameObject src = Object.Instantiate(source, staging.transform, false);
                Transform srcRoot = src.transform;
                srcRoot.localPosition = Vector3.zero;
                srcRoot.localRotation = Quaternion.identity;

                GameObject statue = new GameObject("Statue");
                statue.transform.SetParent(parent, false);

                int parts = 0;
                foreach (Renderer r in src.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!ActiveUpTo(r.transform, srcRoot)) continue;

                    Mesh mesh = null;
                    bool scaleBaked = false;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        if (!smr.sharedMesh) continue;
                        mesh = new Mesh();
                        smr.BakeMesh(mesh, true);
                        scaleBaked = true;
                    }
                    else if (r is MeshRenderer)
                    {
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        if (!mf || !mf.sharedMesh) continue;
                        mesh = mf.sharedMesh;
                    }
                    if (!mesh) continue;

                    GameObject part = new GameObject(r.name);
                    part.transform.SetParent(statue.transform, false);
                    part.transform.localPosition = srcRoot.InverseTransformPoint(r.transform.position);
                    part.transform.localRotation = Quaternion.Inverse(srcRoot.rotation) * r.transform.rotation;
                    part.transform.localScale = scaleBaked ? Vector3.one : r.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                    parts++;
                }

                Object.Destroy(staging);
                staging = null;
                if (parts == 0) { Object.Destroy(statue); return false; }

                ZoneFit fit = statue.AddComponent<ZoneFit>();
                fit.Height = targetHeight;
                fit.Width = maxWidth > 0f ? maxWidth : targetHeight;
                fit.Fit();
                return true;
            }
            catch (Exception e)
            {
                if (staging) Object.Destroy(staging);
                DuelistPlugin.Log.LogWarning($"Statue for {label} failed: {e.Message}");
                return false;
            }
        }

        private static bool ActiveUpTo(Transform t, Transform root)
        {
            for (Transform cur = t; cur && cur != root; cur = cur.parent)
                if (!cur.gameObject.activeSelf) return false;
            return true;
        }
    }

    /// <summary>
    /// Shrinks a model copied from the map so it fits inside its card zone (Width x Width footprint,
    /// Height tall), centred and standing on the card. Only the body counts toward its size: glow,
    /// flame and shadow effects are ignored, and effect meshes far bigger than the body are hidden.
    /// Re-fits for a few seconds because skins and meshes finish loading after the copy is made.
    /// </summary>
    public class ZoneFit : MonoBehaviour
    {
        public float Width = 2f;
        public float Height = 2.4f;
        private float _until;
        private float _next;

        private void OnEnable() => _until = Time.time + 3f;

        private void LateUpdate()
        {
            if (Time.time > _until) { enabled = false; return; }
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;
            Fit();
        }

        public static bool IsEffect(Renderer r)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return true;
            Material m = r.sharedMaterial;
            string sh = m && m.shader ? m.shader.name : "";
            return sh.Contains("FX") || sh.Contains("Cloud") || sh.Contains("Particle") || sh.Contains("Distortion") || sh.Contains("Sprite");
        }

        public void Fit()
        {
            Transform parent = transform.parent;
            if (!parent) return;
            Renderer[] all = GetComponentsInChildren<Renderer>(true);

            bool haveBody = false;
            Bounds body = default(Bounds);
            foreach (Renderer r in all)
            {
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || IsEffect(r)) continue;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                if (!haveBody) { body = r.bounds; haveBody = true; } else body.Encapsulate(r.bounds);
            }
            if (!haveBody)          // all effects (a wisp, say): measure them instead
            {
                foreach (Renderer r in all)
                {
                    if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!haveBody) { body = r.bounds; haveBody = true; } else body.Encapsulate(r.bounds);
                }
                if (!haveBody) return;
            }
            else
            {
                // effect meshes much bigger than the body (shadow discs, aura shells) would spill off the zone
                float bodySize = body.size.magnitude;
                foreach (Renderer r in all)
                    if (r && IsEffect(r) && !(r is ParticleSystemRenderer) && r.bounds.size.magnitude > bodySize * 1.6f)
                        r.forceRenderingOff = true;
            }

            // size in the parent's space
            Vector3 size = parent.InverseTransformVector(body.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            float foot = Mathf.Max(size.x, size.z);
            float f = Mathf.Min(foot > 0.01f ? Width / foot : 1f, size.y > 0.01f ? Height / size.y : 1f);
            if (float.IsNaN(f) || float.IsInfinity(f) || f <= 0f) return;
            if (Mathf.Abs(f - 1f) > 0.02f) transform.localScale *= f;
            else f = 1f;

            // centre it over the card and stand it on the surface
            Vector3 pivot = transform.localPosition;
            Vector3 c = parent.InverseTransformPoint(body.center);
            float bottom = parent.InverseTransformPoint(body.min).y;
            Vector3 nc = pivot + (c - pivot) * f;
            float nb = pivot.y + (bottom - pivot.y) * f;
            transform.localPosition = pivot - new Vector3(nc.x, nb, nc.z);
        }
    }

    /// <summary>Spins an object in place (The Director's crystal).</summary>
    public class DuelSpin : MonoBehaviour
    {
        public float Speed = 30f;
        private void Update() => transform.Rotate(Vector3.up, Speed * Time.deltaTime, Space.World);
    }
}

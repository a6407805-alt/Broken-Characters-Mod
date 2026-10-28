using System.Collections.Generic;
using System.Reflection;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Surveyor
{
    // v0.6.8 additions to the LiDAR: the Black Hole orb (M2 mode 2), the demon's blink tracking, and the close-range
    // chest X-ray (wireframe box + tier nameplate + what is inside). All drawn as overlay; no world material is touched.
    public partial class SvrLidar
    {
        // ================================================================ Black Hole (fired singularity orb)

        private bool _loggedFeatureError;
        private bool _bhActive, _bhAnchored;
        private Vector3 _bhPos, _bhVel;
        private float _bhUntil, _bhTick, _bhReadyAt;

        public float BlackHoleCooldownLeft { get { return Mathf.Max(0f, _bhReadyAt - Time.time); } }

        /// <summary>Fires the orb if it is off cooldown (authority only). Returns false when it can't fire.</summary>
        public bool TryFireSingularity(Ray aim, CharacterBody owner)
        {
            if (_bhActive || Time.time < _bhReadyAt || !owner) return false;
            _bhActive = true;
            _bhAnchored = false;
            _bhPos = aim.origin + aim.direction * 1.5f;
            _bhVel = aim.direction * 42f;
            _bhUntil = Time.time + 1.1f;                       // flight time before it anchors on its own (~45 m)
            _bhReadyAt = Time.time + SvrPlugin.BlackHoleCooldown.Value;
            Util.PlaySound("Play_MULT_m2_aim", owner.gameObject);
            return true;
        }

        private void BlackHoleTick(float dt)
        {
            if (!_bhActive || !_body) return;
            if (!_bhAnchored)
            {
                Vector3 step = _bhVel * dt;
                RaycastHit h;
                if (Physics.Raycast(_bhPos, _bhVel.normalized, out h, step.magnitude + 0.5f, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore)
                    && !(h.collider && h.collider.transform.IsChildOf(_body.gameObject.transform)))
                {
                    _bhPos = h.point - _bhVel.normalized * 0.8f;
                    AnchorBlackHole();
                }
                else
                {
                    _bhPos += step;
                    if (Time.time >= _bhUntil) AnchorBlackHole();
                }
                BlackHoleDots(false);
                return;
            }

            float r = SvrPlugin.BlackHoleRadius.Value;
            TeamIndex team = _body.teamComponent ? _body.teamComponent.teamIndex : TeamIndex.Player;

            // pull enemies (ground and flying) into the event horizon
            var bodies = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < bodies.Count; i++)
            {
                CharacterBody e = bodies[i];
                if (!e || e == _body || !e.healthComponent || !e.healthComponent.alive) continue;
                if (e.teamComponent && e.teamComponent.teamIndex == team) continue;
                Vector3 d = _bhPos - e.corePosition;
                float dist = d.magnitude;
                if (dist > r || dist < 0.4f) continue;
                float strength = Mathf.Lerp(26f, 8f, dist / r);
                if (e.characterMotor) e.characterMotor.velocity = Vector3.Lerp(e.characterMotor.velocity, d / dist * strength, 6f * dt);
                else if (e.rigidbody) e.rigidbody.velocity = Vector3.Lerp(e.rigidbody.velocity, d / dist * strength, 6f * dt);
            }

            // swallow enemy projectiles
            try
            {
                int n = Physics.OverlapSphereNonAlloc(_bhPos, r, _colHits, LayerIndex.projectile.mask);
                for (int i = 0; i < n; i++)
                {
                    Collider c = _colHits[i];
                    ProjectileController pc = c ? c.GetComponentInParent<ProjectileController>() : null;
                    if (!pc || pc.owner == _body.gameObject) continue;
                    Transform pt = pc.transform;
                    pt.position = Vector3.MoveTowards(pt.position, _bhPos, 30f * dt);
                    if ((pt.position - _bhPos).sqrMagnitude < 1.5f) Destroy(pc.gameObject);
                }
            }
            catch { }

            if (_demon != null && _demon.Active && (_demon.Pos - _bhPos).magnitude < r) _demon.PullToward(_bhPos, 7f, dt);

            _bhTick -= dt;
            if (_bhTick <= 0f)
            {
                _bhTick = 0.5f;
                FireBlast(_bhPos, 3.5f, SvrPlugin.BlackHoleTickDamage.Value, 0.2f, false, 0f);
            }

            BlackHoleDots(true);
            if (Time.time >= _bhUntil) ImplodeBlackHole();
        }

        private void AnchorBlackHole()
        {
            _bhAnchored = true;
            _bhUntil = Time.time + SvrPlugin.BlackHoleSeconds.Value;
            _bhTick = 0.25f;
            Util.PlaySound("Play_voidman_shift_pop", _body.gameObject);
        }

        private void ImplodeBlackHole()
        {
            _bhActive = false;
            float r = SvrPlugin.BlackHoleRadius.Value * 0.8f;
            FireBlast(_bhPos, r, SvrPlugin.BlackHoleDamage.Value, 1f, true, -1600f);
            try
            {
                GameObject fx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
                if (fx) EffectManager.SpawnEffect(fx, new EffectData { origin = _bhPos, scale = r }, true);
            }
            catch { }
            Util.PlaySound("Play_item_proc_shieldBreak", _body.gameObject);
            for (int i = 0; i < 420; i++)
                AddDot(_bhPos + Random.onUnitSphere * Random.Range(0.5f, r), new Color(0.75f, 0.45f, 1f, 1f), 1.4f, true);
            if (_demon != null && _demon.Active && (_demon.Pos - _bhPos).magnitude < r) _demon.Knock(_bhPos, 0f, 2f);
        }

        private void FireBlast(Vector3 at, float radius, float damageCoef, float proc, bool stun, float force)
        {
            if (!_body) return;
            try
            {
                new BlastAttack
                {
                    attacker = _body.gameObject,
                    inflictor = _body.gameObject,
                    teamIndex = TeamComponent.GetObjectTeam(_body.gameObject),
                    baseDamage = _body.damage * damageCoef,
                    baseForce = force,
                    position = at,
                    radius = radius,
                    falloffModel = BlastAttack.FalloffModel.Linear,
                    crit = _body.RollCrit(),
                    procCoefficient = proc,
                    damageType = stun ? DamageType.Stun1s : DamageType.Generic
                }.Fire();
            }
            catch { }
        }

        private void BlackHoleDots(bool anchored)
        {
            float now = Time.time;
            float r = anchored ? SvrPlugin.BlackHoleRadius.Value : 1.2f;
            int n = anchored ? 34 : 10;
            for (int i = 0; i < n; i++)
            {
                float prog = (now * 0.9f + i / (float)n) % 1f;                 // dots spiral inward
                float rad = Mathf.Lerp(r * 0.55f, 0.35f, prog);
                float ang = i * 2.39996f + now * 3.5f + prog * 7f;
                Vector3 p = _bhPos + new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang * 0.5f) * rad * 0.18f, Mathf.Sin(ang) * rad);
                AddDot(p, (i & 1) == 0 ? new Color(0.68f, 0.32f, 1f, 1f) : Color.white, 0.12f, true);
            }
            for (int i = 0; i < 6; i++)
                AddDot(_bhPos + Random.insideUnitSphere * 0.35f, new Color(0.05f, 0.02f, 0.1f, 1f), 0.08f, true);
        }

        // ================================================================ Shimmer Demon blink tracking

        private float _lastBlinkSeen = -10f;

        private void DemonBlinkFx()
        {
            if (_demon == null || !_demon.Active) return;
            float now = Time.time;

            // telegraph: the air tears around it, and a ghost of the destination flickers
            float w = _demon.BlinkWindup;
            if (w > 0f)
            {
                for (int i = 0; i < 10; i++)
                    AddDot(_demon.Center + Random.insideUnitSphere * (1.2f + 2f * w), new Color(0.85f, 0.3f, 1f, 1f), 0.12f, true);
                if (Random.value < w)
                    for (int i = 0; i < 6; i++)
                        AddDot(_demon.BlinkTarget + Vector3.up * Random.Range(0f, 2.6f) + Random.insideUnitSphere * 0.5f, new Color(1f, 0.4f, 1f, 0.8f), 0.1f, true);
            }

            if (_demon.LastBlinkAt > _lastBlinkSeen)
            {
                _lastBlinkSeen = _demon.LastBlinkAt;
                for (int i = 0; i < 160; i++)
                    AddDot(_demon.LastBlinkFrom + Vector3.up * 1.2f + Random.onUnitSphere * Random.Range(0.4f, 2.4f), new Color(0.9f, 0.35f, 1f, 1f), 0.9f, true);
            }

            // for 3 s after a blink, a ring + spire marks where it went (dots show through walls)
            float since = now - _demon.LastBlinkAt;
            if (since >= 0f && since < 3f)
            {
                Vector3 at = _demon.LastBlinkTo;
                float pulse = 1.2f + 0.6f * Mathf.Sin(now * 10f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * 0.3927f + now * 2f;
                    AddDot(at + new Vector3(Mathf.Cos(a) * pulse, 0.1f, Mathf.Sin(a) * pulse), new Color(1f, 0.3f, 0.95f, 1f), 0.1f, true);
                }
                for (int i = 0; i < 6; i++)
                    AddDot(at + Vector3.up * Random.Range(0f, 9f), new Color(1f, 0.55f, 1f, 0.9f), 0.1f, true);
            }
        }

        // ================================================================ close-range chest X-ray

        private GUIStyle _xrayStyle;
        private readonly Dictionary<int, Bounds> _xrayBounds = new Dictionary<int, Bounds>();
        private readonly Dictionary<int, float> _xrayBoundsAt = new Dictionary<int, float>();
        private static FieldInfo _fDropPickup;
        private static bool _fDropSearched;

        private void DrawChestXray(Camera cam, float scale)
        {
            if (Peeking || _peekK > 0.5f || _body == null) return;        // LiDAR mode only
            if (_xrayStyle == null)
            {
                _xrayStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            float range = SvrPlugin.XrayRange.Value;
            Vector3 me = _body.corePosition;
            Texture2D white = Texture2D.whiteTexture;

            for (int i = 0; i < _pois.Count; i++)
            {
                Poi p = _pois[i];
                if (!p.Tr || !p.Tr.gameObject.activeInHierarchy) continue;
                if (p.Kind != 0 && p.Kind != 2) continue;
                float dist = (p.Tr.position - me).magnitude;
                if (dist > range) continue;

                ChestBehavior cb = p.Tr.GetComponentInParent<ChestBehavior>();
                if (!cb) cb = p.Tr.GetComponentInChildren<ChestBehavior>();
                string lname = (cb ? cb.gameObject.name : p.Tr.gameObject.name).ToLowerInvariant();
                bool isBarrel = p.Kind == 2 || lname.Contains("barrel");
                if (!cb && !isBarrel && !lname.Contains("lockbox") && !lname.Contains("void")) continue;   // chests and crates only

                Bounds b = XrayBounds(cb ? cb.transform : p.Tr);
                float fade = Mathf.Clamp01((range - dist) / 3f);

                string contents; Color contentsCol; string tierWord;
                bool known = ReadContents(cb, out contents, out contentsCol, out tierWord);
                string tag = PlateName(lname, isBarrel, known ? tierWord : null, out Color tagCol);

                DrawWireBox(cam, b, new Color(tagCol.r, tagCol.g, tagCol.b, 0.9f * fade), 2f * scale);

                Vector3 top = b.center + Vector3.up * (b.extents.y + 0.45f);
                Vector3 sp = cam.WorldToScreenPoint(top);
                if (sp.z <= 0.3f) continue;
                float x = sp.x, y = Screen.height - sp.y;
                float w = 290f * scale, h1 = 22f * scale, h2 = 20f * scale;
                bool showContents = cb != null;
                float h = showContents ? h1 + h2 + 6f * scale : h1 + 4f * scale;
                GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.82f * fade);
                GUI.DrawTexture(new Rect(x - w * 0.5f, y - h, w, h), white);
                GUI.color = new Color(tagCol.r, tagCol.g, tagCol.b, fade);
                GUI.DrawTexture(new Rect(x - w * 0.5f, y - h, w, 2f * scale), white);
                _xrayStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(13f * scale));
                GUI.Label(new Rect(x - w * 0.5f, y - h + 2f * scale, w, h1), tag, _xrayStyle);
                if (showContents)
                {
                    _xrayStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(11.5f * scale));
                    GUI.color = known ? new Color(contentsCol.r, contentsCol.g, contentsCol.b, fade) : new Color(0.7f, 0.75f, 0.8f, 0.8f * fade);
                    GUI.Label(new Rect(x - w * 0.5f, y - h + h1 + 2f * scale, w, h2), known ? "CONTAINS: " + contents : "CONTAINS: ENCRYPTED (HOST ONLY)", _xrayStyle);
                }
            }
        }

        private Bounds XrayBounds(Transform root)
        {
            int id = root.GetInstanceID();
            float at;
            if (_xrayBounds.TryGetValue(id, out Bounds cached) && _xrayBoundsAt.TryGetValue(id, out at) && Time.time - at < 2f) return cached;
            Bounds b = new Bounds(root.position + Vector3.up * 0.6f, Vector3.one * 1.2f);
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (b.size.magnitude > 12f) b = new Bounds(root.position + Vector3.up * 0.6f, Vector3.one * 1.4f);   // guard against huge parent meshes
            _xrayBounds[id] = b;
            _xrayBoundsAt[id] = Time.time;
            return b;
        }

        private static string PlateName(string lname, bool isBarrel, string tierWord, out Color col)
        {
            string baseName; string defTier;
            if (lname.Contains("equipmentbarrel") || (isBarrel && lname.Contains("equipment"))) { baseName = "EQUIPMENT BARREL"; defTier = "ORANGE"; }
            else if (lname.Contains("void")) { baseName = "VOID CRADLE"; defTier = "PURPLE"; }
            else if (lname.Contains("lunar")) { baseName = "LUNAR POD"; defTier = "BLUE"; }
            else if (lname.Contains("goldchest") || lname.Contains("legendary")) { baseName = "LEGENDARY CHEST"; defTier = "RED"; }
            else if (lname.Contains("chest2") || lname.Contains("large")) { baseName = "LARGE CHEST"; defTier = "GREEN"; }
            else if (lname.Contains("lockbox")) { baseName = "RUSTY LOCKBOX"; defTier = "GREEN"; }
            else if (isBarrel) { baseName = "BARREL"; defTier = "GOLD"; }
            else { baseName = "CHEST"; defTier = "WHITE"; }
            string t = tierWord ?? defTier;
            col = TierColor(t);
            return baseName + " (" + t + ")";
        }

        private static Color TierColor(string t)
        {
            switch (t)
            {
                case "GREEN": return new Color(0.45f, 1f, 0.35f, 1f);
                case "RED": return new Color(1f, 0.3f, 0.25f, 1f);
                case "YELLOW": return new Color(1f, 0.9f, 0.25f, 1f);
                case "BLUE": return new Color(0.45f, 0.7f, 1f, 1f);
                case "PURPLE": return new Color(0.85f, 0.4f, 1f, 1f);
                case "ORANGE": return new Color(1f, 0.6f, 0.15f, 1f);
                case "GOLD": return new Color(1f, 0.82f, 0.25f, 1f);
                default: return new Color(0.95f, 0.95f, 0.95f, 1f);
            }
        }

        /// <summary>X-ray: what the chest rolled (ChestBehavior.dropPickup is only filled on the host).</summary>
        private static bool ReadContents(ChestBehavior cb, out string name, out Color col, out string tierWord)
        {
            name = null; col = Color.white; tierWord = null;
            if (!cb) return false;
            try
            {
                if (!_fDropSearched)
                {
                    _fDropSearched = true;
                    const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    _fDropPickup = typeof(ChestBehavior).GetField("dropPickup", any) ?? typeof(ChestBehavior).GetField("currentPickup", any);
                }
                if (_fDropPickup == null) return false;
                object v = _fDropPickup.GetValue(cb);
                PickupIndex idx = PickupIndex.none;
                if (v is PickupIndex pi) idx = pi;
                else if (v != null)
                {
                    FieldInfo inner = v.GetType().GetField("pickupIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (inner != null && inner.GetValue(v) is PickupIndex pi2) idx = pi2;
                }
                if (!idx.isValid) return false;
                PickupDef def = PickupCatalog.GetPickupDef(idx);
                if (def == null) return false;
                name = Language.GetString(def.nameToken).ToUpperInvariant();
                col = def.baseColor;
                if (def.equipmentIndex != EquipmentIndex.None)
                {
                    EquipmentDef ed = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                    tierWord = ed && ed.isLunar ? "BLUE" : "ORANGE";
                }
                else
                {
                    switch (def.itemTier)
                    {
                        case ItemTier.Tier1: tierWord = "WHITE"; break;
                        case ItemTier.Tier2: tierWord = "GREEN"; break;
                        case ItemTier.Tier3: tierWord = "RED"; break;
                        case ItemTier.Boss: tierWord = "YELLOW"; break;
                        case ItemTier.Lunar: tierWord = "BLUE"; break;
                        case ItemTier.VoidTier1:
                        case ItemTier.VoidTier2:
                        case ItemTier.VoidTier3:
                        case ItemTier.VoidBoss: tierWord = "PURPLE"; break;
                        default: tierWord = null; break;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private static readonly int[,] BoxEdges = { { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 }, { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
        private readonly Vector3[] _boxSp = new Vector3[8];

        private void DrawWireBox(Camera cam, Bounds b, Color c, float width)
        {
            Vector3 mn = b.min, mx = b.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                _boxSp[i] = cam.WorldToScreenPoint(corner);
            }
            GUI.color = c;
            for (int e = 0; e < 12; e++)
            {
                Vector3 a = _boxSp[BoxEdges[e, 0]], z = _boxSp[BoxEdges[e, 1]];
                if (a.z <= 0.3f || z.z <= 0.3f) continue;
                DrawScreenLine(new Vector2(a.x, Screen.height - a.y), new Vector2(z.x, Screen.height - z.y), width);
            }
        }

        private static void DrawScreenLine(Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), Texture2D.whiteTexture);
            GUI.matrix = m;
        }
    }
}
